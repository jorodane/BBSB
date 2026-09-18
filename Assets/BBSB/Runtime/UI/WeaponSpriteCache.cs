using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;

namespace BBSB.Runtime.UI
{
    public static class WeaponSpriteCache
    {
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        public static bool HasAttributeArtwork(string id) => Resources.Load<Texture2D>(WeaponAttributeArtLayout.ResourcePath(id)) != null;
        public static Sprite Get(string id, WeaponRarity rarity, RangedWeaponPose pose = RangedWeaponPose.Idle,
            WeaponAttribute? attribute = null)
        {
            var weapon = WeaponCatalog.Find(id);
            if ((int)pose < 0 || (int)pose > 2) throw new System.ArgumentOutOfRangeException(nameof(pose));
            if (!weapon.IsRanged) pose = RangedWeaponPose.Idle;
            if (attribute.HasValue)
            {
                WeaponAttributes.Validate(attribute.Value);
                string variantKey = "attribute/" + id + "/" + (int)attribute.Value + "/" + (int)pose;
                if (sprites.TryGetValue(variantKey, out var variant) && variant != null) return variant;
                var atlas = Resources.Load<Texture2D>(WeaponAttributeArtLayout.ResourcePath(id));
                if (atlas != null)
                {
                    var frame = WeaponAttributeArtLayout.Frame(id, attribute.Value, pose);
                    int x = Mathf.RoundToInt((float)frame.X * atlas.width), y = Mathf.RoundToInt((float)frame.Y * atlas.height);
                    int right = Mathf.RoundToInt((float)(frame.X + frame.Width) * atlas.width);
                    int top = Mathf.RoundToInt((float)(frame.Y + frame.Height) * atlas.height);
                    variant = Sprite.Create(atlas, new Rect(x, y, right - x, top - y), new Vector2(.5f, .5f), 100,
                        0, SpriteMeshType.FullRect, Vector4.zero, false);
                    variant.name = id + "-" + attribute.Value + "-" + pose;
                    sprites[variantKey] = variant; return variant;
                }
            }
            string path = WeaponArtLayout.ResourcePath(id, rarity), key = path + "/" + (int)pose;
            if (sprites.TryGetValue(key, out var result) && result != null) return result;
            if (!weapon.IsRanged)
            {
                result = Resources.Load<Sprite>(path);
                if (result == null)
                    result = Resources.Load<Sprite>(WeaponArtLayout.ResourcePath(id, WeaponRarity.Common));
                if (result == null)
                {
                    string fallback = WeaponArtLayout.FallbackId(id);
                    result = Resources.Load<Sprite>(WeaponArtLayout.ResourcePath(fallback, rarity)) ??
                        Resources.Load<Sprite>(WeaponArtLayout.ResourcePath(fallback, WeaponRarity.Common));
                }
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
