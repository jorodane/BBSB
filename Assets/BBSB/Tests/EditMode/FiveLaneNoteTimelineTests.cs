using System;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class FiveLaneNoteTimelineTests
    {
        private static FiveLaneBattle Battle(string weapon, WeaponPhrase phrase = null, params BeatAttack[] attacks) =>
            new FiveLaneBattle(new[] { new WeaponState(weapon) }, 120, 32, attacks, new StageHealth(10000), 100, 100,
                phrase == null ? null : new[] { phrase });
        private static void Tap(FiveLaneBattle battle, double beat) { battle.Press(0, beat); battle.Release(0, beat); }
        private static TrackNote At(FiveLaneNoteTimeline timeline, double beat) =>
            timeline.Notes.Single(note => Math.Abs(note.Beat - beat) < .000001);

        [Test] public void DaggerShowsOneRealNoteAndAssumesSuccessfulRepeatsWithinThreeBeats()
        {
            var b = Battle("dagger"); var timeline = new FiveLaneNoteTimeline();
            timeline.Refresh(b); Check.Equal(0, timeline.Notes.Count);
            Tap(b, 0); timeline.Refresh(b);
            Check.Equal(3, timeline.Notes.Count);
            Check.False(At(timeline, 1).IsPreview); Check.True(At(timeline, 2).IsPreview); Check.True(At(timeline, 3).IsPreview);
            for (int i = 0; i < 20; i++) timeline.Refresh(b);
            Check.Equal(1, b.Lanes[0].NoteStates.Count); Check.Equal(1, b.Combo); Check.Equal(6m, b.TotalDamage);
            Tap(b, 1); timeline.Refresh(b);
            Check.False(At(timeline, 2).IsPreview); Check.True(At(timeline, 3).IsPreview); Check.True(At(timeline, 4).IsPreview);
            Check.Equal(3, timeline.Notes.Count); Check.Equal(0, timeline.Broken.Count);
            Tap(b, 2.2); timeline.Refresh(b); // Half-miss still succeeds and preserves the forecast.
            Check.Equal(1, b.HalfMissCount); Check.False(At(timeline, 3).IsPreview); Check.Equal(0, timeline.Broken.Count);
        }
        [Test] public void BowShotIsPreviewedDuringDrawThenPromotedInPlaceOnCompletion()
        {
            var b = Battle("bow"); b.Press(0, 0);
            var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
            var forecast = At(timeline, 2); Check.True(forecast.IsPreview);
            Check.Equal(PhraseNoteState.Locked, b.Lanes[0].NoteStates[1]);
            Check.False(b.Lanes[0].IsNoteVisible(1));
            b.Release(0, 1); timeline.Refresh(b);
            var real = At(timeline, 2); Check.False(real.IsPreview);
            Check.Equal(forecast.CycleStart, real.CycleStart); Check.Equal(forecast.Index, real.Index);
            Check.True(b.Lanes[0].IsNoteVisible(1)); Check.Equal(0, timeline.Broken.Count);
            Check.True(At(timeline, 4).IsPreview); // Next draw assumes the current shot will succeed.
        }
        [Test] public void FailedDrawShattersItsShotOnceAndFragmentsFreezeWithTheBattle()
        {
            var b = Battle("bow"); b.Press(0, 0);
            var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
            b.Release(0, .5); timeline.Refresh(b);
            Check.Equal(0, timeline.Notes.Count);
            Check.True(timeline.Broken.Any(note => note.Note.Beat == 2 && note.Note.IsPreview));
            Check.Equal(PhraseNoteState.Skipped, b.Lanes[0].NoteStates[1]); Check.Equal(0m, b.TotalDamage);
            int count = timeline.Broken.Count; var shard = timeline.Broken.First(note => note.Note.Beat == 2);
            timeline.Refresh(b); Check.Equal(count, timeline.Broken.Count);
            b.Pause(); b.Advance(20); timeline.Refresh(b);
            Check.Equal(count, timeline.Broken.Count); Check.Equal(0.0, shard.Progress(b.Beat));
            b.Resume(); b.Advance(.75); timeline.Refresh(b); Check.Equal(.5, shard.Progress(b.Beat));
            b.Advance(1); timeline.Refresh(b); Check.Equal(0, timeline.Broken.Count);
        }
        [Test] public void FailedDaggerBreaksFutureRepeatsWithoutMakingGhostsPlayable()
        {
            var b = Battle("dagger"); Tap(b, 0);
            var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
            Tap(b, .75); timeline.Refresh(b); // Too early for the actual note at 1.
            Check.Equal(0, timeline.Notes.Count);
            Check.True(timeline.Broken.Any(note => note.Note.Beat == 2));
            Check.True(timeline.Broken.Any(note => note.Note.Beat == 3));
            Check.False(b.Lanes[0].CanRepeat); Tap(b, 2);
            Check.Equal(6m, b.TotalDamage); Check.Equal(1, b.MissCount); Check.Equal(0, b.Combo);
        }
        [Test] public void FailedConditionAndFutureRepeatBreakWhileConfirmedRemainingNoteSurvives()
        {
            var phrase = new WeaponPhrase("sword", "test", "", 4, new[] {
                new WeaponPhraseNote(0, 1), new WeaponPhraseNote(1, 2),
                new WeaponPhraseNote(2, 3, prerequisite: 1, condition: PhraseNoteCondition.Hit), new WeaponPhraseNote(3, 4) });
            var b = Battle("sword", phrase); Tap(b, 0); b.Advance(1.1);
            var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
            Check.True(At(timeline, 2).IsPreview); Check.True(At(timeline, 4).IsPreview); Check.False(At(timeline, 3).IsPreview);
            b.Advance(1.25); timeline.Refresh(b);
            Check.Equal(1, timeline.Notes.Count); Check.False(At(timeline, 3).IsPreview);
            Check.True(timeline.Broken.Any(note => note.Note.Beat == 2));
            Check.True(timeline.Broken.Any(note => note.Note.Beat == 4));
            Check.False(timeline.Broken.Any(note => note.Note.Beat == 3));
            Tap(b, 3); Check.Equal(5m, b.TotalDamage); Check.Equal(1, b.MissCount);
        }
        [Test] public void CounterPreviewDependsOnARealParryAndBreaksWhenItFails()
        {
            var phrase = new WeaponPhrase("shield", "test", "", 3, new[] {
                new WeaponPhraseNote(0, 0), new WeaponPhraseNote(1, 0, effect: PhraseEffect.Parry),
                new WeaponPhraseNote(2, 9, prerequisite: 1, condition: PhraseNoteCondition.Parry) }, repeat: false);
            foreach (bool parry in new[] { false, true })
            {
                var b = Battle("shield", phrase, parry ? new[] { new BeatAttack("a", 1, 10) } : Array.Empty<BeatAttack>());
                Tap(b, 0); var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
                Check.True(At(timeline, 2).IsPreview); Tap(b, 1); timeline.Refresh(b);
                if (parry)
                { Check.False(At(timeline, 2).IsPreview); Check.Equal(0, timeline.Broken.Count); Check.Equal(10m, b.TotalBlocked); }
                else
                { Check.Equal(0, timeline.Notes.Count); Check.True(timeline.Broken.Any(note => note.Note.Beat == 2)); }
            }
        }
        [Test] public void AlreadyUnlockedCountersRemainSolidAfterMissingAnotherCounter()
        {
            var b = Battle("shield", null, new BeatAttack("a", 0, 10)); Tap(b, 0);
            var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
            Check.Equal(3, timeline.Notes.Count); Check.True(timeline.Notes.All(note => !note.IsPreview));
            b.Advance(1.25); timeline.Refresh(b);
            Check.False(At(timeline, 1.5).IsPreview); Check.False(At(timeline, 2).IsPreview);
            Check.False(timeline.Broken.Any(note => note.Note.Beat == 1.5 || note.Note.Beat == 2));
            Tap(b, 1.5); Tap(b, 2); Check.Equal(14m, b.TotalDamage);
        }
        [Test] public void VoluntaryGuardReleaseEndsTheLiveHoldAndOnlyBreaksCanceledPreviews()
        {
            foreach (bool hasFollowup in new[] { false, true })
            {
                var phrase = hasFollowup ? new WeaponPhrase("heater-shield", "guard", "", 3, new[] {
                    new WeaponPhraseNote(0, 0, 1, PhraseEffect.Parry),
                    new WeaponPhraseNote(2, 3, prerequisite: 0, condition: PhraseNoteCondition.Hit) },
                    repeat: false, releaseEndsPhrase: true, parryRequired: false, completionCooldownBeats: 2) : null;
                var b = Battle("heater-shield", phrase); b.Press(0, 0);
                var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
                b.Release(0, .5); timeline.Refresh(b);
                Check.Equal(0, b.MissCount); Check.Equal(0, timeline.Notes.Count);
                Check.False(timeline.Broken.Any(note => !note.Note.IsPreview));
                Check.Equal(hasFollowup ? 1 : 0, timeline.Broken.Count);
            }
        }
        [Test] public void SkippedRenderFramesAndFractionalCyclesDoNotDuplicateOrShatterPromotions()
        {
            var b = Battle("dagger"); Tap(b, 0);
            var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
            Tap(b, 1); Tap(b, 2); timeline.Refresh(b);
            Check.Equal(3, timeline.Notes.Count); Check.False(At(timeline, 3).IsPreview); Check.Equal(0, timeline.Broken.Count);
            var phrase = new WeaponPhrase("dagger", "thirds", "", 1.0 / 3, new[] { new WeaponPhraseNote(0, 1) });
            var thirds = Battle("dagger", phrase); Tap(thirds, 0); timeline.Refresh(thirds);
            for (int i = 0; i < 60; i++)
            {
                Tap(thirds, thirds.Lanes[0].NextBeat); timeline.Refresh(thirds);
                Check.Equal(0, timeline.Broken.Count); Check.Equal(1, timeline.Notes.Count(note => !note.IsPreview));
                Check.True(timeline.Notes.All(note => note.Beat <= thirds.Beat + 3));
            }
        }
        [Test] public void PreviewBudgetAndBattleRebindKeepThePresentationBounded()
        {
            var phrase = new WeaponPhrase("dagger", "short", "", .001, new[] { new WeaponPhraseNote(0, 1) });
            var b = Battle("dagger", phrase); Tap(b, 0);
            var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
            Check.Equal(FiveLaneNoteTimeline.MaxNotesPerLane, timeline.Notes.Count);
            Check.Equal(1, b.Lanes[0].NoteStates.Count); Check.Equal(1, b.PerfectCount);
            var bow = Battle("bow"); bow.Press(0, 0); timeline.Refresh(bow); bow.Release(0, .5); timeline.Refresh(bow);
            Check.True(timeline.Broken.Count > 0); timeline.Refresh(Battle("dagger"));
            Check.Equal(0, timeline.Notes.Count); Check.Equal(0, timeline.Broken.Count);
        }
        [Test] public void LongHoldPreviewClipsItsTailAtTheThreeBeatHorizon()
        {
            var phrase = new WeaponPhrase("bow", "long draw", "", 5, new[] {
                new WeaponPhraseNote(0, 0, .5),
                new WeaponPhraseNote(.75, 0, 4, prerequisite: 0, condition: PhraseNoteCondition.Hit) });
            var b = Battle("bow", phrase); b.Press(0, 0);
            var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
            Check.Equal(2, timeline.Notes.Count); b.Release(0, .25); timeline.Refresh(b);
            var broken = timeline.Broken.Single(note => note.Note.IsPreview);
            Check.Equal(3.0, broken.TailDistance); Check.False(broken.TailVisible);
            Check.True(timeline.Broken.All(note => note.HeadDistance <= 3 && note.TailDistance <= 3));
        }
    }
}
