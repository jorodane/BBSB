using System;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class WeaponEquipmentTests
    {
        private static WeaponState Multi(int lines = 2) => new WeaponState("greatsword", WeaponRarity.Epic, 2, lines);
        private static void Tap(FiveLaneBattle battle, int slot, double beat) { battle.Press(slot, beat); battle.Release(slot, beat); }
        private static FiveLaneBattle Battle(WeaponState weapon, int[] slots, WeaponPhraseSet patterns = null,
            InputExtensions extensions = InputExtensions.None, params BeatAttack[] attacks) => new FiveLaneBattle(
                new[] { weapon }, 120, 32, attacks, new StageHealth(10000), 100, 100,
                placements: new[] { new WeaponPlacement(weapon, slots) },
                phraseSets: new[] { patterns ?? ShortWeaponPhrases.Set(weapon) }, extensions: extensions);

        [Test] public void DaggerAndDualSwordsKeepDifferentCadencesAndTheSameOpeningGrace()
        {
            foreach (var id in new[] { "dagger", "dual-swords" })
            foreach (double first in new[] { .18, .42 })
            {
                double interval = id == "dagger" ? 2 : 1, start = first < .25 ? 0 : .5;
                var battle = Battle(new WeaponState(id, WeaponAttribute.Dual), new[] { 4 });
                Tap(battle, 4, first); Check.Equal(6m, battle.TotalDamage); Check.Equal(0, battle.MissCount);
                Check.Equal(start + interval, battle.LaneAt(4).NextBeat);
                Tap(battle, 4, start + interval);
                Check.Equal(start + 2 * interval, battle.LaneAt(4).NextBeat);
                battle.Advance(battle.LaneAt(4).NextBeat + .25);
                Check.Equal(1, battle.MissCount);
                Check.Equal(2.0, battle.LaneAt(4).ReadyAtBeat - battle.LaneAt(4).LastJudgedBeat);
            }
        }
        [Test] public void TwoItemsCanUseSpaceAndKWithoutCompactingEmptyPositions()
        {
            var run = new RunSession(31, useFiveLaneCombat: true);
            Check.True(run.EquipWeapon(0, 2)); Check.True(run.EquipWeapon(1, 4));
            Check.Equal(2, run.Equipment.Capacity); Check.Equal(2, run.Weapons.Count);
            Check.True(run.Equipment.At(0) == null && run.Equipment.At(1) == null && run.Equipment.At(3) == null);
            run.Enter(run.Map.Nodes.First(n => run.CanEnter(n.Id)).Id);
            var battle = run.StartFiveLaneBattle();
            Check.Equal(2, battle.Lanes.Count); Check.True(battle.LaneAt(0) == null);
            Tap(battle, 0, 0); Check.Equal(0, battle.PerfectCount);
            Tap(battle, 2, 0); Check.Equal(6m, battle.TotalDamage);
            battle.Press(4, 0); Check.True(battle.LaneAt(4).Holding); Check.Equal(4, battle.LaneAt(4).HoldingSlot);
            Check.Equal(2, battle.LaneAt(2).Slot);
        }
        [Test] public void MovementAndUnequipOnlyChangePlacementAndRetainTheSameItemReference()
        {
            var run = new RunSession(31, useFiveLaneCombat: true);
            var weapon = run.OwnedWeapons[0]; weapon.Level = 3;
            for (int slot = 4; slot >= 0; slot--)
            {
                Check.True(run.EquipWeapon(0, slot));
                Check.True(ReferenceEquals(weapon, run.Equipment.At(slot).Weapon));
                Check.Equal(slot, run.Equipment.At(slot).Offset); Check.Equal(2, run.OwnedWeapons.Count);
            }
            Check.True(run.UnequipWeapon(0)); Check.True(run.Equipment.PlacementOf(weapon) == null);
            Check.True(ReferenceEquals(weapon, run.OwnedWeapons[0])); Check.Equal(3, weapon.Level);
            Check.True(run.EquipWeaponAtCenter(0, 4)); Check.True(ReferenceEquals(weapon, run.Equipment.At(4).Weapon));
        }
        [Test] public void OverlappingDropUnplacesEveryConflictingItemAtomically()
        {
            var run = new RunSession(31, useFiveLaneCombat: true);
            var first = run.OwnedWeapons[0]; var second = run.OwnedWeapons[1];
            first.Level = 2; second.Level = 3;
            var wide = Multi(); run.Equipment.Acquire(wide);
            Check.True(run.EquipWeaponAtCenter(2, .5)); // D/F replaces both starter bindings.
            Check.Equal(1, run.Weapons.Count); Check.Equal(2, run.Equipment.OccupiedLaneCount);
            Check.Equal(3, run.OwnedWeapons.Count);
            Check.True(run.Equipment.PlacementOf(first) == null && run.Equipment.PlacementOf(second) == null);
            Check.True(ReferenceEquals(first, run.OwnedWeapons[0]) && ReferenceEquals(second, run.OwnedWeapons[1]));
            Check.Equal(2, first.Level); Check.Equal(3, second.Level);
            Check.True(ReferenceEquals(wide, run.Equipment.At(0).Weapon));
            Check.True(ReferenceEquals(run.Equipment.At(0), run.Equipment.At(1)));
            // Covering only one end still unplaces the old footprint in full.
            Check.True(run.EquipWeapon(0, 1)); Check.True(run.Equipment.At(0) == null);
            Check.True(run.Equipment.PlacementOf(wide) == null); Check.Equal(3, run.OwnedWeapons.Count);
        }
        [Test] public void MultiLineItemsConsumeOneEquipmentCapacityRegardlessOfTheirWidth()
        {
            var run = new RunSession(31, useFiveLaneCombat: true);
            Check.True(run.UnequipWeapon(0)); Check.True(run.UnequipWeapon(1));
            var wide = Multi(3); run.Equipment.Acquire(wide);
            Check.True(run.EquipWeaponAtCenter(2, 1)); // D/F/Space, one item.
            Check.True(run.EquipWeapon(0, 4)); Check.Equal(2, run.Weapons.Count);
            Check.Equal(4, run.Equipment.OccupiedLaneCount); Check.Equal(2, run.Equipment.Capacity);
            Check.False(run.EquipWeapon(1, 3)); // Free J, but no third equipment capacity.
            Check.True(run.EquipWeapon(1, 4)); // Replaces the item on K even while at capacity.
            Check.Equal(2, run.Weapons.Count); Check.True(ReferenceEquals(run.OwnedWeapons[1], run.Equipment.At(4).Weapon));
        }
        [Test] public void CenterSnappingUsesTheWholeContiguousFootprintForOddAndEvenWidths()
        {
            foreach (int width in new[] { 1, 2, 3, 4, 5, 6, 7 })
            {
                var weapon = Multi(width);
                for (int start = -1; start + width - 1 <= 5; start++)
                {
                    if (width == 1 && (start == -1 || start == 5)) continue;
                    double center = start + (width - 1) * .5;
                    foreach (double error in new[] { -.49, 0, .49 })
                    {
                        Check.True(WeaponPlacement.TryCentered(weapon, center + error, out var p));
                        Check.Equal(start, p.Offset); Check.Equal(center, p.Center);
                        Check.Equal(width, p.Slots.Count);
                        for (int i = 0; i < width; i++) Check.Equal(start + i, BattleInputLayout.Position(p.Slots[i]));
                    }
                }
            }
            Check.True(WeaponPlacement.TryCentered(Multi(), 3.5, out var jk)); Check.Equal("3,4", string.Join(",", jk.Slots));
            Check.True(WeaponPlacement.TryCentered(Multi(), 4.5, out var kl)); Check.Equal("4,6", string.Join(",", kl.Slots));
            Check.True(WeaponPlacement.TryCentered(Multi(), -.5, out var sd)); Check.Equal("5,0", string.Join(",", sd.Slots));
            bool rejected = false; try { new WeaponPlacement(Multi(), 0, 4); } catch (ArgumentException) { rejected = true; }
            Check.True(rejected);
        }
        [Test] public void LockedExtensionsAndOutOfBoundsDropsNeverDisplaceExistingBindings()
        {
            var run = new RunSession(31, useFiveLaneCombat: true); var wide = Multi(); run.Equipment.Acquire(wide);
            Check.True(run.EquipWeapon(1, 4)); var before = run.Equipment.At(4);
            Check.False(run.EquipWeaponAtCenter(2, 4.5)); Check.True(ReferenceEquals(before, run.Equipment.At(4)));
            Check.False(run.EquipWeaponAtCenter(2, -.5)); Check.Equal(2, run.Weapons.Count);
            foreach (double center in new[] { -2, 6, double.NaN, double.PositiveInfinity, double.MaxValue })
            { Check.False(run.EquipWeaponAtCenter(2, center)); Check.True(ReferenceEquals(before, run.Equipment.At(4))); }
            Check.True(run.UnlockInputExtensions(InputExtensions.Right));
            Check.False(run.EquipWeaponAtCenter(2, -.5)); // Right unlock cannot open S.
            Check.True(run.EquipWeaponAtCenter(2, 4.5)); Check.Equal(2, run.Weapons.Count);
            Check.True(ReferenceEquals(wide, run.Equipment.At(4).Weapon)); Check.True(run.Equipment.PlacementOf(run.OwnedWeapons[1]) == null);
        }
        [Test] public void ExtensionsNeverAcceptSingleLineItemsEvenWhenBothAreUnlocked()
        {
            var run = new RunSession(31, useFiveLaneCombat: true);
            Check.True(run.UnlockInputExtensions(InputExtensions.Left | InputExtensions.Right));
            Check.False(run.EquipWeaponAtCenter(0, -1)); Check.False(run.EquipWeaponAtCenter(0, 5));
            Check.False(run.EquipWeapon(0, BattleInputLayout.Left)); Check.False(run.EquipWeapon(1, BattleInputLayout.Right));
            Check.Equal(2, run.Weapons.Count); Check.Equal(7, run.Equipment.AvailableLaneCount);
            Check.Equal(2, run.Equipment.Capacity);
        }
        [Test] public void OnlyBossVictoriesIncreaseCapacityOnceAndItStopsAtFive()
        {
            var run = new RunSession(31, useFiveLaneCombat: true); int bosses = 0;
            for (int stage = 0; stage < 30; stage++)
            {
                if (run.Phase == RunPhase.FieldCleared) Check.True(run.AdvanceField());
                var node = run.Map.Nodes.First(n => run.CanEnter(n.Id));
                Check.True(run.Enter(node.Id)); int before = run.Equipment.Capacity;
                Check.False(run.EquipmentCapacityIncreased);
                if (node.IsBattle)
                {
                    string ticket = run.StageTicket; Check.True(run.ResolveBattle(ticket, true, 100));
                    if (node.Kind == StageKind.Boss) bosses++;
                    Check.Equal(Math.Min(5, 2 + bosses), run.Equipment.Capacity);
                    Check.Equal(node.Kind == StageKind.Boss && before < 5, run.EquipmentCapacityIncreased);
                    Check.False(run.ResolveBattle(ticket, true, 100)); Check.True(run.SkipReward());
                }
                else { Check.True(run.LeaveService()); Check.Equal(before, run.Equipment.Capacity); }
                Check.Equal(2, run.Weapons.Count); Check.Equal(InputExtensions.None, run.Equipment.Extensions);
            }
            Check.Equal(5, bosses); Check.Equal(5, run.Equipment.Capacity);
            run.Restart(31); Check.Equal(2, run.Equipment.Capacity);
        }
        [Test] public void BattleInProgressLocksPlacementEvenWhenPausedAndZeroEquipmentCannotStart()
        {
            var run = new RunSession(31, useFiveLaneCombat: true);
            run.Enter(run.Map.Nodes.First(n => run.CanEnter(n.Id)).Id);
            Check.True(run.UnequipWeapon(0)); Check.True(run.UnequipWeapon(1)); Check.True(run.StartFiveLaneBattle() == null);
            Check.True(run.EquipWeapon(0, 4)); var battle = run.StartFiveLaneBattle(); battle.Pause();
            Check.False(run.CanEditEquipment); Check.False(run.EquipWeapon(0, 0)); Check.False(run.UnequipWeapon(0));
            Check.False(run.UnlockInputExtensions(InputExtensions.Left)); Check.True(ReferenceEquals(battle, run.StartFiveLaneBattle()));
            Check.Equal(4, battle.Lanes[0].Slot);
            Check.True(run.ResolveBattle(run.StageTicket, true, 100)); Check.True(run.CanEditEquipment);
            Check.True(run.EquipWeapon(0, 0));
        }
        [Test] public void StartingOffsetChoosesDifferentPatternsEffectsAndSharedCooldown()
        {
            var weapon = new WeaponState("sword", WeaponRarity.Common, requiredLanes: 2);
            var slash = new WeaponPhrase("sword", "slash", "", 1, new[] { new WeaponPhraseNote(0, 7) }, repeat: false);
            var guard = new WeaponPhrase("sword", "guard", "", 2, new[] { new WeaponPhraseNote(0, 0, 2, PhraseEffect.Parry) },
                repeat: false, holdDamageReduction: .5m, releaseEndsPhrase: true, completionCooldownBeats: 2);
            var b = Battle(weapon, new[] { 3, 4 }, new WeaponPhraseSet(weapon, new[] { slash, guard }), InputExtensions.None,
                new BeatAttack("a", 1, 10), new BeatAttack("b", 2, 10));
            Check.Equal(1, b.Lanes.Count); Check.True(ReferenceEquals(b.LaneAt(3), b.LaneAt(4)));
            Tap(b, 3, 0); Check.Equal(7m, b.TotalDamage); Check.Equal(0, b.Lanes[0].StartOffset);
            Tap(b, 4, .5); Check.Equal(7m, b.TotalDamage); Check.Equal("slash", b.Lanes[0].Phrase.Name);
            b.Press(4, 1); Check.Equal(1, b.Lanes[0].StartOffset); Check.Equal("guard", b.Lanes[0].Phrase.Name);
            Check.Equal(10m, b.TotalBlocked); Check.True(b.Lanes[0].Holding);
            b.Press(3, 1.5); b.Release(3, 1.5); Check.True(b.Lanes[0].Holding); // The other contact cannot release K.
            b.Advance(2.3); Check.Equal(95m, b.PlayerHealth); Check.Equal(5m, b.TotalReduced);
            b.Release(4, 2.4); Check.False(b.Lanes[0].Holding); Check.Equal(4.4, b.Lanes[0].ReadyAtBeat);
        }
        [Test] public void MultiLineNotesAndForecastsUseOffsetsRelativeToTheStartingInput()
        {
            var weapon = new WeaponState("sword", WeaponRarity.Common, requiredLanes: 2);
            var phrase = new WeaponPhrase("sword", "alternate", "", 2,
                new[] { new WeaponPhraseNote(0, 3), new WeaponPhraseNote(1, 5, laneOffset: 1) }, repeat: true);
            foreach (int start in new[] { 3, 4 })
            {
                var b = Battle(weapon, new[] { 3, 4 }, WeaponPhraseSet.Uniform(weapon, phrase));
                Tap(b, start, .18); var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
                int other = start == 3 ? 4 : 3;
                Check.Equal(other, timeline.Notes.Single(n => n.Beat == 1).Slot);
                Check.Equal(start, timeline.Notes.Single(n => n.Beat == 2).Slot);
                Check.True(timeline.Notes.Single(n => n.Beat == 2).IsPreview);
                Tap(b, start, 1); Check.Equal(3m, b.TotalDamage); // Cannot hit the note on the other line.
                Tap(b, other, 1); Check.Equal(8m, b.TotalDamage); Check.Equal(0, b.MissCount);
                Tap(b, start, 2); Check.Equal(11m, b.TotalDamage); Check.Equal(start == 3 ? 0 : 1, b.Lanes[0].StartOffset);
            }
        }
        [Test] public void ExtensionStartingOffsetsFollowPhysicalOrderAndCanDriveDistinctEffects()
        {
            var weapon = new WeaponState("sword", WeaponRarity.Common, requiredLanes: 2);
            var left = new WeaponPhrase("sword", "left", "", 1, new[] { new WeaponPhraseNote(0, 2) }, repeat: false);
            var right = new WeaponPhrase("sword", "right", "", 1, new[] { new WeaponPhraseNote(0, 9) }, repeat: false);
            var set = new WeaponPhraseSet(weapon, new[] { left, right });
            foreach (var slots in new[] { new[] { 0, 5 }, new[] { 4, 6 } })
            {
                var b = Battle(weapon, slots, set, InputExtensions.Left | InputExtensions.Right);
                var placement = b.Lanes[0].Placement;
                Tap(b, placement.Slots[0], 0); Check.Equal(2m, b.TotalDamage);
                Tap(b, placement.Slots[1], 1); Check.Equal(11m, b.TotalDamage); Check.Equal(1, b.Lanes[0].StartOffset);
            }
        }
        [Test] public void DaggerForecastUsesTwoBeatSpacingWithinTheThreeBeatHorizon()
        {
            var b = Battle(new WeaponState("dagger"), new[] { 4 }); Tap(b, 4, 0);
            var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
            Check.Equal(1, timeline.Notes.Count); Check.Equal(2.0, timeline.Notes[0].Beat); Check.Equal(4, timeline.Notes[0].Slot);
            b.Advance(1); timeline.Refresh(b); Check.Equal(2, timeline.Notes.Count);
            Check.True(timeline.Notes.Single(n => n.Beat == 4).IsPreview);
            Tap(b, 4, 2); timeline.Refresh(b); Check.False(timeline.Notes.Single(n => n.Beat == 4).IsPreview);
            Check.Equal(0, timeline.Broken.Count);
        }
        [Test] public void DuplicateInventoryItemsRemainDistinctAcrossReplacementAndRestart()
        {
            var run = new RunSession(31, useFiveLaneCombat: true);
            var extra = new WeaponState("dagger", WeaponRarity.Legendary, 3); run.Equipment.Acquire(extra);
            Check.True(run.EquipWeapon(2, 0)); Check.Equal(3, run.OwnedWeapons.Count);
            Check.True(ReferenceEquals(extra, run.Equipment.At(0).Weapon)); Check.Equal(0, run.OwnedWeapons[0].Level);
            Check.True(run.SwapWeapons(0, 2)); Check.True(ReferenceEquals(run.OwnedWeapons[0], run.Equipment.At(0).Weapon));
            Check.Equal(3, extra.Level); Check.Equal(WeaponRarity.Legendary, extra.Rarity);
            run.Abandon(); Check.Equal(0, run.OwnedWeapons.Count); Check.Equal(0, run.Equipment.Placements.Count);
            run.Restart(31); Check.Equal(2, run.OwnedWeapons.Count); Check.False(run.OwnedWeapons.Contains(extra));
        }
        [Test] public void ShopAcquisitionAndUnequippedUpgradesDoNotRequireOrChangeBindings()
        {
            var run = new RunSession(31, new RunRules(startingGold: 10000), useFiveLaneCombat: true);
            bool bought = false, upgraded = false;
            for (int step = 0; step < 90 && !(bought && upgraded); step++)
            {
                if (run.Phase == RunPhase.FieldCleared) run.AdvanceField();
                var node = run.Map.Nodes.Where(n => run.CanEnter(n.Id))
                    .OrderByDescending(n => !bought && n.Kind == StageKind.Shop || !upgraded && n.Kind == StageKind.Upgrade).First();
                Check.True(run.Enter(node.Id));
                if (node.IsBattle) { run.ResolveBattle(run.StageTicket, true, 100); run.SkipReward(); continue; }
                if (node.Kind == StageKind.Shop && !bought)
                {
                    int count = run.OwnedWeapons.Count, gold = run.Gold;
                    var bindings = run.Equipment.Placements.ToArray();
                    int index = run.Offers.ToList().FindIndex(o => o.Content.Kind == RewardKind.Weapon);
                    int price = run.Offers[index].Price; var attribute = run.Offers[index].Attribute;
                    Check.True(run.Buy(index)); Check.Equal(count + 1, run.OwnedWeapons.Count);
                    Check.Equal(attribute, run.OwnedWeapons[count].Attribute);
                    Check.Equal(gold - price, run.Gold); Check.False(run.Buy(index));
                    Check.True(bindings.SequenceEqual(run.Equipment.Placements)); bought = true;
                }
                if (node.Kind == StageKind.Upgrade && !upgraded)
                {
                    var weapon = run.OwnedWeapons[0]; int level = weapon.Level;
                    Check.True(run.UnequipWeapon(0)); Check.True(run.Upgrade(0));
                    Check.True(run.Equipment.PlacementOf(weapon) == null); Check.Equal(level + 1, weapon.Level);
                    Check.False(run.Upgrade(0)); Check.True(run.EquipWeapon(0, 4));
                    Check.True(ReferenceEquals(weapon, run.Equipment.At(4).Weapon)); upgraded = true;
                }
                Check.True(run.LeaveService());
            }
            Check.True(bought && upgraded);
        }
        [Test] public void PatternAndBattleValidationRejectInvalidOffsetsAndLockedInputExtensions()
        {
            var weapon = Multi();
            var bad = new WeaponPhrase("greatsword", "outside", "", 2,
                new[] { new WeaponPhraseNote(0, 1), new WeaponPhraseNote(1, 1, laneOffset: 2) });
            bool rejected = false;
            try { WeaponPhraseSet.Uniform(weapon, bad); } catch (ArgumentException) { rejected = true; }
            Check.True(rejected); rejected = false;
            try { Battle(weapon, new[] { 4, 6 }); } catch (ArgumentException) { rejected = true; }
            Check.True(rejected); rejected = false;
            try { new WeaponPhrase("greatsword", "wrong start", "", 1, new[] { new WeaponPhraseNote(0, 1, laneOffset: 1) }); }
            catch (ArgumentException) { rejected = true; }
            Check.True(rejected);
        }
    }
}
