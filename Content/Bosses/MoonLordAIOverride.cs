using Terraria.DataStructures;
using DZT.Common.Systems;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria;

namespace DZT.Content.Bosses
{
    internal class MoonLordLaserSpeed : GlobalProjectile
    {
        private const float LaserSpeedMultiplier = 1.2f; // 激光角速度倍率

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return;
            if (projectile.type == ProjectileID.PhantasmalDeathray)
            {
                projectile.ai[0] *= LaserSpeedMultiplier;
                projectile.netUpdate = true;
            }
        }
    }

    internal class MoonLordEyeTracking : GlobalProjectile
    {
        private const int ExtraTrackingTicks = 60; // 期望额外追踪的帧数 

        public override bool InstancePerEntity => true;
        private int _compensatedTicks;

        public override void AI(Projectile projectile)
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return;
            if (projectile.type != ProjectileID.PhantasmalEye) return;
            if (projectile.ai[0] >= 135f && projectile.ai[0] < 225f)
            {
                if (_compensatedTicks < ExtraTrackingTicks)
                {
                    projectile.ai[0] -= 1f;
                    _compensatedTicks++;
                }

                projectile.timeLeft += 1;
                projectile.netUpdate = true;
            }
        }
    }
}