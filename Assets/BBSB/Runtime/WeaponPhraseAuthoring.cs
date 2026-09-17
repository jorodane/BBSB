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
        [Range(0, 6), Tooltip("Starting Offset within this weapon's occupied lines, ordered left to right. Each Offset may define a different pattern and effects.")]
        public int startLaneOffset;
        public string displayName = "검";
        [TextArea] public string hint = "Tap 0 / 1 / 2";
        [Min(.5f)] public float lengthBeats = 4;
        [Min(.25f)] public float missCooldownBeats = 2;
        public bool repeat;
        [Tooltip("Timing of notes marked Parry: keydown, or release at that Hold's end. Any notes can be parries, including repeats.")]
        public ParryInputEdge parryInput;
        [Min(0), Tooltip("The opening input always succeeds. This grid sets the following rhythm: 0.5 chooses the nearest beat/offbeat; zero keeps the exact input time.")]
        public float startGridBeats = .5f;
        [Range(0, 1)] public float holdDamageReduction;
        public bool releaseEndsPhrase;
        [Tooltip("Later parry notes require a timed block. The opening press is always accepted, but counters still require an actual parry.")]
        public bool parryRequired = true;
        [Min(0)] public float completionCooldownBeats;
        [Min(0)] public int finisherEvery;
        [Min(0)] public float finisherDamage, groggyBeats;
        public Note[] notes = { new Note(), new Note { beat = 1 }, new Note { beat = 2, damage = 12 } };
        [Serializable] public sealed class Note
        {
            [Range(0, 6), Tooltip("Line offset relative to the input that started the pattern; wraps within this weapon's selected lines. The first note must use zero.")]
            public int laneOffset;
            [Min(0)] public float beat, holdBeats;
            [Min(0)] public float damage = 8;
            [Tooltip("Mark any desired notes Parry; their positions and count are not restricted.")]
            public PhraseEffect effect;
            public PhraseNoteCondition condition;
            [Tooltip("Earlier note index. Used only for a conditional note.")]
            public int prerequisite = -1;
        }
        public WeaponPhrase Build()
        {
            var result = new List<WeaponPhraseNote>();
            foreach (var note in notes ?? Array.Empty<Note>())
            {
                if (note == null) throw new ArgumentException("Null phrase note.");
                result.Add(new WeaponPhraseNote(note.beat, (decimal)note.damage, note.holdBeats, note.effect,
                    note.condition == PhraseNoteCondition.Always ? -1 : note.prerequisite, note.condition, note.laneOffset));
            }
            return new WeaponPhrase(weaponId, displayName, hint, lengthBeats, result, missCooldownBeats,
                repeat, finisherEvery, (decimal)finisherDamage, groggyBeats, parryInput, startGridBeats,
                (decimal)holdDamageReduction, releaseEndsPhrase, parryRequired, completionCooldownBeats);
        }
        public static IReadOnlyList<WeaponPhrase> LoadFor(IReadOnlyList<WeaponState> weapons)
        {
            var result = new List<WeaponPhrase>();
            foreach (var set in LoadSetsFor(weapons)) result.Add(set.Starts[0]);
            return result.AsReadOnly();
        }
        public static IReadOnlyList<WeaponPhraseSet> LoadSetsFor(IReadOnlyList<WeaponState> weapons)
        {
            var overrides = new Dictionary<string, WeaponPhrase>();
            var duplicateIds = new HashSet<string>();
            foreach (var asset in Resources.LoadAll<WeaponPhraseAuthoring>(ResourceFolder))
            {
                try
                {
                    var phrase = asset.Build();
                    if (asset.startLaneOffset < 0 || asset.startLaneOffset >= BattleInputLayout.LaneCount)
                        throw new ArgumentException("Starting lane offset must be between zero and six.");
                    string key = phrase.WeaponId + "/" + asset.startLaneOffset;
                    if (overrides.ContainsKey(key)) duplicateIds.Add(key);
                    else overrides.Add(key, phrase);
                }
                catch (Exception error) { Debug.LogError(asset.name + ": " + error.Message, asset); }
            }
            foreach (var id in duplicateIds)
            { overrides.Remove(id); Debug.LogError("Duplicate Weapon Phrase: " + id + ". Using the built-in phrase."); }
            var result = new List<WeaponPhraseSet>();
            foreach (var weapon in weapons)
            {
                var starts = new WeaponPhrase[weapon.RequiredLanes];
                for (int offset = 0; offset < starts.Length; offset++)
                {
                    string key = weapon.DefinitionId + "/" + offset;
                    starts[offset] = overrides.TryGetValue(key, out var phrase) ? phrase : WeaponPhraseCatalog.Find(weapon.DefinitionId);
                    foreach (var note in starts[offset].Notes)
                        if (note.LaneOffset >= weapon.RequiredLanes)
                        {
                            Debug.LogError("Weapon Phrase " + key + " uses a line outside its weapon's footprint. Using the built-in phrase.");
                            starts[offset] = WeaponPhraseCatalog.Find(weapon.DefinitionId); break;
                        }
                }
                result.Add(new WeaponPhraseSet(weapon, starts));
            }
            return result.AsReadOnly();
        }
    }
}
