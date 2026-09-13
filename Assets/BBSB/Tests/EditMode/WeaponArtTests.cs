using System;
using System.Collections.Generic;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class WeaponArtTests
    {
        [Test]
        public void EveryRarityHasAUniqueResourceAndOneNonoverlappingSocketPerAction()
        {
            var paths = new HashSet<string>();
            foreach (var weapon in WeaponCatalog.All)
            foreach (var rarity in WeaponRarities.All)
            {
                Check.True(paths.Add(WeaponArtLayout.ResourcePath(weapon.Id, rarity)));
                var sockets = WeaponArtLayout.Sockets(weapon.Id, rarity);
                Check.Equal(weapon.ActionCountAt(rarity), sockets.Count);
                for (int i = 0; i < sockets.Count; i++)
                {
                    var a = sockets[i];
                    Check.True(a.RadiusX > .015 && a.RadiusY > .015);
                    Check.True(a.X - a.RadiusX > 0 && a.X + a.RadiusX < 1);
                    Check.True(a.Y - a.RadiusY > 0 && a.Y + a.RadiusY < 1);
                    for (int j = 0; j < i; j++)
                    {
                        var b = sockets[j];
                        Check.True(Math.Abs(a.X - b.X) > a.RadiusX + b.RadiusX ||
                            Math.Abs(a.Y - b.Y) > a.RadiusY + b.RadiusY);
                    }
                }
            }
            Check.Equal(32, paths.Count);
        }

        [Test]
        public void SocketPulseUsesOnlyElapsedSongTimeAndEndsWithoutAResidualFlash()
        {
            Check.Equal(0.0, WeaponSocketPulse.Sample(-.001));
            Check.Equal(1.0, WeaponSocketPulse.Sample(0));
            Check.Equal(.25, WeaponSocketPulse.Sample(WeaponSocketPulse.Duration * .5));
            Check.Equal(0.0, WeaponSocketPulse.Sample(WeaponSocketPulse.Duration));
            Check.Equal(0.0, WeaponSocketPulse.Sample(double.NaN));
            Check.Equal(0.0, WeaponSocketPulse.Sample(double.PositiveInfinity));
            for (int step = 0; step < 100; step++)
            {
                double age = step * WeaponSocketPulse.Duration / 100;
                Check.True(WeaponSocketPulse.Sample(age) > WeaponSocketPulse.Sample(age + .001));
            }
        }

        [Test]
        public void InvalidRarityCannotEnterEquipmentOrLookUpActionCapabilities()
        {
            foreach (int value in new[] { -1, 4, int.MaxValue })
            {
                var rarity = (WeaponRarity)value; bool rejected = false;
                try { new WeaponState("sword", rarity); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Check.True(rejected); rejected = false;
                try { WeaponCatalog.Find("shield").ActionsAt(rarity); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Check.True(rejected);
            }
        }
    }
}
