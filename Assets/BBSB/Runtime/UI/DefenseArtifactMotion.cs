using System;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    // Player-height units. Rendering and projectile endpoints share this geometry;
    // none of these poses changes a timing window or a blocked damage calculation.
    public static class DefenseArtifactMotion
    {
        public const float GripX = .52f, GripY = .43f;
        public static BattlePathPoint Contact(int direction)
        {
            switch (direction)
            {
                case 0: return new BattlePathPoint(-.32, 0);
                case 1: return new BattlePathPoint(0, -.32);
                case 2: return new BattlePathPoint(0, .32);
                case 3: return new BattlePathPoint(.32, 0);
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }
        public static int Mask(FiveLaneBattle battle)
        {
            if (!battle.SeparateHands) return 0;
            int mask = 0;
            for (int d = 0; d < 4; d++)
            {
                var lane = battle.LaneAt(4 + d);
                if (lane != null && (FiveLaneArtTimeline.Guarding(lane) || (!lane.Phrase.ReleaseEndsPhrase || lane.Phrase.ParriesOnKeyUp) &&
                    lane.LastGrade != RhythmGrade.Miss && FiveLaneArtTimeline.Recent(battle.Beat, lane.LastJudgedBeat, .3)))
                    mask |= 1 << d;
            }
            return mask;
        }
        public static WeaponMotionFrame Sample(FiveLaneBattle battle)
        {
            return PlayerDefenseMotion.Staff(PlayerDefenseMotion.Sample(battle));
        }
        public static BattlePathPoint Projectile(BattlePathPoint source, BattlePathPoint body,
            double height, int direction, double progress)
        {
            double t = Math.Max(0, Math.Min(1, progress)), u = 1 - t;
            var contact = Contact(direction);
            double x = body.X + contact.X * height, y = body.Y + contact.Y * height;
            double cx = x + contact.X * height * 2.3, cy = y + contact.Y * height * 2.3;
            return new BattlePathPoint(u * u * source.X + 2 * u * t * cx + t * t * x,
                u * u * source.Y + 2 * u * t * cy + t * t * y);
        }
    }
}
