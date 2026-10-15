using System;

namespace BBSB.Core
{
    // Preserve the item's authored guard/parry properties without asking the right
    // hand to perform its former counterattack combo. Original assets are untouched.
    internal static class HandDefensePatterns
    {
        internal static WeaponPhraseSet From(WeaponState weapon, WeaponPhraseSet source)
        {
            var light = new WeaponPhrase[weapon.RequiredLanes];
            var dark = new WeaponPhrase[weapon.RequiredLanes];
            for (int i = 0; i < light.Length; i++)
            { light[i] = Guard(source.LightStarts[i]); dark[i] = Guard(source.DarkStarts[i]); }
            return new WeaponPhraseSet(weapon, light, dark);
        }
        private static WeaponPhrase Guard(WeaponPhrase source)
        {
            var first = source.Notes[0];
            var defense = new WeaponPhraseNote(0, 0, first.HoldBeats,
                first.IsParry ? PhraseEffect.Parry : PhraseEffect.Strike, bonusHealing: first.BonusHealing);
            return new WeaponPhrase(source.WeaponId, source.Name, source.Hint,
                source.LengthBeats, new[] { defense }, source.MissCooldownBeats,
                parryInput: source.ParryInput, holdDamageReduction: source.HoldDamageReduction,
                releaseEndsPhrase: source.ReleaseEndsPhrase, parryRequired: source.ParryRequired,
                completionCooldownBeats: source.CompletionCooldownBeats);
        }
    }
}
