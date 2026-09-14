using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using BBSB.Core;
using BBSB.Runtime.UI;
using UnityEngine;

namespace BBSB.Runtime
{
    public enum MonsterPatternStrategy { Independent, BeatShift }

    [CreateAssetMenu(menuName = "BBSB/Monster", fileName = "NewMonster")]
    public sealed class MonsterAuthoring : ScriptableObject
    {
        public const string ResourceFolder = "BBSB/Monsters";
        public bool includeInEncounters = true;
        public string monsterId = "new-monster";
        public string displayName = "새 몬스터";
        [TextArea] public string description;
        public GestureKind mainGesture;
        public double encounterWeight = 1;
        [Min(0)] public int damagePerNote = 4;
        public Sprite portrait;
        public string artId;
        public MonsterPatternStrategy patternStrategy;
        [Range(1, 16)] public int steadyCallsPerPhase = 3;
        public bool usePatternProbabilities = true;
        public bool useLegacyBodyAnimation;
        public bool UsesLegacyBodyAnimation => useLegacyBodyAnimation && controller == null && visualPrefab == null;
        [Min(.1f)] public float displayScale = 1;
        public Vector2 displayOffset;
        public RuntimeAnimatorController controller;
        [Tooltip("Optional UI prefab. Root must be a RectTransform; its Animator drives child Images.")]
        public RectTransform visualPrefab;
        public Motion[] motions = DefaultMotions();
        public Pattern[] patterns = { new Pattern() };

        [Serializable]
        public sealed class Motion
        {
            public MonsterAnimationSituation situation;
            public string state;
            [Min(.25f)] public float durationBeats = 1;
            public bool loop;
        }
        [Serializable]
        public sealed class Call
        {
            [Min(0)] public int offsetTick;
            public string label = "통!";
            public CallSound sound = CallSound.Wood;
            public CallMotion motion = CallMotion.Hop;
            public string animatorState;
        }
        [Serializable]
        public sealed class Step
        {
            public GestureKind kind;
            [Min(0)] public int offsetTick;
            [Min(0)] public int durationTicks;
            public Attack attack = new Attack();
        }

        [Serializable]
        public sealed class Attack
        {
            public bool enabled;
            public MonsterAttackMotion motion;
            public MonsterAttackShape shape;
            public MonsterAttackReaction reaction;
            [Min(0)] public int callIndex;
            [Min(0)] public int spawnOffsetTicks;
            [Min(1)] public int rushTicks = 2;
            [Min(.01f)] public float height = .3f;
            public float arc = .18f;
            public bool stretch;
            public bool grounded;
            public bool landsAfterMiss;
            public string resourceFolder;
            public bool overrideDisplay;
            public MonsterAttackDisplay.Slot display = new MonsterAttackDisplay.Slot();
            public AttackFrames[] images = Array.Empty<AttackFrames>();

            public Sprite SpriteFor(MonsterAttackPhase phase, double index)
            {
                if (images != null) foreach (var image in images)
                {
                    if (image == null || image.phase != phase || image.frames == null || image.frames.Length == 0) continue;
                    int frame = image.loop ? (int)(Math.Max(0, Math.Floor(index)) % image.frames.Length) :
                        (int)Math.Min(image.frames.Length - 1, Math.Max(0, Math.Floor(index)));
                    return image.frames[frame];
                }
                return null;
            }
        }
        [Serializable]
        public sealed class AttackFrames
        {
            public MonsterAttackPhase phase = MonsterAttackPhase.Travel;
            public bool loop = true;
            public Sprite[] frames = Array.Empty<Sprite>();
        }
        private static readonly ConditionalWeakTable<MonsterAttackDefinition, Attack> attackArt =
            new ConditionalWeakTable<MonsterAttackDefinition, Attack>();
        public static Attack FindAttackArt(MonsterAttackDefinition definition) =>
            attackArt.TryGetValue(definition, out var value) ? value : null;

