using System;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class WeaponMechanicTests
    {
        private static FiveLaneBattle Battle(params WeaponPlacement[] placements) =>
            new FiveLaneBattle(placements.Select(p => p.Weapon).ToArray(), 120, 32, Array.Empty<BeatAttack>(),
                new StageHealth(10000), 100, 100, placements: placements,
                extensions: InputExtensions.Left | InputExtensions.Right);
        private static WeaponPlacement Item(string id, params int[] slots) => new WeaponPlacement(new WeaponState(id), slots);
        private static void Tap(FiveLaneBattle battle, int slot, double beat)
        { battle.Press(slot, beat); battle.Release(slot, beat); }

        [Test] public void OnlyDaggerAndDualSwordsRepeatAndNewPatternsDefaultToOneShot()
        {
            Check.Equal("dagger,dual-swords", string.Join(",", WeaponPhraseCatalog.All.Where(p => p.Repeat).Select(p => p.WeaponId)));
            Check.False(new WeaponPhrase("sword", "one shot", "", 1, new[] { new WeaponPhraseNote(0, 1) }).Repeat);
            Check.Equal(2, WeaponCatalog.Find("staff").RequiredLanes);
            Check.Equal(1, WeaponCatalog.Find("spirit-bell").RequiredLanes);
        }

        [Test] public void StaffMirrorsItsTwoHalfBeatTapsAndOppositeOneBeatHoldAtEitherEdge()
        {
            foreach (var slots in new[] { new[] { 0, 1 }, new[] { 3, 4 }, new[] { 5, 0 }, new[] { 4, 6 } })
            foreach (int offset in new[] { 0, 1 })
            {
                int first = slots[offset], other = slots[1 - offset];
                var b = Battle(Item("staff", slots));
                Tap(b, first, .18); var lane = b.Lanes[0];
                Check.Equal(offset, lane.StartOffset); Check.Equal(8m, b.TotalDamage);
                var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
                Check.Equal(first, timeline.Notes.Single(n => n.Beat == .5).Slot);
                Check.Equal(other, timeline.Notes.Single(n => n.Beat == 1).Slot);
                Check.True(timeline.Notes.All(n => !n.IsPreview));
                Tap(b, other, .5); Check.Equal(8m, b.TotalDamage);
                Tap(b, first, .5); b.Press(other, 1);
                Check.True(lane.Holding); Check.Equal(other, lane.HoldingSlot);
                Tap(b, first, 1.4); Check.True(lane.Holding);
                b.Release(other, 2);
                Check.Equal(34m, b.TotalDamage); Check.Equal(0, b.MissCount); Check.False(lane.CanRepeat);
                b.Advance(5); Check.Equal(PhraseLanePhase.Ready, lane.Phase); Check.Equal(0, b.MissCount);
            }
        }

        [Test] public void StaffMissKeepsItsRemainingUnconditionalNotesPlayable()
        {
            var b = Battle(Item("staff", 0, 1)); Tap(b, 0, 0); b.Advance(.75);
            Check.Equal(1, b.MissCount); Check.True(b.Lanes[0].IsNoteVisible(2));
            b.Press(1, 1); b.Release(1, 2);
            Check.Equal(26m, b.TotalDamage); Check.Equal(4.0, b.Lanes[0].ReadyAtBeat);
        }

        [Test] public void StaffConsumesOneItemCapacityAndOneOwnedInstanceAcrossBothLines()
        {
            var equipment = new WeaponEquipment(); var staff = new WeaponState("staff");
            var bell = new WeaponState("spirit-bell"); equipment.Acquire(staff); equipment.Acquire(bell);
            Check.True(equipment.EquipCentered(staff, 3.5)); Check.True(equipment.Equip(bell, 2));
            Check.Equal(2, equipment.Equipped.Count); Check.Equal(3, equipment.OccupiedLaneCount);
            Check.True(ReferenceEquals(staff, equipment.At(3).Weapon));
            Check.True(ReferenceEquals(equipment.At(3), equipment.At(4)));
        }

        [Test] public void BellStartsBothNeighborsAfterOneBeatThroughCooldownWithoutForgingInput()
        {
            var b = Battle(Item("dagger", 0), Item("spirit-bell", 1), Item("bow", 2));
            Tap(b, 0, 0); b.Press(2, 0); Tap(b, 0, .5); b.Release(2, .5);
            Check.Equal(2.5, b.LaneAt(0).ReadyAtBeat); Check.Equal(2.5, b.LaneAt(2).ReadyAtBeat);
            Tap(b, 1, 1); Check.Equal(2, b.ScheduledStarts.Count);
            var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
            Check.True(timeline.Notes.Where(n => n.Beat == 2).All(n => n.IsPreview));
            Check.Equal(2, timeline.Notes.Count(n => n.Beat == 2));
            b.Advance(1.7); Check.Equal(PhraseLanePhase.Cooldown, b.LaneAt(0).Phase);
            b.Advance(2); timeline.Refresh(b);
            Check.Equal(PhraseLanePhase.Playing, b.LaneAt(0).Phase);
            Check.Equal(PhraseLanePhase.Playing, b.LaneAt(2).Phase);
            Check.Equal(6m, b.TotalDamage); Check.False(b.LaneAt(2).Holding);
            Check.True(b.ScheduledStarts.All(s => s.State == ScheduledStartState.Started));
            Check.True(timeline.Notes.Where(n => n.Beat == 2).All(n => !n.IsPreview));
            Check.Equal(2, timeline.Confirmed.Count); Check.Equal(0, timeline.Broken.Count);
            Tap(b, 0, 2); b.Press(2, 2); b.Release(2, 3); Tap(b, 0, 4); Tap(b, 2, 4);
            Check.Equal(28m, b.LaneAt(2).DamageDealt); Check.False(b.LaneAt(2).CanRepeat);
        }

        [Test] public void BellUsesTheContactedStaffOffsetAndAcceptsAnEarlyScheduledInput()
        {
            foreach (bool leftBell in new[] { false, true })
            {
                int bellSlot = leftBell ? 0 : 3, contacted = leftBell ? 1 : 2, other = leftBell ? 2 : 1;
                var b = Battle(Item("staff", 1, 2), Item("spirit-bell", bellSlot));
                Tap(b, bellSlot, .1);
                var scheduled = b.ScheduledStarts.Single(); Check.Equal(1.0, scheduled.Beat); Check.Equal(contacted, scheduled.Slot);
                var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
                Check.Equal(other, timeline.Notes.Single(n => n.Beat == 2).Slot);
                Tap(b, contacted, .85); Check.Equal(1.0, b.Lanes[0].StartBeat);
                Check.Equal(leftBell ? 0 : 1, b.Lanes[0].StartOffset); Check.Equal(8m, b.TotalDamage);
                Tap(b, contacted, 1.5); b.Press(other, 2); b.Release(other, 3);
                Check.Equal(34m, b.TotalDamage); Check.Equal(0, b.MissCount);
            }
        }

        [Test] public void BellChecksPlayingAtExecutionAndShattersOnlyTheCanceledReservation()
        {
            var b = Battle(Item("spirit-bell", 0), Item("staff", 1, 2));
            Tap(b, 0, 0); var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
            Tap(b, 2, .5); timeline.Refresh(b); Tap(b, 2, 1); timeline.Refresh(b);
            Check.Equal(ScheduledStartState.Skipped, b.ScheduledStarts.Single().State);
            Check.Equal(.5, b.LaneAt(2).StartBeat); Check.Equal(1, b.LaneAt(2).StartOffset);
            Check.True(timeline.Broken.Any(n => n.Note.CycleStart == 1));
            Check.True(timeline.Notes.Any(n => n.Slot == 1 && n.Beat == 1.5 && !n.IsPreview));
            b.Press(1, 1.5); b.Release(1, 2.5); Check.Equal(34m, b.TotalDamage);
        }

        [Test] public void BellCanRestartAWeaponThatFinishesDuringTheDelay()
        {
            var b = Battle(Item("staff", 0, 1), Item("spirit-bell", 2));
            Tap(b, 0, 0); Tap(b, 0, .5); b.Press(1, 1); Tap(b, 2, 1.5);
            b.Release(1, 2); b.Advance(2.5);
            Check.Equal(ScheduledStartState.Started, b.ScheduledStarts.Single().State);
            Check.Equal(1, b.Lanes[0].StartOffset); Check.Equal(2.5, b.Lanes[0].StartBeat);
        }

        [Test] public void BellDoesNotSkipEmptyLinesOrDoubleStartOneMultiLineWeapon()
        {
            var sparse = Battle(Item("dagger", 0), Item("spirit-bell", 2), Item("bow", 4));
            Tap(sparse, 2, 0); Check.Equal(0, sparse.ScheduledStarts.Count);
            var b = Battle(Item("spirit-bell", 0), Item("staff", 1, 2), Item("spirit-bell", 3));
            Tap(b, 0, 0); Tap(b, 3, 0); Check.Equal(1, b.ScheduledStarts.Count);
            b.Advance(1); Check.Equal(0, b.LaneAt(1).StartOffset); Check.Equal(0m, b.TotalDamage);
        }

        [Test] public void BellRespectsPhysicalExtensionOrderAndDoesNotResetARunningLoop()
        {
            var left = Battle(Item("staff", 5, 0), Item("spirit-bell", 1)); Tap(left, 1, 0); left.Advance(1);
            Check.Equal(1, left.Lanes[0].StartOffset); Check.Equal(0, left.Lanes[0].Slot);
            var right = Battle(Item("staff", 4, 6), Item("spirit-bell", 3)); Tap(right, 3, 0); right.Advance(1);
            Check.Equal(0, right.Lanes[0].StartOffset); Check.Equal(4, right.Lanes[0].Slot);
            var loop = Battle(Item("dagger", 0), Item("spirit-bell", 1)); Tap(loop, 0, 0); Tap(loop, 1, 0); loop.Advance(1);
            Check.Equal(ScheduledStartState.Skipped, loop.ScheduledStarts.Single().State);
            Check.Equal(2.0, loop.Lanes[0].NextBeat); Check.Equal(6m, loop.TotalDamage);
        }

        [Test] public void ScheduledHoldStillNeedsARealContactAndCannotGrantFreeGuardOrParry()
        {
            var b = Battle(Item("heater-shield", 0), Item("spirit-bell", 1));
            Tap(b, 1, 0); b.Advance(1);
            Check.False(b.LaneAt(0).Holding); Check.False(b.IsInputHeld(0));
            b.Advance(1.25); Check.Equal(1, b.MissCount); Check.Equal(0m, b.TotalBlocked);
            Check.Equal(0m, b.TotalReduced);
        }

        [Test] public void BellReservationDoesNotDuplicateOrFalselyConfirmAnExistingLoopNote()
        {
            var b = Battle(Item("dual-swords", 0), Item("spirit-bell", 1));
            Tap(b, 0, 0); Tap(b, 1, 0);
            var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
            Check.Equal(3, timeline.Notes.Count); Check.Equal(1, timeline.Notes.Count(n => n.Beat == 1));
            b.Advance(1); timeline.Refresh(b);
            Check.Equal(ScheduledStartState.Skipped, b.ScheduledStarts.Single().State);
            Check.Equal(0, timeline.Confirmed.Count); Check.Equal(0, timeline.Broken.Count);
            Tap(b, 0, 1); timeline.Refresh(b);
            Check.Equal(1, timeline.Confirmed.Count); Check.Equal(2.0, timeline.Confirmed.Single().Note.Beat);
        }

        [Test] public void ScheduledStartsPauseAndResolveChronologicallyDuringLongFrames()
        {
            var b = Battle(Item("spirit-bell", 0), Item("staff", 1, 2)); Tap(b, 0, 0);
            b.Advance(.5); b.Pause(); b.Advance(20);
            Check.Equal(ScheduledStartState.Pending, b.ScheduledStarts.Single().State); Check.Equal(.5, b.Beat);
            b.Resume(); b.Advance(4);
            Check.Equal(3, b.MissCount); Check.Equal(PhraseLanePhase.Cooldown, b.LaneAt(1).Phase);
            Check.Equal(1.0, b.LaneAt(1).StartBeat); Check.Equal(0m, b.TotalDamage);
            b.Advance(10); Check.Equal(0, b.ScheduledStarts.Count);
        }

        [Test] public void NeighborBellsRequireInputAndDoNotCreateAnAutomaticFeedbackLoop()
        {
            var b = Battle(Item("spirit-bell", 0), Item("spirit-bell", 1)); Tap(b, 0, 0);
            b.Advance(10); Check.Equal(1, b.PerfectCount); Check.Equal(1, b.MissCount);
            Check.Equal(0, b.ScheduledStarts.Count); Check.Equal(0m, b.TotalDamage);
        }

        [Test] public void BowCompletesOneDrawAndShotWithoutForecastingOrMissingAnotherCycle()
        {
            var b = Battle(Item("bow", 0)); b.Press(0, 0); b.Release(0, 1);
            var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
            Check.True(timeline.Notes.All(n => n.CycleStart == 0)); Tap(b, 0, 2); b.Advance(10); timeline.Refresh(b);
            Check.Equal(28m, b.TotalDamage); Check.Equal(0, b.MissCount); Check.Equal(0, timeline.Notes.Count);
            Check.Equal(PhraseLanePhase.Ready, b.Lanes[0].Phase);
        }
    }
}
