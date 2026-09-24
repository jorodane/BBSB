using System;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class FiveLaneArtTimelineTests
    {
        private static FiveLaneBattle Battle(string weapon, params BeatAttack[] attacks) =>
            new FiveLaneBattle(new[] { new WeaponState(weapon) }, 120, 32, attacks, new StageHealth(10000), 100, 100);
        private static void Tap(FiveLaneBattle battle, double beat) { battle.Press(0, beat); battle.Release(0, beat); }

        [Test] public void BowDrawDoesNotPlayAnAttackUntilTheConfirmedShotDealsDamage()
        {
            var b = Battle("bow"); Tap(b, 0); b.Press(0, 1);
            Check.Equal("Base Layer.Bow", FiveLaneArtTimeline.Player(b).State);
            Check.True(double.IsNegativeInfinity(b.Lanes[0].LastDamageBeat));
            b.Release(0, 2); Check.Equal("Base Layer.Bow", FiveLaneArtTimeline.Player(b).State);
            Check.True(double.IsNegativeInfinity(b.Lanes[0].LastDamageBeat));
            Tap(b, 3); Check.Equal(3.0, b.Lanes[0].LastDamageBeat);
            Check.Equal("Base Layer.TapImpact", FiveLaneArtTimeline.Player(b).State);
            b.Advance(3.5); Check.Equal("Base Layer.Idle", FiveLaneArtTimeline.Player(b).State);
        }
        [Test] public void FailedDrawAndMistimedDaggerCannotTriggerAnAttackEffect()
        {
            var bow = Battle("bow"); Tap(bow, 0); bow.Press(0, 1); bow.Release(0, 1.5);
            Check.True(double.IsNegativeInfinity(bow.Lanes[0].LastDamageBeat));
            Check.Equal("Base Layer.Idle", FiveLaneArtTimeline.Player(bow).State);
            var dagger = Battle("dagger"); Tap(dagger, 0); Tap(dagger, 1); Tap(dagger, 1.75);
            Check.Equal(1.0, dagger.Lanes[0].LastDamageBeat);
            Check.Equal("Base Layer.Idle", FiveLaneArtTimeline.Player(dagger).State);
        }
        [Test] public void HeaterGuardPoseLastsForTheActualHoldAndStopsOnRelease()
        {
            var b = Battle("heater-shield"); b.Press(0, .18);
            Check.Equal("Base Layer.Guard", FiveLaneArtTimeline.Player(b).State);
            b.Advance(1.6); Check.Equal("Base Layer.Guard", FiveLaneArtTimeline.Player(b).State);
            b.Release(0, 1.6); Check.Equal("Base Layer.Idle", FiveLaneArtTimeline.Player(b).State);
            Check.True(double.IsNegativeInfinity(b.Lanes[0].LastDamageBeat));
        }
        [Test] public void EarlyAndLateParriesTimestampTheResolutionRatherThanTheScheduledImpact()
        {
            foreach (double at in new[] { .9, 1.1 })
            {
                var b = Battle("heater-shield", new BeatAttack("enemy", 1, 10)); b.Press(0, at);
                var attack = b.Incoming.Single(a => a.Beat == 1);
                Check.Equal(IncomingAttackState.Blocked, attack.State); Check.Equal(at, attack.ResolvedBeat);
                Check.True(FiveLaneArtTimeline.Recent(b.Beat, attack.ResolvedBeat, .4));
                b.Pause(); b.Advance(20);
                Check.Equal(at, b.Beat); Check.Equal("Base Layer.Guard", FiveLaneArtTimeline.Player(b).State);
                Check.Equal(at, attack.ResolvedBeat);
            }
        }
        [Test] public void EnemyInstancesTelegraphIndependentlyAndOffbeatProjectilesArriveExactly()
        {
            var b = Battle("dagger", new BeatAttack("one", 2.5, 10), new BeatAttack("two", 4, 10));
            b.Advance(1.6); Check.Equal("Base Layer.Call", FiveLaneArtTimeline.Monster(b, "one").State);
            Check.Equal("Base Layer.Idle", FiveLaneArtTimeline.Monster(b, "two").State);
            Check.Equal(0f, FiveLaneArtTimeline.ProjectileProgress(2.5, 2.1));
            b.Advance(2.2); Check.Equal("Base Layer.Attack", FiveLaneArtTimeline.Monster(b, "one").State);
            Check.True(FiveLaneArtTimeline.ProjectileProgress(2.5, 2.2) > 0);
            Check.Equal(1f, FiveLaneArtTimeline.ProjectileProgress(2.5, 2.5));
            Check.Equal(1f, FiveLaneArtTimeline.ProjectileProgress(2.5, 2.6));
            b.Pause(); var frame = FiveLaneArtTimeline.Monster(b, "one"); b.Advance(50);
            Check.Equal(frame.Age, FiveLaneArtTimeline.Monster(b, "one").Age);
        }
        [Test] public void DamageResolutionDrivesTheHurtPoseWithoutReplacingTheNextTelegraph()
        {
            var b = Battle("dagger", new BeatAttack("enemy", 1, 10)); b.Advance(1.3);
            var hit = b.Incoming.Single(a => a.Beat == 1);
            Check.Equal(IncomingAttackState.Hit, hit.State); Check.Equal(b.LastHitBeat, hit.ResolvedBeat);
            Check.Equal("Base Layer.Hit", FiveLaneArtTimeline.Player(b).State);
            b.Advance(1.8); Check.Equal("Base Layer.Idle", FiveLaneArtTimeline.Player(b).State);
        }
        [Test] public void ConfirmationEffectsAppearOnceOnlyForPromotedPreviewsAndFreezeOnPause()
        {
            var b = Battle("bow"); Tap(b, 0); b.Press(0, 1); var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
            Check.Equal(0, timeline.Confirmed.Count); b.Release(0, 2); timeline.Refresh(b);
            Check.Equal(1, timeline.Confirmed.Count); Check.Equal(3.0, timeline.Confirmed[0].Note.Beat);
            Check.Equal(2.0, timeline.Confirmed[0].ConfirmedAt); Check.Equal(0, timeline.Broken.Count);
            timeline.Refresh(b); Check.Equal(1, timeline.Confirmed.Count);
            b.Pause(); b.Advance(10); timeline.Refresh(b); Check.Equal(1, timeline.Confirmed.Count);
            b.Resume(); b.Advance(2.5); timeline.Refresh(b); Check.Equal(0, timeline.Confirmed.Count);
            var failed = Battle("bow"); Tap(failed, 0); failed.Press(0, 1); timeline.Refresh(failed);
            failed.Release(0, 1.5); timeline.Refresh(failed);
            Check.Equal(0, timeline.Confirmed.Count); Check.True(timeline.Broken.Count > 0);
        }
    }
}
