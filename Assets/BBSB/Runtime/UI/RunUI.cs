using System;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    internal sealed class RunUI
    {
        public static readonly Color Ink = Hex("101321");
        public static readonly Color Panel = Hex("1C2336");
        public static readonly Color Gold = Hex("F3CA79");
        public static readonly Color TextColor = Hex("F3EFE6");
        public static readonly Color Muted = Hex("A6B1C5");
        public static readonly Color Teal = Hex("80DAC5");
        public static readonly Color Red = Hex("EE8B92");
        private readonly Font font;
        public Font Font => font;

        public RunUI(Font font) { this.font = font; }
        public static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }

        public RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); return rect;
        }

        public static void Stretch(RectTransform rect, float inset = 0)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset); rect.offsetMax = new Vector2(-inset, -inset);
        }

        public static LayoutElement Size(RectTransform rect, float height, float flexibleWidth = 0)
        {
            var layout = rect.gameObject.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height; layout.preferredHeight = height; layout.flexibleWidth = flexibleWidth;
            return layout;
        }

        public RectTransform Stack(Transform parent, string name = "Stack", int padding = 0, int spacing = 12)
        {
            var rect = Rect(name, parent);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding); layout.spacing = spacing;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            return rect;
        }

        public RectTransform Row(Transform parent, float height, int spacing = 10)
        {
            var rect = Rect("Row", parent); Size(rect, height);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing; layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
            return rect;
        }

        public Image Background(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = raycast;
            return image;
        }

        public RectTransform Card(Transform parent, int padding = 22)
        {
            var card = Stack(parent, "Card", padding); Background(card, Panel);
            return card;
        }

        public Text Label(Transform parent, string value, int size = 24, Color? color = null,
            float height = 40, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var rect = Rect("Text", parent); Size(rect, height, 1);
            var text = rect.gameObject.AddComponent<Text>(); text.font = font; text.text = value;
            text.fontSize = size; text.color = color ?? TextColor; text.alignment = alignment;
            text.raycastTarget = false; text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        public Button Button(Transform parent, string label, Action action, bool enabled = true,
            bool primary = false, float height = 70)
        {
            var rect = Rect("Button " + label, parent); Size(rect, height, 1);
            var image = Background(rect, primary ? Gold : Hex("2B3850"), true);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = Hex("DDD9CC"); colors.pressedColor = Hex("AAA99F");
            colors.disabledColor = new Color(.5f, .5f, .5f, .55f); button.colors = colors;
            button.interactable = enabled;
            var text = Label(rect, label, 24, primary ? Ink : TextColor, height, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 8);
            button.onClick.AddListener(() => action());
            return button;
        }

        public RectTransform Scroll(Transform parent)
        {
            var root = Rect("Scroll", parent);
            var layout = root.gameObject.AddComponent<LayoutElement>(); layout.flexibleHeight = 1; layout.minHeight = 100;
            var scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
            Background(root, Ink, true);
            var viewport = Rect("Viewport", root); Stretch(viewport); viewport.gameObject.AddComponent<RectMask2D>();
            var content = Stack(viewport, "Content", 0, 14);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            return content;
        }

        public void Bar(Transform parent, float ratio, Color color)
        {
            var track = Rect("Health bar", parent); Size(track, 8); Background(track, Hex("344055"));
            var fill = Rect("Fill", track); Stretch(fill); fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1);
            Background(fill, color);
        }
    }
}
