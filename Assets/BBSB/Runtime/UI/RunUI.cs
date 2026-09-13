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
            // A nested layout group also reports flexible size. Explicitly override it for
            // fixed controls; only the caller that owns a growing content area opts back in.
            layout.flexibleHeight = 0;
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
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
            return rect;
        }

        public static void Pin(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }

        public static void Overlay(RectTransform rect, Vector2 min, Vector2 max, Vector2 lower, Vector2 upper)
        {
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = lower; rect.offsetMax = upper;
        }

        public Button FloatingMenu(Transform parent, Action action)
        {
            var rect = Rect("Menu button", parent);
            Pin(rect, Vector2.one, Vector2.one, new Vector2(-20, -20), new Vector2(80, 80));
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var graphic = rect.gameObject.AddComponent<RoundMenuGraphic>();
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = graphic;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var label = Label(rect, "메뉴", 18, TextColor, 30, TextAnchor.MiddleCenter);
            Overlay(label.rectTransform, new Vector2(.12f, .08f), new Vector2(.88f, .43f), Vector2.zero, Vector2.zero);
            button.onClick.AddListener(() => action());
            return button;
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
            bool primary = false, float height = 80)
        {
            var rect = Rect("Button " + label, parent); Size(rect, height, 1);
            var image = Background(rect, primary ? Gold : Hex("2B3850"), true);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
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

        public RectTransform Modal(Transform parent, string name, string title, Action close, out RectTransform content)
        {
            var overlay = Rect(name, parent); Stretch(overlay);
            overlay.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Background(overlay, new Color(.04f, .05f, .09f, .97f), true);
            var panel = Stack(overlay, "Menu panel", 16, 12); Stretch(panel);
            var header = Row(panel, 80);
            Label(header, title, 30, Gold, 80);
            var button = Button(header, "닫기", close, height: 80);
            var size = button.GetComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = 104; size.flexibleWidth = 0;
            content = Scroll(panel);
            return overlay;
        }

        public void Controls(Transform parent)
        {
            Label(parent, "Call을 보고, 같은 리듬으로 대응해.", 27, Gold, 52);
            var card = Card(parent, 18);
            Label(card, "Tap  ·  박자에 맞춰 누르기", 25, null, 54);
            Label(card, "Hold  ·  누르고 끝까지 유지하기", 25, null, 54);
            Label(card, "Dive  ·  누르고 마지막 박자에 떼기", 25, null, 54);
            Label(card, "Flick  ·  미리 누른 뒤 튕기며 떼기", 25, null, 54);
            Label(card, "Shake  ·  목표 박자 전후의 반미스 범위 안에서 한 번 왕복\n왕복 완료면 성공 / 미완료면 미스", 24, Teal, 90);
            Label(parent, "메뉴 버튼 외에는 화면 어디서든 연주할 수 있어.\n마우스 왼쪽 버튼이나 한 손가락을 사용해.", 23, Muted, 86);
            Label(parent, "메뉴를 열면 박자와 판정이 멈춰.\n유지 중에 멈췄다면 이어하기 후 화면을 다시 눌러줘.", 23, Muted, 86);
            Label(parent, "받는 피해: 미스 100% · 반미스 50% · 퍼펙트 0%\nHP가 0이 되면 탐험이 끝나. 준비로 돌아가도 HP는 유지돼.", 23, Muted, 86);
        }
    }
}
