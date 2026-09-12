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
            if (count == 1) { x = .83; depth = .015; }
            else if (count == 2) { x = .79 + index * .12; depth = index == 0 ? .09 : -.005; }
            else { x = .77 + index * .08; depth = .12 - index * .065; }
            double amount = Math.Max(0, Math.Min(1, advance));
            double stride = Math.Min(.055, Math.Max(0, x - playerX - .30));
            // Walk toward the player along the floor; retain separate depth lanes.
            depth *= 1 - amount * .2;
            return new BattleStagePose(x - stride * amount, playerY + depth, 1 - depth * .85);
        }

        public static double MonsterSize(int count, double width, double height, double depthScale) =>
            Math.Min(height * .44, width * (count == 1 ? .28 : count == 2 ? .23 : .19)) * depthScale * .55;
    }
}
