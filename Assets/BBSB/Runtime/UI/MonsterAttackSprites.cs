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
            authoredPose = false;
            var idle = Get(MonsterAttackDefinition.ResourceRoot + monster.Monster.Id, "idle", seconds / beatSeconds * 2);
            if (victory) return idle != null ? idle : fallback;
            Sprite selected = null; double latest = double.NegativeInfinity;
            foreach (var attack in monster.Attacks)
            {
                string folder = MonsterAttackDefinition.ResourceRoot + monster.Monster.Id + "/" + attack.Pattern.Id + "/body";
                for (int i = 0; i < attack.Call.Count; i++)
                {
                    double time = attack.Call[i].Tick * beatSeconds / RhythmTime.TicksPerBeat;
                    if (seconds >= time && seconds < time + beatSeconds * .65 && time >= latest)
                    {
                        var sprite = Get(folder, "call-" + i, (seconds - time) / beatSeconds * 2);
                        if (sprite != null) { selected = sprite; latest = time; }
                    }
                }
                foreach (var step in attack.Pattern.Pattern.Steps)
                {
                    double start = (attack.ResponseStartTick + step.OffsetTick) * beatSeconds / RhythmTime.TicksPerBeat;
                    double end = start + Math.Max(step.DurationTicks / 4.0, .3) * beatSeconds;
                    bool contact = seconds >= start && seconds < end;
                    double time = contact ? start : end;
                    if (seconds < start || seconds >= end + beatSeconds * .35 || time < latest) continue;
                    var sprite = Get(folder, contact ? "attack" : "recover", (seconds - time) / beatSeconds * 2);
                    if (sprite != null) { selected = sprite; latest = time; }
                }
            }
            authoredPose = selected != null;
            return selected != null ? selected : idle != null ? idle : fallback;
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
