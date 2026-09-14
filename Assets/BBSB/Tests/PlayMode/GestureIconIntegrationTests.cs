using System;
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
    public sealed class GestureIconIntegrationTests
    {
        [UnityTest]
        public IEnumerator IdleSocketsAlwaysShowShapesAndInheritWeaponRotationAndScale()
        {
            var canvas = new GameObject("Gesture socket test",typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var root = new GameObject("Weapon",typeof(RectTransform)); root.transform.SetParent(canvas.transform,false);
            var weapon = root.AddComponent<WeaponIconGraphic>(); weapon.rectTransform.sizeDelta = new Vector2(220,220);
            try
            {
                foreach (var rarity in WeaponRarities.All)
                {
                    weapon.Bind(new WeaponState("sword",rarity));
                    foreach (float angle in new[] { 0f,37f,90f,180f,270f })
                    {
                        root.transform.localRotation = Quaternion.Euler(0,0,angle); root.transform.localScale = new Vector3(1.2f,.8f,1);
                        yield return null; Canvas.ForceUpdateCanvases();
                        var socket = weapon.GetComponentInChildren<WeaponSocketGraphic>();
                        var icons = socket.GetComponentsInChildren<GestureIconGraphic>();
                        Assert.AreEqual(WeaponCatalog.Find("sword").ActionCountAt(rarity),icons.Length);
                        for (int i=0;i<icons.Length;i++)
                        {
                            Assert.AreEqual(WeaponCatalog.Find("sword").Actions[i].Kind,icons[i].Kind);
                            Assert.IsTrue(icons[i].enabled && icons[i].gameObject.activeInHierarchy);
                            Assert.Less(Quaternion.Angle(root.transform.rotation,icons[i].transform.rotation),.001f);
                            Assert.AreEqual(Vector3.one,icons[i].transform.localScale);
                            Assert.IsFalse(icons[i].raycastTarget);
                        }
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(canvas); }
        }

        [UnityTest]
        public IEnumerator AllTenInstalledSpritesRemainDistinctAndTransparentWithoutTakingInput()
        {
            var go = new GameObject("Gesture image test",typeof(RectTransform)); var icon=go.AddComponent<GestureIconGraphic>();
            try
            {
                foreach(GestureKind kind in Enum.GetValues(typeof(GestureKind)))
                foreach(bool large in new[]{false,true})
                {
                    icon.Bind(kind,large); yield return null;
                    var sprite=Resources.Load<Sprite>(GestureIconCatalog.ResourcePath(kind,large));
                    Assert.IsNotNull(sprite,GestureIconCatalog.ResourcePath(kind,large));
                    Assert.IsTrue(icon.HasArtwork); Assert.AreSame(sprite.texture,icon.mainTexture);
                    Assert.IsFalse(icon.raycastTarget); Assert.AreEqual(sprite.rect.width,sprite.rect.height);
                    var previous=RenderTexture.active; var rt=RenderTexture.GetTemporary(sprite.texture.width,sprite.texture.height,0,RenderTextureFormat.ARGB32);
                    var copy=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
                    try { Graphics.Blit(sprite.texture,rt); RenderTexture.active=rt;
                        copy.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); copy.Apply();
                        Assert.Less(copy.GetPixel(0,0).a,.01f); Assert.IsTrue(copy.GetPixels32().Any(p=>p.a>240)); }
                    finally { RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(copy); }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
