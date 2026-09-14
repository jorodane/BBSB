using System;

namespace BBSB.Runtime.UI
{
    public readonly struct BattleStagePose
    {
        public double X { get; }
        public double Y { get; }
        public double Scale { get; }
        public BattleStagePose(double x, double y, double scale) { X = x; Y = y; Scale = scale; }
    }

    /// <summary>Fixed shallow side view. Coordinates describe feet on the shared floor.</summary>
    public static class BattleStageLayout
    {
        public static BattleStagePose Monster(int index, int count, double playerX, double playerY, double advance)
        {
            if (count < 1 || count > 3 || index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            double x, depth;
            if (count == 1) { x = .80; depth = .015; }
            else if (count == 2) { x = .73 + index * .13; depth = index == 0 ? .045 : -.005; }
            else { x = .64 + index * .115; depth = .065 - index * .04; }
            double amount = Math.Max(0, Math.Min(1, advance));
            double stride = Math.Min(.055, Math.Max(0, x - playerX - .30));
            // Walk toward the player along the floor; retain separate depth lanes.
            depth *= 1 - amount * .2;
            return new BattleStagePose(x - stride * amount, playerY + depth, 1 - depth * .85);
        }

        public static double MonsterSize(int count, double width, double height, double depthScale, double playerHeight = 0)
        {
            double reference = playerHeight > 0 ? playerHeight : Math.Min(width * .4, height * .7) * .5;
            return Math.Min(reference * 1.05, Math.Min(height * .4, width * .24)) * depthScale;
        }
    }
}
