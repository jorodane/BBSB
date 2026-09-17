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
        [Tooltip("Common fallback for all attributes. Disable to author this exact attribute variant.")]
        public bool anyAttribute = true;
        public WeaponAttribute attribute;
        [Tooltip("Use this phrase on both beat sides. Disable to give Dual/Chaos distinct Light and Dark effects.")]
        public bool bothSides = true;
        public WeaponBeatSide beatSide;
        [Tooltip("Chaos-only bridge: repeating, contains both beat sides, and its length ends in .5 beats.")]
        public bool chaosTransition;
        [Min(6), Tooltip("Rules come from the selected Light/Offset 0 base asset, or the first matching override if absent.")] public float chaosMinimumBeats = 6;
        [Range(0, 1)] public float chaosTransitionChance = .25f;
        [Min(1.01f)] public float chaosEffectMultiplier = 1.5f;
        [Range(0, 6), Tooltip("Starting Offset within this weapon's occupied lines, ordered left to right. Each Offset may define a different pattern and effects.")]
        public int startLaneOffset;
        public string displayName = "검";
        [TextArea] public string hint = "Tap 0 / 1 / 2";
        [Min(.5f)] public float lengthBeats = 4;
        [Min(.25f)] public float missCooldownBeats = 2;
        public bool repeat;
        [Tooltip("Timing of notes marked Parry: keydown, or release at that Hold's end. Any notes can be parries, including repeats.")]
        public ParryInputEdge parryInput;
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
            [Min(0), Tooltip("Effect amount: damage for Strike/Parry, health restored for Heal.")] public float damage = 8;
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
                repeat, finisherEvery, (decimal)finisherDamage, groggyBeats, parryInput,
                (decimal)holdDamageReduction, releaseEndsPhrase, parryRequired, completionCooldownBeats);
        }
        private ChaosRules BuildChaosRules() => new ChaosRules(chaosMinimumBeats, (decimal)chaosTransitionChance, (decimal)chaosEffectMultiplier);
        public static IReadOnlyList<WeaponPhrase> LoadFor(IReadOnlyList<WeaponState> weapons)
        {
            var result = new List<WeaponPhrase>();
            var sets = LoadSetsFor(weapons);
            for (int i = 0; i < sets.Count; i++) result.Add(sets[i].For(0,
                weapons[i].Attribute == WeaponAttribute.Dark ? WeaponBeatSide.Dark : WeaponBeatSide.Light));
            return result.AsReadOnly();
        }
        public static IReadOnlyList<WeaponPhraseSet> LoadSetsFor(IReadOnlyList<WeaponState> weapons)
        {
            return BuildSetsFor(weapons, Resources.LoadAll<WeaponPhraseAuthoring>(ResourceFolder));
        }
        public static IReadOnlyList<WeaponPhraseSet> BuildSetsFor(IReadOnlyList<WeaponState> weapons, IEnumerable<WeaponPhraseAuthoring> assets)
        {
            var overrides = new Dictionary<string, WeaponPhraseAuthoring>();
            var phrases = new Dictionary<string, WeaponPhrase>();
            var duplicates = new HashSet<string>();
            foreach (var asset in assets)
            {
                try
                {
                    var phrase = asset.Build(); WeaponAttributes.Validate(asset.attribute);
                    asset.BuildChaosRules();
                    if (asset.startLaneOffset < 0 || asset.startLaneOffset >= BattleInputLayout.LaneCount ||
                        !Enum.IsDefined(typeof(WeaponBeatSide), asset.beatSide)) throw new ArgumentException("Invalid phrase selector.");
                    string key = Key(asset.weaponId, asset.anyAttribute ? "*" : asset.attribute.ToString(), asset.startLaneOffset,
                        asset.bothSides ? "*" : asset.beatSide.ToString(), asset.chaosTransition);
                    if (overrides.ContainsKey(key)) duplicates.Add(key);
                    else { overrides.Add(key, asset); phrases.Add(key, phrase); }
                }
                catch (Exception error) { Debug.LogError(asset.name + ": " + error.Message, asset); }
            }
            foreach (var key in duplicates)
            { overrides.Remove(key); phrases.Remove(key); Debug.LogError("Duplicate Weapon Phrase: " + key + ". Using fallback."); }
            var result = new List<WeaponPhraseSet>();
            foreach (var weapon in weapons)
            {
                var light = new WeaponPhrase[weapon.RequiredLanes]; var dark = new WeaponPhrase[light.Length];
                var lightBridge = new WeaponPhrase[light.Length]; var darkBridge = new WeaponPhrase[light.Length];
                ChaosRules rules = null;
                for (int offset = 0; offset < light.Length; offset++)
                {
                    light[offset] = Resolve(weapon, offset, WeaponBeatSide.Light, false);
                    dark[offset] = Resolve(weapon, offset, WeaponBeatSide.Dark, false);
                    if (weapon.Attribute != WeaponAttribute.Chaos) continue;
                    lightBridge[offset] = Resolve(weapon, offset, WeaponBeatSide.Light, true);
                    darkBridge[offset] = Resolve(weapon, offset, WeaponBeatSide.Dark, true);
                }
                try
                {
                    result.Add(new WeaponPhraseSet(weapon, light, dark,
                        lightBridge, darkBridge, rules));
                }
                catch (ArgumentException error)
                {
                    Debug.LogError(weapon.DisplayName + ": " + error.Message + " Using built-in patterns.");
                    result.Add(WeaponPhraseSet.Uniform(weapon));
                }

                WeaponPhrase Resolve(WeaponState item, int offset, WeaponBeatSide side, bool transition)
                {
                    foreach (string attributeKey in new[] { item.Attribute.ToString(), "*" })
                    foreach (string sideKey in new[] { side.ToString(), "*" })
                    {
                        string key = Key(item.DefinitionId, attributeKey, offset, sideKey, transition);
                        if (!phrases.TryGetValue(key, out var phrase)) continue;
                        bool valid = true;
                        foreach (var note in phrase.Notes) valid &= note.LaneOffset < item.RequiredLanes;
                        if (!valid) { Debug.LogError("Weapon Phrase " + key + " exceeds its footprint."); continue; }
                        if (item.Attribute == WeaponAttribute.Chaos && rules == null)
                        {
                            var asset = overrides[key];
                            rules = asset.BuildChaosRules();
                        }
                        return phrase;
                    }
                    return transition ? null : WeaponPhraseCatalog.Find(item.DefinitionId);
                }
            }
            return result.AsReadOnly();
        }
        private static string Key(string id, string attributeKey, int offset, string sideKey, bool transition) =>
            id + "/" + attributeKey + "/" + offset + "/" + sideKey + "/" + transition;
    }
}
