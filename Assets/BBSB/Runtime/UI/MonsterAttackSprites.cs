using System;
using BBSB.Core;
using UnityEngine;

namespace BBSB.Runtime.UI
{
    /// <summary>Optional PNGs or named Sprite Editor frames. No generated backgrounds, runtime cutting or asset destruction.</summary>
    public sealed class MonsterAttackSprites
    {
        private readonly NamedResourceClips<Sprite> clips;

        public MonsterAttackSprites(Func<string, Sprite[]> loader = null)
        { clips = new NamedResourceClips<Sprite>(loader ?? (path => Resources.LoadAll<Sprite>(path)), sprite => sprite.name); }

        public Sprite Get(string folder, string name, double frame = 0)
            => clips.Get(folder, name, frame);

        internal Sprite Body(MonsterPlan monster, double seconds, double beatSeconds, Sprite fallback,
            bool victory, out bool authoredPose)
        {
            var first = BodyBlend(monster, seconds, beatSeconds, fallback, victory, out var next, out float blend, out authoredPose);
            return blend < .5f ? first : next;
        }

        internal Sprite BodyBlend(MonsterPlan monster, double seconds, double beatSeconds, Sprite fallback,
            bool victory, out Sprite next, out float blend, out bool authoredPose)
        {
            var idle = Get(MonsterAttackDefinition.ResourceRoot + monster.Monster.Id, "idle", seconds / beatSeconds * 2) ?? fallback;
            var frame = victory ? default : MonsterBodyTimeline.Evaluate(monster, seconds, beatSeconds);
            next = idle; blend = 0; authoredPose = false;
            if (!frame.Active) return idle;
            string folder = MonsterAttackDefinition.ResourceRoot + monster.Monster.Id + "/" + frame.Attack.Pattern.Id + "/body";
            double animation = (seconds - frame.Attack.Call[frame.CallIndex].Tick * beatSeconds / RhythmTime.TicksPerBeat) / beatSeconds * 2;
            // Existing single-Call art can demonstrate newly authored multi-Call rhythms too.
            var call = Get(folder, "call-" + frame.CallIndex, animation) ?? Get(folder, "call-0", animation);
            var attack = Get(folder, "attack", animation); var recover = Get(folder, "recover", animation);
            authoredPose = call != null || attack != null || recover != null;
            Sprite Pose(MonsterBodyPose pose) => pose == MonsterBodyPose.Call ? call ?? idle :
                pose == MonsterBodyPose.Attack ? attack ?? call ?? idle : pose == MonsterBodyPose.Recover ? recover ?? idle : idle;
            var first = Pose(frame.From); next = Pose(frame.To);
            blend = first == next ? 0 : (float)frame.Blend;
            return first;
        }

        public Sprite Attack(MonsterAttackDefinition definition, MonsterAttackFrame frame, float framesPerBeat, double beatSeconds, out bool exactPhase)
        {
            double index = frame.IsReaction ? frame.PhaseAge * 12 : frame.PhaseAge / beatSeconds * framesPerBeat;
            var sprite = Get(definition.ResourceFolder, frame.ImageName, index);
            exactPhase = sprite != null;
            return sprite != null ? sprite : Get(definition.ResourceFolder, "travel", index) ?? Get(definition.ResourceFolder, "spawn") ??
                Get(definition.ResourceFolder, "contact");
        }
    }
}
