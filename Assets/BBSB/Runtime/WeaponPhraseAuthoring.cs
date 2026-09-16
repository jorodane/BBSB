using System;
using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;

namespace BBSB.Runtime
{
    [CreateAssetMenu(menuName = "BBSB/Weapon Phrase", fileName = "WeaponPhrase")]
    public sealed class WeaponPhraseAuthoring : ScriptableObject
    {
        public const string ResourceFolder = "BBSB/WeaponPhrases";
        public string weaponId = "sword";
        public string displayName = "검";
        [TextArea] public string hint = "Tap 0 / 1 / 2";
        [Min(.5f)] public float lengthBeats = 4;
        [Min(.25f)] public float missCooldownBeats = 2;
        public bool repeat = true;
        [Tooltip("Timing of notes marked Parry: keydown, or release at that Hold's end. Any notes can be parries, including repeats.")]
        public ParryInputEdge parryInput;
        [Min(0)] public int finisherEvery;
        [Min(0)] public float finisherDamage, groggyBeats;
        public Note[] notes = { new Note(), new Note { beat = 1 }, new Note { beat = 2, damage = 12 } };
        [Serializable] public sealed class Note
        {
            [Min(0)] public float beat, holdBeats;
            [Min(0)] public float damage = 8;
            [Tooltip("Mark any desired notes Parry; their positions and count are not restricted.")]
            public PhraseEffect effect;
        }
        public WeaponPhrase Build()
        {
            var result = new List<WeaponPhraseNote>();
            foreach (var note in notes ?? Array.Empty<Note>())
            {
                if (note == null) throw new ArgumentException("Null phrase note.");
                result.Add(new WeaponPhraseNote(note.beat, (decimal)note.damage, note.holdBeats, note.effect));
            }
            return new WeaponPhrase(weaponId, displayName, hint, lengthBeats, result, missCooldownBeats,
                repeat, finisherEvery, (decimal)finisherDamage, groggyBeats, parryInput);
        }
        public static IReadOnlyList<WeaponPhrase> LoadFor(IReadOnlyList<WeaponState> weapons)
        {
            var overrides = new Dictionary<string, WeaponPhrase>();
            var duplicateIds = new HashSet<string>();
            foreach (var asset in Resources.LoadAll<WeaponPhraseAuthoring>(ResourceFolder))
            {
                try
                {
                    var phrase = asset.Build();
                    if (overrides.ContainsKey(phrase.WeaponId)) duplicateIds.Add(phrase.WeaponId);
                    else overrides.Add(phrase.WeaponId, phrase);
                }
                catch (Exception error) { Debug.LogError(asset.name + ": " + error.Message, asset); }
            }
            foreach (var id in duplicateIds)
            { overrides.Remove(id); Debug.LogError("Duplicate Weapon Phrase: " + id + ". Using the built-in phrase."); }
            var result = new List<WeaponPhrase>();
            foreach (var weapon in weapons)
                result.Add(overrides.TryGetValue(weapon.DefinitionId, out var phrase) ? phrase : WeaponPhraseCatalog.Find(weapon.DefinitionId));
            return result.AsReadOnly();
        }
    }
}
