using System.Collections.Generic;
using Microsoft.Xna.Framework;
using DZT.Common.Systems;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria;
using System;

namespace DZT.Content.Bosses
{
    internal class EaterofWorldsAIOverride : GlobalNPC
    {
        private enum EoWState { NormalTracking, TransitionOut }
        // 【核心修改】八字阶段只有 Approaching 和 Looping
        private enum E8Phase { Approaching, Looping }

        private class BossFigureEightState
        {
            public bool Active;
            public E8Phase SubPhase = E8Phase.Approaching;
            public int E8Timer;              // Looping 阶段 t 进度（帧）
            public int ApproachingTimer;     // Approaching 阶段计时
            // 【新增】当前圈的几何
            public bool CurrentIsRight;      // true=右圈，false=左圈
            public float CurrentSpin;        // +1=顺时针（屏幕视觉），-1=逆时针
            public Vector2 Center;           // 玩家坐标记录点
            public Rectangle ArenaBox;
            public uint CooldownUntilTick;
            public int LoopsCompleted;
            public bool LowHpLocked;
        }

        private class HeadState
        {
            public EoWState State = EoWState.NormalTracking;
            public int StateTimer;
            public int SegmentShootTimer;
            public Vector2 LastNonZeroVelocity;
            public int VanillaGraceTimer;
            public int InitTimer;
            public Vector2 InertiaDirection;
            public bool InertiaInitialized;
            public uint RollCooldownUntilTick;
        }

        private class PathChain
        {
            public readonly Queue<Vector2> Points = new();
            public Vector2 LastRecordedPoint;
            public bool Initialized;

            public void Record(Vector2 position, float minSpacing)
            {
                if (!Initialized)
                {
                    Points.Enqueue(position);
                    LastRecordedPoint = position;
                    Initialized = true;
                    return;
                }
                if (Vector2.DistanceSquared(position, LastRecordedPoint) >= minSpacing * minSpacing)
                {
                    Points.Enqueue(position);
                    LastRecordedPoint = position;
                }
            }

            public Vector2? SampleFromTail(int indexFromTail)
            {
                if (Points.Count == 0) return null;
                if (indexFromTail < 0) indexFromTail = 0;
                if (indexFromTail >= Points.Count) return null;
                int skip = Points.Count - 1 - indexFromTail;
                int i = 0;
                foreach (var p in Points)
                {
                    if (i == skip) return p;
                    i++;
                }
                return null;
            }
        }

        private static readonly Dictionary<int, HeadState> _headStates = [];
        private static readonly Dictionary<int, PathChain> _pathChains = [];
        private static BossFigureEightState _bossE8State = new();

        private static long _initialMaxLife = 0;
        private static long _maxDamageRecorded = 0;

        // ==========================================
        // 可调参数
        // ==========================================
        private const float E8Radius = 400f;
        private const int E8LoopFrames = 200;
        private const int E8LoopCount = 3;
        private const int ApproachingFrames = 20;
        private const int TransitionFrames = 60;
        private const float ApproachStopDist = 80f;
        private const float E8MoveSpeed = 24f;

        private const float AbortFigureEightLifePercent = 0.6f;
        private const float FigureEightTriggerChance = 0.5f;
        private const uint FigureEightCooldownTicks = 300u;
        private const uint RollCooldownTicks = 60u;

        private const int VanillaGraceFrames = 50;
        private const int InertiaFrames = 30;
        private const int InitFrames = 60;
        private const float InitSpeed = 8f;
        private const float InertiaLerp = 0.1f;
        private const float TurnLerp = 0.08f;
        private const float TurnDoneDot = 0.95f;

        private const float PendingTriggerDist = 450f;
        private const float PendingTriggerDot = 0.85f;
        private const float PendingTriggerMinSpeed = 4f;

        private const float PathRecordSpacing = 24f;
        private const int PathMaxPoints = 400;
        private const float SegmentFollowSpeed = 0.35f;
        private const float SegmentMaxSpeed = 20f;

        private const float SegmentShootChanceMultiplier = 1.5f; // 体节弹幕概率提升倍率（+50%）
        private const float SpikyBallDamageMultiplier = 0.8f; // 尖钉球伤害倍率（-20%）
        private const float SegmentDamageReductionPerSegment = 0.01f; // 每体节减伤比例

