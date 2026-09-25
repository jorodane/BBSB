using System;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class FlexibleResponseTests
    {
        private static FiveLaneBattle Battle(string id, int bpm = 120, WeaponState weapon = null, WeaponPhrase phrase = null) =>
            new FiveLaneBattle(new[] { weapon ?? new WeaponState(id) }, bpm, 64, Array.Empty<BeatAttack>(),
                new StageHealth(100000), 100, 100, phrases: phrase == null ? null : new[] { phrase });
        private static void Tap(FiveLaneBattle b, double at) { b.Press(0, at); b.Release(0, at); }
        private static void Load(FiveLaneBattle b)
        {
            Tap(b, 0); var lane = b.Lanes[0];
            while (!lane.Loaded && lane.Phase == PhraseLanePhase.Playing)
            { double at = lane.NextBeat; b.Press(0, at); b.Release(0, lane.Holding ? lane.HoldEndBeat : at); }
            Check.True(lane.Loaded);
        }

        [Test] public void BowDrawIsOnlyPreparationAndReleaseDamageTracksActualHoldDurationAcrossTempos()
        {
            foreach (int bpm in new[] { 60, 120, 240 })
            foreach (double held in new[] { .25, .5, 1d, 2d })
            {
                var b = Battle("bow", bpm); Tap(b, 0); b.Press(0, 1); b.Advance(1 + held);
                var lane = b.Lanes[0]; Check.True(lane.Charging); Check.Equal(0m, b.TotalDamage);
                Check.True(lane.LastPerformedNote == null); Check.Equal(0, b.PerfectCount);
                Check.Equal(RangedWeaponPose.Prepare, FiveLaneArtTimeline.Weapon(lane, b.Beat));
                b.Release(0, 1 + held);
                Check.Equal(24m * (decimal)Math.Min(1, held), b.TotalDamage); Check.Equal(2, b.PerfectCount);
                Check.Equal(0, b.MissCount); Check.False(lane.Charging);
                Check.True(lane.LastPerformedNote.Definition.IsChargedRelease);
                Check.Equal(b.Beat, lane.LastDamageBeat); Check.Equal(b.Beat, lane.LastPerformedNote.Beat);
                Check.Equal(AttackMotionMode.Single, FiveLaneAttackMotion.Player(b, lane).Mode);
            }
        }

        [Test] public void FullChargeWaitsForReleaseAndLateDrawingKeepsLaterCallsOnTheBeatGrid()
        {
            var b = Battle("bow"); Tap(b, 0); b.Press(0, 1); b.Advance(10);
            var lane = b.Lanes[0]; Check.True(lane.Charging); Check.Equal(1d, lane.ChargeFraction(b.Beat));
            Check.Equal(0m, b.TotalDamage); Check.Equal(0, b.MissCount);
            var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
            Check.True(timeline.Notes.Any(n => n.Index == 0 && n.EndBeat > b.Beat));
            b.Release(0, 10); Check.Equal(24m, b.TotalDamage);
            Check.Equal(12d, lane.NextBeat); Check.Equal(WeaponBeatSide.Light, WeaponAttributes.SideAt(lane.NextBeat));
            b.Press(0, 12); b.Release(0, 12.5); Check.Equal(38m, b.TotalDamage);
            b.Press(0, 15); b.Release(0, 17); Check.Equal(82m, b.TotalDamage);
            Check.Equal(PhraseLanePhase.Cooldown, lane.Phase); Check.Equal(25d, lane.ReadyAtBeat);
        }

        [Test] public void BowMeasuresFromTheActualPressAndPauseCannotChargeOrFireIt()
        {
            var b = Battle("bow"); Tap(b, 0); b.Press(0, 1.2); b.Advance(1.5);
            Check.Equal(2.2, b.Lanes[0].HoldEndBeat);
            var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
            Check.Equal(2.2, timeline.Notes.Single(n => n.Index == 1).Beat);
            b.Pause(); b.Advance(100); b.Release(0, 100);
            Check.Equal(1.5, b.Beat); Check.True(b.Lanes[0].Charging); Check.Equal(0m, b.TotalDamage);
            b.Resume(); b.Release(0, 1.7);
            Check.Equal(6m, b.TotalDamage); Check.Equal(2, b.HalfMissCount); Check.Equal(0, b.MissCount);
            Check.Equal(PhraseNoteState.Hit, b.Lanes[0].NoteStates[1]); Check.False(b.Lanes[0].IsNoteVisible(1));
            var tap = Battle("bow"); Tap(tap, 0); Tap(tap, 1);
            Check.Equal(0m, tap.TotalDamage); Check.Equal(PhraseNoteState.Skipped, tap.Lanes[0].NoteStates[1]);
        }

        [Test] public void CrossbowSkipsUnlimitedOpportunitiesWithoutMissesOrDamageThenConsumesOneShot()
        {
            var b = Battle("crossbow"); Load(b); var lane = b.Lanes[0];
            Check.Equal(2, b.PerfectCount); Check.Equal(5d, lane.NextBeat);
            var timeline = new FiveLaneNoteTimeline(); b.Advance(100000.3); timeline.Refresh(b);
            Check.True(lane.Loaded); Check.Equal(0, b.MissCount); Check.Equal(2, b.Combo); Check.Equal(0m, b.TotalDamage);
            Check.Equal(100001d, lane.NextBeat); Check.True(timeline.Notes.Count >= 2 && timeline.Notes.Count <= 4);
            Check.True(timeline.Notes.All(n => n.Definition.IsOptional && !n.IsPreview));
            Check.Equal(timeline.Notes.Count, timeline.Notes.Select(n => n.Opportunity).Distinct().Count());
            Tap(b, 100001); timeline.Refresh(b);
            Check.Equal(32m, b.TotalDamage); Check.Equal(3, b.PerfectCount); Check.Equal(0, b.MissCount);
            Check.False(lane.Loaded); Check.Equal(0, timeline.Notes.Count); Check.Equal(0, timeline.Broken.Count);
            Check.Equal(100009d, lane.ReadyAtBeat);
            Tap(b, 100002); Check.Equal(32m, b.TotalDamage);
        }

        [Test] public void EveryReloadCallIsRequiredAndKeepingTheKeyDownNeverFires()
        {
            var failed = Battle("crossbow"); Tap(failed, 0); failed.Press(0, 1); failed.Release(0, 1.2);
            failed.Press(0, 3); failed.Release(0, 4); failed.Advance(7);
            Check.False(failed.Lanes[0].Loaded); Check.Equal(PhraseNoteState.Skipped, failed.Lanes[0].NoteStates[2]);
            Check.Equal(0m, failed.TotalDamage); Check.Equal(1, failed.MissCount);
            var b = Battle("crossbow"); Tap(b, 0); b.Press(0, 1); b.Release(0, 1.5);
            b.Press(0, 3); b.Advance(4); Check.True(b.Lanes[0].Loaded);
            b.Press(0, 5); Check.Equal(0m, b.TotalDamage); b.Release(0, 5); Check.Equal(0m, b.TotalDamage);
            Tap(b, 6); Check.Equal(32m, b.TotalDamage);
        }

        [Test] public void LoadedPauseKeepsAmmoAndAnOffGridShotUsesTheNormalMissRule()
        {
            var b = Battle("crossbow"); Load(b); b.Advance(6); b.Pause(); b.Advance(99); Tap(b, 99);
            Check.Equal(6d, b.Beat); Check.True(b.Lanes[0].Loaded); Check.Equal(0m, b.TotalDamage);
            b.Resume(); Tap(b, 6.4); Check.Equal(1, b.MissCount); Check.Equal(0m, b.TotalDamage);
            Check.False(b.Lanes[0].Loaded); Check.Equal(PhraseLanePhase.Cooldown, b.Lanes[0].Phase);
        }

        [Test] public void InjectedCrossbowNotesAreAlternativeHalfBeatShotsAndAllDisappearAfterSelection()
        {
            var weapon = new WeaponState("crossbow");
            weapon.SetNoteBindings(new[] { new WeaponNoteBinding(new NotePartState(1, "inject-tap"), 2) });
            var b = Battle("crossbow", weapon: weapon); Load(b); var lane = b.Lanes[0];
            Check.True(lane.Phrase.Notes[3].IsOptional); Check.Equal(PhraseNoteCondition.AllCalls, lane.Phrase.Notes[3].Condition);
            b.Advance(5.3); Check.Equal(5.5, lane.NextBeat); Check.Equal(0, b.MissCount);
            var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b); Check.True(timeline.Notes.Count > 3);
            Tap(b, 5.5); timeline.Refresh(b); Check.Equal(19.2m, b.TotalDamage);
            Check.Equal(PhraseNoteState.Skipped, lane.NoteStates[2]); Check.Equal(PhraseNoteState.Hit, lane.NoteStates[3]);
            Check.Equal(0, timeline.Notes.Count); Check.Equal(0, timeline.Broken.Count);
        }

        [Test] public void FramesKeepReleaseSemanticsAndAnOptionalCrownRequiresItsChosenHold()
        {
            var bow = new WeaponState("bow");
            bow.SetNoteBindings(new[] { new WeaponNoteBinding(new NotePartState(1, "frame-power"), 1) });
            var b = Battle("bow", weapon: bow); Tap(b, 0); b.Press(0, 1); b.Release(0, 1.5);
            Check.Equal(18m, b.TotalDamage); Check.True(b.Lanes[0].Phrase.Notes[1].IsChargedRelease);
            bow.SetNoteBindings(new[] { new WeaponNoteBinding(new NotePartState(3, "inject-hold"), 1) });
            bool rejected = false;
            try { WeaponNoteAssembly.Apply(bow, WeaponPhraseSet.Uniform(bow)); }
            catch (ArgumentException) { rejected = true; }
            Check.True(rejected); // The release endpoint already belongs to the preceding draw Hold.
            var crossbow = new WeaponState("crossbow");
            crossbow.SetNoteBindings(new[] { new WeaponNoteBinding(new NotePartState(2, "inject-hold"), 2) });
            var held = Battle("crossbow", weapon: crossbow); Load(held); held.Press(0, 5);
            Check.True(held.Lanes[0].Holding); Check.False(held.Lanes[0].Loaded); Check.Equal(0m, held.TotalDamage);
            held.Release(0, 5.5); Check.Equal(44.8m, held.TotalDamage); Check.Equal(PhraseLanePhase.Cooldown, held.Lanes[0].Phase);
        }

        [Test] public void ChaosWaitsForTheChosenShotBeforeStartingItsSecondReloadSection()
        {
            var b = Battle("crossbow", weapon: new WeaponState("crossbow", WeaponAttribute.Chaos)); Load(b);
            var lane = b.Lanes[0]; var timeline = new FiveLaneNoteTimeline(); b.Advance(20); timeline.Refresh(b);
            Check.True(timeline.Notes.All(n => n.CycleStart == lane.StartBeat));
            double shot = lane.NextBeat < b.Beat ? lane.NextBeat + 1 : lane.NextBeat;
            Tap(b, shot); Check.Equal(1, lane.Cycle.BaseIndex); Check.True(lane.StartBeat > shot);
            Check.False(lane.Loaded); Check.Equal(0, b.MissCount);
            while (!lane.Loaded) { b.Press(0, lane.NextBeat); b.Release(0, lane.HoldEndBeat); }
            Tap(b, lane.NextBeat); Check.Equal(PhraseLanePhase.Cooldown, lane.Phase);
            Check.Equal(6, b.PerfectCount); Check.Equal(96m, b.TotalDamage);
        }

        [Test] public void WorkshopShowsReleaseShotsAndPassingTwoLoadedOpportunitiesBeforeFiring()
        {
            var bow = new NoteWorkshopPlayback(WeaponPhraseCatalog.Find("bow"), 1, 0);
            Check.Equal(NoteDemoInput.Call, bow.InputAt(1, 0)); Check.Equal(NoteDemoInput.Hold, bow.InputAt(1.5, 0));
            Check.Equal(NoteDemoInput.Release, bow.InputAt(2, 0)); Check.Equal(NoteDemoInput.Idle, bow.InputAt(3, 0));
            Check.Equal(17d, bow.LoopBeats);
            var crossbow = new NoteWorkshopPlayback(WeaponPhraseCatalog.Find("crossbow"), 1, 0);
            Check.Equal(NoteDemoInput.Ready, crossbow.InputAt(5, 0)); Check.Equal(NoteDemoInput.Ready, crossbow.InputAt(6, 0));
            Check.Equal(NoteDemoInput.Press, crossbow.InputAt(7, 0));
            var times = new System.Collections.Generic.List<double>();
            crossbow.VisitNotes(4.5, 8, (n, s, e, l) => { if (n.IsOptional) times.Add(s); });
            Check.True(times.SequenceEqual(new[] { 5d, 6, 7 }));
            times.Clear(); crossbow.VisitNotes(7.3, 8, (n, s, e, l) => times.Add(s)); Check.Equal(0, times.Count);
        }

        [Test] public void InjectedReloadCallsAlsoGateTheLoadedShotAndInvalidTriggerPairsAreRejected()
        {
            var weapon = new WeaponState("crossbow");
            weapon.SetNoteBindings(new[] { new WeaponNoteBinding(new NotePartState(1, "inject-tap"), 0) });
            var good = Battle("crossbow", weapon: weapon); Load(good); Check.Equal(3, good.PerfectCount);
            Tap(good, good.Lanes[0].NextBeat); Check.Equal(32m, good.TotalDamage);
            var failed = Battle("crossbow", weapon: weapon); Tap(failed, 0); failed.Press(0, 1); failed.Release(0, 1.5);
            failed.Advance(2.3); failed.Press(0, 3); failed.Release(0, 4);
            Check.False(failed.Lanes[0].Loaded); Check.Equal(0m, failed.TotalDamage); Check.Equal(1, failed.MissCount);
            bool rejected = false;
            try
            {
                new WeaponPhrase("bow", "Invalid draw", "", 3, new[] {
                    new WeaponPhraseNote(0, 1, 1), new WeaponPhraseNote(1, 10, prerequisite: 0,
                        condition: PhraseNoteCondition.Hit, trigger: WeaponNoteTrigger.ChargedRelease) });
            }
            catch (ArgumentException) { rejected = true; }
            Check.True(rejected);
            rejected = false;
            try { new WeaponPhraseNote(0, 1, trigger: WeaponNoteTrigger.OptionalPress, opportunityIntervalBeats: 0); }
            catch (ArgumentException) { rejected = true; }
            Check.True(rejected);
        }

        [Test] public void BowInjectedHalfBeatAlwaysFollowsTheActualReleaseInsteadOfTheChargeCap()
        {
            foreach (double release in new[] { 1.5, 2d, 3.3 })
            {
                var weapon = new WeaponState("bow");
                weapon.SetNoteBindings(new[] { new WeaponNoteBinding(new NotePartState(1, "inject-tap"), 1) });
                var b = Battle("bow", weapon: weapon); Tap(b, 0); b.Press(0, 1); b.Release(0, release);
                Check.True(Math.Abs(release + .5 - b.Lanes[0].NextBeat) < .000001);
                Check.Equal(AttackMotionMode.Combo, FiveLaneAttackMotion.Player(b, b.Lanes[0]).Mode);
                var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
                Check.True(timeline.Notes.Any(n => n.Definition.IsInjected && Math.Abs(n.Beat - release - .5) < .000001));
                Tap(b, release + .5); Check.Equal(24m * (decimal)Math.Min(1, release - 1) + 14.4m, b.TotalDamage);
                Check.Equal(0, b.MissCount); Check.True(FiveLaneAttackMotion.Player(b, b.Lanes[0]).Continues);
            }
        }
    }
}
