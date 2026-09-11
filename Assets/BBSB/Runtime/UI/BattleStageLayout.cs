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
            if (count == 1) { x = .77; depth = .015; }
            else if (count == 2) { x = .69 + index * .18; depth = index == 0 ? .09 : -.005; }
            else { x = .615 + index * .135; depth = .12 - index * .065; }
            double amount = Math.Max(0, Math.Min(1, advance));
            double stride = Math.Min(.105, Math.Max(0, x - playerX - .30));
            // Walk toward the player along the floor; retain separate depth lanes.
            depth *= 1 - amount * .2;
            return new BattleStagePose(x - stride * amount, playerY + depth, 1 - depth * .85);
        }

        public static double MonsterSize(int count, double width, double height, double depthScale) =>
            Math.Min(height * .44, width * (count == 1 ? .28 : count == 2 ? .23 : .19)) * depthScale;
    }
}
