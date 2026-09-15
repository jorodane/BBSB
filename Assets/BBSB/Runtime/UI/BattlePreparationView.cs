using TMPro;
using System;
using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    /// <summary>Read-only monster rhythms and automatic weapon responses, with confirmed pattern practice.</summary>
    public sealed class BattlePreparationView : MonoBehaviour
    {
        public const string ArtRoot = "BBSB/PreparationArt/";
        private RunUI ui;
        private RunSession session;
        private Action<int> practice;
        private RectTransform page, viewport, confirmation;
        private CanvasGroup pageGroup;
        private PreparationScreenBindings bindings;
        private readonly MonsterAttackSprites sprites = new MonsterAttackSprites();
        private readonly List<Image> portraits = new List<Image>();
        private readonly List<RectTransform> monsterRows = new List<RectTransform>();
        private readonly List<Button> patternButtons = new List<Button>();
        private readonly List<WeaponIconGraphic> equippedIcons = new List<WeaponIconGraphic>();
        private readonly List<RectTransform> rhythmGraphs = new List<RectTransform>();
        private readonly List<RectTransform> responseAreas = new List<RectTransform>();
        private float lastHeight = -1;
        public Image HeroPortrait { get; private set; }
        public IReadOnlyList<Image> MonsterPortraits => portraits;
        public IReadOnlyList<RectTransform> MonsterRows => monsterRows;
        public IReadOnlyList<Button> PatternButtons => patternButtons;
        public IReadOnlyList<WeaponIconGraphic> EquippedIcons => equippedIcons;
        public Button StartButton { get; private set; }
        public PreparationGraphic PlayerHealthBar { get; private set; }
        public PreparationGraphic EnemyHealthBar { get; private set; }
        public int PendingPracticeIndex { get; private set; } = -1;
        public bool HasPracticeConfirmation => confirmation != null;

        internal void Bind(RunUI ui, RunSession session, Action<int> practice, Action codex, Action menu,
            Action start, Action<MonsterDefinition> monsterCodex)
        {
            this.ui = ui; this.session = session; this.practice = practice;
            var root = (RectTransform)transform;
            bindings = root.GetComponentInParent<CanvasScreen>()?.preparation;
            if (bindings != null)
            {
                if (!bindings.IsValid) throw new InvalidOperationException("준비 화면의 UI 참조를 모두 연결해줘.");
                page = root; pageGroup = root.GetComponent<CanvasGroup>() ?? root.gameObject.AddComponent<CanvasGroup>();
                HeroPortrait = bindings.portrait;
                var player = Resources.Load<PlayerAuthoring>(PlayerAuthoring.ResourcePath);
                HeroPortrait.sprite = player != null && player.portrait != null ? player.portrait : PlayerIdle();
                bindings.playerName.text = player != null ? player.displayName : "WEAPON MASTER";
                bindings.song.text = session.BattleMusic.Music.Name + " / " + session.BattleMusic.Music.Bpm + " BPM";
                bindings.playerHealth.text = "HP " + session.Health.ToString("0.##") + " / " + session.MaxHealth;
                bindings.enemyHealth.text = "MONSTER HP " + session.EnemyHealth.Current.ToString("0.##") + " / " + session.EnemyHealth.Maximum;
                bindings.playerFill.anchorMax = new Vector2((float)(session.Health / session.MaxHealth), 1);
                bindings.enemyFill.anchorMax = new Vector2(session.EnemyHealth.Maximum > 0 ? (float)(session.EnemyHealth.Current / session.EnemyHealth.Maximum) : 0, 1);
                bindings.menu.onClick.AddListener(() => menu()); bindings.codex.onClick.AddListener(() => codex());
                viewport = bindings.patterns.viewport;
                foreach (var monster in session.BattlePlan.Monsters) DrawMonster(bindings.patterns.content, monster.InstanceId, monster.Monster, monsterCodex);
                DrawEquipment(start); Canvas.ForceUpdateCanvases(); ReflowRows(); Canvas.ForceUpdateCanvases(); return;
            }
            ui.Background(root, RunUI.Ink); StageScenery.Add(ui, root, session.BattleMusic.Music, "Preparation scenery");
            var shade = ui.Rect("Preparation shade", root); RunUI.Stretch(shade);
            ui.Background(shade, new Color(.04f, .055f, .09f, .79f));
            page = ui.Rect("Pattern overview", root); RunUI.Stretch(page, 18);
            pageGroup = page.gameObject.AddComponent<CanvasGroup>();
            DrawHeader(codex, menu);
            var content = ui.Scroll(page);
            var scroll = content.GetComponentInParent<ScrollRect>(); viewport = scroll.viewport;
            RunUI.Overlay((RectTransform)scroll.transform, Vector2.zero, Vector2.one, new Vector2(0, 136), new Vector2(0, -98));
            scroll.GetComponent<Image>().color = Color.clear;
            content.GetComponent<VerticalLayoutGroup>().spacing = (float)PreparationLayout.RowGap;
            foreach (var monster in session.BattlePlan.Monsters)
                DrawMonster(content, monster.InstanceId, monster.Monster, monsterCodex);
            DrawEquipment(start);
            Canvas.ForceUpdateCanvases(); ReflowRows(); Canvas.ForceUpdateCanvases();
        }

        private void DrawHeader(Action codex, Action menu)
        {
            var header = ui.Rect("Preparation header", page);
            RunUI.Overlay(header, new Vector2(0, 1), Vector2.one, new Vector2(0, -92), Vector2.zero);
            HeroPortrait = Portrait(header, "Weapon master portrait", Resources.Load<PlayerAuthoring>(PlayerAuthoring.ResourcePath)?.portrait ?? Resources.Load<Sprite>(ArtRoot + "player") ?? PlayerIdle(), "W");
            RunUI.Pin((RectTransform)HeroPortrait.transform.parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -2), new Vector2(44, 44));
            var name = Text(header, "WEAPON MASTER", 20, RunUI.TextColor);
            Top(name.rectTransform, 0, .26f, 0, 26, 52);
            var epithet = Text(header, "다섯 현의 조종자", 15, RunUI.Muted);
            Top(epithet.rectTransform, 0, .26f, 28, 24, 52);
            var music = session.BattleMusic.Music;
            var song = Text(header, music.Name, 29, RunUI.Gold, TextAlignmentOptions.Center);
            Top(song.rectTransform, .27f, .75f, 0, 36);
            var subtitle = Text(header, session.Map.Theme.Genre + "  ·  " + music.Bpm.ToString("0.##") + " BPM  ·  STAGE " +
                (session.CurrentNode.Row + 1).ToString("00"), 16, RunUI.Muted, TextAlignmentOptions.Center);
            Top(subtitle.rectTransform, .27f, .75f, 38, 22);
            var book = Button(header, "도감", codex, height: 48);
            book.name = "Preparation codex all";
            Top((RectTransform)book.transform, .78f, .88f, 2, 46);
            var options = Button(header, "메뉴", menu, height: 48);
            Top((RectTransform)options.transform, .90f, 1, 2, 46);
            PlayerHealthBar = Health(header, "Player", session.Health, session.MaxHealth, 0, .48f, RunUI.Teal);
            EnemyHealthBar = Health(header, "Enemy", session.EnemyHealth.Current, session.EnemyHealth.Maximum, .52f, 1, RunUI.Red);
        }

        private PreparationGraphic Health(Transform parent, string name, decimal current, decimal maximum, float left, float right, Color tint)
        {
            var label = Text(parent, (name == "Player" ? "HP  " : "MONSTER HP  ") + current.ToString("0.##") + " / " + maximum.ToString("0.##"),
                15, tint, name == "Player" ? TextAlignmentOptions.Left : TextAlignmentOptions.Right);
            Top(label.rectTransform, left, right, 63, 20);
            var rect = ui.Rect("Preparation " + name + " HP", parent); Top(rect, left, right, 85, 7);
            var bar = rect.gameObject.AddComponent<PreparationGraphic>();
            bar.Configure(PreparationGraphicKind.Health, tint, value: maximum > 0 ? (float)(current / maximum) : 0);
            return bar;
        }

        private void DrawMonster(RectTransform content, string instanceId, MonsterDefinition monster, Action<MonsterDefinition> codex)
        {
            var row = ui.Rect("Preparation monster " + instanceId, content);
            RunUI.Size(row, 140); ui.Background(row, new Color(.11f, .14f, .22f, .96f)); monsterRows.Add(row);
            var portrait = Portrait(row, "Monster icon " + instanceId, MonsterPortrait(monster), monster.Name);
            RunUI.Pin((RectTransform)portrait.transform.parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -4), new Vector2(28, 28));
            portraits.Add(portrait);
            var label = Text(row, monster.Name, 19, RunUI.TextColor); Top(label.rectTransform, 0, .85f, 3, 30, 44);
            var book = Button(row, "도감", () => codex(monster), height: 28);
            book.name = "Preparation codex " + instanceId;
            RunUI.Pin((RectTransform)book.transform, Vector2.one, Vector2.one, new Vector2(-8, -4), new Vector2(56, 28));
            var bookLabel = book.GetComponentInChildren<TextMeshProUGUI>();
            bookLabel.fontSize = bookLabel.fontSizeMax = 14;
            RunUI.Stretch(book.GetComponentInChildren<TextMeshProUGUI>().rectTransform, 2);
            var cards = ui.Rect("Patterns " + instanceId, row);
            RunUI.Overlay(cards, Vector2.zero, Vector2.one, new Vector2(8, 6), new Vector2(-8, -38));
            var layout = cards.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8; layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            int count = 0;
            for (int i = 0; i < session.BattleLoadout.Patterns.Count; i++)
            {
                var pattern = session.BattleLoadout.Patterns[i];
                if (pattern.MonsterId != instanceId) continue;
                DrawPattern(cards, pattern, i); count++;
            }
            if (count == 0) Text(cards, "이번 연주에는 공격이 없어.", 17, RunUI.Muted);
        }

        private void DrawPattern(RectTransform parent, PlannedAttack pattern, int index)
        {
            var button = Button(parent, "", () => RequestPractice(index), height: 0);
            button.name = "Practice pattern " + index; patternButtons.Add(button);
            var element = button.GetComponent<LayoutElement>(); element.minHeight = element.preferredHeight = 0; element.flexibleHeight = 1;
            Destroy(button.GetComponentInChildren<TextMeshProUGUI>().gameObject);
            var rect = (RectTransform)button.transform;
            var icon = Portrait(rect, "Pattern icon " + pattern.Pattern.Id,
                sprites.Get(MonsterCodexView.IconRoot + "Patterns", pattern.Pattern.Id) ?? MonsterPortrait(pattern.Monster), pattern.Pattern.Name);
            RunUI.Pin((RectTransform)icon.transform.parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(7, -5), new Vector2(27, 27));
            var title = Text(rect, pattern.Pattern.Name, 20, RunUI.TextColor);
            Top(title.rectTransform, 0, 1, 2, 28, 41, 7);
            var graph = ui.Rect("Response rhythm", rect); rhythmGraphs.Add(graph);
            Top(graph, 0, 1, 30, 18, 12, 12);
            var rhythm = graph.gameObject.AddComponent<PatternOverviewGraphic>();
            rhythm.MaximumMarkerRadius = 16; rhythm.Bind(pattern.Placement.Pattern);
            var icons = ui.Rect("Responding weapons", rect);
            responseAreas.Add(icons);
            RunUI.Overlay(icons, Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-10, -56));
            var active = session.BattleLoadout.RespondingSlots(pattern);
            if (active.Count == 0)
            {
                var none = Text(icons, "발동 무기 없음", 13, RunUI.Muted); RunUI.Stretch(none.rectTransform); return;
            }
            float span = 1f / Math.Max(3, active.Count), left = (1 - active.Count * span) * .5f;
            for (int i = 0; i < active.Count; i++)
            {
                int slot = active[i]; var state = session.BattleLoadout.Equipment[slot];
                var cell = ui.Rect("Automatic weapon " + slot, icons);
                RunUI.Overlay(cell, new Vector2(left + i * span, 0), new Vector2(left + (i + 1) * span, 1), new Vector2(4, 0), new Vector2(-4, 0));
                var weapon = cell.gameObject.AddComponent<WeaponIconGraphic>(); weapon.FitVisibleArtwork = true; weapon.Bind(state);
            }
        }

        private void DrawEquipment(Action start)
        {
            RectTransform footer = null, deck;
            if (bindings != null) deck = bindings.weapons;
            else
            {
            footer = ui.Rect("Equipped weapons", page);
            RunUI.Overlay(footer, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 126));
            var hint = Text(footer, "패턴을 누르면 연습할 수 있어.", 16, RunUI.Muted);
            Top(hint.rectTransform, 0, .78f, 0, 24);
            deck = ui.Rect("Five equipped weapons", footer);
            RunUI.Overlay(deck, Vector2.zero, new Vector2(.79f, 1), Vector2.zero, new Vector2(0, -30));
            }
            for (int slot = 0; slot < session.Weapons.Count; slot++)
            {
                var state = session.Weapons[slot]; var weapon = WeaponCatalog.Find(state.DefinitionId);
                var cell = ui.Rect("Equipped weapon " + slot, deck);
                RunUI.Overlay(cell, new Vector2(slot / 5f, 0), new Vector2((slot + 1) / 5f, 1), Vector2.zero, new Vector2(-7, 0));
                ui.Background(cell, new Color(.11f, .14f, .22f, .96f));
                var icon = ui.Rect("Weapon icon " + slot, cell);
                RunUI.Overlay(icon, new Vector2(0, 0), new Vector2(.35f, 1), new Vector2(8, 8), new Vector2(-4, -8));
                var graphic = icon.gameObject.AddComponent<WeaponIconGraphic>(); graphic.FitVisibleArtwork = true; graphic.Bind(state); equippedIcons.Add(graphic);
                var name = Text(cell, WeaponRarities.Name(state.Rarity) + " " + weapon.Name + " +" + state.Level, 15, WeaponIconGraphic.RarityColor(state.Rarity));
                RunUI.Overlay(name.rectTransform, new Vector2(.37f, .47f), new Vector2(1, .95f), Vector2.zero, new Vector2(-5, 0));
                var actions = Text(cell, weapon.ActionLabelAt(state.Rarity), 14, RunUI.Teal);
                RunUI.Overlay(actions.rectTransform, new Vector2(.37f, .08f), new Vector2(1, .47f), Vector2.zero, new Vector2(-5, 0));
            }
            if (bindings != null) { StartButton = bindings.start; StartButton.onClick.AddListener(() => start()); return; }
            StartButton = Button(footer, "연주 시작", start, primary: true, height: 84);
            RunUI.Overlay((RectTransform)StartButton.transform, new Vector2(.81f, 0), new Vector2(1, 1), new Vector2(0, 4), new Vector2(0, -30));
        }

        public void RequestPractice(int patternIndex)
        {
            if (HasPracticeConfirmation || patternIndex < 0 || patternIndex >= session.BattleLoadout.Patterns.Count) return;
            PendingPracticeIndex = patternIndex; pageGroup.interactable = pageGroup.blocksRaycasts = false;
            confirmation = ui.Rect("Practice confirmation", transform); RunUI.Stretch(confirmation);
            ui.Background(confirmation, new Color(0, 0, 0, .72f), true);
            var panel = ui.Rect("Practice confirmation panel", confirmation);
            RunUI.Overlay(panel, new Vector2(.24f, .30f), new Vector2(.76f, .70f), Vector2.zero, Vector2.zero);
            ui.Background(panel, RunUI.Panel, true);
            var pattern = session.BattleLoadout.Patterns[patternIndex];
            var title = Text(panel, "이 패턴을 연습할까?", 26, RunUI.Gold, TextAlignmentOptions.Center);
            RunUI.Overlay(title.rectTransform, new Vector2(.04f, .68f), new Vector2(.96f, .94f), Vector2.zero, Vector2.zero);
            var name = Text(panel, pattern.Monster.Name + " · " + pattern.Pattern.Name, 20, RunUI.TextColor, TextAlignmentOptions.Center);
            RunUI.Overlay(name.rectTransform, new Vector2(.04f, .40f), new Vector2(.96f, .68f), Vector2.zero, Vector2.zero);
            var cancel = Button(panel, "돌아가기", CancelPractice, height: 52); cancel.name = "Cancel pattern practice";
            RunUI.Overlay((RectTransform)cancel.transform, new Vector2(.05f, .08f), new Vector2(.47f, .33f), Vector2.zero, Vector2.zero);
            var accept = Button(panel, "연습 시작", ConfirmPractice, primary: true, height: 52); accept.name = "Confirm pattern practice";
            RunUI.Overlay((RectTransform)accept.transform, new Vector2(.53f, .08f), new Vector2(.95f, .33f), Vector2.zero, Vector2.zero);
        }

        public void ConfirmPractice()
        {
            int index = PendingPracticeIndex; if (index < 0) return;
            CancelPractice(); practice(index);
        }
        public void CancelPractice()
        {
            PendingPracticeIndex = -1;
            if (confirmation != null) { confirmation.gameObject.SetActive(false); Destroy(confirmation.gameObject); }
            confirmation = null;
            if (pageGroup != null) pageGroup.interactable = pageGroup.blocksRaycasts = true;
        }
        private void LateUpdate() { ReflowRows(); }
        private void ReflowRows()
        {
            if (viewport == null || monsterRows.Count == 0 || Mathf.Abs(viewport.rect.height - lastHeight) < .1f) return;
            lastHeight = viewport.rect.height;
            float height = (float)PreparationLayout.RowHeight(lastHeight, monsterRows.Count);
            foreach (var row in monsterRows) RunUI.Size(row, height);
            float rhythmHeight = (float)PreparationLayout.RhythmHeight(height);
            foreach (var graph in rhythmGraphs) Top(graph, 0, 1, 30, rhythmHeight, 12, 12);
            // Keep the source art centered in the space below the rhythm, with a useful preview cap.
            float available = height - 44 - 30 - rhythmHeight - 14;
            float weaponHeight = (float)PreparationLayout.WeaponHeight(height);
            float bottom = 6 + Mathf.Max(0, available - weaponHeight) * .5f;
            foreach (var area in responseAreas)
                RunUI.Overlay(area, Vector2.zero, new Vector2(1, 0), new Vector2(10, bottom), new Vector2(-10, bottom + weaponHeight));
        }
        private Button Button(Transform parent, string value, Action action, float height, bool primary = false)
        {
            var button = ui.Button(parent, value, action, primary: primary, height: height);
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            // Fit the actual font line metrics inside short HUD controls.
            text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = 24;
            text.rectTransform.offsetMin = new Vector2(8, 3);
            text.rectTransform.offsetMax = new Vector2(-8, -3);
            return button;
        }
        private TextMeshProUGUI Text(Transform parent, string value, int size, Color tint, TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var label = ui.Label(parent, value, size, tint, 24, align);
            label.enableAutoSizing = true; label.fontSizeMin = Math.Max(11, size - 4); label.fontSizeMax = size;
            return label;
        }
        private static void Top(RectTransform rect, float left, float right, float top, float height, float insetLeft = 0, float insetRight = 0) =>
            RunUI.Overlay(rect, new Vector2(left, 1), new Vector2(right, 1), new Vector2(insetLeft, -top - height), new Vector2(-insetRight, -top));
        private Sprite MonsterPortrait(MonsterDefinition monster) => MonsterAuthoringRegistry.Find(monster.Id)?.portrait ?? sprites.Get(MonsterCodexView.IconRoot + "Monsters", monster.Id) ??
            Resources.Load<Sprite>(ArtRoot + "Monsters/" + monster.Id) ??
            sprites.Get(MonsterAttackDefinition.ResourceRoot + monster.Id, "idle") ?? Resources.Load<Sprite>("BBSB/BattleArt/" + monster.ArtId);
        private Image Portrait(Transform parent, string name, Sprite sprite, string fallback)
        {
            var frame = ui.Rect(name, parent);
            var stencil = frame.gameObject.AddComponent<PreparationPortraitMask>(); stencil.raycastTarget = false;
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var rect = ui.Rect("Image", frame); RunUI.Stretch(rect);
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite; image.preserveAspect = true;
            image.raycastTarget = false; image.enabled = sprite != null;
            if (sprite == null)
            {
                var text = Text(frame, fallback, 13, RunUI.Gold, TextAlignmentOptions.Center); RunUI.Stretch(text.rectTransform);
            }
            return image;
        }
        private static Sprite PlayerIdle()
        {
            foreach (var sprite in Resources.LoadAll<Sprite>(PlayerMotionSprites.ResourcePath + "idle"))
                if (sprite.name == "idle_0") return sprite;
            return Resources.Load<Sprite>("BBSB/BattleArt/weapon-master");
        }
    }
}
