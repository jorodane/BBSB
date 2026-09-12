using System;
using System.Collections.Generic;

namespace BBSB.Runtime.UI
{
    /// <summary>Loads individual image paths first; folder scans only support legacy named atlas frames.</summary>
    public sealed class NamedResourceClips<T> where T : class
    {
        private readonly Func<string, T[]> load;
        private readonly Func<T, string> name;
        private readonly Dictionary<string, T[]> resources = new Dictionary<string, T[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, T[]> clips = new Dictionary<string, T[]>(StringComparer.Ordinal);

        public NamedResourceClips(Func<string, T[]> load, Func<T, string> name)
        { this.load = load; this.name = name; }

        public T Get(string folder, string clipName, double frame = 0)
        {
            string key = folder + "/" + clipName;
            if (!clips.TryGetValue(key, out var clip))
            {
                // Separate numbered PNGs take priority over their optional static fallback.
                var numbered = new List<T>();
                for (int i = 0; ; i++)
                {
                    var file = Read(key + "-" + i);
                    if (file.Length != 1 || file[0] == null) break;
                    numbered.Add(file[0]);
                }
                if (numbered.Count > 0) clip = numbered.ToArray();
                else
                {
                    var file = Read(key);
                    // A Single sprite is identified by its asset path, not its Sprite Editor name.
                    clip = file.Length == 1 && file[0] != null ? file : Named(file, clipName);
                    if (clip.Length == 0) clip = Named(Read(folder), clipName);
                }
                clips.Add(key, clip);
            }
            if (clip.Length == 0) return null;
            if (double.IsNaN(frame) || double.IsInfinity(frame)) frame = 0;
            return clip[(int)(Math.Max(0, Math.Floor(frame)) % clip.Length)];
        }

        private T[] Read(string path)
        {
            if (!resources.TryGetValue(path, out var values)) resources.Add(path, values = load(path) ?? Array.Empty<T>());
            return values;
        }

        private T[] Named(T[] values, string clipName)
        {
            T single = null;
            var sequence = new SortedDictionary<int, T>();
            foreach (var value in values)
            {
                if (value == null) continue;
                string valueName = name(value);
                if (valueName == clipName) single = value;
                else if (valueName != null && valueName.StartsWith(clipName + "-", StringComparison.Ordinal) &&
                    int.TryParse(valueName.Substring(clipName.Length + 1), out int index) && index >= 0)
                    sequence[index] = value;
            }
            var ordered = new List<T>();
            for (int i = 0; sequence.TryGetValue(i, out var value); i++) ordered.Add(value);
            if (ordered.Count == 0 && single != null) ordered.Add(single);
            return ordered.ToArray();
        }
    }
}
