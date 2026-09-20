using System;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class BeatCombatRevisionTests
    {
        private static FiveLaneBattle Battle(params BeatAttack[] attacks) => new FiveLaneBattle(
            new[] { new WeaponState("dagger", WeaponAttribute.Dual), new WeaponState("heater-shield", WeaponAttribute.Dual) }, 120, 32, attacks, new StageHealth(10000), 100, 100);
        private static void Tap(FiveLaneBattle b, int slot, double beat) { b.Press(slot, beat); b.Release(slot, beat); }

        [Test] public void DaggerCreatesExactlyOneNoteTwoBeatsAfterSuccess()
        {
            var b = Battle(); var lane = b.Lanes[0];
            Check.False(lane.IsNoteVisible(0)); Tap(b, 0, 1);
            Check.Equal(3.0, lane.NextBeat); Check.True(lane.IsNoteVisible(0));
            Check.Equal(1, lane.NoteStates.Count); Check.Equal(6m, b.TotalDamage);
            Tap(b, 0, 3.2); Check.Equal(5.0, lane.NextBeat); Check.Equal(9m, b.TotalDamage);
            b.Advance(5.25); Check.Equal(PhraseLanePhase.Cooldown, lane.Phase);
            Check.False(lane.IsNoteVisible(0));
            Check.True(Math.Abs(lane.ReadyAtBeat - lane.LastJudgedBeat - 2) < .00001);
            b.Advance(lane.ReadyAtBeat); Check.Equal(PhraseLanePhase.Ready, lane.Phase);
        }
        [Test] public void DaggerCanStartAndKeepAnOffbeatChain()
        {
            var b = Battle(); Tap(b, 0, .42);
            Check.Equal(6m, b.TotalDamage); Check.Equal(0, b.MissCount);
            Check.Equal(2.5, b.Lanes[0].NextBeat); Tap(b, 0, 2.5); Tap(b, 0, 4.5);
            Check.Equal(18m, b.TotalDamage); Check.Equal(6.5, b.Lanes[0].NextBeat);
        }
        [Test] public void HeaterOpeningChoosesTheNearestHalfBeatAndEndsItsTwoBeatPattern()
        {
            var cases = new[,] { { .24, 0.0 }, { .26, .5 }, { .76, 1.0 } };
            for (int i = 0; i < cases.GetLength(0); i++)
            {
                var b = Battle(); b.Press(1, cases[i, 0]);
                Check.True(b.Lanes[1].Holding); Check.Equal(cases[i, 1], b.Lanes[1].StartBeat);
                Check.Equal(0, b.MissCount); Check.Equal(0m, b.TotalBlocked);
                double end = cases[i, 1] + 2;
                b.Advance(end - .01); Check.True(b.Lanes[1].Holding);
                b.Advance(end); Check.False(b.Lanes[1].Holding);
                Check.Equal(end + 2, b.Lanes[1].ReadyAtBeat); Check.Equal(1, b.PerfectCount);
            }
        }
        [Test] public void HeaterParriesAtPressThenReducesDamageOnlyDuringItsTwoBeatHold()
        {
            var b = Battle(new BeatAttack("a", 0, 10), new BeatAttack("b", 1, 10), new BeatAttack("c", 2.5, 10));
            b.Press(1, 0); Check.True(b.Lanes[1].Holding); Check.Equal(10m, b.TotalBlocked);
            b.Advance(1.3); Check.Equal(95m, b.PlayerHealth); Check.Equal(5m, b.TotalReduced);
            b.Advance(2); Check.False(b.Lanes[1].Holding); Check.Equal(2.0, b.Lanes[1].ReadyAtBeat);
            Check.Equal(PhraseLanePhase.Ready, b.Lanes[1].Phase);
            b.Advance(2.8); Check.Equal(85m, b.PlayerHealth);
            b.Advance(4); b.Press(1, 4); Check.False(b.Lanes[1].Holding); // Still held: no new keydown.
            b.Release(1, 4); b.Press(1, 4); Check.True(b.Lanes[1].Holding);
        }
        [Test] public void HeaterCanParrySuccessiveHalfBeatAttacksAndOnlyTheNextUnparriedUseHasCooldown()
        {
            foreach (double delay in new[] { 0.0, .2 })
            {
                var b = Battle(new[] { 1.0, 1.5, 2.0, 2.5 }.Select(at => new BeatAttack("a", at, 10)).ToArray());
                var lane = b.Lanes[1];
                foreach (double at in new[] { 1.0, 1.5, 2.0, 2.5 })
                {
                    b.Press(1, at + delay);
                    Check.True(lane.Holding); Check.Equal(at + delay, lane.ReadyAtBeat);
                    b.Release(1, at + delay + .01);
                    Check.Equal(PhraseLanePhase.Ready, lane.Phase); Check.False(lane.Holding);
                }
                Check.Equal(40m, b.TotalBlocked); Check.Equal(100m, b.PlayerHealth); Check.Equal(0, b.MissCount);
                // An accepted opening without an attack must not inherit the previous refund.
                b.Press(1, 3); b.Release(1, 3.1);
                Check.Equal(PhraseLanePhase.Cooldown, lane.Phase); Check.Equal(5.1, lane.ReadyAtBeat);
            }
        }
        [Test] public void ParryRecoveryLeavesOtherWeaponsCooldownsUntouched()
        {
            var b = new FiveLaneBattle(new[] { new WeaponState("heater-shield"), new WeaponState("round-shield"),
                new WeaponState("dagger") }, 120, 32, new[] { new BeatAttack("a", 1, 10) }, new StageHealth(100), 100, 100);
            Tap(b, 2, 0); Tap(b, 1, .1); Tap(b, 2, .5);
            double otherShieldReady = b.Lanes[1].ReadyAtBeat, daggerReady = b.Lanes[2].ReadyAtBeat;
            b.Press(0, 1); b.Release(0, 1.01);
            Check.Equal(PhraseLanePhase.Ready, b.Lanes[0].Phase); Check.Equal(10m, b.TotalBlocked);
            Check.Equal(PhraseLanePhase.Cooldown, b.Lanes[1].Phase); Check.Equal(otherShieldReady, b.Lanes[1].ReadyAtBeat);
            Check.Equal(PhraseLanePhase.Cooldown, b.Lanes[2].Phase); Check.Equal(daggerReady, b.Lanes[2].ReadyAtBeat);
        }
        [Test] public void AuthoredParryOnAnOffensiveWeaponDoesNotRecoverItsCooldown()
        {
            var phrase = new WeaponPhrase("sword", "parry strike", "", 1,
                new[] { new WeaponPhraseNote(0, 3, effect: PhraseEffect.Parry) }, completionCooldownBeats: 2);
            var b = new FiveLaneBattle(new[] { new WeaponState("sword") }, 120, 32,
                new[] { new BeatAttack("a", 1, 10) }, new StageHealth(100), 100, 100, new[] { phrase });
            Tap(b, 0, 1); Check.Equal(10m, b.TotalBlocked); Check.Equal(3m, b.TotalDamage);
            Check.Equal(PhraseLanePhase.Cooldown, b.Lanes[0].Phase); Check.Equal(4.0, b.Lanes[0].ReadyAtBeat);
        }
        [Test] public void HeaterCanGuardWithoutAParryAndEarlyReleaseStartsTwoBeatCooldown()
        {
            var b = Battle(new BeatAttack("a", .5, 10), new BeatAttack("a", 1.5, 10));
            b.Press(1, .1); Check.True(b.Lanes[1].Holding); Check.Equal(0.0, b.Lanes[1].StartBeat);
            Check.Equal("GUARD", b.Lanes[1].Feedback); b.Release(1, .75);
            Check.Equal(2.75, b.Lanes[1].ReadyAtBeat); Check.Equal(0, b.MissCount);
            b.Advance(1.8); Check.Equal(85m, b.PlayerHealth); Check.Equal(5m, b.TotalReduced);
            b.Advance(2.74); Check.Equal(PhraseLanePhase.Cooldown, b.Lanes[1].Phase);
            b.Advance(2.75); Check.Equal(PhraseLanePhase.Ready, b.Lanes[1].Phase);
        }
        [Test] public void ProtectionIsSampledAtImpactEvenIfHoldEndsBeforeLateParryDeadline()
        {
            var b = Battle(new BeatAttack("a", 1.9, 10), new BeatAttack("a", 2, 10));
            b.Press(1, 0); b.Advance(2.3);
            Check.Equal(85m, b.PlayerHealth); Check.Equal(5m, b.TotalReduced);
        }
        [Test] public void LateGuardDoesNotRetroactivelyProtectAnEarlierImpact()
        {
            // A hold with no parry distinguishes mitigation from the separate late-parry window.
            var phrase = new WeaponPhrase("heater-shield", "guard", "", 2,
                new[] { new WeaponPhraseNote(0, 0, 2) }, repeat: false,
                holdDamageReduction: .5m, releaseEndsPhrase: true, completionCooldownBeats: 2);
            var b = new FiveLaneBattle(new[] { new WeaponState("heater-shield") }, 120, 32,
                new[] { new BeatAttack("a", .5, 10) }, new StageHealth(100), 100, 100, new[] { phrase });
            b.Press(0, .6); b.Advance(.8); Check.Equal(90m, b.PlayerHealth); Check.Equal(0m, b.TotalReduced);
        }
        [Test] public void HeaterTimingIsTheSameAcrossLargeFramesAndPause()
        {
            var attacks = new[] { new BeatAttack("a", .8, 10), new BeatAttack("a", 1.9, 10), new BeatAttack("a", 2.1, 10) };
            var fine = Battle(attacks); var coarse = Battle(attacks);
            fine.Press(1, 0); coarse.Press(1, 0);
            fine.Advance(.5); fine.Pause(); fine.Release(1, 20); fine.Advance(20);
            Check.Equal(.5, fine.Beat); Check.True(fine.Lanes[1].Holding); fine.Resume();
            for (int i = 6; i <= 30; i++) fine.Advance(i / 10.0);
            coarse.Advance(3);
            Check.Equal(80m, fine.PlayerHealth); Check.Equal(fine.PlayerHealth, coarse.PlayerHealth);
            Check.Equal(fine.TotalReduced, coarse.TotalReduced); Check.Equal(4.0, fine.Lanes[1].ReadyAtBeat);
        }
        [Test] public void BowShotIsNotGeneratedUntilDrawCompletes()
        {
            foreach (bool complete in new[] { false, true })
            {
                var b = new FiveLaneBattle(new[] { new WeaponState("bow") }, 120, 32, Array.Empty<BeatAttack>(), new StageHealth(100), 100, 100);
                b.Press(0, 0); Check.Equal(PhraseNoteState.Locked, b.Lanes[0].NoteStates[1]);
                Check.False(b.Lanes[0].IsNoteVisible(1)); b.Release(0, complete ? 1 : .5);
                Check.Equal(complete, b.Lanes[0].IsNoteVisible(1));
                Check.Equal(complete ? PhraseNoteState.Pending : PhraseNoteState.Skipped, b.Lanes[0].NoteStates[1]);
            }
        }
        [Test] public void GeneratedCountersSurviveAMissedCounter()
        {
            var b = new FiveLaneBattle(new[] { new WeaponState("shield") }, 120, 32,
                new[] { new BeatAttack("a", 0, 10) }, new StageHealth(100), 100, 100);
            Tap(b, 0, 0); Check.True(b.Lanes[0].IsNoteVisible(1)); Check.True(b.Lanes[0].IsNoteVisible(3));
            b.Advance(1.25); Check.Equal(PhraseNoteState.Missed, b.Lanes[0].NoteStates[1]);
            Check.True(b.Lanes[0].IsNoteVisible(2)); Check.True(b.Lanes[0].IsNoteVisible(3));
            Tap(b, 0, 1.5); Tap(b, 0, 2); Check.Equal(14m, b.TotalDamage);
            Check.Equal(PhraseLanePhase.Ready, b.Lanes[0].Phase); Check.Equal(2.0, b.Lanes[0].ReadyAtBeat);
        }
        [Test] public void MissingARepeatedOpeningStillLeavesUnconditionalLaterNotesPlayable()
        {
            var phrase = new WeaponPhrase("sword", "authored repeat", "", 4,
                WeaponPhraseCatalog.Find("sword").Notes, repeat: true);
            var b = new FiveLaneBattle(new[] { new WeaponState("sword") }, 120, 32, Array.Empty<BeatAttack>(), new StageHealth(100), 100, 100,
                new[] { phrase });
            Tap(b, 0, 0); Tap(b, 0, 1); Tap(b, 0, 2); b.Advance(4.25);
            Check.Equal(PhraseNoteState.Missed, b.Lanes[0].NoteStates[0]);
            Check.True(b.Lanes[0].IsNoteVisible(1)); Check.True(b.Lanes[0].IsNoteVisible(2));
            Tap(b, 0, 5); Tap(b, 0, 6); Check.Equal(48m, b.TotalDamage); Check.Equal(1, b.MissCount);
        }
        [Test] public void ConditionalNotesRejectForwardOrNonParryPrerequisites()
        {
            foreach (int prerequisite in new[] { 0, 1 })
            {
                bool rejected = false;
                try { new WeaponPhrase("sword", "", "", 2, new[] { new WeaponPhraseNote(0, 1),
                    new WeaponPhraseNote(1, 1, prerequisite: prerequisite, condition: PhraseNoteCondition.Parry) }); }
                catch (ArgumentException) { rejected = true; }
                Check.True(rejected);
            }
        }
        [Test] public void WholeBeatStepsTakeTheSameRealTimeAtEverySongTempo()
        {
            foreach (double bpm in new[] { 30.0, 60, 90, 120, 180, 240 })
            foreach (double secondsBefore in new[] { .2, .18, .135, .09, .045, .005, 0 })
            {
                double expected = SteppedNoteTrack.Distance(2, 2 - secondsBefore, 60);
                double actual = SteppedNoteTrack.Distance(2, 2 - secondsBefore * bpm / 60, bpm);
                Check.True(Math.Abs(expected - actual) < 1e-9);
            }
            // At 60 BPM the note waits longer, but its 90 ms midpoint is unchanged.
            Check.Equal(1.0, SteppedNoteTrack.Distance(2, 1.5, 60));
            Check.Equal(1.0, SteppedNoteTrack.Distance(2, 1.8, 60));
            Check.True(Math.Abs(.5 - SteppedNoteTrack.Distance(2, 1.91, 60)) < 1e-9);
            Check.True(Math.Abs(.5 - SteppedNoteTrack.Distance(2, 1.82, 120)) < 1e-9);
        }
        [Test] public void ReceptorPulseStaysBriefEvenInSlowSongs()
        {
            foreach (double bpm in new[] { 30.0, 60, 90, 120, 180, 240 })
            foreach (double secondsAfter in new[] { 0.0, .02, .04, .08, .15 })
                Check.True(Math.Abs(SteppedNoteTrack.BeatPulse(2 + secondsAfter * bpm / 60, bpm) -
                    SteppedNoteTrack.BeatPulse(2 + secondsAfter, 60)) < 1e-9);
            Check.Equal(1.0, SteppedNoteTrack.BeatPulse(2, 60));
            Check.Equal(0.0, SteppedNoteTrack.BeatPulse(2.1, 60));
        }
        [Test] public void FutureNotesShareWholeBeatStepsAndPreserveTheirRhythmSpacing()
        {
            foreach (double bpm in new[] { 60.0, 120, 240 })
            foreach (double now in new[] { .1, .4, .7, .9 })
            {
                double movement = SteppedNoteTrack.Distance(1, now, bpm) - SteppedNoteTrack.Distance(1, now + .05, bpm);
                foreach (double due in new[] { 1.5, 2, 2.375, 2 + 1.0 / 3 })
                {
                    double other = SteppedNoteTrack.Distance(due, now, bpm) - SteppedNoteTrack.Distance(due, now + .05, bpm);
                    Check.True(Math.Abs(movement - other) < 1e-9);
                    Check.True(Math.Abs(due - 1 - (SteppedNoteTrack.Distance(due, now, bpm) -
                        SteppedNoteTrack.Distance(1, now, bpm))) < 1e-9);
                }
                Check.True(Math.Abs(SteppedNoteTrack.Distance(2, now, bpm) -
                    SteppedNoteTrack.Distance(34, now + 32, bpm)) < 1e-9);
            }
        }
        [Test] public void WholeHalfAndFreeTimingNotesArriveAtTheirActualScheduledBeats()
        {
            foreach (double bpm in new[] { 30.0, 60, 120, 240, 600 })
            foreach (double due in new[] { 1, 1.5, 2.375, 2 + 1.0 / 3 })
            {
                Check.True(SteppedNoteTrack.Distance(due, due - .01, bpm) > 0);
                Check.Equal(0.0, SteppedNoteTrack.Distance(due, due, bpm));
                Check.Equal(0.0, SteppedNoteTrack.Distance(due, due + .01, bpm));
            }
            foreach (double bpm in new[] { 60.0, 120 })
            {
                Check.Equal(.5, SteppedNoteTrack.Distance(2.5, 2, bpm));
                Check.True(Math.Abs(.25 - SteppedNoteTrack.Distance(2.5, 2.5 - .09 * bpm / 60, bpm)) < 1e-9);
            }
            var b = Battle(); b.Press(1, .137);
            double end = b.Lanes[1].HoldEndBeat;
            Check.Equal(2.0, SteppedNoteTrack.Distance(end, b.Lanes[1].StartBeat, b.Bpm));
            b.Advance(end);
            Check.Equal(0.0, SteppedNoteTrack.Distance(end, b.Beat, b.Bpm));
            Check.False(b.Lanes[1].Holding);
        }
        [Test] public void TrackKeepsThreeBeatHorizonAndAttackMarkersMeetTheirImpactTime()
        {
            Check.Equal(3.0, SteppedNoteTrack.LookAheadBeats);
            Check.True(SteppedNoteTrack.InHorizon(3)); Check.False(SteppedNoteTrack.InHorizon(3.00001));
            foreach (double bpm in new[] { 30.0, 60, 120, 240 })
            {
                foreach (double now in new[] { 0, .17, .5, .99 })
                {
                    Check.True(SteppedNoteTrack.Distance(now + 3, now, bpm) <= 3);
                    Check.Equal(3.0, SteppedNoteTrack.Distance(now + 4, now, bpm));
                }
                foreach (double due in new[] { 2, 2.5, 2.137 })
                {
                    Check.Equal(0.0, SteppedNoteTrack.ImpactProgress(due, due - 1.1, bpm));
                    Check.True(SteppedNoteTrack.ImpactProgress(due, due - .01, bpm) < 1);
                    Check.Equal(1.0, SteppedNoteTrack.ImpactProgress(due, due, bpm));
                    Check.Equal(1.0, SteppedNoteTrack.ImpactProgress(due, due + .1, bpm));
                }
            }
        }
        [Test] public void PausingAndSkippedFramesDoNotChangeTrackPhase()
        {
            var fine = Battle(); var coarse = Battle(); Tap(fine, 0, 0); Tap(coarse, 0, 0);
            fine.Advance(.2);
            double heldPosition = SteppedNoteTrack.Distance(fine.Lanes[0].NextBeat, fine.Beat, fine.Bpm);
            fine.Pause(); fine.Advance(20);
            Check.Equal(heldPosition, SteppedNoteTrack.Distance(fine.Lanes[0].NextBeat, fine.Beat, fine.Bpm));
            fine.Resume();
            for (int frame = 21; frame <= 95; frame++) fine.Advance(frame / 100.0);
            coarse.Advance(.95);
            Check.Equal(SteppedNoteTrack.Distance(fine.Lanes[0].NextBeat, fine.Beat, fine.Bpm),
                SteppedNoteTrack.Distance(coarse.Lanes[0].NextBeat, coarse.Beat, coarse.Bpm));
            Check.True(fine.Lanes[0].IsNoteVisible(0)); Check.Equal(2.0, fine.Lanes[0].NextBeat);
        }
        [Test] public void FractionalApproachesNeverOvertakeOrJumpAtWholeBeatBoundaries()
        {
            double[] due = { .25, 1.0 / 3, .5, .75, 1, 1.5, 2.375, 3 };
            foreach (double bpm in new[] { 30.0, 60, 120, 240, 600 })
            {
                var previous = due.Select(at => SteppedNoteTrack.Distance(at, 0, bpm)).ToArray();
                for (int frame = 1; frame <= 3100; frame++)
                {
                    double now = frame / 1000.0, earlier = 0;
                    for (int i = 0; i < due.Length; i++)
                    {
                        double distance = SteppedNoteTrack.Distance(due[i], now, bpm);
                        Check.True(distance >= earlier - 1e-9 && distance <= previous[i] + 1e-9);
                        Check.True(due[i] <= now || distance > 0);
                        earlier = previous[i] = distance;
                    }
                }
                foreach (double boundary in new[] { 1.0, 2 })
                    Check.True(Math.Abs(SteppedNoteTrack.Distance(2.375, boundary - 1e-6, bpm) -
                        SteppedNoteTrack.Distance(2.375, boundary + 1e-6, bpm)) < .0001);
            }
        }
        [Test] public void WeaponRewardsAccumulateWithoutChangingEquippedInputs()
        {
            var run = new RunSession(31, useFiveLaneCombat: true);
            Check.True(run.EquipWeapon(1, 4));
            int rewards = 0;
            for (int step = 0; step < 40 && rewards < 6; step++)
            {
                if (run.Phase == RunPhase.FieldCleared) Check.True(run.AdvanceField());
                var node = run.Map.Nodes.Where(n => run.CanEnter(n.Id)).OrderByDescending(n => n.IsBattle).First();
                Check.True(run.Enter(node.Id));
                if (!node.IsBattle) { Check.True(run.LeaveService()); continue; }
                Check.True(run.ResolveBattle(run.StageTicket, true, 100));
                int index = run.Offers.ToList().FindIndex(o => o.Content.Kind == RewardKind.Weapon);
                int count = run.OwnedWeapons.Count;
                Check.True(run.ChooseReward(index)); rewards++;
                Check.Equal(count + 1, run.OwnedWeapons.Count); Check.Equal(2, run.Weapons.Count);
                Check.Equal("dagger", run.Equipment.At(0).Weapon.DefinitionId);
                Check.Equal("heater-shield", run.Equipment.At(4).Weapon.DefinitionId);
                Check.True(run.Equipment.At(1) == null);
            }
            Check.Equal(8, run.OwnedWeapons.Count);
            run.Restart(31); Check.Equal(2, run.Weapons.Count); Check.Equal(2, run.OwnedWeapons.Count);
            Check.Equal(2, run.Equipment.Capacity); Check.True(run.Equipment.At(4) == null);
        }
    }
}
