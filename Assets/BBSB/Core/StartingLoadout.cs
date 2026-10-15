using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    // Both the pre-run draft and an active session expose the same placement operations.
    // Each owner decides whether editing is allowed; views never manipulate ownership.
    public interface IEquipmentEditor
    {
        WeaponEquipment Equipment { get; }
        IReadOnlyList<WeaponState> OwnedWeapons { get; }
        bool CanEditEquipment { get; }
        bool EquipWeaponAtCenter(int inventoryIndex, double center);
        bool UnequipWeapon(int inventoryIndex);
    }

    [Serializable] public sealed class StartingWeaponBinding
    {
        public string key, weaponId;
        public WeaponAttribute attribute;
        public int lanes, offset;
        public bool equipped;
    }
    [Serializable] public sealed class StartingLoadoutPreset
    {
        public string characterId;
        public bool separateHands;
        public List<StartingWeaponBinding> bindings = new List<StartingWeaponBinding>();
    }

    public sealed class StartingLoadout : IEquipmentEditor
    {
        public RunCharacterDefinition Character { get; }
        public WeaponEquipment Equipment { get; }
        public IReadOnlyList<WeaponState> OwnedWeapons => Equipment.Owned;
        public bool CanEditEquipment => true;
        public bool CanStart => Equipment.Equipped.Count > 0;
        public StartingLoadout(RunCharacterDefinition character, StartingLoadoutPreset preset = null, bool separateHands = false)
        {
            Character = character ?? throw new ArgumentNullException(nameof(character));
            Equipment = new WeaponEquipment(separateHands);
            Populate(Equipment, Character, preset);
        }
        public bool EquipWeaponAtCenter(int inventoryIndex, double center) => Valid(inventoryIndex) && Equipment.EquipCentered(OwnedWeapons[inventoryIndex], center);
        public bool UnequipWeapon(int inventoryIndex) => Valid(inventoryIndex) && Equipment.Unequip(OwnedWeapons[inventoryIndex]);
        private bool Valid(int index) => index >= 0 && index < OwnedWeapons.Count;

        public StartingLoadoutPreset Capture()
        {
            var result = new StartingLoadoutPreset { characterId = Character.Id, separateHands = Equipment.SeparateHands };
            for (int i = 0; i < Character.StartingWeapons.Count; i++)
            {
                var starter = Character.StartingWeapons[i]; var placement = Equipment.PlacementOf(OwnedWeapons[i]);
                result.bindings.Add(new StartingWeaponBinding { key = starter.Key, weaponId = starter.WeaponId, attribute = starter.Attribute,
                    lanes = starter.RequiredLanes, equipped = placement != null, offset = placement?.Offset ?? 0 });
            }
            return result;
        }

        internal static void Populate(WeaponEquipment equipment, RunCharacterDefinition character, StartingLoadoutPreset preset)
        {
            equipment.Reset();
            foreach (var starter in character.StartingWeapons) equipment.Acquire(new WeaponState(starter.WeaponId, starter.Attribute));
            var saved = new Dictionary<string, StartingWeaponBinding>(StringComparer.Ordinal);
            var duplicates = new HashSet<string>(StringComparer.Ordinal);
            if (preset != null && preset.characterId == character.Id && preset.bindings != null && preset.separateHands == equipment.SeparateHands)
                foreach (var binding in preset.bindings)
                {
                    if (binding == null || string.IsNullOrWhiteSpace(binding.key)) continue;
                    if (saved.ContainsKey(binding.key)) duplicates.Add(binding.key); else saved.Add(binding.key, binding);
                }
            foreach (var key in duplicates) saved.Remove(key);
            var useDefault = new List<int>();
            // Reserve still-valid saved placements first. New/changed starter definitions
            // must not displace a player's other saved bindings when content is updated.
            for (int i = 0; i < character.StartingWeapons.Count; i++)
            {
                var starter = character.StartingWeapons[i];
                if (!saved.TryGetValue(starter.Key, out var binding) || binding.weaponId != starter.WeaponId || binding.lanes != starter.RequiredLanes || binding.attribute != starter.Attribute)
                { useDefault.Add(i); continue; }
                if (binding.equipped && !TryFreePlacement(equipment, equipment.Owned[i], binding.offset)) useDefault.Add(i);
            }
            foreach (int i in useDefault)
            {
                var starter = character.StartingWeapons[i];
                if (equipment.SeparateHands)
                {
                    if (!starter.DefaultOffset.HasValue) continue;
                    foreach (int slot in equipment.DisplayOrder)
                        if (TryFreePlacement(equipment, equipment.Owned[i], slot)) break;
                    continue;
                }
                if (!starter.DefaultOffset.HasValue || TryFreePlacement(equipment, equipment.Owned[i], starter.DefaultOffset.Value)) continue;
                for (int offset = -1; offset <= BattleInputLayout.MainLaneCount; offset++)
                    if (TryFreePlacement(equipment, equipment.Owned[i], offset)) break;
            }
        }
        private static bool TryFreePlacement(WeaponEquipment equipment, WeaponState weapon, int offset)
        {
            double center = offset + (equipment.SeparateHands ? 0 : (weapon.RequiredLanes - 1) * .5);
            return equipment.TryPlacement(weapon, center, out var placement) && equipment.CanPlace(placement) &&
                equipment.DisplacedBy(placement).Count == 0 && equipment.EquipCentered(weapon, center);
        }
    }
}
