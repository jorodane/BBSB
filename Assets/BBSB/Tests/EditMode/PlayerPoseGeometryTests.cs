using System;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class PlayerPoseGeometryTests
    {
        [Test]
        public void CroppedCrouchingAndLyingPosesKeepTheirSourceProportions()
        {
            var standing = PlayerPoseGeometry.Calculate(4, 6, 6, 480, 1);
            var crouching = PlayerPoseGeometry.Calculate(5, 3, 6, 480, 1);
            var lying = PlayerPoseGeometry.Calculate(8, 2, 6, 480, 1);
            Check.Equal(480d, standing.Height);
            Check.Equal(240d, crouching.Height);
            Check.Equal(160d, lying.Height);
            Check.Equal(640d, lying.Width); // A wide pose is not shrunk to a square cell.
            Check.Equal(80d, standing.Width / 4);
            Check.Equal(80d, crouching.Width / 5);
            Check.Equal(80d, lying.Width / 8);
        }

        [Test]
        public void CalibrationAndViewportChangesApplyOneUniformScale()
        {
            var before = PlayerPoseGeometry.Calculate(4, 6, 6, 480, 1);
            var calibrated = PlayerPoseGeometry.Calculate(4, 6, 6, 480, 1.25);
            var resized = PlayerPoseGeometry.Calculate(4, 6, 6, 240, 1.25);
            Check.Equal(before.Width * 1.25, calibrated.Width);
            Check.Equal(before.Height * 1.25, calibrated.Height);
            Check.Equal(calibrated.Width / 2, resized.Width);
            Check.Equal(calibrated.Height / 2, resized.Height);
        }

        [Test]
        public void InvalidDimensionsCannotCreateAnInfiniteOrNegativeImage()
        {
            foreach (double value in new[] { 0d, -1, double.NaN, double.PositiveInfinity })
            {
                bool threw = false;
                try { PlayerPoseGeometry.Calculate(4, 6, 6, 480, value); }
                catch (ArgumentOutOfRangeException) { threw = true; }
                Check.Equal(true, threw);
            }
        }
    }
}
