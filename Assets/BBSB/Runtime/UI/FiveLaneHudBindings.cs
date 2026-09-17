using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    // Serialized references allow the whole battle screen to be edited as an ordinary Canvas prefab.
    public sealed class FiveLaneHudBindings : MonoBehaviour
    {
        public RectTransform scenery, actors, tracks;
        public RectTransform playerSlot, monsterArea;
        public ShoulderViewPresentation presentation;
        public TextMeshProUGUI song, health, enemyHealth, beat, feedback, help;
        public TextMeshProUGUI attackCue;
        [Tooltip("Centre the short tracks for the number of equipped weapons. Disable to use custom prefab positions.")]
        public bool arrangeEquippedLanes = true;
        public RectTransform playerFill, enemyFill;
        public Button pause;
        public RectTransform[] weaponRoots = new RectTransform[5];
        public RectTransform[] judgmentPoints = new RectTransform[5];
        public RectTransform[] inputAreas = new RectTransform[5];
        public TextMeshProUGUI[] laneLabels = new TextMeshProUGUI[5];
        public TextMeshProUGUI[] laneStatus = new TextMeshProUGUI[5];
        public TextMeshProUGUI[] laneResults = new TextMeshProUGUI[5];
        [SerializeField, HideInInspector] private int stageLayoutVersion;

        public bool TryValidate(out string problem)
        {
            problem = null;
            if (scenery == null || actors == null || tracks == null || song == null || health == null ||
                enemyHealth == null || beat == null || feedback == null || help == null ||
                playerFill == null || enemyFill == null || pause == null)
                problem = "FiveLaneHudBindings is missing a stage, text, HP bar or pause reference.";
            else if (!Complete(weaponRoots) || !Complete(inputAreas) || !Complete(laneLabels) || !Complete(laneStatus) || !Complete(laneResults))
                problem = "FiveLaneHudBindings requires five non-null entries in each weapon, input and label array.";
            return problem == null;
        }
        private static bool Complete<T>(T[] entries) where T : Object
        {
            if (entries == null || entries.Length != 5) return false;
            foreach (var entry in entries) if (entry == null) return false;
            return true;
        }

        // Upgrade only untouched positions from the first generated HUD. Artist-edited
        // anchors, offsets, graphics and text styles remain owned by the prefab.
        public bool EnsureStageLayout()
        {
            if (!TryValidate(out _)) return false;
            bool changed = false;
            var ui = new RunUI(song.font);
            if (attackCue == null)
            {
                attackCue = Text(ui, transform, "", 24, .40f, .82f, .86f, .89f);
                attackCue.color = RunUI.Red; changed = true;
            }
            if (playerSlot == null)
            { playerSlot = ui.Rect("Player stage slot", actors); Place(playerSlot, .025f, .15f, .345f, .71f); changed = true; }
            if (monsterArea == null)
            { monsterArea = ui.Rect("Monster stage area", actors); Place(monsterArea, .39f, .50f, .86f, .88f); changed = true; }
            if (judgmentPoints == null || judgmentPoints.Length != 5)
            { judgmentPoints = new RectTransform[5]; changed = true; }
            for (int i = 0; i < 5; i++)
                if (judgmentPoints[i] == null)
                {
                    var point = FiveLaneTrackGraphic.LanePoint(i, 0);
                    judgmentPoints[i] = ui.Rect("Judgment point " + i, tracks);
                    Place(judgmentPoints[i], point.x - .035f, point.y - .025f, point.x + .035f, point.y + .025f);
                    changed = true;
                }
            if (stageLayoutVersion >= 2) return changed;
            if (stageLayoutVersion < 1)
            {
                MoveDefault(song.rectTransform, .30f, .93f, .70f, .99f, .72f, .025f, .92f, .09f);
                MoveDefault(health.rectTransform, .02f, .93f, .28f, .99f, .025f, .085f, .29f, .14f);
                MoveDefault((RectTransform)playerFill.parent, .02f, .91f, .28f, .925f, .025f, .06f, .29f, .08f);
                MoveDefault(enemyHealth.rectTransform, .72f, .93f, .90f, .99f, .40f, .93f, .86f, .985f);
                MoveDefault((RectTransform)enemyFill.parent, .72f, .91f, .90f, .925f, .40f, .90f, .86f, .92f);
                MoveDefault(beat.rectTransform, .30f, .855f, .70f, .92f, .82f, .54f, .98f, .64f);
                MoveDefault(feedback.rectTransform, .30f, .785f, .70f, .85f, .48f, .32f, .81f, .40f);
                MoveDefault(help.rectTransform, .10f, .005f, .90f, .045f, .36f, .01f, .70f, .05f);
                for (int i = 0; i < 5; i++)
                {
                    float oldX = .12f + .19f * i, oldY = .32f + .045f * (2 - Mathf.Abs(i - 2));
                    float x = FiveLaneTrackGraphic.LanePoint(i, 0).x;
                    var weapon = WeaponPoint(i);
                    MoveDefault(weaponRoots[i], oldX - .052f, oldY - .07f, oldX + .052f, oldY + .07f,
                        weapon.x - .052f, weapon.y - .07f, weapon.x + .052f, weapon.y + .07f);
                    MoveDefault(laneLabels[i].rectTransform, oldX - .09f, .105f, oldX + .09f, .20f, x - .06f, .105f, x + .06f, .17f);
                    MoveDefault(laneStatus[i].rectTransform, oldX - .09f, .05f, oldX + .09f, .10f, x - .06f, .065f, x + .06f, .10f);
                    MoveDefault(laneResults[i].rectTransform, oldX - .09f, .205f, oldX + .09f, .255f, x - .06f, .255f, x + .06f, .30f);
                    MoveDefault(inputAreas[i], i * .2f, .045f, (i + 1) * .2f, .77f, x - .065f, .06f, x + .065f, .48f);
                }
            }
            MoveDefault(playerSlot, .025f, .15f, .345f, .71f, .015f, .15f, .385f, .80f);
            MoveDefault(monsterArea, .39f, .50f, .86f, .88f, .43f, .50f, .91f, .86f);
            // Keep the global countdown clear of per-enemy attack labels.
            MoveDefault(attackCue.rectTransform, .40f, .82f, .86f, .89f, .015f, .82f, .38f, .88f);
            stageLayoutVersion = 2;
            return true;
        }
        public void ConfigureLanes(int count)
        {
            for (int i = 0; i < 5; i++)
            {
                bool active = i < count;
                weaponRoots[i].gameObject.SetActive(active); judgmentPoints[i].gameObject.SetActive(active);
                inputAreas[i].gameObject.SetActive(active); laneLabels[i].gameObject.SetActive(active);
                laneStatus[i].gameObject.SetActive(active); laneResults[i].gameObject.SetActive(active);
                if (!active || !arrangeEquippedLanes) continue;
                float x = FiveLaneTrackGraphic.LanePoint(i, 0, count).x;
                Place(judgmentPoints[i], x - .035f, .195f, x + .035f, .245f);
                Place(laneLabels[i].rectTransform, x - .065f, .105f, x + .065f, .175f);
                laneLabels[i].fontSize = 18;
                Place(laneStatus[i].rectTransform, x - .065f, .065f, x + .065f, .10f);
                Place(laneResults[i].rectTransform, x - .065f, .435f, x + .065f, .485f);
                Place(inputAreas[i], x - .065f, .06f, x + .065f, .49f);
            }
            if (arrangeEquippedLanes) Place(feedback.rectTransform, .40f, .49f, .81f, .55f);
        }
        private static Vector2 WeaponPoint(int slot)
        {
            switch (slot)
            {
                case 0: return new Vector2(.075f, .64f);
                case 1: return new Vector2(.28f, .76f);
                case 2: return new Vector2(.08f, .33f);
                case 3: return new Vector2(.30f, .52f);
                default: return new Vector2(.31f, .25f);
            }
        }
        private static void MoveDefault(RectTransform rect, float x0, float y0, float x1, float y1,
            float nextX0, float nextY0, float nextX1, float nextY1)
        {
            if (rect != null && rect.anchorMin == new Vector2(x0, y0) && rect.anchorMax == new Vector2(x1, y1) &&
                rect.offsetMin == Vector2.zero && rect.offsetMax == Vector2.zero)
                Place(rect, nextX0, nextY0, nextX1, nextY1);
        }

        public static FiveLaneHudBindings CreateDefault(RectTransform parent, TMP_FontAsset font)
        {
            var ui = new RunUI(font);
            var root = ui.Rect("Five lane HUD", parent); RunUI.Stretch(root);
            var b = root.gameObject.AddComponent<FiveLaneHudBindings>();
            b.presentation = Resources.Load<ShoulderViewPresentation>(ShoulderViewPresentation.ResourcePath);
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
                var point = new Vector2(x, .32f + .045f * (2 - Mathf.Abs(i - 2)));
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
            b.EnsureStageLayout();
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
