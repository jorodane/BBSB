using System;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class WeaponFormationTests
    {
        private static readonly BattlePathPoint Hero = new BattlePathPoint(.14, .32);

        [Test]
        public void IdleWeaponsFloatIndependentlyWithinThePlayerSideOfTheArena()
        {
            for (int slot = 0; slot < 5; slot++)
            {
                var first = Frame(slot, 0, 0);
                Check.True(Distance(first, Frame(slot, 1, 0)) > .001);
                for (int i = 0; i < 400; i++)
                {
                    var pose = Frame(slot, i * .05, 0);
                    Check.True(pose.Position.X > 0 && pose.Position.X < .3);
                    Check.True(pose.Position.Y > .14 && pose.Position.Y < .52);
                    Check.True(pose.Scale >= .975 && pose.Scale <= 1.025);
                }
            }
        }

        [Test]
        public void SupportCompletesMultipleOrbitsAndReturnsContinuouslyToTheMovingIdlePose()
        {
            var round = Round(); double at = round.Notes[0].StartSeconds; round.Press(at, 0, 0);
            var activations = round.Combat.Activations;
            Check.Equal(0.0, WeaponFormation.SupportStrength(activations, at - .01));
            Check.Equal(0.0, WeaponFormation.SupportStrength(activations, at));
            Check.Equal(1.0, WeaponFormation.SupportStrength(activations, at + .3));
            Check.True(WeaponFormation.SupportDuration / WeaponFormation.OrbitPeriod > 2);
            Check.True(Distance(Frame(2, .3, 1), Frame(2, .3 + WeaponFormation.OrbitPeriod, 1)) < 1e-8);
            Check.True(Distance(Frame(2, .3, 1), Frame(2, .3 + WeaponFormation.OrbitPeriod * .5, 1)) > .05);
            foreach (double boundary in new[] { at, at + WeaponFormation.SupportDuration })
            {
                var before = Frame(2, boundary - 1e-6, WeaponFormation.SupportStrength(activations, boundary - 1e-6));
                var after = Frame(2, boundary + 1e-6, WeaponFormation.SupportStrength(activations, boundary + 1e-6));
                Check.True(Distance(before, after) < 1e-5);
            }
            double end = at + WeaponFormation.SupportDuration + .001;
            Check.Equal(0.0, WeaponFormation.SupportStrength(activations, end));
            Check.True(Distance(Frame(2, end, 0), Frame(2, end, WeaponFormation.SupportStrength(activations, end))) < 1e-9);
        }

        [Test]
        public void DenseAttacksSustainRatherThanRestartTheOrbitWithoutMutatingCombat()
        {
            var round = Round(); double at = round.Notes[0].StartSeconds;
            round.Press(at, 0, 0); round.Release(at + .01, 0, 0);
            double next = round.Notes[1].StartSeconds;
            double before = WeaponFormation.SupportStrength(round.Combat.Activations, next);
            round.Press(next, 0, 0);
            Check.Equal(before, WeaponFormation.SupportStrength(round.Combat.Activations, next));
            decimal health = round.Combat.EnemyHealth.Current, damage = round.Combat.TotalDamage;
            int count = round.Combat.Activations.Count;
            var frozen = Frame(3, next + .1, WeaponFormation.SupportStrength(round.Combat.Activations, next + .1));
            for (int i = 0; i < 120; i++)
            {
                var again = Frame(3, next + .1, WeaponFormation.SupportStrength(round.Combat.Activations, next + .1));
                Check.Equal(frozen.Position.X, again.Position.X); Check.Equal(frozen.Rotation, again.Rotation);
            }
            Check.Equal(count, round.Combat.Activations.Count); Check.Equal(health, round.Combat.EnemyHealth.Current);
            Check.Equal(damage, round.Combat.TotalDamage); Check.Equal(next, round.ElapsedSeconds);
            var replay = round.RepeatPractice();
            Check.Equal(0.0, WeaponFormation.SupportStrength(replay.Combat.Activations, 0));
        }

        [Test]
        public void AttacksKeepTheirImpactMotionAndJoinBothFloatingEndpointsWithoutSnapping()
        {
            var launch = Frame(2, .3, 1); var home = Frame(2, 1.5, .2);
            var target = new BattlePathPoint(.75, .45);
            foreach (WeaponAttackStyle style in Enum.GetValues(typeof(WeaponAttackStyle)))
            {
                var start = WeaponFormation.Attack(style, launch, target, home, 0);
                var end = WeaponFormation.Attack(style, launch, target, home, 1);
                Check.Equal(launch.Position.X, start.Position.X); Check.Equal(launch.Rotation, start.Rotation);
                Check.Equal(home.Position.X, end.Position.X); Check.Equal(home.Rotation, end.Rotation);
                Check.True(Distance(start, WeaponFormation.Attack(style, launch, target, home, 1e-6)) < 1e-5);
                Check.True(Distance(end, WeaponFormation.Attack(style, launch, target, home, 1 - 1e-6)) < 1e-5);
                double impact = WeaponMotion.ImpactSeconds(style) / WeaponMotion.Duration(style);
                var original = WeaponMotion.Sample(style, launch.Position, target, impact);
                var actual = WeaponFormation.Attack(style, launch, target, home, impact);
                Check.True(Distance(original, actual) < 1e-9); Check.True(Math.Abs(original.Scale - actual.Scale) < 1e-9);
            }
        }

        private static WeaponMotionFrame Frame(int slot, double time, double support) =>
            WeaponFormation.Sample(slot, time, Hero, .32, 16.0 / 9, support);
        private static double Distance(WeaponMotionFrame a, WeaponMotionFrame b) =>
            Math.Abs(a.Position.X - b.Position.X) + Math.Abs(a.Position.Y - b.Position.Y);

        private static RhythmRound Round()
        {
            var pattern = new RhythmPattern("formation", 4, new[] { new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 4) });
            var monster = new MonsterDefinition("formation-target", "Target", "", pattern, new[] { new CallSignal(0, "CALL") }, 12, 4, 1);
            var preview = new MonsterPreview(monster, monster.Patterns[0]);
            var plan = preview.Round.Plan;
            var weapons = new[] { "sword", "shield", "greatsword", "bell", "blade" }.Select(id => new WeaponState(id)).ToArray();
            return new RhythmRound(plan, combat: new WeaponBattle(new WeaponLoadout(plan, weapons), new StageHealth(10000), 100, 100, true));
        }
    }
}
