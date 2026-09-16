using TMPro;
using System;
using System.Collections.Generic;
using System.IO;
using BBSB.Runtime;
using BBSB.Runtime.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace BBSB.Editor
{
    // Creates editable assets once. Never regenerate over artist edits.
    public static class PresentationPrefabBuilder
    {
        private const string Folder = "Assets/BBSB/Resources/BBSB/Presentation";
        private static TMP_FontAsset Font => PresentationFonts.Load();
        [InitializeOnLoadMethod] private static void Schedule() => EditorApplication.delayCall += Ensure;
        [MenuItem("BBSB/Presentation/Create missing Canvas and actor prefabs")]
        public static void Ensure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            PresentationFontBuilder.Ensure();
            string path = Folder + "/PresentationPrefabs.asset";
            if (AssetDatabase.LoadAssetAtPath<PresentationPrefabs>(path) == null)
            {
                var library = ScriptableObject.CreateInstance<PresentationPrefabs>();
                library.defaultFont = Font; library.textMeshProVersion = 1;
                library.primaryButton = CreateButtonPrefab("PrimaryButton", new Color(.95f, .79f, .47f), Color.black);
                library.secondaryButton = CreateButtonPrefab("SecondaryButton", new Color(.17f, .22f, .31f), Color.white);
                library.titleText = CreateTextPrefab("TitleText", 48); library.headingText = CreateTextPrefab("HeadingText", 28);
                library.bodyText = CreateTextPrefab("BodyText", 24); library.captionText = CreateTextPrefab("CaptionText", 19);
                var card = Rect("Card", null); Background(card, new Color(.11f, .14f, .21f));
                var stack = card.gameObject.AddComponent<VerticalLayoutGroup>(); stack.padding = new RectOffset(18, 18, 18, 18);
                stack.spacing = 12; stack.childControlHeight = stack.childControlWidth = true; stack.childForceExpandHeight = false;
                library.card = Save(card.gameObject, "Card").GetComponent<RectTransform>(); UnityEngine.Object.DestroyImmediate(card.gameObject);
                var screens = new List<PresentationPrefabs.Screen>();
                foreach (RunScreenKind kind in Enum.GetValues(typeof(RunScreenKind))) screens.Add(new PresentationPrefabs.Screen { kind = kind, prefab = CreateScreen(kind) });
                library.screens = screens.ToArray(); AssetDatabase.CreateAsset(library, path); EditorUtility.SetDirty(library);
            }
            // Add only the new screens to existing catalogs; retain all authored prefab changes.
            var catalog = AssetDatabase.LoadAssetAtPath<PresentationPrefabs>(path);
            var entries = new List<PresentationPrefabs.Screen>(catalog.screens);
            foreach (var kind in new[] { RunScreenKind.FiveLaneBattle, RunScreenKind.FiveLanePreparation })
                if (!entries.Exists(entry => entry != null && entry.kind == kind && entry.prefab != null))
                { entries.RemoveAll(entry => entry != null && entry.kind == kind); entries.Add(new PresentationPrefabs.Screen { kind = kind, prefab = CreateScreen(kind) }); }
            if (entries.Count != catalog.screens.Length || Array.Exists(catalog.screens, entry => entry != null && entry.prefab == null))
            { catalog.screens = entries.ToArray(); EditorUtility.SetDirty(catalog); }
            foreach (var entry in catalog.screens)
                if (entry != null && entry.prefab != null) RepairCollapsedScreen(entry.prefab);
            if (Resources.Load<PlayerAuthoring>(PlayerAuthoring.ResourcePath) == null)
            {
                var asset = ScriptableObject.CreateInstance<PlayerAuthoring>();
                asset.portrait = Resources.Load<Sprite>("BBSB/BattleArt/weapon-master");
                foreach (var sprite in Resources.LoadAll<Sprite>(PlayerMotionSprites.ResourcePath + "idle")) if (sprite.name == "idle_0") asset.portrait = sprite;
                asset.layout = Resources.Load<PlayerMotionDisplay>(PlayerMotionDisplay.ResourcePath);
                asset.spriteReferenceHeight = asset.portrait != null ? asset.portrait.bounds.size.y : 4;
                var root = new GameObject("PlayerVisual", typeof(Animator));
                var body = new GameObject("Body", typeof(SpriteRenderer)); body.transform.SetParent(root.transform, false);
                body.GetComponent<SpriteRenderer>().sprite = asset.portrait;
                asset.visualPrefab = Save(root, "PlayerVisual"); UnityEngine.Object.DestroyImmediate(root);
                AssetDatabase.CreateAsset(asset, "Assets/BBSB/Resources/BBSB/Player.asset");
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("BBSB/Presentation/Open prefab catalog")]
        private static void Open() { Ensure(); Selection.activeObject = Resources.Load<PresentationPrefabs>(PresentationPrefabs.ResourcePath); }
        [MenuItem("BBSB/Presentation/Create SpriteRenderer prefab for selected monster")]
        private static void MonsterPrefab()
        {
            var monster = Selection.activeObject as MonsterAuthoring;
            if (monster == null) { Debug.LogWarning("Project 창에서 Monster 에셋을 선택해줘."); return; }
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var root = new GameObject(monster.displayName, typeof(Animator)); var body = new GameObject("Body", typeof(SpriteRenderer));
            body.transform.SetParent(root.transform, false); body.GetComponent<SpriteRenderer>().sprite = monster.portrait;
            float height = monster.portrait != null ? monster.portrait.bounds.size.y : 4;
            string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + monster.monsterId + "-Visual.prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path); UnityEngine.Object.DestroyImmediate(root);
            Undo.RecordObject(monster, "Assign monster SpriteRenderer prefab"); monster.actorPrefab = prefab;
            monster.spriteReferenceHeight = height; EditorUtility.SetDirty(monster); AssetDatabase.SaveAssets();
            AssetDatabase.OpenAsset(prefab);
        }
        private static CanvasScreen CreateScreen(RunScreenKind kind)
        {
            // A standalone overlay Canvas drives its RectTransform from the editor's current
            // display, which may be zero during import. Build beneath a fixed-size Canvas instead.
            var preview = Rect("Screen prefab authoring canvas", null);
            preview.gameObject.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            preview.localScale = Vector3.one; preview.sizeDelta = new Vector2(1280, 720);
            try { return CreateScreenContent(kind, preview); }
            finally { UnityEngine.Object.DestroyImmediate(preview.gameObject); }
        }
        private static CanvasScreen CreateScreenContent(RunScreenKind kind, Transform preview)
        {
            var root = Rect(kind + "Screen", preview); root.sizeDelta = new Vector2(1280, 720);
            root.gameObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root.gameObject.AddComponent<GraphicRaycaster>(); root.gameObject.AddComponent<CanvasGroup>();
            var view = root.gameObject.AddComponent<CanvasScreen>();
            var backdrop = Rect("Background", root); Stretch(backdrop); Background(backdrop, new Color(.06f, .075f, .13f));
            view.content = Rect("Content", root); Stretch(view.content);
            if (kind == RunScreenKind.FiveLaneBattle)
                view.fiveLane = FiveLaneHudBindings.CreateDefault(view.content, Font);
            if (kind == RunScreenKind.Title)
            {
                var bindings = root.gameObject.AddComponent<TitleScreenBindings>(); view.title = bindings;
                Place(Text("Title", view.content, "Beat! Block, Shake~ Beat!", 48), .12f, .60f, .88f, .88f);
                Place(Text("Subtitle", view.content, "다섯 무기로 지휘하는 하나의 리듬", 26), .15f, .48f, .85f, .59f);
                bindings.mapName = Text("Map name", view.content, "랜덤 맵", 26); Place(bindings.mapName, .3f, .34f, .7f, .43f);
                bindings.previousMap = Button("Previous map", view.content, "이전 맵"); Place(bindings.previousMap, .13f, .34f, .28f, .43f);
                bindings.nextMap = Button("Next map", view.content, "다음 맵"); Place(bindings.nextMap, .72f, .34f, .87f, .43f);
                bindings.start = Button("Start", view.content, "탐험 시작"); Place(bindings.start, .15f, .12f, .48f, .25f);
                bindings.codex = Button("Codex", view.content, "몬스터 도감"); Place(bindings.codex, .52f, .12f, .85f, .25f);
            }
            if (kind == RunScreenKind.Preparation)
            {
                var b = root.gameObject.AddComponent<PreparationScreenBindings>(); view.preparation = b;
                b.portrait = Background(Rect("Player portrait", view.content), Color.white); b.portrait.preserveAspect = true;
                Place(b.portrait, .02f, .86f, .075f, .98f);
                b.playerName = Text("Player name", view.content, "WEAPON MASTER", 22); Place(b.playerName, .08f, .88f, .28f, .96f);
                b.song = Text("Song", view.content, "SONG / BPM", 26); Place(b.song, .3f, .88f, .75f, .98f);
                b.menu = Button("Menu", view.content, "메뉴"); Place(b.menu, .9f, .9f, .98f, .98f);
                b.codex = Button("Codex", view.content, "도감"); Place(b.codex, .8f, .9f, .88f, .98f);
                b.playerHealth = Text("Player HP", view.content, "HP", 20); Place(b.playerHealth, .02f, .8f, .45f, .86f);
                b.enemyHealth = Text("Enemy HP", view.content, "MONSTER HP", 20); Place(b.enemyHealth, .55f, .8f, .98f, .86f);
                b.playerFill = Bar("Player health", view.content, .02f, .78f, .45f, .795f);
                b.enemyFill = Bar("Enemy health", view.content, .55f, .78f, .98f, .795f);
                var scrollRoot = Rect("Pattern list", view.content); Place(scrollRoot, .02f, .23f, .98f, .76f);
                Background(scrollRoot, new Color(.1f, .13f, .2f)).raycastTarget = true;
                b.patterns = scrollRoot.gameObject.AddComponent<ScrollRect>(); b.patterns.horizontal = false;
                b.patterns.viewport = Rect("Viewport", scrollRoot); Stretch(b.patterns.viewport); b.patterns.viewport.gameObject.AddComponent<RectMask2D>();
                b.patterns.content = Rect("Content", b.patterns.viewport);
                b.patterns.content.anchorMin = new Vector2(0, 1); b.patterns.content.anchorMax = Vector2.one;
                b.patterns.content.pivot = new Vector2(.5f, 1); b.patterns.content.sizeDelta = Vector2.zero;
                var stack = b.patterns.content.gameObject.AddComponent<VerticalLayoutGroup>(); stack.spacing = 12;
                stack.childControlWidth = stack.childControlHeight = true; stack.childForceExpandHeight = false;
                b.patterns.content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                b.weapons = Rect("Five weapon slots", view.content); Place(b.weapons, .02f, .035f, .78f, .19f);
                b.start = Button("Start performance", view.content, "연주 시작"); Place(b.start, .8f, .035f, .98f, .19f);
            }
            if (kind == RunScreenKind.Battle)
            {
                var b = root.gameObject.AddComponent<BattleHudBindings>(); view.battle = b;
                b.arena = Rect("Arena", view.content); Stretch(b.arena);
                var hud = Rect("HUD", view.content); Stretch(hud);
                b.song = Text("Song", hud, "SONG / BPM", 25); Place(b.song, .02f, .89f, .5f, .97f);
                b.health = Text("Player HP", hud, "HP 100 / 100", 25); Place(b.health, .02f, .78f, .32f, .85f);
                b.enemyHealth = Text("Enemy HP", hud, "MONSTER HP", 25); Place(b.enemyHealth, .55f, .78f, .85f, .85f);
                b.combo = Text("Combo", hud, "COMBO 0", 34); Place(b.combo, .3f, .08f, .7f, .16f);
                b.feedback = Text("Feedback", hud, "", 23); Place(b.feedback, .3f, .02f, .7f, .08f);
                b.damage = Text("Damage", hud, "", 22); Place(b.damage, .02f, .68f, .32f, .74f);
                b.songFill = Bar("Song progress", hud, 0, .992f, 1, 1);
                b.healthFill = Bar("Player health", hud, .02f, .755f, .32f, .77f);
                b.enemyFill = Bar("Enemy health", hud, .55f, .755f, .85f, .77f);
                b.menu = Button("Menu", hud, "메뉴"); Place(b.menu, .89f, .87f, .98f, .97f);
            }
            return Save(root.gameObject, kind + "Screen").GetComponent<CanvasScreen>();
        }
        private static void RepairCollapsedScreen(CanvasScreen screen)
        {
            var root = (RectTransform)screen.transform;
            if (root.localScale != Vector3.zero) return;
            // Repair only the known invalid root state; retain content, controls and bindings.
            root.localScale = Vector3.one;
            if (root.rect.width <= 0 || root.rect.height <= 0)
            {
                root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
                root.anchoredPosition = Vector2.zero; root.sizeDelta = new Vector2(1280, 720);
            }
            EditorUtility.SetDirty(root);
            PrefabUtility.SavePrefabAsset(screen.gameObject);
        }
        private static RectTransform Rect(string name, Transform parent)
        { var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); if (parent != null) r.SetParent(parent, false); return r; }
        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        private static Image Background(RectTransform r, Color color)
        { var i = r.gameObject.AddComponent<Image>(); i.color = color; i.raycastTarget = false; return i; }
        private static TextMeshProUGUI Text(string name, Transform parent, string text, int size)
        { var r = Rect(name, parent); var t = r.gameObject.AddComponent<TextMeshProUGUI>(); t.font = Font; t.fontSize = size; t.text = text; t.color = Color.white; t.raycastTarget = false; t.richText = false; t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Truncate; t.alignment = TextAlignmentOptions.Center; return t; }
        private static Button Button(string name, Transform parent, string label)
        {
            var r = Rect(name, parent); r.sizeDelta = new Vector2(260, 72); var image = Background(r, new Color(.17f, .22f, .31f)); image.raycastTarget = true;
            var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var t = Text("Label", r, label, 24); Stretch(t.rectTransform); return button;
        }
        private static void Place(Component c, float x0, float y0, float x1, float y1)
        { var r = (RectTransform)c.transform; r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero; }
        private static RectTransform Bar(string name, Transform parent, float x0, float y0, float x1, float y1)
        { var root = Rect(name, parent); Place(root, x0, y0, x1, y1); Background(root, new Color(.2f, .24f, .3f)); var fill = Rect("Fill", root); Stretch(fill); Background(fill, new Color(.5f, .85f, .77f)); return fill; }
        private static GameObject Save(GameObject source, string name)
        {
            string path = Folder + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return existing != null ? existing : PrefabUtility.SaveAsPrefabAsset(source, path);
        }
        private static TextMeshProUGUI CreateTextPrefab(string name, int size)
        { var text = Text(name, null, name, size); text.rectTransform.sizeDelta = new Vector2(400, 60); var result = Save(text.gameObject, name).GetComponent<TextMeshProUGUI>(); UnityEngine.Object.DestroyImmediate(text.gameObject); return result; }
        private static Button CreateButtonPrefab(string name, Color background, Color foreground)
        { var button = Button(name, null, name); button.GetComponent<Image>().color = background; button.GetComponentInChildren<TextMeshProUGUI>().color = foreground; var result = Save(button.gameObject, name).GetComponent<Button>(); UnityEngine.Object.DestroyImmediate(button.gameObject); return result; }
    }
}
