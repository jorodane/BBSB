using System;
using BBSB.Core;
using BBSB.Runtime.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime
{
    /// <summary>Runtime-built portrait UI. RunSession owns all gameplay mutations.</summary>
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
        private int? fixedSeed;
        private bool testControls;
        private bool title = true;
        private bool inventory;
        private bool confirmAbandon;
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
            scaler.referenceResolution = new Vector2(720, 1280); scaler.matchWidthOrHeight = .5f;
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
            inventory = confirmAbandon = false; pendingOffer = -1; notice = ""; Render(); return true;
        }

        private void StartRun()
        {
            int seed = fixedSeed ?? Guid.NewGuid().GetHashCode();
            if (Session == null) Session = new RunSession(seed, rules); else Session.Restart(seed);
            ActiveRound = completedRound = null;
            title = inventory = confirmAbandon = false; pendingOffer = -1; notice = ""; Render();
        }

        private void Render()
        {
            if (rendering || ui == null) return;
            rendering = true;
            if (screen != null) { screen.gameObject.SetActive(false); Destroy(screen.gameObject); }
            screen = ui.Stack(safeArea, "Run screen", 24, 14); RunUI.Stretch(screen);
            // The live view fits the reference height, including in a wide Editor Game view.
            safeArea.GetComponentInParent<CanvasScaler>().matchWidthOrHeight = ActiveRound != null ? 1 : .5f;
            if (title) { DrawTitle(); rendering = false; return; }
            if (ActiveRound != null)
            {
                string ticket = Session.StageTicket;
                screen.gameObject.AddComponent<RhythmPlayback>().Bind(ActiveRound, ui,
                    round => FinishRhythmRound(ticket, round), () => LeaveRhythmRound(ticket));
                rendering = false; return;
            }
            DrawHeader();
            body = ui.Scroll(screen);
            if (completedRound != null) { DrawRoundReport(); rendering = false; return; }
            if (confirmAbandon) DrawAbandon();
            else if (inventory) DrawInventory();
            else if (pendingOffer >= 0) DrawReplacement();
            else
            {
                switch (Session.Phase)
                {
                    case RunPhase.Map: DrawMap(); break;
                    case RunPhase.Stage: DrawStage(); break;
                    case RunPhase.Reward: DrawRewards(); break;
                    case RunPhase.FieldCleared: DrawFieldCleared(); break;
                    case RunPhase.GameOver: DrawGameOver(); break;
                }
            }
            if (!string.IsNullOrEmpty(notice)) ui.Label(body, notice, 21, RunUI.Teal, 64);
            if (Session.Phase != RunPhase.GameOver) DrawFooter();
            rendering = false;
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
            var header = ui.Stack(screen, "Status", 0, 6);
            var row = ui.Row(header, 38);
            ui.Label(row, "BBSB  /  FIELD " + Session.Map.Number.ToString("00"), 24, RunUI.Gold, 38);
            ui.Label(row, Session.Gold + " G", 25, RunUI.Gold, 38, TextAnchor.MiddleRight);
            var stats = ui.Row(header, 32);
            ui.Label(stats, "HP  " + Session.Health + " / " + Session.MaxHealth, 22, RunUI.Teal, 32);
            ui.Label(stats, "통과한 스테이지  " + Session.ClearedStages, 18, RunUI.Muted, 32, TextAnchor.MiddleRight);
            ui.Bar(header, (float)Session.Health / Session.MaxHealth, RunUI.Teal);
        }

        private void Heading(string kicker, string heading, string description)
        {
            ui.Label(body, kicker, 18, RunUI.Gold, 28);
            ui.Label(body, heading, 34, RunUI.TextColor, 55);
            if (!string.IsNullOrEmpty(description)) ui.Label(body, description, 22, RunUI.Muted, 72);
        }

        private void DrawMap()
        {
            Heading("CHOOSE YOUR PATH", "다음 무대를 골라줘", "아래에서 위로 진행해. 밝게 표시된 무대를 선택할 수 있어.");
            var map = ui.Rect("Field map", body); RunUI.Size(map, 530); ui.Background(map, RunUI.Panel);
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
                rect.anchorMin = new Vector2(pos.x - .135f, pos.y);
                rect.anchorMax = new Vector2(pos.x + .135f, pos.y);
                rect.sizeDelta = new Vector2(0, node.Kind == StageKind.Boss ? 98 : 86);
                rect.anchoredPosition = Vector2.zero;
                var colors = button.colors;
                colors.disabledColor = visited ? Color.white : new Color(.65f, .65f, .65f, .85f);
                button.colors = colors;
                if (visited) button.GetComponent<Image>().color = RunUI.Hex("426A65");
                else if (!available && node.Kind == StageKind.Boss) button.GetComponent<Image>().color = RunUI.Hex("643A53");
                var labelText = button.GetComponentInChildren<Text>(); labelText.fontSize = 21;
            }
            ui.Label(body, "몬스터 · 엘리트 · 강화 · 휴식 · 상점\n04  보스에서 모든 경로가 만나.", 20, RunUI.Muted, 72);
        }

        private bool HasVisited(string id)
        { foreach (var entry in Session.Visited) if (entry == id) return true; return false; }

        private void EnterStage(string nodeId)
        {
            if (!Session.Enter(nodeId)) return;
            ActiveRound = completedRound = null;
            if (Session.CurrentNode.IsBattle) musicPreview.Reset(Session.BattleMusic);
            notice = ""; Render();
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
            Heading("CALL & RESPONSE", "준비하기", "몬스터의 전조를 보고, 이어질 대응 리듬을 확인해.");
            var card = ui.Card(body);
            var plan = Session.BattlePlan;
            ui.Label(card, ContentCatalog.StageName(kind) + "  ·  몬스터 " + plan.Monsters.Count + "마리", 25, RunUI.Red, 42);
            var music = Session.BattleMusic.Music;
            ui.Label(card, music.Name, 32, RunUI.TextColor, 55);
            ui.Label(card, music.Bpm + " BPM  ·  " + music.BarCount + "마디  ·  " + music.DurationSeconds.ToString("0.0") + "초", 23, RunUI.Teal, 45);
            ui.Label(card, "각 몬스터는 아래 패턴을 반복해.\n전조 다음에 같은 리듬으로 대응하면 돼.", 22, RunUI.Muted, 84);
            ui.Button(card, "연주 시작", () => StartRhythmRound(), primary: true, height: 78);
            for (int i = 0; i < plan.Monsters.Count; i++)
                ui.Card(body).gameObject.AddComponent<MonsterPatternView>().Bind(plan.Monsters[i], ui, i + 1);
            if (!testControls) return;
            ui.Label(body, "개발용 계획 요약  ·  공격 " + plan.Attacks.Count + "묶음 / 양보 " + plan.Withdrawals.Count + "묶음", 19, RunUI.Muted, 48);
            ui.Label(body, "샘플 곡은 박자음으로 재생돼.\n입력 결과에 따른 무기 효과와 피해 계산은 다음 단계야.", 20, RunUI.Muted, 82);
            musicPreview.Draw(ui, body, RenderMusicPreview);
            card = ui.Card(body);
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
            inventory = confirmAbandon = false; pendingOffer = -1; notice = ""; Render(); return true;
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
            float offset = body.anchoredPosition.y;
            Render();
            Canvas.ForceUpdateCanvases();
            var scroll = body.GetComponentInParent<ScrollRect>();
            float range = Mathf.Max(0, body.rect.height - scroll.viewport.rect.height);
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

        private void DrawFooter()
        {
            var footer = ui.Stack(screen, "Loadout", 0, 8);
            ui.Label(footer, "WEAPON MASTER  /  5 SLOTS", 17, RunUI.Gold, 26);
            var row = ui.Row(footer, 70, 8);
            for (int i = 0; i < Session.Weapons.Count; i++)
            {
                var slot = ui.Stack(row, "Weapon " + i, 4, 0); ui.Background(slot, RunUI.Panel);
                RunUI.Size(slot, 70, 1).minWidth = 0;
                ui.Label(slot, (i + 1) + "  " + ContentCatalog.Find(Session.Weapons[i].DefinitionId).Name,
                    18, RunUI.TextColor, 34, TextAnchor.MiddleCenter);
                ui.Label(slot, "+" + Session.Weapons[i].Level, 18, RunUI.Teal, 24, TextAnchor.MiddleCenter);
            }
            var actions = ui.Row(footer, 60);
            ui.Button(actions, inventory ? "돌아가기" : "가방 · 증강", () =>
            { inventory = !inventory; pendingOffer = -1; confirmAbandon = false; notice = ""; Render(); }, height: 60);
            ui.Button(actions, "탐험 종료", () =>
            { confirmAbandon = true; inventory = false; pendingOffer = -1; notice = ""; Render(); }, height: 60);
        }

        private void DrawInventory()
        {
            Heading("INVENTORY", "가방과 증강", "이번 탐험에서 얻은 보상이야.");
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
            ui.Button(body, "계속 탐험하기", () => { confirmAbandon = false; Render(); }, primary: true);
            ui.Button(body, "탐험 종료", () => { Session.Abandon(); confirmAbandon = false; Render(); });
        }

        private string WeaponName(int index)
        {
            var weapon = Session.Weapons[index];
            return ContentCatalog.Find(weapon.DefinitionId).Name + " +" + weapon.Level;
        }
    }
}
