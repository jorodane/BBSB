using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public sealed class WeaponExpansionEntry
    {
        public WeaponDefinition Definition { get; }
        public string Name { get; }
        public int Price { get; }
        internal WeaponExpansionEntry(WeaponDefinition definition, string name, int price)
        { Definition = definition; Name = name; Price = price; }
    }

    // Families keep their existing motion/fallback silhouette; IDs, footprints,
    // patterns, attributes and artwork remain independent per weapon.
    public static class WeaponExpansion
    {
        private static WeaponExpansionEntry Melee(string id, string name, int price, WeaponKind family, int lanes,
            decimal damage, WeaponAttackStyle motion, WeaponAttribute? exclusive = null) =>
            new WeaponExpansionEntry(new WeaponDefinition(id, family, lanes, exclusive,
                new WeaponActionDefinition(GestureKind.Tap, "타격", "피해", motion, damage),
                new WeaponActionDefinition(GestureKind.Hold, "모아치기", "유지 후 피해", WeaponAttackStyle.ChargedSlam, damage * 2),
                new WeaponActionDefinition(GestureKind.Flick, "휘두르기", "피해", WeaponAttackStyle.Sweep, damage * 1.5m)), name, price);
        private static WeaponExpansionEntry Support(string id, string name, int price, int lanes,
            WeaponAttribute? exclusive = null) =>
            new WeaponExpansionEntry(new WeaponDefinition(id, WeaponKind.Bell, lanes, exclusive,
                new WeaponActionDefinition(GestureKind.Tap, "공명", "다음 공격 강화", WeaponAttackStyle.Resonance, 0, resonance: true),
                new WeaponActionDefinition(GestureKind.Hold, "보호", "방어막 10", WeaponAttackStyle.Ward, 0, guard: 10),
                new WeaponActionDefinition(GestureKind.Shake, "파동", "8 피해", WeaponAttackStyle.Resonance, 8)), name, price);
        public static IReadOnlyList<WeaponExpansionEntry> All { get; } = Array.AsReadOnly(new[] {
            Melee("rapier", "레이피어", 50, WeaponKind.Sword, 1, 7, WeaponAttackStyle.Thrust),
            Melee("battle-axe", "전투 도끼", 65, WeaponKind.Hammer, 2, 12, WeaponAttackStyle.Slam),
            Melee("chain-sickle", "사슬낫", 65, WeaponKind.Blade, 2, 8, WeaponAttackStyle.Returning),
            Melee("war-fan", "철선", 55, WeaponKind.Blade, 1, 6, WeaponAttackStyle.Sweep),
            Melee("halberd", "미늘창", 75, WeaponKind.Spear, 3, 12, WeaponAttackStyle.Thrust),
            Support("war-drum", "전쟁 북", 75, 3),
            Support("tuning-fork", "소리쇠", 60, 2),
            Support("war-banner", "군기", 65, 2),
            Support("censer", "향로", 60, 1),
            Melee("chakram", "차크람", 65, WeaponKind.Blade, 2, 8, WeaponAttackStyle.Spin),
            Melee("twilight-staff", "양면 지팡이", 75, WeaponKind.Staff, 2, 12, WeaponAttackStyle.MagicPulse, WeaponAttribute.Dual),
            Support("eclipse-mirror", "일식 거울", 75, 1, WeaponAttribute.Dual),
            Melee("chaos-pendulum", "혼돈의 진자", 90, WeaponKind.Blade, 3, 10, WeaponAttackStyle.Spin, WeaponAttribute.Chaos)
        });
        public static WeaponExpansionEntry Find(string id)
        { foreach (var entry in All) if (entry.Definition.Id == id) return entry; return null; }

        public static WeaponPhrase Pattern(string id, int startOffset, WeaponBeatSide side, bool transition = false) =>
            Find(id) == null ? null : LongWeaponPatterns.Pattern(id, startOffset, side, transition);
    }
}
