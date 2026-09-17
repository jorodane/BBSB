using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public sealed class StartingWeaponDefinition
    {
        // A stable key distinguishes two copies of the same weapon and survives list reordering.
        public string Key { get; }
        public string WeaponId { get; }
        public int? DefaultOffset { get; }
        public int RequiredLanes => WeaponCatalog.Find(WeaponId).RequiredLanes;
        public StartingWeaponDefinition(string key, string weaponId, int? defaultOffset)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A starter needs a stable key.", nameof(key));
            WeaponCatalog.Find(weaponId);
            Key = key; WeaponId = weaponId; DefaultOffset = defaultOffset;
        }
    }

    public sealed class RunCharacterDefinition
    {
        public const string DefaultId = "weapon-master";
        public static RunCharacterDefinition Default { get; } = new RunCharacterDefinition(DefaultId, new[] {
            new StartingWeaponDefinition("dagger", "dagger", 0),
            new StartingWeaponDefinition("heater-shield", "heater-shield", 1)
        });
        public string Id { get; }
        public IReadOnlyList<StartingWeaponDefinition> StartingWeapons { get; }
        public RunCharacterDefinition(string id, IEnumerable<StartingWeaponDefinition> startingWeapons)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A character needs a stable ID.", nameof(id));
            var entries = new List<StartingWeaponDefinition>(startingWeapons ?? throw new ArgumentNullException(nameof(startingWeapons)));
            if (entries.Count == 0) throw new ArgumentException("A character needs at least one starting weapon.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var defaults = new WeaponEquipment();
            foreach (var entry in entries)
            {
                if (entry == null || !keys.Add(entry.Key)) throw new ArgumentException("Starting weapon keys must be unique.");
                var weapon = new WeaponState(entry.WeaponId); defaults.Acquire(weapon);
                if (!entry.DefaultOffset.HasValue) continue;
                double center = entry.DefaultOffset.Value + (weapon.RequiredLanes - 1) * .5;
                if (!WeaponPlacement.TryCentered(weapon, center, out var placement) || !defaults.CanPlace(placement) ||
                    defaults.DisplacedBy(placement).Count != 0) throw new ArgumentException("Starting placements must fit unlocked lines and equipment capacity without overlap.");
                defaults.EquipCentered(weapon, center);
            }
            Id = id; StartingWeapons = entries.AsReadOnly();
        }
    }
}
