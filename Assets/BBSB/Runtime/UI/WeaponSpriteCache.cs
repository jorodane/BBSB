using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;

namespace BBSB.Runtime.UI
{
    public static class WeaponSpriteCache
    {
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        public static Sprite Get(string id, WeaponRarity rarity, RangedWeaponPose pose = RangedWeaponPose.Idle)
        {
            var weapon = WeaponCatalog.Find(id);
            if ((int)pose < 0 || (int)pose > 2) throw new System.ArgumentOutOfRangeException(nameof(pose));
            if (!weapon.IsRanged) pose = RangedWeaponPose.Idle;
            string path = WeaponArtLayout.ResourcePath(id, rarity), key = path + "/" + (int)pose;
            if (sprites.TryGetValue(key, out var result) && result != null) return result;
            if (!weapon.IsRanged)
            {
                result = Resources.Load<Sprite>(path);
                if (result == null && weapon.Kind == WeaponKind.Shield)
                    result = Resources.Load<Sprite>(WeaponArtLayout.ResourcePath("shield", rarity));
            }
            else
            {
                var texture = Resources.Load<Texture2D>(path);
                if (texture == null) return null;
                // Preserve the whole-file atlas; measured empty gutters keep each pose intact.
                var slice = RangedWeaponArtLayout.Slice(id, rarity, pose);
                int left = Mathf.RoundToInt(texture.width * (float)slice.Left);
                int right = Mathf.RoundToInt(texture.width * (float)slice.Right);
                result = Sprite.Create(texture, new Rect(left, 0, right - left, texture.height),
                    new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, Vector4.zero, false);
                result.name = id + "-" + rarity + "-" + pose;
            }
            if (result != null) sprites[key] = result;
            return result;
        }
    }
}
