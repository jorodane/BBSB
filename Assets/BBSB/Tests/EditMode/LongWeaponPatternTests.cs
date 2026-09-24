using System;
using System.Collections.Generic;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class LongWeaponPatternTests
    {
        private static FiveLaneBattle Battle(string id, WeaponAttribute attribute, int seed = 1, WeaponPhraseSet set = null) =>
            new FiveLaneBattle(new[] { new WeaponState(id, attribute) }, 120, 128,
                Array.Empty<BeatAttack>(), new StageHealth(1000000), 100, 10000,
                phraseSets: set == null ? null : new[] { set }, rhythmSeed: seed);
        private static void Open(FiveLaneBattle b, int slot = 0, double at = 0)
        {
            b.Press(slot, at); var lane = b.Lanes[0];
            b.Release(slot, lane.Holding ? Math.Max(at, lane.HoldEndBeat) : at);
            if (lane.Phrase.FirstNoteDelayBeats > 0)
            {
                double first = lane.NextBeat;
                b.Press(slot, first); b.Release(slot, lane.Holding ? lane.HoldEndBeat : first);
            }
        }
        private static void FinishSection(FiveLaneBattle b)
        {
            var lane = b.Lanes[0]; int cycle = lane.Cycle.Index;
            while (lane.Phase == PhraseLanePhase.Playing && lane.Cycle.Index == cycle)
            {
                int slot = lane.SlotForNote(lane.NextNote);
                double at = lane.NextBeat, end = at + lane.Phrase.Notes[lane.NextNote].HoldBeats;
                b.Press(slot, at); b.Release(slot, end);
            }
        }

        [Test] public void BasicPulseWeaponsKeepTheirCadenceThroughHalfMissesAndPauseUsingTheActualCatalog()
        {
            foreach (string id in new[] { "dagger", "dual-swords" })
            foreach (var attribute in new[] { WeaponAttribute.Light, WeaponAttribute.Dark, WeaponAttribute.Dual })
            foreach (double at in new[] { .13, .49 })
            {
                var b = Battle(id, attribute); var lane = b.Lanes[0];
                double interval = id == "dagger" ? 2 : 1;
                Open(b, at: at); double start = lane.StartBeat - interval;
                Check.Equal(1, lane.Phrase.Notes.Count); Check.False(lane.Phrase.Notes[0].IsHold);
                Check.Equal(interval, lane.Phrase.LengthBeats); Check.Equal(0, lane.Phrase.MaximumCycles);
                Check.Equal(0.0, lane.Phrase.CompletionCooldownBeats); Check.Equal(2.0, lane.Phrase.MissCooldownBeats);
                var side = lane.ActiveSide;
                for (int i = 1; i <= 24; i++)
                {
                    double next = start + i * interval;
                    Check.Equal(next, lane.NextBeat); Check.Equal(side, lane.ActiveSide);
                    if (i == 5)
                    {
                        double paused = b.Beat; b.Pause(); b.Advance(1000);
                        Check.Equal(paused, b.Beat); Check.Equal(next, lane.NextBeat); b.Resume();
                    }
                    double input = next + (i == 3 ? .2 : 0);
                    b.Press(0, input); b.Release(0, input);
                    Check.Equal(PhraseLanePhase.Playing, lane.Phase); Check.True(lane.CanRepeat);
                }
                Check.Equal(start + 25 * interval, lane.NextBeat);
                Check.Equal(24, b.PerfectCount); Check.Equal(1, b.HalfMissCount); Check.Equal(0, b.MissCount);
            }
        }

        [Test] public void BasicPulseMissesBreakTheForecastAndAllowANewStartAfterTwoBeats()
        {
            foreach (string id in new[] { "dagger", "dual-swords" })
            foreach (var attribute in new[] { WeaponAttribute.Light, WeaponAttribute.Dark, WeaponAttribute.Dual })
            {
                var b = Battle(id, attribute); var lane = b.Lanes[0];
                double interval = id == "dagger" ? 2 : 1;
                Open(b, at: .49); double missedBeat = lane.NextBeat; decimal damage = b.TotalDamage;
                var timeline = new FiveLaneNoteTimeline();
                b.Advance(missedBeat - interval * .25); timeline.Refresh(b);
                Check.True(timeline.Notes.Any(n => n.IsPreview));
                b.Advance(missedBeat + b.HalfMissWindow + .01); timeline.Refresh(b);
                Check.Equal(1, b.MissCount); Check.Equal(0, b.Combo); Check.False(lane.CanRepeat);
                Check.Equal(PhraseLanePhase.Cooldown, lane.Phase); Check.Equal(0, timeline.Notes.Count);
                Check.True(timeline.Broken.Any(n => n.Note.IsPreview));
                Check.True(Math.Abs(lane.ReadyAtBeat - lane.LastJudgedBeat - 2) < .000001,
                    "A missed pulse must recover two beats after the miss is judged.");
                double ready = lane.ReadyAtBeat;
                b.Press(0, ready - .1); b.Release(0, b.Beat);
                Check.Equal(damage, b.TotalDamage); Check.Equal(1, b.PerfectCount);
                b.Advance(ready); Check.Equal(PhraseLanePhase.Ready, lane.Phase);
                Open(b, at: ready);
                Check.Equal(2, b.PerfectCount); Check.Equal(1, b.MissCount); Check.True(lane.CanRepeat);
            }
        }

        [Test] public void OtherOffensiveAndSupportWeaponsHaveLongPhrasesCooldownsAndAMajorityOfTheirOwnPulse()
        {
            Check.Equal(32, WeaponPhraseCatalog.All.Count);
            foreach (var weapon in WeaponCatalog.All.Where(w => w.Kind != WeaponKind.Shield && w.Id != "dagger" && w.Id != "dual-swords"))
            foreach (var side in new[] { WeaponBeatSide.Light, WeaponBeatSide.Dark })
            for (int offset = 0; offset < weapon.RequiredLanes; offset++)
            {
                var p = WeaponPhraseCatalog.Default(weapon.Id, offset, side);
                Check.True(p.LengthBeats >= 8 && p.LengthBeats <= 12);
                Check.True(p.CompletionCooldownBeats >= 6 && p.CompletionCooldownBeats <= 10);
                Check.True(p.MissCooldownBeats >= 6 && p.Notes.Count >= 6);
                double start = side == WeaponBeatSide.Light ? 0 : .5;
                var edges = p.Notes.Select(n => start + n.Beat).Concat(p.Notes.Where(n => n.IsHold).Select(n => start + n.Beat + n.HoldBeats)).ToArray();
                Check.True(edges.Count(at => WeaponAttributes.SideAt(at) == side) * 3 >= edges.Length * 2);
            }
        }

        [Test] public void EveryAllowedOneShotVariantCompletesFromEveryLineThenActuallyWaitsForItsCooldown()
        {
            foreach (var weapon in WeaponCatalog.All.Where(w => w.Kind != WeaponKind.Shield && !WeaponPhraseCatalog.Find(w.Id).Repeat))
            foreach (var attribute in new[] { WeaponAttribute.Light, WeaponAttribute.Dark, WeaponAttribute.Dual })
            {
                if (weapon.ExclusiveAttribute.HasValue && weapon.ExclusiveAttribute != attribute) continue;
                for (int offset = 0; offset < weapon.RequiredLanes; offset++)
                foreach (double at in new[] { .13, .49 })
                {
                    var b = Battle(weapon.Id, attribute); var lane = b.Lanes[0];
                    Open(b, offset, at); double start = lane.StartBeat; var phrase = lane.Phrase;
                    FinishSection(b);
                    Check.Equal(phrase.Notes.Count, b.PerfectCount); Check.Equal(0, b.MissCount); Check.Equal(0, b.HalfMissCount);
                    Check.Equal(PhraseLanePhase.Cooldown, lane.Phase);
                    Check.Equal(start + phrase.LengthBeats + phrase.CompletionCooldownBeats, lane.ReadyAtBeat);
                    int hits = b.PerfectCount;
                    b.Press(offset, lane.ReadyAtBeat - .1); b.Release(offset, b.Beat);
                    Check.Equal(hits, b.PerfectCount); Check.Equal(PhraseLanePhase.Cooldown, lane.Phase);
                    b.Advance(lane.ReadyAtBeat); Check.Equal(PhraseLanePhase.Ready, lane.Phase);
                }
            }
        }

        [Test] public void AllTwentyOneNewChaosWeaponsPlayExactlyTwoRandomSectionsWithoutABridge()
        {
            var eligible = WeaponCatalog.All.Where(w => WeaponAttributes.SupportsChaos(w.Id) && !WeaponAttributes.UsesChaosTransitions(w.Id)).ToArray();
            Check.Equal(21, eligible.Length);
            foreach (var weapon in eligible)
            for (int offset = 0; offset < weapon.RequiredLanes; offset++)
            for (int seed = 0; seed < 8; seed++)
            {
                var b = Battle(weapon.Id, WeaponAttribute.Chaos, seed); var lane = b.Lanes[0];
                Open(b, offset); var first = lane.Cycle; int count = first.Phrase.Notes.Count;
                var expected = lane.NextCycle(first);
                Check.True(lane.Patterns.RandomizeChaosSections); Check.False(expected.IsTransition);
                FinishSection(b);
                Check.Equal(expected.Side, lane.ActiveSide); Check.Equal(expected.StartBeat, lane.StartBeat);
                Check.Equal(1, lane.Cycle.BaseIndex); Check.False(lane.CanRepeat);
                Check.True(lane.StartBeat >= first.StartBeat + first.Phrase.LengthBeats);
                double ready = lane.StartBeat + lane.Phrase.LengthBeats + lane.Phrase.CompletionCooldownBeats;
                count += lane.Phrase.Notes.Count;
                FinishSection(b);
                Check.Equal(count, b.PerfectCount); Check.Equal(0, b.MissCount); Check.Equal(0, b.HalfMissCount);
                Check.Equal(PhraseLanePhase.Cooldown, lane.Phase); Check.Equal(ready, lane.ReadyAtBeat);
                var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b); Check.Equal(0, timeline.Notes.Count);
                b.Advance(ready); Check.Equal(PhraseLanePhase.Ready, lane.Phase);
            }
        }

        [Test] public void RandomChaosAllowsAllFourIndependentSidePairsAndForecastDoesNotRerollThem()
        {
            var pairs = new HashSet<string>();
            for (int seed = 0; seed < 128; seed++)
            {
                var b = Battle("sword", WeaponAttribute.Chaos, seed); var control = Battle("sword", WeaponAttribute.Chaos, seed);
                Open(b); Open(control); var lane = b.Lanes[0]; var initial = lane.Cycle;
                var next = lane.NextCycle(initial); var timeline = new FiveLaneNoteTimeline();
                while (lane.NextNote < lane.Phrase.Notes.Count - 1)
                { int slot = lane.SlotForNote(lane.NextNote); double at = lane.NextBeat; b.Press(slot, at); b.Release(slot, at); }
                for (int i = 0; i < 20; i++) timeline.Refresh(b);
                Check.True(timeline.Notes.Any(n => n.CycleStart == next.StartBeat && n.Index == 0));
                FinishSection(b); FinishSection(control);
                Check.Equal(next.Side, lane.ActiveSide); Check.Equal(next.StartBeat, lane.StartBeat);
                Check.Equal(control.Lanes[0].ActiveSide, lane.ActiveSide); Check.Equal(control.Lanes[0].StartBeat, lane.StartBeat);
                pairs.Add(initial.Side + "/" + lane.ActiveSide);
                FinishSection(b); Check.Equal(PhraseLanePhase.Cooldown, lane.Phase);
            }
            Check.Equal(4, pairs.Count);
        }

        [Test] public void OriginalThreeChaosWeaponsKeepRepeatingThroughTheirBridgesAndFlipSideAfterTheBridge()
        {
            foreach (string id in new[] { "dagger", "dual-swords", "chaos-pendulum" })
            foreach (double start in new[] { 0.0, .5 })
            {
                var weapon = new WeaponState(id, WeaponAttribute.Chaos); var defaults = WeaponPhraseSet.Uniform(weapon);
                var set = new WeaponPhraseSet(weapon, defaults.LightStarts, defaults.DarkStarts,
                    defaults.LightTransitions, defaults.DarkTransitions, new ChaosRules(transitionChance: 1));
                var b = Battle(id, WeaponAttribute.Chaos, set: set); Open(b, at: start); var lane = b.Lanes[0];
                for (int i = 0; i < 3; i++)
                {
                    var before = lane.ActiveSide; Check.Equal(0, lane.Phrase.MaximumCycles);
                    for (int section = 0; section < 12 && !lane.IsTransition; section++) FinishSection(b);
                    Check.True(lane.IsTransition); Check.Equal(before, lane.ActiveSide);
                    Check.True(lane.StartBeat - lane.Cycle.StableSinceBeat >= 6);
                    FinishSection(b); Check.False(lane.IsTransition); Check.Equal(WeaponAttributes.Opposite(before), lane.ActiveSide);
                    Check.True(lane.CanRepeat); Check.Equal(PhraseLanePhase.Playing, lane.Phase);
                }
                Check.Equal(0, b.MissCount); Check.Equal(0, b.HalfMissCount);
            }
        }

        [Test] public void AFailedChaosSectionFinishesItsRemainingNotesButDoesNotStartTheSecondSection()
        {
            var b = Battle("sword", WeaponAttribute.Chaos); Open(b); var lane = b.Lanes[0];
            b.Advance(lane.NextBeat + b.HalfMissWindow + .01); Check.Equal(1, b.MissCount);
            Check.False(lane.CanRepeat); Check.True(lane.IsNoteVisible(lane.NextNote));
            FinishSection(b); Check.Equal(0, lane.Cycle.BaseIndex); Check.Equal(PhraseLanePhase.Cooldown, lane.Phase);
            Check.Equal(b.Beat + lane.Phrase.MissCooldownBeats, lane.ReadyAtBeat);
        }

        [Test] public void BowDrawsRecoverIndependentlyAndThePoseClearsBetweenShots()
        {
            var b = Battle("bow", WeaponAttribute.Light); var lane = b.Lanes[0];
            b.Press(0, 0); b.Release(0, 0); b.Press(0, 1); b.Release(0, 1.5); Check.Equal(PhraseNoteState.Skipped, lane.NoteStates[1]);
            Check.Equal(RangedWeaponPose.Idle, FiveLaneArtTimeline.Weapon(lane, b.Beat));
            FinishSection(b); Check.Equal(72m, b.TotalDamage); Check.Equal(1, b.MissCount);
            var good = Battle("bow", WeaponAttribute.Light); Open(good);
            good.Press(0, 3); good.Release(0, 3); good.Advance(3.5);
            Check.Equal(RangedWeaponPose.Idle, FiveLaneArtTimeline.Weapon(good.Lanes[0], good.Beat));
            Check.Equal("Base Layer.Idle", FiveLaneArtTimeline.Player(good).State);
            FinishSection(good); Check.Equal(96m, good.TotalDamage); Check.Equal(0, good.MissCount);
        }

        [Test] public void AChaoticNeighborKeepsTheSideAndTimingPromisedByTheBellPreview()
        {
            for (int seed = 0; seed < 16; seed++)
            {
                var b = new FiveLaneBattle(new[] { new WeaponState("spirit-bell"), new WeaponState("sword", WeaponAttribute.Chaos) },
                    120, 128, Array.Empty<BeatAttack>(), new StageHealth(100000), 100, 100, rhythmSeed: seed);
                Open(b); var reservation = b.ScheduledStarts.Single();
                var side = WeaponAttributes.SideAt(reservation.Beat); var timeline = new FiveLaneNoteTimeline();
                timeline.Refresh(b); Check.True(timeline.Notes.Any(n => n.Beat == reservation.Beat && n.Slot == 1));
                b.Press(1, reservation.Beat); b.Release(1, reservation.Beat);
                Check.Equal(side, b.Lanes[1].ActiveSide); Check.Equal(reservation.Beat, b.Lanes[1].StartBeat);
                Check.Equal(0, b.MissCount);
            }
        }

        [Test] public void ShieldsKeepTheirExactShortPatternsAndDefensiveRules()
        {
            foreach (string id in ShortWeaponPhrases.ShieldIds)
            {
                var old = ShortWeaponPhrases.Find(id); var current = WeaponPhraseCatalog.Find(id);
                Check.Equal(old.LengthBeats, current.LengthBeats); Check.Equal(old.MissCooldownBeats, current.MissCooldownBeats);
                Check.Equal(old.CompletionCooldownBeats, current.CompletionCooldownBeats); Check.Equal(old.Repeat, current.Repeat);
                Check.Equal(old.ParryInput, current.ParryInput); Check.Equal(old.HoldDamageReduction, current.HoldDamageReduction);
                Check.Equal(old.ReleaseEndsPhrase, current.ReleaseEndsPhrase); Check.Equal(old.ParryRequired, current.ParryRequired);
                Check.Equal(old.Notes.Count, current.Notes.Count); Check.False(WeaponAttributes.SupportsChaos(id));
                for (int i = 0; i < old.Notes.Count; i++)
                {
                    var a = old.Notes[i]; var n = current.Notes[i];
                    Check.Equal(a.Beat, n.Beat); Check.Equal(a.HoldBeats, n.HoldBeats); Check.Equal(a.Damage, n.Damage);
                    Check.Equal(a.Condition, n.Condition); Check.Equal(a.Prerequisite, n.Prerequisite); Check.Equal(a.Effect, n.Effect);
                }
            }
        }
    }
}
