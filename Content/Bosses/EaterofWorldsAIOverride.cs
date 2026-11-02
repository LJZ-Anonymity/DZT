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
        private enum EoWState { NormalTracking, FigureEightSetup, FigureEightLoop, TransitionOut }
        private enum E8Phase { Approaching, LeftLoop, RightLoop }

        private class HeadState
        {
            public EoWState State = EoWState.NormalTracking;
            public int StateTimer;
            public Vector2 E8Center;
            public E8Phase E8SubPhase = E8Phase.Approaching;
            public int E8Timer;
            public Rectangle ArenaBox;
            public bool HasEnteredFigureEight;
            public int SegmentShootTimer;

            // 【修复】记录最近一次"有效"（长度足够大）的速度方向，用于贴身时维持滑行
            public Vector2 LastNonZeroVelocity;
        }

        private static readonly Dictionary<int, HeadState> _states = [];
        private static bool _figureEightTriggered;

        private const float E8Radius = 500f;
        private const int E8LoopFrames = 150;
        private const int SetupFrames = 60;
        private const int TransitionFrames = 120;
        private const float TriggerLifePercent = 0.8f;
        private const float ApproachStopDist = 80f;

        public override bool PreAI(NPC npc)
        {
            if (npc.type != NPCID.EaterofWorldsHead ||
                !ModContent.GetInstance<DZTWorldSystem>().DZTActive)
                return true;

            if (npc.ai[0] == 0f)
                return true;

            if (!_states.TryGetValue(npc.whoAmI, out var state))
            {
                state = new()
                {
                    HasEnteredFigureEight = _figureEightTriggered
                };
                _states[npc.whoAmI] = state;
            }

            RunCustomAI(npc, state);
            return false;
        }

        private static void RunCustomAI(NPC npc, HeadState state)
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

            switch (state.State)
            {
                case EoWState.NormalTracking:
                    DoNormalTracking(npc, target, state);   // 【修复】传入 state
                    if (ShouldEnterFigureEight(npc, state))
                    {
                        state.State = EoWState.FigureEightSetup;
                        state.StateTimer = 0;
                    }
                    break;

                case EoWState.FigureEightSetup:
                    state.StateTimer++;
                    if (state.StateTimer == 1)
                    {
                        state.E8Center = target.Center;
                        state.ArenaBox = BuildArenaBox(state.E8Center);
                        state.E8SubPhase = E8Phase.Approaching;
                        state.E8Timer = 0;
                    }
                    npc.velocity *= 0.95f;
                    if (state.StateTimer > SetupFrames)
                    {
                        state.State = EoWState.FigureEightLoop;
                        state.StateTimer = 0;
                    }
                    break;

                case EoWState.FigureEightLoop:
                    state.StateTimer++;
                    MoveInFigureEight(npc, state);
                    HandleBoxPunishment(npc, target, state);
                    SpawnCorruptionMinions(npc, target, state);
                    DriveSegmentShooting(npc, target, state, predict: true);

                    if (state.E8SubPhase == E8Phase.RightLoop && state.E8Timer >= E8LoopFrames)
                    {
                        state.State = EoWState.TransitionOut;
                        state.StateTimer = 0;
                        _figureEightTriggered = true;
                        state.HasEnteredFigureEight = true;
                    }
                    break;

                case EoWState.TransitionOut:
                    state.StateTimer++;
                    npc.velocity *= 0.98f;
                    if (state.StateTimer > TransitionFrames)
                    {
                        state.State = EoWState.NormalTracking;
                        state.StateTimer = 0;
                        state.HasEnteredFigureEight = true;
                    }
                    break;
            }

            if (state.State == EoWState.NormalTracking)
                DriveSegmentShooting(npc, target, state, predict: false);

            // 【修复】旋转只有在速度足够大时才更新，避免低速时方向抖动
            if (npc.velocity.LengthSquared() > 1f)
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            // 原版透明渐显
            if (npc.alpha > 0)
            {
                if ((npc.position - npc.oldPosition).Length() > 2f)
                {
                    npc.alpha -= 42;
                    if (npc.alpha < 0) npc.alpha = 0;
                }
            }
        }

        // 【修复】核心改动：接近玩家时减速 + 贴身时维持最小滑行
        private static void DoNormalTracking(NPC npc, Player target, HeadState state)
        {
            const float maxSpeed = 10f;
            const float accel = 0.1f;

            Vector2 toTarget = target.Center - npc.Center;
            float dist = toTarget.Length();

            // 距离 < 250 像素时逐渐减速；距离趋于 0 时 desired 长度趋于 0，
            // 避免"贴脸时 desired 方向疯狂翻转"
            float speedScale = MathHelper.Clamp(dist / 250f, 0f, 1f);
            Vector2 desired = toTarget.SafeNormalize(Vector2.Zero) * (maxSpeed * speedScale);

            npc.velocity = Vector2.Lerp(npc.velocity, desired, accel);

            if (npc.velocity.Length() > maxSpeed)
                npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;

            // 记录最后的有效速度方向
            if (npc.velocity.LengthSquared() > 4f)
                state.LastNonZeroVelocity = npc.velocity;

            // 贴身且速度很低时，用"上一次的有效方向"维持一个最小滑行速度，
            // 让头部从玩家身边"穿过去"而不是在玩家中心反复震荡
            if (dist < 100f && npc.velocity.LengthSquared() < 4f &&
                state.LastNonZeroVelocity.LengthSquared() > 0.01f)
            {
                npc.velocity = state.LastNonZeroVelocity.SafeNormalize(Vector2.Zero) * 5f;
            }

            npc.TargetClosest(true);
        }

        private static bool ShouldEnterFigureEight(NPC npc, HeadState state)
        {
            if (state.HasEnteredFigureEight) return false;
            if (_figureEightTriggered) return false;
            return npc.life < npc.lifeMax * TriggerLifePercent;
        }

        private static Rectangle BuildArenaBox(Vector2 center)
        {
            const int halfW = 2000, halfH = 1800;
            return new Rectangle(
                (int)(center.X - halfW), (int)(center.Y - halfH),
                halfW * 2, halfH * 2);
        }

        private static void MoveInFigureEight(NPC npc, HeadState state)
        {
            Vector2 targetPos = npc.Center;

            switch (state.E8SubPhase)
            {
                case E8Phase.Approaching:
                    targetPos = state.E8Center;
                    break;

                case E8Phase.LeftLoop:
                    {
                        float t = state.E8Timer / (float)E8LoopFrames * MathHelper.TwoPi;
                        Vector2 centerL = state.E8Center + new Vector2(-E8Radius, 0f);
                        targetPos = centerL + new Vector2(
                             E8Radius * (float)Math.Cos(t),
                            -E8Radius * (float)Math.Sin(t));
                        break;
                    }

                case E8Phase.RightLoop:
                    {
                        float t = state.E8Timer / (float)E8LoopFrames * MathHelper.TwoPi;
                        Vector2 centerR = state.E8Center + new Vector2(E8Radius, 0f);
                        targetPos = centerR + new Vector2(
                            -E8Radius * (float)Math.Cos(t),
                            -E8Radius * (float)Math.Sin(t));
                        break;
                    }
            }

            Vector2 dir = (targetPos - npc.Center).SafeNormalize(Vector2.Zero);
            npc.velocity = Vector2.Lerp(npc.velocity, dir * 12f, 0.08f);

            // 记录有效方向（虽然八字阶段速度通常 > 1，但保持一致）
            if (npc.velocity.LengthSquared() > 4f)
                state.LastNonZeroVelocity = npc.velocity;

            state.E8Timer++;

            switch (state.E8SubPhase)
            {
                case E8Phase.Approaching:
                    if (Vector2.Distance(npc.Center, state.E8Center) < ApproachStopDist)
                    {
                        state.E8SubPhase = E8Phase.LeftLoop;
                        state.E8Timer = 0;
                    }
                    break;

                case E8Phase.LeftLoop:
                    if (state.E8Timer >= E8LoopFrames)
                    {
                        state.E8SubPhase = E8Phase.RightLoop;
                        state.E8Timer = 0;
                    }
                    break;

                case E8Phase.RightLoop:
                    break;
            }
        }

        private static void HandleBoxPunishment(NPC npc, Player target, HeadState state)
        {
            if (state.ArenaBox == Rectangle.Empty) return;
            if (!state.ArenaBox.Contains(target.Center.ToPoint()) && state.StateTimer % 30 == 0)
                target.AddBuff(BuffID.CursedInferno, 600);
        }

        private static void SpawnCorruptionMinions(NPC npc, Player target, HeadState state)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            if (state.ArenaBox == Rectangle.Empty) return;
            if (!state.ArenaBox.Contains(target.Center.ToPoint())) return;
            if (state.StateTimer % 180 != 0) return;

            Vector2 leftSpawn = new(state.ArenaBox.Left + 80, state.ArenaBox.Center.Y);
            NPC.NewNPC(npc.GetSource_FromAI(), (int)leftSpawn.X, (int)leftSpawn.Y, NPCID.Corruptor);

            Vector2 rightSpawn = new(state.ArenaBox.Right - 80, state.ArenaBox.Center.Y);
            NPC.NewNPC(npc.GetSource_FromAI(), (int)rightSpawn.X, (int)rightSpawn.Y, NPCID.CorruptSlime);

            Vector2 centerSpawn = state.ArenaBox.Center.ToVector2() + Main.rand.NextVector2Circular(200, 150);
            NPC.NewNPC(npc.GetSource_FromAI(), (int)centerSpawn.X, (int)centerSpawn.Y, NPCID.EaterofSouls);
        }

        private static void DriveSegmentShooting(NPC npc, Player target, HeadState state, bool predict)
        {
            state.SegmentShootTimer++;
            int interval = predict ? 60 : 120;
            if (state.SegmentShootTimer % interval != 0) return;
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            const float finalChance = 0.02f * 1.5f;

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
                _states.Remove(npc.whoAmI);
        }

        public override void PostAI(NPC npc)
        {
            if (Main.GameUpdateCount % 300 != 0) return;

            var toRemove = new List<int>();
            foreach (var key in _states.Keys)
            {
                if (key < 0 || key >= Main.maxNPCs ||
                    !Main.npc[key].active ||
                    Main.npc[key].type != NPCID.EaterofWorldsHead)
                {
                    toRemove.Add(key);
                }
            }
            foreach (var k in toRemove) _states.Remove(k);

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
                _figureEightTriggered = false;
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return;
            if (npc.type < NPCID.EaterofWorldsHead || npc.type > NPCID.EaterofWorldsTail) return;

            if (projectile.type == ProjectileID.SpikyBall)
                modifiers.FinalDamage *= 0.8f;

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
            float reduction = MathHelper.Clamp(aliveSegments * 0.01f, 0f, 0.6f);
            modifiers.FinalDamage *= 1f - reduction;
        }

        private static bool IsEaterOfWorldsAlive()
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (n.active && n.type >= NPCID.EaterofWorldsHead && n.type <= NPCID.EaterofWorldsTail)
                    return true;
            }
            return false;
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