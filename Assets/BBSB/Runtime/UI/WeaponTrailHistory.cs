using System;

namespace BBSB.Runtime.UI
{
    public readonly struct WeaponTrailPoint
    {
        public double Time { get; }
        public BattlePathPoint Position { get; }
        public bool InFront { get; }
        public WeaponTrailPoint(double time, BattlePathPoint position, bool inFront)
        { Time = time; Position = position; InFront = inFront; }
    }

    public sealed class WeaponTrailHistory
    {
        public const int Capacity = 64;
        public const double Lifetime = .34;
        private readonly WeaponTrailPoint[] points = new WeaponTrailPoint[Capacity];
        public int Count { get; private set; }
        public WeaponTrailPoint this[int index] => index >= 0 && index < Count ? points[index] : throw new ArgumentOutOfRangeException(nameof(index));
        public void Clear() { Count = 0; }

        public void Add(double time, BattlePathPoint position, bool inFront)
        {
            if (double.IsNaN(time) || double.IsInfinity(time)) return;
            if (Count > 0 && (time < points[Count - 1].Time || time - points[Count - 1].Time > Lifetime)) Clear();
            int expired = 0;
            while (expired < Count && time - points[expired].Time >= Lifetime) expired++;
            if (expired > 0) { Array.Copy(points, expired, points, 0, Count - expired); Count -= expired; }
            var point = new WeaponTrailPoint(time, position, inFront);
            // Refreshing a paused frame or a resized viewport must not accumulate extra samples.
            if (Count > 0 && time == points[Count - 1].Time) { points[Count - 1] = point; return; }
            if (Count == Capacity) { Array.Copy(points, 1, points, 0, Capacity - 1); Count--; }
            points[Count++] = point;
        }
        public static double Opacity(double age) => Math.Max(0, Math.Min(1, 1 - age / Lifetime));
    }
}
