using System;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class BattleEffectsTests
    {
        [Test]
        public void RangedPreparationObservesNotesButOnlySuccessfulActivationReleases()
        {
            var round = Round(GestureKind.Tap, "bow");
            double at = round.Notes.Single().StartSeconds;
            Check.Equal(RangedWeaponPose.Idle, RangedWeaponTimeline.Evaluate(round.Combat, 0, at - .3, round.BeatSeconds).Pose);
            Check.Equal(RangedWeaponPose.Prepare, RangedWeaponTimeline.Evaluate(round.Combat, 0, at - .1, round.BeatSeconds).Pose);
            Check.Equal(0, round.Combat.Activations.Count);
            round.Press(at, 0, 0);
            Check.Equal(RangedWeaponPose.Release, RangedWeaponTimeline.Evaluate(round.Combat, 0, at, round.BeatSeconds).Pose);
            Check.Equal(RangedWeaponPose.Idle, RangedWeaponTimeline.Evaluate(round.Combat, 0, at + .1, round.BeatSeconds).Pose);
            var miss = Round(GestureKind.Tap, "bow"); miss.Advance(at + .3);
            Check.Equal(0, miss.Combat.Activations.Count);
            Check.Equal(RangedWeaponPose.Idle, RangedWeaponTimeline.Evaluate(miss.Combat, 0, miss.ElapsedSeconds, miss.BeatSeconds).Pose);
        }

        [Test]
        public void BowChargeStaysPreparedThroughoutContactAndReleasesAtTheHoldEnd()
        {
            var round = Round(GestureKind.Hold, "bow"); var note = round.Notes.Single();
            round.Press(note.StartSeconds, 0, 0);
            double middle = (note.StartSeconds + note.EndSeconds) / 2;
            round.Advance(middle);
            var frame = RangedWeaponTimeline.Evaluate(round.Combat, 0, middle, round.BeatSeconds);
            Check.Equal(RangedWeaponPose.Prepare, frame.Pose); Check.True(frame.Tension > .2 && frame.Tension < 1);
            Check.Equal(0, round.Combat.Activations.Count);
            round.Release(note.EndSeconds, 0, 0);
            Check.Equal(1, round.Combat.Activations.Count);
            Check.Equal(RangedWeaponPose.Release, RangedWeaponTimeline.Evaluate(round.Combat, 0, round.ElapsedSeconds, round.BeatSeconds).Pose);
        }

        [Test]
        public void VolleyUsesThreeConvergingVisualsWithOnlyOneDamageActivation()
        {
            var round = Round(GestureKind.Flick, "bow"); double at = round.Notes.Single().StartSeconds;
            round.Press(at - .08, 0, 0); round.Move(at - .02, .1, 0); round.Release(at, .2, 0);
            var activation = round.Combat.Activations.Single();
            Check.Equal(WeaponAttackStyle.ArrowVolley, activation.Action.Motion);
            Check.Equal(3, BattleVfxCatalog.ProjectileCount(activation.Action.Motion));
            decimal damage = activation.Damage;
            var from = new BattlePathPoint(.2, .3); var to = new BattlePathPoint(.8, .6);
            for (int lane = -1; lane <= 1; lane++)
            {
                var start = RangedWeaponTimeline.Projectile(from, to, 0, lane);
                var end = RangedWeaponTimeline.Projectile(from, to, 1, lane);
                Check.True(Math.Abs(start.X - from.X) < 1e-9 && Math.Abs(start.Y - from.Y) < 1e-9);
                Check.True(Math.Abs(end.X - to.X) < 1e-9 && Math.Abs(end.Y - to.Y) < 1e-9);
                var mid = RangedWeaponTimeline.Projectile(from, to, .5, lane);
                Check.True(Math.Abs(mid.Y - (.45 + lane * .055)) < 1e-9);
            }
            for (int i = 0; i < 200; i++)
            {
                RangedWeaponTimeline.Evaluate(round.Combat, 0, at + i * .001, round.BeatSeconds);
                BattleHitFeedback.Monster(round.Combat, activation.Target.MonsterId, at + i * .003, round.BeatSeconds);
            }
            Check.Equal(1, round.Combat.Activations.Count); Check.Equal(damage, round.Combat.Activations[0].Damage);
            Check.Equal(at, round.ElapsedSeconds); Check.Equal(1, round.Results.Count);
            Check.Equal(damage, round.Combat.TotalDamage); Check.Equal(10000m - damage, round.Combat.EnemyHealth.Current);
        }

        [Test]
        public void MonsterHitStopStartsAtProjectileArrivalAndDenseWeaponsDoNotAddStops()
        {
            var round = Round(GestureKind.Tap, "bow", "bow", "bow", "bow", "bow");
            double at = round.Notes.Single().StartSeconds; round.Press(at, 0, 0);
            Check.Equal(5, round.Combat.Activations.Count);
            string monster = round.Combat.Activations[0].Target.MonsterId;
            double impact = at + WeaponMotion.ImpactSeconds(WeaponAttackStyle.ArrowShot);
            Check.True(!BattleHitFeedback.Monster(round.Combat, monster, impact - .001, round.BeatSeconds).Active);
            var hit = BattleHitFeedback.Monster(round.Combat, monster, impact, round.BeatSeconds);
            var single = BattleHitFeedback.Sample(impact, impact, .3 + (double)round.Combat.Activations[0].Damage / 55, round.BeatSeconds);
            Check.True(hit.Stopped); Check.Equal(single.Strength, hit.Strength); Check.Equal(single.StopSeconds, hit.StopSeconds);
            Check.Equal(impact, hit.PoseSeconds(impact + .01));
            Check.True(!BattleHitFeedback.Monster(round.Combat, "another-monster", impact, round.BeatSeconds).Active);
            Check.True(!BattleHitFeedback.Monster(round.Combat, monster, impact + .29, round.BeatSeconds).Active);
            Check.True(WeaponMotion.ImpactSeconds(WeaponAttackStyle.BoltShot) < WeaponMotion.ImpactSeconds(WeaponAttackStyle.ArrowShot));
            Check.True(WeaponMotion.ImpactSeconds(WeaponAttackStyle.ArrowShot) < WeaponMotion.ImpactSeconds(WeaponAttackStyle.OrbShot));
        }

        [Test]
        public void HitFeedbackIsBoundedAtFastTemposAndInvalidOrExpiredSamplesStaySilent()
        {
            foreach (double beat in new[] { .08, .2, .5, 1.0 })
            {
                var hit = BattleHitFeedback.Sample(1, 1, 50, beat);
                Check.True(hit.Stopped); Check.Equal(1.0, hit.Strength);
                Check.True(hit.StopSeconds <= .065 && hit.StopSeconds <= beat * .18);
                var released = BattleHitFeedback.Sample(1, 1 + hit.StopSeconds + .001, 50, beat);
                Check.True(!released.Stopped); Check.True(released.Active);
                Check.Equal(1.1, released.PoseSeconds(1.1));
            }
            foreach (double seconds in new[] { .99, 1.29, double.NaN, double.PositiveInfinity })
                Check.True(!BattleHitFeedback.Sample(1, seconds, 1, .5).Active);
            Check.True(!BattleHitFeedback.Sample(1, 1, 0, .5).Active);
            Check.True(!BattleHitFeedback.Sample(1, 1, 1, 0).Active);
        }

        [Test]
        public void NewMonsterCallInterruptsBodyFreezeAndFastReleaseLeavesRoomForTheNextCue()
        {
            var round = Round(GestureKind.Tap, "bow");
            var plan = round.Plan.Monsters.Single(); double beat = round.BeatSeconds;
            double callAt = plan.Attacks[0].Call[0].Tick * beat / RhythmTime.TicksPerBeat;
            var hit = BattleHitFeedback.Sample(callAt - .02, callAt + .01, 1, beat);
            Check.True(hit.Stopped); Check.Equal(callAt + .01, hit.PoseSeconds(callAt + .01, plan, beat));
            var frozen = BattleHitFeedback.Sample(callAt + .01, callAt + .02, 1, beat);
            Check.Equal(callAt + .01, frozen.PoseSeconds(callAt + .02, plan, beat));
            Check.Equal(frozen.Strength, frozen.Envelope);
            double at = round.Notes.Single().StartSeconds; round.Press(at, 0, 0);
            Check.Equal(RangedWeaponPose.Release, RangedWeaponTimeline.Evaluate(round.Combat, 0, at + .01, .08).Pose);
            Check.Equal(RangedWeaponPose.Idle, RangedWeaponTimeline.Evaluate(round.Combat, 0, at + .03, .08).Pose);
        }

        [Test]
        public void ActualPlayerDamageTriggersFeedbackAndPracticeRepeatClearsIt()
        {
            var round = Round(GestureKind.Tap, "bow"); double at = round.Notes.Single().StartSeconds;
            round.Advance(at + .3); var result = round.Results.Single();
            Check.True(result.DamageTaken > 0);
            Check.True(BattleHitFeedback.Player(round, result.JudgedAtSeconds).Stopped);
            var replay = round.RepeatPractice();
            Check.True(!BattleHitFeedback.Player(replay, result.JudgedAtSeconds).Active);
            Check.Equal(RangedWeaponPose.Idle, RangedWeaponTimeline.Evaluate(replay.Combat, 0, 0, replay.BeatSeconds).Pose);
            var success = Round(GestureKind.Tap, "bow"); success.Press(at, 0, 0);
            Check.True(!BattleHitFeedback.Player(success, at).Active);
        }

        [Test]
        public void EveryRangedPoseKeepsItsActionSocketsInsideItsOwnFrame()
        {
            int frames = 0, sockets = 0;
            foreach (var weapon in WeaponCatalog.All.Where(w => w.IsRanged))
            foreach (var rarity in WeaponRarities.All)
            foreach (RangedWeaponPose pose in Enum.GetValues(typeof(RangedWeaponPose)))
            {
                var layout = WeaponArtLayout.Sockets(weapon.Id, rarity, pose); frames++; sockets += layout.Count;
                var muzzle = RangedWeaponArtLayout.Muzzle(weapon.Id, rarity, pose);
                Check.True(muzzle.X > 0 && muzzle.X < 1 && muzzle.Y > 0 && muzzle.Y < 1);
                var slice = RangedWeaponArtLayout.Slice(weapon.Id, rarity, pose);
                Check.True(slice.Left >= 0 && slice.Right <= 1 && slice.Right - slice.Left > .28);
                if (pose == RangedWeaponPose.Idle) Check.Equal(0.0, slice.Left);
                else Check.Equal(slice.Left, RangedWeaponArtLayout.Slice(weapon.Id, rarity, (RangedWeaponPose)((int)pose - 1)).Right);
                if (pose == RangedWeaponPose.Release) Check.Equal(1.0, slice.Right);
                Check.Equal(weapon.ActionCountAt(rarity), layout.Count);
                for (int i = 0; i < layout.Count; i++)
                {
                    var a = layout[i];
                    Check.True(a.RadiusX > .015 && a.RadiusY > .015);
                    Check.True(a.X - a.RadiusX > 0 && a.X + a.RadiusX < 1 && a.Y - a.RadiusY > 0 && a.Y + a.RadiusY < 1);
                    for (int j = 0; j < i; j++)
                        Check.True(Math.Abs(a.X - layout[j].X) > a.RadiusX + layout[j].RadiusX || Math.Abs(a.Y - layout[j].Y) > a.RadiusY + layout[j].RadiusY);
                }
            }
            Check.Equal(36, frames); Check.Equal(72, sockets);
        }

        private static RhythmRound Round(GestureKind gesture, params string[] weapons)
        {
            int duration = gesture == GestureKind.Hold || gesture == GestureKind.Dive ? 8 : 0;
            var step = new PatternStep(gesture, 0, duration);
            var pattern = new RhythmPattern("effects", 4, new[] { step });
            var stage = MusicStage.Generate(new MusicDefinition("effects-test", "Effects", 120, 4,
                new[] { new MusicSection("BODY", 0, 4, 1) }, new[] { new[] { new SlotTemplate(gesture, 0, duration) } }));
            var monster = new MonsterDefinition("effects-target", "Target", "", pattern,
                new[] { new CallSignal(0, "CALL") }, 12, 4, 1);
            var plan = BattlePlanner.Resolve(stage, new[] { new MonsterProposal("target", monster,
                stage.FindPlacements(pattern).Where(p => p.StartTick == 16)) }, 1);
            var loadout = new WeaponLoadout(plan, weapons.Select(id => new WeaponState(id, WeaponRarity.Legendary)).ToArray());
            return new RhythmRound(plan, combat: new WeaponBattle(loadout, new StageHealth(10000), 100, 100, true));
        }
    }
}
