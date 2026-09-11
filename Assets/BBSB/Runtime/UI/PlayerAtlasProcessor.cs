using System;

namespace BBSB.Runtime.UI
{
    /// <summary>
    /// Import-only pose alignment using the supplied alpha, without background removal or color
    /// correction. Wide fallen poses stay separate. RGBA rows are bottom-to-top, as in Unity.
    /// </summary>
    public static class PlayerAtlasProcessor
    {
        private sealed class Figure
        {
            public int Label, Count, MinX, MinY, MaxX, MaxY;
            public int Width => MaxX - MinX + 1;
            public int Height => MaxY - MinY + 1;
        }

        public static byte[] Prepare(byte[] rgba, int width, int height, int columns, int rows)
        {
            if (rgba == null || width <= 0 || height <= 0 || rgba.Length != width * height * 4 || columns <= 0 || rows <= 0)
                throw new ArgumentException("Invalid player atlas dimensions.");
            var source = rgba;
            bool hasTransparentPixels = false;
            for (int i = 3; i < source.Length; i += 4)
                if (source[i] < 24) { hasTransparentPixels = true; break; }
            // An opaque sheet has no alpha silhouette to align. Keep it unchanged until the
            // artist supplies a transparent PNG; a visible background is part of that source.
            if (!hasTransparentPixels) return source;
            var labels = new int[width * height]; var queue = new int[labels.Length];
            var figures = new Figure[columns * rows]; int nextLabel = 0;
            double cellWidth = (double)width / columns, cellHeight = (double)height / rows;
            for (int p = 0; p < labels.Length; p++)
            {
                if (labels[p] != 0 || source[p * 4 + 3] < 24) continue;
                var f = new Figure { Label = ++nextLabel, MinX = width, MinY = height };
                int head = 0, tail = 0; labels[p] = f.Label; queue[tail++] = p;
                while (head < tail)
                {
                    int pixel = queue[head++], x = pixel % width, y = pixel / width;
                    f.MinX = Math.Min(f.MinX, x); f.MaxX = Math.Max(f.MaxX, x);
                    f.MinY = Math.Min(f.MinY, y); f.MaxY = Math.Max(f.MaxY, y); f.Count++;
                    if (x > 0) Visit(pixel - 1);
                    if (x + 1 < width) Visit(pixel + 1);
                    if (y > 0) Visit(pixel - width);
                    if (y + 1 < height) Visit(pixel + width);

                    void Visit(int at)
                    {
                        if (labels[at] != 0 || source[at * 4 + 3] < 24) return;
                        labels[at] = f.Label; queue[tail++] = at;
                    }
                }
                if (f.Count < Math.Max(64, cellWidth * cellHeight * .003)) continue;
                int column = Math.Min(columns - 1, (int)((f.MinX + f.MaxX) * .5 / cellWidth));
                int row = Math.Min(rows - 1, (int)((f.MinY + f.MaxY) * .5 / cellHeight));
                int index = (rows - row - 1) * columns + column;
                if (figures[index] != null)
                    throw new InvalidOperationException("More than one disconnected figure in player atlas cell " + index);
                figures[index] = f;
            }
            double scale = 1;
            for (int i = 0; i < figures.Length; i++)
            {
                var f = figures[i];
                if (f == null) throw new InvalidOperationException("Missing figure in player atlas cell " + i);
                // Use ONE scale for the whole sheet; sitting and lying must not grow to standing height.
                scale = Math.Min(scale, Math.Min(cellWidth * .86 / f.Width, cellHeight * .86 / f.Height));
            }
            var output = new byte[source.Length];
            for (int index = 0; index < figures.Length; index++)
            {
                var f = figures[index]; int column = index % columns, row = rows - index / columns - 1;
                double left = (column + .5) * cellWidth - f.Width * scale * .5;
                double bottom = (row + .08) * cellHeight;
                int x0 = Math.Max(0, (int)Math.Floor(left)), x1 = Math.Min(width, (int)Math.Ceiling(left + f.Width * scale));
                int y0 = Math.Max(0, (int)Math.Floor(bottom)), y1 = Math.Min(height, (int)Math.Ceiling(bottom + f.Height * scale));
                for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    double sx = f.MinX + (x + .5 - left) / scale - .5;
                    double sy = f.MinY + (y + .5 - bottom) / scale - .5;
                    int ix = (int)Math.Floor(sx), iy = (int)Math.Floor(sy);
                    double fx = sx - ix, fy = sy - iy, red = 0, green = 0, blue = 0, alpha = 0;
                    Sample(ix, iy, (1 - fx) * (1 - fy)); Sample(ix + 1, iy, fx * (1 - fy));
                    Sample(ix, iy + 1, (1 - fx) * fy); Sample(ix + 1, iy + 1, fx * fy);
                    if (alpha <= 0) continue;
                    int offset = (y * width + x) * 4;
                    output[offset] = Byte(red / alpha); output[offset + 1] = Byte(green / alpha);
                    output[offset + 2] = Byte(blue / alpha); output[offset + 3] = Byte(alpha * 255);

                    void Sample(int px, int py, double weight)
                    {
                        if (px < 0 || py < 0 || px >= width || py >= height) return;
                        int pixel = py * width + px, at = pixel * 4;
                        if (labels[pixel] != f.Label && !(labels[pixel] == 0 && source[at + 3] < 24)) return;
                        double a = source[at + 3] / 255.0 * weight;
                        alpha += a; red += source[at] * a; green += source[at + 1] * a; blue += source[at + 2] * a;
                    }
                }
            }
            return output;
        }

        private static byte Byte(double value) => (byte)Math.Max(0, Math.Min(255, Math.Round(value)));
    }
}
