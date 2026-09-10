using System;
using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    /// <summary>Read-only presentation of a round. Never resolves notes or applies combat damage.</summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class BattleArenaView : MonoBehaviour
    {
        private sealed class Actor
        {
            public MonsterPlan Plan;
            public RectTransform Root;
            public Image Portrait;
            public Text Signal;
            public Color Tint;
            public Vector2 Ground;
            public Vector2 Impact;
        }

        private readonly List<Actor> monsters = new List<Actor>();
        private readonly List<Image> portraits = new List<Image>();
        private readonly List<BattleEffect> effects = new List<BattleEffect>();
        private readonly HashSet<int> attackTicks = new HashSet<int>();
        private readonly Dictionary<ResponseNote, double> shakeAmounts = new Dictionary<ResponseNote, double>();
        private readonly Dictionary<ResponseNote, double> shakeMovedAt = new Dictionary<ResponseNote, double>();
        private RhythmRound round;
        private RectTransform area;
        private Actor hero;
        private BattleArenaGraphic foreground;
        private Text heroLabel;
        private Vector2 lastSize;
        private bool paused;
        private float weaponEnergy, guardStrength, shakeStrength;
        internal static readonly Vector2 HeroFoot = new Vector2(.24f, .17f);
        internal static readonly Vector2 HeroImpact = new Vector2(.24f, .49f);

        public Image HeroPortrait => hero?.Portrait;
        public IReadOnlyList<Image> MonsterPortraits => portraits;
        public int ActiveResponseEffects { get; private set; }

        public void SetPaused(bool value) { paused = value; }

        public void Initialize(RhythmRound value, Font font)
        {
            if (round != null) throw new InvalidOperationException("Battle arena is already bound.");
            round = value ?? throw new ArgumentNullException(nameof(value));
            area = (RectTransform)transform; var ui = new RunUI(font);
            if (GetComponent<RectMask2D>() == null) gameObject.AddComponent<RectMask2D>();
            var background = ui.Rect("Arena backdrop", area); RunUI.Stretch(background);
            background.gameObject.AddComponent<BattleArenaGraphic>().SetBackdrop(round.Plan.Monsters.Count);
            foreach (var plan in round.Plan.Monsters)
            {
                var actor = CreateActor(ui, plan.InstanceId, plan.Monster.Id, plan.Monster.Name);
                actor.Plan = plan; actor.Tint = MonsterColor(plan.Monster.Id);
                monsters.Add(actor); portraits.Add(actor.Portrait);
            }
            hero = CreateActor(ui, "Weapon master", "weapon-master", null);
            hero.Tint = RunUI.Gold;
            var fx = ui.Rect("Battle effects and five weapons", area); RunUI.Stretch(fx);
            foreground = fx.gameObject.AddComponent<BattleArenaGraphic>();
            heroLabel = ui.Label(area, "WEAPON MASTER", 22, RunUI.Gold, 36, TextAnchor.MiddleCenter);
            Anchor(heroLabel.rectTransform, new Vector2(HeroFoot.x, .06f), new Vector2(HeroFoot.x, .06f),
                Vector2.zero, new Vector2(420, 36), new Vector2(.5f, 0));
            LayoutActors(); Refresh();
        }

        /// <summary>The round's frozen clock also freezes every transform, particle and weapon orbit.</summary>
        public void Refresh()
        {
            if (round == null || paused) return;
            LayoutActors(); effects.Clear(); ActiveResponseEffects = 0;
            double seconds = round.ElapsedSeconds;
            foreach (var actor in monsters) RefreshMonster(actor, seconds);
            RefreshHero(seconds);
            foreground.SetFrame(seconds / round.BeatSeconds, weaponEnergy, guardStrength, shakeStrength, effects);
        }

        public static Vector2 MonsterPosition(int index, int count)
        {
            if (count == 1) return new Vector2(.75f, .38f);
            if (count == 2) return new Vector2(.64f + index * .21f, index == 0 ? .43f : .35f);
            return new Vector2(.58f + index * .16f, index == 1 ? .48f : index == 0 ? .38f : .32f);
        }

        public static float MonsterX(int index, int count) => MonsterPosition(index, count).x;

        private void OnRectTransformDimensionsChange()
        {
            if (hero == null) return;
            LayoutActors();
            Refresh();
        }

        private Actor CreateActor(RunUI ui, string instance, string asset, string label)
        {
            var root = ui.Rect("Actor " + instance, area);
            root.pivot = new Vector2(.5f, 0);
            var spriteRect = ui.Rect("Portrait " + instance, root); RunUI.Stretch(spriteRect);
            var image = spriteRect.gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>("BBSB/BattleArt/" + asset);
            if (image.sprite == null) throw new InvalidOperationException("Missing battle sprite: " + asset);
            image.preserveAspect = true; image.raycastTarget = false;
            var actor = new Actor { Root = root, Portrait = image };
            if (label != null)
            {
                var nameLabel = ui.Label(root, label, 22, RunUI.TextColor, 34, TextAnchor.MiddleCenter);
                Anchor(nameLabel.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, -30), new Vector2(230, 34), new Vector2(.5f, 0));
                actor.Signal = ui.Label(root, "대기", 23, RunUI.Muted, 36, TextAnchor.MiddleCenter);
                Anchor(actor.Signal.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -4), new Vector2(230, 36), new Vector2(.5f, 0));
            }
            return actor;
        }

        private void LayoutActors()
        {
            Vector2 size = area.rect.size;
            if (size == lastSize || size.x <= 0 || size.y <= 0) return;
            lastSize = size;
            float side = Mathf.Min(size.y * (monsters.Count == 1 ? .4f : .36f),
                size.x * (monsters.Count == 1 ? .26f : monsters.Count == 2 ? .21f : .15f));
            for (int i = 0; i < monsters.Count; i++)
            {
                var actor = monsters[i]; actor.Ground = MonsterPosition(i, monsters.Count);
                Anchor(actor.Root, actor.Ground, actor.Ground, Vector2.zero, Vector2.one * side, new Vector2(.5f, 0));
                actor.Impact = actor.Ground + new Vector2(0, side / size.y * .5f);
            }
            float heroSize = Mathf.Min(size.x * .4f, size.y * .7f);
            Anchor(hero.Root, HeroFoot, HeroFoot, Vector2.zero, Vector2.one * heroSize, new Vector2(.5f, 0));
        }

        private void RefreshMonster(Actor actor, double seconds)
        {
            var size = area.rect.size;
            double beat = seconds / round.BeatSeconds;
            float call = 0, attackPulse = 0, windup = 0, counter = 0;
            PlannedAttack current = null;
            foreach (var attack in actor.Plan.Attacks)
            {
                if (seconds >= Time(attack.CallStartTick) && seconds <= Time(attack.ResponseStartTick + actor.Plan.Monster.ResponseTicks + actor.Plan.Monster.RestTicks))
                    current = attack;
                foreach (var signal in attack.Call)
                {
                    double age = seconds - Time(signal.Tick);
                    call += Pulse(age, round.BeatSeconds * .8);
                    Add(BattleEffectKind.Call, actor.Ground, actor.Impact, age, round.BeatSeconds * .85, RunUI.Gold);
                }
            }
            foreach (var note in round.Notes)
            {
                if (note.Attack.MonsterId != actor.Plan.InstanceId) continue;
                // The monster always performs its scheduled attack, including a missed player's note.
                if (attackTicks.Add(note.StartTick)) AttackBeat(note.StartSeconds, actor, seconds, ref attackPulse, ref windup);
                if (note.EndSeconds > note.StartSeconds && attackTicks.Add(note.EndTick))
                    AttackBeat(note.EndSeconds, actor, seconds, ref attackPulse, ref windup);
                if (note.Result != null && note.Result.Grade != RhythmGrade.Miss)
                    counter += Pulse(seconds - note.Result.JudgedAtSeconds - .08, .28) * (float)note.Result.Efficiency;
            }
            attackTicks.Clear();
            call = Mathf.Clamp01(call); attackPulse = Mathf.Clamp01(attackPulse); counter = Mathf.Clamp01(counter);
            float bounce = Mathf.Sin((float)beat * Mathf.PI * 2 + actor.Impact.x * 4);
            float x = 0, y = (bounce + 1) * 1.5f, tilt = bounce * 1.2f, squash = .015f;
            switch (actor.Plan.Monster.Id)
            {
                case "tap-slime": y += call * size.y * .065f; squash += call * .16f; break;
                case "spark-bat": y += 7 + bounce * 5 + call * size.y * .04f; tilt += call * 14; break;
                case "iron-turtle": y -= call * 5; squash += call * .09f; tilt += call * 4; break;
                case "diving-ray": x = bounce * 7; y += 10 + bounce * 6 + call * 12; tilt += call * 13; break;
                case "bubble-spirit": y += 8 + bounce * 7 + call * size.y * .06f; squash += call * .08f; break;
                case "flick-goblin": x += call * 13; tilt -= call * 14; y += call * 8; break;
            }
            float direction = Mathf.Sign(HeroImpact.x - actor.Impact.x);
            x += direction * (attackPulse * size.x * .035f - windup * 6) - direction * counter * 9;
            y += windup * 7 - attackPulse * size.y * .055f + counter * 7;
            tilt += direction * (attackPulse * 9 - counter * 8);
            SetPose(actor, x, y, tilt, 1 + squash * call + attackPulse * .07f,
                1 - squash * call - attackPulse * .035f, Color.Lerp(Color.white, RunUI.Teal, counter * .3f));
            RefreshSignal(actor, current, seconds, call);
        }

        private void AttackBeat(double target, Actor actor, double seconds, ref float pulse, ref float windup)
        {
            double age = seconds - target;
            pulse += Pulse(age, Math.Min(.3, round.BeatSeconds * .65));
            double travel = Math.Min(.28, round.BeatSeconds * .5);
            if (age < 0 && age >= -travel) windup = Mathf.Max(windup, (float)(1 + age / travel));
            Add(BattleEffectKind.Attack, actor.Impact, HeroImpact, age + travel, travel * 2, actor.Tint);
        }

        private void RefreshSignal(Actor actor, PlannedAttack current, double seconds, float pulse)
        {
            var label = actor.Signal;
            if (current == null) { label.text = "대기"; label.color = RunUI.Muted; }
            else if (seconds < Time(current.ResponseStartTick))
            {
                string call = "CALL";
                foreach (var signal in current.Call) if (Time(signal.Tick) <= seconds) call = signal.Label;
                label.text = "CALL · " + call; label.color = RunUI.Gold;
            }
            else if (seconds <= Time(current.ResponseStartTick + actor.Plan.Monster.ResponseTicks) + round.HalfMissWindow)
            { label.text = "RESPONSE"; label.color = RunUI.Teal; }
            else { label.text = "쉬는 박자"; label.color = RunUI.Muted; }
            label.rectTransform.localScale = Vector3.one * (1 + pulse * .1f);
        }

        private void RefreshHero(double seconds)
        {
            var size = area.rect.size;
            float x = 0, y = (float)Math.Sin(seconds / round.BeatSeconds * Math.PI * 2) * 1.5f;
            float tilt = 0, sx = 1, sy = 1, miss = 0;
            weaponEnergy = guardStrength = shakeStrength = 0;
            string action = "WEAPON MASTER"; double lastAction = -1;
            foreach (var note in round.Notes)
            {
                if (note.State != ResponseState.Holding || !round.IsDown) continue;
                switch (note.Step.Kind)
                {
                    case GestureKind.Hold:
                        guardStrength = 1; sy = .95f; sx = 1.04f; action = "방어 유지"; break;
                    case GestureKind.Dive:
                        x = size.x * .035f; sy = .87f; tilt = -8; action = "회피 준비 · 끝에 떼기"; break;
                    case GestureKind.Shake:
                        // Holding is automatic for Shake. Only credited real movement animates the hero.
                        if (!shakeAmounts.TryGetValue(note, out var previous)) previous = 0;
                        if (note.ShakeActiveSeconds > previous + 1e-9) shakeMovedAt[note] = seconds;
                        shakeAmounts[note] = note.ShakeActiveSeconds;
                        if (shakeMovedAt.TryGetValue(note, out var moved)) shakeStrength = Mathf.Max(shakeStrength, Pulse(seconds - moved, .12));
                        if (shakeStrength > 0) action = "무기 휘젓기";
                        break;
                }
            }
            x += Mathf.Sin((float)seconds * 55) * 4 * shakeStrength;
            weaponEnergy = Mathf.Max(guardStrength * .4f, shakeStrength * .8f);
            foreach (var result in round.Results)
            {
                double age = seconds - result.JudgedAtSeconds;
                const double duration = .48;
                if (age < 0 || age >= duration) continue;
                var actor = FindMonster(result.Note.Attack.MonsterId);
                if (actor == null) continue;
                float strength = result.Grade == RhythmGrade.Perfect ? 1 : result.Grade == RhythmGrade.HalfMiss ? .55f : .65f;
                float pulse = Pulse(age, duration) * strength;
                float direction = actor.Impact.x >= HeroImpact.x ? 1 : -1;
                if (result.Grade == RhythmGrade.Miss)
                {
                    miss = Mathf.Max(miss, pulse); x += Mathf.Sin((float)age * 65) * 6 * pulse;
                    y -= 8 * pulse; tilt -= direction * 6 * pulse;
                    Add(BattleEffectKind.Miss, HeroImpact, HeroImpact, age, duration, RunUI.Red, strength);
                }
                else
                {
                    weaponEnergy = Mathf.Max(weaponEnergy, pulse);
                    BattleEffectKind kind = EffectFor(result.Note.Step.Kind);
                    Add(kind, HeroImpact, actor.Impact, age, duration, RhythmPlaybackView.GradeColor(result.Grade), strength);
                    switch (result.Note.Step.Kind)
                    {
                        case GestureKind.Tap: y += size.y * .055f * pulse; tilt -= direction * 8 * pulse; break;
                        case GestureKind.Hold: guardStrength = Mathf.Max(guardStrength, pulse); sy -= .05f * pulse; break;
                        case GestureKind.Dive: x += direction * size.x * .065f * pulse; sy -= .09f * pulse; break;
                        case GestureKind.Flick: y += size.y * .08f * pulse; tilt -= direction * 16 * pulse; break;
                        case GestureKind.Shake: shakeStrength = Mathf.Max(shakeStrength, pulse); x += Mathf.Sin((float)age * 70) * 7 * pulse; break;
                    }
                }
                ActiveResponseEffects++;
                if (result.JudgedAtSeconds >= lastAction)
                {
                    lastAction = result.JudgedAtSeconds;
                    action = result.Grade == RhythmGrade.Miss ? (result.Reason == MissReason.TooEarly ? "너무 이른 동작" : "대응 실패") :
                        ActionLabel(result.Note.Step.Kind) + " · " + RhythmPlaybackView.GradeLabel(result.Grade);
                }
            }
            SetPose(hero, Mathf.Clamp(x, -size.x * .11f, size.x * .11f), Mathf.Clamp(y, -size.y * .035f, size.y * .1f),
                Mathf.Clamp(tilt, -24, 24), Mathf.Clamp(sx, .8f, 1.2f), Mathf.Clamp(sy, .75f, 1.1f), Color.Lerp(Color.white, RunUI.Red, miss * .45f));
            heroLabel.text = action; heroLabel.color = miss > .1f ? RunUI.Red : weaponEnergy > .1f ? RunUI.Teal : RunUI.Gold;
        }

        private void Add(BattleEffectKind kind, Vector2 from, Vector2 to, double age, double duration, Color tint, float strength = 1)
        {
            if (age < 0 || age >= duration) return;
            effects.Add(new BattleEffect { Kind = kind, From = from, To = to, Progress = (float)(age / duration), Tint = tint, Strength = strength });
        }
        private Actor FindMonster(string id)
        { foreach (var actor in monsters) if (actor.Plan.InstanceId == id) return actor; return null; }
        private double Time(int tick) => RhythmTime.Seconds(tick, round.Plan.Stage.Music.Bpm);
        private static float Pulse(double age, double duration)
        { if (age < 0 || age >= duration) return 0; float p = (float)(1 - age / duration); return p * p; }
        private static void SetPose(Actor actor, float x, float y, float tilt, float sx, float sy, Color tint)
        {
            var rect = actor.Portrait.rectTransform;
            rect.anchoredPosition = new Vector2(x, y); rect.localScale = new Vector3(sx, sy, 1);
            rect.localRotation = Quaternion.Euler(0, 0, tilt); actor.Portrait.color = tint;
        }
        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size, Vector2 pivot)
        { rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = position; }
        private static Color MonsterColor(string id)
        {
            switch (id)
            {
                case "tap-slime": return RunUI.Hex("A3E676");
                case "spark-bat": return RunUI.Hex("CE8BFF");
                case "iron-turtle": return RunUI.Gold;
                case "diving-ray": return RunUI.Hex("77BFFF");
                case "bubble-spirit": return RunUI.Teal;
                default: return RunUI.Hex("FFA766");
            }
        }
        private static BattleEffectKind EffectFor(GestureKind kind)
        {
            switch (kind)
            {
                case GestureKind.Hold: return BattleEffectKind.Guard;
                case GestureKind.Dive: return BattleEffectKind.Dive;
                case GestureKind.Flick: return BattleEffectKind.Flick;
                case GestureKind.Shake: return BattleEffectKind.Shake;
                default: return BattleEffectKind.Counter;
            }
        }
        private static string ActionLabel(GestureKind kind)
        {
            switch (kind)
            {
                case GestureKind.Hold: return "받아내기";
                case GestureKind.Dive: return "회피 반격";
                case GestureKind.Flick: return "올려 베기";
                case GestureKind.Shake: return "무기 난무";
                default: return "받아 베기";
            }
        }
    }
}
