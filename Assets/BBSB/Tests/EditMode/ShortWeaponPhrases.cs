using System;
using System.Collections.Generic;
using BBSB.Core;

// Fixed, small input sequences for timing/mechanic tests; catalog balance is tested separately.
namespace BBSB.Tests
{
    internal static class ShortWeaponPhrases
    {
        public static IReadOnlyList<string> ShieldIds { get; } = Array.AsReadOnly(new[] { "shield", "round-shield", "tower-shield", "heater-shield" });
        private static WeaponPhraseNote Tap(double at, decimal damage) => new WeaponPhraseNote(at, damage);
        private static WeaponPhraseNote Counter(double at, decimal damage) =>
            new WeaponPhraseNote(at, damage, prerequisite: 0, condition: PhraseNoteCondition.Parry);
        private static WeaponPhraseNote Hold(double at, double duration, decimal damage) => new WeaponPhraseNote(at, damage, duration);
        private static readonly WeaponPhrase[] phrases = Build();
        private static WeaponPhrase[] Build()
        {
            var result = new List<WeaponPhrase> {
            new WeaponPhrase("sword", "검", "Tap 0 / 1 / 2 · 4박 패턴", 4,
                new[] { Tap(0, 8), Tap(1, 8), Tap(2, 12) }),
            new WeaponPhrase("hammer", "트레실로 해머", "Tap 0 / 1.5 / 3 · 세 번째 완주에 그로기", 4,
                new[] { Tap(0, 6), Tap(1.5, 8), Tap(3, 12) }, 3, finisherEvery: 3, finisherDamage: 38, groggyBeats: 4),
            new WeaponPhrase("shield", "버클러", "첫 Tap 방어 성공 → 1 / 1.5 / 2 반격 · 패링 시 쿨타임 회복", 2.5,
                new[] { new WeaponPhraseNote(0, 0, effect: PhraseEffect.Parry), Counter(1, 5), Counter(1.5, 5), Counter(2, 9) }, 2, false),
            new WeaponPhrase("round-shield", "원형 방패", "첫 Tap 방어 성공 → 0.5 / 1.5 빠른 반격 · 패링 시 쿨타임 회복", 2,
                new[] { new WeaponPhraseNote(0, 0, effect: PhraseEffect.Parry), Counter(.5, 6), Counter(1.5, 10) }, 1.5, false),
            new WeaponPhrase("tower-shield", "대형 방패", "시작·1.5박 떼기 패링 → 2.5박 강타 · 패링 시 쿨타임 회복", 3,
                new[] { new WeaponPhraseNote(0, 0, 1.5, PhraseEffect.Parry), Counter(2.5, 30) }, 3, false,
                parryInput: ParryInputEdge.Both),
            new WeaponPhrase("heater-shield", "Heater Shield", "Hold 2 beats · press to parry · guard 50% · cooldown 2 beats, reset on parry", 2,
                new[] { new WeaponPhraseNote(0, 0, 2, PhraseEffect.Parry) }, repeat: false,
                holdDamageReduction: .5m, releaseEndsPhrase: true, parryRequired: false,
                completionCooldownBeats: 2),
            new WeaponPhrase("bow", "활", "Hold 1박으로 당기기 → 2박 Tap 발사", 4,
                new[] { Hold(0, 1, 0), new WeaponPhraseNote(2, 28, prerequisite: 0, condition: PhraseNoteCondition.Hit) }),
            new WeaponPhrase("dagger", "단검", "속성의 시작 박자에 맞춰 시작 → 성공하면 2박 뒤 다음 노트 · 미스 대기 2박", 2,
                new[] { Tap(0, 6) }, repeat: true),
            new WeaponPhrase("dual-swords", "쌍검", "속성의 시작 박자에 맞춰 시작 → 성공하면 1박 뒤 다음 노트 · 미스 대기 2박", 1,
                new[] { Tap(0, 6) }, repeat: true),
            new WeaponPhrase("staff", "봉", "시작한 쪽 Tap 0 / 0.5 → 반대쪽 1박에서 Hold 1박 · 2라인", 2,
                new[] { Tap(0, 8), Tap(.5, 8), new WeaponPhraseNote(1, 18, 1, laneOffset: 1) }),
            new WeaponPhrase("spirit-bell", "신령 방울", "Tap → 바로 양옆 무기 패턴을 1박 뒤 시작 · 쿨타임 무시 · 진행 중인 패턴 유지", 1,
                new[] { new WeaponPhraseNote(0, 0, effect: PhraseEffect.StartAdjacent) }),
            new WeaponPhrase("spear", "창", "Tap 0 / 2 · 4박 패턴", 4, new[] { Tap(0, 12), Tap(2, 18) }),
            new WeaponPhrase("greatsword", "대검", "Hold 1박 → 2박 Tap", 4, new[] { Hold(0, 1, 18), Tap(2, 20) }),
            new WeaponPhrase("bell", "종", "Tap → 1박에서 Hold 1박", 4, new[] { Tap(0, 8), Hold(1, 1, 16) }),
            new WeaponPhrase("blade", "쌍날검", "Tap 0 / 0.5 / 2 / 2.5", 4, new[] { Tap(0, 6), Tap(.5, 6), Tap(2, 6), Tap(2.5, 10) }),
            new WeaponPhrase("crossbow", "석궁", "Tap 0 / 1 · 3박 패턴", 3, new[] { Tap(0, 10), Tap(1, 14) }),
            new WeaponPhrase("wand", "마도봉", "Hold 1.5박 → 2.5박 Tap", 4, new[] { Hold(0, 1.5, 10), Tap(2.5, 22) })
            };
            foreach (var entry in ShortExpansionPhrases.All)
                result.Add(ShortExpansionPhrases.Pattern(entry.Definition.Id, 0, WeaponBeatSide.Light));
            return result.ToArray();
        }
        public static IReadOnlyList<WeaponPhrase> All { get; } = Array.AsReadOnly(phrases);
        public static WeaponPhrase Default(string weaponId, int offset, WeaponBeatSide side, bool transition = false)
        {
            var expanded = ShortExpansionPhrases.Pattern(weaponId, offset, side, transition);
            return expanded ?? (transition ? null : Find(weaponId));
        }
        public static WeaponPhraseSet Set(WeaponState weapon)
        {
            var light = new WeaponPhrase[weapon.RequiredLanes]; var dark = new WeaponPhrase[light.Length];
            var lb = new WeaponPhrase[light.Length]; var db = new WeaponPhrase[light.Length];
            for (int i = 0; i < light.Length; i++)
            {
                light[i] = Default(weapon.DefinitionId, i, WeaponBeatSide.Light);
                dark[i] = Default(weapon.DefinitionId, i, WeaponBeatSide.Dark);
                lb[i] = Default(weapon.DefinitionId, i, WeaponBeatSide.Light, true);
                db[i] = Default(weapon.DefinitionId, i, WeaponBeatSide.Dark, true);
            }
            return new WeaponPhraseSet(weapon, light, dark, lb, db);
        }
        public static WeaponPhrase Find(string weaponId)
        {
            foreach (var phrase in phrases) if (phrase.WeaponId == weaponId) return phrase;
            throw new ArgumentException("Unknown phrase weapon: " + weaponId, nameof(weaponId));
        }
    }
    internal static class ShortExpansionPhrases
    {
        private static WeaponExpansionEntry Find(string id) => WeaponExpansion.Find(id);
        public static IReadOnlyList<WeaponExpansionEntry> All => WeaponExpansion.All;
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