        [Serializable]
        public sealed class Pattern
        {
            public string id = "tap";
            public string displayName = "한 박 뒤 Tap";
            [TextArea] public string description;
            [Min(1)] public int cueLeadTicks = 4;
            [Min(1)] public int responseTicks = 4;
            [Min(0)] public int restTicks = 4;
            [Min(1)] public int cueAlignmentTicks = 1;
            [Min(0)] public int silentWaitTicks;
            public double participationChance = .25;
            public Call[] calls = { new Call() };
            public Step[] steps = { new Step() };
            public string attackState;
            public string recoverState;
        }
        public static Motion[] DefaultMotions()
        {
            var values = new List<Motion>();
            foreach (MonsterAnimationSituation situation in Enum.GetValues(typeof(MonsterAnimationSituation)))
                values.Add(new Motion { situation = situation, state = "Base Layer." + situation,
                    loop = situation == MonsterAnimationSituation.Idle, durationBeats = situation == MonsterAnimationSituation.Idle ? 2 : .5f });
            return values.ToArray();
        }
        public MonsterDefinition BuildDefinition()
        {
            ValidateId(monsterId);
            if (portrait == null) throw new ArgumentException("몬스터의 기본 Sprite를 등록해줘.");
            if (!Finite(displayScale) || displayScale <= 0 || !Finite(displayOffset.x) || !Finite(displayOffset.y))
                throw new ArgumentException("외형 크기와 위치가 올바르지 않아.");
            var definitions = new List<MonsterPatternDefinition>();
            if (patterns == null) throw new ArgumentException("패턴을 하나 이상 추가해줘.");
            foreach (var pattern in patterns)
            {
                if (pattern == null) throw new ArgumentException("빈 패턴이 있어.");
                ValidateId(pattern.id);
                var steps = new List<PatternStep>(); var calls = new List<CallSignal>();
                if (pattern.steps == null || pattern.calls == null) throw new ArgumentException("Call과 Response가 필요해.");
                foreach (var step in pattern.steps)
                {
                    if (step == null) throw new ArgumentException("빈 Response가 있어.");
                    steps.Add(new PatternStep(step.kind, step.offsetTick, step.durationTicks));
                }
                foreach (var call in pattern.calls)
                {
                    if (call == null) throw new ArgumentException("빈 Call이 있어.");
                    calls.Add(new CallSignal(call.offsetTick, call.label, call.sound, call.motion));
                }
                definitions.Add(MonsterPatternDefinition.FromPhrase(pattern.id, pattern.displayName, pattern.description,
                    pattern.cueLeadTicks, steps, calls, pattern.responseTicks,
                    pattern.restTicks, pattern.participationChance, pattern.cueAlignmentTicks, pattern.silentWaitTicks));
            }
            var situations = new HashSet<MonsterAnimationSituation>();
            if (motions != null) foreach (var motion in motions)
                if (motion == null || !Enum.IsDefined(typeof(MonsterAnimationSituation), motion.situation) ||
                    !situations.Add(motion.situation) || !Finite(motion.durationBeats) || motion.durationBeats <= 0)
                    throw new ArgumentException("상황별 모션은 중복 없이, 길이는 0보다 크게 지정해줘.");
            if (!Enum.IsDefined(typeof(MonsterPatternStrategy), patternStrategy))
                throw new ArgumentException("패턴 배치 방식을 확인해줘.");
            IMonsterPatternPlanner planner = patternStrategy == MonsterPatternStrategy.BeatShift
                ? (IMonsterPatternPlanner)new BeatShiftPlanner(steadyCallsPerPhase, usePatternProbabilities) : IndependentPatternPlanner.Instance;
            var monster = new MonsterDefinition(monsterId, displayName, description, mainGesture, definitions, encounterWeight, damagePerNote,
                string.IsNullOrWhiteSpace(artId) ? null : artId, planner, validateCallReadability: false);
            RegisterAttacks(monster);
            return monster;
        }

