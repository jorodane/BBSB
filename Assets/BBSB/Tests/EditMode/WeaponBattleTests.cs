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
    public sealed class WeaponBattleTests
    {
        [Test]
        public void AllEightWeaponsHavePlayablePatternsAndProduceTheirDistinctEffects()
        {
            Check.Equal(8, WeaponCatalog.All.Count);
            var damage = new Dictionary<string, decimal> { { "sword", 28 }, { "shield", 0 }, { "spear", 20 },
                { "hammer", 50 }, { "dagger", 12 }, { "greatsword", 36 }, { "bell", 8 }, { "blade", 16 } };
            foreach (var weapon in WeaponCatalog.All)
            {
                Check.True(InputCompatibility.IsPlayable(weapon.Pattern));
                var plan = Plan(weapon.Pattern.Steps.ToArray());
                var loadout = Loadout(plan, weapon.Id); Place(loadout, 0, plan.Attacks[0], 0);
                var round = Round(plan, loadout);
                Perform(round, weapon.Pattern.Steps, 2);
                Check.Equal(damage[weapon.Id], round.Combat.TotalDamage);
                Check.Equal(1000 - damage[weapon.Id], round.Combat.EnemyHealth.Current);
                Check.Equal(0m, round.TotalDamageTaken);
                Check.Equal(weapon.Pattern.Steps.Count, round.PerfectCount);
                Check.Equal(weapon.Pattern.Steps.Count, round.Notes.Count);
                Check.Equal(weapon.Pattern.Steps.Count, round.WeaponResults.Count);
                Check.Equal(weapon.Id == "shield" ? 12m : 0m, round.Combat.GuardAt(round.ElapsedSeconds));
            }
        }

        [Test]
        public void HalfMissHalvesBasicWeaponsAndPrecisionSpearDoesNotFire()
        {
            foreach (var pair in new[] { ("sword", 14m), ("spear", 0m), ("greatsword", 13m), ("shield", 0m), ("blade", 8m) })
            {
                var weapon = WeaponCatalog.Find(pair.Item1); var plan = Plan(weapon.Pattern.Steps.ToArray());
                var loadout = Loadout(plan, pair.Item1); Place(loadout, 0, plan.Attacks[0], 0);
                var round = Round(plan, loadout); Perform(round, weapon.Pattern.Steps, 2, .1);
                Check.Equal(pair.Item2, round.Combat.TotalDamage);
                Check.Equal(pair.Item1 == "shield" ? 4m : 0m, round.Combat.GuardAt(round.ElapsedSeconds));
            }
        }

        [Test]
        public void SharedInputTriggersEveryEquippedCopyWithoutDuplicatingMonsterDamageOrBodyMotion()
        {
            var plan = Plan(new PatternStep(GestureKind.Tap, 0));
            var loadout = Loadout(plan, "spear", "spear", "spear", "spear", "spear");
            for (int slot = 0; slot < 5; slot++) Place(loadout, slot, plan.Attacks[0], 0);
            var round = Round(plan, loadout, 20); round.Press(2, 0, 0);
            Check.Equal(100m, round.Combat.TotalDamage); Check.Equal(5, round.Combat.Activations.Count);
            Check.True(round.Combat.Victory); Check.Equal(0m, round.Combat.EnemyHealth.Current);
            Check.Equal(1, round.Notes.Count); Check.Equal(1, round.Results.Count);
            Check.Equal(5, round.WeaponResults.Count);
            Check.True(round.WeaponResults.All(x => ReferenceEquals(x.Source, round.Results[0])));
            Check.True(round.Combat.Activations.All(x => x.AtSeconds == 2));
            Check.Equal(1, round.PerfectCount); Check.Equal(100.0, round.ScorePercent);
            var motion = new PlayerMotionTimeline(1); motion.Evaluate(round);
            Check.Equal(1, motion.PunchSelections);
            round.Release(2.001, 0, 0); round.Advance(2.1);
            Check.Equal(100m, round.Combat.TotalDamage);
        }

        [Test]
        public void PlacementRequiresExistingMonsterBeatsAndCannotAddAnExtraInput()
        {
            var plan = Plan(new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 4));
            var loadout = Loadout(plan, "sword", "dagger"); var source = plan.Attacks[0];
            Place(loadout, 0, source, 0);
            Check.False(loadout.TryPlace(1, source.MonsterId, source.Pattern.Id, 0, out _));
            Check.Equal(0, loadout.ValidOffsets(1, source).Count);
            var round = Round(plan, loadout);
            var unarmed = new RhythmRound(plan);
            Check.Equal(unarmed.ResponseNoteCount, round.ResponseNoteCount);
            Check.True(round.Notes.Select(x => (x.Attack.Id, x.StepIndex, x.StartTick, x.Step.Kind, x.Step.DurationTicks))
                .SequenceEqual(unarmed.Notes.Select(x => (x.Attack.Id, x.StepIndex, x.StartTick, x.Step.Kind, x.Step.DurationTicks))));
            Perform(round, source.Placement.Pattern.Steps, 2);
            Check.Equal(2, round.PerfectCount); Check.Equal(0, round.MissCount); Check.Equal(28m, round.Combat.TotalDamage);
            Check.False(round.FreeInput.Kind.HasValue);
        }

        [Test]
        public void SharedMissReachesEveryWeaponButDamagesAndReactsOnlyOnce()
        {
            var plan = Plan(new PatternStep(GestureKind.Tap, 0));
            var loadout = Loadout(plan, "spear", "spear", "spear", "spear", "spear");
            for (int slot = 0; slot < 5; slot++) Place(loadout, slot, plan.Attacks[0], 0);
            var round = Round(plan, loadout); var motion = new PlayerMotionTimeline(3);
            round.Advance(2.2); var frame = motion.Evaluate(round);
            Check.Equal(4m, round.TotalDamageTaken); Check.Equal(1, round.MissCount);
            Check.Equal(5, round.WeaponResults.Count(x => x.Grade == RhythmGrade.Miss));
            Check.True(round.WeaponResults.All(x => ReferenceEquals(x.Source, round.Results.Single())));
            Check.Equal(0, round.Combat.Activations.Count); Check.Equal(0m, round.Combat.TotalDamage);
            Check.Equal(1, motion.PunchSelections); Check.True(frame.Grade == RhythmGrade.Miss);
        }

        [Test]
        public void SharedHoldStacksEveryShieldFromOneHalfMissJudgment()
        {
            var plan = Plan(new PatternStep(GestureKind.Hold, 0, 4));
            var loadout = Loadout(plan, "shield", "shield", "shield", "shield", "shield");
            for (int slot = 0; slot < 5; slot++) Place(loadout, slot, plan.Attacks[0], 0);
            var round = Round(plan, loadout); round.Press(2.1, 0, 0); round.Release(2.5, 0, 0);
            Check.Equal(1, round.HalfMissCount); Check.Equal(5, round.Combat.Activations.Count);
            Check.True(round.WeaponResults.All(x => x.Grade == RhythmGrade.HalfMiss && ReferenceEquals(x.Source, round.Results[0])));
            Check.Equal(2m, round.Combat.TotalBlocked); Check.Equal(0m, round.TotalDamageTaken);
            Check.Equal(28m, round.Combat.GuardAt(2.5)); // Five shields at half strength, one incoming hit.
        }

        [Test]
        public void MovingOneSlotReplacesItsAssignmentAndRepeatsOnEveryMatchingPhrase()
        {
            var plan = Plan(new[] { new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 4), new PatternStep(GestureKind.Tap, 8) }, new[] { 16, 48 });
            var loadout = Loadout(plan, "sword"); var source = plan.Attacks[0];
            Place(loadout, 0, source, 0); Place(loadout, 0, source, 4);
            Check.Equal(1, loadout.Placements.Count); Check.Equal(4, loadout.At(0).OffsetTick);
            var round = Round(plan, loadout);
            Check.Equal(6, round.Notes.Count); Check.Equal(4, round.Combat.Bindings.Count);
            Check.True(round.Combat.Bindings.Select(x => x.Note.StartTick).SequenceEqual(new[] { 20, 24, 52, 56 }));
            Check.False(loadout.TryPlace(0, source.MonsterId, source.Pattern.Id, 11, out _));
            Check.Equal(4, loadout.At(0).OffsetTick);
            Perform(round, source.Placement.Pattern.Steps, 2); Perform(round, source.Placement.Pattern.Steps, 6);
            Check.Equal(56m, round.Combat.TotalDamage); Check.Equal(6, round.PerfectCount);
            Check.Equal(2, round.Combat.Activations.Count(x => x.CompletedPattern));
        }

        [Test]
        public void WeaponsUseTheAuthoredDiveShakeAndFlickJudgmentsWithoutAddingGestures()
        {
            var plan = Plan(new PatternStep(GestureKind.Dive, 0, 4), new PatternStep(GestureKind.Shake, 0, 4),
                new PatternStep(GestureKind.Flick, 4)); var source = plan.Attacks[0];
            var loadout = Loadout(plan, "greatsword", "bell", "blade");
            Place(loadout, 0, source, 0); Place(loadout, 1, source, 0); Place(loadout, 2, source, 4);
            var round = Round(plan, loadout);
            round.Press(2, 0, 0); round.Move(2.08, .1, 0); round.Move(2.16, 0, 0);
            round.Move(2.25, 0, 0); round.Move(2.34, 0, 0); round.Move(2.42, .08, 0); round.Release(2.5, .16, 0);
            Check.Equal(3, round.PerfectCount); Check.Equal(0m, round.TotalDamageTaken);
            Check.Equal(3, round.Notes.Count); Check.Equal(3, round.Results.Count); Check.Equal(3, round.Combat.Activations.Count);
            Check.True(round.WeaponResults.All(x => round.Results.Contains(x.Source)));
            Check.Equal(78m, round.Combat.TotalDamage); // Bell 8, amplified greatsword 36 * 1.5, then blade 16.
        }

        [Test]
        public void OtherWeaponsNeverConsumeAPlacementAndSharedBeatsTriggerAllPatterns()
        {
            var plan = Plan(new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 2),
                new PatternStep(GestureKind.Tap, 4), new PatternStep(GestureKind.Tap, 8)); var source = plan.Attacks[0];
            foreach (var order in new[] { new[] { 0, 1, 2, 3, 4 }, new[] { 4, 3, 2, 1, 0 } })
            {
                var loadout = Loadout(plan, "sword", "spear", "hammer", "dagger", "spear");
                var offsets = Enumerable.Range(0, 5).Select(slot => loadout.ValidOffsets(slot, source).ToArray()).ToArray();
                foreach (int slot in order)
                {
                    Place(loadout, slot, source, 0);
                    for (int other = 0; other < 5; other++)
                        Check.True(offsets[other].SequenceEqual(loadout.ValidOffsets(other, source)));
                }
                var round = Round(plan, loadout); round.Press(2, 0, 0); round.Release(2.001, 0, 0);
                Check.Equal(5, round.Combat.Activations.Count); Check.Equal(1, round.PerfectCount);
                foreach (double at in new[] { 2.25, 2.5, 3.0 }) { round.Press(at, 0, 0); round.Release(at + .001, 0, 0); }
                Check.Equal(130m, round.Combat.TotalDamage); Check.Equal(4, round.Notes.Count); Check.Equal(4, round.PerfectCount);
            }
        }

        [Test]
        public void ActiveRoundSnapshotsArrangementSoEditingPreparationCannotRewriteIt()
        {
            var plan = Plan(new PatternStep(GestureKind.Tap, 0)); var loadout = Loadout(plan, "spear");
            Place(loadout, 0, plan.Attacks[0], 0); var round = Round(plan, loadout);
            loadout.Remove(0); round.Press(2, 0, 0);
            Check.Equal(20m, round.Combat.TotalDamage); Check.Equal(1, round.Combat.Loadout.Placements.Count);
            var replay = Round(plan, loadout); replay.Press(2, 0, 0); Check.Equal(0m, replay.Combat.TotalDamage);
        }

        [Test]
        public void OverlappingSourcePhrasesAreRemovedBeforeOfferingWeaponPlacements()
        {
            var plan = Plan(new[] { new PatternStep(GestureKind.Flick, 0) }, new[] { 16, 18 });
            Check.Equal(1, plan.Attacks.Count); Check.True(plan.Withdrawals.Any(x => x.IsSelfConflict));
            var loadout = Loadout(plan, "blade"); var source = plan.Attacks[0];
            Place(loadout, 0, source, 0);
            var round = Round(plan, loadout); Check.Equal(1, round.Notes.Count); Check.Equal(1, round.Combat.Bindings.Count);
        }

        [Test]
        public void ChainBonusRequiresCompletionAndDaggerChainResetsOnItsOwnMiss()
        {
            var plan = Plan(new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 4));
            var loadout = Loadout(plan, "sword"); Place(loadout, 0, plan.Attacks[0], 0);
            var round = Round(plan, loadout); round.Press(2, 0, 0); round.Release(2.001, 0, 0); round.Advance(2.7);
            Check.Equal(8m, round.Combat.TotalDamage);
            var dagger = WeaponCatalog.Find("dagger"); plan = Plan(dagger.Pattern.Steps.ToArray(), new[] { 16, 48 });
            loadout = Loadout(plan, "dagger"); Place(loadout, 0, plan.Attacks[0], 0);
            round = Round(plan, loadout); round.Press(2, 0, 0); round.Release(2.001, 0, 0); round.Advance(2.5);
            Perform(round, dagger.Pattern.Steps, 6); Check.Equal(17m, round.Combat.TotalDamage);
        }

        [Test]
        public void ShieldAbsorbsOnlyAfterActivationAndExpiresOnSongTime()
        {
            var plan = Plan(new PatternStep(GestureKind.Hold, 0, 4), new PatternStep(GestureKind.Tap, 8));
            var loadout = Loadout(plan, "shield"); Place(loadout, 0, plan.Attacks[0], 0);
            var round = Round(plan, loadout); round.Press(2, 0, 0); round.Release(2.5, 0, 0); round.Advance(3.2);
            Check.Equal(0m, round.TotalDamageTaken); Check.Equal(4m, round.Combat.TotalBlocked); Check.Equal(8m, round.Combat.GuardAt(3.2));
            Check.Equal(0m, round.Combat.GuardAt(4.51));
            round.Suspend(); round.Advance(100); Check.Equal(3.2, round.ElapsedSeconds); Check.Equal(8m, round.Combat.GuardAt(round.ElapsedSeconds));
        }

        [Test]
        public void LongFrameResolvesEarlierIncomingDamageBeforeALaterShield()
        {
            var plan = Plan(new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Hold, 2, 4));
            var loadout = Loadout(plan, "shield"); Place(loadout, 0, plan.Attacks[0], 2);
            var round = Round(plan, loadout); round.Press(2.25, 0, 0); round.Advance(3);
            Check.Equal(4m, round.TotalDamageTaken); Check.Equal(0m, round.Combat.TotalBlocked);
            Check.Equal(12m, round.Combat.GuardAt(3));
        }

        [Test]
        public void SharedHealthVictoryStopsNewPhrasesButAllowsTheActiveFinalInputAndFinale()
        {
            var plan = Plan(new[] { new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 4) }, new[] { 16, 48 });
            var loadout = Loadout(plan, "sword"); Place(loadout, 0, plan.Attacks[0], 0);
            var round = Round(plan, loadout, 8); round.Press(2, 0, 0); round.Release(2.001, 0, 0);
            Check.True(round.Combat.Victory); Check.False(round.Finished); Check.Equal(0m, round.Combat.EnemyHealth.Current);
            round.Press(2.5, 0, 0); round.Release(2.501, 0, 0);
            Check.True(round.Combat.FinaleSuccess); Check.Equal(28m, round.Combat.TotalDamage);
            round.Advance(100); Check.True(round.Finished); Check.False(round.Aborted);
            Check.Equal(2, round.Results.Count); Check.Equal(0, round.MissCount); Check.Equal(0m, round.TotalDamageTaken);
        }

        [Test]
        public void MissingTheOverkillFinalInputStillWinsWithoutIncomingDamage()
        {
            var plan = Plan(new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 4));
            var loadout = Loadout(plan, "sword"); Place(loadout, 0, plan.Attacks[0], 0);
            var round = Round(plan, loadout, 8); round.Press(2, 0, 0); round.Release(2.001, 0, 0); round.Advance(100);
            Check.True(round.Combat.Victory); Check.False(round.Combat.FinaleSuccess); Check.True(round.Finished);
            Check.Equal(1, round.MissCount); Check.Equal(0m, round.TotalDamageTaken);
        }

        [Test]
        public void EarlierMissDoesNotDisqualifyTheSuccessfulFinale()
        {
            var plan = Plan(new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 4));
            var loadout = Loadout(plan, "sword"); Place(loadout, 0, plan.Attacks[0], 0);
            var round = Round(plan, loadout, 8); round.Press(2.5, 0, 0); round.Release(2.501, 0, 0);
            Check.True(round.Combat.Victory); Check.True(round.Combat.FinaleSuccess);
            Check.Equal(1, round.MissCount); Check.Equal(4m, round.TotalDamageTaken);
        }

        [Test]
        public void OverlappingMonstersShareHealthAndTheLatestActiveInputOwnsTheFinale()
        {
            var stage = Stage();
            var plan = BattlePlanner.Resolve(stage, new[] {
                Proposal(stage, "short", new[] { new PatternStep(GestureKind.Tap, 0) }, new[] { 16 }),
                Proposal(stage, "long", new[] { new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 8) }, new[] { 16 })
            }, 1);
            var loadout = Loadout(plan, "spear"); Place(loadout, 0, plan.Attacks.First(x => x.MonsterId == "short"), 0);
            var round = Round(plan, loadout, 20); round.Press(2, 0, 0); round.Release(2.001, 0, 0);
            Check.True(round.Combat.Victory); Check.False(round.Combat.FinaleSuccess);
            round.Press(3, 0, 0); round.Release(3.001, 0, 0);
            Check.True(round.Combat.FinaleSuccess); Check.Equal(3, round.PerfectCount);
            Check.Equal(20m, round.Combat.TotalDamage); Check.Equal(0m, round.TotalDamageTaken);
        }

        [Test]
        public void PracticeCannotChangeRunHealthEnemyHealthRewardsOrArrangement()
        {
            var run = Session(); run.BattleLoadout.AutoArrange(); var plan = run.BattlePlan;
            string ticket = run.StageTicket; decimal health = run.Health, enemy = run.EnemyHealth.Current; int gold = run.Gold;
            var assignments = run.BattleLoadout.Placements.ToArray();
            var practice = WeaponPractice.Create(plan, run.BattleLoadout, plan.Attacks[0], run.EnemyHealth.Maximum, run.MaxHealth);
            Check.Equal(plan.Attacks[0].Placement.Pattern.Steps.Count, practice.Notes.Count);
            Check.True(practice.Combat.Bindings.All(x => practice.Notes.Contains(x.Note)));
            practice.Advance(100);
            Check.True(practice.Combat.IsPractice); Check.True(practice.TotalDamageTaken > 0);
            Check.Equal(health, run.Health); Check.Equal(enemy, run.EnemyHealth.Current); Check.Equal(gold, run.Gold);
            Check.Equal(RunPhase.Stage, run.Phase); Check.Equal(ticket, run.StageTicket); Check.Equal(0, run.Offers.Count);
            Check.True(run.ActiveRhythmRound == null); Check.True(assignments.SequenceEqual(run.BattleLoadout.Placements));
            Check.Equal(1, practice.Plan.Attacks.Count); Check.Equal(plan.Stage.Music.Bpm, practice.Plan.Stage.Music.Bpm);
        }

        [Test]
        public void RealEnemyHealthPersistsWhenReturningToPreparationAndResetsForNewStage()
        {
            var run = Session();
            var source = run.BattleLoadout.Patterns.First(p => run.BattleLoadout.CanPlace(2, p.MonsterId, p.Pattern.Id, 0, out _));
            Place(run.BattleLoadout, 2, source, 0);
            var round = run.StartRhythmRound();
            double time = round.Combat.Bindings.First(x => x.Slot == 2).Note.StartSeconds;
            round.Press(time, 0, 0); Check.True(round.Combat.TotalDamage > 0);
            decimal remaining = run.EnemyHealth.Current; var enemy = run.EnemyHealth;
            Check.True(run.CloseRhythmRound(round)); var replay = run.StartRhythmRound();
            Check.True(ReferenceEquals(enemy, replay.Combat.EnemyHealth)); Check.Equal(remaining, run.EnemyHealth.Current);
            run.Restart(42); Check.True(run.EnemyHealth == null); Check.True(run.BattleLoadout == null);
            Check.True(run.Enter(run.Map.Nodes.First(x => run.CanEnter(x.Id)).Id));
            Check.Equal(run.EnemyHealth.Maximum, run.EnemyHealth.Current);
        }

        [Test]
        public void WeaponPathsStayFiniteReturnToTheirOwnOrbitAndHaveDistinctAttackStyles()
        {
            var from = new BattlePathPoint(.18, .4); var to = new BattlePathPoint(.8, .32);
            var styles = new HashSet<string>();
            foreach (WeaponKind kind in Enum.GetValues(typeof(WeaponKind)))
            {
                var end = WeaponMotion.Sample(kind, from, to, 1);
                Check.True(Math.Abs(end.Position.X - from.X) < 1e-8 && Math.Abs(end.Position.Y - from.Y) < 1e-8);
                var frame = WeaponMotion.Sample(kind, from, to, .45);
                styles.Add(frame.Position.X.ToString("R") + "/" + frame.Position.Y.ToString("R") + "/" + frame.Rotation.ToString("R"));
                for (int i = 0; i <= 100; i++)
                {
                    frame = WeaponMotion.Sample(kind, from, to, i / 100.0);
                    Check.True(frame.Position.X >= 0 && frame.Position.X <= 1 && frame.Position.Y > 0 && frame.Position.Y < .95);
                }
            }
            Check.Equal(8, styles.Count);
        }

        private static RunSession Session()
        {
            var run = new RunSession(73, new RunRules(startingHealth: 10000));
            Check.True(run.Enter(run.Map.Nodes.First(x => run.CanEnter(x.Id)).Id)); return run;
        }
        private static RhythmRound Round(BattlePlan plan, WeaponArrangement loadout, decimal health = 1000) =>
            new RhythmRound(plan, combat: new WeaponBattle(loadout, new StageHealth(health), 100, 100));
        private static WeaponArrangement Loadout(BattlePlan plan, params string[] ids) =>
            new WeaponArrangement(plan, ids.Select(id => new WeaponState(id)).ToArray());
        private static void Place(WeaponArrangement loadout, int slot, PlannedAttack attack, int offset)
        { Check.True(loadout.TryPlace(slot, attack.MonsterId, attack.Pattern.Id, offset, out string reason), reason); }
        private static BattlePlan Plan(params PatternStep[] steps) => Plan(steps, new[] { 16 });
        private static BattlePlan Plan(PatternStep[] steps, int[] starts)
        {
            var stage = Stage(); return BattlePlanner.Resolve(stage, new[] { Proposal(stage, "target", steps, starts) }, 1);
        }
        private static MonsterProposal Proposal(MusicStage stage, string id, PatternStep[] steps, int[] starts)
        {
            var pattern = new RhythmPattern(id, 4, steps);
            var monster = new MonsterDefinition(id, id, "", pattern, new[] { new CallSignal(0, "CALL") }, 12, 4, 1);
            return new MonsterProposal(id, monster, stage.FindPlacements(pattern).Where(p => starts.Contains(p.StartTick)));
        }
        private static MusicStage Stage()
        {
            var slots = new List<SlotTemplate>();
            for (int tick = 0; tick < 16; tick++)
            {
                slots.Add(new SlotTemplate(GestureKind.Tap, tick)); slots.Add(new SlotTemplate(GestureKind.Flick, tick));
                foreach (var kind in new[] { GestureKind.Hold, GestureKind.Dive, GestureKind.Shake })
                    foreach (int duration in new[] { 4, 8 }) slots.Add(new SlotTemplate(kind, tick, duration));
            }
            return MusicStage.Generate(new MusicDefinition("weapons-test", "Weapons", 120, 4,
                new[] { new MusicSection("BODY", 0, 6, 1) }, new[] { slots }));
        }
        private static void Perform(RhythmRound round, IReadOnlyList<PatternStep> steps, double start, double error = 0)
        {
            foreach (var step in steps)
            {
                double at = start + RhythmTime.Seconds(step.OffsetTick, 120), end = at + RhythmTime.Seconds(step.DurationTicks, 120);
                switch (step.Kind)
                {
                    case GestureKind.Tap: round.Press(at + error, 0, 0); round.Release(at + error + .001, 0, 0); break;
                    case GestureKind.Hold: round.Press(at + error, 0, 0); round.Release(end, 0, 0); break;
                    case GestureKind.Dive: round.Press(at + error, 0, 0); round.Release(end + error, 0, 0); break;
                    case GestureKind.Flick:
                        round.Press(at - .1, 0, 0); round.Move(at - .04 + error, .08, 0); round.Release(at + error, .16, 0); break;
                    case GestureKind.Shake:
                        round.Press(at, 0, 0); round.Move(at + .08, .1, 0); round.Move(at + .16, 0, 0); round.Release(end, 0, 0); break;
                }
            }
        }
    }
}
