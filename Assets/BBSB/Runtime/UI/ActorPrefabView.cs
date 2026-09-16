using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    // SpriteRenderers keep their native hierarchy/Animator. Their meshes are presented
    // through Canvas so overlay sorting, clipping and the existing weapon layers agree.
    public sealed class ActorPrefabView : MonoBehaviour
    {
        public Animator Animator { get; private set; }
        public GameObject Instance { get; private set; }
        private readonly List<SpriteCanvasGraphic> sprites = new List<SpriteCanvasGraphic>();
        private Image fallback;
        public void Initialize(GameObject prefab, RuntimeAnimatorController controller, Sprite portrait, float referenceHeight = 4)
        {
            if (Instance != null) throw new InvalidOperationException("Actor visual already initialized.");
            Instance = prefab != null ? Instantiate(prefab, transform, false) : new GameObject("Visual", typeof(RectTransform));
            if (prefab == null) Instance.transform.SetParent(transform, false);
            var renderers = Instance.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length > 0)
            {
                // A reimported/missing sprite reference must not hide an otherwise valid
                // actor prefab when its authoring asset still supplies a portrait.
                if (portrait != null && !Array.Exists(renderers, renderer => renderer.sprite != null))
                { renderers[0].sprite = portrait; referenceHeight = portrait.bounds.size.y; }
                foreach (var source in renderers)
                {
                    source.forceRenderingOff = true;
                    var rect = new GameObject(source.name + " Canvas mesh", typeof(RectTransform)).GetComponent<RectTransform>();
                    rect.SetParent(transform, false); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
                    rect.pivot = new Vector2(.5f, 0); rect.sizeDelta = new Vector2(512, 512);
                    var graphic = rect.gameObject.AddComponent<SpriteCanvasGraphic>();
                    graphic.Bind(source, Instance.transform, 512 / Mathf.Max(.001f, referenceHeight)); sprites.Add(graphic);
                }
            }
            else
            {
                var rect = Instance.transform as RectTransform;
                if (rect == null) throw new ArgumentException("외형 프리팹에는 SpriteRenderer 또는 RectTransform + Image가 필요해.");
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0); rect.anchoredPosition = Vector2.zero;
                if (prefab == null)
                {
                    rect.sizeDelta = new Vector2(512, 512);
                    var image = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    image.transform.SetParent(rect, false); fallback = image.GetComponent<Image>(); fallback.sprite = portrait;
                    fallback.rectTransform.anchorMin = fallback.rectTransform.anchorMax = new Vector2(.5f, 0);
                    fallback.rectTransform.pivot = portrait != null ? portrait.pivot / portrait.rect.size : new Vector2(.5f, 0);
                    fallback.rectTransform.sizeDelta = new Vector2(portrait != null ? 512 * portrait.rect.width / portrait.rect.height : 512, 512);
                }
                else fallback = Instance.GetComponentInChildren<Image>(true);
            }
            Animator = Instance.GetComponentInChildren<Animator>(true);
            if (Animator == null && controller != null) Animator = Instance.AddComponent<Animator>();
            if (Animator != null)
            {
                if (controller != null) Animator.runtimeAnimatorController = controller;
                Animator.speed = 0; Animator.applyRootMotion = false; Animator.fireEvents = false;
                Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (Animator.runtimeAnimatorController != null) { Animator.Rebind(); Animator.Update(0); }
            }
            foreach (var graphic in Instance.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            var group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = group.interactable = false; RefreshSprites();
        }
        public void SetSprite(Sprite sprite)
        {
            if (sprite == null) return;
            if (sprites.Count > 0) sprites[0].Source.sprite = sprite;
            else if (fallback != null) fallback.sprite = sprite;
        }
        public bool Sample(string state, double ageBeats, float duration, bool loop)
        {
            if (Animator == null || Animator.runtimeAnimatorController == null || string.IsNullOrEmpty(state) ||
                !Animator.HasState(0, Animator.StringToHash(state))) return false;
            float t = (float)(ageBeats / Math.Max(.001, duration));
            Animator.Play(state, 0, loop ? Mathf.Repeat(t, 1) : Mathf.Clamp(t, 0, .99999f)); Animator.Update(0);
            RefreshSprites(); return true;
        }
        public void RefreshSprites()
        {
            sprites.Sort((a, b) => {
                int layer = SortingLayer.GetLayerValueFromID(a.Source.sortingLayerID).CompareTo(SortingLayer.GetLayerValueFromID(b.Source.sortingLayerID));
                return layer != 0 ? layer : a.Source.sortingOrder.CompareTo(b.Source.sortingOrder);
            });
            for (int i = 0; i < sprites.Count; i++)
            { sprites[i].transform.SetSiblingIndex(i + 1); sprites[i].Refresh(); }
        }
    }
}
