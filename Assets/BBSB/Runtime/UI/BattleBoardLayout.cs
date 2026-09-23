using System;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    // One projection shared by the note mesh, receptors and touch hit testing.
    // Coordinates are normalized to the full battle Canvas, measured from bottom left.
    public static class BattleBoardLayout
    {
        public const double NearY = .065, FarY = .42, HeaderTop = .465;
        public static int Count(InputExtensions extensions) => 5 +
            ((extensions & InputExtensions.Left) != 0 ? 1 : 0) + ((extensions & InputExtensions.Right) != 0 ? 1 : 0);
        public static double Width(double distance) => .86 + (.53 - .86) * Clamp(distance);
        public static double Y(double distance) => NearY + (FarY - NearY) * Clamp(distance);
        public static double CellWidth(double distance, InputExtensions extensions) => Width(distance) / Count(extensions);
        public static double X(int slot, double distance, InputExtensions extensions)
        {
            int position = BattleInputLayout.Position(slot) + ((extensions & InputExtensions.Left) != 0 ? 1 : 0);
            return .5 + ((position + .5) / Count(extensions) - .5) * Width(distance);
        }
        public static int HitSlot(double x, double y, InputExtensions extensions)
        {
            if (y < .025 || y > HeaderTop) return -1;
            double distance = Clamp((y - NearY) / (FarY - NearY));
            double width = Width(distance), start = .5 - width * .5;
            if (x < start || x >= start + width) return -1;
            int position = (int)Math.Floor((x - start) / width * Count(extensions));
            return BattleInputLayout.SlotAtPosition(position - ((extensions & InputExtensions.Left) != 0 ? 1 : 0));
        }
        public static bool IsOffbeat(double beat) => Math.Abs(beat - Math.Round(beat)) > .001;
        private static double Clamp(double value) => Math.Max(0, Math.Min(1, value));
    }
}
