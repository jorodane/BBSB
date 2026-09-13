using System;

namespace BBSB.Runtime.UI
{
    public readonly struct BattlePathPoint
    {
        public double X { get; }
        public double Y { get; }
        public BattlePathPoint(double x, double y) { X = x; Y = y; }
    }

    /// <summary>Incoming attacks cross the middle; outgoing weapons take the upper arc.</summary>
    public static class BattleTrajectory
    {
        public const double CounterArrival = .65;

        public static BattlePathPoint Incoming(double fromX, double fromY, double toX, double toY, double progress)
        {
            double t = Clamp(progress), u = 1 - t;
            double controlY = Math.Max(fromY, toY) + .16;
            return new BattlePathPoint(fromX + (toX - fromX) * t,
                u * u * fromY + 2 * u * t * controlY + t * t * toY);
        }

        public static BattlePathPoint Counter(double fromX, double fromY, double toX, double toY, double progress)
        {
            double t = Clamp(progress), u = 1 - t;
            double controlY = Math.Max(.8, Math.Max(fromY, toY) + .3);
            return new BattlePathPoint(fromX + (toX - fromX) * t,
                u * u * u * fromY + 3 * u * t * controlY + t * t * t * toY);
        }

        public static BattlePathPoint MissLanding(BattlePathPoint contact, double groundY,
            double bodyHeight, double pivotY, double playerHeight, double progress)
        {
            double t = Clamp(progress);
            return new BattlePathPoint(contact.X - playerHeight * .12 * t,
                contact.Y + (groundY + bodyHeight * pivotY - contact.Y) * t * t);
        }

        private static double Clamp(double value) => Math.Max(0, Math.Min(1, value));
    }
}
