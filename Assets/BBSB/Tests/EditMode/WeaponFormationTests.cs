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
            double duration = WeaponFormation.OrbitPeriod * 2;
            Check.True(Math.Abs(WeaponFormation.OrbitOffset(activations, at + duration * .5) - Math.PI * 2) < 1e-9);
            Check.True(Math.Abs(WeaponFormation.OrbitOffset(activations, at + duration - 1e-6) - Math.PI * 4) < 1e-8);
            Check.Equal(0.0, WeaponFormation.OrbitOffset(activations, at + duration));
            foreach (double boundary in new[] { at, at + duration, at + WeaponFormation.SupportDuration })
            {
                var before = WeaponFormation.Sample(2, boundary - 1e-6, Hero, .32, 16.0 / 9,
                    WeaponFormation.SupportStrength(activations, boundary - 1e-6),
                    WeaponFormation.OrbitOffset(activations, boundary - 1e-6));
                var after = WeaponFormation.Sample(2, boundary + 1e-6, Hero, .32, 16.0 / 9,
                    WeaponFormation.SupportStrength(activations, boundary + 1e-6),
                    WeaponFormation.OrbitOffset(activations, boundary + 1e-6));
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

        [Test]
        public void OrbitTransitionsKeepSlotsAwayFromTheCenterAndEachOther()
        {
            for (int t = 0; t < 240; t++)
                for (int blend = 0; blend <= 10; blend++)
                {
                    var frames = new WeaponMotionFrame[5];
                    for (int slot = 0; slot < 5; slot++)
                    {
                        frames[slot] = WeaponFormation.Sample(slot, t * .05, Hero, .32, 1,
                            blend * .1, t * .13);
                        double dx = frames[slot].Position.X - Hero.X, dy = frames[slot].Position.Y - Hero.Y;
                        Check.True(Math.Sqrt(dx * dx + dy * dy) > .08);
                    }
                    for (int i = 0; i < 5; i++)
                        for (int j = i + 1; j < 5; j++)
                            Check.True(Distance(frames[i], frames[j]) > .08);
                }
        }

        [Test]
        public void VisibleBoundsIncludeSpriteAspectPivotOffsetAndRotation()
        {
            var bounds = new PreviewRect(.25, .1, .5, .8);
            var upright = WeaponFormation.HalfExtents(bounds, 1, 100, 0);
            var sideways = WeaponFormation.HalfExtents(bounds, 1, 100, 90);
            Check.True(Math.Abs(upright.X - 25) < 1e-9 && Math.Abs(upright.Y - 40) < 1e-9);
            Check.True(Math.Abs(sideways.X - 40) < 1e-9 && Math.Abs(sideways.Y - 25) < 1e-9);
            var offset = WeaponFormation.HalfExtents(new PreviewRect(.5, .1, .4, .8), .5, 100, 0);
            Check.True(Math.Abs(offset.X - 20) < 1e-9 && Math.Abs(offset.Y - 40) < 1e-9);
        }

        [Test]
        public void MixedAndWideWeaponFormationsRemainSeparatedAcrossOrbitsAndAspectRatios()
        {
            var groups = new[] {
                new[] { "greatsword", "bell", "spear", "blade", "dagger" },
                new[] { "hammer", "shield", "bow", "crossbow", "wand" },
                new[] { "hammer", "hammer", "hammer", "hammer", "hammer" }
            };
            foreach (double aspect in new[] { 16.0 / 9, 16.0 / 10 })
                foreach (WeaponRarity rarity in Enum.GetValues(typeof(WeaponRarity)))
                    foreach (var ids in groups)
                        for (int t = 0; t < 120; t++)
                            for (int blend = 0; blend <= 4; blend++)
                            {
                                var frames = new WeaponMotionFrame[5]; var extents = new BattlePathPoint[5];
                                var center = new BattlePathPoint(.14 * aspect, .32);
                                for (int i = 0; i < 5; i++)
                                {
                                    var weapon = WeaponCatalog.Find(ids[i]);
                                    frames[i] = WeaponFormation.Sample(i, t * .1, center, .32, 1, blend * .25, t * .17);
                                    double length = weapon.IsRanged ? 3.4 : weapon.Kind == WeaponKind.Spear ? 3.3
                                        : weapon.Kind == WeaponKind.Greatsword ? 2.95 : 2.6;
                                    double size = (weapon.Kind == WeaponKind.Dagger ? 17 : 22) / 720.0 * length * 1.8 * frames[i].Scale;
                                    // Full square is conservative for aspect-fitted ranged source frames.
                                    extents[i] = WeaponFormation.HalfExtents(WeaponPreviewBounds.Get(ids[i], rarity),
                                        1, size, frames[i].Rotation);
                                }
                                var original = (WeaponMotionFrame[])frames.Clone();
                                var viewport = new PreviewRect(.008, .008, aspect - .016, .984);
                                WeaponFormation.Separate(frames, extents, 5, center, viewport, .008);
                                var repeated = (WeaponMotionFrame[])original.Clone();
                                WeaponFormation.Separate(repeated, extents, 5, center, viewport, .008);
                                for (int i = 0; i < 5; i++)
                                {
                                    Check.Equal(frames[i].Position.X, repeated[i].Position.X);
                                    Check.Equal(original[i].Rotation, frames[i].Rotation);
                                    Check.Equal(original[i].Scale, frames[i].Scale);
                                    Check.True(frames[i].Position.X - extents[i].X >= viewport.X - 1e-9);
                                    Check.True(frames[i].Position.X + extents[i].X <= viewport.X + viewport.Width + 1e-9);
                                    Check.True(frames[i].Position.Y - extents[i].Y >= viewport.Y - 1e-9);
                                    Check.True(frames[i].Position.Y + extents[i].Y <= viewport.Y + viewport.Height + 1e-9);
                                    for (int j = i + 1; j < 5; j++)
                                        Check.True(Math.Abs(frames[i].Position.X - frames[j].Position.X) >= extents[i].X + extents[j].X + .008 - 1e-9
                                            || Math.Abs(frames[i].Position.Y - frames[j].Position.Y) >= extents[i].Y + extents[j].Y + .008 - 1e-9);
                                }
                            }
        }

        private static WeaponMotionFrame Frame(int slot, double time, double support) =>
            WeaponFormation.Sample(slot, time, Hero, .32, 16.0 / 9, support);
        private static double Distance(WeaponMotionFrame a, WeaponMotionFrame b) =>
            Math.Abs(a.Position.X - b.Position.X) + Math.Abs(a.Position.Y - b.Position.Y);

        [Test]
        public void PinkTrailHistoryFreezesOnPauseAndClearsOnRewindOrLongGaps()
        {
            var trail = new WeaponTrailHistory(); var position = new BattlePathPoint(.2, .3);
            trail.Add(1, position, false); trail.Add(1.1, position, false);
            for (int i = 0; i < 100; i++) trail.Add(1.1, position, false);
            Check.Equal(2, trail.Count);
            trail.Add(1.2, position, true);
            Check.False(trail[1].InFront); Check.True(trail[2].InFront);
            trail.Add(.1, position, false); Check.Equal(1, trail.Count);
            trail.Add(2, position, false); Check.Equal(1, trail.Count);
            trail.Clear(); Check.Equal(0, trail.Count);
        }

        [Test]
        public void PinkTrailSamplesExpireAndMemoryStaysBoundedAtHighFrameRates()
        {
            var trail = new WeaponTrailHistory();
            for (int i = 0; i <= 1000; i++) trail.Add(i * .001, new BattlePathPoint(.2 + i * .0001, .3), false);
            Check.Equal(WeaponTrailHistory.Capacity, trail.Count);
            Check.True(trail[0].Time > 1 - WeaponTrailHistory.Lifetime);
            Check.Equal(1.0, trail[trail.Count - 1].Time);
            Check.Equal(1.0, WeaponTrailHistory.Opacity(0));
            Check.Equal(0.0, WeaponTrailHistory.Opacity(WeaponTrailHistory.Lifetime));
            Check.Equal(0.0, WeaponTrailHistory.Opacity(10));
            trail.Add(1 + WeaponTrailHistory.Lifetime + .01, new BattlePathPoint(.5, .3), false);
            Check.Equal(1, trail.Count);
        }

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
