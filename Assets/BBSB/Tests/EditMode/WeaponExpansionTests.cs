using System;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class WeaponExpansionTests
    {
        private static void Tap(FiveLaneBattle b, int slot, double beat) { b.Press(slot, beat); b.Release(slot, beat); }
        private static FiveLaneBattle Battle(params WeaponState[] weapons) => new FiveLaneBattle(weapons, 120, 64,
            Array.Empty<BeatAttack>(), new StageHealth(100000), 30, 100);
        private static void PlayCurrent(FiveLaneBattle b, PhraseLane lane)
        {
            var phrase = lane.Phrase; double start = lane.StartBeat;
            // Called after the opening has been played; finish every remaining fixed note.
            for (int i = lane.NextNote; i < phrase.Notes.Count; i++)
            {
                var note = phrase.Notes[i]; int slot = lane.SlotForNote(i);
                b.Press(slot, start + note.Beat);
                b.Release(slot, start + note.Beat + note.HoldBeats);
            }
        }

        [Test] public void ExpansionAddsTenOrdinaryAndThreeExclusiveWeaponsToTheRewardPool()
        {
            Check.Equal(13, WeaponExpansion.All.Count);
            Check.Equal(10, WeaponExpansion.All.Count(e => !e.Definition.ExclusiveAttribute.HasValue));
            Check.Equal(2, WeaponExpansion.All.Count(e => e.Definition.ExclusiveAttribute == WeaponAttribute.Dual));
            Check.Equal(1, WeaponExpansion.All.Count(e => e.Definition.ExclusiveAttribute == WeaponAttribute.Chaos));
            var seen = new System.Collections.Generic.HashSet<string>(); var random = new SeededRandom(42);
            for (int i = 0; i < 4096; i++) seen.Add(ContentCatalog.Pick(RewardKind.Weapon, random).Id);
            foreach (var entry in WeaponExpansion.All)
            {
                Check.True(seen.Contains(entry.Definition.Id));
                Check.Equal(entry.Name, ContentCatalog.Find(entry.Definition.Id).Name);
                Check.True(entry.Definition.RequiredLanes >= 1 && entry.Definition.RequiredLanes <= 3);
                var state = new WeaponState(entry.Definition.Id);
                Check.Equal(entry.Definition.DefaultAttribute, state.Attribute);
                if (!entry.Definition.ExclusiveAttribute.HasValue) continue;
                for (int i = 0; i < 16; i++) Check.Equal(state.Attribute, WeaponAttributes.Roll(state.DefinitionId, random));
                bool rejected = false;
                try { new WeaponState(state.DefinitionId, WeaponAttribute.Light); } catch (ArgumentException) { rejected = true; }
                Check.True(rejected);
            }
        }

        [Test] public void EveryNewWeaponCanCompleteFromEveryStartingLineAndAllowedBeatSide()
        {
            foreach (var entry in WeaponExpansion.All)
            foreach (var attribute in new[] { WeaponAttribute.Light, WeaponAttribute.Dark, WeaponAttribute.Dual, WeaponAttribute.Chaos })
            {
                if (entry.Definition.ExclusiveAttribute.HasValue ? attribute != entry.Definition.ExclusiveAttribute : attribute == WeaponAttribute.Chaos) continue;
                for (int offset = 0; offset < entry.Definition.RequiredLanes; offset++)
                foreach (double at in new[] { .1, .49 })
                {
                    var b = Battle(new WeaponState(entry.Definition.Id, attribute));
                    b.Press(offset, at); var lane = b.Lanes[0];
                    double end = lane.StartBeat + lane.Phrase.Notes[0].HoldBeats;
                    b.Release(offset, Math.Max(at, end));
                    int count = lane.Phrase.Notes.Count;
                    if (lane.Phase == PhraseLanePhase.Playing) PlayCurrent(b, lane);
                    Check.Equal(count, b.PerfectCount); Check.Equal(0, b.MissCount); Check.Equal(0, b.HalfMissCount);
                    Check.Equal(offset, lane.StartOffset);
                    Check.Equal(entry.Definition.Id == "chaos-pendulum", lane.CanRepeat);
                }
            }
        }

        [Test] public void HalberdChangesItsSweepRouteForTheStartingOffset()
        {
            int[][] routes = { new[] { 0, 1, 2 }, new[] { 1, 0, 2 }, new[] { 2, 1, 0 } };
            for (int start = 0; start < 3; start++)
            {
                var b = Battle(new WeaponState("halberd")); Tap(b, start, 0);
                for (int i = 0; i < 3; i++) Check.Equal(routes[start][i], b.Lanes[0].SlotForNote(i));
                PlayCurrent(b, b.Lanes[0]); Check.Equal(58m, b.TotalDamage);
            }
        }

        [Test] public void TwilightStaffUsesHealingOnBeatAndDamageOffbeat()
        {
            foreach (int offset in new[] { 0, 1 })
            foreach (bool dark in new[] { false, true })
            {
                var b = Battle(new WeaponState("twilight-staff")); Tap(b, offset, dark ? .5 : 0);
                PlayCurrent(b, b.Lanes[0]);
                Check.Equal(dark ? 36m : 0m, b.TotalDamage);
                Check.Equal(dark ? 30m : 36m, b.PlayerHealth);
                Check.Equal(PhraseLanePhase.Cooldown, b.Lanes[0].Phase);
            }
        }

        [Test] public void WarDrumSchedulesBothOuterNeighborsAndDoesNotAutoplayTheirNotes()
        {
            var b = Battle(new WeaponState("dagger"), new WeaponState("war-drum"), new WeaponState("dual-swords"));
            Tap(b, 0, 0); Tap(b, 0, .5); // Fail the dagger so the drum must bypass its cooldown.
            Tap(b, 1, .5); // Light start snaps to 1.
            Tap(b, 2, 1.5); Tap(b, 3, 2);
            Check.Equal(2, b.ScheduledStarts.Count); Check.True(b.ScheduledStarts.All(s => s.Beat == 3));
            b.Advance(3); Check.Equal(0, b.LaneAt(4).Activations);
            Check.Equal(3.0, b.LaneAt(0).NextBeat); Check.Equal(3.0, b.LaneAt(4).NextBeat);
            Tap(b, 0, 3); Tap(b, 4, 3); Check.Equal(1, b.LaneAt(4).Activations);
        }

        [Test] public void TuningForkReducesCooldownButLeavesActivePatternsUntouched()
        {
            var b = Battle(new WeaponState("dagger"), new WeaponState("tuning-fork"), new WeaponState("sword"));
            Tap(b, 0, 0); Tap(b, 3, 0); Tap(b, 0, .5);
            Check.Equal(2.5, b.LaneAt(0).ReadyAtBeat);
            Tap(b, 1, .5); Tap(b, 3, 1); Tap(b, 2, 2);
            Check.Equal(PhraseLanePhase.Ready, b.LaneAt(0).Phase);
            Check.Equal(2.0, b.LaneAt(0).ReadyAtBeat);
            Check.Equal(2.0, b.LaneAt(3).NextBeat); Check.Equal(2, b.LaneAt(3).Activations);
            Tap(b, 3, 2); Check.Equal(3, b.LaneAt(3).Activations);
        }

        [Test] public void BannerEmpowersOneActualAttackPerNeighborWithoutMultiplyingItsFootprint()
        {
            var b = Battle(new WeaponState("dagger"), new WeaponState("war-banner"), new WeaponState("staff"));
            b.Press(1, 0); b.Release(1, 1); Tap(b, 2, 1.5);
            Check.Equal(1.5m, b.LaneAt(0).PendingDamageMultiplier);
            Check.True(ReferenceEquals(b.LaneAt(3), b.LaneAt(4)));
            Check.Equal(1.5m, b.LaneAt(4).PendingDamageMultiplier);
            Tap(b, 0, 2); Tap(b, 4, 2); Tap(b, 4, 2.5);
            Check.Equal(9m, b.LaneAt(0).DamageDealt);
            Check.Equal(20m, b.LaneAt(4).DamageDealt); // Staff 8 * 1.5, then unboosted 8.
            Check.Equal(1m, b.LaneAt(4).PendingDamageMultiplier);
            Tap(b, 0, 4); Check.Equal(15m, b.LaneAt(0).DamageDealt);
        }

        [Test] public void MirrorLightRecoversAndHealsWhileDarkBoostsAndAttacks()
        {
            foreach (bool dark in new[] { false, true })
            {
                var b = Battle(new WeaponState("dagger"), new WeaponState("eclipse-mirror"));
                Tap(b, 0, 0); Tap(b, 0, .5); Tap(b, 1, dark ? .5 : 1);
                Check.Equal(dark ? 2.5 : 1.5, b.LaneAt(0).ReadyAtBeat);
                Check.Equal(dark ? 1.5m : 1m, b.LaneAt(0).PendingDamageMultiplier);
                Tap(b, 1, dark ? 1.5 : 2);
                Check.Equal(dark ? 30m : 32m, b.PlayerHealth);
                Check.Equal(dark ? 16m : 6m, b.TotalDamage);
            }
        }

        [Test] public void SupportDoesNotJumpAcrossAnEmptyLineAndCanReachAnExtensionFootprint()
        {
            var target = new WeaponState("staff"); var source = new WeaponState("eclipse-mirror");
            var b = new FiveLaneBattle(new[] { target, source }, 120, 64, Array.Empty<BeatAttack>(), new StageHealth(1000), 100, 100,
                placements: new[] { new WeaponPlacement(target, BattleInputLayout.Left, 0), new WeaponPlacement(source, 1) },
                extensions: InputExtensions.Left);
            Tap(b, 1, .5); Check.Equal(1.5m, b.LaneAt(BattleInputLayout.Left).PendingDamageMultiplier);
            var gap = new FiveLaneBattle(new[] { target, source }, 120, 64, Array.Empty<BeatAttack>(), new StageHealth(1000), 100, 100,
                placements: new[] { new WeaponPlacement(target, 0, 1), new WeaponPlacement(source, 3) });
            Tap(gap, 3, .5); Check.Equal(1m, gap.LaneAt(0).PendingDamageMultiplier);
        }

        [Test] public void EmpowerExpiresAndReapplicationKeepsOnlyTheStrongerCharge()
        {
            var left = new WeaponState("war-banner"); var middle = new WeaponState("dagger");
            var right = new WeaponState("eclipse-mirror", WeaponRarity.Common, 2);
            var b = Battle(left, middle, right);
            b.Press(0, 0); b.Release(0, 1); Tap(b, 1, 1.5);
            Tap(b, 3, 1.5); Check.Equal(1.75m, b.LaneAt(2).PendingDamageMultiplier);
            Tap(b, 3, 2.5); Tap(b, 2, 4.5); // Exactly at expiry: unboosted.
            Check.Equal(6m, b.LaneAt(2).DamageDealt); Check.Equal(1m, b.LaneAt(2).PendingDamageMultiplier);
        }

        [Test] public void HealingAndSupportDoNotConsumeTheNextDamageCharge()
        {
            var b = Battle(new WeaponState("eclipse-mirror"), new WeaponState("twilight-staff"));
            Tap(b, 0, .5); Tap(b, 1, 1);
            Check.Equal(32m, b.PlayerHealth); Check.Equal(1.5m, b.LaneAt(1).PendingDamageMultiplier);
            Tap(b, 0, 1.5); b.Press(2, 2); b.Release(2, 3);
            Check.Equal(36m, b.PlayerHealth); Check.Equal(1.5m, b.LaneAt(1).PendingDamageMultiplier);
        }

        [Test] public void ChaosPendulumMaintainsSixBeatsThenBridgesAcrossThreeLinesAndSwitchesSide()
        {
            var weapon = new WeaponState("chaos-pendulum"); var normal = WeaponPhraseSet.Uniform(weapon);
            var set = new WeaponPhraseSet(weapon, normal.LightStarts, normal.DarkStarts, normal.LightTransitions,
                normal.DarkTransitions, new ChaosRules(transitionChance: 1));
            var b = new FiveLaneBattle(new[] { weapon }, 120, 64, Array.Empty<BeatAttack>(), new StageHealth(10000), 100, 100, phraseSets: new[] { set });
            var lane = b.Lanes[0];
            for (int beat = 0; beat <= 8; beat++)
            {
                Tap(b, beat % 3, beat); Check.Equal(beat == 8, lane.IsTransition);
            }
            Check.Equal(9.0, lane.StartBeat); Check.Equal(5, lane.Phrase.Notes.Count);
            Tap(b, 0, 9); Tap(b, 1, 9.5); Tap(b, 2, 10); Tap(b, 1, 10.5); Tap(b, 0, 11);
            Check.Equal(WeaponBeatSide.Dark, lane.ActiveSide); Check.Equal(11.5, lane.NextBeat);
            Check.Equal(0, b.MissCount); Check.True(lane.CanRepeat);
            Check.Equal(186m, b.TotalDamage); // (3 * 28 + 40) * 1.5.
        }

        [Test] public void MissedFixedNoteLeavesTheRestOfTheNewPatternPlayable()
        {
            var b = Battle(new WeaponState("chakram")); Tap(b, 0, 0); b.Advance(.75);
            Check.Equal(1, b.MissCount); Check.True(b.Lanes[0].IsNoteVisible(2)); Check.True(b.Lanes[0].IsNoteVisible(3));
            Tap(b, 0, 1); Tap(b, 1, 1.5); Check.Equal(32m, b.TotalDamage);
            Check.Equal(PhraseLanePhase.Cooldown, b.Lanes[0].Phase);
        }

        [Test] public void BowArtworkStaysDrawnAfterTheHoldAndReleasesOnlyOnAnActualShot()
        {
            var b = Battle(new WeaponState("bow")); var lane = b.Lanes[0];
            Check.Equal(RangedWeaponPose.Idle, FiveLaneArtTimeline.Weapon(lane, b.Beat));
            b.Press(0, 0); Check.Equal(RangedWeaponPose.Prepare, FiveLaneArtTimeline.Weapon(lane, b.Beat));
            b.Release(0, 1); b.Advance(1.5);
            Check.Equal(RangedWeaponPose.Prepare, FiveLaneArtTimeline.Weapon(lane, b.Beat));
            Tap(b, 0, 2); Check.Equal(RangedWeaponPose.Release, FiveLaneArtTimeline.Weapon(lane, b.Beat));
            b.Advance(2.2); Check.Equal(RangedWeaponPose.Idle, FiveLaneArtTimeline.Weapon(lane, b.Beat));
            var failed = Battle(new WeaponState("bow")); failed.Press(0, 0); failed.Release(0, .5);
            Check.Equal(RangedWeaponPose.Idle, FiveLaneArtTimeline.Weapon(failed.Lanes[0], failed.Beat));
        }

        [Test] public void AttributeFramesAreDisjointAndRangedPosesKeepTheirOwnRow()
        {
            foreach (var weapon in WeaponCatalog.All)
            {
                var frames = new System.Collections.Generic.List<PreviewRect>();
                foreach (WeaponAttribute attribute in Enum.GetValues(typeof(WeaponAttribute)))
                {
                    if (weapon.ExclusiveAttribute.HasValue && weapon.ExclusiveAttribute != attribute) continue;
                    int poses = weapon.IsRanged ? 3 : 1;
                    for (int p = 0; p < poses; p++)
                    {
                        var frame = WeaponAttributeArtLayout.Frame(weapon.Id, attribute, (RangedWeaponPose)p);
                        Check.True(frame.X >= 0 && frame.Y >= 0 && frame.Width > 0 && frame.Height > 0);
                        Check.True(frame.X + frame.Width <= 1.000001 && frame.Y + frame.Height <= 1.000001);
                        foreach (var other in frames)
                            Check.True(frame.X + frame.Width <= other.X + .000001 || other.X + other.Width <= frame.X + .000001 ||
                                frame.Y + frame.Height <= other.Y + .000001 || other.Y + other.Height <= frame.Y + .000001);
                        frames.Add(frame);
                    }
                }
                Check.Equal(weapon.ExclusiveAttribute.HasValue ? 1 : weapon.IsRanged ? 12 : 4, frames.Count);
            }
        }
    }
}
