using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    // Serialized references allow the whole battle screen to be edited as an ordinary Canvas prefab.
    public sealed class FiveLaneHudBindings : MonoBehaviour
    {
        public RectTransform scenery, actors, tracks;
        public TextMeshProUGUI song, health, enemyHealth, beat, feedback, help;
        public RectTransform playerFill, enemyFill;
        public Button pause;
        public RectTransform[] weaponRoots = new RectTransform[5];
        public RectTransform[] inputAreas = new RectTransform[5];
        public TextMeshProUGUI[] laneLabels = new TextMeshProUGUI[5];
        public TextMeshProUGUI[] laneStatus = new TextMeshProUGUI[5];
        public TextMeshProUGUI[] laneResults = new TextMeshProUGUI[5];

        public static FiveLaneHudBindings CreateDefault(RectTransform parent, TMP_FontAsset font)
        {
            var ui = new RunUI(font);
            var root = ui.Rect("Five lane HUD", parent); RunUI.Stretch(root);
            var b = root.gameObject.AddComponent<FiveLaneHudBindings>();
            b.scenery = ui.Rect("Scenery", root); RunUI.Stretch(b.scenery);
            ui.Background(b.scenery, RunUI.Ink);
            b.actors = ui.Rect("Actors", root); RunUI.Stretch(b.actors);
            b.tracks = ui.Rect("Note tracks", root); RunUI.Stretch(b.tracks);
            b.song = Text(ui, root, "SONG", 21, .30f, .93f, .70f, .99f);
            b.health = Text(ui, root, "HP", 22, .02f, .93f, .28f, .99f);
            b.enemyHealth = Text(ui, root, "ENEMY", 22, .72f, .93f, .90f, .99f);
            b.playerFill = Bar(ui, root, "Player HP", .02f, .91f, .28f, .925f, RunUI.Teal);
            b.enemyFill = Bar(ui, root, "Enemy HP", .72f, .91f, .90f, .925f, RunUI.Red);
            b.beat = Text(ui, root, "1 · 2 · 3 · 4", 25, .30f, .855f, .70f, .92f);
            b.feedback = Text(ui, root, "", 29, .30f, .785f, .70f, .85f);
            b.help = Text(ui, root, "D / F / SPACE / J / K  ·  Tap / Hold  ·  Esc", 18, .10f, .005f, .90f, .045f);
            for (int i = 0; i < 5; i++)
            {
                float x = .12f + .19f * i;
                var point = FiveLaneTrackGraphic.LanePoint(i, 0);
                b.weaponRoots[i] = ui.Rect("Weapon " + i, root);
                Place(b.weaponRoots[i], point.x - .052f, point.y - .07f, point.x + .052f, point.y + .07f);
                b.laneLabels[i] = Text(ui, root, "", 21, x - .09f, .105f, x + .09f, .20f);
                b.laneStatus[i] = Text(ui, root, "READY", 17, x - .09f, .05f, x + .09f, .10f);
                b.laneResults[i] = Text(ui, root, "", 20, x - .09f, .205f, x + .09f, .255f);
                b.inputAreas[i] = ui.Rect("Input lane " + i, root);
                Place(b.inputAreas[i], i * .2f, .045f, (i + 1) * .2f, .77f);
                ui.Background(b.inputAreas[i], Color.clear, true);
            }
            b.pause = ui.Button(root, "II", () => { });
            Place((RectTransform)b.pause.transform, .925f, .905f, .985f, .985f);
            return b;
        }
        private static TextMeshProUGUI Text(RunUI ui, Transform parent, string value, int size, float x0, float y0, float x1, float y1)
        {
            var text = ui.Label(parent, value, size);
            // Apply explicitly: project text prefabs can carry their own alignment/font size.
            text.fontSize = size; text.alignment = TextAlignmentOptions.Center;
            Place(text.rectTransform, x0, y0, x1, y1); return text;
        }
        private static RectTransform Bar(RunUI ui, Transform parent, string name, float x0, float y0, float x1, float y1, Color color)
        {
            var track = ui.Rect(name, parent); Place(track, x0, y0, x1, y1); ui.Background(track, RunUI.Panel);
            var fill = ui.Rect("Fill", track); RunUI.Stretch(fill); ui.Background(fill, color); return fill;
        }
        internal static void Place(RectTransform rect, float x0, float y0, float x1, float y1)
        { rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1); rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
