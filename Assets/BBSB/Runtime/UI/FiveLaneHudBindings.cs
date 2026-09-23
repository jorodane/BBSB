using TMPro;
using BBSB.Core;
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
        public BattleVisualTheme visualTheme;
        public TextMeshProUGUI song, health, beat, feedback, help;
        public TextMeshProUGUI countIn, rhythmBeat, stageBadge;
        // Retain the serialized references only to retire widgets in existing prefabs.
        [HideInInspector] public TextMeshProUGUI enemyHealth;
        public TextMeshProUGUI attackCue;
        [Tooltip("Arrange S/D/F/Space/J/K/L on the perspective board. Disable to use custom prefab positions.")]
        public bool arrangeEquippedLanes = true;
        public RectTransform playerFill;
        [HideInInspector] public RectTransform enemyFill;
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
                beat == null || feedback == null || help == null || playerFill == null || pause == null)
                problem = "FiveLaneHudBindings is missing a stage, text, HP bar or pause reference.";
            else if (!Complete(weaponRoots) || !Complete(inputAreas) || !Complete(laneLabels) || !Complete(laneStatus) || !Complete(laneResults))
                problem = "FiveLaneHudBindings requires five non-null entries in each weapon, input and label array.";
            return problem == null;
        }
        private static bool Complete<T>(T[] entries) where T : Object
        {
            if (entries == null || entries.Length != 5 && entries.Length != BattleInputLayout.LaneCount) return false;
            foreach (var entry in entries) if (entry == null) return false;
            return true;
        }

        // Upgrade only untouched positions from the first generated HUD. Artist-edited
        // anchors, offsets, graphics and text styles remain owned by the prefab.
        public bool EnsureStageLayout()
        {
            if (!TryValidate(out _)) return false;
            bool changed = false;
            if (enemyHealth != null && enemyHealth.gameObject.activeSelf)
            { enemyHealth.gameObject.SetActive(false); changed = true; }
            if (enemyFill != null)
            {
                var bar = enemyFill.parent;
                // Older generated HUDs have a dedicated track parent. Keep a custom
                // shared HUD container alive if it also owns the player's health.
                var retired = bar != null && bar != transform && !transform.IsChildOf(bar) && !playerFill.IsChildOf(bar) ? bar : enemyFill;
                if (retired.gameObject.activeSelf) { retired.gameObject.SetActive(false); changed = true; }
            }
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
            if (judgmentPoints == null || judgmentPoints.Length != weaponRoots.Length)
            { judgmentPoints = new RectTransform[weaponRoots.Length]; changed = true; }
            for (int i = 0; i < 5; i++)
                if (judgmentPoints[i] == null)
                {
                    var point = FiveLaneTrackGraphic.LanePoint(i, 0);
                    judgmentPoints[i] = ui.Rect("Judgment point " + i, tracks);
                    Place(judgmentPoints[i], point.x - .035f, point.y - .025f, point.x + .035f, point.y + .025f);
                    changed = true;
                }
            if (countIn == null)
            { countIn = Text(ui, transform, "", 72, .25f, .54f, .75f, .75f); countIn.color = RunUI.Gold; changed = true; }
            if (rhythmBeat == null)
            { rhythmBeat = Text(ui, transform, "", 29, .875f, .36f, .975f, .48f); changed = true; }
            if (stageBadge == null)
            { stageBadge = Text(ui, transform, "", 18, .735f, .94f, .92f, .98f); changed = true; }
            beat.richText = true; rhythmBeat.richText = true;
            if (stageLayoutVersion >= 3) return changed;
            if (stageLayoutVersion >= 2)
            { UpgradeReferenceLayout(); stageLayoutVersion = 3; return true; }
            if (stageLayoutVersion < 1)
            {
                MoveDefault(song.rectTransform, .30f, .93f, .70f, .99f, .72f, .025f, .92f, .09f);
                MoveDefault(health.rectTransform, .02f, .93f, .28f, .99f, .025f, .085f, .29f, .14f);
                MoveDefault((RectTransform)playerFill.parent, .02f, .91f, .28f, .925f, .025f, .06f, .29f, .08f);
                MoveDefault(beat.rectTransform, .30f, .855f, .70f, .92f, .82f, .54f, .98f, .64f);
                MoveDefault(feedback.rectTransform, .30f, .785f, .70f, .85f, .48f, .32f, .81f, .40f);
                MoveDefault(help.rectTransform, .10f, .005f, .90f, .045f, .36f, .01f, .70f, .05f);
                for (int i = 0; i < 5; i++)
                {
                    float oldX = .12f + .19f * i, oldY = .32f + .045f * (2 - Mathf.Abs(i - 2));
                    float x = FiveLaneTrackGraphic.LanePoint(i, 0).x;
                    var weapon = LegacyWeaponPoint(i);
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
            UpgradeReferenceLayout(); stageLayoutVersion = 3;
            return true;
        }
        private void UpgradeReferenceLayout()
        {
            MoveDefault(playerSlot, .015f, .15f, .385f, .80f, .38f, .505f, .62f, .785f);
            MoveDefault(monsterArea, .43f, .50f, .91f, .86f, .23f, .735f, .84f, .955f);
            MoveDefault(song.rectTransform, .72f, .025f, .92f, .09f, .76f, .005f, .93f, .06f);
            MoveDefault(health.rectTransform, .025f, .085f, .29f, .14f, .605f, .48f, .735f, .515f);
            MoveDefault((RectTransform)playerFill.parent, .025f, .06f, .29f, .08f, .405f, .49f, .60f, .505f);
            var fillImage = playerFill.GetComponent<Image>();
            if (fillImage != null && fillImage.color == RunUI.Teal) fillImage.color = new Color(.86f, .15f, .23f);
            MoveDefault(beat.rectTransform, .82f, .54f, .98f, .64f, .015f, .355f, .18f, .49f);
            MoveDefault(feedback.rectTransform, .48f, .32f, .81f, .40f, .015f, .285f, .195f, .345f);
            MoveDefault(feedback.rectTransform, .40f, .49f, .81f, .55f, .015f, .285f, .195f, .345f);
            MoveDefault(help.rectTransform, .36f, .01f, .70f, .05f, .26f, .004f, .74f, .028f);
            MoveDefault(attackCue.rectTransform, .015f, .82f, .38f, .88f, .01f, .50f, .22f, .55f);
            for (int i = 0; i < 5; i++)
            {
                var previous = LegacyWeaponPoint(i); var next = WeaponPoint(i);
                MoveDefault(weaponRoots[i], previous.x - .052f, previous.y - .07f, previous.x + .052f, previous.y + .07f,
                    next.x - .06f, next.y - .085f, next.x + .06f, next.y + .085f);
            }
            beat.fontSize = 36; health.fontSize = 18; help.fontSize = 13; feedback.fontSize = 23; song.fontSize = 13;
        }
        public void ConfigureLanes(BBSB.Core.FiveLaneBattle battle)
        {
            EnsureExtensionSlots(battle.Extensions);
            for (int i = 0; i < BattleInputLayout.LaneCount; i++)
            {
                bool active = battle.LaneAt(i) != null;
                weaponRoots[i].gameObject.SetActive(active); judgmentPoints[i].gameObject.SetActive(active);
                inputAreas[i].gameObject.SetActive(active); laneLabels[i].gameObject.SetActive(battle.IsInputAvailable(i));
                laneStatus[i].gameObject.SetActive(active); laneResults[i].gameObject.SetActive(active);
                if (!arrangeEquippedLanes) continue;
                float x = FiveLaneTrackGraphic.InputPoint(i, 0, battle.Extensions).x;
                float headerX = FiveLaneTrackGraphic.InputPoint(i, 1, battle.Extensions).x;
                float half = (float)BattleBoardLayout.CellWidth(1, battle.Extensions) * .47f;
                Place(judgmentPoints[i], x - .025f, .055f, x + .025f, .075f);
                Place(laneLabels[i].rectTransform, headerX - half, .41f, headerX + half, .464f);
                laneLabels[i].fontSize = 17; laneLabels[i].enableAutoSizing = true;
                laneLabels[i].fontSizeMin = 11; laneLabels[i].fontSizeMax = 17;
                Place(laneStatus[i].rectTransform, headerX - half, .367f, headerX + half, .408f);
                laneStatus[i].fontSize = 12; laneStatus[i].enableAutoSizing = true;
                laneStatus[i].fontSizeMin = 9; laneStatus[i].fontSizeMax = 12;
                Place(laneResults[i].rectTransform, x - .07f, .095f, x + .07f, .14f);
                laneResults[i].fontSize = 17;
                // The raycast filter follows the trapezoid; the bounding box alone must
                // not let an outer lane steal the neighbouring lane's touch near the top.
                float nearHalf = (float)BattleBoardLayout.CellWidth(0, battle.Extensions) * .5f;
                Place(inputAreas[i], Mathf.Min(x - nearHalf, headerX - half), .025f,
                    Mathf.Max(x + nearHalf, headerX + half), .465f);
            }
        }
        private void EnsureExtensionSlots(InputExtensions extensions)
        {
            System.Array.Resize(ref weaponRoots, BattleInputLayout.LaneCount);
            System.Array.Resize(ref judgmentPoints, BattleInputLayout.LaneCount);
            System.Array.Resize(ref inputAreas, BattleInputLayout.LaneCount);
            System.Array.Resize(ref laneLabels, BattleInputLayout.LaneCount);
            System.Array.Resize(ref laneStatus, BattleInputLayout.LaneCount);
            System.Array.Resize(ref laneResults, BattleInputLayout.LaneCount);
            var ui = new RunUI(song.font);
            for (int i = BattleInputLayout.MainLaneCount; i < BattleInputLayout.LaneCount; i++)
            {
                float x = FiveLaneTrackGraphic.InputPoint(i, 0, extensions).x;
                string key = BattleInputLayout.Key(i);
                if (weaponRoots[i] == null)
                { var p = WeaponPoint(i); weaponRoots[i] = ui.Rect("Weapon " + key, actors); Place(weaponRoots[i], p.x - .045f, p.y - .075f, p.x + .045f, p.y + .075f); }
                if (judgmentPoints[i] == null)
                { judgmentPoints[i] = ui.Rect("Judgment point " + key, tracks); Place(judgmentPoints[i], x - .035f, .195f, x + .035f, .245f); }
                if (inputAreas[i] == null)
                {
                    inputAreas[i] = ui.Rect("Input lane " + key, transform); Place(inputAreas[i], x - .045f, .06f, x + .045f, .49f);
                    ui.Background(inputAreas[i], Color.clear, true);
                }
                if (laneLabels[i] == null) laneLabels[i] = Text(ui, transform, key, 18, x - .045f, .105f, x + .045f, .175f);
                if (laneStatus[i] == null) laneStatus[i] = Text(ui, transform, "", 17, x - .045f, .065f, x + .045f, .10f);
                if (laneResults[i] == null) laneResults[i] = Text(ui, transform, "", 20, x - .045f, .435f, x + .045f, .485f);
            }
        }
        private static Vector2 WeaponPoint(int slot)
        {
            switch (slot)
            {
                case 0: return new Vector2(.26f, .60f);
                case 1: return new Vector2(.355f, .64f);
                case 2: return new Vector2(.50f, .86f);
                case 3: return new Vector2(.65f, .64f);
                case 4: return new Vector2(.75f, .60f);
                case 5: return new Vector2(.17f, .61f);
                default: return new Vector2(.84f, .61f);
            }
        }
        private static Vector2 LegacyWeaponPoint(int slot)
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
            b.visualTheme = Resources.Load<BattleVisualTheme>(BattleVisualTheme.ResourcePath);
            b.scenery = ui.Rect("Scenery", root); RunUI.Stretch(b.scenery);
            ui.Background(b.scenery, RunUI.Ink);
            b.actors = ui.Rect("Actors", root); RunUI.Stretch(b.actors);
            b.tracks = ui.Rect("Note tracks", root); RunUI.Stretch(b.tracks);
            b.song = Text(ui, root, "SONG", 21, .30f, .93f, .70f, .99f);
            b.health = Text(ui, root, "HP", 22, .02f, .93f, .28f, .99f);
            b.playerFill = Bar(ui, root, "Player HP", .02f, .91f, .28f, .925f, RunUI.Teal);
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
