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
                // The new dual swords use the repository's paired vector silhouette until
                // dedicated sprite artwork is authored; validate that path separately below.
                foreach (var weapon in WeaponCatalog.All.Where(w => w.Kind != WeaponKind.DualSwords &&
                    w.Kind != WeaponKind.Staff && w.Kind != WeaponKind.SpiritBell))
                foreach (var rarity in WeaponRarities.All)
                {
                    var state = new WeaponState(weapon.Id, rarity, 3);
                    var sprite = WeaponSpriteCache.Get(weapon.Id, rarity);
                    Assert.IsNotNull(sprite, weapon.Id + "/" + rarity);
                    if (weapon.IsRanged)
                    {
                        var slice = RangedWeaponArtLayout.Slice(weapon.Id, rarity, RangedWeaponPose.Idle);
                        float expected = (float)(sprite.texture.width * (slice.Right - slice.Left));
                        Assert.That(sprite.rect.width, Is.InRange(expected - 1, expected + 1));
                    }
                    else Assert.AreEqual(sprite.texture.width, sprite.rect.width);
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

        [UnityTest]
        public IEnumerator NewWeaponsRemainVisibleWithOrWithoutTheirOptionalArtPack()
        {
            var canvas = new GameObject("Dual swords fallback", typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var node = new GameObject("Weapon", typeof(RectTransform), typeof(CanvasRenderer));
            node.transform.SetParent(canvas.transform, false);
            var graphic = node.AddComponent<WeaponIconGraphic>(); graphic.rectTransform.sizeDelta = new Vector2(140, 180);
            try
            {
                foreach (string id in new[] { "dual-swords", "staff", "spirit-bell" })
                foreach (var rarity in WeaponRarities.All)
                {
                    graphic.Bind(new WeaponState(id, rarity)); yield return null;
                    Canvas.ForceUpdateCanvases(); graphic.Rebuild(CanvasUpdate.PreRender);
                    var mesh = graphic.canvasRenderer.GetMesh();
                    Assert.IsNotNull(mesh); Assert.Greater(mesh.vertexCount, 0);
                    Assert.AreEqual(WeaponCatalog.Find(id).Kind, graphic.Kind);
                    Assert.AreEqual(WeaponCatalog.Find(id).ActionCountAt(rarity), graphic.GetComponentInChildren<WeaponSocketGraphic>().SocketCount);
                    var sprite = WeaponSpriteCache.Get(id, rarity);
                    if (sprite != null) AssertTransparent(sprite.texture);
                }
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        [UnityTest]
        public IEnumerator RangedAtlasFramesAndAllTenEffectsImportWithRealAlpha()
        {
            foreach (var weapon in WeaponCatalog.All.Where(x => x.IsRanged))
            foreach (var rarity in WeaponRarities.All)
            {
                var idle = WeaponSpriteCache.Get(weapon.Id, rarity, RangedWeaponPose.Idle);
                var prepare = WeaponSpriteCache.Get(weapon.Id, rarity, RangedWeaponPose.Prepare);
                var release = WeaponSpriteCache.Get(weapon.Id, rarity, RangedWeaponPose.Release);
                Assert.IsNotNull(idle); Assert.IsNotNull(prepare); Assert.IsNotNull(release);
                Assert.AreSame(idle.texture, prepare.texture); Assert.AreSame(idle.texture, release.texture);
                Assert.AreEqual(idle.rect.xMax, prepare.rect.xMin); Assert.AreEqual(prepare.rect.xMax, release.rect.xMin);
                Assert.AreEqual(idle.texture.width, release.rect.xMax);
                AssertTransparent(idle.texture);
            }
            foreach (string key in BattleVfxCatalog.Keys)
            {
                var sprite = Resources.Load<Sprite>(BattleVfxCatalog.Root + key);
                Assert.IsNotNull(sprite, key); AssertTransparent(sprite.texture);
            }
            yield return null;
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
