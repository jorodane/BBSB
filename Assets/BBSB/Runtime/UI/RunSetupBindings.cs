using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    // Serialized slots let the setup screen be arranged in an ordinary Canvas prefab.
    public sealed class RunSetupBindings : MonoBehaviour
    {
        public RectTransform characterList, boardRoot, weaponList;
        public Image portrait;
        public TMP_Text characterName, description, capacity, status;
        public Button start, back;
        public bool IsValid => characterList != null && boardRoot != null && weaponList != null && portrait != null &&
            characterName != null && description != null && capacity != null && status != null && start != null && back != null;

        public static RunSetupBindings CreateDefault(RectTransform parent, TMP_FontAsset font)
        {
            var ui = new RunUI(font);
            var b = parent.gameObject.AddComponent<RunSetupBindings>();
            var heading = Caption(ui, parent, "캐릭터 · 시작 무기 편성", 30, RunUI.Gold);
            Place(heading.rectTransform, .025f, .90f, .78f, .98f);
            b.back = ui.Button(parent, "돌아가기", () => { }, height: 54);
            Place((RectTransform)b.back.transform, .84f, .91f, .98f, .985f);

            var left = ui.Rect("Character selection", parent); Place(left, .025f, .035f, .275f, .88f);
            ui.Background(left, RunUI.Panel);
            var portrait = ui.Rect("Selected character portrait", left); Place(portrait, .04f, .40f, .96f, .98f);
            b.portrait = ui.Background(portrait, Color.white); b.portrait.preserveAspect = true;
            b.characterName = Caption(ui, left, "", 25, RunUI.TextColor); Place(b.characterName.rectTransform, .06f, .33f, .94f, .40f);
            b.description = Caption(ui, left, "", 18, RunUI.Muted); Place(b.description.rectTransform, .06f, .23f, .94f, .33f);
            b.characterList = ui.Scroll(left);
            Place((RectTransform)b.characterList.GetComponentInParent<ScrollRect>().transform, .04f, .02f, .96f, .22f);

            var right = ui.Stack(parent, "Starting weapon arrangement", 0, 10); Place(right, .30f, .15f, .98f, .88f);
            b.boardRoot = ui.Rect("Starting placement board slot", right); RunUI.Size(b.boardRoot, 178);
            b.capacity = Caption(ui, right, "", 21, RunUI.Gold); RunUI.Size(b.capacity.rectTransform, 30, 1);
            b.weaponList = ui.Scroll(right);
            b.status = Caption(ui, parent, "", 19, RunUI.Muted); Place(b.status.rectTransform, .30f, .035f, .74f, .13f);
            b.start = ui.Button(parent, "편성하고 시작", () => { }, primary: true, height: 64);
            Place((RectTransform)b.start.transform, .76f, .035f, .98f, .13f);
            return b;
        }
        private static TMP_Text Caption(RunUI ui, RectTransform parent, string text, int size, Color color)
        {
            var label = ui.Label(parent, text, size, color);
            label.fontSize = size; label.color = color; label.alignment = TextAlignmentOptions.Left;
            label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }
        private static void Place(RectTransform rect, float x0, float y0, float x1, float y1) =>
            RunUI.Overlay(rect, new Vector2(x0, y0), new Vector2(x1, y1), Vector2.zero, Vector2.zero);
    }
}
