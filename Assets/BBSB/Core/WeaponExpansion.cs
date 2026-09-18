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

        private static WeaponPhraseNote Hit(double beat, decimal damage, int offset = 0, double hold = 0) =>
            new WeaponPhraseNote(beat, damage, hold, laneOffset: offset);
        private static WeaponPhraseNote Effect(double beat, decimal amount, PhraseEffect effect, int offset = 0,
            double hold = 0, double duration = 2) =>
            new WeaponPhraseNote(beat, amount, hold, effect, laneOffset: offset, effectDurationBeats: duration);
        private static WeaponPhrase Phrase(string id, string hint, double length, params WeaponPhraseNote[] notes) =>
            new WeaponPhrase(id, Find(id).Name, hint, length, notes);

        public static WeaponPhrase Pattern(string id, int startOffset, WeaponBeatSide side, bool transition = false)
        {
            if (Find(id) == null) return null;
            if (transition && id != "chaos-pendulum") return null;
            bool light = side == WeaponBeatSide.Light;
            switch (id)
            {
                case "rapier": return Phrase(id, "Tap 0 / 0.5 / 1 · 마지막 찌르기 강화", 2,
                    Hit(0, 6), Hit(.5, 6), Hit(1, 16));
                case "battle-axe": return Phrase(id, "시작 쪽 Tap → 반대쪽 1박 Hold · 2라인", 3,
                    Hit(0, 10), Hit(1, 32, 1, 1));
                case "chain-sickle": return Phrase(id, "시작 쪽 → 반대쪽 반박 → 시작 쪽 1.5박 당기기", 3,
                    Hit(0, 8), Hit(.5, 10, 1), Hit(1.5, 24, 0, .5));
                case "war-fan": return Phrase(id, "Tap 0 / 0.5 / 1.5 · 빠르게 펼쳐 베기", 2,
                    Hit(0, 6), Hit(.5, 8), Hit(1.5, 16));
                case "halberd":
                    // Starting in the center sweeps left then right; edge starts
                    // sweep across the footprint. The finish belongs to that route.
                    int next = startOffset == 0 ? 1 : 2;
                    int end = startOffset == 0 ? 2 : 1;
                    return Phrase(id, "시작 쪽 찌르기 → 다음 라인 → 마지막 라인 1박 Hold · 3라인", 4,
                        Hit(0, 10), Hit(1, 14, next), Hit(2, 34, end, 1));
                case "war-drum": return Phrase(id, "3라인 반박 타격 · 마지막에 바깥 양옆 무기 1박 뒤 시작", 2,
                    Hit(0, 8), Hit(.5, 8, 1), Effect(1, 0, PhraseEffect.StartAdjacent, 2));
                case "tuning-fork": return Phrase(id, "시작 쪽 Tap → 반대쪽 1박 Tap · 바깥 양옆 쿨타임 1박 감소", 2,
                    Hit(0, 6), Effect(1, 1, PhraseEffect.ReduceAdjacentCooldown, 1));
                case "war-banner": return Phrase(id, "시작 쪽 1박 Hold → 반대쪽 Tap · 양옆 다음 공격 +50%, 3박 유지", 3,
                    Hit(0, 0, hold: 1), Effect(1.5, .5m, PhraseEffect.EmpowerAdjacent, 1, duration: 3));
                case "censer": return Phrase(id, "2박 Hold 완료 시 체력 5 회복 · 단발", 3,
                    Effect(0, 5, PhraseEffect.Heal, hold: 2));
                case "chakram": return Phrase(id, "두 라인 왕복 Tap 0 / 0.5 / 1 / 1.5", 2,
                    Hit(0, 8), Hit(.5, 8, 1), Hit(1, 8), Hit(1.5, 16, 1));
                case "twilight-staff": return Phrase(id,
                    light ? "Tap 회복 2 → Hold 회복 4" : "Tap 피해 10 → Hold 피해 26", 3,
                    Effect(0, light ? 2 : 10, light ? PhraseEffect.Heal : PhraseEffect.Strike),
                    Effect(1, light ? 4 : 26, light ? PhraseEffect.Heal : PhraseEffect.Strike, 1, 1));
                case "eclipse-mirror": return Phrase(id,
                    light ? "양옆 쿨타임 -1박 · 회복 2" : "양옆 공격 +50% · 피해 10", 2,
                    Effect(0, light ? 1 : .5m, light ? PhraseEffect.ReduceAdjacentCooldown : PhraseEffect.EmpowerAdjacent, duration: 3),
                    Effect(1, light ? 2 : 10, light ? PhraseEffect.Heal : PhraseEffect.Strike));
                case "chaos-pendulum":
                    if (transition)
                        return new WeaponPhrase(id, Find(id).Name, "3라인 교차 Tap 0 / 0.5 / 1 / 1.5 / 2 → 반대 박자 유지", 2.5,
                            new[] { Hit(0, 7), Hit(.5, 7, 1), Hit(1, 7, 2), Hit(1.5, 7, 1), Hit(2, 12) }, repeat: true);
                    return new WeaponPhrase(id, Find(id).Name, "3라인 순환 Tap · 성공 시 반복", 3,
                        new[] { Hit(0, 8), Hit(1, 8, 1), Hit(2, 12, 2) }, repeat: true);
                default: throw new ArgumentException("Unknown expansion weapon.", nameof(id));
            }
        }
    }
}
