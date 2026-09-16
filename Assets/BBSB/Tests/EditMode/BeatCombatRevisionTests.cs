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
            new[] { new WeaponState("dagger"), new WeaponState("heater-shield") }, 120, 32, attacks, new StageHealth(10000), 100, 100);
        private static void Tap(FiveLaneBattle b, int slot, double beat) { b.Press(slot, beat); b.Release(slot, beat); }

        [Test] public void DaggerCreatesExactlyOneNextBeatOnlyAfterSuccess()
        {
            var b = Battle(); var lane = b.Lanes[0];
            Check.False(lane.IsNoteVisible(0)); Tap(b, 0, 1);
            Check.Equal(2.0, lane.NextBeat); Check.True(lane.IsNoteVisible(0));
            Check.Equal(1, lane.NoteStates.Count); Check.Equal(6m, b.TotalDamage);
            Tap(b, 0, 2.2); Check.Equal(3.0, lane.NextBeat); Check.Equal(9m, b.TotalDamage);
            b.Advance(3.25); Check.Equal(PhraseLanePhase.Cooldown, lane.Phase);
            Check.False(lane.IsNoteVisible(0));
            Check.True(Math.Abs(lane.ReadyAtBeat - lane.LastJudgedBeat - 2) < .00001);
            b.Advance(lane.ReadyAtBeat); Check.Equal(PhraseLanePhase.Ready, lane.Phase);
        }
        [Test] public void DaggerCannotStartAnOffbeatChain()
        {
            var b = Battle(); Tap(b, 0, .5);
            Check.Equal(0m, b.TotalDamage); Check.Equal(1, b.MissCount);
            Check.Equal(2.5, b.Lanes[0].ReadyAtBeat);
        }
        [Test] public void HeaterParriesAtPressThenReducesDamageOnlyDuringItsTwoBeatHold()
        {
            var b = Battle(new BeatAttack("a", 0, 10), new BeatAttack("b", 1, 10), new BeatAttack("c", 2.5, 10));
            b.Press(1, 0); Check.True(b.Lanes[1].Holding); Check.Equal(10m, b.TotalBlocked);
            b.Advance(1.3); Check.Equal(95m, b.PlayerHealth); Check.Equal(5m, b.TotalReduced);
            b.Advance(2); Check.False(b.Lanes[1].Holding); Check.Equal(4.0, b.Lanes[1].ReadyAtBeat);
            b.Advance(2.8); Check.Equal(85m, b.PlayerHealth);
            b.Advance(4); b.Press(1, 4); Check.False(b.Lanes[1].Holding); // Still held: no new keydown.
            b.Release(1, 4); b.Press(1, 4); Check.True(b.Lanes[1].Holding);
        }
        [Test] public void HeaterCanGuardWithoutAParryAndEarlyReleaseStartsTwoBeatCooldown()
        {
            var b = Battle(new BeatAttack("a", .5, 10), new BeatAttack("a", 1.5, 10));
            b.Press(1, .1); Check.True(b.Lanes[1].Holding); Check.Equal(.1, b.Lanes[1].StartBeat);
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
                new[] { new WeaponPhraseNote(0, 0, 2) }, repeat: false, startGridBeats: 0,
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
            Check.Equal(PhraseLanePhase.Cooldown, b.Lanes[0].Phase);
        }
        [Test] public void MissingAnOpeningStillLeavesUnconditionalLaterNotesPlayable()
        {
            var b = new FiveLaneBattle(new[] { new WeaponState("sword") }, 120, 32, Array.Empty<BeatAttack>(), new StageHealth(100), 100, 100);
            Tap(b, 0, .25); Check.Equal(PhraseNoteState.Missed, b.Lanes[0].NoteStates[0]);
            Check.True(b.Lanes[0].IsNoteVisible(1)); Check.True(b.Lanes[0].IsNoteVisible(2));
            Tap(b, 0, 1.5); Tap(b, 0, 2.5); Check.Equal(20m, b.TotalDamage); Check.Equal(1, b.MissCount);
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
        [Test] public void NotesDwellThenDropLinearlyEveryHalfBeatAndArriveOnTime()
        {
            Check.Equal(3.0, SteppedNoteTrack.LookAheadBeats);
            Check.Equal(1.5, SteppedNoteTrack.Distance(1.4)); Check.Equal(1.5, SteppedNoteTrack.Distance(1.2));
            Check.Equal(1.5, SteppedNoteTrack.Distance(1.05));
            Check.True(Math.Abs(1.25 - SteppedNoteTrack.Distance(1.025)) < .00001);
            Check.Equal(1.0, SteppedNoteTrack.Distance(1)); Check.Equal(.5, SteppedNoteTrack.Distance(.5));
            Check.Equal(.5, SteppedNoteTrack.Distance(.05));
            Check.True(Math.Abs(.25 - SteppedNoteTrack.Distance(.025)) < .00001);
            Check.Equal(0.0, SteppedNoteTrack.Distance(0)); Check.Equal(0.0, SteppedNoteTrack.Distance(-.1));
            Check.True(SteppedNoteTrack.InHorizon(3)); Check.False(SteppedNoteTrack.InHorizon(3.00001));
            // Incoming attack markers wait too, and cross only just before their impact.
            Check.Equal(0.0, SteppedNoteTrack.DropProgress(.5));
            Check.Equal(0.0, SteppedNoteTrack.DropProgress(.2));
            Check.Equal(0.0, SteppedNoteTrack.DropProgress(.05));
            Check.Equal(.5, SteppedNoteTrack.DropProgress(.025));
            Check.Equal(1.0, SteppedNoteTrack.DropProgress(0));
            Check.Equal(1.0, SteppedNoteTrack.DropProgress(-.1));
        }
        [Test] public void WeaponRewardsFillEmptySlotsThenRequireReplacementAtFive()
        {
            var run = new RunSession(31, useFiveLaneCombat: true);
            for (int step = 0, rewards = 0; step < 40 && rewards < 4; step++)
            {
                if (run.Phase == RunPhase.FieldCleared) Check.True(run.AdvanceField());
                var node = run.Map.Nodes.Where(n => run.CanEnter(n.Id)).OrderByDescending(n => n.IsBattle).First();
                Check.True(run.Enter(node.Id));
                if (!node.IsBattle) { Check.True(run.LeaveService()); continue; }
                Check.True(run.ResolveBattle(run.StageTicket, true, 100));
                int index = run.Offers.ToList().FindIndex(o => o.Content.Kind == RewardKind.Weapon);
                int count = run.Weapons.Count;
                if (count < 5)
                {
                    Check.True(run.ChooseReward(index)); Check.Equal(count + 1, run.Weapons.Count);
                    Check.Equal("dagger", run.Weapons[0].DefinitionId); Check.Equal("heater-shield", run.Weapons[1].DefinitionId);
                }
                else
                {
                    Check.False(run.ChooseReward(index)); Check.Equal(RunPhase.Reward, run.Phase);
                    Check.True(run.ChooseReward(index, 4)); Check.Equal(5, run.Weapons.Count);
                }
                rewards++;
            }
            Check.Equal(5, run.Weapons.Count);
            run.Restart(31); Check.Equal(2, run.Weapons.Count);
        }
    }
}
