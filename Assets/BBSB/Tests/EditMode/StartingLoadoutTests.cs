using System;
using BBSB.Core;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class StartingLoadoutTests
    {
        [Test] public void StarterLayoutRestoresSparseKeysAndCreatesNewRunItems()
        {
            var draft = new StartingLoadout(RunCharacterDefinition.Default);
            Check.True(draft.EquipWeaponAtCenter(0, 2)); Check.True(draft.EquipWeaponAtCenter(1, 4));
            var preset = draft.Capture();
            var run = new RunSession(21, useFiveLaneCombat: true, startingLoadout: preset);
            Check.Equal(RunCharacterDefinition.DefaultId, run.CharacterId);
            Check.Equal("dagger", run.Equipment.At(2).Weapon.DefinitionId);
            Check.Equal("heater-shield", run.Equipment.At(4).Weapon.DefinitionId);
            Check.True(run.Equipment.At(0) == null && run.Equipment.At(1) == null);
            Check.False(ReferenceEquals(draft.OwnedWeapons[0], run.OwnedWeapons[0]));
            // Changing either draft data or live equipment cannot rewrite the run's initial layout.
            preset.bindings[0].offset = 3; draft.EquipWeaponAtCenter(0, 0);
            var firstItem = run.OwnedWeapons[0]; firstItem.Level = 3;
            run.Equipment.Acquire(new WeaponState("staff")); run.Equipment.IncreaseCapacity();
            run.UnlockInputExtensions(InputExtensions.Left | InputExtensions.Right); run.EquipWeaponAtCenter(0, 3);
            run.Abandon(); run.Restart(22);
            Check.Equal(2, run.OwnedWeapons.Count); Check.Equal(2, run.Equipment.Capacity);
            Check.Equal(InputExtensions.None, run.Equipment.Extensions);
            Check.Equal(0, run.Equipment.At(2).Weapon.Level);
            Check.False(ReferenceEquals(firstItem, run.Equipment.At(2).Weapon));
            Check.Equal(0, run.ClearedStages); Check.Equal(100m, run.Health); Check.Equal(60, run.Gold);
        }
        [Test] public void UnequippedStarterIsRememberedWithoutRemovingOwnership()
        {
            var draft = new StartingLoadout(RunCharacterDefinition.Default);
            Check.True(draft.UnequipWeapon(1));
            var restored = new StartingLoadout(RunCharacterDefinition.Default, draft.Capture());
            Check.Equal(2, restored.OwnedWeapons.Count); Check.Equal(1, restored.Equipment.Equipped.Count);
            Check.True(restored.Equipment.PlacementOf(restored.OwnedWeapons[1]) == null);
            Check.True(restored.UnequipWeapon(0)); Check.False(restored.CanStart);
            Check.True(restored.EquipWeaponAtCenter(1, 4)); Check.True(restored.CanStart);
        }
        [Test] public void StableStarterKeysSurviveReorderingAndDuplicateWeaponTypes()
        {
            var left = new StartingWeaponDefinition("first", "dagger", 0);
            var right = new StartingWeaponDefinition("second", "dagger", 1);
            var character = new RunCharacterDefinition("twins", new[] { left, right });
            var draft = new StartingLoadout(character); draft.EquipWeaponAtCenter(0, 4); draft.EquipWeaponAtCenter(1, 2);
            var reordered = new RunCharacterDefinition("twins", new[] { right, left });
            var restored = new StartingLoadout(reordered, draft.Capture());
            Check.True(ReferenceEquals(restored.OwnedWeapons[0], restored.Equipment.At(2).Weapon));
            Check.True(ReferenceEquals(restored.OwnedWeapons[1], restored.Equipment.At(4).Weapon));
        }
        [Test] public void MultiLineStarterRestoresItsWholeFootprintAndCharacterIdentity()
        {
            var character = new RunCharacterDefinition("staff-master", new[] {
                new StartingWeaponDefinition("main", "staff", 0), new StartingWeaponDefinition("support", "spirit-bell", 2) });
            var draft = new StartingLoadout(character); draft.EquipWeaponAtCenter(0, 3.5); draft.EquipWeaponAtCenter(1, 2);
            var run = new RunSession(20, useFiveLaneCombat: true, character: character, startingLoadout: draft.Capture());
            Check.Equal("staff-master", run.CharacterId); Check.Equal(2, run.Weapons.Count);
            Check.Equal(3, run.Equipment.OccupiedLaneCount);
            Check.True(ReferenceEquals(run.Equipment.At(3), run.Equipment.At(4)));
            Check.Equal(0, run.Equipment.At(3).OffsetOf(3)); Check.Equal(1, run.Equipment.At(4).OffsetOf(4));
            Check.Equal("spirit-bell", run.Equipment.At(2).Weapon.DefinitionId);
        }
        [Test] public void InvalidSavedPositionsCannotUnlockLinesOrOverwriteAnotherBinding()
        {
            foreach (int invalid in new[] { -1, 5, int.MinValue, int.MaxValue, 4 })
            {
                var preset = new StartingLoadout(RunCharacterDefinition.Default).Capture();
                preset.bindings[0].offset = 4; preset.bindings[1].offset = invalid;
                var draft = new StartingLoadout(RunCharacterDefinition.Default, preset);
                Check.Equal("dagger", draft.Equipment.At(4).Weapon.DefinitionId);
                Check.Equal("heater-shield", draft.Equipment.At(1).Weapon.DefinitionId);
                Check.Equal(InputExtensions.None, draft.Equipment.Extensions);
                Check.Equal(2, draft.Equipment.Equipped.Count);
            }
        }
        [Test] public void ChangedStarterUsesDefaultsWithoutDisturbingAnUnchangedSavedWeapon()
        {
            var draft = new StartingLoadout(RunCharacterDefinition.Default); draft.EquipWeaponAtCenter(1, 4);
            var updated = new RunCharacterDefinition(RunCharacterDefinition.DefaultId, new[] {
                new StartingWeaponDefinition("dagger", "staff", 0), new StartingWeaponDefinition("heater-shield", "heater-shield", 2) });
            var restored = new StartingLoadout(updated, draft.Capture());
            Check.Equal("staff", restored.Equipment.At(0).Weapon.DefinitionId);
            Check.True(ReferenceEquals(restored.Equipment.At(0), restored.Equipment.At(1)));
            Check.Equal("heater-shield", restored.Equipment.At(4).Weapon.DefinitionId);
        }
        [Test] public void ForeignNullAndDuplicateSaveEntriesRecoverFromCurrentStarterDefinitions()
        {
            var preset = new StartingLoadout(RunCharacterDefinition.Default).Capture();
            preset.bindings[0].offset = 4; preset.bindings[1].offset = 2;
            preset.bindings.Add(null); preset.bindings.Add(preset.bindings[0]);
            preset.bindings.Add(new StartingWeaponBinding { key = "loot", weaponId = "staff", equipped = true, lanes = 2, offset = 3 });
            var recovered = new StartingLoadout(RunCharacterDefinition.Default, preset);
            Check.Equal(2, recovered.OwnedWeapons.Count); Check.Equal("dagger", recovered.Equipment.At(0).Weapon.DefinitionId);
            Check.Equal("heater-shield", recovered.Equipment.At(2).Weapon.DefinitionId);
            preset.characterId = "deleted-character";
            recovered = new StartingLoadout(RunCharacterDefinition.Default, preset);
            Check.Equal("heater-shield", recovered.Equipment.At(1).Weapon.DefinitionId);
            preset.bindings = null;
            Check.True(new StartingLoadout(RunCharacterDefinition.Default, preset).CanStart);
        }
        [Test] public void CharacterDefinitionsRejectAmbiguousKeysAndInvalidDefaultFootprints()
        {
            bool duplicate = false, locked = false;
            try { new RunCharacterDefinition("bad", new[] { new StartingWeaponDefinition("same", "dagger", 0), new StartingWeaponDefinition("same", "bow", 1) }); }
            catch (ArgumentException) { duplicate = true; }
            try { new RunCharacterDefinition("bad", new[] { new StartingWeaponDefinition("staff", "staff", 4) }); }
            catch (ArgumentException) { locked = true; }
            Check.True(duplicate && locked);
        }
    }
}
