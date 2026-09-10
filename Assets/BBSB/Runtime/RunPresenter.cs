using System;
using BBSB.Core;
using BBSB.Runtime.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BBSB.Runtime
{
    /// <summary>One viewport per screen, with secondary information in a modal menu.</summary>
    public sealed class RunPresenter : MonoBehaviour
    {
        public RunSession Session { get; private set; }
        public RhythmRound ActiveRound { get; private set; }
        // Session.BattleMusic and BattlePlan are ready when this fires. Return via SubmitBattleResult.
        public event Action<string, StageKind, int> BattleRequested;
        private RunRules rules;
        private RunUI ui;
        private RectTransform safeArea;
        private RectTransform screen;
        private RectTransform body;
        private RectTransform menuOverlay, menuBody;
        private enum MenuPage { None, Home, Inventory, Help, Patterns, Development, Abandon }
        private MenuPage menuPage;
        private int? fixedSeed;
        private bool testControls;
        private bool title = true;
        private int pendingOffer = -1;
        private string notice = "";
        private bool rendering;
        private readonly MusicPreview musicPreview = new MusicPreview();
        private RhythmRound completedRound;

        public void Initialize(RunRules runRules, Font font, int? seed, bool showTestControls)
        {
            rules = runRules; ui = new RunUI(font); fixedSeed = seed; testControls = showTestControls;
            var canvasRoot = ui.Rect("BBSB Canvas", transform);
            var canvas = canvasRoot.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Keep the short side at 720 UI units in portrait AND landscape. A wide Game view
            // must not shrink every control to fit a virtual 1280-unit portrait height.
            scaler.referenceResolution = new Vector2(720, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasRoot.gameObject.AddComponent<GraphicRaycaster>();
            var background = ui.Rect("Backdrop", canvasRoot); RunUI.Stretch(background); ui.Background(background, RunUI.Ink);
            safeArea = ui.Rect("Safe area", canvasRoot); RunUI.Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaPanel>();
            Render();
        }

        public bool SubmitBattleResult(string ticket, bool victory, int remainingHealth)
        {
            if (Session == null || !Session.ResolveBattle(ticket, victory, remainingHealth)) return false;
            ActiveRound = completedRound = null;
            menuPage = MenuPage.None; pendingOffer = -1; notice = ""; Render(); return true;
        }

        private void StartRun()
        {
            int seed = fixedSeed ?? Guid.NewGuid().GetHashCode();
            if (Session == null) Session = new RunSession(seed, rules); else Session.Restart(seed);
            ActiveRound = completedRound = null;
            title = false; menuPage = MenuPage.None; pendingOffer = -1; notice = ""; Render();
        }

        private void Render()
        {
            if (rendering || ui == null) return;
            rendering = true;
            if (screen != null) { screen.gameObject.SetActive(false); Destroy(screen.gameObject); }
            ClearMenu();
            screen = ui.Stack(safeArea, "Run screen", 16, 10); RunUI.Stretch(screen);
            body = null;
            screen.gameObject.AddComponent<CanvasGroup>();
            if (title) { DrawTitle(); rendering = false; return; }
            if (ActiveRound != null)
            {
                string ticket = Session.StageTicket;
                screen.gameObject.AddComponent<RhythmPlayback>().Bind(ActiveRound, ui,
                    round => FinishRhythmRound(ticket, round), () => LeaveRhythmRound(ticket));
                rendering = false; return;
            }
            DrawHeader();
            // Map and preparation use all remaining space. Only lists/results need scrolling.
            if (completedRound == null && pendingOffer < 0 && Session.Phase == RunPhase.Map)
                DrawMap();
            else if (completedRound == null && pendingOffer < 0 && Session.Phase == RunPhase.Stage && Session.CurrentNode.IsBattle)
                DrawBattle();
            else
            {
                body = ui.Scroll(screen);
                if (completedRound != null) DrawRoundReport();
                else if (pendingOffer >= 0) DrawReplacement();
                else
                {
                    switch (Session.Phase)
                    {
                        case RunPhase.Stage: DrawStage(); break;
                        case RunPhase.Reward: DrawRewards(); break;
                        case RunPhase.FieldCleared: DrawFieldCleared(); break;
                        case RunPhase.GameOver: DrawGameOver(); break;
                    }
                }
            }
            if (!string.IsNullOrEmpty(notice)) ui.Label(screen, notice, 21, RunUI.Teal, 42);
            RenderMenu();
            rendering = false;
        }

        private void Update()
        {
            if (title || ActiveRound != null || Session == null || Session.Phase == RunPhase.GameOver) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (menuPage == MenuPage.None) OpenMenu(MenuPage.Home);
                else CloseMenu();
            }
        }

        private void DrawTitle()
        {
            ui.Label(screen, "RHYTHM  /  WEAPONS  /  ROGUELIKE", 19, RunUI.Gold, 40);
            var content = ui.Scroll(screen);
            ui.Label(content, "Beat! Block,\nShake~ Beat!", 58, RunUI.TextColor, 185);
            ui.Label(content, "다섯 무기로 지휘하는 하나의 리듬", 25, RunUI.Teal, 55);
            var card = ui.Card(content);
            ui.Label(card, "길을 고르고, 다음 박자를 준비해.", 29, RunUI.Gold, 90);
            ui.Label(card, "무작위로 연결된 길에서 전투와 휴식을 선택해.\n네 번째 단계의 보스를 넘으면 다음 필드로 향해.", 24, null, 125);
            ui.Label(card, "탐험이 끝나면 획득한 장비와 보상도 초기화돼.", 21, RunUI.Muted, 70);
            ui.Label(content, "무기 5개  ·  분기 선택  ·  보스 도전", 22, RunUI.Muted, 70);
            ui.Button(screen, "탐험 시작", StartRun, primary: true, height: 84);
        }

        private void DrawHeader()
        {
            var row = ui.Row(screen, 80); row.name = "Status";
            var field = ui.Label(row, "FIELD " + Session.Map.Number.ToString("00"), 26, RunUI.Gold, 80);
            var fieldSize = field.GetComponent<LayoutElement>();
            fieldSize.minWidth = fieldSize.preferredWidth = 148; fieldSize.flexibleWidth = 0;
            ui.Label(row, "HP  " + Session.Health + " / " + Session.MaxHealth + "\n" + Session.Gold + " G", 22, RunUI.Teal, 80);
            if (Session.Phase == RunPhase.GameOver) return;
            var menu = ui.Button(row, "메뉴", () => OpenMenu(MenuPage.Home), height: 80);
            var size = menu.GetComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = 104; size.flexibleWidth = 0;
        }

        private void Heading(string kicker, string heading, string description)
        {
            ui.Label(body, kicker, 18, RunUI.Gold, 28);
            ui.Label(body, heading, 34, RunUI.TextColor, 55);
            if (!string.IsNullOrEmpty(description)) ui.Label(body, description, 22, RunUI.Muted, 72);
        }

        private void DrawMap()
        {
            ui.Label(screen, "다음 무대를 골라줘", 34, RunUI.TextColor, 48);
            var map = ui.Rect("Field map", screen);
            RunUI.Size(map, 200).flexibleHeight = 1; ui.Background(map, RunUI.Panel);
            var links = ui.Rect("Connections", map); RunUI.Stretch(links);
            links.gameObject.AddComponent<MapConnectionsGraphic>().Bind(Session);
            foreach (var node in Session.Map.Nodes)
            {
                bool available = Session.CanEnter(node.Id);
                bool visited = HasVisited(node.Id);
                string label = (node.Row + 1).ToString("00") + "  " + ContentCatalog.StageName(node.Kind);
                if (visited) label += "\n완료";
                else if (available) label += "\n진입";
                var id = node.Id;
                var button = ui.Button(map, label, () => EnterStage(id), available, available, 86);
                var rect = (RectTransform)button.transform;
                var pos = MapConnectionsGraphic.Position(node);
                rect.anchorMin = new Vector2(pos.x - .135f, pos.y - .08f);
                rect.anchorMax = new Vector2(pos.x + .135f, pos.y + .08f);
                rect.sizeDelta = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;
                var colors = button.colors;
                colors.disabledColor = visited ? Color.white : new Color(.65f, .65f, .65f, .85f);
                button.colors = colors;
                if (visited) button.GetComponent<Image>().color = RunUI.Hex("426A65");
                else if (!available && node.Kind == StageKind.Boss) button.GetComponent<Image>().color = RunUI.Hex("643A53");
                var labelText = button.GetComponentInChildren<Text>(); labelText.fontSize = 30;
                labelText.resizeTextForBestFit = true;
                labelText.resizeTextMinSize = 18; labelText.resizeTextMaxSize = 30;
            }
            ui.Label(screen, "아래에서 위로  ·  밝은 무대를 선택해", 22, RunUI.Muted, 34, TextAnchor.MiddleCenter);
        }

        private bool HasVisited(string id)
        { foreach (var entry in Session.Visited) if (entry == id) return true; return false; }

        private void EnterStage(string nodeId)
        {
            if (!Session.Enter(nodeId)) return;
            ActiveRound = completedRound = null;
            if (Session.CurrentNode.IsBattle) musicPreview.Reset(Session.BattleMusic);
            menuPage = MenuPage.None; notice = ""; Render();
            if (Session.CurrentNode.IsBattle) BattleRequested?.Invoke(Session.StageTicket, Session.CurrentNode.Kind, Session.Map.Number);
        }

        private void DrawStage()
        {
            var kind = Session.CurrentNode.Kind;
            switch (kind)
            {
                case StageKind.Monster: case StageKind.Elite: case StageKind.Boss: DrawBattle(); break;
                case StageKind.Rest:
                    Heading("TAKE A BREATH", "휴식", "다음 무대를 위해 숨을 고르고 체력을 회복해.");
                    var rest = ui.Card(body);
                    ui.Label(rest, "체력 +" + Session.RestAmount, 36, RunUI.Teal, 75);
                    ui.Label(rest, "최대 체력을 넘지 않아. 한 번만 사용할 수 있어.", 22, RunUI.Muted, 65);
                    ui.Button(rest, Session.ServiceClaimed ? "휴식 완료" : "쉬어가기", () =>
                    { if (Session.Rest()) { notice = "체력을 회복했어."; Render(); } }, !Session.ServiceClaimed, true);
                    LeaveButton(); break;
                case StageKind.Upgrade:
                    Heading("TUNE YOUR WEAPONS", "강화", "무기 한 개를 골라 +1 강화해. 최대 +3까지 가능해.");
                    for (int i = 0; i < Session.Weapons.Count; i++)
                    {
                        int slot = i; var weapon = Session.Weapons[i];
                        ui.Button(body, WeaponName(i) + "  →  +" + Math.Min(RunRules.MaximumUpgrade, weapon.Level + 1), () =>
                        { if (Session.Upgrade(slot)) { notice = WeaponName(slot) + " 강화 완료!"; Render(); } },
                            !Session.ServiceClaimed && weapon.Level < RunRules.MaximumUpgrade, height: 76);
                    }
                    LeaveButton(); break;
                case StageKind.Shop:
                    Heading("THE TRAVELING SHOP", "상점", "필요한 물건을 골라줘. 각 상품은 한 번만 구매할 수 있어.");
                    DrawOffers(true); LeaveButton(); break;
            }
        }

        private void DrawBattle()
        {
            var kind = Session.CurrentNode.Kind;
            var plan = Session.BattlePlan;
            var music = Session.BattleMusic.Music;
            ui.Label(screen, "준비하기", 34, RunUI.TextColor, 48);
            ui.Label(screen, ContentCatalog.StageName(kind) + "  ·  " + music.Name + "  /  " + music.Bpm + " BPM", 24, RunUI.Gold, 42);
            var stage = ui.Rect("Preparation arena", screen); RunUI.Size(stage, 180).flexibleHeight = 1;
            var arena = stage.gameObject.AddComponent<BattleArenaView>();
            // A still preview reads the same plan without starting playback or changing the session.
            arena.Initialize(new RhythmRound(plan), ui.Font);
            var actions = ui.Row(screen, 76);
            ui.Button(actions, "몬스터 패턴", () => OpenMenu(MenuPage.Patterns), height: 76);
            ui.Button(actions, "연주 시작", () => StartRhythmRound(), primary: true, height: 76);
        }

        private void DrawPatterns()
        {
            var plan = Session.BattlePlan;
            var music = plan.Stage.Music;
            ui.Label(body, music.Name + "  /  " + music.Bpm + " BPM", 28, RunUI.Gold, 50);
            ui.Label(body, music.BarCount + "마디  ·  " + music.DurationSeconds.ToString("0.0") + "초\n전조 다음에 같은 리듬으로 대응하면 돼.", 23, RunUI.Muted, 84);
            for (int i = 0; i < plan.Monsters.Count; i++)
                ui.Card(body).gameObject.AddComponent<MonsterPatternView>().Bind(plan.Monsters[i], ui, i + 1);
        }

        private void DrawDevelopment()
        {
            var plan = Session.BattlePlan;
            var kind = Session.CurrentNode.Kind;
            ui.Label(body, "개발용 계획 요약  ·  공격 " + plan.Attacks.Count + "묶음 / 양보 " + plan.Withdrawals.Count + "묶음", 19, RunUI.Muted, 48);
            ui.Label(body, "샘플 곡은 박자음으로 재생돼.\n입력 결과에 따른 무기 효과와 피해 계산은 다음 단계야.", 20, RunUI.Muted, 82);
            musicPreview.Draw(ui, body, RenderMusicPreview);
            var card = ui.Card(body);
            ui.Label(card, "테스트용 전투 결과", 21, RunUI.Gold, 40);
            string ticket = Session.StageTicket;
            ui.Button(card, "클리어 처리", () => SubmitBattleResult(ticket, true, Session.Health), primary: true);
            int damage = kind == StageKind.Boss ? 30 : kind == StageKind.Elite ? 20 : 12;
            ui.Button(card, "HP -" + damage + " 후 클리어 처리", () =>
                SubmitBattleResult(ticket, Session.Health > damage, Math.Max(0, Session.Health - damage)));
            ui.Button(card, "게임오버 처리", () => SubmitBattleResult(ticket, false, 0));
        }

        public bool StartRhythmRound()
        {
            if (Session == null || Session.Phase != RunPhase.Stage || Session.BattlePlan == null || ActiveRound != null) return false;
            ActiveRound = new RhythmRound(Session.BattlePlan); completedRound = null;
            menuPage = MenuPage.None; pendingOffer = -1; notice = ""; Render(); return true;
        }

        private void FinishRhythmRound(string ticket, RhythmRound round)
        {
            if (Session.StageTicket != ticket || ActiveRound != round) return;
            completedRound = round; ActiveRound = null; Render();
        }

        private void LeaveRhythmRound(string ticket)
        {
            if (Session.StageTicket != ticket || ActiveRound == null) return;
            ActiveRound = completedRound = null; notice = ""; Render();
        }

        private void DrawRoundReport()
        {
            Heading("ROUND COMPLETE", "연주 결과", "같은 몬스터 계획으로 다시 준비할 수 있어.");
            var card = ui.Card(body);
            ui.Label(card, completedRound.ScorePercent.ToString("0.0") + "%", 60, RunUI.Teal, 100, TextAnchor.MiddleCenter);
            ui.Label(card, "정확 " + completedRound.PerfectCount + "  ·  반미스 " + completedRound.HalfMissCount + "  ·  미스 " + completedRound.MissCount,
                25, RunUI.TextColor, 60, TextAnchor.MiddleCenter);
            ui.Label(card, "정확 100% · 반미스 50% · 미스 0%로 집계했어.", 20, RunUI.Muted, 48);
            foreach (var monster in completedRound.Plan.Monsters)
            {
                int perfect = 0, half = 0, miss = 0;
                foreach (var result in completedRound.Results)
                {
                    if (result.Note.Attack.MonsterId != monster.InstanceId) continue;
                    if (result.Grade == RhythmGrade.Perfect) perfect++; else if (result.Grade == RhythmGrade.HalfMiss) half++; else miss++;
                }
                var line = ui.Card(body, 16);
                ui.Label(line, monster.Monster.Name, 27, RunUI.Gold, 42);
                ui.Label(line, "정확 " + perfect + "  ·  반미스 " + half + "  ·  미스 " + miss, 23, null, 42);
            }
            ui.Button(body, "다시 준비", () => { completedRound = null; Render(); }, primary: true, height: 82);
        }

        private void RenderMusicPreview()
        {
            float offset = menuBody.anchoredPosition.y;
            RenderMenu();
            Canvas.ForceUpdateCanvases();
            var scroll = menuBody.GetComponentInParent<ScrollRect>();
            float range = Mathf.Max(0, menuBody.rect.height - scroll.viewport.rect.height);
            scroll.verticalNormalizedPosition = range > 0 ? 1 - Mathf.Clamp(offset, 0, range) / range : 1;
        }

        private void LeaveButton()
        {
            ui.Button(body, "지도에 돌아가기", () => { if (Session.LeaveService()) { notice = ""; Render(); } }, height: 72);
        }

        private void DrawRewards()
        {
            Heading("STAGE CLEAR", "다음 박자를 위한 보상", "전투 골드를 획득했어. 아래 보상 중 하나를 선택해.");
            DrawOffers(false);
            ui.Button(body, "보상 건너뛰기", () => { if (Session.SkipReward()) Render(); });
        }

        private void DrawOffers(bool shop)
        {
            for (int i = 0; i < Session.Offers.Count; i++)
            {
                int index = i; var offer = Session.Offers[i]; var definition = offer.Content;
                var card = ui.Card(body, 18);
                string category = definition.Kind == RewardKind.Weapon ? "무기" : definition.Kind == RewardKind.Item ? "아이템" : "증강";
                ui.Label(card, category + "  /  " + definition.Name, 28, RunUI.Gold, 44);
                ui.Label(card, definition.Description, 22, RunUI.Muted, 78);
                bool enabled = !offer.Purchased && (!shop || Session.Gold >= offer.Price);
                string label = offer.Purchased ? "구매 완료" : shop ? offer.Price + " G  ·  구매" : "선택";
                if (shop && !offer.Purchased && Session.Gold < offer.Price) label += "  ·  골드 부족";
                ui.Button(card, label, () => PickOffer(index), enabled, !shop, 62);
            }
        }

        private void PickOffer(int index)
        {
            if (Session.Offers[index].Content.Kind == RewardKind.Weapon)
            { pendingOffer = index; notice = ""; Render(); return; }
            GrantOffer(index, -1);
        }

        private void GrantOffer(int index, int slot)
        {
            string name = Session.Offers[index].Content.Name;
            bool success = Session.Phase == RunPhase.Reward ? Session.ChooseReward(index, slot) : Session.Buy(index, slot);
            if (success) { pendingOffer = -1; notice = name + " 획득!"; Render(); }
        }

        private void DrawReplacement()
        {
            var offer = Session.Offers[pendingOffer];
            Heading("FIVE WEAPONS, ONE RHYTHM", offer.Content.Name + " 장착", "다섯 무기 중 교체할 슬롯을 골라줘. 교체한 무기와 강화는 사라져.");
            for (int i = 0; i < Session.Weapons.Count; i++)
            {
                int slot = i;
                ui.Button(body, (i + 1) + "  " + WeaponName(i) + "  교체", () => GrantOffer(pendingOffer, slot), height: 82);
            }
            ui.Button(body, "돌아가기", () => { pendingOffer = -1; Render(); });
        }

        private void DrawFieldCleared()
        {
            Heading("FIELD CLEAR", "보스의 무대를 넘었어!", "현재 체력과 획득한 보상을 가지고 다음 필드로 향해.");
            var card = ui.Card(body);
            ui.Label(card, "FIELD " + Session.Map.Number.ToString("00") + "  COMPLETE", 36, RunUI.Gold, 120);
            ui.Label(card, "새로운 갈림길이 기다리고 있어.", 26, RunUI.Teal, 85);
            ui.Button(card, "다음 필드로", () => { if (Session.AdvanceField()) { notice = ""; Render(); } }, primary: true, height: 82);
        }

        private void DrawGameOver()
        {
            Heading("GAME OVER", "이번 탐험은 여기까지", "획득한 무기·아이템·증강과 골드는 초기화됐어.");
            var card = ui.Card(body);
            ui.Label(card, "도달한 필드  " + Session.Map.Number, 31, RunUI.Gold, 70);
            ui.Label(card, "통과한 스테이지  " + Session.ClearedStages, 27, null, 60);
            ui.Button(card, "새로운 탐험", StartRun, primary: true, height: 84);
            ui.Button(card, "처음으로", () => { title = true; Render(); });
        }

        private void OpenMenu(MenuPage page)
        {
            menuPage = page; RenderMenu();
        }

        private void CloseMenu() { menuPage = MenuPage.None; RenderMenu(); }

        private void ClearMenu()
        {
            if (menuOverlay != null) { menuOverlay.gameObject.SetActive(false); Destroy(menuOverlay.gameObject); }
            menuOverlay = menuBody = null;
        }

        private void RenderMenu()
        {
            ClearMenu();
            if (title || ActiveRound != null || Session.Phase == RunPhase.GameOver) menuPage = MenuPage.None;
            var group = screen.GetComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = menuPage == MenuPage.None;
            if (menuPage == MenuPage.None) return;
            if ((menuPage == MenuPage.Patterns || menuPage == MenuPage.Development) && Session.BattlePlan == null)
                menuPage = MenuPage.Home;
            if (menuPage == MenuPage.Development && !testControls) menuPage = MenuPage.Home;
            string heading = menuPage == MenuPage.Inventory ? "장비 · 가방 · 증강" :
                menuPage == MenuPage.Help ? "조작 방법" : menuPage == MenuPage.Patterns ? "몬스터 패턴" :
                menuPage == MenuPage.Development ? "개발 도구" : menuPage == MenuPage.Abandon ? "탐험 종료" : "탐험 메뉴";
            menuOverlay = ui.Modal(safeArea, "Run menu", heading, CloseMenu, out menuBody);
            var previousBody = body; body = menuBody;
            switch (menuPage)
            {
                case MenuPage.Home:
                    ui.Label(body, "FIELD " + Session.Map.Number.ToString("00") + "  ·  통과한 스테이지 " + Session.ClearedStages, 25, RunUI.Gold, 54);
                    ui.Button(body, "돌아가기", CloseMenu, primary: true);
                    ui.Button(body, "장비 · 가방 · 증강", () => OpenMenu(MenuPage.Inventory));
                    if (Session.BattlePlan != null) ui.Button(body, "몬스터 패턴", () => OpenMenu(MenuPage.Patterns));
                    ui.Button(body, "조작 방법", () => OpenMenu(MenuPage.Help));
                    if (testControls && Session.BattlePlan != null) ui.Button(body, "개발 도구", () => OpenMenu(MenuPage.Development));
                    ui.Button(body, "탐험 종료", () => OpenMenu(MenuPage.Abandon));
                    break;
                case MenuPage.Inventory: DrawInventory(); break;
                case MenuPage.Help:
                    ui.Label(body, "지도는 아래에서 위로 진행해.\n밝은 무대만 선택할 수 있고 네 번째 무대는 보스야.", 24, RunUI.Muted, 92);
                    ui.Controls(body); break;
                case MenuPage.Patterns: DrawPatterns(); break;
                case MenuPage.Development: DrawDevelopment(); break;
                case MenuPage.Abandon: DrawAbandon(); break;
            }
            if (menuPage != MenuPage.Home) ui.Button(body, "메뉴로 돌아가기", () => OpenMenu(MenuPage.Home));
            body = previousBody;
        }

        private void DrawLoadout()
        {
            ui.Label(body, "장착한 무기  /  5 SLOTS", 27, RunUI.Gold, 46);
            for (int i = 0; i < Session.Weapons.Count; i++)
            {
                var card = ui.Card(body, 16); card.name = "Weapon " + i;
                ui.Label(card, (i + 1) + "  " + WeaponName(i), 27, RunUI.TextColor, 44);
                ui.Label(card, ContentCatalog.Find(Session.Weapons[i].DefinitionId).Description, 22, RunUI.Muted, 66);
            }
        }

        private void DrawInventory()
        {
            ui.Label(body, "HP  " + Session.Health + " / " + Session.MaxHealth + "  ·  " + Session.Gold + " G", 26, RunUI.Teal, 48);
            DrawLoadout();
            ui.Label(body, "아이템", 27, RunUI.Gold, 45);
            if (Session.Items.Count == 0) ui.Label(body, "아직 아이템이 없어.", 22, RunUI.Muted, 60);
            for (int i = 0; i < Session.Items.Count; i++)
            {
                int index = i;
                bool canUse = Session.Health < Session.MaxHealth &&
                    !(Session.Phase == RunPhase.Stage && Session.CurrentNode.IsBattle);
                ui.Button(body, "회복 물약  ·  HP +25", () =>
                { if (Session.UseItem(index)) { notice = "체력을 회복했어."; Render(); } }, canUse);
            }
            ui.Label(body, "증강", 27, RunUI.Gold, 45);
            if (Session.Augments.Count == 0) ui.Label(body, "아직 증강이 없어.", 22, RunUI.Muted, 60);
            foreach (var id in new[] { "vitality", "recovery", "bargain" })
            {
                int count = Session.CountAugment(id); if (count == 0) continue;
                var card = ui.Card(body, 16); var definition = ContentCatalog.Find(id);
                ui.Label(card, definition.Name + "  ×" + count, 26, RunUI.Teal, 45);
                ui.Label(card, definition.Description, 22, RunUI.Muted, 85);
            }
        }

        private void DrawAbandon()
        {
            Heading("END THIS RUN", "탐험을 마칠까?", "이번 탐험의 장비와 보상은 사라지고 처음부터 다시 시작하게 돼.");
            ui.Button(body, "계속 탐험하기", CloseMenu, primary: true);
            ui.Button(body, "탐험 종료", () => { Session.Abandon(); menuPage = MenuPage.None; Render(); });
        }

        private string WeaponName(int index)
        {
            var weapon = Session.Weapons[index];
            return ContentCatalog.Find(weapon.DefinitionId).Name + " +" + weapon.Level;
        }
    }
}
