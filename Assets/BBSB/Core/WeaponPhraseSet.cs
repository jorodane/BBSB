using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    // One entry per possible starting Offset. Variants can change the whole phrase:
    // rhythm, damage, parry edge, guard, finisher and cooldown, not just its first note.
    public sealed class WeaponPhraseSet
    {
        public IReadOnlyList<WeaponPhrase> Starts { get; }
        public WeaponPhraseSet(WeaponState weapon, IReadOnlyList<WeaponPhrase> starts)
        {
            if (weapon == null || starts == null || starts.Count != weapon.RequiredLanes)
                throw new ArgumentException("Each occupied line needs a starting pattern.", nameof(starts));
            var copy = new WeaponPhrase[starts.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                var phrase = starts[i];
                if (phrase == null || phrase.WeaponId != weapon.DefinitionId) throw new ArgumentException("Wrong weapon pattern.");
                foreach (var note in phrase.Notes)
                    if (note.LaneOffset >= weapon.RequiredLanes) throw new ArgumentException("Note offset lies outside this weapon's footprint.");
                copy[i] = phrase;
            }
            Starts = Array.AsReadOnly(copy);
        }
        public static WeaponPhraseSet Uniform(WeaponState weapon, WeaponPhrase phrase = null)
        {
            var starts = new WeaponPhrase[weapon.RequiredLanes];
            for (int i = 0; i < starts.Length; i++) starts[i] = phrase ?? WeaponPhraseCatalog.Find(weapon.DefinitionId);
            return new WeaponPhraseSet(weapon, starts);
        }
    }
}
