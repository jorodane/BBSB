using System;

namespace BBSB.Runtime.UI
{
    public static class PreparationLayout
    {
        public const double RowGap = 8;
        public const double MinimumRowHeight = 128;

        // Fill the viewport for one to three enemies; keep longer lists scrollable.
        public static double RowHeight(double viewportHeight, int monsters)
        {
            if (monsters <= 0) return 0;
            int visible = Math.Min(3, monsters);
            return Math.Max(MinimumRowHeight, (viewportHeight - RowGap * (visible - 1)) / visible);
        }

        public static double RhythmHeight(double rowHeight) => Math.Min(36, Math.Max(18, (rowHeight - 44) * .15));
        public static double WeaponHeight(double rowHeight) => Math.Min(180, Math.Max(0, rowHeight - 44 - 30 - RhythmHeight(rowHeight) - 14));
    }
}
