using System;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class MonsterHoldCombatTests
    {
        private static FiveLaneBattle Battle(string shield = "shield", double start = 2, double duration = 4,
            decimal damage = 16, double loop = 16) => new FiveLaneBattle(
                new[] { new WeaponState(shield, WeaponAttribute.Dual) }, 120, loop,
                new[] { new BeatAttack("enemy", start, damage, duration) }, new StageHealth(10000), 100, 100);

        [Test] public void HalfBeatTicksKeepTheOriginalTotalAndLongFramesResolveEveryTick()
        {
            var b = Battle(); b.Advance(2.24); Check.Equal(100m, b.PlayerHealth);
            b.Advance(2.74); Check.Equal(100m, b.PlayerHealth);
            b.Advance(2.75); Check.Equal(98m, b.PlayerHealth);
            b.Advance(3.25); Check.Equal(96m, b.PlayerHealth);
            b.Advance(6.25); Check.Equal(84m, b.PlayerHealth);
            var oneFrame = Battle(); oneFrame.Advance(6.25);
            Check.Equal(b.PlayerHealth, oneFrame.PlayerHealth);
            Check.Equal(IncomingAttackState.Hit, oneFrame.Incoming[0].State);
        }
        [Test] public void FractionalLastTickDistributesTheExactBudget()
        {
            var b = Battle(duration: 1.25, damage: 10);
            b.Advance(2.75); Check.Equal(96m, b.PlayerHealth);
            b.Advance(3.25); Check.Equal(92m, b.PlayerHealth);
            b.Advance(3.5); Check.Equal(90m, b.PlayerHealth);
        }
        [Test] public void PerfectEntrySustainsABucklerThroughTheWholeAttackAndDelaysCounters()
        {
            var b = Battle(); b.Press(0, 2); b.Advance(5.5);
            var lane = b.Lanes[0]; Check.True(lane.Holding); Check.True(lane.SustainingGuard);
            Check.Equal(6.0, lane.HoldEndBeat); Check.Equal(0, b.MissCount);
            var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
            var held = timeline.Notes.Single(n => n.Index == 0);
            Check.True(held.IsHold); Check.Equal(6.0, held.EndBeat);
            Check.True(timeline.Notes.Any(n => n.Index == 1 && n.Beat == 7));
            b.Release(0, 6); b.Advance(6.25);
            Check.Equal(100m, b.PlayerHealth); Check.Equal(16m, b.TotalBlocked);
            Check.Equal(7.0, lane.NextBeat); Check.Equal(1, b.PerfectCount);
            b.Press(0, 7); b.Release(0, 7);
            Check.Equal(5m, b.TotalDamage); Check.Equal(0, b.MissCount);
        }
        [Test] public void HalfEntryKeepsItsGradeForAllLaterTicks()
        {
            var b = Battle(); b.Press(0, 2.2); b.Release(0, 6); b.Advance(6.25);
            Check.Equal(92m, b.PlayerHealth); Check.Equal(8m, b.TotalReduced);
            Check.Equal(0m, b.TotalBlocked); Check.Equal(1, b.HalfMissCount);
        }
        [Test] public void ReleasingAContinuedGuardStopsOnlyFutureProtection()
        {
            var b = Battle(); b.Press(0, 2); b.Release(0, 3.1); b.Advance(6.25);
            Check.Equal(88m, b.PlayerHealth); Check.Equal(4m, b.TotalBlocked);
        }
        [Test] public void MidHoldEntryProtectsTheUnresolvedTickAndRemainingTailWithoutRefundingEarlierDamage()
        {
            var b = Battle(start: 1, duration: 4); b.Press(0, 3);
            Check.Equal(94m, b.PlayerHealth); Check.True(b.Lanes[0].SustainingGuard);
            b.Release(0, 5); b.Advance(5.25);
            Check.Equal(94m, b.PlayerHealth); Check.Equal(10m, b.TotalBlocked);
        }
        [Test] public void ShortHeaterGuardCanEnterLateWithoutAParryAndContinuePastItsOwnDuration()
        {
            var b = Battle("heater-shield", start: 1, duration: 5, damage: 20);
            b.Press(0, 1.25); // Between parry windows: ordinary guard, not a free Perfect parry.
            b.Advance(5); Check.True(b.Lanes[0].Holding); Check.Equal(6.0, b.Lanes[0].HoldEndBeat);
            b.Release(0, 6); b.Advance(6.25);
            Check.Equal(90m, b.PlayerHealth); Check.Equal(10m, b.TotalReduced); Check.Equal(0m, b.TotalBlocked);
            Check.Equal(8.0, b.Lanes[0].ReadyAtBeat);
        }
        [Test] public void GuardAlreadyHeldAtTheHeadExtendsAndCooldownInputsDoNotAutoCatchLater()
        {
            var b = Battle("heater-shield", start: 1, duration: 4);
            b.Press(0, 0); b.Release(0, 5); b.Advance(5.25);
            Check.Equal(92m, b.PlayerHealth); Check.Equal(0, b.MissCount);
            var unavailable = Battle("heater-shield", start: 1, duration: 4);
            unavailable.Press(0, 0); unavailable.Release(0, .1);
            unavailable.Press(0, 1); unavailable.Advance(3);
            Check.False(unavailable.Lanes[0].Holding);
            unavailable.Release(0, 3); unavailable.Press(0, 3);
            Check.True(unavailable.Lanes[0].SustainingGuard); Check.Equal(5.0, unavailable.Lanes[0].HoldEndBeat);
        }
        [Test] public void TowerContinuationDoesNotDemandAnEarlyReleaseOrMissItsDelayedCounter()
        {
            var b = Battle("tower-shield"); b.Press(0, 2); b.Advance(4);
            Check.True(b.Lanes[0].Holding); Check.False(b.Lanes[0].WaitingForParryRelease); Check.Equal(0, b.MissCount);
            b.Release(0, 6); b.Advance(6.25); Check.Equal(100m, b.PlayerHealth);
            Check.Equal(7.0, b.Lanes[0].NextBeat);
            b.Press(0, 7); b.Release(0, 7); Check.Equal(30m, b.TotalDamage);
        }
        [Test] public void PauseAndLoopingKeepHoldIdentityAndNeverReuseAnEarlierParry()
        {
            var b = Battle(loop: 8); b.Press(0, 2); b.Advance(3); b.Pause(); b.Advance(40); b.Release(0, 40);
            Check.Equal(3.0, b.Beat); Check.True(b.Lanes[0].Holding);
            b.Resume(); b.Release(0, 6); b.Advance(6.25); Check.Equal(100m, b.PlayerHealth);
            b.Advance(14.25); Check.Equal(84m, b.PlayerHealth); Check.Equal(16m, b.TotalBlocked);
        }
        [Test] public void LegacyPlanAdapterPreservesHoldHeadDurationAndTheFullDamageBudget()
        {
            var monster = MonsterCatalog.BuiltIn.Single(x => x.Id == "iron-turtle");
            var pattern = monster.Patterns.Single(x => x.Id == "turtle-long-hold");
            var stage = MusicStage.Generate(MusicCatalog.All.First());
            var placement = stage.FindPlacements(pattern.Pattern).First();
            var plan = BattlePlanner.Resolve(stage, new[] { new MonsterProposal(monster.Id, monster, new[] { placement }) }, 1);
            var b = FiveLaneBattle.FromPlan(plan, new[] { new WeaponState("shield") }, new StageHealth(1000), 100, 100, useFormation: false);
            double start = placement.StartTick / 4.0; b.Advance(start);
            var attack = b.Incoming.First(); Check.Equal(start, attack.Beat); Check.Equal(2.0, attack.Definition.HoldBeats);
            Check.Equal(monster.DamagePerNote * plan.Attacks[0].JudgmentWeight, attack.Definition.Damage);
        }
        [Test] public void AttackAnimationStaysActiveAndThePlayerKeepsGuardingThroughALongHold()
        {
            var b = Battle(); b.Press(0, 2); b.Advance(4);
            Check.Equal("Base Layer.Attack", FiveLaneArtTimeline.Monster(b, "enemy").State);
            Check.Equal("Base Layer.Guard", FiveLaneArtTimeline.Player(b).State);
        }
        [Test] public void SustainingOneAttackDoesNotAutomaticallyParryAnotherAttack()
        {
            var b = new FiveLaneBattle(new[] { new WeaponState("shield", WeaponAttribute.Dual) }, 120, 16,
                new[] { new BeatAttack("held", 2, 16, 4), new BeatAttack("tap", 3, 9), new BeatAttack("other", 4, 8, 1) },
                new StageHealth(10000), 100, 100);
            b.Press(0, 2); b.Release(0, 6); b.Advance(6.25);
            Check.Equal(83m, b.PlayerHealth); Check.Equal(16m, b.TotalBlocked);
        }
        [Test] public void SimultaneousHoldsKeepSeparateTickBudgetsAndExtendToTheLongestTail()
        {
            var b = new FiveLaneBattle(new[] { new WeaponState("shield", WeaponAttribute.Dual) }, 120, 16,
                new[] { new BeatAttack("short", 2, 8, 1), new BeatAttack("long", 2, 16, 4) },
                new StageHealth(10000), 100, 100);
            b.Press(0, 2); Check.Equal(6.0, b.Lanes[0].HoldEndBeat);
            b.Release(0, 6); b.Advance(6.25); Check.Equal(100m, b.PlayerHealth); Check.Equal(24m, b.TotalBlocked);
        }
        [Test] public void GroggyInterruptsTheRemainingTicksOfAnAlreadyActiveHold()
        {
            var b = new FiveLaneBattle(new[] { new WeaponState("hammer", WeaponAttribute.Dual) }, 120, 32,
                new[] { new BeatAttack("enemy", 8, 16, 8) }, new StageHealth(10000), 100, 100);
            foreach (double start in new[] { 0.0, 4, 8 }) foreach (double offset in new[] { 0.0, 1.5, 3 })
            { b.Press(0, start + offset); b.Release(0, start + offset); }
            decimal health = b.PlayerHealth;
            Check.Equal(IncomingAttackState.Interrupted, b.Incoming[0].State);
            b.Advance(17); Check.Equal(health, b.PlayerHealth);
        }
    }
}
