using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public enum WeaponKind { Sword, Shield, Spear, Hammer, Dagger, Greatsword, Bell, Blade }
    public enum WeaponAttackStyle { Slash, Sweep, Guard, ShieldBash, Thrust, Slam, ChargedSlam,
        QuickStab, CounterStab, ChargedSlash, RisingSlash, Resonance, Ward, Returning, Spin }

    public sealed class WeaponActionDefinition
    {
        public GestureKind Kind { get; }
        public string Name { get; }
        public string EffectLabel { get; }
        public WeaponAttackStyle Motion { get; }
        public decimal Damage { get; }
        public decimal Guard { get; }
        public decimal PerfectBonus { get; }
        public bool PerfectOnly { get; }
        public bool BuildsCombo { get; }
        public bool GrantsResonance { get; }

        internal WeaponActionDefinition(GestureKind kind, string name, string effect, WeaponAttackStyle motion,
            decimal damage, decimal guard = 0, decimal perfectBonus = 0, bool perfectOnly = false,
            bool combo = false, bool resonance = false)
        { Kind = kind; Name = name; EffectLabel = effect; Motion = motion; Damage = damage; Guard = guard;
            PerfectBonus = perfectBonus; PerfectOnly = perfectOnly; BuildsCombo = combo; GrantsResonance = resonance; }
    }

    public sealed class WeaponDefinition
    {
        public string Id { get; }
        public string Name => ContentCatalog.Find(Id).Name;
        public WeaponKind Kind { get; }
        public IReadOnlyList<WeaponActionDefinition> Actions { get; }
        public string ActionLabel => Actions[0].Kind + " / " + Actions[1].Kind;
        public string EffectLabel => Actions[0].Kind + " · " + Actions[0].Name + ": " + Actions[0].EffectLabel + "\n" +
            Actions[1].Kind + " · " + Actions[1].Name + ": " + Actions[1].EffectLabel;

        internal WeaponDefinition(string id, WeaponKind kind, params WeaponActionDefinition[] actions)
        {
            if (actions.Length != 2 || actions[0] == null || actions[1] == null || actions[0].Kind == actions[1].Kind)
                throw new ArgumentException("A weapon needs two distinct actions.", nameof(actions));
            Id = id; Kind = kind; Actions = Array.AsReadOnly((WeaponActionDefinition[])actions.Clone());
        }
        public WeaponActionDefinition ActionFor(GestureKind kind)
        { foreach (var action in Actions) if (action.Kind == kind) return action; return null; }
        public decimal LevelMultiplier(int level) => 1m + .25m * level;
    }

    // Weapons have action capabilities, never a beat pattern or a required sustain length.
    public static class WeaponCatalog
    {
        private static readonly WeaponDefinition[] weapons = {
            new WeaponDefinition("sword", WeaponKind.Sword,
                new WeaponActionDefinition(GestureKind.Tap, "베기", "12 피해", WeaponAttackStyle.Slash, 12),
                new WeaponActionDefinition(GestureKind.Flick, "회전 베기", "18 피해", WeaponAttackStyle.Sweep, 18)),
            new WeaponDefinition("shield", WeaponKind.Shield,
                new WeaponActionDefinition(GestureKind.Hold, "방어 전개", "완료 시 방어막 12 · 4박 유지", WeaponAttackStyle.Guard, 0, guard: 12),
                new WeaponActionDefinition(GestureKind.Shake, "방패 밀치기", "8 피해 + 방어막 6 · 4박 유지", WeaponAttackStyle.ShieldBash, 8, guard: 6)),
            new WeaponDefinition("spear", WeaponKind.Spear,
                new WeaponActionDefinition(GestureKind.Tap, "찌르기", "Perfect에만 20 피해", WeaponAttackStyle.Thrust, 20, perfectOnly: true),
                new WeaponActionDefinition(GestureKind.Flick, "휘두르기", "16 피해", WeaponAttackStyle.Sweep, 16)),
            new WeaponDefinition("hammer", WeaponKind.Hammer,
                new WeaponActionDefinition(GestureKind.Tap, "내려찍기", "26 피해", WeaponAttackStyle.Slam, 26),
                new WeaponActionDefinition(GestureKind.Hold, "모아 찍기", "유지 완료 시 36 피해", WeaponAttackStyle.ChargedSlam, 36)),
            new WeaponDefinition("dagger", WeaponKind.Dagger,
                new WeaponActionDefinition(GestureKind.Tap, "빠른 찌르기", "5 피해 · 연속 성공마다 +2, 최대 +6", WeaponAttackStyle.QuickStab, 5, combo: true),
                new WeaponActionDefinition(GestureKind.Dive, "회피 반격", "끝 박에 떼면 14 피해", WeaponAttackStyle.CounterStab, 14)),
            new WeaponDefinition("greatsword", WeaponKind.Greatsword,
                new WeaponActionDefinition(GestureKind.Hold, "모아 베기", "유지 완료 시 26 피해", WeaponAttackStyle.ChargedSlash, 26),
                new WeaponActionDefinition(GestureKind.Dive, "올려 베기", "끝 박에 떼면 26 피해 · Perfect 추가 10", WeaponAttackStyle.RisingSlash, 26, perfectBonus: 10)),
            new WeaponDefinition("bell", WeaponKind.Bell,
                new WeaponActionDefinition(GestureKind.Shake, "공명 파동", "8 피해 · 2박 안의 다음 공격 +50%", WeaponAttackStyle.Resonance, 8, resonance: true),
                new WeaponActionDefinition(GestureKind.Hold, "보호 울림", "완료 시 방어막 10 · 4박 유지", WeaponAttackStyle.Ward, 0, guard: 10)),
            new WeaponDefinition("blade", WeaponKind.Blade,
                new WeaponActionDefinition(GestureKind.Flick, "왕복 베기", "왕복 10 + 6 피해를 함께 적용", WeaponAttackStyle.Returning, 16),
                new WeaponActionDefinition(GestureKind.Shake, "회전 난무", "회전 공격 12 피해", WeaponAttackStyle.Spin, 12))
        };
        public static IReadOnlyList<WeaponDefinition> All { get; } = Array.AsReadOnly(weapons);
        public static WeaponDefinition Find(string id)
        {
            foreach (var weapon in weapons) if (weapon.Id == id) return weapon;
            throw new ArgumentException("Unknown weapon: " + id, nameof(id));
        }
    }
}
