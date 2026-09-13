using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    /// <summary>Equipped weapons automatically answer every supported action in every monster pattern.</summary>
    public sealed class WeaponLoadout
    {
        private readonly List<WeaponState> equipment = new List<WeaponState>();
        private readonly List<PlannedAttack> patterns = new List<PlannedAttack>();
        public IReadOnlyList<WeaponState> Equipment { get; }
        public IReadOnlyList<PlannedAttack> Patterns { get; }

        public WeaponLoadout(BattlePlan plan, IReadOnlyList<WeaponState> weapons)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (weapons == null || weapons.Count > RunRules.WeaponSlots) throw new ArgumentException("Invalid equipment.", nameof(weapons));
            foreach (var weapon in weapons)
            {
                if (weapon == null) throw new ArgumentException("A weapon slot cannot be null.", nameof(weapons));
                WeaponCatalog.Find(weapon.DefinitionId);
                equipment.Add(new WeaponState(weapon.DefinitionId, weapon.Rarity, weapon.Level));
            }
            foreach (var attack in plan.Attacks)
                if (!patterns.Exists(p => p.MonsterId == attack.MonsterId && p.Pattern.Id == attack.Pattern.Id)) patterns.Add(attack);
            Equipment = equipment.AsReadOnly(); Patterns = patterns.AsReadOnly();
        }

        public WeaponActionDefinition ActionFor(int slot, GestureKind kind)
        {
            if (slot < 0 || slot >= equipment.Count) return null;
            var state = equipment[slot];
            return WeaponCatalog.Find(state.DefinitionId).ActionFor(kind, state.Rarity);
        }

        public bool RespondsTo(int slot, PlannedAttack pattern)
        {
            if (pattern == null) return false;
            foreach (var step in pattern.Placement.Pattern.Steps)
                if (ActionFor(slot, step.Kind) != null) return true;
            return false;
        }

        // A slot appears once on a pattern card even when it answers several steps or action kinds.
        public IReadOnlyList<int> RespondingSlots(PlannedAttack pattern)
        {
            var result = new List<int>();
            for (int slot = 0; slot < equipment.Count; slot++)
                if (RespondsTo(slot, pattern)) result.Add(slot);
            return result.AsReadOnly();
        }

        internal WeaponLoadout Snapshot(BattlePlan target) => new WeaponLoadout(target, Equipment);
    }
}
