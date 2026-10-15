using System;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class PlayerDefenseMotionTests
    {
        private static FiveLaneBattle Battle(string attack = "dagger", BeatAttack[] incoming = null, string defense = "heater-shield") =>
            new FiveLaneBattle(new[] { new WeaponState(attack), new WeaponState(defense) }, 120, 64,
                incoming ?? Array.Empty<BeatAttack>(), new StageHealth(10000), 100, 100, separateHands: true);

        [Test] public void EveryChordHasOneShortSmearThenSettlesWithoutSpinning()
        {
            for (int mask = 1; mask <= 15; mask++)
            {
                var b = Battle();
                for (int d = 0; d < 4; d++) if ((mask & 1 << d) != 0) b.Press(4 + d, .4);
                Check.Equal(DefenseMotionPhase.Windup, PlayerDefenseMotion.Sample(b).Phase);
                b.Advance(.48); var smear = PlayerDefenseMotion.Sample(b);
                Check.Equal(DefenseMotionPhase.Smear, smear.Phase); Check.Equal(mask, smear.Directions);
                b.Advance(.58); Check.Equal(DefenseMotionPhase.Contact, PlayerDefenseMotion.Sample(b).Phase);
                b.Advance(.75); Check.Equal(DefenseMotionPhase.Recover, PlayerDefenseMotion.Sample(b).Phase);
                b.Advance(1); Check.Equal(DefenseMotionPhase.Hold, PlayerDefenseMotion.Sample(b).Phase);
                Check.Equal(0d, PlayerDefenseMotion.Sample(b).Smear);
                double angle = DefenseArtifactMotion.Sample(b).Rotation;
                b.Advance(1.4); Check.Equal(angle, DefenseArtifactMotion.Sample(b).Rotation);
                Check.Equal(PlayerDefenseMotion.Pose(mask, .4, true).Pose, PlayerDefenseMotion.Sample(b).Pose);
                Check.Equal("Base Layer.Guard", FiveLaneArtTimeline.Player(b).State);
            }
        }

        [Test] public void OffensiveCallDoesNotRestartTheBodyAndPauseFreezesTheExactFrame()
        {
            var b = Battle(); b.Press(4, .4); b.Press(7, .4); b.Advance(.48);
            var before = PlayerDefenseMotion.Sample(b); b.Press(0, .48);
            var after = PlayerDefenseMotion.Sample(b);
            Check.Equal(before.Pose, after.Pose); Check.Equal(before.Age, after.Age); Check.Equal(6m, b.TotalDamage);
            Check.Equal("Base Layer.Guard", FiveLaneArtTimeline.Player(b).State);
            b.Pause(); b.Advance(20);
            Check.Equal(after.Age, PlayerDefenseMotion.Sample(b).Age); Check.Equal(after.Pose, PlayerDefenseMotion.Sample(b).Pose);
        }

        [Test] public void GuardReleaseRecoversWithoutReplayingASmearOrLeavingActiveContacts()
        {
            var b = Battle(); b.Press(4, .2); b.Press(7, .2); b.Advance(1);
            b.Release(4, 1); b.Release(7, 1);
            Check.Equal(DefenseMotionPhase.Recover, PlayerDefenseMotion.Sample(b).Phase);
            Check.Equal(0d, PlayerDefenseMotion.Sample(b).Smear); Check.Equal(0, DefenseArtifactMotion.Mask(b));
            b.Advance(1.3); Check.Equal(DefenseMotionPhase.Ready, PlayerDefenseMotion.Sample(b).Phase);
        }

        [Test] public void AddingAnotherDirectionReplacesTheBodyStrokeWithoutAddingTheOlderHold()
        {
            var b = Battle(); b.Press(4, .2); b.Press(5, 1); b.Advance(1.08);
            Check.Equal(2, PlayerDefenseMotion.Sample(b).Directions);
            Check.Equal(DefenseMotionPhase.Smear, PlayerDefenseMotion.Sample(b).Phase);
            b.Release(5, 1.2); b.Advance(1.6);
            Check.Equal(1, PlayerDefenseMotion.Sample(b).Directions);
            Check.Equal(DefenseMotionPhase.Hold, PlayerDefenseMotion.Sample(b).Phase);
            Check.Equal(1, DefenseArtifactMotion.Mask(b));
        }

        [Test] public void LeftHoldKeepsSpinningInPlaceWhileTheBodyDefendsUpAndReturns()
        {
            var b = Battle(incoming: new[] { new BeatAttack("held", 1, 8, 4, defenseDirection: 0),
                new BeatAttack("tap", 2, 5, defenseDirection: 2) });
            b.Press(4, 1); b.Advance(1.8);
            Check.Equal(DefenseMotionPhase.Hold, PlayerDefenseMotion.Sample(b).Phase);
            b.Advance(2); var before = DefenseHoldMotion.Sample(b, 0);
            b.Press(6, 2); b.Press(0, 2);
            var after = DefenseHoldMotion.Sample(b, 0);
            Check.True(before.Active, "Left contact before UP"); Check.True(after.Active, "Left contact after UP"); Check.Equal(before.Rotation, after.Rotation);
            Check.Equal(before.Position.X, after.Position.X); Check.Equal(before.Position.Y, after.Position.Y);
            Check.Equal(1, DefenseHoldMotion.Mask(b)); Check.False(DefenseHoldMotion.Sample(b, 2).Active);
            b.Advance(2.08); Check.Equal(4, PlayerDefenseMotion.Sample(b).Directions);
            Check.Equal(DefenseMotionPhase.Smear, PlayerDefenseMotion.Sample(b).Phase);
            Check.True(DefenseHoldMotion.Sample(b, 0).Rotation != before.Rotation, "The contact must keep spinning");
            Check.Equal(100m, b.PlayerHealth); Check.Equal(6m, b.TotalDamage);
            b.Release(6, 2.2); b.Advance(2.5);
            Check.Equal(1, PlayerDefenseMotion.Sample(b).Directions);
            Check.Equal(DefenseMotionPhase.Hold, PlayerDefenseMotion.Sample(b).Phase);
            Check.Equal(1, DefenseHoldMotion.Mask(b));
            b.Release(4, 3); Check.Equal(0, DefenseHoldMotion.Mask(b));
            Check.True(PlayerDefenseMotion.Sample(b).Releasing, "Release should recover the left guard: " + PlayerDefenseMotion.Sample(b).Phase);
            Check.Equal(1, PlayerDefenseMotion.Sample(b).Directions);
        }

        [Test] public void HoldOrnamentsAreIndependentAndPauseFreezesBodyAndEveryRotation()
        {
            var b = Battle(incoming: Enumerable.Range(0, 4).Select(d => new BeatAttack("held" + d, 1, 8, 2, defenseDirection: d)).ToArray());
            for (int d = 0; d < 4; d++) b.Press(4 + d, 1);
            b.Advance(1.125); Check.Equal(15, DefenseHoldMotion.Mask(b));
            b.Release(5, 1.125); Check.Equal(13, DefenseHoldMotion.Mask(b));
            var before = DefenseHoldMotion.Sample(b, 0); var body = PlayerDefenseMotion.Sample(b);
            b.Pause(); b.Advance(30); b.Release(4, 30);
            Check.Equal(13, DefenseHoldMotion.Mask(b)); Check.Equal(before.Rotation, DefenseHoldMotion.Sample(b, 0).Rotation);
            Check.Equal(body.Age, PlayerDefenseMotion.Sample(b).Age);
            b.Resume(); b.Advance(3); Check.Equal(0, DefenseHoldMotion.Mask(b));
        }

        [Test] public void HoldingAnUnavailableKeyOrPreviewingANoteDoesNotCreateAContact()
        {
            var b = Battle(incoming: new[] { new BeatAttack("held", 1, 8, 3, defenseDirection: 0) });
            b.Advance(.1); Check.Equal(0, DefenseHoldMotion.Mask(b));
            b.Press(4, .2); Check.Equal(0, DefenseHoldMotion.Mask(b));
            b.Release(4, .3); b.Press(4, 1); b.Advance(1.1);
            Check.True(b.IsInputHeld(4)); Check.Equal(0, DefenseHoldMotion.Mask(b));
            Check.False(b.HasActiveHoldDefense(-1)); Check.False(b.HasActiveHoldDefense(8));
            decimal health = b.PlayerHealth, blocked = b.TotalBlocked;
            for (int i = 0; i < 100; i++) DefenseHoldMotion.Sample(b, 0);
            Check.Equal(health, b.PlayerHealth); Check.Equal(blocked, b.TotalBlocked);
        }

        [Test] public void ANewInputInterruptsRecoveryWithoutAnIdleFrameOrAnOldDirection()
        {
            var b = Battle(); b.Press(4, .2); b.Release(4, .7);
            Check.True(PlayerDefenseMotion.Sample(b).Releasing);
            b.Press(6, .72);
            Check.Equal(DefenseMotionPhase.Windup, PlayerDefenseMotion.Sample(b).Phase);
            Check.Equal(4, PlayerDefenseMotion.Sample(b).Directions);
            Check.False(PlayerDefenseMotion.Sample(b).Releasing);
        }

        [Test] public void EveryEmptyQuickTapFinishesItsSwingIncludingDuringCooldown()
        {
            for (int mask = 1; mask <= 15; mask++)
            {
                var b = Battle();
                foreach (double start in new[] { .2, .8 })
                {
                    for (int d = 0; d < 4; d++) if ((mask & 1 << d) != 0) b.Press(4 + d, start);
                    for (int d = 0; d < 4; d++) if ((mask & 1 << d) != 0) b.Release(4 + d, start + .01);
                    Check.Equal(DefenseMotionPhase.Windup, PlayerDefenseMotion.Sample(b).Phase);
                    var ages = new[] { .08, .18, .33, .6 };
                    var phases = new[] { DefenseMotionPhase.Smear, DefenseMotionPhase.Contact, DefenseMotionPhase.Recover, DefenseMotionPhase.Ready };
                    for (int i = 0; i < ages.Length; i++)
                    {
                        b.Advance(start + ages[i]); var frame = PlayerDefenseMotion.Sample(b);
                        Check.Equal(phases[i], frame.Phase);
                        Check.Equal(i == 3 ? 0 : mask, frame.Directions);
                        Check.Equal(0, DefenseHoldMotion.Mask(b)); Check.Equal(0, DefenseArtifactMotion.Mask(b));
                    }
                }
                for (int d = 0; d < 4; d++) if ((mask & 1 << d) != 0)
                {
                    Check.Equal(PhraseLanePhase.Cooldown, b.LaneAt(4 + d).Phase);
                    Check.True(Math.Abs(b.LaneAt(4 + d).ReadyAtBeat - 2.21) < .000001, "Whiffs must not reset cooldown.");
                }
                Check.Equal(0m, b.TotalBlocked); Check.Equal(0m, b.TotalReduced);
                Check.Equal(0, b.PerfectCount); Check.Equal(0, b.MissCount); Check.Equal(100m, b.PlayerHealth);
            }
            // A release near the end of the swing must not replay its recovery.
            var ending = Battle(); ending.Press(4, .2); ending.Release(4, .67);
            Check.Equal(DefenseMotionPhase.Recover, PlayerDefenseMotion.Sample(ending).Phase);
            Check.False(PlayerDefenseMotion.Sample(ending).Releasing);
            ending.Advance(.69); Check.Equal(DefenseMotionPhase.Ready, PlayerDefenseMotion.Sample(ending).Phase);
        }

        [Test] public void MissedParryReleasesSwingWithoutCreatingSuccessfulResults()
        {
            // Early, on time without a target, and expired in this or an earlier frame.
            foreach (bool advanceFirst in new[] { false, true })
            foreach (double release in new[] { .7, 1.5, 1.9 })
            {
                var b = Battle(defense: "tower-shield"); b.Press(6, 0);
                if (advanceFirst) b.Advance(release);
                b.Release(6, release);
                Check.Equal(1, b.MissCount); Check.Equal(RhythmGrade.Miss, b.LaneAt(6).LastGrade);
                Check.Equal(DefenseMotionPhase.Windup, PlayerDefenseMotion.Sample(b).Phase);
                double cooldown = b.LaneAt(6).ReadyAtBeat;
                b.Advance(release + .08); var frame = PlayerDefenseMotion.Sample(b);
                Check.Equal(4, frame.Directions); Check.Equal(DefenseMotionPhase.Smear, frame.Phase);
                Check.Equal(0m, b.TotalBlocked); Check.Equal(0, b.PerfectCount); Check.Equal(0, DefenseHoldMotion.Mask(b));
                Check.Equal(cooldown, b.LaneAt(6).ReadyAtBeat);
                b.Advance(release + .6); Check.Equal(DefenseMotionPhase.Ready, PlayerDefenseMotion.Sample(b).Phase);
            }
        }

        [Test] public void WhiffInAnotherDirectionDoesNotStealAnExistingHoldContact()
        {
            var b = Battle(incoming: new[] { new BeatAttack("held", 1, 8, 4, defenseDirection: 0) });
            b.Press(6, .2); b.Release(6, .21); b.Press(4, 1);
            double cooldown = b.LaneAt(6).ReadyAtBeat;
            b.Press(6, 1.8); b.Release(6, 1.81); b.Advance(1.88);
            Check.Equal(4, PlayerDefenseMotion.Sample(b).Directions);
            Check.Equal(DefenseMotionPhase.Smear, PlayerDefenseMotion.Sample(b).Phase);
            Check.Equal(1, DefenseHoldMotion.Mask(b)); Check.Equal(1, DefenseArtifactMotion.Mask(b));
            Check.Equal(cooldown, b.LaneAt(6).ReadyAtBeat);
            b.Advance(2.4); Check.Equal(1, PlayerDefenseMotion.Sample(b).Directions);
            Check.Equal(DefenseMotionPhase.Hold, PlayerDefenseMotion.Sample(b).Phase);
            Check.Equal(1, DefenseHoldMotion.Mask(b)); Check.Equal(100m, b.PlayerHealth);
        }

        [Test] public void OnlyNewLiveDefenseEdgesRestartAWhiff()
        {
            var b = Battle(); b.Press(4, .2); b.Release(4, .21); b.Press(4, .8);
            b.Press(4, .88); b.Press(0, .88);
            Check.Equal(.8, b.LaneAt(4).LastInputBeat);
            Check.Equal(DefenseMotionPhase.Smear, PlayerDefenseMotion.Sample(b).Phase);
            b.Pause(); b.Press(6, 20); b.Release(4, 20);
            Check.Equal(double.NegativeInfinity, b.LaneAt(6).LastInputBeat);
            Check.Equal(DefenseMotionPhase.Smear, PlayerDefenseMotion.Sample(b).Phase);
            b.Resume(); b.Release(4, .9); b.Release(4, .91); b.Advance(1.4);
            Check.Equal(DefenseMotionPhase.Ready, PlayerDefenseMotion.Sample(b).Phase);
            b.Stop(); b.Press(6, 2);
            Check.Equal(double.NegativeInfinity, b.LaneAt(6).LastInputBeat);
            Check.Equal(DefenseMotionPhase.Ready, PlayerDefenseMotion.Sample(b).Phase);
        }

        [Test] public void LateWhiffInterruptsAnOlderHitReactionButLaterDamageStillShowsHit()
        {
            var b = Battle(incoming: new[] { new BeatAttack("first", 1, 5, defenseDirection: 0),
                new BeatAttack("second", 1.5, 5, defenseDirection: 0) });
            b.Advance(1.3); Check.Equal("Base Layer.Hit", FiveLaneArtTimeline.Player(b).State);
            b.Press(6, 1.3); b.Release(6, 1.31); b.Advance(1.38);
            Check.Equal(DefenseMotionPhase.Smear, PlayerDefenseMotion.Sample(b).Phase);
            Check.Equal("Base Layer.Guard", FiveLaneArtTimeline.Player(b).State);
            Check.Equal(95m, b.PlayerHealth); Check.Equal(0m, b.TotalBlocked);
            b.Advance(1.75);
            Check.Equal(DefenseMotionPhase.Hit, PlayerDefenseMotion.Sample(b).Phase);
            Check.Equal("Base Layer.Hit", FiveLaneArtTimeline.Player(b).State);
            Check.Equal(90m, b.PlayerHealth);
        }

        [Test] public void UnansweredAttacksNeverStartADefenseSwingEvenWithoutAnArtifact()
        {
            var attack = new[] { new BeatAttack("enemy", 1, 5, defenseDirection: 0) };
            var bare = new FiveLaneBattle(new[] { new WeaponState("dagger") }, 120, 64,
                attack, new StageHealth(10000), 100, 100, separateHands: true);
            foreach (var b in new[] { Battle(incoming: attack), bare })
            {
                b.Advance(1.3); Check.Equal(DefenseMotionPhase.Hit, PlayerDefenseMotion.Sample(b).Phase);
                Check.Equal("Base Layer.Hit", FiveLaneArtTimeline.Player(b).State);
                b.Advance(1.8); Check.Equal(DefenseMotionPhase.Ready, PlayerDefenseMotion.Sample(b).Phase);
                Check.Equal("Base Layer.Idle", FiveLaneArtTimeline.Player(b).State);
            }
        }

        [Test] public void AllFifteenActionsHaveTheirOwnClipAndAllSixParts()
        {
            var clips = Enumerable.Range(1, 15).Select(DefenseMotionCatalog.For).ToArray();
            Check.Equal(15, clips.Select(c => c.Id).Distinct().Count());
            Check.Equal(15, clips.Select(c => c.Atlas + ":" + c.Row).Distinct().Count());
            foreach (var clip in clips)
            {
                var times = new[] { .02, .08, .18, .24, .32, .6 };
                for (int part = 0; part < 6; part++)
                    Check.Equal(part, DefenseMotionCatalog.Part(PlayerDefenseMotion.Pose(clip.Directions, times[part], true)));
            }
        }

        [Test] public void BodyAnimationDoesNotDelayTheRealParryOrChangeBowPreparation()
        {
            var attacks = Enumerable.Range(0, 4).Select(d => new BeatAttack("e" + d, 1, 5, defenseDirection: d)).ToArray();
            var b = Battle("bow", attacks); b.Press(0, 0); b.Release(0, 0); b.Press(0, b.LaneAt(0).NextBeat);
            for (int d = 0; d < 4; d++) b.Press(4 + d, 1);
            Check.Equal(20m, b.TotalBlocked); Check.Equal(100m, b.PlayerHealth);
            Check.Equal(RangedWeaponPose.Prepare, FiveLaneArtTimeline.Weapon(b.LaneAt(0), b.Beat));
            Check.Equal(DefenseMotionPhase.Windup, PlayerDefenseMotion.Sample(b).Phase);
            Check.Equal("Base Layer.Guard", FiveLaneArtTimeline.Player(b).State);
        }

        [Test] public void OffensiveCallsAloneLeaveTheBodyReadyAndDirectionsHaveDifferentContactPoses()
        {
            var b = Battle(); b.Press(0, .3);
            Check.Equal(DefenseMotionPhase.Ready, PlayerDefenseMotion.Sample(b).Phase);
            Check.Equal("Base Layer.Idle", FiveLaneArtTimeline.Player(b).State);
            var poses = new[] { 1, 2, 4, 8, 9, 6, 15 }.Select(mask => PlayerDefenseMotion.Pose(mask, .2, true).Pose).ToArray();
            Check.Equal(7, poses.Distinct().Count());
        }
    }
}
