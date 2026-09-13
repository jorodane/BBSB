using System;
using System.Collections.Generic;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    public readonly struct WeaponAtlasSlice
    {
        public double Left { get; }
        public double Right { get; }
        public WeaponAtlasSlice(double left, double right) { Left = left; Right = right; }
    }

    // Measured per frame; origin is the bottom left of each authored atlas slice.
    public static class RangedWeaponArtLayout
    {
        private static readonly Dictionary<string, IReadOnlyList<WeaponArtSocket>> layouts =
            new Dictionary<string, IReadOnlyList<WeaponArtSocket>> {
            { "bow/common/Idle", Array.AsReadOnly(new[] { new WeaponArtSocket(0.714779, 0.53453, 0.037983, 0.042818) }) },
            { "bow/common/Prepare", Array.AsReadOnly(new[] { new WeaponArtSocket(0.732735, 0.537293, 0.037983, 0.042818) }) },
            { "bow/common/Release", Array.AsReadOnly(new[] { new WeaponArtSocket(0.709945, 0.53384, 0.037293, 0.043508) }) },
            { "bow/epic/Idle", Array.AsReadOnly(new[] { new WeaponArtSocket(0.662983, 0.654696, 0.038674, 0.038674), new WeaponArtSocket(0.70442, 0.532459, 0.038674, 0.037983) }) },
            { "bow/epic/Prepare", Array.AsReadOnly(new[] { new WeaponArtSocket(0.688536, 0.654696, 0.037983, 0.038674), new WeaponArtSocket(0.734807, 0.532459, 0.038674, 0.037983) }) },
            { "bow/epic/Release", Array.AsReadOnly(new[] { new WeaponArtSocket(0.616713, 0.654696, 0.037983, 0.038674), new WeaponArtSocket(0.660221, 0.532459, 0.038674, 0.037983) }) },
            { "bow/legendary/Idle", Array.AsReadOnly(new[] { new WeaponArtSocket(0.622928, 0.685083, 0.030387, 0.03453), new WeaponArtSocket(0.65884, 0.537983, 0.030387, 0.03384), new WeaponArtSocket(0.622928, 0.389503, 0.030387, 0.03453) }) },
            { "bow/legendary/Prepare", Array.AsReadOnly(new[] { new WeaponArtSocket(0.629834, 0.685083, 0.030387, 0.03453), new WeaponArtSocket(0.66989, 0.538674, 0.030387, 0.03453), new WeaponArtSocket(0.630525, 0.390193, 0.031077, 0.03384) }) },
            { "bow/legendary/Release", Array.AsReadOnly(new[] { new WeaponArtSocket(0.630525, 0.685083, 0.031077, 0.03453), new WeaponArtSocket(0.666436, 0.538674, 0.031077, 0.03453), new WeaponArtSocket(0.630525, 0.389503, 0.031077, 0.03453) }) },
            { "bow/rare/Idle", Array.AsReadOnly(new[] { new WeaponArtSocket(0.698895, 0.588398, 0.037293, 0.037293), new WeaponArtSocket(0.699586, 0.470304, 0.037983, 0.037983) }) },
            { "bow/rare/Prepare", Array.AsReadOnly(new[] { new WeaponArtSocket(0.723066, 0.588398, 0.037983, 0.037293), new WeaponArtSocket(0.723066, 0.470994, 0.037983, 0.037293) }) },
            { "bow/rare/Release", Array.AsReadOnly(new[] { new WeaponArtSocket(0.694751, 0.588398, 0.038674, 0.037293), new WeaponArtSocket(0.694751, 0.470994, 0.038674, 0.037293) }) },
            { "crossbow/common/Idle", Array.AsReadOnly(new[] { new WeaponArtSocket(0.424033, 0.53384, 0.051105, 0.050414) }) },
            { "crossbow/common/Prepare", Array.AsReadOnly(new[] { new WeaponArtSocket(0.421961, 0.532459, 0.050414, 0.050414) }) },
            { "crossbow/common/Release", Array.AsReadOnly(new[] { new WeaponArtSocket(0.423343, 0.533149, 0.051796, 0.051105) }) },
            { "crossbow/epic/Idle", Array.AsReadOnly(new[] { new WeaponArtSocket(0.38605, 0.532459, 0.047652, 0.049033), new WeaponArtSocket(0.519337, 0.529696, 0.046961, 0.046271) }) },
            { "crossbow/epic/Prepare", Array.AsReadOnly(new[] { new WeaponArtSocket(0.417127, 0.531768, 0.048343, 0.049724), new WeaponArtSocket(0.553867, 0.529696, 0.046961, 0.046271) }) },
            { "crossbow/epic/Release", Array.AsReadOnly(new[] { new WeaponArtSocket(0.380525, 0.531077, 0.047652, 0.049033), new WeaponArtSocket(0.514503, 0.530387, 0.046271, 0.046961) }) },
            { "crossbow/legendary/Idle", Array.AsReadOnly(new[] { new WeaponArtSocket(0.365278, 0.536602, 0.045833, 0.04489), new WeaponArtSocket(0.491667, 0.536602, 0.045833, 0.04489), new WeaponArtSocket(0.618056, 0.536602, 0.045833, 0.04489) }) },
            { "crossbow/legendary/Prepare", Array.AsReadOnly(new[] { new WeaponArtSocket(0.363824, 0.536602, 0.044704, 0.04489), new WeaponArtSocket(0.489684, 0.536602, 0.045392, 0.04489), new WeaponArtSocket(0.614856, 0.536602, 0.045392, 0.04489) }) },
            { "crossbow/legendary/Release", Array.AsReadOnly(new[] { new WeaponArtSocket(0.366897, 0.536602, 0.045517, 0.04489), new WeaponArtSocket(0.492414, 0.536602, 0.045517, 0.04489), new WeaponArtSocket(0.618621, 0.536602, 0.044828, 0.04489) }) },
            { "crossbow/rare/Idle", Array.AsReadOnly(new[] { new WeaponArtSocket(0.428177, 0.529696, 0.049724, 0.049033), new WeaponArtSocket(0.614641, 0.529696, 0.049724, 0.049033) }) },
            { "crossbow/rare/Prepare", Array.AsReadOnly(new[] { new WeaponArtSocket(0.425414, 0.530387, 0.049724, 0.049724), new WeaponArtSocket(0.610497, 0.529696, 0.049724, 0.049033) }) },
            { "crossbow/rare/Release", Array.AsReadOnly(new[] { new WeaponArtSocket(0.425414, 0.529696, 0.049724, 0.049033), new WeaponArtSocket(0.610497, 0.529696, 0.049724, 0.049033) }) },
            { "wand/common/Idle", Array.AsReadOnly(new[] { new WeaponArtSocket(0.330801, 0.40884, 0.047652, 0.048343) }) },
            { "wand/common/Prepare", Array.AsReadOnly(new[] { new WeaponArtSocket(0.316298, 0.40884, 0.046961, 0.048343) }) },
            { "wand/common/Release", Array.AsReadOnly(new[] { new WeaponArtSocket(0.30663, 0.40884, 0.046961, 0.048343) }) },
            { "wand/epic/Idle", Array.AsReadOnly(new[] { new WeaponArtSocket(0.495166, 0.561464, 0.032459, 0.032459), new WeaponArtSocket(0.344613, 0.407459, 0.035221, 0.035912) }) },
            { "wand/epic/Prepare", Array.AsReadOnly(new[] { new WeaponArtSocket(0.462707, 0.553867, 0.031768, 0.031768), new WeaponArtSocket(0.31768, 0.402624, 0.03453, 0.035221) }) },
            { "wand/epic/Release", Array.AsReadOnly(new[] { new WeaponArtSocket(0.372928, 0.553177, 0.031768, 0.032459), new WeaponArtSocket(0.232735, 0.403315, 0.03384, 0.03453) }) },
            { "wand/legendary/Idle", Array.AsReadOnly(new[] { new WeaponArtSocket(0.464088, 0.538674, 0.037293, 0.03453), new WeaponArtSocket(0.361188, 0.441989, 0.037983, 0.037293), new WeaponArtSocket(0.257597, 0.334254, 0.039365, 0.038674) }) },
            { "wand/legendary/Prepare", Array.AsReadOnly(new[] { new WeaponArtSocket(0.462707, 0.539365, 0.037293, 0.035221), new WeaponArtSocket(0.359807, 0.441989, 0.037983, 0.037293), new WeaponArtSocket(0.254834, 0.334254, 0.039365, 0.038674) }) },
            { "wand/legendary/Release", Array.AsReadOnly(new[] { new WeaponArtSocket(0.457182, 0.539365, 0.037293, 0.035221), new WeaponArtSocket(0.352901, 0.441989, 0.037983, 0.037293), new WeaponArtSocket(0.247928, 0.334254, 0.039365, 0.038674) }) },
            { "wand/rare/Idle", Array.AsReadOnly(new[] { new WeaponArtSocket(0.536254, 0.538674, 0.05287, 0.046961), new WeaponArtSocket(0.369335, 0.398481, 0.053625, 0.049033) }) },
            { "wand/rare/Prepare", Array.AsReadOnly(new[] { new WeaponArtSocket(0.467033, 0.539365, 0.046703, 0.046271), new WeaponArtSocket(0.317308, 0.399171, 0.048077, 0.046961) }) },
            { "wand/rare/Release", Array.AsReadOnly(new[] { new WeaponArtSocket(0.455882, 0.538674, 0.045396, 0.046961), new WeaponArtSocket(0.316496, 0.399862, 0.045396, 0.047652) }) },
        };
        private static readonly Dictionary<string, BattlePathPoint[]> muzzles = new Dictionary<string, BattlePathPoint[]> {
            { "bow/common", new[] { new BattlePathPoint(0.752762, 0.535), new BattlePathPoint(0.770718, 0.535), new BattlePathPoint(0.747238, 0.535) } },
            { "bow/epic", new[] { new BattlePathPoint(0.743094, 0.535), new BattlePathPoint(0.773481, 0.535), new BattlePathPoint(0.698895, 0.535) } },
            { "bow/legendary", new[] { new BattlePathPoint(0.689227, 0.535), new BattlePathPoint(0.700277, 0.535), new BattlePathPoint(0.697513, 0.535) } },
            { "bow/rare", new[] { new BattlePathPoint(0.736188, 0.535), new BattlePathPoint(0.761049, 0.535), new BattlePathPoint(0.733425, 0.535) } },
            { "crossbow/common", new[] { new BattlePathPoint(0.9, 0.555), new BattlePathPoint(0.91, 0.579), new BattlePathPoint(0.895, 0.555) } },
            { "crossbow/epic", new[] { new BattlePathPoint(0.834, 0.551), new BattlePathPoint(0.872, 0.581), new BattlePathPoint(0.828, 0.551) } },
            { "crossbow/legendary", new[] { new BattlePathPoint(0.897, 0.55), new BattlePathPoint(0.904, 0.568), new BattlePathPoint(0.893, 0.551) } },
            { "crossbow/rare", new[] { new BattlePathPoint(0.895, 0.554), new BattlePathPoint(0.91, 0.577), new BattlePathPoint(0.891, 0.554) } },
            { "wand/common", new[] { new BattlePathPoint(0.706, 0.745), new BattlePathPoint(0.668, 0.74), new BattlePathPoint(0.772, 0.753) } },
            { "wand/epic", new[] { new BattlePathPoint(0.643, 0.71), new BattlePathPoint(0.62, 0.695), new BattlePathPoint(0.59, 0.7) } },
            { "wand/legendary", new[] { new BattlePathPoint(0.683, 0.745), new BattlePathPoint(0.663, 0.735), new BattlePathPoint(0.704, 0.736) } },
            { "wand/rare", new[] { new BattlePathPoint(0.816, 0.792), new BattlePathPoint(0.683, 0.766), new BattlePathPoint(0.759, 0.783) } },
        };
        private static readonly double[] equalSlices = { 0, 1.0 / 3, 2.0 / 3, 1 };
        private static readonly Dictionary<string, double[]> sliceBounds = new Dictionary<string, double[]> {
            { "crossbow/legendary", new[] { 0.0, 0.3314917127071823, 0.666206261510129, 1.0 } },
            { "wand/rare", new[] { 0.0, 0.30478821362799263, 0.639963167587477, 1.0 } },
        };
        public static WeaponAtlasSlice Slice(string id, WeaponRarity rarity, RangedWeaponPose pose)
        {
            Sockets(id, rarity, pose);
            if (!sliceBounds.TryGetValue(id + "/" + WeaponRarities.Key(rarity), out var bounds)) bounds = equalSlices;
            return new WeaponAtlasSlice(bounds[(int)pose], bounds[(int)pose + 1]);
        }
        public static IReadOnlyList<WeaponArtSocket> Sockets(string id, WeaponRarity rarity, RangedWeaponPose pose)
        {
            if (!WeaponCatalog.Find(id).IsRanged) throw new ArgumentException("A ranged weapon is required.", nameof(id));
            if ((int)pose < 0 || (int)pose > 2) throw new ArgumentOutOfRangeException(nameof(pose));
            return layouts[id + "/" + WeaponRarities.Key(rarity) + "/" + pose];
        }
        public static BattlePathPoint Muzzle(string id, WeaponRarity rarity, RangedWeaponPose pose)
        {
            Sockets(id, rarity, pose);
            return muzzles[id + "/" + WeaponRarities.Key(rarity)][(int)pose];
        }
    }
}
