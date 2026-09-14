using System;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class PreparationLayoutTests
    {
        [Test]
        public void OneToThreeMonstersFillTheAvailableHeightWithoutHidingWeaponPreviews()
        {
            foreach (double viewport in new[] { 410.0, 450.0, 530.0 })
            for (int count = 1; count <= 3; count++)
            {
                double row = PreparationLayout.RowHeight(viewport, count);
                Near(viewport, row * count + PreparationLayout.RowGap * (count - 1));
                double graph = PreparationLayout.RhythmHeight(row), weapon = PreparationLayout.WeaponHeight(row);
                Check.True(graph >= 18 && weapon >= 22);
                Check.True(44 + 30 + graph + 14 + weapon <= row + .00001);
            }
        }

        [Test]
        public void ShortViewportsAndLongListsScrollInsteadOfCrushingTheCards()
        {
            double shortRow = PreparationLayout.RowHeight(280, 3);
            Check.True(shortRow * 3 + PreparationLayout.RowGap * 2 > 280);
            Check.True(PreparationLayout.WeaponHeight(shortRow) >= 22);
            double longRow = PreparationLayout.RowHeight(450, 5);
            Check.True(longRow * 5 + PreparationLayout.RowGap * 4 > 450);
            Check.Equal(0.0, PreparationLayout.RowHeight(450, 0));
        }

        [Test]
        public void EveryWeaponPoseFitsInsideItsPreviewAndKeepsAllAuthoredSockets()
        {
            foreach (var weapon in WeaponCatalog.All)
            foreach (var rarity in WeaponRarities.All)
            foreach (RangedWeaponPose pose in Enum.GetValues(typeof(RangedWeaponPose)))
            {
                var bounds = WeaponPreviewBounds.Get(weapon.Id, rarity, pose);
                Check.True(bounds.X >= 0 && bounds.Y >= 0 && bounds.Width > 0 && bounds.Height > 0);
                Check.True(bounds.X + bounds.Width <= 1.000001 && bounds.Y + bounds.Height <= 1.000001);
                foreach (var socket in WeaponArtLayout.Sockets(weapon.Id, rarity, pose))
                {
                    Check.True(socket.X - socket.RadiusX >= bounds.X && socket.X + socket.RadiusX <= bounds.X + bounds.Width,
                        weapon.Id + "/" + rarity + "/" + pose + " clips a socket horizontally");
                    Check.True(socket.Y - socket.RadiusY >= bounds.Y && socket.Y + socket.RadiusY <= bounds.Y + bounds.Height,
                        weapon.Id + "/" + rarity + "/" + pose + " clips a socket vertically");
                }
                foreach (double aspect in new[] { .91, 1.0, 1.08 })
                foreach (var box in new[] { (Width: 42.0, Height: 80.0), (Width: 120.0, Height: 180.0), (Width: 150.0, Height: 24.0) })
                {
                    var frame = WeaponPreviewBounds.Fit(bounds, aspect, box.Width, box.Height);
                    double left = frame.X + bounds.X * frame.Width, bottom = frame.Y + bounds.Y * frame.Height;
                    double width = bounds.Width * frame.Width, height = bounds.Height * frame.Height;
                    Check.True(left >= -.00001 && bottom >= -.00001);
                    Check.True(left + width <= box.Width + .00001 && bottom + height <= box.Height + .00001);
                    Near(box.Width * .5, left + width * .5); Near(box.Height * .5, bottom + height * .5);
                    Check.True(Math.Abs(width - box.Width) < .00001 || Math.Abs(height - box.Height) < .00001);
                    Near(aspect, frame.Width / frame.Height);
                }
            }
        }

        [Test]
        public void NarrowSpearUsesThePreviewHeightInsteadOfItsTransparentSquareWidth()
        {
            var bounds = WeaponPreviewBounds.Get("spear", WeaponRarity.Common);
            var frame = WeaponPreviewBounds.Fit(bounds, 1, 42, 80);
            Near(80, bounds.Height * frame.Height);
            Check.True(bounds.Height * frame.Height > bounds.Height * 42 * 1.8);
        }

        private static void Near(double expected, double actual) =>
            Check.True(Math.Abs(expected - actual) < .00001, "Expected " + expected + ", got " + actual);
    }
}