        // ==========================================
        // 弧线参数化（统一"边 + 旋向"）
        // ==========================================
        private static Vector2 GetE8Position(bool isRight, float spin, float t)
        {
            Vector2 c = _bossE8State.Center;
            if (!isRight)
            {
                Vector2 centerL = c + new Vector2(-E8Radius, 0f);
                float angle = spin * t;
                return centerL + E8Radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }
            else
            {
                Vector2 centerR = c + new Vector2(E8Radius, 0f);
                float angle = MathHelper.Pi + spin * t;
                return centerR + E8Radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }
        }

        private static Vector2 GetE8Tangent(bool isRight, float spin, float t)
        {
            // dpos/dt 的方向（模长为 E8Radius）
            if (!isRight)
            {
                float angle = spin * t;
                return E8Radius * spin * new Vector2(-MathF.Sin(angle), MathF.Cos(angle));
            }
            else
            {
                float angle = MathHelper.Pi + spin * t;
                return E8Radius * spin * new Vector2(-MathF.Sin(angle), MathF.Cos(angle));
            }
        }

        // 【核心修改】触发时按规则 1/2 决定第一圈
        private static void DetermineInitialLoop(NPC npc, Player target)
        {
            float headX = npc.Center.X - target.Center.X;
            float vy = npc.velocity.Y;

            bool isRight;
            float spin;

            if (headX > 0f) // 从右边冲
            {
                isRight = false;                     // 左圈
                spin = vy < 0f ? -1f : 1f;           // 上=逆，下=顺
            }
            else // 从左边冲
            {
                isRight = true;                      // 右圈
                spin = vy < 0f ? 1f : -1f;           // 上=顺，下=逆
            }

            _bossE8State.CurrentIsRight = isRight;
            _bossE8State.CurrentSpin = spin;
        }

        // 【修正】每圈完成后，换到另一侧并使用相反 spin。
        // 这样既保证入口切线与当前 vy 匹配（无反转），
        // 也保证左右两圈视觉旋向相反（标准 8 字）。
        private static void DetermineNextLoop(NPC driver)
        {
            float vy = driver.velocity.Y;
            bool wasRight = _bossE8State.CurrentIsRight;
            bool isRight = !wasRight;  // 必须换边，才形成 8 字
            float spin;

            if (vy > 0f)
            {
                // 出口向下 → 入口必须向下，只有"左顺"或"右逆"满足
                spin = isRight ? -1f : +1f;
            }
            else
            {
                // 出口向上 → 入口必须向上，只有"左逆"或"右顺"满足
                spin = isRight ? +1f : -1f;
            }

            _bossE8State.CurrentIsRight = isRight;
            _bossE8State.CurrentSpin = spin;
        }

        public override bool PreAI(NPC npc)
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive)
                return true;

            if (npc.type == NPCID.EaterofWorldsHead)
            {
                if (npc.ai[0] == 0f) return true;

                if (!_headStates.TryGetValue(npc.whoAmI, out var headState))
                {
                    npc.TargetClosest(true);
                    headState = new HeadState { VanillaGraceTimer = 0 };
                    _headStates[npc.whoAmI] = headState;

                    int chainKey = npc.realLife >= 0 ? npc.realLife : npc.whoAmI;
                    if (!_pathChains.ContainsKey(chainKey))
                        _pathChains[chainKey] = new PathChain();
                }

                if (headState.VanillaGraceTimer < VanillaGraceFrames)
                {
                    headState.VanillaGraceTimer++;
                    return true;
                }

                RunCustomAI(npc, headState);

                int key = npc.realLife >= 0 ? npc.realLife : npc.whoAmI;
                if (_pathChains.TryGetValue(key, out var chain))
                {
                    chain.Record(npc.Center, PathRecordSpacing);
                    while (chain.Points.Count > PathMaxPoints)
                        chain.Points.Dequeue();
                }

                return false;
            }

            if (npc.type == NPCID.EaterofWorldsBody || npc.type == NPCID.EaterofWorldsTail)
                return DriveSegmentWithPath(npc);

