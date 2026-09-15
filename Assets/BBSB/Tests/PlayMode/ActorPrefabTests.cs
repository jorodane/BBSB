using System.Linq;
using BBSB.Core;
using BBSB.Runtime;
using BBSB.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
namespace BBSB.Tests
{
    public sealed class ActorPrefabTests
    {
        [Test]
        public void SpritePrefabKeepsNativeRendererAndCreatesNonBlockingCanvasMesh()
        {
            var prefab = new GameObject("Prefab", typeof(SpriteRenderer));
            var host = new GameObject("Host", typeof(RectTransform));
            var texture = new Texture2D(16, 32); var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 32), Vector2.zero);
            try
            {
                prefab.GetComponent<SpriteRenderer>().sprite = sprite;
                var view = host.AddComponent<ActorPrefabView>(); view.Initialize(prefab, null, sprite, 4);
                var source = view.Instance.GetComponent<SpriteRenderer>();
                Assert.IsTrue(source.forceRenderingOff); Assert.IsTrue(source.enabled);
                Assert.IsFalse(prefab.GetComponent<SpriteRenderer>().forceRenderingOff);
                var mesh = host.GetComponentInChildren<SpriteCanvasGraphic>();
                Assert.AreSame(sprite.texture, mesh.mainTexture); Assert.IsFalse(mesh.raycastTarget);
                Assert.IsFalse(host.GetComponent<CanvasGroup>().blocksRaycasts);
                source.enabled = false; view.RefreshSprites(); Assert.IsFalse(mesh.enabled);
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(prefab); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }
        [Test]
        public void UIImagePrefabRemainsAnEditableImageHierarchy()
        {
            var prefab = new GameObject("Prefab", typeof(RectTransform));
            var child = new GameObject("Body", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); child.transform.SetParent(prefab.transform);
            var host = new GameObject("Host", typeof(RectTransform));
            try
            {
                var view = host.AddComponent<ActorPrefabView>(); view.Initialize(prefab, null, null);
                Assert.IsNotNull(view.Instance.GetComponentInChildren<Image>());
                Assert.IsEmpty(host.GetComponentsInChildren<SpriteCanvasGraphic>());
                Assert.IsFalse(view.Instance.GetComponentInChildren<Image>().raycastTarget);
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(prefab); }
        }
        [Test]
        public void ScreenInstantiationRetainsAuthoredContentLayoutAndDoesNotEditSource()
        {
            var library = ScriptableObject.CreateInstance<PresentationPrefabs>();
            var prefab = new GameObject("Screen", typeof(RectTransform), typeof(CanvasScreen));
            var child = new GameObject("Content", typeof(RectTransform)); child.transform.SetParent(prefab.transform, false);
            var content = (RectTransform)child.transform; content.anchorMin = new Vector2(.2f, .1f); content.anchorMax = new Vector2(.8f, .9f);
            prefab.GetComponent<CanvasScreen>().content = content;
            library.screens = new[] { new PresentationPrefabs.Screen { kind = RunScreenKind.Map, prefab = prefab.GetComponent<CanvasScreen>() } };
            var parent = new GameObject("Parent", typeof(RectTransform));
            try
            {
                var instance = library.Create(RunScreenKind.Map, parent.transform);
                Assert.AreEqual(new Vector2(.2f, .1f), instance.content.anchorMin);
                Assert.AreEqual(new Vector2(.8f, .9f), instance.content.anchorMax);
                Assert.AreNotSame(content, instance.content);
                Assert.IsNull(library.Create(RunScreenKind.Shop, parent.transform));
            }
            finally { Object.DestroyImmediate(parent); Object.DestroyImmediate(prefab); Object.DestroyImmediate(library); }
        }
    }
}
