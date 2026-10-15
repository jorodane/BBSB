using System;
using System.Collections.Generic;
using System.Linq;

namespace BBSB.Core
{
    // A placement owns physical input positions, not additional copies of the item.
    // Offsets are ordered left to right in a contiguous physical footprint, including S/L.
    public sealed class WeaponPlacement
    {
        public WeaponState Weapon { get; }
        public IReadOnlyList<int> Slots { get; }
        public bool IsHandSlot { get; }
        public int Offset => BattleInputLayout.Position(Slots[0]);
        public double Center => Offset + (Slots.Count - 1) * .5;
        public WeaponPlacement(WeaponState weapon, params int[] slots)
        {
            Weapon = weapon ?? throw new ArgumentNullException(nameof(weapon));
            if (slots == null || slots.Length != weapon.RequiredLanes)
                throw new ArgumentException("Select exactly the number of lanes required by this weapon.", nameof(slots));
            var copy = (int[])slots.Clone(); Array.Sort(copy, (a, b) => BattleInputLayout.Position(a).CompareTo(BattleInputLayout.Position(b)));
            for (int i = 0; i < copy.Length; i++)
                if (copy[i] < 0 || copy[i] >= BattleInputLayout.LaneCount || i > 0 && copy[i] == copy[i - 1])
                    throw new ArgumentException("Input positions must be distinct and valid.", nameof(slots));
            for (int i = 1; i < copy.Length; i++)
                if (BattleInputLayout.Position(copy[i]) != BattleInputLayout.Position(copy[i - 1]) + 1)
                    throw new ArgumentException("A weapon occupies consecutive physical lines.", nameof(slots));
            if (copy.Length == 1 && (copy[0] == BattleInputLayout.Left || copy[0] == BattleInputLayout.Right))
                throw new ArgumentException("Single-line weapons cannot use extension lines.", nameof(slots));
            Slots = Array.AsReadOnly(copy);
        }
        private WeaponPlacement(WeaponState weapon, int slot)
        {
            Weapon = weapon ?? throw new ArgumentNullException(nameof(weapon));
            if (!HandCombatLayout.Fits(weapon, slot) && !(HandCombatLayout.IsShield(weapon) && HandCombatLayout.IsDefense(slot)))
                throw new ArgumentException("Attack weapons use Z/X/C/V; shields use the defense slot.");
            IsHandSlot = true; Slots = Array.AsReadOnly(new[] { slot });
        }
        public static WeaponPlacement ForHand(WeaponState weapon, int slot) => new WeaponPlacement(weapon, slot);
        public static bool TryCentered(WeaponState weapon, double center, out WeaponPlacement placement)
        {
            placement = null;
            if (weapon == null || double.IsNaN(center) || double.IsInfinity(center)) return false;
            double left = Math.Floor(center - (weapon.RequiredLanes - 1) * .5 + .5);
            if (left < -1 || left + weapon.RequiredLanes - 1 > BattleInputLayout.MainLaneCount) return false;
            var slots = new int[weapon.RequiredLanes];
            for (int i = 0; i < slots.Length; i++) slots[i] = BattleInputLayout.SlotAtPosition((int)left + i);
            try { placement = new WeaponPlacement(weapon, slots); return true; }
            catch (ArgumentException) { return false; }
        }
        public int OffsetOf(int slot)
        { for (int i = 0; i < Slots.Count; i++) if (Slots[i] == slot) return i; return -1; }
    }

