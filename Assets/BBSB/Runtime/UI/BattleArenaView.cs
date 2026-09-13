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
            public RectTransform Labels;
            public Image Portrait;
            public Image BlendPortrait;
            public float PoseBlend;
            public Sprite FallbackPortrait;
            public bool UsesNewArt;
            public bool UsesAuthoredPose;
            public Text Signal;
            public Color Tint;
            public Vector2 Ground;
            public Vector2 Impact;
            public MonsterStageMotion StageMotion;
            public float Advance;
            public int Order;
        }

        private readonly List<Actor> monsters = new List<Actor>();
        private readonly List<Actor> depthOrder = new List<Actor>();
        private readonly List<BattleGroundShadow> shadows = new List<BattleGroundShadow>();
        private readonly List<Image> portraits = new List<Image>();
        private readonly List<BattleEffect> effects = new List<BattleEffect>();
        private PlayerMotionTimeline playerMotion = new PlayerMotionTimeline();
        private PlayerMotionSprites playerSprites;
        [SerializeField] private PlayerMotionDisplay playerDisplay;
        [SerializeField] private MonsterAttackDisplay monsterAttackDisplay;
        private MonsterAttackView monsterAttacks;
        private RhythmRound round;
        private RectTransform area;
        private RectTransform actorLayer, labelLayer;
        private Actor hero;
        private BattleArenaGraphic foreground;
        private BattleArenaGraphic backdrop;
        private WeaponBattleGraphic weaponGraphic;
        private Text heroLabel;
        private bool paused;
        private float weaponEnergy, guardStrength, shakeStrength;
        private float heroDisplayHeight;
        public Vector2 HeroGroundPosition { get; private set; } = PlayerMotionDisplay.DefaultGround;
        public Vector2 HeroImpactPosition { get; private set; } = new Vector2(.14f, .31f);

        public Image HeroPortrait => hero?.Portrait;
        public IReadOnlyList<Image> MonsterPortraits => portraits;
        public MonsterAttackView MonsterAttacks => monsterAttacks;
        public int ActiveResponseEffects { get; private set; }
        public PlayerMotionFrame CurrentHeroMotion { get; private set; }

        public void SetPaused(bool value) { paused = value; }

        public void Repeat(RhythmRound value)
        {
            if (round == null || !ReferenceEquals(round.Plan, value.Plan))
                throw new InvalidOperationException("An arena can only repeat its current plan.");
            round = value; playerMotion = new PlayerMotionTimeline(); paused = false;
            monsterAttacks.Repeat(value); Refresh();
        }

        internal void HideLabels() { labelLayer.gameObject.SetActive(false); heroLabel.gameObject.SetActive(false); }

        public void Initialize(RhythmRound value, Font font, PlayerMotionDisplay display = null)
        {
            if (round != null) throw new InvalidOperationException("Battle arena is already bound.");
            round = value ?? throw new ArgumentNullException(nameof(value));
            if (display != null) playerDisplay = display;
            else if (playerDisplay == null) playerDisplay = Resources.Load<PlayerMotionDisplay>(PlayerMotionDisplay.ResourcePath);
            if (monsterAttackDisplay == null) monsterAttackDisplay = Resources.Load<MonsterAttackDisplay>(MonsterAttackDisplay.ResourcePath);
            area = (RectTransform)transform; var ui = new RunUI(font);
            if (GetComponent<RectMask2D>() == null) gameObject.AddComponent<RectMask2D>();
            var background = ui.Rect("Arena backdrop", area); RunUI.Stretch(background);
            backdrop = background.gameObject.AddComponent<BattleArenaGraphic>();
            backdrop.SetBackdrop();
            actorLayer = ui.Rect("Actors sorted by ground depth", area); RunUI.Stretch(actorLayer);
            var attacks = ui.Rect("Monster attack image slots", area); RunUI.Stretch(attacks);
            monsterAttacks = attacks.gameObject.AddComponent<MonsterAttackView>();
            monsterAttacks.Initialize(round, ui, monsterAttackDisplay);
            var fx = ui.Rect("Battle effects and five weapons", area); RunUI.Stretch(fx);
            foreground = fx.gameObject.AddComponent<BattleArenaGraphic>();
            if (round.Combat != null)
            {
                foreground.ShowLegacyWeapons = false;
                var weapons = ui.Rect("Equipped weapon attacks", area); RunUI.Stretch(weapons);
                weaponGraphic = weapons.gameObject.AddComponent<WeaponBattleGraphic>();
            }
            labelLayer = ui.Rect("Actor labels", area); RunUI.Stretch(labelLayer);
            foreach (var plan in round.Plan.Monsters)
            {
                var actor = CreateActor(ui, plan.InstanceId, plan.Monster.ArtId, plan.Monster.Name);
                actor.Plan = plan; actor.Tint = MonsterColor(plan.Monster.Id);
                actor.StageMotion = new MonsterStageMotion(plan, round.Plan.Stage.Music.Bpm, round.HalfMissWindow);
                actor.Order = monsters.Count;
                monsters.Add(actor); portraits.Add(actor.Portrait);
                depthOrder.Add(actor);
            }
            playerSprites = new PlayerMotionSprites();
            hero = CreateActor(ui, "Weapon master", "weapon-master", null, playerSprites.Get("idle", 0));
            hero.Root.name = "Player ground";
            hero.Tint = RunUI.Gold;
            hero.Order = monsters.Count; depthOrder.Add(hero);
            heroLabel = ui.Label(area, "WEAPON MASTER", 22, RunUI.Gold, 36, TextAnchor.MiddleCenter);
            LayoutActors(); Refresh();
        }

        /// <summary>Pause freezes poses/effects; explicit layout or display-setting changes still apply.</summary>
        public void Refresh()
        {
            if (round == null) return;
            if (!paused) foreach (var actor in monsters)
            {
                actor.Advance = round.Combat != null && round.Combat.Victory ?
                    (float)actor.StageMotion.Evaluate(round.Combat.DefeatedAtSeconds) * Mathf.Clamp01(1 - (float)((round.ElapsedSeconds - round.Combat.DefeatedAtSeconds) / .3)) :
                    (float)actor.StageMotion.Evaluate(round.ElapsedSeconds);
                actor.Portrait.sprite = monsterAttacks.Sprites.BodyBlend(actor.Plan, round.ElapsedSeconds, round.BeatSeconds,
                    actor.FallbackPortrait, round.Combat != null && round.Combat.Victory,
                    out var next, out actor.PoseBlend, out actor.UsesAuthoredPose);
                actor.BlendPortrait.sprite = next; actor.BlendPortrait.enabled = actor.PoseBlend > 0;
                actor.UsesNewArt = actor.Portrait.sprite != actor.FallbackPortrait;
            }
            LayoutActors();
            if (paused)
            {
                ApplyHeroLayout();
                monsterAttacks.Refresh(round.ElapsedSeconds, HeroGroundPosition, heroDisplayHeight);
                return;
            }
            effects.Clear(); ActiveResponseEffects = 0;
            double seconds = round.ElapsedSeconds;
            foreach (var actor in monsters) RefreshMonster(actor, seconds);
            RefreshHero(seconds);
            monsterAttacks.Refresh(seconds, HeroGroundPosition, heroDisplayHeight);
            foreground.SetFrame(seconds / round.BeatSeconds, weaponEnergy, guardStrength, shakeStrength, effects);
        }

        public static Vector2 MonsterPosition(int index, int count)
        {
            var position = BattleStageLayout.Monster(index, count,
                PlayerMotionDisplay.DefaultGround.x, PlayerMotionDisplay.DefaultGround.y, 0);
            return new Vector2((float)position.X, (float)position.Y);
        }

        public static float MonsterX(int index, int count) => MonsterPosition(index, count).x;

        private void OnRectTransformDimensionsChange()
        {
            if (hero == null) return;
            LayoutActors();
            Refresh();
        }

        private void OnDestroy() { playerSprites?.Dispose(); }

        private Actor CreateActor(RunUI ui, string instance, string asset, string label, Sprite portrait = null)
        {
            var root = ui.Rect("Actor " + instance, actorLayer);
            root.pivot = new Vector2(.5f, 0);
            var spriteRect = ui.Rect("Portrait " + instance, root); RunUI.Stretch(spriteRect);
            var image = spriteRect.gameObject.AddComponent<Image>();
            image.sprite = portrait != null ? portrait : Resources.Load<Sprite>("BBSB/BattleArt/" + asset);
            if (image.sprite == null) throw new InvalidOperationException("Missing battle sprite: " + asset);
            image.preserveAspect = true; image.raycastTarget = false;
            var actor = new Actor { Root = root, Portrait = image, FallbackPortrait = image.sprite };
            if (label != null)
            {
                var blend = ui.Rect("Next body pose", root);
                actor.BlendPortrait = ui.Background(blend, Color.clear);
                actor.BlendPortrait.preserveAspect = false; actor.BlendPortrait.enabled = false;
                actor.Labels = ui.Rect("Labels " + instance, labelLayer);
                var nameLabel = ui.Label(actor.Labels, label, 20, RunUI.TextColor, 30, TextAnchor.MiddleCenter);
                Anchor(nameLabel.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, -29), new Vector2(210, 30), new Vector2(.5f, 0));
                actor.Signal = ui.Label(actor.Labels, "대기", 21, RunUI.Muted, 32, TextAnchor.MiddleCenter);
                Anchor(actor.Signal.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, 4), new Vector2(230, 32), new Vector2(.5f, 0));
            }
            return actor;
        }

        private void LayoutActors()
        {
            Vector2 size = area.rect.size;
            if (size.x <= 0 || size.y <= 0) return;
            HeroGroundPosition = playerDisplay != null ? playerDisplay.groundPosition : PlayerMotionDisplay.DefaultGround;
            heroDisplayHeight = PlayerMotionDisplay.ReferenceDisplayHeight(size,
                playerDisplay != null ? playerDisplay.characterScale : PlayerMotionDisplay.DefaultCharacterScale);
            var punch = monsterAttackDisplay != null ? monsterAttackDisplay.Socket(GestureKind.Tap) : MonsterAttackDisplay.DefaultSocket(GestureKind.Tap);
            HeroImpactPosition = HeroGroundPosition + new Vector2(punch.x * heroDisplayHeight / size.x, punch.y * heroDisplayHeight / size.y);
            hero.Ground = HeroGroundPosition;
            Anchor(hero.Root, HeroGroundPosition, HeroGroundPosition, Vector2.zero, Vector2.zero, new Vector2(.5f, 0));
            var labelPoint = HeroGroundPosition - new Vector2(0, .09f);
            Anchor(heroLabel.rectTransform, labelPoint, labelPoint, Vector2.zero, new Vector2(420, 36), new Vector2(.5f, 0));
            backdrop.SetHeroAnchors(HeroGroundPosition, HeroImpactPosition);
            foreground.SetHeroAnchors(HeroGroundPosition, HeroImpactPosition);
            shadows.Clear();
            shadows.Add(new BattleGroundShadow { Ground = HeroGroundPosition,
                Radius = new Vector2(heroDisplayHeight * .26f / size.x, heroDisplayHeight * .045f / size.y) });
            for (int i = 0; i < monsters.Count; i++)
            {
                var actor = monsters[i];
                var position = BattleStageLayout.Monster(i, monsters.Count, HeroGroundPosition.x, HeroGroundPosition.y, actor.Advance);
                actor.Ground = new Vector2((float)position.X, (float)position.Y);
                float side = (float)BattleStageLayout.MonsterSize(monsters.Count, size.x, size.y, position.Scale);
                Anchor(actor.Root, actor.Ground, actor.Ground, Vector2.zero, Vector2.zero, new Vector2(.5f, 0));
                var sprite = actor.Portrait.sprite;
                var foot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
                // Rotation and squash now pivot at the contact point instead of floating around the image center.
                var rect = actor.Portrait.rectTransform;
                var portraitSize = new Vector2(side * sprite.rect.width / sprite.rect.height, side);
                Anchor(rect, Vector2.zero, Vector2.zero, rect.anchoredPosition, portraitSize, foot);
                actor.Portrait.preserveAspect = false;
                var next = actor.BlendPortrait.sprite ?? sprite;
                var nextRect = actor.BlendPortrait.rectTransform;
                Anchor(nextRect, Vector2.zero, Vector2.zero, nextRect.anchoredPosition,
                    new Vector2(side * next.rect.width / next.rect.height, side),
                    new Vector2(next.pivot.x / next.rect.width, next.pivot.y / next.rect.height));
                float bodyHeight = side * (1 - foot.y);
                Anchor(actor.Labels, actor.Ground, actor.Ground, Vector2.zero, new Vector2(side, bodyHeight), new Vector2(.5f, 0));
                actor.Impact = actor.Ground + new Vector2(0, bodyHeight / size.y * .48f);
                shadows.Add(new BattleGroundShadow { Ground = actor.Ground,
                    Radius = new Vector2(side * .35f / size.x, side * .065f / size.y), Advance = actor.Advance });
            }
            depthOrder.Sort((a, b) => a.Ground.y != b.Ground.y ? b.Ground.y.CompareTo(a.Ground.y) : a.Order.CompareTo(b.Order));
            for (int i = 0; i < depthOrder.Count; i++) depthOrder[i].Root.SetSiblingIndex(i);
            backdrop.SetGroundShadows(shadows);
        }

        private void ApplyHeroLayout()
        {
            if (heroDisplayHeight <= 0) return;
            var sprite = hero.Portrait.sprite;
            var reference = playerDisplay != null && playerDisplay.referencePose != null ?
                playerDisplay.referencePose : playerSprites.Get("idle", 0);
            float scale = 1; Vector2 offset = Vector2.zero;
            if (playerDisplay != null && !playerSprites.UsesFallbackPortrait)
                playerDisplay.GetCalibration(CurrentHeroMotion.SourceSheet, CurrentHeroMotion.SourceIndex, out scale, out offset);
            var size = PlayerMotionDisplay.Measure(sprite, reference, heroDisplayHeight, scale);
            var foot = playerSprites.UsesFallbackPortrait ? new Vector2(.5f, 0) :
                new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            // UI Image does not use Sprite.pivot to place its rectangle. Make the source foot
            // pivot the UI pivot explicitly, then size in source units instead of fitting a box.
            Anchor(hero.Portrait.rectTransform, Vector2.zero, Vector2.zero, offset * heroDisplayHeight, size, foot);
            hero.Portrait.preserveAspect = false; // The rectangle already has the exact source aspect.
            hero.Portrait.useSpriteMesh = false;
            var deformation = PlayerSquashStretch.Calculate(CurrentHeroMotion, round.BeatSeconds,
                playerDisplay != null ? playerDisplay.squashStretchStrength : PlayerSquashStretch.DefaultStrength);
            // The Image pivot is the authored foot, so deformation cannot move its ground point.
            hero.Portrait.rectTransform.localScale = new Vector3((float)deformation.X, (float)deformation.Y, 1);
            hero.Portrait.rectTransform.localRotation = Quaternion.identity;
        }

        private void RefreshMonster(Actor actor, double seconds)
        {
            var size = area.rect.size;
            double beat = seconds / round.BeatSeconds;
            float call = 0, counter = 0;
            float bounce = Mathf.Sin((float)beat * Mathf.PI * 2 + actor.Impact.x * 4);
            float x = 0, y = 0, tilt = 0, sx = 1, sy = 1, flash = 0;
            // Species provide the idle pose; each individual Call chooses its own readable action.
            if (!actor.UsesNewArt) switch (actor.Plan.Monster.ArtId)
            {
                case "spark-bat": y += 7 + bounce * 5; break;
                case "diving-ray": x = bounce * 7; y += 10 + bounce * 6; break;
                case "bubble-spirit": y += 8 + bounce * 7; break;
            }
            PlannedAttack current = null;
            foreach (var attack in actor.Plan.Attacks)
            {
                if (round.Combat != null && round.Combat.Victory) continue;
                if (seconds >= Time(attack.CallStartTick) && seconds <= Time(attack.PhraseEndTick + attack.Pattern.RestTicks))
                    current = attack;
                for (int i = 0; i < attack.Call.Count; i++)
                {
                    var signal = attack.Call[i];
                    double age = seconds - Time(signal.Tick);
                    float amount = Pulse(age, MonsterBodyTimeline.CallDuration(attack, i, round.BeatSeconds));
                    call += amount;
                    if (!actor.UsesAuthoredPose) ApplyCallPose(signal.Motion, amount, size, ref x, ref y, ref tilt, ref sx, ref sy);
                    if (signal.Motion == CallMotion.Flash) flash = Mathf.Max(flash, amount);
                    Add(CallEffect(signal.Motion), actor.Ground, actor.Impact, age, round.BeatSeconds * .85, CueColor(signal.Motion));
                }
            }
            foreach (var note in round.Notes)
            {
                if (note.Attack.MonsterId != actor.Plan.InstanceId ||
                    (round.Combat != null && !round.Combat.AllowsEnemyEffect(seconds))) continue;
                if (round.Combat == null && note.Result != null && note.Result.Grade != RhythmGrade.Miss)
                    counter += Pulse(seconds - note.Result.JudgedAtSeconds - .08, .28) * (float)note.Result.Efficiency;
            }
            if (round.Combat != null)
                foreach (var activation in round.Combat.Activations)
                    if (activation.Damage > 0 && activation.Target.MonsterId == actor.Plan.InstanceId)
                        counter += Pulse(seconds - activation.AtSeconds - WeaponMotion.Duration(activation.Action.Motion) * .6, .22);
            call = Mathf.Clamp01(call); counter = Mathf.Clamp01(counter);
            float direction = Mathf.Sign(HeroImpactPosition.x - actor.Impact.x);
            x -= direction * counter * 9;
            y += counter * 7;
            tilt -= direction * counter * 8;
            Color baseTint = actor.UsesNewArt || actor.Plan.Monster.ArtId == actor.Plan.Monster.Id ? Color.white : Color.Lerp(Color.white, actor.Tint, .35f);
            SetPose(actor, x, y, tilt, sx, sy,
                Color.Lerp(Color.Lerp(baseTint, CueColor(CallMotion.Flash), flash * .45f), RunUI.Teal, counter * .3f));
            RefreshSignal(actor, current, seconds, call);
            if (round.Combat != null && round.Combat.Victory)
            { actor.Signal.text = "격파"; actor.Signal.color = RunUI.Teal; }
        }

        private void RefreshSignal(Actor actor, PlannedAttack current, double seconds, float pulse)
        {
            var label = actor.Signal;
            if (current == null) { label.text = "대기"; label.color = RunUI.Muted; }
            else if (seconds < Time(current.ResponseStartTick))
            {
                string call = "CALL"; Color tint = RunUI.Gold;
                foreach (var signal in current.Call) if (Time(signal.Tick) <= seconds)
                { call = signal.Label; tint = CueColor(signal.Motion); }
                label.text = "CALL · " + call; label.color = tint;
                if (current.Pattern.SilentWaitTicks > 0 &&
                    seconds >= Time(current.ResponseStartTick - current.Pattern.SilentWaitTicks) + round.BeatSeconds)
                { label.text = current.Pattern.Id == "clock-seven-beat-wait" ? "쉼 · 인형의 걸음 따라가기" : "쉼 · 박자 기억하기"; label.color = RunUI.Muted; }
            }
            else if (seconds <= Time(current.PhraseEndTick) + round.HalfMissWindow)
            { label.text = "RESPONSE"; label.color = RunUI.Teal; }
            else { label.text = "쉬는 박자"; label.color = RunUI.Muted; }
            // A linked cadence may cue its next Tap while the player responds to the previous one.
            if (current?.Chain != null && seconds < Time(current.ResponseStartTick))
                foreach (var note in round.Notes)
                    if (note.Attack.MonsterId == actor.Plan.InstanceId && seconds >= note.StartSeconds &&
                        seconds <= note.StartSeconds + round.HalfMissWindow)
                    { label.text += " · TAP"; break; }
            label.rectTransform.localScale = Vector3.one * (1 + pulse * .1f);
        }

        private void RefreshHero(double seconds)
        {
            float miss = 0;
            weaponEnergy = guardStrength = shakeStrength = 0;
            CurrentHeroMotion = playerMotion.Evaluate(round);
            var motion = CurrentHeroMotion;
            hero.Portrait.sprite = playerSprites.Get(motion);
            ApplyHeroLayout();
            string action = "WEAPON MASTER";
            if (motion.IsFreeInput)
                action = ActionLabel(motion.Kind.Value, motion.Punch) + " · 공미스";
            else if (motion.Phase == PlayerMotionPhase.Sustain)
            {
                switch (motion.Kind)
                {
                    case GestureKind.Hold:
                        guardStrength = 1; action = "크로스가드 유지"; break;
                    case GestureKind.Dive:
                        action = "회피 준비 · 끝에 떼기"; break;
                    case GestureKind.Shake:
                        shakeStrength = 1; action = "양손 밀쳐내기"; break;
                }
            }
            else if (motion.Kind.HasValue)
            {
                float strength = motion.Grade == RhythmGrade.Perfect ? 1 : motion.Grade == RhythmGrade.HalfMiss ? .55f : .65f;
                float pulse = Pulse(motion.Age, .48) * strength;
                bool failed = motion.Grade == RhythmGrade.Miss;
                miss = failed ? pulse : motion.Grade == RhythmGrade.HalfMiss ? pulse * .3f : 0;
                weaponEnergy = failed ? 0 : pulse;
                // Pose changes supply the body motion. Only separate effects/tints react here.
                if (!failed) switch (motion.Kind.Value)
                {
                    case GestureKind.Hold: guardStrength = pulse; break;
                    case GestureKind.Shake: shakeStrength = pulse; break;
                }
                action = motion.Reason == MissReason.TooEarly ? "너무 이른 동작" :
                    ActionLabel(motion.Kind.Value, motion.Punch) + " · " + RhythmPlaybackView.GradeLabel(motion.Grade.Value);
                if (motion.Phase == PlayerMotionPhase.Recover && motion.IsFall)
                    action = motion.Index == 4 ? "잠깐 정비" : "다시 준비";
            }
            weaponEnergy = Mathf.Max(weaponEnergy, Mathf.Max(guardStrength * .4f, shakeStrength * .8f));
            RefreshContactFeedback(seconds);
            foreach (var result in round.Results)
            {
                double age = seconds - result.JudgedAtSeconds;
                const double duration = .48;
                if (age < 0 || age >= duration) continue;
                var actor = FindMonster(result.Note.Attack.MonsterId);
                if (actor == null) continue;
                float strength = result.Grade == RhythmGrade.Perfect ? 1 : result.Grade == RhythmGrade.HalfMiss ? .55f : .65f;
                if (result.BlockedDamage > 0)
                    Add(BattleEffectKind.ShieldAbsorb, HeroImpactPosition, HeroImpactPosition, age, duration, RunUI.Teal);
                if (result.Grade == RhythmGrade.Miss && result.DamageTaken > 0)
                {
                    Add(BattleEffectKind.Miss, HeroImpactPosition, HeroImpactPosition, age, duration, RunUI.Red, strength);
                }
                else if (round.Combat == null)
                {
                    BattleEffectKind kind = EffectFor(result.Note.Step.Kind);
                    Add(kind, HeroImpactPosition, actor.Impact, age, duration, RhythmPlaybackView.GradeColor(result.Grade), strength);
                }
                ActiveResponseEffects++;
            }
            if (round.Combat != null)
            {
                weaponGraphic.SetFrame(round.Combat, seconds, HeroGroundPosition, HeroImpactPosition);
                foreach (var activation in round.Combat.Activations)
                {
                    double age = seconds - activation.AtSeconds;
                    if (age < 0 || age >= WeaponMotion.Duration(activation.Action.Motion)) continue;
                    var target = FindMonster(activation.Target.MonsterId);
                    if (target != null) { weaponGraphic.SetActivation(activation, target.Impact); ActiveResponseEffects++; }
                }
                weaponGraphic.Refresh();
            }
            hero.Portrait.color = Color.Lerp(Color.white, RunUI.Red, miss * .45f);
            heroLabel.text = action; heroLabel.color = miss > .1f ? RunUI.Red : weaponEnergy > .1f ? RunUI.Teal : RunUI.Gold;
        }

        private void Add(BattleEffectKind kind, Vector2 from, Vector2 to, double age, double duration, Color tint, float strength = 1)
        {
            if (age < 0 || age >= duration) return;
            effects.Add(new BattleEffect { Kind = kind, From = from, To = to, Progress = (float)(age / duration), Tint = tint, Strength = strength });
        }

        private void RefreshContactFeedback(double seconds)
        {
            if (round.Combat != null && !round.Combat.AllowsEnemyEffect(seconds)) return;
            var size = area.rect.size;
            foreach (var note in round.Notes)
            {
                var feedback = MonsterAttackTimeline.ContactFeedback(note, seconds, round.BeatSeconds, round.HalfMissWindow);
                if (!feedback.Active) continue;
                var definition = MonsterAttackCatalog.For(note);
                var calibration = monsterAttackDisplay != null ? monsterAttackDisplay.Find(definition) : null;
                var socket = monsterAttackDisplay != null ? monsterAttackDisplay.Socket(note.Step.Kind) : MonsterAttackDisplay.DefaultSocket(note.Step.Kind);
                socket += calibration?.targetOffset ?? Vector2.zero;
                var point = HeroGroundPosition + new Vector2(socket.x * heroDisplayHeight / size.x, socket.y * heroDisplayHeight / size.y);
                bool dive = note.Step.Kind == GestureKind.Dive, shake = note.Step.Kind == GestureKind.Shake;
                var kind = feedback.IsEnding ? (dive ? BattleEffectKind.DiveEnd : BattleEffectKind.SustainEnd) :
                    shake ? BattleEffectKind.ShakeCharge : dive ? BattleEffectKind.DiveSustain : BattleEffectKind.HoldSustain;
                var tint = feedback.IsEnding ? RunUI.Gold : round.IsDown ? RunUI.Teal : RunUI.Muted;
                if (shake && feedback.IsEnding && note.Result != null) tint = RhythmPlaybackView.GradeColor(note.Result.Grade);
                effects.Add(new BattleEffect { Kind = kind, From = point, To = point, Tint = tint, Strength = 1,
                    Progress = (float)(feedback.IsEnding ? feedback.EndProgress : feedback.Progress), Pulse = (float)feedback.Pulse,
                    Beats = shake ? 2 : Math.Max(1, note.Step.DurationTicks / RhythmTime.TicksPerBeat) });
            }
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
            rect.localRotation = Quaternion.Euler(0, 0, tilt);
            if (actor.BlendPortrait != null)
            {
                var next = actor.BlendPortrait.rectTransform;
                next.anchoredPosition = rect.anchoredPosition; next.localScale = rect.localScale; next.localRotation = rect.localRotation;
                var paint = tint; paint.a *= actor.PoseBlend; actor.BlendPortrait.color = paint;
                tint.a *= 1 - actor.PoseBlend;
            }
            actor.Portrait.color = tint;
        }
        private static void ApplyCallPose(CallMotion motion, float p, Vector2 size,
            ref float x, ref float y, ref float tilt, ref float sx, ref float sy)
        {
            switch (motion)
            {
                case CallMotion.Hop: y += size.y * .065f * p; sx += .14f * p; sy -= .14f * p; break;
                case CallMotion.Step: x += 16 * p; y += 4 * p; tilt -= 9 * p; break;
                case CallMotion.Stomp: sx += .2f * p; sy -= .16f * p; break;
                case CallMotion.TailSweep: x -= 18 * p; tilt += 38 * p; sx -= .12f * p; break;
                case CallMotion.Rise: y += size.y * .09f * p; sx -= .08f * p; sy += .12f * p; break;
                case CallMotion.Dip: tilt -= 20 * p; sy -= .1f * p; break;
                case CallMotion.Sway: x += size.x * .018f * p; tilt -= 24 * p; break;
                case CallMotion.Flash: sx += .17f * p; sy += .17f * p; break;
            }
        }
        private static BattleEffectKind CallEffect(CallMotion motion)
        {
            switch (motion)
            {
                case CallMotion.Step: return BattleEffectKind.CallStep;
                case CallMotion.Stomp: return BattleEffectKind.CallStomp;
                case CallMotion.TailSweep: return BattleEffectKind.CallSweep;
                case CallMotion.Rise: return BattleEffectKind.CallRise;
                case CallMotion.Dip: return BattleEffectKind.CallDip;
                case CallMotion.Sway: return BattleEffectKind.CallSway;
                case CallMotion.Flash: return BattleEffectKind.CallFlash;
                default: return BattleEffectKind.Call;
            }
        }
        internal static Color CueColor(CallMotion motion)
        {
            switch (motion)
            {
                case CallMotion.Step: return RunUI.Hex("FFCE75");
                case CallMotion.Stomp: return RunUI.Hex("ECAA70");
                case CallMotion.TailSweep: return RunUI.Hex("7BDDE5");
                case CallMotion.Rise: return RunUI.Hex("9EDBFF");
                case CallMotion.Dip: return RunUI.Hex("A6ABFF");
                case CallMotion.Sway: return RunUI.Hex("E5A0ED");
                case CallMotion.Flash: return RunUI.Hex("FFF1B8");
                default: return RunUI.Gold;
            }
        }
        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size, Vector2 pivot)
        { rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = position; }
        private static Color MonsterColor(string id)
        {
            switch (id)
            {
                case "tap-slime": return RunUI.Hex("A3E676");
                case "march-slime": return RunUI.Hex("FFCE75");
                case "tresillo-bat": return RunUI.Hex("7BDDE5");
                case "offbeat-goblin": return RunUI.Hex("9CB4FF");
                case "drowsy-slime": return RunUI.Hex("C4A8EA");
                case "clock-spirit": return RunUI.Hex("EBCF88");
                case "seesaw-goblin": return RunUI.Hex("F1A0BA");
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
        private static string ActionLabel(GestureKind kind, int punch)
        {
            switch (kind)
            {
                case GestureKind.Hold: return "크로스가드";
                case GestureKind.Dive: return "덕킹";
                case GestureKind.Flick: return "점프";
                case GestureKind.Shake: return "밀쳐내기";
                default: return punch == 0 ? "왼손 펀치" : punch == 1 ? "오른손 펀치" : "어퍼";
            }
        }
    }
}
