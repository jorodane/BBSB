using System;

namespace BBSB.Runtime.UI
{
    /// <summary>Scale source units uniformly, independent of a pose's cropped rectangle.</summary>
    public readonly struct PlayerPoseGeometry
    {
        public double Width { get; }
        public double Height { get; }

        private PlayerPoseGeometry(double width, double height) { Width = width; Height = height; }

        public static PlayerPoseGeometry Calculate(double widthInUnits, double heightInUnits,
            double referenceHeightInUnits, double referenceDisplayHeight, double scale)
        {
            if (!Positive(widthInUnits) || !Positive(heightInUnits) || !Positive(referenceHeightInUnits) ||
                !Positive(referenceDisplayHeight) || !Positive(scale))
                throw new ArgumentOutOfRangeException(nameof(scale), "Sprite dimensions and scale must be finite and positive.");
            double unit = referenceDisplayHeight / referenceHeightInUnits * scale;
            return new PlayerPoseGeometry(widthInUnits * unit, heightInUnits * unit);
        }

        private static bool Positive(double value) => value > 0 && !double.IsInfinity(value) && !double.IsNaN(value);
    }
}