    // Item capacity and physical lane occupancy are deliberately independent.
    public sealed class WeaponEquipment
    {
        private readonly List<WeaponState> owned = new List<WeaponState>();
        private readonly List<WeaponState> equipped = new List<WeaponState>();
        private readonly List<WeaponPlacement> placements = new List<WeaponPlacement>();
        public IReadOnlyList<WeaponState> Owned { get; }
        public IReadOnlyList<WeaponState> Equipped { get; }
        public IReadOnlyList<WeaponPlacement> Placements { get; }
        public int Capacity { get; private set; }
        public bool SeparateHands { get; }
        public IReadOnlyList<int> DisplayOrder => SeparateHands ? HandCombatLayout.EquipmentOrder : BattleInputLayout.DisplayOrder;
        public string Key(int slot) => SeparateHands ? slot == HandCombatLayout.ShieldSlot ? "방어 · 방향키" : HandCombatLayout.Key(slot) : BattleInputLayout.Key(slot);
        public InputExtensions Extensions { get; private set; }
        public bool IsAvailable(int slot) => SeparateHands ? slot >= 0 && slot < HandCombatLayout.EquipmentCount : BattleInputLayout.Available(slot, Extensions);
        public int AvailableLaneCount => SeparateHands ? HandCombatLayout.EquipmentCount : BattleInputLayout.MainLaneCount + ((Extensions & InputExtensions.Left) != 0 ? 1 : 0) +
            ((Extensions & InputExtensions.Right) != 0 ? 1 : 0);
        public int OccupiedLaneCount
        { get { int count = 0; foreach (var p in placements) count += p.Slots.Count; return count; } }
        internal WeaponEquipment(bool separateHands = false)
        { SeparateHands = separateHands; Owned = owned.AsReadOnly(); Equipped = equipped.AsReadOnly(); Placements = placements.AsReadOnly(); Reset(); }
        internal void Reset()
        { owned.Clear(); equipped.Clear(); placements.Clear(); Capacity = SeparateHands ? HandCombatLayout.EquipmentCount : 2; Extensions = InputExtensions.All; }
        internal void Unlock(InputExtensions extensions)
        { BattleInputLayout.Validate(extensions); Extensions |= extensions; }
        internal void Acquire(WeaponState weapon)
        {
            if (weapon == null || owned.Contains(weapon)) throw new ArgumentException("Acquire a new item instance.", nameof(weapon));
            owned.Add(weapon);
        }
        internal bool IncreaseCapacity()
        { if (Capacity == RunRules.WeaponSlots) return false; Capacity++; return true; }
        public WeaponPlacement PlacementOf(WeaponState weapon)
        { foreach (var p in placements) if (ReferenceEquals(p.Weapon, weapon)) return p; return null; }
        public WeaponPlacement At(int slot)
        { foreach (var p in placements) if (p.OffsetOf(slot) >= 0) return p; return null; }
        internal bool Equip(WeaponState weapon, params int[] slots)
        {
            if (weapon == null || !owned.Contains(weapon)) return false;
            var previous = PlacementOf(weapon);
            WeaponPlacement next;
            try { next = SeparateHands && slots?.Length == 1 ? WeaponPlacement.ForHand(weapon, slots[0]) : new WeaponPlacement(weapon, slots); }
            catch (ArgumentException) { return false; }
            if (!CanPlace(next)) return false;
            // Validate everything before changing a binding. A failed move preserves the old layout.
            var displaced = DisplacedBy(next);
            foreach (var item in displaced) Unequip(item.Weapon);
            if (previous == null) { placements.Add(next); equipped.Add(weapon); }
            else placements[placements.IndexOf(previous)] = next;
            return true;
        }
        public bool CanPlace(WeaponPlacement placement)
        {
            if (placement == null || !owned.Contains(placement.Weapon)) return false;
            if (SeparateHands && (!placement.IsHandSlot || !HandCombatLayout.Fits(placement.Weapon, placement.Slots[0]))) return false;
            var previous = PlacementOf(placement.Weapon);
            foreach (int slot in placement.Slots)
                if (!IsAvailable(slot)) return false;
            var displaced = DisplacedBy(placement);
            foreach (var other in placements)
                if (!ReferenceEquals(other.Weapon, placement.Weapon) && other.Weapon.SameVariant(placement.Weapon) && !displaced.Contains(other)) return false;
            return equipped.Count - displaced.Count + (previous == null ? 1 : 0) <= Capacity;
        }
        public IReadOnlyList<WeaponPlacement> DisplacedBy(WeaponPlacement placement)
        {
            var result = new List<WeaponPlacement>();
            if (placement == null) return result.AsReadOnly();
            foreach (int slot in placement.Slots)
            {
                var occupant = At(slot);
                if (occupant != null && !ReferenceEquals(occupant.Weapon, placement.Weapon) && !result.Contains(occupant)) result.Add(occupant);
            }
            return result.AsReadOnly();
        }
        internal bool EquipCentered(WeaponState weapon, double center)
        {
            return TryPlacement(weapon, center, out var placement) && Equip(weapon, Copy(placement.Slots));
        }
        public bool TryPlacement(WeaponState weapon, double center, out WeaponPlacement placement)
        {
            if (!SeparateHands) return WeaponPlacement.TryCentered(weapon, center, out placement);
            placement = null;
            if (double.IsNaN(center) || double.IsInfinity(center) || center < -.5 || center >= 4.5) return false;
            int slot = (int)Math.Floor(center + .5);
            if (!HandCombatLayout.Fits(weapon, slot)) return false;
            placement = WeaponPlacement.ForHand(weapon, slot); return true;
        }
        internal bool Unequip(WeaponState weapon)
        {
            var previous = PlacementOf(weapon);
            if (previous == null) return false;
            placements.Remove(previous); equipped.Remove(weapon); return true;
        }
        internal bool Swap(WeaponState first, WeaponState second)
        {
            if (ReferenceEquals(first, second) || first == null || second == null ||
                !owned.Contains(first) || !owned.Contains(second) ||
                (SeparateHands ? HandCombatLayout.IsShield(first) != HandCombatLayout.IsShield(second) : first.RequiredLanes != second.RequiredLanes)) return false;
            var a = PlacementOf(first); var b = PlacementOf(second);
            if (a == null && b == null) return false;
            if (a != null && b != null)
            {
                placements[placements.IndexOf(a)] = SeparateHands ? WeaponPlacement.ForHand(first, b.Slots[0]) : new WeaponPlacement(first, Copy(b.Slots));
                placements[placements.IndexOf(b)] = SeparateHands ? WeaponPlacement.ForHand(second, a.Slots[0]) : new WeaponPlacement(second, Copy(a.Slots));
            }
            else
            {
                var outgoing = a ?? b; var incoming = a == null ? first : second;
                var replacement = SeparateHands ? WeaponPlacement.ForHand(incoming, outgoing.Slots[0]) : new WeaponPlacement(incoming, Copy(outgoing.Slots));
                if (!CanPlace(replacement)) return false;
                int index = placements.IndexOf(outgoing);
                placements[index] = replacement; equipped[index] = incoming;
            }
            return true;
        }
        private static int[] Copy(IReadOnlyList<int> slots)
        { var copy = new int[slots.Count]; for (int i = 0; i < copy.Length; i++) copy[i] = slots[i]; return copy; }
    }
}
