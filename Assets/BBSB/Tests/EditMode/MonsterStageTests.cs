using System;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class MonsterStageTests
    {
        [Test]
        public void OnePhraseStepsOutOnceAndReturnsAfterItsLastJudgmentWindow()
        {
            var round = Independent(32); var actor = round.Plan.Monsters[0]; var attack = actor.Attacks.Single();
            var motion = Motion(round, 0);
            double start = Time(round, attack.CallStartTick), end = Time(round, attack.PhraseEndTick) + round.HalfMissWindow;
            Check.Equal(0.0, motion.Evaluate(start - .01)); Check.Equal(0.0, motion.Evaluate(start));
            double halfway = motion.Evaluate(start + motion.ApproachSeconds / 2);
            Check.True(halfway > 0 && halfway < 1);
            for (double time = start + motion.ApproachSeconds; time <= end; time += .031)
                Check.Equal(1.0, motion.Evaluate(time));
            Check.Equal(1.0, motion.Evaluate(end));
            halfway = motion.Evaluate(end + motion.ReturnSeconds / 2);
            Check.True(halfway > 0 && halfway < 1);
            Check.True(motion.Evaluate(end + motion.ReturnSeconds + .001) < 1e-9);
        }

        [Test]
        public void LinkedCadenceStaysForwardAcrossEveryMemberAndInternalRest()
        {
            var stage = MusicStage.Generate(MusicCatalog.All.Single(x => x.Id == "rapid-drive"));
            var monster = MonsterCatalog.All.Single(x => x.Id == "seesaw-goblin");
            var chain = monster.PatternPlanner.Candidates(stage, monster).Single(x => x.CallStartTick == 16);
            var round = new RhythmRound(BattlePlanner.Resolve(stage,
                new[] { new MonsterProposal(monster.Id, monster, new System.Collections.Generic.List<PatternChain> { chain }) }, 1));
            Check.True(round.Plan.Attacks.Count > 1);
            var motion = Motion(round, 0);
            for (double time = Time(round, chain.CallStartTick) + motion.ApproachSeconds;
                time < Time(round, chain.PhraseEndTick) + round.HalfMissWindow; time += .041)
                Check.Equal(1.0, motion.Evaluate(time));
            Check.Equal(0.0, motion.Evaluate(Time(round, chain.PhraseEndTick) + round.HalfMissWindow + motion.ReturnSeconds + .01));
        }

        [Test]
        public void SilentWaitNeverAddsAnApproachHintBeforeTheRememberedBeat()
        {
            var stage = Stage();
            foreach (var monster in MonsterCatalog.All.Where(x => x.Patterns.Any(p => p.SilentWaitTicks > 0)))
            {
                var pattern = monster.Patterns.Single(x => x.SilentWaitTicks > 0);
                var placement = stage.FindPlacements(pattern.Pattern).Single(x => x.StartTick == 64);
                var round = new RhythmRound(BattlePlanner.Resolve(stage,
                    new[] { new MonsterProposal(monster.Id, monster, new[] { placement }) }, 1));
                var motion = Motion(round, 0);
                for (double time = Time(round, placement.CueStartTick) + round.BeatSeconds;
                    time <= Time(round, placement.StartTick); time += .013)
                    Check.Equal(1.0, motion.Evaluate(time));
            }
        }

        [Test]
        public void OnlyScheduledActorsAdvanceAndSharedPatternsKeepEveryAttacker()
        {
            var round = Independent(32, 64, 32);
            double first = Time(round, round.Plan.Monsters[0].Attacks.Single().CallStartTick) + .3;
            Check.Equal(1.0, Motion(round, 0).Evaluate(first));
            Check.Equal(0.0, Motion(round, 1).Evaluate(first));
            Check.Equal(1.0, Motion(round, 2).Evaluate(first));
            double later = Time(round, round.Plan.Monsters[1].Attacks.Single().CallStartTick) + .3;
            Check.Equal(0.0, Motion(round, 0).Evaluate(later));
            Check.Equal(1.0, Motion(round, 1).Evaluate(later));
            Check.Equal(0.0, Motion(round, 2).Evaluate(later));
        }

        [Test]
        public void SongPauseAndSeekingDoNotAccumulateOrReplayStageMovement()
        {
            var round = Independent(32); var motion = Motion(round, 0);
            double start = Time(round, round.Plan.Attacks.Single().CallStartTick);
            round.Advance(start + .1); double position = motion.Evaluate(round.ElapsedSeconds);
            round.Suspend(); round.Advance(100);
            Check.Equal(position, motion.Evaluate(round.ElapsedSeconds));
            round.Resume(false); round.Advance(15);
            Check.Equal(0.0, motion.Evaluate(round.ElapsedSeconds));
            Check.Equal(position, motion.Evaluate(start + .1));
            int count = round.Results.Count; decimal damage = round.TotalDamageTaken;
            for (int i = 0; i < 20; i++) motion.Evaluate(start + i * .05);
            Check.Equal(count, round.Results.Count); Check.Equal(damage, round.TotalDamageTaken);
        }

        [Test]
        public void OneToThreeActorsShareTheFloorAndFitBothLandscapeAspectRatios()
        {
            foreach (int count in new[] { 1, 2, 3 })
            foreach (double height in new[] { 720.0, 800.0 })
            foreach (double advance in new[] { 0.0, .5, 1.0 })
            {
                double previousY = 1, previousScale = 0;
                for (int i = 0; i < count; i++)
                {
                    var pose = BattleStageLayout.Monster(i, count, .24, .17, advance);
                    var home = BattleStageLayout.Monster(i, count, .24, .17, 0);
                    double side = BattleStageLayout.MonsterSize(count, 1280, height, pose.Scale);
                    Check.True(pose.X > .5 && pose.X <= home.X);
                    Check.True(pose.Y < .31 && pose.Y > .14 && pose.Y < previousY);
                    Check.True(pose.Scale > previousScale);
                    Check.True(pose.X + side * .5 / 1280 < 1 && pose.X - side * .5 / 1280 > .38);
                    Check.True(pose.Y + side / height < .8);
                    var raisedFloor = BattleStageLayout.Monster(i, count, .24, .22, advance);
                    Check.True(Math.Abs(raisedFloor.Y - pose.Y - .05) < 1e-9);
                    previousY = pose.Y; previousScale = pose.Scale;
                }
            }
        }

        private static MonsterStageMotion Motion(RhythmRound round, int index) =>
            new MonsterStageMotion(round.Plan.Monsters[index], round.Plan.Stage.Music.Bpm, round.HalfMissWindow);
        private static double Time(RhythmRound round, int tick) => RhythmTime.Seconds(tick, round.Plan.Stage.Music.Bpm);
        private static MusicStage Stage() => MusicStage.Generate(MusicCatalog.All.Single(x => x.Id == "steady-pulse"));
        private static RhythmRound Independent(params int[] ticks)
        {
            var stage = Stage(); var monster = MonsterCatalog.All.Single(x => x.Id == "tap-slime");
            var pattern = monster.Patterns[0];
            return new RhythmRound(BattlePlanner.Resolve(stage, ticks.Select((tick, i) =>
                new MonsterProposal("actor-" + i, monster, stage.FindPlacements(pattern.Pattern).Where(x => x.StartTick == tick))), 1));
        }
    }
}
