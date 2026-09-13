using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public enum WeaponRarity { Common, Rare, Epic, Legendary }

    public static class WeaponRarities
    {
        public static IReadOnlyList<WeaponRarity> All { get; } = Array.AsReadOnly(new[] {
            WeaponRarity.Common, WeaponRarity.Rare, WeaponRarity.Epic, WeaponRarity.Legendary });
        public static void Validate(WeaponRarity rarity)
        { if (rarity < WeaponRarity.Common || rarity > WeaponRarity.Legendary) throw new ArgumentOutOfRangeException(nameof(rarity)); }
        public static string Name(WeaponRarity rarity)
        {
            Validate(rarity);
            switch (rarity)
            {
                case WeaponRarity.Common: return "일반";
                case WeaponRarity.Rare: return "희귀";
                case WeaponRarity.Epic: return "영웅";
                default: return "전설";
            }
        }
        public static string Key(WeaponRarity rarity)
        {
            Validate(rarity);
            return rarity == WeaponRarity.Common ? "common" : rarity == WeaponRarity.Rare ? "rare" :
                rarity == WeaponRarity.Epic ? "epic" : "legendary";
        }
        // Initial loot weights: 55 / 30 / 12 / 3. Rarity is rolled once and retained by the offer.
        public static WeaponRarity Roll(SeededRandom random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            int roll = random.Next(100);
            return roll < 55 ? WeaponRarity.Common : roll < 85 ? WeaponRarity.Rare : roll < 97 ? WeaponRarity.Epic : WeaponRarity.Legendary;
        }
    }
}
