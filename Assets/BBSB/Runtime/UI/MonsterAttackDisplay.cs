using System;
using BBSB.Core;
using UnityEngine;

namespace BBSB.Runtime.UI
{
    [CreateAssetMenu(menuName = "BBSB/Monster Attack Display")]
    public sealed class MonsterAttackDisplay : ScriptableObject
    {
        public const string ResourcePath = MonsterAttackDefinition.ResourceRoot + "Display";
        [Tooltip("Contact sockets measured in reference player heights from the player's ground point.")]
        public Vector2 punch = new Vector2(.24f, .46f);
        public Vector2 guard = new Vector2(.17f, .47f);
        public Vector2 head = new Vector2(.05f, .78f);
        public Vector2 feet = new Vector2(.03f, .08f);
        public Vector2 push = new Vector2(.24f, .47f);
        public Slot[] slots = Array.Empty<Slot>();

        [Serializable]
        public sealed class Slot
        {
            public string monsterId;
            public string patternId;
            public int stepIndex;
            [Min(.01f)] public float scale = 1;
            [Tooltip("Relative to monster height, from its default launch socket.")]
            public Vector2 sourceOffset;
            [Tooltip("Relative to player height, from the gesture contact socket.")]
            public Vector2 targetOffset;
            [Tooltip("Offsets the image without moving the contact socket; in player heights.")]
            public Vector2 imageOffset;
            [Min(.01f)] public float framesPerBeat = 2;
            public Pose[] poses = Array.Empty<Pose>();

            public Pose FindPose(MonsterAttackPhase phase)
            {
                if (poses != null) foreach (var pose in poses) if (pose != null && pose.phase == phase) return pose;
                return null;
            }
        }

        [Serializable]
        public sealed class Pose
        {
            public MonsterAttackPhase phase;
            [Min(.01f)] public float scale = 1;
            public Vector2 offset;
            public float rotation;
            public bool overridePivot;
            public Vector2 pivot = new Vector2(.5f, .5f);
        }

        public Slot Find(MonsterAttackDefinition definition)
        {
            if (slots != null) foreach (var slot in slots)
                if (slot != null && slot.monsterId == definition.MonsterId && slot.patternId == definition.PatternId && slot.stepIndex == definition.StepIndex)
                    return slot;
            return null;
        }

        public Vector2 Socket(GestureKind kind)
        {
            switch (kind)
            {
                case GestureKind.Hold: return guard;
                case GestureKind.Dive: return head;
                case GestureKind.Flick: return feet;
                case GestureKind.Shake: return push;
                default: return punch;
            }
        }

        public static Vector2 DefaultSocket(GestureKind kind)
        {
            switch (kind)
            {
                case GestureKind.Hold: return new Vector2(.17f, .47f);
                case GestureKind.Dive: return new Vector2(.05f, .78f);
                case GestureKind.Flick: return new Vector2(.03f, .08f);
                case GestureKind.Shake: return new Vector2(.24f, .47f);
                default: return new Vector2(.24f, .46f);
            }
        }

        internal static float Positive(float value, float fallback = 1) =>
            float.IsNaN(value) || float.IsInfinity(value) || value <= 0 ? fallback : value;
    }
}
