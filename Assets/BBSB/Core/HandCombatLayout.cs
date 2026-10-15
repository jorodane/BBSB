using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    // Equipment slots and defense directions are separate concepts. The one shield
    // is bound to four independent contacts; it is never copied into the inventory.
    public static class HandCombatLayout
    {
        public const int AttackCount = 4, DefenseCount = 4, EquipmentCount = 5, InputCount = 8, ShieldSlot = 4;
        public static readonly IReadOnlyList<int> EquipmentOrder = Array.AsReadOnly(new[] { 0, 1, 2, 3, 4 });
        public static readonly IReadOnlyList<int> InputOrder = Array.AsReadOnly(new[] { 0, 1, 2, 3, 4, 5, 6, 7 });
        public static string Key(int input)
        {
            switch (input)
            {
                case 0: return "Z"; case 1: return "X"; case 2: return "C"; case 3: return "V";
                case 4: return "←"; case 5: return "↓"; case 6: return "↑"; case 7: return "→";
                default: throw new ArgumentOutOfRangeException(nameof(input));
            }
        }
        public static bool IsDefense(int input) => input >= ShieldSlot && input < InputCount;
        public static bool IsShield(WeaponState weapon) => weapon != null && WeaponCatalog.Find(weapon.DefinitionId).Kind == WeaponKind.Shield;
        public static bool Fits(WeaponState weapon, int slot) => weapon != null &&
            (IsShield(weapon) ? slot == ShieldSlot : slot >= 0 && slot < AttackCount);
    }
}
