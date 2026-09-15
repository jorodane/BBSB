using TMPro;
using System;
using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    /// <summary>Build once, update text/meshes in place. No informational element consumes rhythm input.</summary>
    internal sealed class RhythmPlaybackView
    {
        private RhythmRound round;
        private readonly RunSession session;
        private readonly TextMeshProUGUI combo, feedback, counters;
        private readonly TextMeshProUGUI healthLabel, damageLabel;
        private readonly TextMeshProUGUI enemyLabel;
        private readonly RectTransform enemyFill;
        private readonly RectTransform healthFill;
        private readonly Image healthImage;
        private double damageShownAt = double.NegativeInfinity;
        private double feedbackShownAt = double.NegativeInfinity;
        private readonly RectTransform songProgress;
        private readonly GameObject pauseOverlay;
        private readonly CanvasGroup pauseInput;
        private readonly TextMeshProUGUI soundLabel;
        private readonly BattleArenaView arena;
        private readonly List<MonsterCard> monsters = new List<MonsterCard>();
        private int callCursor, resultCursor;

        public RhythmPlaybackView(RectTransform root, RunUI ui, RhythmRound round, RunSession session,
            Action pause, Action resume, Action sound, Action leave, Action codex)
        {
            this.round = round; this.session = session; var music = round.Plan.Stage.Music;
            root.name = "Rhythm playback";
            var bindings = root.GetComponentInParent<CanvasScreen>()?.battle;
            if (bindings != null && !bindings.IsValid) throw new InvalidOperationException("전투 HUD의 UI 참조를 모두 연결해줘.");
            if (bindings != null)
            {
                arena = bindings.arena.gameObject.AddComponent<BattleArenaView>(); arena.Initialize(round, ui.Font);
                bindings.song.text = music.Name + " / " + music.Bpm + " BPM";
                songProgress = bindings.songFill; healthLabel = bindings.health; healthFill = bindings.healthFill;
                healthImage = healthFill.GetComponent<Image>(); damageLabel = bindings.damage;
                enemyLabel = bindings.enemyHealth; enemyFill = bindings.enemyFill;
                combo = bindings.combo; feedback = bindings.feedback;
                bindings.menu.onClick.AddListener(() => pause()); arena.ShowResponseJudgment = false;
            }
            else
            {
            ui.Background(root, RunUI.Ink, true);
            var stage = ui.Rect("Battle arena", root); RunUI.Stretch(stage);
            arena = stage.gameObject.AddComponent<BattleArenaView>(); arena.Initialize(round, ui.Font);
            // HUD is layered over the full battle scene; it never participates in its layout.
            var song = ui.Label(root, music.Name + "  /  " + music.Bpm + " BPM", 25, RunUI.Gold, 40);
            RunUI.Overlay(song.rectTransform, new Vector2(0, 1), new Vector2(.35f, 1), new Vector2(24, -64), new Vector2(0, -24));
            songProgress = Progress(root, ui, "Song progress", 4);
            var track = (RectTransform)songProgress.parent;
            RunUI.Overlay(track, new Vector2(0, 1), Vector2.one, new Vector2(0, -4), Vector2.zero);

            healthLabel = ui.Label(root, "", 25, RunUI.Teal, 36);
            healthLabel.gameObject.name = "Player health";
            RunUI.Overlay(healthLabel.rectTransform, new Vector2(0, 1), new Vector2(.32f, 1), new Vector2(24, -140), new Vector2(0, -104));
            healthFill = Progress(root, ui, "Player health bar", 10);
            RunUI.Overlay((RectTransform)healthFill.parent, new Vector2(0, 1), new Vector2(.30f, 1), new Vector2(24, -156), new Vector2(0, -146));
            healthImage = healthFill.GetComponent<Image>();
            damageLabel = ui.Label(root, "", 22, RunUI.Red, 34);
            damageLabel.gameObject.name = "Player damage";
            RunUI.Overlay(damageLabel.rectTransform, new Vector2(0, 1), new Vector2(.32f, 1), new Vector2(24, -194), new Vector2(0, -160));

            if (round.Combat != null)
            {
                enemyLabel = ui.Label(root, "", 23, RunUI.Red, 34, TextAlignmentOptions.Center);
                enemyLabel.name = "Shared stage health";
                RunUI.Overlay(enemyLabel.rectTransform, new Vector2(.38f, 1), new Vector2(.83f, 1), new Vector2(0, -135), new Vector2(0, -101));
                enemyFill = Progress(root, ui, "Shared stage health bar", 8);
                enemyFill.GetComponent<Image>().color = RunUI.Red;
                RunUI.Overlay((RectTransform)enemyFill.parent, new Vector2(.38f, 1), new Vector2(.83f, 1), new Vector2(0, -149), new Vector2(0, -141));
            }
            combo = ui.Label(root, "COMBO " + round.Combo, 34, RunUI.Gold, 48, TextAlignmentOptions.Center);
            combo.gameObject.name = "Combo counter";
            RunUI.Overlay(combo.rectTransform, new Vector2(.28f, 0), new Vector2(.72f, 0), new Vector2(0, 62), new Vector2(0, 110));
            combo.enableAutoSizing = true; combo.fontSizeMin = 20; combo.fontSizeMax = 34;
            feedback = ui.Label(root, "", 23, RunUI.TextColor, 36, TextAlignmentOptions.Center);
            feedback.gameObject.name = "Response feedback";
            RunUI.Overlay(feedback.rectTransform, new Vector2(.28f, 0), new Vector2(.72f, 0), new Vector2(0, 24), new Vector2(0, 60));
            feedback.enableAutoSizing = true; feedback.fontSizeMin = 15; feedback.fontSizeMax = 23;
            // Playback owns readiness, aggregate grades and victory; avoid a duplicate arena grade.
            arena.ShowResponseJudgment = false;
            ui.FloatingMenu(root, pause);

            }
            var overlay = ui.Modal(root, "Pause overlay", "일시정지", resume, out var panel);
            pauseInput = overlay.GetComponent<CanvasGroup>();
            // Cache while building: the overlay is inactive when ShowPause opens it again.
            var pauseScroll = panel.GetComponentInParent<ScrollRect>(true);
            var home = ui.Stack(panel, "Pause menu");
            var details = ui.Stack(panel, "Performance details");
            var help = ui.Stack(panel, "Input help");
            Action<GameObject> select = page =>
            {
                home.gameObject.SetActive(page == home.gameObject);
                details.gameObject.SetActive(page == details.gameObject);
                help.gameObject.SetActive(page == help.gameObject);
                pauseScroll.StopMovement();
                pauseScroll.verticalNormalizedPosition = 1;
            };
            resetMenu = () => select(home.gameObject);
            ui.Label(home, "박자와 판정이 멈췄어. 받은 피해는 유지돼.\n유지 중이었다면 이어할 때 화면을 다시 눌러줘.", 24, RunUI.Muted, 92);
            ui.Button(home, "이어하기", resume, primary: true);
            ui.Button(home, "연주 정보 · 패턴", () => select(details.gameObject));
            ui.Button(home, "몬스터 도감", codex);
            ui.Button(home, "조작 방법", () => select(help.gameObject));
            soundLabel = ui.Button(home, "음악·Call 소리 끄기", sound).GetComponentInChildren<TextMeshProUGUI>();
            ui.Button(home, round.Combat != null && round.Combat.IsPractice ? "패턴 목록으로" : "준비로 돌아가기", leave);
            counters = ui.Label(details, "", 26, RunUI.Gold, 58);
            if (round.Combat != null)
                foreach (var state in round.Combat.Loadout.Equipment)
                {
                    var weapon = WeaponCatalog.Find(state.DefinitionId);
                    ui.WeaponActions(details, state);
                    ui.Label(details, WeaponRarities.Name(state.Rarity) + " " + weapon.Name + " +" + state.Level + " · " + weapon.ActionLabelAt(state.Rarity) + "\n" + weapon.EffectLabelAt(state.Rarity, state.Level), 22, WeaponIconGraphic.RarityColor(state.Rarity), (weapon.ActionCountAt(state.Rarity) + 1) * 60);
                }
            foreach (var monster in round.Plan.Monsters) monsters.Add(new MonsterCard(details, ui, round, monster));
            ui.Button(details, "메뉴로 돌아가기", resetMenu);
            ui.Controls(help);
            ui.Button(help, "메뉴로 돌아가기", resetMenu);
            resetMenu();
            pauseOverlay = overlay.gameObject; pauseOverlay.SetActive(false);
        }

        private readonly Action resetMenu;
        public void ShowPause(bool value)
        { pauseOverlay.SetActive(value); if (value) resetMenu(); arena.SetPaused(value); }
        public void SetCodexOpen(bool value)
        {
            pauseInput.interactable = pauseInput.blocksRaycasts = !value;
        }
        public void SetSound(bool enabled) { soundLabel.text = enabled ? "음악·Call 소리 끄기" : "음악·Call 소리 켜기"; }

        public void Repeat(RhythmRound value)
        {
            if (!ReferenceEquals(round.Plan, value.Plan)) throw new InvalidOperationException("Practice must keep the same plan.");
            round = value; callCursor = resultCursor = 0; damageShownAt = double.NegativeInfinity;
            combo.text = "COMBO " + round.Combo;
            feedback.text = ""; feedback.color = RunUI.TextColor; feedbackShownAt = double.NegativeInfinity;
            foreach (var monster in monsters) monster.Repeat(value);
            arena.Repeat(value);
        }

        public void Refresh(double seconds, bool waitingForContact)
        {
            combo.text = "COMBO " + round.Combo;
            bool practice = round.Combat != null && round.Combat.IsPractice;
            decimal health = practice ? round.Combat.PlayerHealth : session.Health;
            healthLabel.text = (practice ? "연습 HP  " : "HP  ") + health.ToString("0.##") + " / " + session.MaxHealth;
            float healthRatio = Mathf.Clamp01((float)(health / session.MaxHealth));
            RefreshEnemyHealth();
            healthFill.anchorMax = new Vector2(healthRatio, 1);
            healthImage.color = healthLabel.color = healthRatio <= .25f ? RunUI.Red : RunUI.Teal;
            var music = round.Plan.Stage.Music;
            songProgress.anchorMax = new Vector2((float)Math.Min(1, seconds / music.DurationSeconds), 1);

            while (callCursor < round.Calls.Count)
            {
                var signal = round.Calls[callCursor++];
                foreach (var card in monsters) if (card.Plan.InstanceId == signal.MonsterId) card.Call(signal);
            }
            int freshPerfect = 0, freshHalf = 0, freshMiss = 0;
            decimal freshDamage = 0;
            while (resultCursor < round.Results.Count)
            {
                var result = round.Results[resultCursor++];
                freshDamage += result.DamageTaken;
                if (result.Grade == RhythmGrade.Perfect) freshPerfect++; else if (result.Grade == RhythmGrade.HalfMiss) freshHalf++; else freshMiss++;
                foreach (var card in monsters) if (card.Plan.InstanceId == result.Note.Attack.MonsterId) card.Result(result);
            }
            if (freshPerfect + freshHalf + freshMiss > 0)
            {
                var labels = new List<string>();
                if (freshPerfect > 0) labels.Add("PERFECT ×" + freshPerfect);
                if (freshHalf > 0) labels.Add("반미스 ×" + freshHalf);
                if (freshMiss > 0) labels.Add("MISS ×" + freshMiss);
                feedback.text = string.Join("  /  ", labels); feedbackShownAt = seconds;
                feedback.color = freshMiss > 0 ? RunUI.Red : freshHalf > 0 ? RunUI.Gold : RunUI.Teal;
                damageLabel.text = freshDamage > 0 ? "받은 피해 -" + freshDamage.ToString("0.##") : "";
                damageShownAt = freshDamage > 0 ? seconds : double.NegativeInfinity;
            }
            damageLabel.enabled = seconds - damageShownAt < 1;
            counters.text = "정확 " + round.PerfectCount + "  ·  반미스 " + round.HalfMissCount + "  ·  미스 " + round.MissCount +
                "  /  전체 " + round.ResponseNoteCount;
            if (seconds - feedbackShownAt >= .7) feedback.text = "";
            if (waitingForContact)
            { feedback.text = "화면을 눌러 연주를 이어가"; feedback.color = RunUI.Gold; }
            if (round.Combat != null && round.Combat.Victory)
            {
                feedback.text = seconds < round.Combat.FinaleAtSeconds ? "OVERKILL · 마지막 패턴을 마무리해" : "STAGE CLEAR";
                feedback.color = RunUI.Gold;
            }
            foreach (var card in monsters) card.Refresh(seconds);
            arena.SetPaused(pauseOverlay.activeSelf || waitingForContact);
            arena.Refresh();
        }

        private void RefreshEnemyHealth()
        {
            var combat = round.Combat; if (combat == null) return;
            enemyLabel.text = (combat.IsPractice ? "연습 표적 HP " : "스테이지 HP ") + combat.EnemyHealth.Current.ToString("0.##") + " / " + combat.EnemyHealth.Maximum;
            enemyFill.anchorMax = new Vector2((float)(combat.EnemyHealth.Current / combat.EnemyHealth.Maximum), 1);
        }

        public static string GradeLabel(RhythmGrade grade) => grade == RhythmGrade.Perfect ? "PERFECT" : grade == RhythmGrade.HalfMiss ? "반미스" : "MISS";
        public static Color GradeColor(RhythmGrade grade) => grade == RhythmGrade.Perfect ? RunUI.Teal : grade == RhythmGrade.HalfMiss ? RunUI.Gold : RunUI.Red;
        private static string ShakeStatus(ResponseNote note) => note.ShakeCompleted ? "왕복 완료" :
            note.State == ResponseState.Resolved ? (note.ShakeProgress >= .5 ? "복귀 미완료" : "왕복 미완료") :
            note.ShakeProgress >= .5 ? "돌아와!" : "흔들어!";

        private static RectTransform Progress(Transform parent, RunUI ui, string name, float height)
        {
            var track = ui.Rect(name, parent); RunUI.Size(track, height); ui.Background(track, RunUI.Panel);
            var fill = ui.Rect("Fill", track); RunUI.Stretch(fill); fill.anchorMax = new Vector2(0, 1);
            ui.Background(fill, RunUI.Teal); return fill;
        }

        private sealed class MonsterCard
        {
            public MonsterPlan Plan { get; }
            private RhythmRound round;
            private readonly TextMeshProUGUI phase, signal, result;
            private readonly MonsterPatternGraphic graphic;
            private ScheduledCall lastCall;

            public MonsterCard(Transform parent, RunUI ui, RhythmRound round, MonsterPlan plan)
            {
                Plan = plan; this.round = round;
                var card = ui.Card(parent, 16); card.name = "Live monster " + plan.InstanceId;
                card.GetComponent<VerticalLayoutGroup>().spacing = 6;
                ui.Label(card, plan.Monster.Name, 27, RunUI.Gold, 40);
                phase = ui.Label(card, "대기", 23, RunUI.Muted, 32);
                signal = ui.Label(card, "Call 대기", 24, RunUI.Muted, 54);
                signal.gameObject.name = "Call signal " + plan.InstanceId;
                var plot = ui.Rect("Live pattern", card); RunUI.Size(plot, 90);
                graphic = plot.gameObject.AddComponent<MonsterPatternGraphic>(); graphic.Bind(plan.Monster.Patterns[0]);
                result = ui.Label(card, "대응 결과", 23, RunUI.Muted, 54);
            }

            public void Call(ScheduledCall value) { lastCall = value; }
            public void Repeat(RhythmRound value)
            { round = value; lastCall = null; result.text = "대응 결과"; result.color = RunUI.Muted; }
            public void Result(RhythmResult value)
            {
                result.text = value.Note.Step.Kind + "  ·  " + GradeLabel(value.Grade);
                result.text += "  ·  피해 " + value.DamageTaken.ToString("0.##");
                if (value.Note.Step.Kind == GestureKind.Shake) result.text += "  ·  " + ShakeStatus(value.Note);
                if (value.Reason == MissReason.TooEarly) result.text += "  너무 일찍 눌렀어";
                else if (value.Reason == MissReason.MissingFlick) result.text += "  튕기며 떼어줘";
                else if (value.Reason == MissReason.MissingShake) result.text += "  한 번 흔들었다 돌아와";
                result.color = GradeColor(value.Grade);
            }

            public void Refresh(double seconds)
            {
                PlannedAttack current = null;
                foreach (var attack in Plan.Attacks)
                {
                    double from = Time(attack.CallStartTick), until = Time(attack.PhraseEndTick + attack.Pattern.RestTicks);
                    if (seconds >= from && seconds < until) current = attack;
                }
                graphic.SetPlayback(round, current, seconds);
                if (current == null)
                { phase.text = "대기"; phase.color = RunUI.Muted; signal.text = "Call 대기"; signal.color = RunUI.Muted; return; }
                double response = Time(current.ResponseStartTick), rest = Time(current.PhraseEndTick);
                if (seconds < response)
                {
                    phase.text = current.Pattern.Name + " · CALL"; phase.color = RunUI.Gold; signal.color = RunUI.Gold;
                    signal.text = (lastCall != null && lastCall.AttackId == current.Id ? lastCall.Label : "CALL") +
                        "  ·  " + ((response - seconds) / round.BeatSeconds).ToString("0.0") + "박 뒤 대응";
                }
                else if (seconds <= rest + round.HalfMissWindow)
                {
                    phase.text = current.Pattern.Name + " · RESPONSE"; phase.color = RunUI.Teal; signal.color = RunUI.Teal;
                    var actions = new List<string>();
                    foreach (var note in round.Notes)
                    {
                        if (note.Attack != current || note.State == ResponseState.Resolved) continue;
                        if (note.State == ResponseState.Holding)
                            actions.Add(note.Step.Kind + (note.Step.Kind == GestureKind.Dive ? " 끝에 떼기" : note.Step.Kind == GestureKind.Shake ?
                                " " + ShakeStatus(note) : " 유지"));
                        else if (note.StartSeconds - seconds <= round.BeatSeconds)
                            actions.Add(note.Step.Kind + " " + Math.Max(0, (note.StartSeconds - seconds) / round.BeatSeconds).ToString("0.0") + "박");
                    }
                    signal.text = actions.Count > 0 ? string.Join(" · ", actions) : "대응 완료";
                }
                else { phase.text = "REST"; phase.color = RunUI.Muted; signal.text = "쉬는 박자"; signal.color = RunUI.Muted; }
            }
            private double Time(int tick) => RhythmTime.Seconds(tick, round.Plan.Stage.Music.Bpm);
        }
    }
}
