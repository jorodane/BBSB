using System;
using System.Collections.Generic;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    public static partial class WeaponAttributeArtLayout
    {
        public const string Root = "BBSB/WeaponAttributeArt/";
        public static string ResourcePath(string id) { WeaponCatalog.Find(id); return Root + id; }
        public static PreviewRect Frame(string id, WeaponAttribute attribute, RangedWeaponPose pose = RangedWeaponPose.Idle)
        {
            var weapon = WeaponCatalog.Find(id); WeaponAttributes.Validate(attribute);
            if ((int)pose < 0 || (int)pose > 2) throw new ArgumentOutOfRangeException(nameof(pose));
            if (!weapon.IsRanged) pose = RangedWeaponPose.Idle;
            if (measuredFrames.TryGetValue(id + "/" + (int)attribute + "/" + (int)pose, out var measured)) return measured;
            if (weapon.ExclusiveAttribute.HasValue) return new PreviewRect(0, 0, 1, 1);
            if (weapon.IsRanged) return new PreviewRect((int)attribute * .25, (2 - (int)pose) / 3.0, .25, 1.0 / 3);
            int index = (int)attribute;
            return new PreviewRect((index % 2) * .5, index < 2 ? .5 : 0, .5, .5);
        }
        public static PreviewRect Bounds(string id, WeaponAttribute attribute, RangedWeaponPose pose = RangedWeaponPose.Idle)
        {
            var weapon = WeaponCatalog.Find(id); WeaponAttributes.Validate(attribute);
            if (!weapon.IsRanged) pose = RangedWeaponPose.Idle;
            return measuredBounds.TryGetValue(id + "/" + (int)attribute + "/" + (int)pose, out var rect) ? rect : new PreviewRect(0, 0, 1, 1);
        }
        public static IReadOnlyList<WeaponArtSocket> Sockets(string id, WeaponRarity rarity)
        {
            int count = WeaponCatalog.Find(id).ActionCountAt(rarity);
            var result = new WeaponArtSocket[count];
            // Attribute art does not reuse sockets measured on the older rarity art.
            // A small consistent footer keeps legacy action markers outside the weapon.
            for (int i = 0; i < count; i++) result[i] = new WeaponArtSocket(.5 + (i - (count - 1) * .5) * .14, .06, .045, .045);
            return Array.AsReadOnly(result);
        }
    }
}
