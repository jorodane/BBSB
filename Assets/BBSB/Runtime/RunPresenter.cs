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
        private int selectedMap = -1;
        private int pendingOffer = -1;
        private string notice = "";
        private bool rendering;
        private readonly MusicPreview musicPreview = new MusicPreview();
        private RhythmRound completedRound;
        private BattlePreparationView preparation;
        private MonsterCodexView codex;

        public void Initialize(RunRules runRules, Font font, int? seed, bool showTestControls)
        {
            rules = runRules; ui = new RunUI(font); fixedSeed = seed; testControls = showTestControls;
            var canvasRoot = ui.Rect("BBSB Canvas", transform);
            var canvas = canvasRoot.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Landscape first: 16:9 uses 1280x720, while 16:10 gains vertical scene space.
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasRoot.gameObject.AddComponent<GraphicRaycaster>();
            var background = ui.Rect("Backdrop", canvasRoot); RunUI.Stretch(background); ui.Background(background, RunUI.Ink);
            safeArea = ui.Rect("Safe area", canvasRoot); RunUI.Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaPanel>();
            Render();
        }

        public bool SubmitBattleResult(string ticket, bool victory, decimal remainingHealth)
        {
            if (Session == null || !Session.ResolveBattle(ticket, victory, remainingHealth)) return false;
            ActiveRound?.Stop(); ActiveRound = completedRound = null;
            menuPage = MenuPage.None; pendingOffer = -1; notice = ""; Render(); return true;
        }

        private void StartRun()
        {
            int seed = fixedSeed ?? Guid.NewGuid().GetHashCode();
            string mapId = selectedMap < 0 ? null : StageCatalog.Maps[selectedMap].Id;
            if (Session == null) Session = new RunSession(seed, rules, mapId); else Session.Restart(seed, mapId);
            ActiveRound = completedRound = null;

            title = false; menuPage = MenuPage.None; pendingOffer = -1; notice = ""; Render();
        }

        private void Render()
        {
            if (rendering || ui == null) return;
            rendering = true;
            if (codex != null) codex.Close();
            if (screen != null) { screen.gameObject.SetActive(false); Destroy(screen.gameObject); }
            ClearMenu(); preparation = null;
            screen = ui.Rect("Run screen", safeArea); RunUI.Stretch(screen);
            body = null;
            screen.gameObject.AddComponent<CanvasGroup>();
            if (title) { DrawTitle(); rendering = false; return; }
            if (ActiveRound != null)
            {
                string ticket = Session.StageTicket;
                screen.gameObject.AddComponent<RhythmPlayback>().Bind(ActiveRound, Session, ui,
                    round => FinishRhythmRound(ticket, round), () => LeaveRhythmRound(ticket), round => ActiveRound = round);
                rendering = false; return;
            }
            // Scenes fill the viewport. HUD controls are siblings layered over the scene.
            RectTransform page = null;
            if (completedRound == null && pendingOffer < 0 && Session.Phase == RunPhase.Map)
                DrawMap();
            else if (completedRound == null && pendingOffer < 0 && Session.Phase == RunPhase.Stage && Session.CurrentNode.IsBattle)
                DrawBattle();
            else
            {
                page = ui.Stack(screen, "Page content", 24, 12); RunUI.Stretch(page);
                page.GetComponent<VerticalLayoutGroup>().padding.top = 116;
                body = ui.Scroll(page);
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
            // The preparation screen owns its header, HP bars and rectangular menu button.
            if (completedRound != null || pendingOffer >= 0 || Session.Phase != RunPhase.Stage || !Session.CurrentNode.IsBattle)
                DrawHud();
            if (!string.IsNullOrEmpty(notice))
            {
                var toast = ui.Label(page ?? screen, notice, 21, RunUI.Teal, 42, TextAnchor.MiddleCenter);
                if (page == null)
                    RunUI.Overlay(toast.rectTransform, new Vector2(.3f, 0), new Vector2(.7f, 0), new Vector2(0, 68), new Vector2(0, 110));
            }
            RenderMenu();
            rendering = false;
        }

        private void Update()
        {
            if (codex != null)
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) codex.Back();
                return;
            }
            if (title || ActiveRound != null || Session == null || Session.Phase == RunPhase.GameOver) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (preparation != null && preparation.HasPracticeConfirmation) { preparation.CancelPractice(); return; }
                if (menuPage == MenuPage.None) OpenMenu(MenuPage.Home);
                else CloseMenu();
            }
        }

        private void DrawTitle()
        {
            var panel = ui.Stack(screen, "Title content", 32, 14); RunUI.Stretch(panel);
            ui.Label(panel, "RHYTHM  /  WEAPONS  /  ROGUELIKE", 19, RunUI.Gold, 40);
            var content = ui.Scroll(panel);
            ui.Label(content, "Beat! Block,\nShake~ Beat!", 58, RunUI.TextColor, 185);
            ui.Label(content, "다섯 무기로 지휘하는 하나의 리듬", 25, RunUI.Teal, 55);
            var card = ui.Card(content);
            ui.Label(card, "길을 고르고, 다음 박자를 준비해.", 29, RunUI.Gold, 90);
            ui.Label(card, "네 곳의 몬스터 지역 중 하나에서 탐험을 시작해.\n여섯 번째 단계의 보스를 넘으면 다음 필드로 향해.", 24, null, 125);
            ui.Label(card, "탐험이 끝나면 획득한 장비와 보상도 초기화돼.", 21, RunUI.Muted, 70);
            ui.Label(content, "무기 5개  ·  분기 선택  ·  보스 도전", 22, RunUI.Muted, 70);
            var selection = ui.Card(content);
            ui.Label(selection, "시작 맵", 23, RunUI.Gold, 36);
            var genres = ui.Row(selection, 64);
            ui.Button(genres, "이전 맵", () => { selectedMap = (selectedMap + StageCatalog.Maps.Count + 1) % (StageCatalog.Maps.Count + 1) - 1; Render(); }, height: 64);
            ui.Label(genres, selectedMap < 0 ? "랜덤 맵" : StageCatalog.Maps[selectedMap].Genre + " · " + StageCatalog.Maps[selectedMap].Name, 22, RunUI.Teal, 64, TextAnchor.MiddleCenter);
            ui.Button(genres, "다음 맵", () => { selectedMap = (selectedMap + 2) % (StageCatalog.Maps.Count + 1) - 1; Render(); }, height: 64);
            var actions = ui.Row(panel, 84);
            ui.Button(actions, "탐험 시작", StartRun, primary: true, height: 84);
            ui.Button(actions, "몬스터 도감", OpenCodex, height: 84);
        }

        private void DrawHud()
        {
            var hud = ui.Rect("Status HUD", screen);
            RunUI.Pin(hud, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -20), new Vector2(500, 80));
            var field = ui.Label(hud, Session.Map.Theme.Name + " · FIELD " + Session.Map.Number.ToString("00"), 27, RunUI.Gold, 40);
            RunUI.Overlay(field.rectTransform, Vector2.zero, Vector2.one, new Vector2(0, 40), Vector2.zero);
            var stats = ui.Label(hud, "HP  " + Session.Health.ToString("0.##") + " / " + Session.MaxHealth + "  ·  " + Session.Gold + " G", 22, RunUI.Teal, 36);
            RunUI.Overlay(stats.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -44));
            if (Session.Phase != RunPhase.GameOver) ui.FloatingMenu(screen, () => OpenMenu(MenuPage.Home));
        }

        private void Heading(string kicker, string heading, string description)
        {
            ui.Label(body, kicker, 18, RunUI.Gold, 28);
            ui.Label(body, heading, 34, RunUI.TextColor, 55);
            if (!string.IsNullOrEmpty(description)) ui.Label(body, description, 22, RunUI.Muted, 72);
        }

        private void DrawMap()
        {
            var map = ui.Rect("Field map", screen);
            RunUI.Stretch(map); ui.Background(map, RunUI.Panel);
            var links = ui.Rect("Connections", map); RunUI.Stretch(links);
            links.gameObject.AddComponent<MapConnectionsGraphic>().Bind(Session);
            foreach (var node in Session.Map.Nodes)
            {
                bool available = Session.CanEnter(node.Id);
                bool visited = HasVisited(node.Id);
                string label = (node.Row + 1).ToString("00") + "  " + ContentCatalog.StageName(node.MapKind);
                if (node.IsBattle && node.MapKind != StageKind.Mystery) label += "\n" + StageCatalog.Find(node.SongId).Music.Name;
                if (visited) label += "\n완료";
                else if (available) label += "\n진입";
                var id = node.Id;
                var button = ui.Button(map, label, () => EnterStage(id), available, available, 86);
                button.name = "Stage " + id;
                var rect = (RectTransform)button.transform;
                var pos = MapConnectionsGraphic.Position(node);
                float halfWidth = .30f / (FieldMap.StageCount - 1);
                rect.anchorMin = new Vector2(pos.x - halfWidth, pos.y);
                rect.anchorMax = new Vector2(pos.x + halfWidth, pos.y);
                rect.sizeDelta = new Vector2(0, node.MapKind == StageKind.Boss ? 116 : 96);
                rect.anchoredPosition = Vector2.zero;
                var colors = button.colors;
                colors.disabledColor = visited ? Color.white : new Color(.65f, .65f, .65f, .85f);
                button.colors = colors;
                if (visited) button.GetComponent<Image>().color = RunUI.Hex("426A65");
                else if (!available && node.MapKind == StageKind.Boss) button.GetComponent<Image>().color = RunUI.Hex("643A53");
                var labelText = button.GetComponentInChildren<Text>(); labelText.fontSize = 26;
                labelText.resizeTextForBestFit = true;
                labelText.resizeTextMinSize = 18; labelText.resizeTextMaxSize = 26;
            }
            var hint = ui.Label(screen, "왼쪽에서 오른쪽으로  ·  밝은 무대를 선택해\n? 지역은 들어가면 정체가 밝혀져.", 20, RunUI.Muted, 56, TextAnchor.MiddleCenter);
            RunUI.Overlay(hint.rectTransform, new Vector2(.20f, 0), new Vector2(.80f, 0), new Vector2(0, 12), new Vector2(0, 68));
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
                    Heading("TUNE YOUR WEAPONS", "강화", "무기 한 개의 능력치를 강화해. 단계마다 기본 수치의 25%, 최대 +3까지 올라.");
                    for (int i = 0; i < Session.Weapons.Count; i++)
                    {
                        int slot = i; var weapon = Session.Weapons[i];
                        int nextLevel = Math.Min(RunRules.MaximumUpgrade, weapon.Level + 1);
                        ui.Button(body, WeaponName(i) + "  →  +" + nextLevel + "\n" +
                            "능력치 " + (100 + 25 * weapon.Level) + "% → " + (100 + 25 * nextLevel) + "%", () =>
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
            var stage = ui.Rect("Preparation arena", screen); RunUI.Stretch(stage);
            preparation = stage.gameObject.AddComponent<BattlePreparationView>();
            preparation.Bind(ui, Session, index => StartPatternPractice(index), OpenCodex, () => OpenMenu(MenuPage.Home),
                () => StartRhythmRound(), monster => OpenCodex(monster));
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
            ui.Label(body, "장착한 무기가 지원하는 행동에 자동으로 대응해.\nHP 0 이후 Overkill을 마치면 클리어야.", 20, RunUI.Muted, 82);
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

        public void OpenPatternPractice()
        {
            if (Session?.BattleLoadout == null || ActiveRound != null) return;
            completedRound = null; menuPage = MenuPage.None; Render();
        }

        public bool StartPatternPractice(int patternIndex)
        {
            if (Session?.BattleLoadout == null || ActiveRound != null || patternIndex < 0 || patternIndex >= Session.BattleLoadout.Patterns.Count) return false;
            ActiveRound = WeaponPractice.Create(Session.BattlePlan, Session.BattleLoadout,
                Session.BattleLoadout.Patterns[patternIndex], Session.EnemyHealth.Maximum, Session.MaxHealth);
            completedRound = null; menuPage = MenuPage.None; Render(); return true;
        }

        public bool StartRhythmRound()
        {
            if (Session == null || Session.Phase != RunPhase.Stage || Session.BattlePlan == null || ActiveRound != null ||
                (preparation != null && preparation.HasPracticeConfirmation)) return false;
            ActiveRound = Session.StartRhythmRound();
            if (ActiveRound == null) return false;
            completedRound = null;
            menuPage = MenuPage.None; pendingOffer = -1; notice = ""; Render(); return true;
        }

        private void FinishRhythmRound(string ticket, RhythmRound round)
        {
            if (ActiveRound != round) return;
            if (round.Combat != null && round.Combat.IsPractice)
            { ActiveRound = round.RepeatPractice(); completedRound = null; Render(); return; }
            // Lethal incoming damage has already ended the run and invalidated its stage ticket.
            if (Session.Phase == RunPhase.GameOver)
            {
                ActiveRound = completedRound = null;
                menuPage = MenuPage.None; pendingOffer = -1; notice = ""; Render(); return;
            }
            if (Session.StageTicket != ticket) return;
            if (round.Combat != null && round.Combat.Victory)
            { Session.ResolveBattle(ticket, true, Session.Health); ActiveRound = completedRound = null; Render(); return; }
            Session.CloseRhythmRound(round);
            completedRound = round; ActiveRound = null; Render();
        }

        private void LeaveRhythmRound(string ticket)
        {
            if (Session.StageTicket != ticket || ActiveRound == null) return;
            if (ActiveRound.Combat != null && ActiveRound.Combat.IsPractice)
            { ActiveRound.Stop(); ActiveRound = completedRound = null; Render(); return; }
            if (ActiveRound.Combat != null && ActiveRound.Combat.Victory) Session.ResolveBattle(ticket, true, Session.Health);
            else Session.CloseRhythmRound(ActiveRound);
            ActiveRound = completedRound = null; notice = ""; Render();
        }

        private void DrawRoundReport()
        {
            Heading("ROUND COMPLETE", "연주 결과", "플레이어와 스테이지의 남은 HP를 유지한 채 다시 준비할 수 있어.");
            var card = ui.Card(body);
            ui.Label(card, completedRound.ScorePercent.ToString("0.0") + "%", 60, RunUI.Teal, 100, TextAnchor.MiddleCenter);
            ui.Label(card, "정확 " + completedRound.PerfectCount + "  ·  반미스 " + completedRound.HalfMissCount + "  ·  미스 " + completedRound.MissCount,
                25, RunUI.TextColor, 60, TextAnchor.MiddleCenter);
            ui.Label(card, "정확 100% · 반미스 50% · 미스 0%로 집계했어.", 20, RunUI.Muted, 48);
            ui.Label(card, "받은 피해 " + completedRound.TotalDamageTaken.ToString("0.##") + "  ·  남은 HP " +
                Session.Health.ToString("0.##") + " / " + Session.MaxHealth, 25, RunUI.Red, 54);
            if (completedRound.Combat != null)
            {
                ui.Label(card, "무기 피해 " + completedRound.Combat.TotalDamage.ToString("0.##") + "  ·  흡수한 피해 " + completedRound.Combat.TotalBlocked.ToString("0.##"), 24, RunUI.Teal, 46);
                ui.Label(card, "스테이지 HP " + completedRound.Combat.EnemyHealth.Current.ToString("0.##") + " / " + completedRound.Combat.EnemyHealth.Maximum, 24, RunUI.Gold, 46);
                for (int slot = 0; slot < completedRound.Combat.Loadout.Equipment.Count; slot++)
                {
                    decimal damage = 0, guard = 0; int triggers = 0;
                    foreach (var activation in completedRound.Combat.Activations) if (activation.Slot == slot)
                    { triggers++; damage += activation.Damage; guard += activation.Guard; }
                    ui.Label(card, WeaponCatalog.Find(completedRound.Combat.Loadout.Equipment[slot].DefinitionId).Name +
                        " · 발동 " + triggers + " / 피해 " + damage.ToString("0.##") + " / 방어막 " + guard.ToString("0.##"), 21, RunUI.Muted, 40);
                }
            }
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
                if (definition.Kind == RewardKind.Weapon) DrawWeaponSummary(card, new WeaponState(definition.Id, offer.Rarity));
                else ui.Label(card, definition.Description, 22, RunUI.Muted, 78);
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
            Heading("FIVE WEAPONS, ONE RHYTHM", WeaponRarities.Name(offer.Rarity) + " " + offer.Content.Name + " 장착", "다섯 무기 중 교체할 슬롯을 골라줘. 교체한 무기와 강화는 사라져.");
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
            ui.Label(card, Session.Map.Theme.Name + " · FIELD " + Session.Map.Number.ToString("00") + "  COMPLETE", 36, RunUI.Gold, 120);
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

        private void OpenCodex() => OpenCodex(null);

        private void OpenCodex(MonsterDefinition monster)
        {
            if (codex != null) return;
            var group = screen.GetComponent<CanvasGroup>(); group.interactable = group.blocksRaycasts = false;
            CanvasGroup menuGroup = null;
            if (menuOverlay != null)
            {
                menuGroup = menuOverlay.GetComponent<CanvasGroup>();
                menuGroup.interactable = menuGroup.blocksRaycasts = false;
            }
            codex = MonsterCodexView.Open(safeArea, ui, () =>
            {
                codex = null;
                if (group != null) group.interactable = group.blocksRaycasts = menuPage == MenuPage.None;
                if (menuGroup != null) menuGroup.interactable = menuGroup.blocksRaycasts = true;
            }, monster);
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
                    ui.Label(body, Session.Map.Theme.Name + " · FIELD " + Session.Map.Number.ToString("00") + "  ·  통과한 스테이지 " + Session.ClearedStages, 25, RunUI.Gold, 54);
                    ui.Button(body, "돌아가기", CloseMenu, primary: true);
                    ui.Button(body, "장비 · 가방 · 증강", () => OpenMenu(MenuPage.Inventory));
                    if (Session.BattlePlan != null) { ui.Button(body, "몬스터 패턴", () => OpenMenu(MenuPage.Patterns)); ui.Button(body, "패턴 연습", OpenPatternPractice); }
                    ui.Button(body, "몬스터 도감", OpenCodex);
                    ui.Button(body, "조작 방법", () => OpenMenu(MenuPage.Help));
                    if (testControls && Session.BattlePlan != null) ui.Button(body, "개발 도구", () => OpenMenu(MenuPage.Development));
                    ui.Button(body, "탐험 종료", () => OpenMenu(MenuPage.Abandon));
                    break;
                case MenuPage.Inventory: DrawInventory(); break;
                case MenuPage.Help:
                    ui.Label(body, "지도는 왼쪽에서 오른쪽으로 진행해.\n시작 지점 네 곳은 모두 몬스터, 여섯 번째 무대는 보스야.\n? 지역은 들어가면 정체가 밝혀져.", 24, RunUI.Muted, 125);
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
                var state = Session.Weapons[i];
                ui.Label(card, (i + 1) + "  " + WeaponName(i), 27, RunUI.TextColor, 44);
                DrawWeaponSummary(card, state);
            }
        }

        private void DrawWeaponSummary(RectTransform parent, WeaponState state)
        {
            var definition = WeaponCatalog.Find(state.DefinitionId);
            var row = ui.Row(parent, 100);
            var icon = ui.Rect("Weapon artwork " + state.DefinitionId, row);
            var layout = RunUI.Size(icon, 100); layout.minWidth = layout.preferredWidth = 100;
            icon.gameObject.AddComponent<WeaponIconGraphic>().Bind(state);
            ui.Label(row, WeaponRarities.Name(state.Rarity) + " · 소켓 " + definition.ActionCountAt(state.Rarity) +
                "개\n" + definition.ActionLabelAt(state.Rarity), 23, WeaponIconGraphic.RarityColor(state.Rarity), 100);
            ui.Label(parent, definition.EffectLabelAt(state.Rarity, state.Level), 21, RunUI.Muted,
                definition.ActionCountAt(state.Rarity) * 60);
        }

        private void DrawInventory()
        {
            ui.Label(body, "HP  " + Session.Health.ToString("0.##") + " / " + Session.MaxHealth + "  ·  " + Session.Gold + " G", 26, RunUI.Teal, 48);
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
            return WeaponRarities.Name(weapon.Rarity) + " " + ContentCatalog.Find(weapon.DefinitionId).Name + " +" + weapon.Level;
        }
    }
}
