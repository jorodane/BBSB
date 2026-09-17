using System;
using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;

namespace BBSB.Runtime
{
    // Only the character choice and starter bindings cross run boundaries.
    // JsonUtility fields contain no owned items, upgrade levels, rewards or run progress.
    [Serializable] public sealed class RunStartPreferences
    {
        public const string StorageKey = "BBSB.RunStart.v1";
        public int version = 1;
        public string selectedCharacterId = RunCharacterDefinition.DefaultId;
        public List<StartingLoadoutPreset> loadouts = new List<StartingLoadoutPreset>();

        public StartingLoadoutPreset Find(string characterId)
        {
            if (loadouts != null) foreach (var preset in loadouts)
                if (preset != null && preset.characterId == characterId) return preset;
            return null;
        }
        public void Remember(RunCharacterDefinition character, StartingLoadoutPreset preset)
        {
            var draft = new StartingLoadout(character, preset);
            if (!draft.CanStart) throw new ArgumentException("Equip at least one starting weapon before saving.");
            if (loadouts == null) loadouts = new List<StartingLoadoutPreset>();
            loadouts.RemoveAll(value => value == null || value.characterId == character.Id);
            loadouts.Add(draft.Capture()); selectedCharacterId = character.Id;
        }
        public static RunStartPreferences Load()
        {
            string json = PlayerPrefs.GetString(StorageKey, "");
            if (string.IsNullOrEmpty(json)) return new RunStartPreferences();
            try
            {
                var result = JsonUtility.FromJson<RunStartPreferences>(json);
                return result != null && result.version == 1 ? result : new RunStartPreferences();
            }
            catch (ArgumentException) { return new RunStartPreferences(); }
        }
        public void Save()
        {
            PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }
    }
}