        private void RegisterAttacks(MonsterDefinition monster)
        {
            var visuals = new List<MonsterAttackDefinition>();
            foreach (var pattern in patterns)
            {
                var ordered = new List<Step>(pattern.steps);
                ordered.Sort((a, b) => a.offsetTick != b.offsetTick ? a.offsetTick.CompareTo(b.offsetTick) : a.kind.CompareTo(b.kind));
                var calls = new List<Call>(pattern.calls); calls.Sort((a, b) => a.offsetTick.CompareTo(b.offsetTick));
                for (int i = 0; i < ordered.Count; i++)
                {
                    var step = ordered[i]; var config = step.attack;
                    if (config == null || !config.enabled) continue;
                    string context = pattern.displayName + " / Response " + (i + 1) + ": ";
                    if (!Enum.IsDefined(typeof(MonsterAttackMotion), config.motion) ||
                        !Enum.IsDefined(typeof(MonsterAttackShape), config.shape) ||
                        !Enum.IsDefined(typeof(MonsterAttackReaction), config.reaction))
                        throw new ArgumentException(context + "공격 종류를 확인해줘.");
                    if (config.callIndex < 0 || config.callIndex >= pattern.calls.Length || config.spawnOffsetTicks < 0 ||
                        config.rushTicks <= 0 || !Finite(config.height) || config.height <= 0 || !Finite(config.arc))
                        throw new ArgumentException(context + "발사 Call·지연·이동 길이·크기를 확인해줘.");
                    int callTick = pattern.calls[config.callIndex].offsetTick;
                    long spawn = (long)callTick + config.spawnOffsetTicks;
                    long contact = (long)pattern.cueLeadTicks + step.offsetTick;
                    if (spawn >= contact)
                        throw new ArgumentException(context + "공격 생성은 입력 판정 시각보다 앞이어야 해.");
                    if (config.motion == MonsterAttackMotion.WaitRush && config.rushTicks > contact - spawn)
                        throw new ArgumentException(context + "돌진 길이가 생성부터 판정까지의 구간보다 길어.");
                    ValidateDisplay(config.display, context);
                    var phases = new HashSet<MonsterAttackPhase>();
                    if (config.images != null) foreach (var frames in config.images)
                    {
                        if (frames == null || frames.phase == MonsterAttackPhase.Hidden ||
                            !Enum.IsDefined(typeof(MonsterAttackPhase), frames.phase) || !phases.Add(frames.phase))
                            throw new ArgumentException(context + "이미지 단계는 중복 없이 지정해줘.");
                        if (frames.frames != null) foreach (var sprite in frames.frames)
                            if (sprite == null) throw new ArgumentException(context + "이미지 프레임에 빈 Sprite가 있어.");
                    }
                    // Copy mutable authoring data so a running round never changes under an inspector edit.
                    var snapshot = CopyAttack(config);
                    int callIndex = calls.FindIndex(x => x.offsetTick == callTick);
                    var definition = new MonsterAttackDefinition(monster.Id, pattern.id, i, config.motion, config.shape, config.reaction,
                        callIndex, config.spawnOffsetTicks, config.height, config.arc, config.rushTicks, config.stretch, config.grounded,
                        config.landsAfterMiss, config.resourceFolder);
                    attackArt.Add(definition, snapshot); visuals.Add(definition);
                }
            }
            MonsterAttackCatalog.Register(monster, visuals);
        }

