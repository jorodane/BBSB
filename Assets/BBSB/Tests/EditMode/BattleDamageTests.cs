using System;
using System.Collections.Generic;
using System.Linq;
using BBSB.Core;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class BattleDamageTests
    {
        [Test]
        public void GradesTakeFullThreeQuartersAndHalfDamageWithoutRounding()
        {
            foreach (int damage in new[] { 1, 3, 4, 10 })
            {
                var miss = Round(damage, new PatternStep(GestureKind.Tap, 0));
                miss.Advance(2.2);
                var half = Round(damage, new PatternStep(GestureKind.Tap, 0));
                half.Press(2.1, 0, 0);
                var perfect = Round(damage, new PatternStep(GestureKind.Tap, 0));
                perfect.Press(2, 0, 0);
                Check.Equal(RhythmGrade.Miss, miss.Results.Single().Grade);
                Check.Equal(RhythmGrade.HalfMiss, half.Results.Single().Grade);
                Check.Equal(RhythmGrade.Perfect, perfect.Results.Single().Grade);
                Check.Equal((decimal)damage, miss.TotalDamageTaken);
                Check.Equal(damage * .75m, half.TotalDamageTaken);
                Check.Equal(damage * .5m, perfect.TotalDamageTaken);
                Check.Equal(.5, half.Results[0].Efficiency); // Score efficiency is not damage mitigation.
                Check.Equal(1.0, perfect.Results[0].Efficiency);
            }
        }

        [Test]
        public void EarlyMissAndRepeatedInputsCannotApplyDamageAgain()
        {
            var round = Round(3, new PatternStep(GestureKind.Tap, 0));
            int notifications = 0; round.ResultJudged += _ => notifications++;
            round.Press(1.8, 0, 0); round.Release(1.9, 0, 0);
            Check.Equal(MissReason.TooEarly, round.Results.Single().Reason);
            round.Press(2, 0, 0); round.Release(2.01, 0, 0);
            round.Advance(8.2); round.Advance(9);
            Check.Equal(1, notifications); Check.Equal(1, round.Results.Count);
            Check.Equal(3m, round.TotalDamageTaken);
        }

        [Test]
        public void HoldsAndDivesApplyDamageOnlyWhenTheirFinalGradeIsKnown()
        {
            foreach (var kind in new[] { GestureKind.Hold, GestureKind.Dive })
            {
                var round = Round(3, new PatternStep(kind, 0, 4));
                round.Press(2, 0, 0); round.Advance(2.2);
                Check.Equal(0m, round.TotalDamageTaken);
                if (kind == GestureKind.Hold) round.Advance(2.5); else round.Release(2.5, 0, 0);
                Check.Equal(RhythmGrade.Perfect, round.Results.Single().Grade);
                Check.Equal(1.5m, round.TotalDamageTaken);
                round.Release(2.6, 0, 0); round.Advance(8.2);
                Check.Equal(1.5m, round.TotalDamageTaken);

                var early = Round(3, new PatternStep(kind, 0, 4));
                early.Press(2, 0, 0); early.Release(2.1, 0, 0); early.Advance(8.2);
                Check.Equal(3m, early.TotalDamageTaken); Check.Equal(1, early.Results.Count);
            }
        }

        [Test]
        public void ShakeAndFlickUseTheSameIncomingDamageRules()
        {
            var shake = Round(3, new PatternStep(GestureKind.Shake, 0, 4));
            shake.Press(2, 0, 0);
            for (int i = 1; i <= 10; i++) shake.Move(2 + i * .05, i % 2 == 0 ? 0 : .1, 0);
            Check.Equal(RhythmGrade.Perfect, shake.Results.Single().Grade);
            Check.Equal(1.5m, shake.TotalDamageTaken);
            shake.Release(2.6, 0, 0); shake.Advance(8.2);
            Check.Equal(1.5m, shake.TotalDamageTaken);

            var flick = Round(3, new PatternStep(GestureKind.Flick, 0));
            flick.Press(1.9, 0, 0); flick.Move(1.96, .08, 0);
            Check.Equal(0m, flick.TotalDamageTaken);
            flick.Release(2, .16, 0);
            Check.Equal(RhythmGrade.Perfect, flick.Results.Single().Grade);
            Check.Equal(1.5m, flick.TotalDamageTaken);
        }

        [Test]
        public void SharedInputTakesDamageFromEveryAttackingMonsterOnce()
        {
            var stage = Fixture();
            var round = new RhythmRound(BattlePlanner.Resolve(stage, new[]
            {
                Proposal(stage, "first", 4, new PatternStep(GestureKind.Tap, 0)),
                Proposal(stage, "second", 8, new PatternStep(GestureKind.Tap, 0))
            }, 1));
            round.Press(2, 0, 0); round.Release(2.01, 0, 0); round.Advance(8.2);
            Check.Equal(2, round.PerfectCount); Check.Equal(6m, round.TotalDamageTaken);
        }

        [Test]
        public void PauseCannotChargeDamageAndStoppingDoesNotInventFutureMisses()
        {
            var round = Round(4, new PatternStep(GestureKind.Hold, 0, 8));
            round.Press(2, 0, 0); round.Advance(2.2); Check.True(round.Suspend());
            round.Advance(100); round.Press(100, 0, 0); round.Release(100, 0, 0);
            Check.Equal(0m, round.TotalDamageTaken); Check.Equal(2.2, round.ElapsedSeconds);
            round.Resume(true); round.Advance(3);
            Check.Equal(2m, round.TotalDamageTaken);
            round.Stop(); round.Advance(100);
            Check.Equal(2m, round.TotalDamageTaken); Check.Equal(1, round.Results.Count);
            Check.True(round.Aborted);
        }

        [Test]
        public void ActiveRunTakesDamageImmediatelyAndCompletedRoundsKeepHealthAndStage()
        {
            var run = BattleSession(); var plan = run.BattlePlan; string ticket = run.StageTicket;
            decimal initial = run.Health; int gold = run.Gold;
            var round = run.StartRhythmRound(); Check.True(round != null);
            Check.True(run.StartRhythmRound() == null);
            MissFirst(round);
            Check.True(round.TotalDamageTaken > 0);
            Check.Equal(initial - round.TotalDamageTaken, run.Health);
            round.Advance(plan.Stage.Music.DurationSeconds + 1);
            Check.True(round.Finished); Check.False(round.Aborted);
            Check.Equal(initial - round.TotalDamageTaken, run.Health);
            Check.Equal(RunPhase.Stage, run.Phase); Check.Equal(ticket, run.StageTicket);
            Check.Equal(gold, run.Gold); Check.Equal(0, run.Offers.Count);
            decimal remaining = run.Health;
            Check.True(run.CloseRhythmRound(round)); Check.Equal(remaining, run.Health);
            var replay = run.StartRhythmRound();
            Check.True(ReferenceEquals(plan, replay.Plan)); Check.Equal(0, replay.Results.Count);
            MissFirst(replay);
            Check.Equal(remaining - replay.TotalDamageTaken, run.Health);
        }

        [Test]
        public void LeavingPreparationAndOldRoundReferencesCannotRestoreOrDamageHealth()
        {
            var run = BattleSession(); var round = run.StartRhythmRound(); MissFirst(round);
            decimal remaining = run.Health; int results = round.Results.Count;
            Check.True(run.CloseRhythmRound(round)); Check.False(run.CloseRhythmRound(round));
            round.Advance(10000); round.Press(10000, 0, 0);
            Check.True(round.Aborted); Check.Equal(results, round.Results.Count);
            Check.Equal(remaining, run.Health);
            var next = run.StartRhythmRound();
            Check.False(run.CloseRhythmRound(round)); Check.True(ReferenceEquals(next, run.ActiveRhythmRound));
            Check.Equal(remaining, run.Health);
        }

        [Test]
        public void LethalDamageClampsToZeroStopsTheSongAndEndsTheRunOnce()
        {
            var run = BattleSession(1); var round = run.StartRhythmRound(); string ticket = run.StageTicket;
            round.Advance(round.Plan.Stage.Music.DurationSeconds + 1);
            Check.Equal(0m, run.Health); Check.Equal(RunPhase.GameOver, run.Phase);
            Check.True(round.Finished); Check.True(round.Aborted);
            Check.Equal(1, round.Results.Count); Check.Equal(0, run.Offers.Count);
            Check.True(run.StageTicket == null); Check.True(run.ActiveRhythmRound == null);
            Check.Equal(0, run.Gold); Check.Equal(0, run.Weapons.Count);
            Check.True(run.StartRhythmRound() == null); Check.False(run.ResolveBattle(ticket, true, 1));
            round.Advance(10000); round.Release(10000, 0, 0);
            Check.Equal(1, round.Results.Count); Check.Equal(0m, run.Health);
        }

        [Test]
        public void RestartAndExternalBattleResultsDetachThePreviousPerformance()
        {
            var run = BattleSession(); var old = run.StartRhythmRound(); MissFirst(old);
            run.Restart(73); Check.True(old.Aborted); old.Advance(10000);
            Check.Equal(10000m, run.Health); Check.True(run.ActiveRhythmRound == null);

            run = BattleSession(); old = run.StartRhythmRound(); MissFirst(old);
            decimal remaining = run.Health; string ticket = run.StageTicket;
            Check.True(run.ResolveBattle(ticket, true, remaining));
            Check.True(old.Aborted); Check.True(run.ActiveRhythmRound == null);
            old.Advance(10000); Check.Equal(remaining, run.Health);
            Check.Equal(RunPhase.Reward, run.Phase); Check.False(run.ResolveBattle(ticket, true, remaining));
        }

        [Test]
        public void FractionalHealthSurvivesRewardsAndHealingWithoutRounding()
        {
            var run = BattleSession(100);
            Check.True(run.ResolveBattle(run.StageTicket, true, 50.25m));
            Check.True(run.ChooseReward(1)); Check.True(run.UseItem(0));
            Check.Equal(75.25m, run.Health);
            run.Restart(73); Check.Equal(100m, run.Health);
        }

        [Test]
        public void ZeroDamageStillJudgesAndNegativeBaseDamageIsRejected()
        {
            var zero = Round(0, new PatternStep(GestureKind.Tap, 0)); zero.Advance(2.2);
            Check.Equal(1, zero.MissCount); Check.Equal(0m, zero.TotalDamageTaken);
            bool rejected = false;
            try { Round(-1, new PatternStep(GestureKind.Tap, 0)); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Check.True(rejected);
        }

        private static void MissFirst(RhythmRound round)
        { round.Advance(round.Notes[0].EndSeconds + round.HalfMissWindow + .001); }

        private static RunSession BattleSession(int health = 10000)
        {
            var run = new RunSession(73, new RunRules(startingHealth: health));
            while (true)
            {
                Check.True(run.Enter(run.Map.Nodes.First(x => run.CanEnter(x.Id)).Id));
                if (run.CurrentNode.IsBattle) return run;
                Check.True(run.LeaveService());
            }
        }

        private static RhythmRound Round(int damage, params PatternStep[] steps)
        {
            var stage = Fixture();
            return new RhythmRound(BattlePlanner.Resolve(stage, new[] { Proposal(stage, "sample", damage, steps) }, 1));
        }

        private static MonsterProposal Proposal(MusicStage stage, string id, int damage, params PatternStep[] steps)
        {
            var pattern = new RhythmPattern(id, 4, steps);
            var monster = new MonsterDefinition(id, id, "", pattern, new[] { new CallSignal(0, "call") },
                Math.Max(4, pattern.EndOffsetTick), 4, 1, damage);
            return new MonsterProposal(id, monster, stage.FindPlacements(pattern).Where(x => x.StartTick == 16));
        }

        private static MusicStage Fixture()
        {
            var slots = new List<SlotTemplate>();
            for (int tick = 0; tick < 16; tick += 2)
            {
                slots.Add(new SlotTemplate(GestureKind.Tap, tick)); slots.Add(new SlotTemplate(GestureKind.Flick, tick));
                foreach (var kind in new[] { GestureKind.Hold, GestureKind.Dive, GestureKind.Shake })
                    foreach (int duration in new[] { 4, 8 }) slots.Add(new SlotTemplate(kind, tick, duration));
            }
            return MusicStage.Generate(new MusicDefinition("damage-fixture", "Damage fixture", 120, 4, new[]
            { new MusicSection("INTRO", 0, 1, 1, false), new MusicSection("BODY", 1, 3, 1) }, new[] { slots }));
        }
    }
}