            return true;
        }

        private static bool DriveSegmentWithPath(NPC npc)
        {
            int chainKey = npc.realLife >= 0 ? npc.realLife : -1;
            if (chainKey < 0 || !_pathChains.TryGetValue(chainKey, out var chain))
                return true;

            if (chain.Points.Count < 2) return true;

            int segmentIndex = GetSegmentIndex(npc);
            if (segmentIndex < 0) return true;

            int pointsPerSegment = Math.Max(1, (int)MathF.Round(npc.width / PathRecordSpacing));
            int sampleIndexFromTail = (segmentIndex + 1) * pointsPerSegment;

            Vector2? targetOpt = chain.SampleFromTail(sampleIndexFromTail);
            if (targetOpt is not Vector2 target) return true;

            Vector2 toTarget = target - npc.Center;
            float dist = toTarget.Length();

            if (dist > 1f)
            {
                Vector2 desiredVel = toTarget / dist * Math.Min(dist * SegmentFollowSpeed, SegmentMaxSpeed);
                npc.velocity = Vector2.Lerp(npc.velocity, desiredVel, 0.5f);
            }
            else
            {
                npc.velocity *= 0.8f;
            }

            npc.position += npc.velocity;
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
            return false;
        }

        private static int GetSegmentIndex(NPC segment)
        {
            int index = 0;
            int current = segment.whoAmI;
            int guard = 0;

            while (guard++ < 200)
            {
                int parent = (int)Main.npc[current].ai[1];
                if (parent < 0 || parent >= Main.maxNPCs || !Main.npc[parent].active)
                    return -1;

                NPC parentNpc = Main.npc[parent];
                if (parentNpc.type == NPCID.EaterofWorldsHead)
                    return index;

                current = parent;
                index++;
            }
            return -1;
        }

        private static void RunCustomAI(NPC npc, HeadState headState)
        {
            Player target = Main.player[npc.target];
            if (!target.active || target.dead)
            {
                npc.TargetClosest(true);
                target = Main.player[npc.target];
                if (!target.active || target.dead)
                {
                    npc.EncourageDespawn(300);
                    return;
                }
            }

            if (headState.State == EoWState.TransitionOut)
            {
                headState.StateTimer++;
                DoNormalTracking(npc, target, headState);
                float slowdown = MathHelper.Lerp(0.5f, 1f, headState.StateTimer / (float)TransitionFrames);
                npc.velocity *= slowdown;
                if (headState.StateTimer > TransitionFrames)
                {
                    headState.State = EoWState.NormalTracking;
                    headState.StateTimer = 0;
                }
                PostAIFinish(npc);
                return;
            }

            if (_bossE8State.Active)
            {
                if (GetBossLifePercent() < AbortFigureEightLifePercent)
                {
                    _bossE8State.Active = false;
                    _bossE8State.LowHpLocked = true;
                    _bossE8State.LoopsCompleted = 0;
                    _bossE8State.CooldownUntilTick = Main.GameUpdateCount + FigureEightCooldownTicks;

                    headState.State = EoWState.TransitionOut;
                    headState.StateTimer = 0;

                    float maxLenExit = (Main.expertMode ? 12f : 10f) * 1.5f;
                    if (npc.velocity.LengthSquared() > maxLenExit * maxLenExit)
                        npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * maxLenExit;

                    DoNormalTracking(npc, target, headState);
                    PostAIFinish(npc);
                    return;
                }

                ExecuteFigureEight(npc, target, headState);
                PostAIFinish(npc);
                return;
            }

            if (!_bossE8State.LowHpLocked &&
                GetBossLifePercent() < AbortFigureEightLifePercent)
            {
                _bossE8State.LowHpLocked = true;
            }

            DoNormalTracking(npc, target, headState);
            PostAIFinish(npc);
        }

        private static void PostAIFinish(NPC npc)
        {
            if (npc.velocity.LengthSquared() > 1f)
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            if (npc.alpha > 0)
            {
                if ((npc.position - npc.oldPosition).Length() > 2f)
                {
                    npc.alpha -= 42;
                    if (npc.alpha < 0) npc.alpha = 0;
                }
            }
        }

        // ==========================================
        // 【核心修改】八字执行：Approaching + Looping
        // ==========================================
        private static void ExecuteFigureEight(NPC npc, Player target, HeadState headState)
        {
            bool wasActive = _bossE8State.Active;
            bool isDriver = IsTimerDriver(npc);

            switch (_bossE8State.SubPhase)
            {
                case E8Phase.Approaching:
                    DoApproaching(npc, isDriver);
                    break;
                case E8Phase.Looping:
                    DoLooping(npc, isDriver);
                    break;
            }

            HandleBoxPunishment(target, _bossE8State);
            SpawnCorruptionMinions(npc, target, _bossE8State);
            DriveSegmentShooting(target, headState, predict: true);

            if (wasActive && !_bossE8State.Active)
            {
                headState.State = EoWState.TransitionOut;
                headState.StateTimer = 0;
            }

            if (npc.velocity.LengthSquared() > 4f)
                headState.LastNonZeroVelocity = npc.velocity;
        }

        // Approaching 阶段：速度平滑过渡到新圈起点切线方向
        private static void DoApproaching(NPC npc, bool isDriver)
        {
            Vector2 toCenter = _bossE8State.Center - npc.Center;
            float dist = toCenter.Length();
            Vector2 toCenterDir = dist > 1f ? toCenter / dist : Vector2.Zero;

            // 新圈在 t=0 处的切线方向（也就是"从 center 出发的方向"）
            Vector2 tangent = GetE8Tangent(_bossE8State.CurrentIsRight, _bossE8State.CurrentSpin, 0f);
            Vector2 tangentDir = tangent.SafeNormalize(Vector2.Zero);

            // 距离远时朝 center 冲，近了就切切线方向
            float blend = MathHelper.Clamp(dist / 300f, 0f, 1f);
            Vector2 targetDir = Vector2.Lerp(tangentDir, toCenterDir, blend).SafeNormalize(Vector2.Zero);
            Vector2 targetVel = targetDir * E8MoveSpeed;

            npc.velocity = Vector2.Lerp(npc.velocity, targetVel, 0.15f);

            if (isDriver)
            {
                _bossE8State.ApproachingTimer++;
                if (_bossE8State.ApproachingTimer >= ApproachingFrames || dist < ApproachStopDist)
                {
                    _bossE8State.SubPhase = E8Phase.Looping;
                    _bossE8State.E8Timer = 0;
                    _bossE8State.ApproachingTimer = 0;
                }
            }
        }

        // Looping 阶段：沿参数化圆弧运动
        private static void DoLooping(NPC npc, bool isDriver)
        {
            float paramSpeed = E8Radius * MathHelper.TwoPi / E8LoopFrames;
            float t = _bossE8State.E8Timer / (float)E8LoopFrames * MathHelper.TwoPi;

            Vector2 targetPos = GetE8Position(_bossE8State.CurrentIsRight, _bossE8State.CurrentSpin, t);
            Vector2 tangent = GetE8Tangent(_bossE8State.CurrentIsRight, _bossE8State.CurrentSpin, t);
            Vector2 tangentDir = tangent.SafeNormalize(Vector2.Zero);
            Vector2 tangentVel = tangentDir * paramSpeed;

            Vector2 radialCorrection = (targetPos - npc.Center) * 0.15f;
            Vector2 desiredVel = tangentVel + radialCorrection;

            npc.velocity = Vector2.Lerp(npc.velocity, desiredVel, 0.2f);

            if (isDriver)
            {
                _bossE8State.E8Timer++;
                if (_bossE8State.E8Timer >= E8LoopFrames)
                {
                    _bossE8State.LoopsCompleted++;
                    if (_bossE8State.LoopsCompleted >= E8LoopCount)
                    {
                        _bossE8State.Active = false;
                        _bossE8State.LoopsCompleted = 0;
                        _bossE8State.CooldownUntilTick = Main.GameUpdateCount + FigureEightCooldownTicks;
                    }
                    else
                    {
                        // 【核心修改】按规则 3 决定下一圈
                        DetermineNextLoop(npc);
                        _bossE8State.SubPhase = E8Phase.Approaching;
                        _bossE8State.ApproachingTimer = 0;
                    }
                }
            }
        }

        private static void DoNormalTracking(NPC npc, Player target, HeadState headState)
        {
            if (!headState.InertiaInitialized)
            {
                Vector2 displacement = npc.position - npc.oldPosition;
                if (displacement.LengthSquared() > 0.25f)
                    headState.InertiaDirection = displacement.SafeNormalize(Vector2.Zero);
                else if (npc.velocity.LengthSquared() > 0.1f)
                    headState.InertiaDirection = npc.velocity.SafeNormalize(Vector2.Zero);
                else
                    headState.InertiaDirection = (target.Center - npc.Center).SafeNormalize(Vector2.Zero);

                npc.velocity = Vector2.Zero;
                headState.InertiaInitialized = true;
            }

            if (headState.InitTimer < InitFrames)
            {
                headState.InitTimer++;
                npc.TargetClosest(true);
                Player t = Main.player[npc.target];
                if (!t.active || t.dead) return;

                if (headState.InitTimer <= InertiaFrames)
                {
                    float currentSpeed = npc.velocity.Length();
                    float newSpeed = MathHelper.Lerp(currentSpeed, InitSpeed, InertiaLerp);
                    npc.velocity = headState.InertiaDirection * newSpeed;
                }
                else
                {
                    Vector2 toTargetDir = (t.Center - npc.Center).SafeNormalize(Vector2.Zero);
                    Vector2 velDir = npc.velocity.SafeNormalize(headState.InertiaDirection);
                    float newAngle = Utils.AngleLerp(velDir.ToRotation(), toTargetDir.ToRotation(), TurnLerp);
                    npc.velocity = newAngle.ToRotationVector2() * InitSpeed;

                    if (Vector2.Dot(npc.velocity.SafeNormalize(Vector2.Zero), toTargetDir) > TurnDoneDot)
                        headState.InitTimer = InitFrames;
                }

                if (npc.velocity.LengthSquared() > 4f)
                    headState.LastNonZeroVelocity = npc.velocity;
                return;
            }

            float num52 = 10f;
            float num53 = 0.07f;
            if (Main.expertMode) { num52 = 12f; num53 = 0.15f; }
            if (Main.getGoodWorld) { num52 += 4f; num53 += 0.05f; }

            bool flag2 = true;
            Rectangle headRect = new((int)npc.position.X, (int)npc.position.Y, npc.width, npc.height);
            const int nearDist = 1000;
            for (int i = 0; i < 255; i++)
            {
                if (!Main.player[i].active) continue;
                Rectangle pRect = new(
                    (int)Main.player[i].position.X - nearDist,
                    (int)Main.player[i].position.Y - nearDist,
                    nearDist * 2, nearDist * 2);
                if (headRect.Intersects(pRect)) { flag2 = false; break; }
            }

            float num55 = target.Center.X - npc.Center.X;
            float num56 = target.Center.Y - npc.Center.Y;
            float num68 = (float)Math.Sqrt(num55 * num55 + num56 * num56);

            // 八字触发判定
            if (!_bossE8State.Active &&
                !_bossE8State.LowHpLocked &&
                Main.GameUpdateCount >= _bossE8State.CooldownUntilTick &&
                Main.GameUpdateCount >= headState.RollCooldownUntilTick)
            {
                if (GetBossLifePercent() >= AbortFigureEightLifePercent)
                {
                    float distToTarget = Vector2.Distance(npc.Center, target.Center);
                    Vector2 toTarget = (target.Center - npc.Center).SafeNormalize(Vector2.Zero);
                    Vector2 velDir = npc.velocity.SafeNormalize(Vector2.Zero);
                    float facing = Vector2.Dot(velDir, toTarget);
                    float speed = npc.velocity.Length();

                    if (distToTarget < PendingTriggerDist &&
                        facing > PendingTriggerDot &&
                        speed > PendingTriggerMinSpeed)
                    {
                        if (Main.rand.NextFloat() < FigureEightTriggerChance)
                        {
                            _bossE8State.Active = true;
                            _bossE8State.SubPhase = E8Phase.Approaching;
                            _bossE8State.E8Timer = 0;
                            _bossE8State.ApproachingTimer = 0;
                            _bossE8State.LoopsCompleted = 0;
                            _bossE8State.Center = target.Center;
                            _bossE8State.ArenaBox = BuildArenaBox(_bossE8State.Center);

                            // 【核心修改】按规则 1/2 决定第一圈
                            DetermineInitialLoop(npc, target);
                        }
                        else
                        {
                            headState.RollCooldownUntilTick = Main.GameUpdateCount + RollCooldownTicks;
                        }
                    }
                }
            }

            if (!flag2)
            {
                if (num68 > 0.001f)
                {
                    float num73 = num52 / num68;
                    num55 *= num73;
                    num56 *= num73;
                }

                if ((npc.velocity.X > 0f && num55 > 0f) || (npc.velocity.X < 0f && num55 < 0f) ||
                    (npc.velocity.Y > 0f && num56 > 0f) || (npc.velocity.Y < 0f && num56 < 0f))
                {
                    if (npc.velocity.X < num55) npc.velocity.X += num53;
                    else if (npc.velocity.X > num55) npc.velocity.X -= num53;

                    if (npc.velocity.Y < num56) npc.velocity.Y += num53;
                    else if (npc.velocity.Y > num56) npc.velocity.Y -= num53;
                }
                else if (Math.Abs(num55) > Math.Abs(num56))
                {
                    if (npc.velocity.X < num55) npc.velocity.X += num53 * 1.1f;
                    else if (npc.velocity.X > num55) npc.velocity.X -= num53 * 1.1f;

                    if (Math.Abs(npc.velocity.X) + Math.Abs(npc.velocity.Y) < num52 * 0.5f)
                    {
                        if (npc.velocity.Y > 0f) npc.velocity.Y += num53;
                        else npc.velocity.Y -= num53;
                    }
                }
                else
                {
                    if (npc.velocity.Y < num56) npc.velocity.Y += num53 * 1.1f;
                    else if (npc.velocity.Y > num56) npc.velocity.Y -= num53 * 1.1f;

                    if (Math.Abs(npc.velocity.X) + Math.Abs(npc.velocity.Y) < num52 * 0.5f)
                    {
                        if (npc.velocity.X > 0f) npc.velocity.X += num53;
                        else npc.velocity.X -= num53;
                    }
                }
            }
            else
            {
                if (num68 > 0.001f)
                {
                    num55 = num55 / num68 * num52;
                    num56 = num56 / num68 * num52;
                }
                npc.velocity = Vector2.Lerp(npc.velocity, new Vector2(num55, num56), 0.1f);
            }

            float maxLen = num52 * 1.5f;
            if (npc.velocity.LengthSquared() > maxLen * maxLen)
                npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * maxLen;

            if (npc.velocity.LengthSquared() > 4f)
                headState.LastNonZeroVelocity = npc.velocity;

            npc.rotation = (float)Math.Atan2(npc.velocity.Y, npc.velocity.X) + 1.57f;
            npc.TargetClosest(true);

            DriveSegmentShooting(target, headState, predict: false);
        }

        private static float GetBossLifePercent()
        {
            if (_initialMaxLife == 0)
            {
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC n = Main.npc[i];
                    if (!n.active) continue;
                    if (n.type < NPCID.EaterofWorldsHead || n.type > NPCID.EaterofWorldsTail) continue;
                    _initialMaxLife += n.lifeMax;
                }
            }
            if (_initialMaxLife == 0) return 1f;

            long currentTotalLife = 0;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (!n.active) continue;
                if (n.type < NPCID.EaterofWorldsHead || n.type > NPCID.EaterofWorldsTail) continue;
                currentTotalLife += n.life;
            }

            long currentDamage = _initialMaxLife - currentTotalLife;
            if (currentDamage < 0) currentDamage = 0;
            if (currentDamage > _maxDamageRecorded) _maxDamageRecorded = currentDamage;

            float lifePercent = 1f - (float)_maxDamageRecorded / _initialMaxLife;
            return MathHelper.Clamp(lifePercent, 0f, 1f);
        }

        private static Rectangle BuildArenaBox(Vector2 center)
        {
            const int halfW = 2000, halfH = 1800;
            return new Rectangle(
                (int)(center.X - halfW), (int)(center.Y - halfH),
                halfW * 2, halfH * 2);
        }

        private static bool IsTimerDriver(NPC self)
        {
            for (int i = 0; i < self.whoAmI; i++)
            {
                NPC n = Main.npc[i];
                if (!n.active || n.type != NPCID.EaterofWorldsHead) continue;

                if (_headStates.TryGetValue(i, out var s) &&
                    s.VanillaGraceTimer < VanillaGraceFrames)
                    continue;

                return false;
            }
            return true;
        }

        private static void HandleBoxPunishment(Player target, BossFigureEightState e8)
        {
            if (e8.ArenaBox == Rectangle.Empty) return;
            if (Main.GameUpdateCount % 30 != 0) return;
            if (!e8.ArenaBox.Contains(target.Center.ToPoint()))
                target.AddBuff(BuffID.CursedInferno, 600);
        }

        private static void SpawnCorruptionMinions(NPC npc, Player target, BossFigureEightState e8)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            if (e8.ArenaBox == Rectangle.Empty) return;
            if (!e8.ArenaBox.Contains(target.Center.ToPoint())) return;
            if (Main.GameUpdateCount % 180 != 0) return;

            Vector2 leftSpawn = new(e8.ArenaBox.Left + 80, e8.ArenaBox.Center.Y);
            NPC.NewNPC(npc.GetSource_FromAI(), (int)leftSpawn.X, (int)leftSpawn.Y, NPCID.Corruptor);
            Vector2 rightSpawn = new(e8.ArenaBox.Right - 80, e8.ArenaBox.Center.Y);
            NPC.NewNPC(npc.GetSource_FromAI(), (int)rightSpawn.X, (int)rightSpawn.Y, NPCID.CorruptSlime);
            Vector2 centerSpawn = e8.ArenaBox.Center.ToVector2() + Main.rand.NextVector2Circular(200, 150);
            NPC.NewNPC(npc.GetSource_FromAI(), (int)centerSpawn.X, (int)centerSpawn.Y, NPCID.EaterofSouls);
        }

        private static void DriveSegmentShooting(Player target, HeadState headState, bool predict)
        {
            headState.SegmentShootTimer++;
            int interval = predict ? 60 : 120;
            if (headState.SegmentShootTimer % interval != 0) return;
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            const float finalChance = 0.02f * SegmentShootChanceMultiplier;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC seg = Main.npc[i];
                if (!seg.active) continue;
                if (seg.type != NPCID.EaterofWorldsBody && seg.type != NPCID.EaterofWorldsTail) continue;
                if (Main.rand.NextFloat() >= finalChance) continue;

                Vector2 aimTarget = predict ? target.Center + target.velocity * 20f : target.Center;
                Vector2 dir = (aimTarget - seg.Center).SafeNormalize(Vector2.Zero);
                Projectile.NewProjectile(
                    seg.GetSource_FromAI(), seg.Center, dir * 6f,
                    ProjectileID.CursedFlameHostile,
                    (int)(seg.damage * 0.5f), 0f, Main.myPlayer);
            }
        }

        public override void OnKill(NPC npc)
        {
            if (npc.type == NPCID.EaterofWorldsHead)
            {
                _headStates.Remove(npc.whoAmI);
                _pathChains.Remove(npc.whoAmI);
            }
        }

        public override void PostAI(NPC npc)
        {
            if (Main.GameUpdateCount % 300 != 0) return;

            var toRemove = new List<int>();
            foreach (var key in _headStates.Keys)
            {
                if (key < 0 || key >= Main.maxNPCs ||
                    !Main.npc[key].active ||
                    Main.npc[key].type != NPCID.EaterofWorldsHead)
                {
                    toRemove.Add(key);
                }
            }
            foreach (var k in toRemove) _headStates.Remove(k);

            bool anyHeadAlive = false;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (n.active && n.type == NPCID.EaterofWorldsHead)
                {
                    anyHeadAlive = true;
                    break;
                }
            }
            if (!anyHeadAlive)
            {
                _bossE8State = new BossFigureEightState();
                _headStates.Clear();
                _pathChains.Clear();
                _initialMaxLife = 0;
                _maxDamageRecorded = 0;
            }
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return;
            if (npc.type < NPCID.EaterofWorldsHead || npc.type > NPCID.EaterofWorldsTail) return;
            if (projectile.type == ProjectileID.SpikyBall)
                modifiers.FinalDamage *= SpikyBallDamageMultiplier;
            ApplySegmentDamageReduction(ref modifiers);
        }

        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers)
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return;
            if (npc.type < NPCID.EaterofWorldsHead || npc.type > NPCID.EaterofWorldsTail) return;
            ApplySegmentDamageReduction(ref modifiers);
        }

        private static void ApplySegmentDamageReduction(ref NPC.HitModifiers modifiers)
        {
            int aliveSegments = CountAliveEaterOfWorldsSegments();
            float reduction = MathHelper.Clamp(aliveSegments * SegmentDamageReductionPerSegment, 0f, 1);
            modifiers.FinalDamage *= 1f - reduction;
        }

        private static int CountAliveEaterOfWorldsSegments()
        {
            int count = 0;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (n.active && n.type >= NPCID.EaterofWorldsHead && n.type <= NPCID.EaterofWorldsTail)
                    count++;
            }
            return count;
        }
    }
}