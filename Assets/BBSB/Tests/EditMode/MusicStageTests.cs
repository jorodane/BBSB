using System;
using System.Collections.Generic;
using System.Linq;
using BBSB.Core;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class MusicStageTests
    {
        [Test]
        public void FiveScoresHaveDistinctReproducibleSlotsAndNoInputsInIntro()
        {
            Check.Equal(5, MusicCatalog.All.Count);
            Check.Equal(5, MusicCatalog.All.Select(x => x.Id).Distinct().Count());
            var arrangements = new HashSet<string>();
            foreach (var music in MusicCatalog.All)
            {
                var stage = MusicStage.Generate(music);
                Check.True(stage.Slots.Count > 0);
                Check.Equal(Fingerprint(stage), Fingerprint(MusicStage.Generate(music)));
                arrangements.Add(string.Join("|", stage.Slots.Select(x => x.StartTick + ":" + x.Kind + ":" + x.DurationTicks)));
                Check.Equal(stage.Slots.Count, stage.Slots.Select(x => (x.StartTick, x.Kind, x.DurationTicks)).Distinct().Count());
                int previousTick = -1;
                foreach (var slot in stage.Slots)
                {
                    Check.True(slot.StartTick >= music.TicksPerBar && slot.StartTick >= previousTick);
                    Check.True(slot.StartTick < music.TotalTicks && slot.EndTick <= music.TotalTicks);
                    Check.True(music.SectionAtBar(slot.StartTick / music.TicksPerBar).AllowsResponse);
                    Check.True(slot.Weight > 0 && !double.IsNaN(slot.Weight));
                    previousTick = slot.StartTick;
                }
                foreach (GestureKind kind in Enum.GetValues(typeof(GestureKind))) Check.True(stage.Slots.Any(x => x.Kind == kind));
            }
            Check.Equal(5, arrangements.Count);
        }

        [Test]
        public void TempoChangesSecondsWithoutMovingTheBeatGrid()
        {
            Check.Equal(.5, RhythmTime.Seconds(4, 120));
            Check.Equal(.25, RhythmTime.Seconds(2, 120));
            var slow = TestMusic(new[] { new SlotTemplate(GestureKind.Tap, 0) }, bpm: 60);
            var fast = TestMusic(new[] { new SlotTemplate(GestureKind.Tap, 0) }, bpm: 120);
            Check.Equal(Fingerprint(MusicStage.Generate(slow)), Fingerprint(MusicStage.Generate(fast)));
            Check.Equal(slow.DurationSeconds, fast.DurationSeconds * 2);
        }

        [Test]
        public void HighlightWeightsReachWholePatternCandidates()
        {
            var stage = MusicStage.Generate(MusicCatalog.All[0]);
            var candidates = stage.FindPlacements(Triplet()).ToArray();
            Check.Equal(1.0, candidates.Single(x => x.StartTick == 16).Weight);
            Check.Equal(3.0, candidates.Single(x => x.StartTick == 112).Weight);
            Check.True(Math.Abs(candidates.Single(x => x.StartTick == 108).Weight - 7.0 / 3) < .000001);
            Check.Equal(1.25, candidates.Single(x => x.StartTick == 176).Weight);
            Check.Equal(58, candidates.Length);
        }

        [Test]
        public void MatchingPreservesSpacingInsteadOfTakingTheNextThreeEligibleSlots()
        {
            var stage = MusicStage.Generate(TestMusic(new[]
            {
                new SlotTemplate(GestureKind.Tap, 0), new SlotTemplate(GestureKind.Tap, 8), new SlotTemplate(GestureKind.Tap, 12)
            }));
            var candidates = stage.FindPlacements(Triplet()).ToArray();
            Check.False(candidates.Any(x => x.StartTick == 16)); // Missing beat at 20 must not be skipped.
            Check.Equal(24, candidates.Single().StartTick); // 24, 28, 32 crosses a bar boundary correctly.
            Check.Equal(3, candidates[0].Slots.Count);
            Check.Equal(32, candidates[0].EndTick);
            Check.Equal(Fingerprint(stage), Fingerprint(MusicStage.Generate(stage.Music))); // Query consumes nothing.
        }

        [Test]
        public void EveryCandidateReservesItsOwnCallAndNeverTruncatesAtSongEnd()
        {
            var stage = MusicStage.Generate(TestMusic(new[] { new SlotTemplate(GestureKind.Tap, 0) }, intro: false));
            var single = new RhythmPattern("tap", 4, new[] { new PatternStep(GestureKind.Tap, 0) });
            var fits = stage.FindPlacements(single).ToArray();
            Check.Equal(2, fits.Length); // t=0 has no lead-in; t=16 and t=32 each have one.
            Check.Equal(12, fits[0].CueStartTick); Check.Equal(28, fits[1].CueStartTick);
            var longCue = new RhythmPattern("long-call", 33, single.Steps);
            Check.Equal(0, stage.FindPlacements(longCue).Count());
            var outOfBounds = new RhythmPattern("long-pattern", 4, new[]
            { new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 48) });
            Check.Equal(0, stage.FindPlacements(outOfBounds).Count());
            var huge = new RhythmPattern("huge", 4, new[]
            { new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, int.MaxValue) });
            Check.Equal(0, stage.FindPlacements(huge).Count());
        }

        [Test]
        public void DurationAndReleaseMustMatchAndCompoundGesturesCanShareTiming()
        {
            var stage = MusicStage.Generate(MusicCatalog.All[0]);
            var compound = new RhythmPattern("dive-shake-flick", 4, new[]
            {
                new PatternStep(GestureKind.Dive, 0, 8), new PatternStep(GestureKind.Tap, 0),
                new PatternStep(GestureKind.Shake, 4), new PatternStep(GestureKind.Flick, 8)
            });
            var candidate = stage.FindPlacements(compound).First();
            Check.Equal(16, candidate.StartTick); Check.Equal(24, candidate.EndTick);
            Check.Equal(4, candidate.Slots.Count);
            Check.Equal(TouchTransition.None, TouchRequirement.For(GestureKind.Shake).Start);
            Check.True(TouchRequirement.For(GestureKind.Shake).HoldThroughInterval);
            Check.Equal(TouchTransition.Release, TouchRequirement.For(GestureKind.Dive).End);
            Check.Equal(TouchTransition.Release, TouchRequirement.For(GestureKind.Flick).End);
            Check.Equal(TouchTransition.None, TouchRequirement.For(GestureKind.Hold).End);
            var wrongLength = new RhythmPattern("wrong-length", 4, new[] { new PatternStep(GestureKind.Dive, 0, 4) });
            Check.Equal(0, stage.FindPlacements(wrongLength).Count());
            Check.Equal(0, stage.FindPlacements(new RhythmPattern("no-release-slot", 4, new[]
            { new PatternStep(GestureKind.Dive, 0, 8), new PatternStep(GestureKind.Flick, 6) })).Count());
        }

        [Test]
        public void SustainsAreKeptWholeAndCannotCrossAResponseFreeSection()
        {
            var slots = new[] { new SlotTemplate(GestureKind.Dive, 12, 8) };
            var atEnd = MusicStage.Generate(TestMusic(slots));
            Check.Equal(1, atEnd.Slots.Count); Check.Equal(28, atEnd.Slots[0].StartTick);
            var gap = new MusicDefinition("gap", "Gap", 120, 4, new[]
            {
                new MusicSection("A", 0, 1, 1), new MusicSection("BREAK", 1, 1, 1, false), new MusicSection("B", 2, 1, 1)
            }, new[] { slots });
            Check.Equal(0, MusicStage.Generate(gap).Slots.Count);
            var exactEnd = MusicStage.Generate(TestMusic(new[] { new SlotTemplate(GestureKind.Dive, 12, 4) }));
            Check.Equal(2, exactEnd.Slots.Count); Check.Equal(48, exactEnd.Slots.Last().EndTick);
        }

        [Test]
        public void MusicAndPatternInputsAreCopiedAndMalformedDefinitionsFailEarly()
        {
            var steps = new List<PatternStep> { new PatternStep(GestureKind.Tap, 0) };
            var pattern = new RhythmPattern("copy", 4, steps); steps.Clear(); Check.Equal(1, pattern.Steps.Count);
            var templates = new List<SlotTemplate> { new SlotTemplate(GestureKind.Tap, 0) };
            var definition = TestMusic(templates); templates.Clear(); Check.Equal(2, MusicStage.Generate(definition).Slots.Count);
            Throws<ArgumentException>(() => new PatternStep(GestureKind.Dive, 0));
            Throws<ArgumentException>(() => new PatternStep(GestureKind.Tap, 0, 4));
            Throws<ArgumentException>(() => new RhythmPattern("bad", 0, pattern.Steps));
            Throws<ArgumentException>(() => new RhythmPattern("bad", 4, new[] { new PatternStep(GestureKind.Tap, 2) }));
            Throws<ArgumentException>(() => new RhythmPattern("bad", 4, pattern.Steps.Concat(pattern.Steps)));
            Throws<ArgumentException>(() => new SlotTemplate(GestureKind.Tap, 0, weight: double.NaN));
            Throws<ArgumentException>(() => TestMusic(new[] { new SlotTemplate(GestureKind.Tap, 16) }));
            Throws<ArgumentException>(() => TestMusic(new[] { new SlotTemplate(GestureKind.Tap, 0), new SlotTemplate(GestureKind.Tap, 0) }));
            Throws<ArgumentException>(() => new MusicDefinition("gap", "Gap", 120, 4,
                new[] { new MusicSection("A", 1, 1, 1) }, new[] { new SlotTemplate[0] }));
        }

        [Test]
        public void EncounterSelectionIsReproducibleAndCanChooseEveryScore()
        {
            var ids = new HashSet<string>();
            for (int seed = -100; seed < 100; seed++)
            {
                var expected = MusicCatalog.ForEncounter(seed, 2, 1, 2);
                ids.Add(expected.Music.Id);
                MusicCatalog.ForEncounter(seed, 1, 0, 0); // Other encounters must not advance a shared music RNG.
                var actual = MusicCatalog.ForEncounter(seed, 2, 1, 2);
                Check.Equal(expected.Music.Id, actual.Music.Id); Check.Equal(Fingerprint(expected), Fingerprint(actual));
            }
            Check.Equal(5, ids.Count);
        }

        [Test]
        public void SessionKeepsOneScoreForActiveCombatAndClearsItOnCompletionLossAndRestart()
        {
            var run = new RunSession(73);
            Check.True(run.BattleMusic == null);
            EnterFirstBattle(run);
            var original = run.BattleMusic;
            Check.True(original != null);
            Check.False(run.Enter(run.CurrentNode.Id));
            Check.False(run.ResolveBattle("stale-ticket", true, run.Health));
            original.FindPlacements(Triplet()).ToArray();
            foreach (var music in MusicCatalog.All) MusicStage.Generate(music); // Inspecting another score cannot reroll combat.
            Check.True(ReferenceEquals(original, run.BattleMusic));
            Check.True(run.ResolveBattle(run.StageTicket, true, run.Health)); Check.True(run.BattleMusic == null);
            run.Restart(73); EnterFirstBattle(run); Check.Equal(original.Music.Id, run.BattleMusic.Music.Id);
            Check.True(run.ResolveBattle(run.StageTicket, false, 0)); Check.True(run.BattleMusic == null);
            run.Restart(73); EnterFirstBattle(run); run.Abandon(); Check.True(run.BattleMusic == null);
            run.Restart(73); EnterFirstBattle(run); run.Restart(73); Check.True(run.BattleMusic == null);
        }

        [Test]
        public void EntireRunsGenerateMusicOnlyForBattlesIncludingBosses()
        {
            var kinds = new HashSet<StageKind>();
            for (int seed = 0; seed < 30; seed++)
            {
                var run = new RunSession(seed);
                for (int field = 1; field <= 2; field++)
                {
                    while (run.Phase != RunPhase.FieldCleared)
                    {
                        Check.True(run.Enter(run.Map.Nodes.First(x => run.CanEnter(x.Id)).Id));
                        kinds.Add(run.CurrentNode.Kind);
                        if (run.CurrentNode.IsBattle)
                        {
                            var expected = MusicCatalog.ForEncounter(seed, field, run.CurrentNode.Row, run.CurrentNode.Column);
                            Check.Equal(expected.Music.Id, run.BattleMusic.Music.Id);
                            Check.True(run.ResolveBattle(run.StageTicket, true, run.Health)); Check.True(run.SkipReward());
                        }
                        else { Check.True(run.BattleMusic == null); Check.True(run.LeaveService()); }
                        Check.True(run.BattleMusic == null);
                    }
                    Check.True(run.AdvanceField()); Check.True(run.BattleMusic == null);
                }
            }
            foreach (StageKind kind in Enum.GetValues(typeof(StageKind)))
                if (kind != StageKind.Mystery) Check.True(kinds.Contains(kind));
            Check.False(kinds.Contains(StageKind.Mystery)); // Enter resolves the area before choosing music.
        }

        private static RhythmPattern Triplet() => new RhythmPattern("three-taps", 4, new[]
        { new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 4), new PatternStep(GestureKind.Tap, 8) });

        private static MusicDefinition TestMusic(IEnumerable<SlotTemplate> bar, bool intro = true, double bpm = 120)
        {
            return new MusicDefinition("test", "Test", bpm, 4, new[]
            { new MusicSection("INTRO", 0, 1, 1, !intro), new MusicSection("BODY", 1, 2, 1) }, new[] { bar });
        }

        private static void EnterFirstBattle(RunSession run)
        {
            while (true)
            {
                Check.True(run.Enter(run.Map.Nodes.First(x => run.CanEnter(x.Id)).Id));
                if (run.CurrentNode.IsBattle) return;
                Check.True(run.BattleMusic == null); Check.True(run.LeaveService());
            }
        }

        private static string Fingerprint(MusicStage stage) => string.Join("|", stage.Slots.Select(x =>
            x.Index + ":" + x.StartTick + ":" + x.Kind + ":" + x.DurationTicks + ":" + x.Weight));

        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new Exception("Expected " + typeof(T).Name);
        }
    }
}
