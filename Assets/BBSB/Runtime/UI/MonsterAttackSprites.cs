using System;
using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;

namespace BBSB.Runtime.UI
{
    /// <summary>Optional PNGs or named Sprite Editor frames. No generated backgrounds, runtime cutting or asset destruction.</summary>
    public sealed class MonsterAttackSprites
    {
        private readonly Func<string, Sprite[]> load;
        private readonly Dictionary<string, Sprite[]> folders = new Dictionary<string, Sprite[]>();
        private readonly Dictionary<string, Sprite[]> clips = new Dictionary<string, Sprite[]>();

        public MonsterAttackSprites(Func<string, Sprite[]> loader = null)
        { load = loader ?? (path => Resources.LoadAll<Sprite>(path)); }

        public Sprite Get(string folder, string name, double frame = 0)
        {
            string key = folder + "/" + name;
            if (!clips.TryGetValue(key, out var clip))
            {
                if (!folders.TryGetValue(folder, out var sprites)) folders.Add(folder, sprites = load(folder));
                var sequence = new List<(int Index, Sprite Sprite)>();
                Sprite single = null;
                foreach (var sprite in sprites)
                {
                    if (sprite == null) continue;
                    if (sprite.name == name) single = sprite;
                    else if (sprite.name.StartsWith(name + "-", StringComparison.Ordinal) &&
                        int.TryParse(sprite.name.Substring(name.Length + 1), out int index) && index >= 0)
                        sequence.Add((index, sprite));
                }
                sequence.Sort((a, b) => a.Index.CompareTo(b.Index));
                // A complete numbered sequence takes priority over the optional single-image fallback.
                var ordered = new List<Sprite>();
                foreach (var item in sequence)
                { if (item.Index != ordered.Count) break; ordered.Add(item.Sprite); }
                if (ordered.Count == 0 && single != null) ordered.Add(single);
                clip = ordered.ToArray(); clips.Add(key, clip);
            }
            return clip.Length == 0 ? null : clip[(int)(Math.Max(0, Math.Floor(frame)) % clip.Length)];
        }

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
