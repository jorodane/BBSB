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
                if (lane != null && (FiveLaneArtTimeline.Guarding(lane) ||
                    lane.LastGrade != RhythmGrade.Miss && FiveLaneArtTimeline.Recent(battle.Beat, lane.LastJudgedBeat, .3)))
                    mask |= 1 << d;
            }
            return mask;
        }
        public static WeaponMotionFrame Sample(FiveLaneBattle battle)
        {
            int mask = Mask(battle), count = 0;
            double latest = double.NegativeInfinity;
            for (int d = 0; d < 4; d++) if ((mask & 1 << d) != 0)
            {
                count++;
                var lane = battle.LaneAt(4 + d);
                latest = Math.Max(latest, Math.Max(lane.InvokedAtBeat, lane.LastJudgedBeat));
            }
            if (count == 0) return new WeaponMotionFrame(new BattlePathPoint(.06, -.08), 52, 1);
            double age = Math.Max(0, battle.Beat - latest);
            double stroke = Math.Sin(Math.Min(1, age / .3) * Math.PI) * 13;
            if (mask == 9) return new WeaponMotionFrame(new BattlePathPoint(0, 0), stroke, 1.12);
            if (mask == 6) return new WeaponMotionFrame(new BattlePathPoint(0, 0), 90 + stroke, 1.12);
            if (count == 1)
            {
                int d = mask == 1 ? 0 : mask == 2 ? 1 : mask == 4 ? 2 : 3;
                return new WeaponMotionFrame(Contact(d), (d == 0 || d == 3 ? 90 : 0) + stroke, 1);
            }
            // Adjacent pairs and three/four-way chords sweep one double-ended staff.
            // Both ends leave a trail; independent contacts remain visible throughout.
            return new WeaponMotionFrame(new BattlePathPoint(0, 0), -55 + age * 540, 1.12);
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
