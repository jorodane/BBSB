using System;
using System.Collections.Generic;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    public readonly struct WeaponArtSocket
    {
        public double X { get; }
        public double Y { get; }
        public double RadiusX { get; }
        public double RadiusY { get; }
        public WeaponArtSocket(double x, double y, double radiusX, double radiusY)
        { X = x; Y = y; RadiusX = radiusX; RadiusY = radiusY; }
    }

    public static class WeaponArtLayout
    {
        public const string Root = "BBSB/WeaponArt/";
        // Coordinates measured on each complete image, origin at bottom left.
        // Socket order follows WeaponDefinition.Actions, reading top to bottom then left to right.
        private static readonly Dictionary<string, IReadOnlyList<WeaponArtSocket>> layouts =
            new Dictionary<string, IReadOnlyList<WeaponArtSocket>> {
            { "bell/common", Array.AsReadOnly(new[] { new WeaponArtSocket(0.499601, 0.530702, 0.102472, 0.104067) }) },
            { "bell/epic", Array.AsReadOnly(new[] { new WeaponArtSocket(0.368022, 0.533493, 0.077751, 0.076555), new WeaponArtSocket(0.63118, 0.533892, 0.077751, 0.076954) }) },
            { "bell/legendary", Array.AsReadOnly(new[] { new WeaponArtSocket(0.5, 0.635167, 0.067783, 0.066587), new WeaponArtSocket(0.366029, 0.452153, 0.070175, 0.069378), new WeaponArtSocket(0.633971, 0.452153, 0.070175, 0.069378) }) },
            { "bell/rare", Array.AsReadOnly(new[] { new WeaponArtSocket(0.381978, 0.522727, 0.072568, 0.078549), new WeaponArtSocket(0.617225, 0.523525, 0.072568, 0.078549) }) },
            { "blade/common", Array.AsReadOnly(new[] { new WeaponArtSocket(0.536683, 0.522329, 0.051834, 0.052632) }) },
            { "blade/epic", Array.AsReadOnly(new[] { new WeaponArtSocket(0.555423, 0.634769, 0.052233, 0.052632), new WeaponArtSocket(0.559809, 0.427831, 0.052632, 0.05303) }) },
            { "blade/legendary", Array.AsReadOnly(new[] { new WeaponArtSocket(0.543062, 0.672249, 0.044657, 0.044657), new WeaponArtSocket(0.543062, 0.50638, 0.044657, 0.044657), new WeaponArtSocket(0.543062, 0.341308, 0.044657, 0.044657) }) },
            { "blade/rare", Array.AsReadOnly(new[] { new WeaponArtSocket(0.527512, 0.586523, 0.050638, 0.050638), new WeaponArtSocket(0.526715, 0.435008, 0.049841, 0.049841) }) },
            { "dagger/common", Array.AsReadOnly(new[] { new WeaponArtSocket(0.500399, 0.463317, 0.049043, 0.049442) }) },
            { "dagger/epic", Array.AsReadOnly(new[] { new WeaponArtSocket(0.499601, 0.562998, 0.044258, 0.044657), new WeaponArtSocket(0.499601, 0.423046, 0.044258, 0.044258) }) },
            { "dagger/legendary", Array.AsReadOnly(new[] { new WeaponArtSocket(0.5, 0.61563, 0.04067, 0.04067), new WeaponArtSocket(0.5, 0.491228, 0.04067, 0.04067), new WeaponArtSocket(0.5, 0.366826, 0.04067, 0.04067) }) },
            { "dagger/rare", Array.AsReadOnly(new[] { new WeaponArtSocket(0.500399, 0.564992, 0.043461, 0.043461), new WeaponArtSocket(0.5, 0.427033, 0.048644, 0.048246) }) },
            { "greatsword/common", Array.AsReadOnly(new[] { new WeaponArtSocket(0.5, 0.391148, 0.032695, 0.033094) }) },
            { "greatsword/epic", Array.AsReadOnly(new[] { new WeaponArtSocket(0.5, 0.51874, 0.032695, 0.033094), new WeaponArtSocket(0.5, 0.386364, 0.035088, 0.035486) }) },
            { "greatsword/legendary", Array.AsReadOnly(new[] { new WeaponArtSocket(0.5, 0.742823, 0.035088, 0.035486), new WeaponArtSocket(0.499601, 0.546252, 0.034689, 0.035088), new WeaponArtSocket(0.5, 0.40311, 0.035885, 0.036284) }) },
            { "greatsword/rare", Array.AsReadOnly(new[] { new WeaponArtSocket(0.5, 0.539872, 0.03429, 0.035088), new WeaponArtSocket(0.5, 0.393541, 0.03429, 0.034689) }) },
            { "hammer/common", Array.AsReadOnly(new[] { new WeaponArtSocket(0.5, 0.768341, 0.073365, 0.073764) }) },
            { "hammer/epic", Array.AsReadOnly(new[] { new WeaponArtSocket(0.385566, 0.76555, 0.05941, 0.059809), new WeaponArtSocket(0.614833, 0.76555, 0.059011, 0.059809) }) },
            { "hammer/legendary", Array.AsReadOnly(new[] { new WeaponArtSocket(0.314195, 0.759171, 0.056619, 0.056619), new WeaponArtSocket(0.500399, 0.758772, 0.057018, 0.057018), new WeaponArtSocket(0.686204, 0.759171, 0.05622, 0.056619) }) },
            { "hammer/rare", Array.AsReadOnly(new[] { new WeaponArtSocket(0.389952, 0.75638, 0.062998, 0.063397), new WeaponArtSocket(0.610048, 0.75638, 0.062998, 0.063397) }) },
            { "shield/common", Array.AsReadOnly(new[] { new WeaponArtSocket(0.499601, 0.564195, 0.112839, 0.112839) }) },
            { "shield/epic", Array.AsReadOnly(new[] { new WeaponArtSocket(0.5, 0.564593, 0.113238, 0.113238) }) },
            { "shield/legendary", Array.AsReadOnly(new[] { new WeaponArtSocket(0.499601, 0.682616, 0.084928, 0.083732), new WeaponArtSocket(0.5, 0.405104, 0.08453, 0.082935) }) },
            { "shield/rare", Array.AsReadOnly(new[] { new WeaponArtSocket(0.498804, 0.563397, 0.111244, 0.111244) }) },
            { "spear/common", Array.AsReadOnly(new[] { new WeaponArtSocket(0.500399, 0.600877, 0.026715, 0.026715) }) },
            { "spear/epic", Array.AsReadOnly(new[] { new WeaponArtSocket(0.5, 0.67504, 0.027113, 0.026715), new WeaponArtSocket(0.5, 0.572169, 0.027113, 0.026715) }) },
            { "spear/legendary", Array.AsReadOnly(new[] { new WeaponArtSocket(0.499601, 0.705343, 0.026715, 0.026715), new WeaponArtSocket(0.5, 0.608054, 0.027113, 0.026715), new WeaponArtSocket(0.5, 0.514753, 0.027113, 0.026715) }) },
            { "spear/rare", Array.AsReadOnly(new[] { new WeaponArtSocket(0.500399, 0.673445, 0.026715, 0.026715), new WeaponArtSocket(0.500399, 0.574561, 0.026715, 0.026715) }) },
            { "sword/common", Array.AsReadOnly(new[] { new WeaponArtSocket(0.5, 0.391547, 0.03748, 0.03748) }) },
            { "sword/epic", Array.AsReadOnly(new[] { new WeaponArtSocket(0.5, 0.550239, 0.032695, 0.032695), new WeaponArtSocket(0.5, 0.392743, 0.038278, 0.037879) }) },
            { "sword/legendary", Array.AsReadOnly(new[] { new WeaponArtSocket(0.5, 0.602871, 0.033493, 0.033493), new WeaponArtSocket(0.5, 0.452153, 0.039075, 0.039075), new WeaponArtSocket(0.500399, 0.324163, 0.030702, 0.030702) }) },
            { "sword/rare", Array.AsReadOnly(new[] { new WeaponArtSocket(0.5, 0.53748, 0.036683, 0.036683), new WeaponArtSocket(0.499601, 0.391946, 0.037879, 0.037879) }) },
        };
        public static string ResourcePath(string id, WeaponRarity rarity)
        { WeaponCatalog.Find(id); return Root + id + "/" + WeaponRarities.Key(rarity); }
        public static string FallbackId(string id)
        {
            switch (WeaponCatalog.Find(id).Kind)
            {
                case WeaponKind.Shield: return "shield";
                case WeaponKind.Spear: return "spear";
                case WeaponKind.Hammer: return "hammer";
                case WeaponKind.Dagger: return "dagger";
                case WeaponKind.Greatsword: return "greatsword";
                case WeaponKind.Bell: return "bell";
                case WeaponKind.Blade: return "blade";
                case WeaponKind.Staff: return "staff";
                case WeaponKind.SpiritBell: return "spirit-bell";
                default: return "sword";
            }
        }
        public static IReadOnlyList<WeaponArtSocket> Sockets(string id, WeaponRarity rarity)
            => Sockets(id, rarity, RangedWeaponPose.Idle);
        public static IReadOnlyList<WeaponArtSocket> Sockets(string id, WeaponRarity rarity, RangedWeaponPose pose)
        {
            var weapon = WeaponCatalog.Find(id);
            if (weapon.IsRanged) return RangedWeaponArtLayout.Sockets(id, rarity, pose);
            string key = id + "/" + WeaponRarities.Key(rarity);
            if (layouts.TryGetValue(key, out var layout)) return layout;
            if (weapon.Kind == WeaponKind.DualSwords) return layouts["sword/" + WeaponRarities.Key(rarity)];
            if (weapon.Kind == WeaponKind.Staff || weapon.Kind == WeaponKind.SpiritBell)
            {
                var sockets = new WeaponArtSocket[weapon.ActionCountAt(rarity)];
                for (int i = 0; i < sockets.Length; i++)
                    sockets[i] = weapon.Kind == WeaponKind.Staff ? new WeaponArtSocket(.5, .563 - .0595 * i, .012, .009) :
                        i == 0 ? new WeaponArtSocket(.5, .842, .027, .018) :
                        new WeaponArtSocket(i == 1 ? .314 : .686, .638, .027, .018);
                return Array.AsReadOnly(sockets);
            }
            // New shield families share the existing art until their own transparent assets are installed.
            return layouts[FallbackId(id) + "/" + WeaponRarities.Key(rarity)];
        }
    }
}
