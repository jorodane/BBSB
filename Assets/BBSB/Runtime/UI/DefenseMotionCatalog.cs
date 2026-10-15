using System;

namespace BBSB.Runtime.UI
{
    public readonly struct DefenseMotionClip
    {
        public int Directions { get; }
        public string Id { get; }
        public string Atlas { get; }
        public int Row { get; }
        public BattlePathPoint Contact { get; }
        public double StaffAngle { get; }
        public double SweepDegrees { get; }
        public DefenseMotionClip(int mask, string id, string atlas, int row, double x, double y, double angle, double sweep)
        { Directions = mask; Id = id; Atlas = atlas; Row = row; Contact = new BattlePathPoint(x, y); StaffAngle = angle; SweepDegrees = sweep; }
    }

    // Fifteen authored actions, not one catch-all diagonal. Body artwork and the
    // code-native staff fallback use the same direction identity. Times are visual
    // prototype values only; these clips never change combat judgment windows.
    public static class DefenseMotionCatalog
    {
        private static readonly DefenseMotionClip[] clips = {
            default,
            new DefenseMotionClip(1, "left", "single-ldu", 0, -.32, 0, 90, -90),
            new DefenseMotionClip(2, "down", "single-ldu", 1, 0, -.32, 0, -75),
            new DefenseMotionClip(3, "left-down", "adjacent-upper", 2, -.18, -.18, -45, -135),
            new DefenseMotionClip(4, "up", "single-ldu", 2, 0, .32, 0, 90),
            new DefenseMotionClip(5, "left-up", "adjacent-upper", 0, -.18, .18, 45, 135),
            new DefenseMotionClip(6, "down-up", "right-and-opposites", 2, 0, 0, 90, 180),
            new DefenseMotionClip(7, "left-down-up", "triples-and-full", 1, -.12, 0, 90, -240),
            new DefenseMotionClip(8, "right", "right-and-opposites", 0, .32, 0, 90, 90),
            new DefenseMotionClip(9, "left-right", "right-and-opposites", 1, 0, 0, 0, 180),
            new DefenseMotionClip(10, "down-right", "lower-and-triples", 0, .18, -.18, 45, 135),
            new DefenseMotionClip(11, "left-down-right", "triples-and-full", 0, 0, -.12, 0, -270),
            new DefenseMotionClip(12, "up-right", "adjacent-upper", 1, .18, .18, -45, -135),
            new DefenseMotionClip(13, "left-up-right", "lower-and-triples", 1, 0, .12, 0, 270),
            new DefenseMotionClip(14, "down-up-right", "lower-and-triples", 2, .12, 0, 90, 240),
            new DefenseMotionClip(15, "all", "triples-and-full", 2, 0, 0, -35, 360)
        };
        public static DefenseMotionClip For(int mask)
        {
            if (mask < 1 || mask > 15) throw new ArgumentOutOfRangeException(nameof(mask));
            return clips[mask];
        }
        public static int Part(PlayerDefenseFrame frame)
        {
            switch (frame.Phase)
            {
                case DefenseMotionPhase.Windup: return 0;
                case DefenseMotionPhase.Smear: return 1;
                case DefenseMotionPhase.Contact: return frame.Age < .205 ? 2 : 3;
                case DefenseMotionPhase.Recover: return frame.Releasing || frame.Age < .38 ? 4 : 5;
                case DefenseMotionPhase.Hold: return 5;
                default: return -1;
            }
        }
    }
}
