using System;
using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;

namespace BBSB.Runtime
{
    public sealed class PlayerCharacter
    {
        public RunCharacterDefinition Definition { get; }
        public PlayerAuthoring Appearance { get; }
        public string Name => Appearance != null ? Appearance.displayName : "WEAPON MASTER";
        public string Description => Appearance != null ? Appearance.description : "단검과 히터실드로 박자를 만들어.";
        public PlayerCharacter(RunCharacterDefinition definition, PlayerAuthoring appearance)
        { Definition = definition; Appearance = appearance; }
    }

    public static class PlayerCharacterRegistry
    {
        public const string ResourceFolder = "BBSB/Characters";
        public static IReadOnlyList<PlayerCharacter> Load()
        {
            var result = new List<PlayerCharacter>();
            var primary = Resources.Load<PlayerAuthoring>(PlayerAuthoring.ResourcePath);
            var first = Build(primary);
            result.Add(first ?? new PlayerCharacter(RunCharacterDefinition.Default, null));
            var additions = new List<PlayerAuthoring>(Resources.LoadAll<PlayerAuthoring>(ResourceFolder));
            additions.Sort((a, b) => string.CompareOrdinal(a.characterId, b.characterId));
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var asset in additions)
                if (asset != null && !string.IsNullOrWhiteSpace(asset.characterId))
                    counts[asset.characterId] = counts.TryGetValue(asset.characterId, out int n) ? n + 1 : 1;
            foreach (var asset in additions)
            {
                if (asset == primary) continue;
                if (!string.IsNullOrWhiteSpace(asset.characterId) &&
                    (asset.characterId == result[0].Definition.Id || counts[asset.characterId] > 1))
                { Debug.LogWarning("Duplicate player character ID: " + asset.characterId + ". Give each character a unique ID.", asset); continue; }
                var character = Build(asset); if (character != null) result.Add(character);
            }
            return result.AsReadOnly();
        }
        private static PlayerCharacter Build(PlayerAuthoring asset)
        {
            if (asset == null) return null;
            try { return new PlayerCharacter(asset.BuildCharacter(), asset); }
            catch (ArgumentException error) { Debug.LogWarning("Invalid player character " + asset.name + ": " + error.Message, asset); return null; }
        }
        public static PlayerAuthoring Find(string characterId)
        {
            var characters = Load();
            foreach (var character in characters) if (character.Definition.Id == characterId) return character.Appearance;
            return characters[0].Appearance;
        }
    }
}
