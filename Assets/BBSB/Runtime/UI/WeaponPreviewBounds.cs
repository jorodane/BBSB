using System;
using System.Collections.Generic;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    public readonly struct PreviewRect
    {
        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public PreviewRect(double x, double y, double width, double height)
        { X = x; Y = y; Width = width; Height = height; }
    }

    public static class WeaponPreviewBounds
    {
        private static readonly Dictionary<string, PreviewRect> frames = new Dictionary<string, PreviewRect> {
            // BEGIN MEASURED BOUNDS
            { "bell/common/0", new PreviewRect(0.140350877, 0.017543860, 0.719298246, 0.972089314) },
            { "bell/epic/0", new PreviewRect(0.089314195, 0.018341308, 0.821371611, 0.972089314) },
            { "bell/legendary/0", new PreviewRect(0.059011164, 0.018341308, 0.881180223, 0.972886762) },
            { "bell/rare/0", new PreviewRect(0.086124402, 0.018341308, 0.827751196, 0.972089314) },
            { "blade/common/0", new PreviewRect(0.211323764, 0.093301435, 0.602073365, 0.898724083) },
            { "blade/epic/0", new PreviewRect(0.188197767, 0.031897927, 0.635566188, 0.960127592) },
            { "blade/legendary/0", new PreviewRect(0.162679426, 0.019936204, 0.675438596, 0.973684211) },
            { "blade/rare/0", new PreviewRect(0.202551834, 0.086921850, 0.618819777, 0.909090909) },
            { "bow/common/0", new PreviewRect(0.343922652, 0.046961326, 0.446132597, 0.922651934) },
            { "bow/common/1", new PreviewRect(0.226519337, 0.092541436, 0.698895028, 0.861878453) },
            { "bow/common/2", new PreviewRect(0.337016575, 0.071823204, 0.448895028, 0.900552486) },
            { "bow/epic/0", new PreviewRect(0.337016575, 0.048342541, 0.450276243, 0.929558011) },
            { "bow/epic/1", new PreviewRect(0.216850829, 0.074585635, 0.701657459, 0.893646409) },
            { "bow/epic/2", new PreviewRect(0.306629834, 0.066298343, 0.435082873, 0.911602210) },
            { "bow/legendary/0", new PreviewRect(0.352209945, 0.073204420, 0.447513812, 0.897790055) },
            { "bow/legendary/1", new PreviewRect(0.209944751, 0.075966851, 0.716850829, 0.895027624) },
            { "bow/legendary/2", new PreviewRect(0.349447514, 0.075966851, 0.457182320, 0.895027624) },
            { "bow/rare/0", new PreviewRect(0.343922652, 0.051104972, 0.421270718, 0.912983425) },
            { "bow/rare/1", new PreviewRect(0.209944751, 0.082872928, 0.714088398, 0.864640884) },
            { "bow/rare/2", new PreviewRect(0.335635359, 0.067679558, 0.425414365, 0.896408840) },
            { "crossbow/common/0", new PreviewRect(0.051104972, 0.193370166, 0.921270718, 0.693370166) },
            { "crossbow/common/1", new PreviewRect(0.046961326, 0.193370166, 0.929558011, 0.693370166) },
            { "crossbow/common/2", new PreviewRect(0.046961326, 0.193370166, 0.924033149, 0.693370166) },
            { "crossbow/epic/0", new PreviewRect(0.092541436, 0.191988950, 0.824585635, 0.675414365) },
            { "crossbow/epic/1", new PreviewRect(0.103591160, 0.187845304, 0.850828729, 0.679558011) },
            { "crossbow/epic/2", new PreviewRect(0.092541436, 0.191988950, 0.819060773, 0.675414365) },
            { "crossbow/legendary/0", new PreviewRect(0.009722222, 0.131215470, 0.990277778, 0.819060773) },
            { "crossbow/legendary/1", new PreviewRect(0.000000000, 0.131215470, 1.000000000, 0.814917127) },
            { "crossbow/legendary/2", new PreviewRect(0.000000000, 0.131215470, 0.995862069, 0.819060773) },
            { "crossbow/rare/0", new PreviewRect(0.049723757, 0.190607735, 0.919889503, 0.698895028) },
            { "crossbow/rare/1", new PreviewRect(0.046961326, 0.190607735, 0.933701657, 0.698895028) },
            { "crossbow/rare/2", new PreviewRect(0.045580110, 0.190607735, 0.922651934, 0.698895028) },
            { "dagger/common/0", new PreviewRect(0.296650718, 0.039074960, 0.409090909, 0.952153110) },
            { "dagger/epic/0", new PreviewRect(0.284688995, 0.037480064, 0.430622010, 0.954545455) },
            { "dagger/legendary/0", new PreviewRect(0.244019139, 0.032695375, 0.511961722, 0.960925040) },
            { "dagger/rare/0", new PreviewRect(0.291866029, 0.037480064, 0.416267943, 0.955342903) },
            { "greatsword/common/0", new PreviewRect(0.323763955, 0.016746411, 0.353269537, 0.971291866) },
            { "greatsword/epic/0", new PreviewRect(0.311802233, 0.014354067, 0.376395534, 0.976076555) },
            { "greatsword/legendary/0", new PreviewRect(0.270334928, 0.007974482, 0.459330144, 0.985645933) },
            { "greatsword/rare/0", new PreviewRect(0.317384370, 0.016746411, 0.367623604, 0.972089314) },
            { "hammer/common/0", new PreviewRect(0.114832536, 0.043062201, 0.771132376, 0.907496013) },
            { "hammer/epic/0", new PreviewRect(0.073365231, 0.037480064, 0.853269537, 0.925039872) },
            { "hammer/legendary/0", new PreviewRect(0.017543860, 0.029505582, 0.964912281, 0.945773525) },
            { "hammer/rare/0", new PreviewRect(0.082934609, 0.041467305, 0.833333333, 0.910685805) },
            { "shield/common/0", new PreviewRect(0.162679426, 0.031897927, 0.673843700, 0.948963317) },
            { "shield/epic/0", new PreviewRect(0.102073365, 0.031100478, 0.795853270, 0.954545455) },
            { "shield/legendary/0", new PreviewRect(0.098883573, 0.019936204, 0.802232855, 0.970494418) },
            { "shield/rare/0", new PreviewRect(0.127591707, 0.025518341, 0.744019139, 0.959330144) },
            { "spear/common/0", new PreviewRect(0.413875598, 0.023125997, 0.173046252, 0.964912281) },
            { "spear/epic/0", new PreviewRect(0.404306220, 0.022328549, 0.192185008, 0.967304625) },
            { "spear/legendary/0", new PreviewRect(0.377990431, 0.023125997, 0.244019139, 0.966507177) },
            { "spear/rare/0", new PreviewRect(0.404306220, 0.018341308, 0.191387560, 0.968899522) },
            { "sword/common/0", new PreviewRect(0.346889952, 0.019936204, 0.306220096, 0.965709729) },
            { "sword/epic/0", new PreviewRect(0.308612440, 0.013556619, 0.382775120, 0.972089314) },
            { "sword/legendary/0", new PreviewRect(0.287878788, 0.012759171, 0.424242424, 0.975279107) },
            { "sword/rare/0", new PreviewRect(0.327751196, 0.019936204, 0.344497608, 0.962519936) },
            { "wand/common/0", new PreviewRect(0.059392265, 0.093922652, 0.773480663, 0.773480663) },
            { "wand/common/1", new PreviewRect(0.046961326, 0.093922652, 0.842541436, 0.843922652) },
            { "wand/common/2", new PreviewRect(0.037292818, 0.093922652, 0.943370166, 0.846685083) },
            { "wand/epic/0", new PreviewRect(0.132596685, 0.162983425, 0.633977901, 0.672651934) },
            { "wand/epic/1", new PreviewRect(0.107734807, 0.156077348, 0.693370166, 0.704419890) },
            { "wand/epic/2", new PreviewRect(0.026243094, 0.162983425, 0.766574586, 0.701657459) },
            { "wand/legendary/0", new PreviewRect(0.033149171, 0.078729282, 0.872928177, 0.890883978) },
            { "wand/legendary/1", new PreviewRect(0.030386740, 0.078729282, 0.893646409, 0.890883978) },
            { "wand/legendary/2", new PreviewRect(0.023480663, 0.078729282, 0.944751381, 0.890883978) },
            { "wand/rare/0", new PreviewRect(0.055891239, 0.062154696, 0.907854985, 0.859116022) },
            { "wand/rare/1", new PreviewRect(0.034340659, 0.062154696, 0.891483516, 0.908839779) },
            { "wand/rare/2", new PreviewRect(0.052429668, 0.062154696, 0.925831202, 0.915745856) },
            // END MEASURED BOUNDS
        };

        public static PreviewRect Get(string id, WeaponRarity rarity, RangedWeaponPose pose = RangedWeaponPose.Idle)
        {
            var weapon = WeaponCatalog.Find(id);
            if ((int)pose < 0 || (int)pose > 2) throw new ArgumentOutOfRangeException(nameof(pose));
            if (!weapon.IsRanged) pose = RangedWeaponPose.Idle;
            return frames[id + "/" + WeaponRarities.Key(rarity) + "/" + (int)pose];
        }

        // Return the original canvas in target coordinates, so authored sockets retain their alignment.
        public static PreviewRect Fit(PreviewRect bounds, double sourceAspect, double width, double height)
        {
            double scale = Math.Max(0, Math.Min(width / (bounds.Width * sourceAspect), height / bounds.Height));
            double fullWidth = sourceAspect * scale;
            return new PreviewRect((width - bounds.Width * fullWidth) * .5 - bounds.X * fullWidth,
                (height - bounds.Height * scale) * .5 - bounds.Y * scale, fullWidth, scale);
        }
    }
}
