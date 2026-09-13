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
        private readonly Text beatLabel, feedback, counters, contact;
        private readonly Text healthLabel, damageLabel;
        private readonly Text enemyLabel;
        private readonly RectTransform enemyFill;
        private readonly Text[] weaponLabels;
        private readonly RectTransform healthFill;
        private readonly Image healthImage;
        private double damageShownAt = double.NegativeInfinity;
        private readonly Image[] pulses;
        private readonly RectTransform songProgress;
        private readonly GameObject pauseOverlay;
        private readonly CanvasGroup pauseInput;
        private readonly Text soundLabel;
        private readonly BattleArenaView arena;
        private readonly List<MonsterCard> monsters = new List<MonsterCard>();
        private int callCursor, resultCursor;

        public RhythmPlaybackView(RectTransform root, RunUI ui, RhythmRound round, RunSession session,
            Action pause, Action resume, Action sound, Action leave, Action codex)
        {
            this.round = round; this.session = session; var music = round.Plan.Stage.Music;
            root.name = "Rhythm playback";
            ui.Background(root, RunUI.Ink, true);
            var stage = ui.Rect("Battle arena", root); RunUI.Stretch(stage);
            arena = stage.gameObject.AddComponent<BattleArenaView>(); arena.Initialize(round, ui.Font);
            // HUD is layered over the full battle scene; it never participates in its layout.
            var song = ui.Label(root, music.Name + "  /  " + music.Bpm + " BPM", 25, RunUI.Gold, 40);
            RunUI.Overlay(song.rectTransform, new Vector2(0, 1), new Vector2(.35f, 1), new Vector2(24, -64), new Vector2(0, -24));
            songProgress = Progress(root, ui, "Song progress", 4);
            var track = (RectTransform)songProgress.parent;
            RunUI.Overlay(track, new Vector2(0, 1), Vector2.one, new Vector2(0, -4), Vector2.zero);

            var beats = ui.Rect("Beat signals", root);
            RunUI.Overlay(beats, new Vector2(.38f, 1), new Vector2(.83f, 1), new Vector2(0, -90), new Vector2(0, -30));
            pulses = new Image[music.BeatsPerBar * 2];
            for (int i = 0; i < pulses.Length; i++)
            {
                var cell = ui.Rect("Beat pulse " + i, beats);
                RunUI.Overlay(cell, new Vector2((float)i / pulses.Length, 0), new Vector2((float)(i + 1) / pulses.Length, 1),
                    new Vector2(3, 0), new Vector2(-3, 0));
                pulses[i] = ui.Background(cell, RunUI.Panel);
                var number = ui.Label(cell, i % 2 == 0 ? (i / 2 + 1).ToString() : "&", i % 2 == 0 ? 34 : 27,
                    RunUI.TextColor, 60, TextAnchor.MiddleCenter);
                RunUI.Stretch(number.rectTransform);
            }
            beatLabel = ui.Label(root, "", 21, RunUI.Muted, 32);
            RunUI.Overlay(beatLabel.rectTransform, new Vector2(0, 1), new Vector2(.36f, 1), new Vector2(24, -96), new Vector2(0, -64));

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
                enemyLabel = ui.Label(root, "", 23, RunUI.Red, 34, TextAnchor.MiddleCenter);
                enemyLabel.name = "Shared stage health";
                RunUI.Overlay(enemyLabel.rectTransform, new Vector2(.38f, 1), new Vector2(.83f, 1), new Vector2(0, -135), new Vector2(0, -101));
                enemyFill = Progress(root, ui, "Shared stage health bar", 8);
                enemyFill.GetComponent<Image>().color = RunUI.Red;
                RunUI.Overlay((RectTransform)enemyFill.parent, new Vector2(.38f, 1), new Vector2(.83f, 1), new Vector2(0, -149), new Vector2(0, -141));
                var strip = ui.Rect("Live weapons", root);
                RunUI.Overlay(strip, new Vector2(.35f, 1), new Vector2(.98f, 1), new Vector2(0, -217), new Vector2(0, -161));
                weaponLabels = new Text[round.Combat.Loadout.Equipment.Count];
                for (int i = 0; i < weaponLabels.Length; i++)
                {
                    var card = ui.Rect("Live weapon " + i, strip); ui.Background(card, RunUI.Panel);
                    RunUI.Overlay(card, new Vector2((float)i / weaponLabels.Length, 0), new Vector2((float)(i + 1) / weaponLabels.Length, 1), new Vector2(3, 0), new Vector2(-3, 0));
                    weaponLabels[i] = ui.Label(card, "", 17, RunUI.Muted, 56, TextAnchor.MiddleCenter); RunUI.Stretch(weaponLabels[i].rectTransform, 3);
                    weaponLabels[i].resizeTextForBestFit = true;
                    weaponLabels[i].resizeTextMinSize = 13; weaponLabels[i].resizeTextMaxSize = 17;
                }
            }
            feedback = ui.Label(root, "Call을 보고 박자를 준비해", 30, RunUI.TextColor, 44, TextAnchor.MiddleCenter);
            feedback.gameObject.name = "Response feedback";
            RunUI.Overlay(feedback.rectTransform, new Vector2(.43f, 0), new Vector2(1, 0), new Vector2(0, 62), new Vector2(-24, 110));
            contact = ui.Label(root, "", 23, RunUI.Teal, 36, TextAnchor.MiddleCenter);
            contact.gameObject.name = "Input status";
            RunUI.Overlay(contact.rectTransform, new Vector2(.43f, 0), new Vector2(1, 0), new Vector2(0, 24), new Vector2(-24, 60));
            ui.FloatingMenu(root, pause);

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
            soundLabel = ui.Button(home, "박자·Call 소리 끄기", sound).GetComponentInChildren<Text>();
            ui.Button(home, round.Combat != null && round.Combat.IsPractice ? "배치로 돌아가기" : "준비로 돌아가기", leave);
            counters = ui.Label(details, "", 26, RunUI.Gold, 58);
            if (round.Combat != null)
                foreach (var state in round.Combat.Loadout.Equipment)
                {
                    var weapon = WeaponCatalog.Find(state.DefinitionId);
                    ui.Label(details, weapon.Name + " +" + state.Level + " · " + weapon.ActionLabel + "\n" + weapon.EffectLabel, 22, RunUI.Teal, 94);
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
        public void SetSound(bool enabled) { soundLabel.text = enabled ? "박자·Call 소리 끄기" : "박자·Call 소리 켜기"; }

        public void Repeat(RhythmRound value)
        {
            if (!ReferenceEquals(round.Plan, value.Plan)) throw new InvalidOperationException("Practice must keep the same plan.");
            round = value; callCursor = resultCursor = 0; damageShownAt = double.NegativeInfinity;
            feedback.text = "Call을 보고 박자를 준비해"; feedback.color = RunUI.TextColor;
            foreach (var monster in monsters) monster.Repeat(value);
            arena.Repeat(value);
        }

        public void Refresh(double seconds, bool waitingForContact)
        {
            bool practice = round.Combat != null && round.Combat.IsPractice;
            decimal health = practice ? round.Combat.PlayerHealth : session.Health;
            healthLabel.text = (practice ? "연습 HP  " : "HP  ") + health.ToString("0.##") + " / " + session.MaxHealth;
            float healthRatio = Mathf.Clamp01((float)(health / session.MaxHealth));
            RefreshWeapons(seconds);
            healthFill.anchorMax = new Vector2(healthRatio, 1);
            healthImage.color = healthLabel.color = healthRatio <= .25f ? RunUI.Red : RunUI.Teal;
            var music = round.Plan.Stage.Music;
            double beat = Math.Min(seconds / round.BeatSeconds, music.BarCount * music.BeatsPerBar - .00001);
            int half = (int)Math.Floor(beat * 2), active = half % pulses.Length;
            float brightness = (float)(1 - (beat * 2 - half));
            for (int i = 0; i < pulses.Length; i++)
                pulses[i].color = i == active ? Color.Lerp(RunUI.Panel, i % 2 == 0 ? RunUI.Gold : RunUI.Teal, .25f + brightness * .55f) : RunUI.Panel;
            beatLabel.text = ((int)beat / music.BeatsPerBar + 1).ToString("00") + " / " + music.BarCount + "마디  ·  " +
                ((int)beat % music.BeatsPerBar + 1) + (half % 2 == 0 ? " 정박" : " 엇박") + "  ·  " +
                Math.Min(seconds, music.DurationSeconds).ToString("0.0") + "초";
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
                feedback.text = string.Join("  /  ", labels);
                feedback.color = freshMiss > 0 ? RunUI.Red : freshHalf > 0 ? RunUI.Gold : RunUI.Teal;
                damageLabel.text = freshDamage > 0 ? "받은 피해 -" + freshDamage.ToString("0.##") : "";
                damageShownAt = freshDamage > 0 ? seconds : double.NegativeInfinity;
            }
            damageLabel.enabled = seconds - damageShownAt < 1;
            counters.text = "정확 " + round.PerfectCount + "  ·  반미스 " + round.HalfMissCount + "  ·  미스 " + round.MissCount +
                "  /  전체 " + round.ResponseNoteCount;
            contact.text = waitingForContact ? "화면을 눌러 연주를 이어가" : round.IsDown ? "누르는 중" : "손을 뗀 상태";
            contact.color = waitingForContact ? RunUI.Gold : round.IsDown ? RunUI.Teal : RunUI.Muted;
            if (!waitingForContact)
            {
                var shakes = new List<string>();
                foreach (var note in round.Notes)
                    if (note.Step.Kind == GestureKind.Shake && note.State == ResponseState.Holding && (round.Combat == null || round.Combat.Allows(note.Attack)))
                        shakes.Add(ShakeStatus(note));
                if (shakes.Count > 0) contact.text = "Shake " + string.Join(" / ", shakes) + "  ·  한 번 왕복";
            }
            if (round.Combat != null && round.Combat.Victory)
            {
                feedback.text = seconds < round.Combat.FinaleAtSeconds ? "OVERKILL · 마지막 패턴을 마무리해" : "STAGE CLEAR";
                feedback.color = RunUI.Gold;
            }
            foreach (var card in monsters) card.Refresh(seconds);
            arena.SetPaused(pauseOverlay.activeSelf || waitingForContact);
            arena.Refresh();
        }

        private void RefreshWeapons(double seconds)
        {
            var combat = round.Combat; if (combat == null) return;
            enemyLabel.text = (combat.IsPractice ? "연습 표적 HP " : "스테이지 HP ") + combat.EnemyHealth.Current.ToString("0.##") + " / " + combat.EnemyHealth.Maximum;
            enemyFill.anchorMax = new Vector2((float)(combat.EnemyHealth.Current / combat.EnemyHealth.Maximum), 1);
            for (int slot = 0; slot < weaponLabels.Length; slot++)
            {
                var definition = WeaponCatalog.Find(combat.Loadout.Equipment[slot].DefinitionId);
                var placement = combat.Loadout.At(slot);
                string state = placement == null ? "미배치" : definition.ActionFor(placement.Kind).Name + " 대기";
                Color tint = RunUI.Muted;
                ResponseNote next = null;
                foreach (var binding in combat.Bindings)
                {
                    var note = binding.Note;
                    if (binding.Slot != slot || note.State == ResponseState.Resolved || !combat.Allows(note.Attack)) continue;
                    if (next == null || note.StartTick < next.StartTick) next = note;
                }
                if (next != null && seconds >= RhythmTime.Seconds(next.Attack.ResponseStartTick, round.Plan.Stage.Music.Bpm))
                {
                    state = next.Step.Kind + (next.State == ResponseState.Holding ?
                        (next.Step.Kind == GestureKind.Shake ? " 한 번 왕복" : " 유지") : " · " + WeaponPreparationView.BeatLabel(next.StartTick - next.Attack.ResponseStartTick));
                    tint = RunUI.Gold;
                }
                for (int i = combat.Activations.Count - 1; i >= 0; i--)
                {
                    var activation = combat.Activations[i];
                    if (activation.Slot != slot || seconds - activation.AtSeconds >= .45) continue;
                    var effects = new List<string>();
                    if (activation.Damage > 0) effects.Add("피해 " + activation.Damage.ToString("0.##"));
                    if (activation.Guard > 0) effects.Add("방어막 +" + activation.Guard.ToString("0.##"));
                    state = activation.Action.Name + " · " + string.Join(" / ", effects);
                    tint = RunUI.Teal; break;
                }
                weaponLabels[slot].text = (slot + 1) + " " + definition.Name + "\n" + state;
                weaponLabels[slot].color = tint;
            }
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
            private readonly Text phase, signal, result;
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
