using System;

namespace BBSB.Runtime.UI
{
    public static class WeaponSocketPulse
    {
        public const double Duration = .55;
        // Song time is supplied by the round, so pausing also freezes every light.
        public static double Sample(double age)
        {
            if (double.IsNaN(age) || age < 0 || age >= Duration) return 0;
            return Math.Pow(1 - age / Duration, 2);
        }
    }
}
