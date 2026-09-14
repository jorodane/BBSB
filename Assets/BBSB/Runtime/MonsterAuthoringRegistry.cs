using System;
using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;

namespace BBSB.Runtime
{
    public static class MonsterAuthoringRegistry
    {
        private static readonly Dictionary<string, MonsterAuthoring> assets = new Dictionary<string, MonsterAuthoring>();
        private static bool loaded;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        { loaded = false; assets.Clear(); MonsterCatalog.SetCustom(Array.Empty<MonsterDefinition>()); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void EnsureLoaded()
        {
            if (loaded) return;
            Reload(Resources.LoadAll<MonsterAuthoring>(MonsterAuthoring.ResourceFolder));
        }
        public static void Reload(IEnumerable<MonsterAuthoring> source)
        {
            var entries = new List<MonsterAuthoring>(source);
            var counts = new Dictionary<string, int>();
            foreach (var asset in entries)
                if (asset != null && asset.includeInEncounters && !string.IsNullOrEmpty(asset.monsterId))
                    counts[asset.monsterId] = counts.TryGetValue(asset.monsterId, out var count) ? count + 1 : 1;
            var next = new Dictionary<string, MonsterAuthoring>(); var definitions = new List<MonsterDefinition>();
            foreach (var asset in entries)
            {
                if (asset == null || !asset.includeInEncounters) continue;
                try
                {
                    var definition = asset.BuildDefinition();
                    if (counts[definition.Id] != 1) throw new ArgumentException("중복된 몬스터 ID: " + definition.Id);
                    definitions.Add(definition); next.Add(definition.Id, asset);
                }
                catch (ArgumentException exception) { Debug.LogError(asset.name + ": " + exception.Message, asset); }
            }
            MonsterCatalog.SetRoster(definitions);
            assets.Clear(); foreach (var entry in next) assets.Add(entry.Key, entry.Value);
            loaded = true;
        }
        public static MonsterAuthoring Find(string id)
        { EnsureLoaded(); return assets.TryGetValue(id, out var asset) ? asset : null; }
    }
}
