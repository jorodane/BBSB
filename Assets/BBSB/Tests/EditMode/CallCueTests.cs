using System;
using System.Linq;
using BBSB.Core;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class CallCueTests
    {
        [Test]
        public void CatalogCuesIdentifyBothPatternsBySoundAndMotionBeforeAnyResponse()
        {
            foreach (var monster in MonsterCatalog.All)
            {
                var a = monster.Patterns[0]; var b = monster.Patterns[1];
                int expected = monster.Id == "iron-turtle" ? 4 : 0;
                foreach (bool sound in new[] { true, false })
                {
                    Check.Equal(expected, CallReadability.FirstDifferenceTick(a, b, sound));
                    Check.Equal(expected, CallReadability.FirstDifferenceTick(b, a, sound));
                    Check.True(Math.Min(a.Pattern.CueLeadTicks, b.Pattern.CueLeadTicks) - expected >= 4);
                }
            }
        }

        [Test]
        public void DifferentCaptionsCannotDisguiseIdenticalCues()
        {
            Reject(() => Monster(Pattern("a", 8, new CallSignal(0, "쿵")),
                Pattern("b", 8, new CallSignal(0, "레몬!"))));
        }

        [Test]
        public void BothSoundAndMotionMustWorkWithoutTheOtherChannel()
        {
            var original = Pattern("a", 8, Cue(0));
            Reject(() => Monster(original, Pattern("b", 8, new CallSignal(0, "other", CallSound.Bell, CallMotion.Stomp))));
            Reject(() => Monster(original, Pattern("b", 8, new CallSignal(0, "other", CallSound.Drum, CallMotion.Flash))));
            var distinct = Pattern("b", 8, new CallSignal(0, "쿵", CallSound.Bell, CallMotion.Flash));
            Check.Equal(2, Monster(original, distinct).Patterns.Count);
        }

        [Test]
        public void ResponseDelayAloneCannotIdentifyAnOtherwiseIdenticalCall()
        {
            Reject(() => Monster(Pattern("a", 4, Cue(0)), Pattern("b", 12, Cue(0))));
        }

        [Test]
        public void EqualLengthCallsNeedDifferentTypesNotJustRepetitionOrSpacing()
        {
            var single = Pattern("a", 12, Cue(0));
            var repeated = Pattern("b", 12, Cue(0), Cue(4));
            var shifted = Pattern("c", 12, Cue(0), Cue(6));
            Reject(() => Monster(single, repeated));
            Reject(() => Monster(repeated, shifted));
        }

        [Test]
        public void SharedPrefixCannotResolveAtOrAfterTheShorterResponse()
        {
            var shortCall = Pattern("a", 4, Cue(0));
            Reject(() => Monster(shortCall, Pattern("b", 12, Cue(0), Tail(4))));
            Reject(() => Monster(shortCall, Pattern("b", 12, Cue(0), Tail(8))));
        }

        [Test]
        public void SharedPrefixLeavesAFullBeatToPrepareIncludingTheSilentAlternative()
        {
            var single = Pattern("a", 8, Cue(0));
            Reject(() => Monster(single, Pattern("late", 8, Cue(0), Tail(6))));
            var doubleCall = Pattern("b", 8, Cue(0), Tail(4));
            Check.Equal(4, CallReadability.FirstDifferenceTick(single, doubleCall, true));
            Check.Equal(2, Monster(single, doubleCall).Patterns.Count);
        }

        [Test]
        public void ImmediateCuesStillLeaveAFullBeatToRecognizeThePattern()
        {
            var a = Pattern("a", 4, Cue(0)); var b = Pattern("b", 6, Tail(0));
            Check.Equal(2, Monster(a, b).Patterns.Count);
            Reject(() => Monster(Pattern("too-soon", 2, Cue(0)), b));
        }

        [Test]
        public void FoxfireFinishesBothCallsBeforeResponsesAndTheSingleEndsWithThePair()
        {
            var fox = MonsterCatalog.All.Single(m => m.Id == "offbeat-goblin");
            foreach (double bpm in new[] { 60.0, 120.0, 168.0, 240.0 })
            {
                var single = new MonsterPreview(fox, fox.Patterns.Single(p => p.Id == "offbeat-single-tap"), bpm);
                var pair = new MonsterPreview(fox, fox.Patterns.Single(p => p.Id == "offbeat-pair"), bpm);
                double start = single.CallSeconds;
                Check.True(Math.Abs(single.Round.Notes[0].StartSeconds - (start + 3.5 * single.BeatSeconds)) < 1e-8);
                Check.True(Math.Abs(pair.Round.Notes[0].StartSeconds - (start + 2.5 * pair.BeatSeconds)) < 1e-8);
                Check.Equal(single.Round.Notes[0].StartSeconds, pair.Round.Notes[1].StartSeconds);
                Check.Equal(single.Attack.PhraseEndTick, pair.Attack.PhraseEndTick);
                double lastCall = RhythmTime.Seconds(pair.Attack.Call.Last().Tick, bpm);
                Check.True(pair.Round.Notes[0].StartSeconds - pair.Round.HalfMissWindow - lastCall >= pair.BeatSeconds);
                foreach (var preview in new[] { single, pair })
                {
                    var round = preview.Round;
                    round.Advance(lastCall); Check.Equal(0, round.Results.Count);
                    foreach (var note in round.Notes)
                    {
                        Check.Equal(2, (note.StartTick - preview.Attack.CallStartTick) % 4);
                        round.Press(note.StartSeconds, 0, 0); round.Release(note.StartSeconds + .001, 0, 0);
                    }
                    Check.Equal(round.Notes.Count, round.PerfectCount);
                }
            }
        }

        [Test]
        public void AllSiblingPairsAreValidatedNotOnlyTheFirstPattern()
        {
            var a = Pattern("a", 8, new CallSignal(0, "one", CallSound.Wood, CallMotion.Step));
            var b = Pattern("b", 8, Cue(0)); var c = Pattern("c", 12, Cue(0));
            Check.Equal(2, Monster(a, b).Patterns.Count);
            Check.Equal(2, Monster(a, c).Patterns.Count);
            Reject(() => Monster(a, b, c));
        }

        [Test]
        public void TurtleCuesSurviveSchedulingAndBothResponsesCanBePlayedInTheSameSong()
        {
            var monster = MonsterCatalog.All.Single(x => x.Id == "iron-turtle");
            var stage = MusicStage.Generate(MusicCatalog.All.Single(x => x.Id == "steady-pulse"));
            foreach (var pattern in monster.Patterns)
            {
                var placement = stage.FindPlacements(pattern.Pattern).First(x => x.StartTick >= 32);
                var plan = BattlePlanner.Resolve(stage,
                    new[] { new MonsterProposal("turtle", monster, new[] { placement }) }, 8);
                var attack = plan.Attacks.Single(); var round = new RhythmRound(plan);
                Check.Equal(8, attack.ResponseStartTick - attack.CallStartTick);
                for (int i = 0; i < plan.Calls.Count; i++)
                {
                    Check.Equal(pattern.Call[i].Sound, plan.Calls[i].Sound);
                    Check.Equal(pattern.Call[i].Motion, plan.Calls[i].Motion);
                    Check.Equal(attack.CallStartTick + pattern.Call[i].OffsetTick, plan.Calls[i].Tick);
                }
                round.Advance(Time(attack.CallStartTick + 4, stage));
                Check.Equal(pattern.Call.Count, round.Calls.Count); Check.Equal(0, round.Results.Count);
                double start = Time(attack.ResponseStartTick, stage);
                round.Press(start, 0, 0); round.Advance(Time(attack.ResponseStartTick + 8, stage));
                round.Release(Time(attack.ResponseStartTick + 8, stage) + .01, 0, 0);
                if (pattern.Pattern.Steps.Count == 2)
                {
                    Check.Equal(CallMotion.TailSweep, pattern.Call[1].Motion);
                    Check.Equal(GestureKind.Tap, pattern.Pattern.Steps[1].Kind);
                    round.Press(Time(attack.ResponseStartTick + 12, stage), 0, 0);
                }
                Check.Equal(pattern.Pattern.Steps.Count, round.PerfectCount);
                Check.Equal(0, round.MissCount); Check.Equal(0, round.HalfMissCount);
            }
        }

        private static CallSignal Cue(int tick) => new CallSignal(tick, "쿵", CallSound.Drum, CallMotion.Stomp);
        private static CallSignal Tail(int tick) => new CallSignal(tick, "휙", CallSound.Sweep, CallMotion.TailSweep);
        private static MonsterPatternDefinition Pattern(string id, int cue, params CallSignal[] calls)
            => new MonsterPatternDefinition(id, "", new RhythmPattern(id, cue, new[] { new PatternStep(GestureKind.Tap, 0) }), calls, 4, 4, 1);
        private static MonsterDefinition Monster(params MonsterPatternDefinition[] patterns)
            => new MonsterDefinition("test", "test", "", GestureKind.Tap, patterns);
        private static double Time(int tick, MusicStage stage) => RhythmTime.Seconds(tick, stage.Music.Bpm);
        private static void Reject(Action action)
        {
            try { action(); } catch (ArgumentException) { return; }
            throw new Exception("An unreadable cue pair should have been rejected.");
        }
    }
}