        private static Attack CopyAttack(Attack source)
        {
            var display = source.display;
            var poses = new List<MonsterAttackDisplay.Pose>();
            if (display.poses != null) foreach (var pose in display.poses)
                poses.Add(new MonsterAttackDisplay.Pose { phase = pose.phase, scale = pose.scale, offset = pose.offset,
                    rotation = pose.rotation, overridePivot = pose.overridePivot, pivot = pose.pivot });
            var images = new List<AttackFrames>();
            if (source.images != null) foreach (var frames in source.images)
                images.Add(new AttackFrames { phase = frames.phase, loop = frames.loop,
                    frames = frames.frames == null ? Array.Empty<Sprite>() : (Sprite[])frames.frames.Clone() });
            return new Attack
            {
                enabled = source.enabled, motion = source.motion, shape = source.shape, reaction = source.reaction,
                callIndex = source.callIndex, spawnOffsetTicks = source.spawnOffsetTicks, rushTicks = source.rushTicks,
                height = source.height, arc = source.arc, stretch = source.stretch, grounded = source.grounded,
                landsAfterMiss = source.landsAfterMiss, resourceFolder = source.resourceFolder, overrideDisplay = source.overrideDisplay,
                display = new MonsterAttackDisplay.Slot { scale = display.scale, sourceOffset = display.sourceOffset,
                    targetOffset = display.targetOffset, imageOffset = display.imageOffset, framesPerBeat = display.framesPerBeat,
                    poses = poses.ToArray() }, images = images.ToArray()
            };
        }

        private static void ValidateDisplay(MonsterAttackDisplay.Slot display, string context)
        {
            if (display == null) throw new ArgumentException(context + "표시 설정이 필요해.");
            bool Vector(Vector2 value) => Finite(value.x) && Finite(value.y);
            if (!Finite(display.scale) || display.scale <= 0 || !Finite(display.framesPerBeat) || display.framesPerBeat <= 0 ||
                !Vector(display.sourceOffset) || !Vector(display.targetOffset) || !Vector(display.imageOffset))
                throw new ArgumentException(context + "표시 크기·위치·프레임 속도를 확인해줘.");
            var phases = new HashSet<MonsterAttackPhase>();
            if (display.poses != null) foreach (var pose in display.poses)
                if (pose == null || !Enum.IsDefined(typeof(MonsterAttackPhase), pose.phase) || !phases.Add(pose.phase) ||
                    !Finite(pose.scale) || pose.scale <= 0 || !Finite(pose.rotation) || !Vector(pose.offset) || !Vector(pose.pivot))
                    throw new ArgumentException(context + "단계별 표시 보정이 올바르지 않아.");
        }
        public static IReadOnlyList<string> Warnings(MonsterDefinition monster)
        {
            var warnings = new List<string>();
            for (int i = 0; i < monster.Patterns.Count; i++)
                for (int j = i + 1; j < monster.Patterns.Count; j++)
                    if (CallReadability.Warning(monster.Patterns[i], monster.Patterns[j]) != null)
                        warnings.Add(monster.Patterns[i].Name + " / " + monster.Patterns[j].Name +
                            ": 첫 입력 전에 소리나 몸짓만으로 구분하기 어려울 수 있어. 테스트 장면에서 확인해줘.");
            return warnings;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void ValidateId(string value)
        {
            if (string.IsNullOrEmpty(value) || !Regex.IsMatch(value, "^[a-z0-9][a-z0-9-]*$"))
                throw new ArgumentException("ID는 영문 소문자·숫자·하이픈만 사용할 수 있어.");
        }
        public Motion FindMotion(MonsterAnimationSituation situation)
        {
            if (motions != null) foreach (var motion in motions) if (motion != null && motion.situation == situation) return motion;
            return null;
        }
        public string StateFor(MonsterAnimationFrame frame)
        {
            if (patterns != null && frame.Attack != null) foreach (var pattern in patterns)
            {
                if (pattern == null || pattern.id != frame.Attack.Pattern.Id) continue;
                if (frame.Situation == MonsterAnimationSituation.Attack && !string.IsNullOrWhiteSpace(pattern.attackState)) return pattern.attackState;
                if (frame.Situation == MonsterAnimationSituation.Recover && !string.IsNullOrWhiteSpace(pattern.recoverState)) return pattern.recoverState;
                if (frame.Situation == MonsterAnimationSituation.Call && pattern.calls != null)
                    foreach (var call in pattern.calls)
                        if (call != null && call.offsetTick == frame.CallOffsetTick && !string.IsNullOrWhiteSpace(call.animatorState)) return call.animatorState;
            }
            return FindMotion(frame.Situation)?.state;
        }
    }
}
