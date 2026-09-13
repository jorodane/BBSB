using System.Collections;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BBSB.Tests
{
    public sealed class WeaponArtIntegrationTests
    {
        [UnityTest]
        public IEnumerator EveryRarityUsesItsOwnTransparentSpriteAndAttachedActionSockets()
        {
            var canvas = new GameObject("Weapon art test", typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var node = new GameObject("Weapon", typeof(RectTransform), typeof(CanvasRenderer));
            node.transform.SetParent(canvas.transform, false);
            var graphic = node.AddComponent<WeaponIconGraphic>();
            graphic.rectTransform.sizeDelta = new Vector2(140, 180);
            try
            {
                foreach (var weapon in WeaponCatalog.All)
                foreach (var rarity in WeaponRarities.All)
                {
                    var state = new WeaponState(weapon.Id, rarity, 3);
                    var sprite = Resources.Load<Sprite>(WeaponArtLayout.ResourcePath(weapon.Id, rarity));
                    Assert.IsNotNull(sprite, weapon.Id + "/" + rarity);
                    Assert.AreEqual(sprite.texture.width, sprite.rect.width);
                    Assert.AreEqual(sprite.texture.height, sprite.rect.height);
                    graphic.Bind(state); yield return null; Canvas.ForceUpdateCanvases();
                    Assert.IsTrue(graphic.HasArtwork); Assert.AreEqual(rarity, graphic.Rarity);
                    Assert.AreSame(sprite.texture, graphic.mainTexture);
                    var sockets = graphic.GetComponentInChildren<WeaponSocketGraphic>();
                    Assert.AreEqual(weapon.ActionCountAt(rarity), sockets.SocketCount);
                    Assert.AreSame(graphic.transform, sockets.transform.parent);
                    Assert.IsFalse(graphic.raycastTarget || sockets.raycastTarget);
                    graphic.rectTransform.localRotation = Quaternion.Euler(0, 0, 37);
                    Assert.AreEqual(graphic.transform.rotation, sockets.transform.rotation);
                    AssertTransparent(sprite.texture);
                }
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        private static void AssertTransparent(Texture2D texture)
        {
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(texture, target); RenderTexture.active = target;
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); copy.Apply();
                Assert.Less(copy.GetPixel(0, 0).a, .01f, texture.name);
                Assert.IsTrue(copy.GetPixels32().Any(p => p.a > 240), "Weapon must remain visible: " + texture.name);
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(copy); }
        }
    }
}
