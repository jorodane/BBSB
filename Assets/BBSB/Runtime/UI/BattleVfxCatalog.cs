using System;
using System.Collections.Generic;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    public static class BattleVfxCatalog
    {
        public const string Root = "BBSB/BattleVfx/";
        public const double ImpactDuration = .28;
        public static IReadOnlyList<string> Keys { get; } = Array.AsReadOnly(new[] {
            "slash", "pierce", "impact", "ring", "parry", "dodge", "arrow", "bolt", "orb", "muzzle" });
        public static string Projectile(WeaponKind kind) => kind == WeaponKind.Bow ? "arrow" : kind == WeaponKind.Crossbow ? "bolt" : "orb";
        public static string Strike(WeaponAttackStyle style)
        {
            switch (style)
            {
                case WeaponAttackStyle.Thrust:
                case WeaponAttackStyle.QuickStab:
                case WeaponAttackStyle.CounterStab: return "pierce";
                case WeaponAttackStyle.Slam:
                case WeaponAttackStyle.ChargedSlam:
                case WeaponAttackStyle.ShieldBash: return "impact";
                case WeaponAttackStyle.Resonance:
                case WeaponAttackStyle.Ward: return "ring";
                case WeaponAttackStyle.Guard: return "parry";
                default: return "slash";
            }
        }
        public static string Reaction(MonsterAttackShape shape, GestureKind gesture)
        {
            if (gesture == GestureKind.Dive || gesture == GestureKind.Flick) return "dodge";
            switch (shape)
            {
                case MonsterAttackShape.Gauntlet:
                case MonsterAttackShape.Scale:
                case MonsterAttackShape.Doll: return "parry";
                case MonsterAttackShape.Feather:
                case MonsterAttackShape.Thread: return "slash";
                case MonsterAttackShape.Veil:
                case MonsterAttackShape.Membrane:
                case MonsterAttackShape.Dream: return "ring";
                default: return "impact";
            }
        }
        public static int ProjectileCount(WeaponAttackStyle style) => style == WeaponAttackStyle.ArrowVolley ? 3 : 1;
        public static double Progress(double age, double duration) => age >= 0 && age < duration ? age / duration : -1;
    }
}
