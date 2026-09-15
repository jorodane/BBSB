using System;
using BBSB.Core;
using BBSB.Runtime.UI;
using UnityEngine;

namespace BBSB.Runtime
{
    [CreateAssetMenu(menuName = "BBSB/Player", fileName = "Player")]
    public sealed class PlayerAuthoring : ScriptableObject
    {
        public const string ResourcePath = "BBSB/Player";
        public string displayName = "WEAPON MASTER";
        public Sprite portrait;
        public GameObject visualPrefab;
        public RuntimeAnimatorController controller;
        [Min(.01f)] public float spriteReferenceHeight = 4;
        [Min(.01f)] public float displayScale = 1;
        public Vector2 displayOffset;
        public bool useLegacyFrames = true;
        public PlayerMotionDisplay layout;
        public Motion[] motions = DefaultMotions();
        [Serializable] public sealed class Motion
        {
            public string name, state;
            public PlayerMotionPhase phase;
            public bool anyGesture = true;
            public GestureKind gesture;
            public bool anyGrade = true;
            public RhythmGrade grade;
            [Range(-1, 2)] public int punch = -1;
            [Min(.01f)] public float durationBeats = 1;
            public bool loop;
            public Sprite[] frames = Array.Empty<Sprite>();
        }
        public Motion Find(PlayerMotionFrame frame)
        {
            Motion found = null; int best = -1;
            foreach (var motion in motions ?? Array.Empty<Motion>())
            {
                if (motion == null || motion.phase != frame.Phase || (!motion.anyGesture && frame.Kind != motion.gesture) ||
                    (!motion.anyGrade && frame.Grade != motion.grade) || (motion.punch >= 0 && frame.Punch != motion.punch)) continue;
                int score = (motion.anyGesture ? 0 : 1) + (motion.anyGrade ? 0 : 2) + (motion.punch < 0 ? 0 : 4);
                if (score > best) { best = score; found = motion; }
            }
            return found;
        }
        public static Motion[] DefaultMotions()
        {
            var result = new System.Collections.Generic.List<Motion>();
            result.Add(new Motion { name = "Idle", state = "Base Layer.Idle", phase = PlayerMotionPhase.Idle, loop = true });
            foreach (GestureKind gesture in Enum.GetValues(typeof(GestureKind)))
            foreach (var phase in new[] { PlayerMotionPhase.Prepare, PlayerMotionPhase.Sustain, PlayerMotionPhase.Impact, PlayerMotionPhase.Recover })
                result.Add(new Motion { name = gesture + " " + phase, state = "Base Layer." + gesture + phase,
                    phase = phase, anyGesture = false, gesture = gesture, loop = phase == PlayerMotionPhase.Sustain });
            return result.ToArray();
        }
    }
}
