using System;
using System.Collections.Generic;
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
