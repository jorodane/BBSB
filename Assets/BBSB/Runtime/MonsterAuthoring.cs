using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BBSB.Core;
using BBSB.Runtime.UI;
using UnityEngine;

namespace BBSB.Runtime
{
    [CreateAssetMenu(menuName = "BBSB/Monster", fileName = "NewMonster")]
    public sealed class MonsterAuthoring : ScriptableObject
    {
        public const string ResourceFolder = "BBSB/Monsters";
        public bool includeInEncounters = true;
        public string monsterId = "new-monster";
        public string displayName = "새 몬스터";
        [TextArea] public string description;
        public GestureKind mainGesture;
        [Min(.01f)] public float encounterWeight = 1;
        [Min(0)] public int damagePerNote = 4;
        public Sprite portrait;
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
        }
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
            [Range(.01f, 1)] public float participationChance = .25f;
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
                definitions.Add(new MonsterPatternDefinition(pattern.displayName, pattern.description,
                    new RhythmPattern(pattern.id, pattern.cueLeadTicks, steps), calls, pattern.responseTicks,
                    pattern.restTicks, pattern.participationChance, pattern.cueAlignmentTicks, pattern.silentWaitTicks));
            }
            var situations = new HashSet<MonsterAnimationSituation>();
            if (motions != null) foreach (var motion in motions)
                if (motion == null || !Enum.IsDefined(typeof(MonsterAnimationSituation), motion.situation) ||
                    !situations.Add(motion.situation) || !Finite(motion.durationBeats) || motion.durationBeats <= 0)
                    throw new ArgumentException("상황별 모션은 중복 없이, 길이는 0보다 크게 지정해줘.");
            return new MonsterDefinition(monsterId, displayName, description, mainGesture, definitions, encounterWeight, damagePerNote);
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
