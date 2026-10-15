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
        private static FiveLaneBattle Battle(string attack = "dagger", BeatAttack[] incoming = null) =>
            new FiveLaneBattle(new[] { new WeaponState(attack), new WeaponState("heater-shield") }, 120, 64,
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

        [Test] public void AddingAnotherHeldDirectionStartsAChordAndReleasingItLeavesTheRemainingGuard()
        {
            var b = Battle(); b.Press(4, .2); b.Press(5, 1); b.Advance(1.08);
            Check.Equal(3, PlayerDefenseMotion.Sample(b).Directions);
            Check.Equal(DefenseMotionPhase.Smear, PlayerDefenseMotion.Sample(b).Phase);
            b.Release(5, 1.2); b.Advance(1.6);
            Check.Equal(1, PlayerDefenseMotion.Sample(b).Directions);
            Check.Equal(DefenseMotionPhase.Hold, PlayerDefenseMotion.Sample(b).Phase);
            Check.Equal(1, DefenseArtifactMotion.Mask(b));
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
