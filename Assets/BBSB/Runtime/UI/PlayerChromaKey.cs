using System;

namespace BBSB.Runtime.UI
{
    /// <summary>Import-time green-screen matte; no runtime shader or per-frame pixel processing.</summary>
    public static class PlayerChromaKey
    {
        public static void Composite(byte r, byte g, byte b, byte a,
            out byte red, out byte green, out byte blue, out byte alpha)
        {
            int foregroundGreen = Math.Max(r, b);
            double coverage = 1 - Math.Min(1, Math.Max(0, g - foregroundGreen) / 235.0);
            alpha = (byte)Math.Round(a * coverage);
            if (alpha == 0) { red = green = blue = 0; return; }
            // Remove spill and undo the green background contribution along antialiased edges.
            red = (byte)Math.Min(255, Math.Round(r / coverage));
            blue = (byte)Math.Min(255, Math.Round(b / coverage));
            green = (byte)Math.Min(255, Math.Round(Math.Min(g, foregroundGreen) / coverage));
        }
    }
}
