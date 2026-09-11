using System;
using System.Collections.Generic;
using System.Linq;
using BBSB.Core;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class MonsterThemesTests
    {
        [Test]
        public void EveryMonsterHasTwoReachablePatternsAndMostThemesUseTap()
        {
            Check.Equal(9, MonsterCatalog.All.Count);
            Check.Equal(5, MonsterCatalog.All.Count(x => x.MainGesture == GestureKind.Tap));
            foreach (var monster in MonsterCatalog.All)
            {
                Check.Equal(2, monster.Patterns.Count);
                foreach (var pattern in monster.Patterns)
                    Check.True(MusicCatalog.All.Any(music => BattlePlanner.Propose(MusicStage.Generate(music), monster.Id, monster, 17)
                        .Placements.Any(x => x.Pattern == pattern.Pattern)), monster.Id + "/" + pattern.Id + " is unreachable.");
                if (monster.Id != "spark-bat" && monster.Id != "iron-turtle")
                    Check.True(monster.Patterns.All(x => x.Pattern.Steps.All(step => step.Kind == monster.MainGesture)));
                if (monster.Id == "iron-turtle")
                    Check.True(monster.Patterns.All(x => x.Pattern.Steps[0].Kind == GestureKind.Hold));
            }
            var mixed = MonsterCatalog.All.Single(x => x.Id == "spark-bat");
            Check.True(mixed.Patterns.SelectMany(x => x.Pattern.Steps).Select(x => x.Kind).Distinct().Count() > 1);
        }

        [Test]
        public void TapThemesAppearMoreOftenAndTheMixedMonsterIsRarer()
        {
            var stage = Fixture(); int taps = 0, mixed = 0, specialist = 0;
            for (int seed = 0; seed < 600; seed++)
            {
                var monster = BattlePlanner.Generate(stage, StageKind.Monster, seed).Monsters.Single().Monster;
                if (monster.MainGesture == GestureKind.Tap) taps++;
                if (monster.Id == "spark-bat") mixed++;
                if (monster.Id == "iron-turtle") specialist++;
            }
            Check.True(taps > 330, "Tap themes should be the majority when all patterns fit.");
            Check.True(mixed > 0 && mixed < specialist * .7, "Mixed input should remain possible but noticeably rarer.");
        }

        [Test]
        public void PatternProposalsAreIndependentOfSiblingOrderAndPresence()
        {
            var stage = Fixture(); var source = MonsterCatalog.All.Single(x => x.Id == "tap-slime");
            var reversed = new MonsterDefinition(source.Id, source.Name, "", source.MainGesture, source.Patterns.Reverse());
            var together = BattlePlanner.Propose(stage, "slime", source, 81);
            Check.Equal(Fingerprint(together.Placements), Fingerprint(BattlePlanner.Propose(stage, "slime", reversed, 81).Placements));
            foreach (var pattern in source.Patterns)
            {
                var alone = new MonsterDefinition(source.Id, source.Name, "", source.MainGesture, new[] { pattern });
                Check.Equal(Fingerprint(together.Placements.Where(x => x.Pattern == pattern.Pattern)),
                    Fingerprint(BattlePlanner.Propose(stage, "slime", alone, 81).Placements));
            }
        }

        [Test]
        public void SelfOverlapRemovesOneWholeCalledPatternEvenWhenBothInputsAreTap()
        {
            var stage = Fixture(); var monster = MonsterCatalog.All.Single(x => x.Id == "tap-slime");
            var placements = monster.Patterns.Select(x => At(stage, x, 28)).ToArray();
            Check.False(InputCompatibility.Conflict(placements[0], placements[1], out _));
            var proposal = new MonsterProposal("one-slime", monster, placements);
            var plan = BattlePlanner.Resolve(stage, new[] { proposal }, 41);
            Check.Equal(1, plan.Attacks.Count); Check.Equal(1, plan.Withdrawals.Count);
            Check.True(plan.Withdrawals[0].IsSelfConflict);
            Check.Equal(plan.Attacks[0].Pattern.Call.Count, plan.Calls.Count);
            Check.True(plan.Calls.All(x => x.AttackId == plan.Attacks[0].Id));
            Check.True(plan.Withdrawals[0].Attack.Id != plan.Attacks[0].Id);
            var shared = BattlePlanner.Resolve(stage, new[]
            {
                new MonsterProposal("first", monster, new[] { placements[0] }),
                new MonsterProposal("second", monster, new[] { placements[1] })
            }, 41);
            Check.Equal(2, shared.Attacks.Count); Check.Equal(0, shared.Withdrawals.Count);
            var round = new RhythmRound(shared); round.Press(3.5, 0, 0);
            Check.Equal(2, round.PerfectCount);
        }

        [Test]
        public void SelfConflictIncludesCallsAndRestEvenWhenResponsesDoNotOverlap()
        {
            var stage = Fixture();
            var shortCue = Pattern("short", 4); var longCue = Pattern("long", 12);
            var monster = new MonsterDefinition("test", "test", "", GestureKind.Tap, new[] { shortCue, longCue });
            var a = At(stage, shortCue, 16); var b = At(stage, longCue, 24);
            Check.False(InputCompatibility.Conflict(a, b, out _));
            var overlap = BattlePlanner.Resolve(stage, new[] { new MonsterProposal("one", monster, new[] { a, b }) }, 4);
            Check.Equal(1, overlap.Withdrawals.Count); Check.True(overlap.Withdrawals[0].IsSelfConflict);
            var rest = BattlePlanner.Resolve(stage, new[] { new MonsterProposal("one", monster, new[] { a, At(stage, shortCue, 20) }) }, 4);
            Check.Equal(1, rest.Withdrawals.Count);
            var boundary = BattlePlanner.Resolve(stage, new[] { new MonsterProposal("one", monster, new[] { a, At(stage, shortCue, 24) }) }, 4);
            Check.Equal(2, boundary.Attacks.Count); Check.Equal(0, boundary.Withdrawals.Count);
        }

        [Test]
        public void TresilloCuesAndResponsesUseThreeThreeTwoTimingOnTheExistingGrid()
        {
            var stage = Fixture(); var slime = MonsterCatalog.All.Single(x => x.Id == "tap-slime");
            var cue = slime.Patterns.Single(x => x.Id == "tresillo-call-tap");
            var plan = BattlePlanner.Resolve(stage, new[] { new MonsterProposal("slime", slime, new[] { At(stage, cue, 28) }) }, 1);
            Check.True(plan.Calls.Select(x => x.Tick).SequenceEqual(new[] { 16, 22 }));
            var round = new RhythmRound(plan);
            round.Advance(2); Check.Equal(1, round.Calls.Count); Check.Equal(0, round.Results.Count);
            round.Advance(2.75); Check.Equal(2, round.Calls.Count); Check.Equal(0, round.Results.Count);
            round.Press(3.5, 0, 0); Check.Equal(1, round.PerfectCount);
            var bat = MonsterCatalog.All.Single(x => x.Id == "tresillo-bat");
            var pattern = bat.Patterns.Single(x => x.Id == "tresillo-taps");
            round = new RhythmRound(BattlePlanner.Resolve(stage,
                new[] { new MonsterProposal("bat", bat, new[] { At(stage, pattern, 32) }) }, 1));
            Check.True(round.Notes.Select(x => x.StartTick).SequenceEqual(new[] { 32, 38, 44 }));
            foreach (double time in new[] { 4.0, 4.75, 5.5 }) { round.Press(time, 0, 0); round.Release(time + .01, 0, 0); }
            Check.Equal(3, round.PerfectCount); Check.Equal(16, stage.Music.TicksPerBar);
        }

        [Test]
        public void OffbeatThemesKeepCallsOnBeatsAndTapResponsesBetweenBeats()
        {
            var stage = Fixture(); var monster = MonsterCatalog.All.Single(x => x.Id == "offbeat-goblin");
            var proposal = BattlePlanner.Propose(stage, monster.Id, monster, 32);
            Check.Equal(2, proposal.Placements.Select(x => x.Pattern.Id).Distinct().Count());
            foreach (var placement in proposal.Placements)
            {
                Check.Equal(0, placement.CueStartTick % 4);
                foreach (var step in placement.Pattern.Steps) Check.Equal(2, (placement.StartTick + step.OffsetTick) % 4);
            }
        }

        private static string Fingerprint(IEnumerable<PatternPlacement> placements)
            => string.Join("|", placements.Select(x => x.Pattern.Id + "@" + x.StartTick).OrderBy(x => x));
        private static MonsterPatternDefinition Pattern(string id, int cue)
            => new MonsterPatternDefinition(id, "", new RhythmPattern(id, cue, new[] { new PatternStep(GestureKind.Tap, 0) }),
                new[] { new CallSignal(0, "call", cue == 4 ? CallSound.Wood : CallSound.Bell,
                    cue == 4 ? CallMotion.Step : CallMotion.Hop) }, 4, 4, 1);
        private static PatternPlacement At(MusicStage stage, MonsterPatternDefinition pattern, int tick)
            => stage.FindPlacements(pattern.Pattern).Single(x => x.StartTick == tick);
        private static MusicStage Fixture()
        {
            var slots = new List<SlotTemplate>();
            for (int tick = 0; tick < 16; tick += 2)
            {
                slots.Add(new SlotTemplate(GestureKind.Tap, tick)); slots.Add(new SlotTemplate(GestureKind.Flick, tick));
                foreach (var kind in new[] { GestureKind.Hold, GestureKind.Dive, GestureKind.Shake })
                    foreach (int duration in new[] { 4, 8, 16 }) slots.Add(new SlotTemplate(kind, tick, duration));
            }
            return MusicStage.Generate(new MusicDefinition("themes", "Themes", 120, 4,
                new[] { new MusicSection("INTRO", 0, 1, 1, false), new MusicSection("BODY", 1, 7, 1) }, new[] { slots }));
        }
    }
}
