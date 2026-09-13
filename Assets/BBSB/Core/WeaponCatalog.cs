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
        private readonly IReadOnlyList<WeaponActionDefinition>[] unlocked;
        public string ActionLabel => ActionLabelAt(RunRules.MaximumUpgrade);
        public string EffectLabel => EffectLabelAt(RunRules.MaximumUpgrade);

        internal WeaponDefinition(string id, WeaponKind kind, params WeaponActionDefinition[] actions)
        {
            if (actions == null || actions.Length != (kind == WeaponKind.Shield ? 2 : 3))
                throw new ArgumentException("Shields need two actions; other weapons need three.", nameof(actions));
            var kinds = new HashSet<GestureKind>();
            foreach (var action in actions)
                if (action == null || !kinds.Add(action.Kind)) throw new ArgumentException("Weapon actions must be distinct.", nameof(actions));
            Id = id; Kind = kind; Actions = Array.AsReadOnly((WeaponActionDefinition[])actions.Clone());
            unlocked = new IReadOnlyList<WeaponActionDefinition>[actions.Length];
            for (int count = 1; count <= actions.Length; count++)
            { var group = new WeaponActionDefinition[count]; Array.Copy(actions, group, count); unlocked[count - 1] = Array.AsReadOnly(group); }
        }
        public WeaponActionDefinition ActionFor(GestureKind kind)
        { foreach (var action in Actions) if (action.Kind == kind) return action; return null; }
        public WeaponActionDefinition ActionFor(GestureKind kind, int level)
        { foreach (var action in ActionsAt(level)) if (action.Kind == kind) return action; return null; }
        public int ActionCountAt(int level)
        {
            if (level < 0 || level > RunRules.MaximumUpgrade) throw new ArgumentOutOfRangeException(nameof(level));
            if (Kind == WeaponKind.Shield) return level == RunRules.MaximumUpgrade ? 2 : 1;
            return level == 0 ? 1 : level == RunRules.MaximumUpgrade ? 3 : 2;
        }
        public IReadOnlyList<WeaponActionDefinition> ActionsAt(int level) => unlocked[ActionCountAt(level) - 1];
        public string ActionLabelAt(int level)
        {
            var labels = new List<string>(); foreach (var action in ActionsAt(level)) labels.Add(action.Kind.ToString());
            return string.Join(" / ", labels);
        }
        public string EffectLabelAt(int level)
        {
            var labels = new List<string>();
            foreach (var action in ActionsAt(level)) labels.Add(action.Kind + " · " + action.Name + ": " + action.EffectLabel);
            return string.Join("\n", labels);
        }
        public string ProgressionLabel => "+0 " + Actions[0].Kind + (Kind == WeaponKind.Shield ? "  ·  +3 " : "  ·  +1 ") +
            Actions[1].Kind + (Actions.Count > 2 ? "  ·  +3 " + Actions[2].Kind : "");
        public decimal LevelMultiplier(int level) => 1m + .25m * level;
    }

    // Weapons have action capabilities, never a beat pattern or a required sustain length.
    public static class WeaponCatalog
    {
        private static readonly WeaponDefinition[] weapons = {
            new WeaponDefinition("sword", WeaponKind.Sword,
                new WeaponActionDefinition(GestureKind.Tap, "베기", "12 피해", WeaponAttackStyle.Slash, 12),
                new WeaponActionDefinition(GestureKind.Flick, "회전 베기", "18 피해", WeaponAttackStyle.Sweep, 18),
                new WeaponActionDefinition(GestureKind.Hold, "모아 베기", "유지 완료 시 24 피해", WeaponAttackStyle.ChargedSlash, 24)),
            new WeaponDefinition("shield", WeaponKind.Shield,
                new WeaponActionDefinition(GestureKind.Hold, "방어 전개", "완료 시 방어막 12 · 4박 유지", WeaponAttackStyle.Guard, 0, guard: 12),
                new WeaponActionDefinition(GestureKind.Shake, "방패 밀치기", "8 피해 + 방어막 6 · 4박 유지", WeaponAttackStyle.ShieldBash, 8, guard: 6)),
            new WeaponDefinition("spear", WeaponKind.Spear,
                new WeaponActionDefinition(GestureKind.Tap, "찌르기", "Perfect에만 20 피해", WeaponAttackStyle.Thrust, 20, perfectOnly: true),
                new WeaponActionDefinition(GestureKind.Flick, "휘두르기", "16 피해", WeaponAttackStyle.Sweep, 16),
                new WeaponActionDefinition(GestureKind.Dive, "회피 찌르기", "끝 박에 떼면 24 피해", WeaponAttackStyle.Thrust, 24)),
            new WeaponDefinition("hammer", WeaponKind.Hammer,
                new WeaponActionDefinition(GestureKind.Tap, "내려찍기", "26 피해", WeaponAttackStyle.Slam, 26),
                new WeaponActionDefinition(GestureKind.Hold, "모아 찍기", "유지 완료 시 36 피해", WeaponAttackStyle.ChargedSlam, 36),
                new WeaponActionDefinition(GestureKind.Shake, "충격 파동", "20 피해", WeaponAttackStyle.Slam, 20)),
            new WeaponDefinition("dagger", WeaponKind.Dagger,
                new WeaponActionDefinition(GestureKind.Dive, "회피 반격", "끝 박에 떼면 14 피해", WeaponAttackStyle.CounterStab, 14),
                new WeaponActionDefinition(GestureKind.Tap, "빠른 찌르기", "5 피해 · 연속 성공마다 +2, 최대 +6", WeaponAttackStyle.QuickStab, 5, combo: true),
                new WeaponActionDefinition(GestureKind.Flick, "회전 찌르기", "12 피해", WeaponAttackStyle.CounterStab, 12)),
            new WeaponDefinition("greatsword", WeaponKind.Greatsword,
                new WeaponActionDefinition(GestureKind.Hold, "모아 베기", "유지 완료 시 26 피해", WeaponAttackStyle.ChargedSlash, 26),
                new WeaponActionDefinition(GestureKind.Dive, "올려 베기", "끝 박에 떼면 26 피해 · Perfect 추가 10", WeaponAttackStyle.RisingSlash, 26, perfectBonus: 10),
                new WeaponActionDefinition(GestureKind.Tap, "베기", "18 피해", WeaponAttackStyle.Slash, 18)),
            new WeaponDefinition("bell", WeaponKind.Bell,
                new WeaponActionDefinition(GestureKind.Shake, "공명 파동", "8 피해 · 2박 안의 다음 공격 +50%", WeaponAttackStyle.Resonance, 8, resonance: true),
                new WeaponActionDefinition(GestureKind.Hold, "보호 울림", "완료 시 방어막 10 · 4박 유지", WeaponAttackStyle.Ward, 0, guard: 10),
                new WeaponActionDefinition(GestureKind.Tap, "공명 타격", "8 피해 · 2박 안의 다음 공격 +50%", WeaponAttackStyle.Resonance, 8, resonance: true)),
            new WeaponDefinition("blade", WeaponKind.Blade,
                new WeaponActionDefinition(GestureKind.Flick, "왕복 베기", "왕복 10 + 6 피해를 함께 적용", WeaponAttackStyle.Returning, 16),
                new WeaponActionDefinition(GestureKind.Shake, "회전 난무", "회전 공격 12 피해", WeaponAttackStyle.Spin, 12),
                new WeaponActionDefinition(GestureKind.Dive, "회피 베기", "끝 박에 떼면 20 피해", WeaponAttackStyle.Spin, 20))
        };
        public static IReadOnlyList<WeaponDefinition> All { get; } = Array.AsReadOnly(weapons);
        public static WeaponDefinition Find(string id)
        {
            foreach (var weapon in weapons) if (weapon.Id == id) return weapon;
            throw new ArgumentException("Unknown weapon: " + id, nameof(id));
        }
    }
}
