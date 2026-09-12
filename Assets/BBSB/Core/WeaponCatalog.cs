using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public enum WeaponKind { Sword, Shield, Spear, Hammer, Dagger, Greatsword, Bell, Blade }

    public sealed class WeaponDefinition
    {
        public string Id { get; }
        public string Name => ContentCatalog.Find(Id).Name;
        public WeaponKind Kind { get; }
        public RhythmPattern Pattern { get; }
        public string PatternLabel { get; }
        public string EffectLabel { get; }
        public IReadOnlyList<decimal> Damage { get; }
        public decimal CompletionBonus { get; }
        public bool PerfectOnly { get; }
        public decimal Shield { get; }

        internal WeaponDefinition(string id, WeaponKind kind, string patternLabel, string effectLabel,
            PatternStep[] steps, decimal[] damage, decimal bonus = 0, bool perfectOnly = false, decimal shield = 0)
        {
            Id = id; Kind = kind; PatternLabel = patternLabel; EffectLabel = effectLabel;
            Pattern = new RhythmPattern("weapon-" + id, 4, steps);
            Damage = Array.AsReadOnly(damage); CompletionBonus = bonus; PerfectOnly = perfectOnly; Shield = shield;
        }

        public decimal LevelMultiplier(int level) => 1m + .25m * level;
    }

    // Damage and guard are multiplied by 1 + .25 * upgrade level. HalfMiss halves output.
    public static class WeaponCatalog
    {
        private static PatternStep Tap(int tick) => new PatternStep(GestureKind.Tap, tick);
        private static readonly WeaponDefinition[] weapons = {
            new WeaponDefinition("sword", WeaponKind.Sword, "Tap · Tap (1박 간격)",
                "8 + 12 피해. 두 입력 모두 성공하면 추가 8 피해.", new[] { Tap(0), Tap(4) }, new[] { 8m, 12m }, 8),
            new WeaponDefinition("shield", WeaponKind.Shield, "Hold (1박)",
                "유지 완료 시 방어막 12. 4박 동안 받는 피해를 흡수.",
                new[] { new PatternStep(GestureKind.Hold, 0, 4) }, new[] { 0m }, shield: 12),
            new WeaponDefinition("spear", WeaponKind.Spear, "Tap", "Perfect에만 관통 찌르기 20 피해.",
                new[] { Tap(0) }, new[] { 20m }, perfectOnly: true),
            new WeaponDefinition("hammer", WeaponKind.Hammer, "Tap · Tap (2박 간격)",
                "10 + 26 피해. 두 입력 모두 성공하면 내려찍기 추가 14 피해.",
                new[] { Tap(0), Tap(8) }, new[] { 10m, 26m }, 14),
            new WeaponDefinition("dagger", WeaponKind.Dagger, "Tap · Tap (반박 간격)",
                "각 5 피해. 연속 성공마다 +2, 최대 +6. 무기 Miss 시 초기화.",
                new[] { Tap(0), Tap(2) }, new[] { 5m, 5m }),
            new WeaponDefinition("greatsword", WeaponKind.Greatsword, "Dive (1박 뒤 떼기)",
                "떼기에 26 피해. Perfect면 추가 10 피해.",
                new[] { new PatternStep(GestureKind.Dive, 0, 4) }, new[] { 26m }, 10),
            new WeaponDefinition("bell", WeaponKind.Bell, "Shake (1박)",
                "파동 8 피해. 2박 안의 다음 무기 공격 피해 +50% (HalfMiss +25%).",
                new[] { new PatternStep(GestureKind.Shake, 0, 4) }, new[] { 8m }),
            new WeaponDefinition("blade", WeaponKind.Blade, "Flick", "비검 왕복 공격. 10 + 6 피해를 함께 판정.",
                new[] { new PatternStep(GestureKind.Flick, 0) }, new[] { 16m })
        };
        public static IReadOnlyList<WeaponDefinition> All { get; } = Array.AsReadOnly(weapons);
        public static WeaponDefinition Find(string id)
        {
            foreach (var weapon in weapons) if (weapon.Id == id) return weapon;
            throw new ArgumentException("Unknown weapon: " + id, nameof(id));
        }
    }
}
