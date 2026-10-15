using System;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class HandCombatTests
    {
        private static FiveLaneBattle Battle(string[] ids = null, BeatAttack[] attacks = null, WeaponPhrase[] phrases = null) =>
            new FiveLaneBattle((ids ?? new[] { "dagger", "heater-shield" }).Select(id => new WeaponState(id)).ToArray(),
                120, 64, attacks ?? Array.Empty<BeatAttack>(), new StageHealth(10000), 100, 100,
                phrases: phrases, separateHands: true);
        private static void Tap(FiveLaneBattle battle, int slot, double beat)
        { battle.Press(slot, beat); battle.Release(slot, beat); }

        [Test] public void EquipmentHasFourAttackSlotsAndOneShieldWithNoFootprintCompetition()
        {
            var equipment = new WeaponEquipment(true);
            var ids = new[] { "staff", "greatsword", "bow", "crossbow", "wide-shield" };
            for (int i = 0; i < ids.Length; i++)
            {
                var weapon = new WeaponState(ids[i]); equipment.Acquire(weapon);
                Check.True(equipment.Equip(weapon, i)); Check.Equal(1, equipment.PlacementOf(weapon).Slots.Count);
            }
            Check.Equal(5, equipment.Equipped.Count); Check.Equal(5, equipment.Capacity);
            Check.True(!equipment.Equip(equipment.Owned[0], 4));
            Check.True(!equipment.Equip(equipment.Owned[4], 0));
            Check.True(!equipment.Equip(equipment.Owned[4], 5));
            Check.True(!equipment.IncreaseCapacity());
            Check.True(equipment.Swap(equipment.Owned[0], equipment.Owned[2]));
            Check.True(!equipment.Swap(equipment.Owned[0], equipment.Owned[4]));
        }

        [Test] public void DefaultStarterInventoryIsPreservedAndOldLayoutDoesNotBecomeWrongHandBindings()
        {
            var old = new StartingLoadout(RunCharacterDefinition.Default).Capture();
            var draft = new StartingLoadout(RunCharacterDefinition.Default, old, true);
            Check.Equal(2, draft.OwnedWeapons.Count);
            Check.Equal("dagger", draft.Equipment.At(0).Weapon.DefinitionId);
            Check.Equal("heater-shield", draft.Equipment.At(4).Weapon.DefinitionId);
            Check.True(draft.EquipWeaponAtCenter(0, 3));
            var saved = draft.Capture(); Check.True(saved.separateHands);
            var restored = new StartingLoadout(RunCharacterDefinition.Default, saved, true);
            Check.Equal("dagger", restored.Equipment.At(3).Weapon.DefinitionId);
            Check.True(restored.Equipment.At(0) == null);
        }

        [Test] public void FourDirectionsShareOneEquippedItemButKeepIndependentHeldStates()
        {
            var battle = Battle();
            Check.Equal(2, battle.Lanes.Count); Check.Equal(5, battle.InputLanes.Count);
            for (int slot = 4; slot < 8; slot++)
            {
                Check.Equal("heater-shield", battle.LaneAt(slot).Weapon.DefinitionId);
                battle.Press(slot, 0);
            }
            Check.Equal(4, battle.RequiredHeldSlots.Count());
            battle.Release(5, .1);
            Check.True(!battle.LaneAt(5).Holding);
            Check.True(battle.LaneAt(4).Holding && battle.LaneAt(6).Holding && battle.LaneAt(7).Holding);
            Check.True(battle.VisualLane(battle.Lanes[1]).Holding);
        }

        [Test] public void OffGridCallDoesNotJudgeOrDealDamageAndHoldingCallCannotPlayTheResponse()
        {
            var battle = Battle(); battle.Press(0, .37);
            double first = battle.LaneAt(0).NextBeat;
            Check.Equal(0m, battle.TotalDamage); Check.Equal(0, battle.PerfectCount); Check.Equal(0, battle.MissCount);
            Check.True(first >= 1.37); Check.Equal("CALL", battle.LaneAt(0).Feedback);
            battle.Press(0, first); Check.Equal(0m, battle.TotalDamage);
            battle.Advance(first + battle.HalfMissWindow + .01); Check.Equal(1, battle.MissCount);
        }

        [Test] public void ZeroDelayAuthoredAttackStillUsesASeparateCall()
        {
            var phrase = new WeaponPhrase("dagger", "Immediate configuration", "", 2, new[] { new WeaponPhraseNote(0, 10) });
            var battle = Battle(new[] { "dagger" }, phrases: new[] { phrase });
            Tap(battle, 0, .4); Check.Equal(0m, battle.TotalDamage); Check.True(battle.LaneAt(0).NextBeat >= .4);
            Tap(battle, 0, battle.LaneAt(0).NextBeat); Check.Equal(10m, battle.TotalDamage);
        }

        [Test] public void WrongArrowAndAttackKeyCannotParryAnotherDefenseDirection()
        {
            var battle = Battle(attacks: new[] { new BeatAttack("enemy", 1, 10, defenseDirection: 3) });
            Tap(battle, 0, 0); battle.Press(4, 1); Tap(battle, 0, 1); battle.Advance(1.25);
            Check.Equal(90m, battle.PlayerHealth); Check.Equal(0m, battle.TotalBlocked);
            Check.True(battle.TotalDamage > 0);
        }

        [Test] public void AttackAndDefenseAtTheSameBeatResolveIndependentlyInEitherInputOrder()
        {
            foreach (bool defenseFirst in new[] { true, false })
            {
                var battle = Battle(attacks: new[] { new BeatAttack("enemy", 1, 10, defenseDirection: 2) });
                Tap(battle, 0, 0);
                if (defenseFirst) battle.Press(6, 1);
                Tap(battle, 0, 1);
                if (!defenseFirst) battle.Press(6, 1);
                battle.Advance(1.25);
                Check.Equal(100m, battle.PlayerHealth); Check.Equal(10m, battle.TotalBlocked);
                Check.Equal(6m, battle.TotalDamage); Check.Equal(3d, battle.LaneAt(0).NextBeat);
            }
        }

        [Test] public void MultipleAttacksAndAllFourDefenseContactsCanBePressedTogether()
        {
            var battle = Battle(new[] { "dagger", "dual-swords", "sword", "hammer", "heater-shield" },
                Enumerable.Range(0, 4).Select(d => new BeatAttack("e" + d, 1, 5, defenseDirection: d)).ToArray());
            for (int slot = 0; slot < 4; slot++) Tap(battle, slot, 0);
            for (int slot = 0; slot < 8; slot++) battle.Press(slot, 1);
            battle.Advance(1.25);
            Check.Equal(100m, battle.PlayerHealth); Check.Equal(20m, battle.TotalBlocked);
            for (int slot = 0; slot < 4; slot++) Check.True(battle.LaneAt(slot).Activations > 0);
        }

        [Test] public void DefenseNotesAreVisibleOnAllFourDirectionsWithoutAWeaponNoteHighway()
        {
            var battle = Battle(attacks: Enumerable.Range(0, 4).Select(d => new BeatAttack("e" + d, 1 + d * .5, 4)).ToArray());
            Tap(battle, 0, .13); var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(battle);
            Check.Equal(0, timeline.Notes.Count); Check.Equal(0, timeline.Broken.Count);
            for (int d = 0; d < 4; d++)
            {
                var attack = battle.Incoming[d]; Check.Equal(4 + d, attack.ShieldSlot);
                Check.True(EnemyAttackNotes.Visible(battle, attack, 4 + d));
                Check.True(!EnemyAttackNotes.Visible(battle, attack, d));
            }
            Check.True(!double.IsInfinity(WeaponCueTimeline.Target(battle.LaneAt(0))));
        }

        [Test] public void RemovingTheShieldDoesNotRemoveIncomingDefenseNotes()
        {
            var battle = Battle(new[] { "dagger" }, new[] { new BeatAttack("e", 1, 7, defenseDirection: 1) });
            Check.True(EnemyAttackNotes.Visible(battle, battle.Incoming[0], 5));
            battle.Press(5, 1); battle.Advance(1.25); Check.Equal(93m, battle.PlayerHealth);
        }

        [Test] public void ContinuousEnemyHoldKeepsItsDirectionAndDamageBudget()
        {
            var battle = Battle(attacks: new[] { new BeatAttack("e", 1, 8, 1), new BeatAttack("e", 2, 8, 1) });
            Check.Equal(battle.Incoming[0].ShieldSlot, battle.Incoming[1].ShieldSlot);
            battle.Press(4, 1); battle.Advance(3.25);
            Check.Equal(96m, battle.PlayerHealth); Check.Equal(8m, battle.TotalBlocked); Check.Equal(4m, battle.TotalReduced);
        }

        [Test] public void ShieldInputDoesNotTriggerTheFormerCounterattackCombo()
        {
            var battle = Battle(new[] { "dagger", "shield" }, new[] { new BeatAttack("e", 1, 10, defenseDirection: 0) });
            Tap(battle, 4, 1); Tap(battle, 4, 2); Tap(battle, 4, 2.5);
            Check.Equal(0m, battle.TotalDamage); Check.Equal(1, battle.LaneAt(4).Phrase.Notes.Count);
            Check.True(WeaponPhraseCatalog.Find("shield").Notes.Count > 1);
        }

        [Test] public void MultiLineWeaponKeepsItsAuthoredRhythmOnItsOwnAttackKey()
        {
            var battle = Battle(new[] { "staff", "heater-shield" }); var lane = battle.LaneAt(0);
            Check.True(lane.Weapon.RequiredLanes > 1); Tap(battle, 0, 0);
            for (int i = 0; i < 3; i++)
            {
                Check.Equal(0, lane.SlotForNote(lane.NextNote));
                double at = lane.NextBeat; battle.Press(0, at);
                battle.Release(0, lane.Holding ? lane.HoldEndBeat : at);
            }
            Check.Equal(0, battle.MissCount); Check.True(battle.TotalDamage > 0);
        }

        [Test] public void BowReleaseAndDefenseHoldRemainIndependentAndUseActualReleaseTime()
        {
            var battle = Battle(new[] { "bow", "heater-shield" }); var bow = battle.LaneAt(0);
            Tap(battle, 0, .1); double draw = bow.NextBeat;
            battle.Press(0, draw); battle.Press(7, draw);
            Check.True(bow.Charging && battle.LaneAt(7).Holding);
            battle.Release(0, draw + .5);
            Check.True(battle.TotalDamage > 0); Check.True(!bow.Charging && battle.LaneAt(7).Holding);
            Check.True(double.IsInfinity(WeaponCueTimeline.Target(battle.LaneAt(7))));
        }

        [Test] public void CrossbowLoadingAndOptionalShotsKeepTheirExistingRules()
        {
            var battle = Battle(new[] { "crossbow", "heater-shield" }); var bow = battle.LaneAt(0);
            Tap(battle, 0, 0);
            for (int i = 0; i < 2; i++)
            { battle.Press(0, bow.NextBeat); battle.Release(0, bow.HoldEndBeat); }
            Check.True(bow.Loaded); double first = bow.NextBeat;
            battle.Advance(first + 2.3); Check.True(bow.Loaded); Check.Equal(0, battle.MissCount);
            Check.Equal(bow.NextBeat, WeaponCueTimeline.Target(bow));
            Tap(battle, 0, bow.NextBeat); Check.True(battle.TotalDamage > 0); Check.True(!bow.Loaded);
        }

        [Test] public void PausingPreservesBothHandsAndCueTargets()
        {
            var battle = Battle(new[] { "bow", "heater-shield" }); Tap(battle, 0, 0);
            battle.Press(0, 1); battle.Press(5, 1); battle.Pause(); battle.Advance(20);
            Check.Equal(1d, battle.Beat); Check.True(battle.RequiredHeldSlots.OrderBy(x => x).SequenceEqual(new[] { 0, 5 }));
            battle.Resume(); battle.Release(0, 1.5); Check.True(battle.LaneAt(5).Holding);
        }

        [Test] public void SessionBuildsTheNewCombatWithTheSameEquipmentInstancesAndMusic()
        {
            var run = new RunSession(31, useFiveLaneCombat: true, separateHands: true);
            Check.True(run.Enter(run.Map.Nodes.First(n => run.CanEnter(n.Id)).Id));
            var battle = run.StartFiveLaneBattle(); Check.True(battle.SeparateHands);
            Check.Equal(run.BattleMusic.Music.Bpm, battle.Bpm);
            Check.Equal(2, battle.Lanes.Count); Check.Equal("heater-shield", battle.LaneAt(7).Weapon.DefinitionId);
        }
    }
}
