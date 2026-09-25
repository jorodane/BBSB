using System;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class AttackMotionTests
    {
        private static readonly BattlePathPoint Body = new BattlePathPoint(.15, .7), Target = new BattlePathPoint(.55, .7);
        private static FiveLaneBattle Battle(WeaponPhrase phrase, int bpm = 120, params BeatAttack[] attacks) =>
            new FiveLaneBattle(new[] { new WeaponState(phrase.WeaponId) }, bpm, 32, attacks,
                new StageHealth(10000), 1000, 1000, phrases: new[] { phrase });
        private static WeaponPhrase Phrase(params WeaponPhraseNote[] notes) =>
            new WeaponPhrase("dagger", "Motion", "", notes.Last().Beat + notes.Last().HoldBeats + 1, notes);
        private static void Tap(FiveLaneBattle b, double at, int slot = 0) { b.Press(slot, at); b.Release(slot, at); }
        private static AttackMotionState Motion(FiveLaneBattle b) => FiveLaneAttackMotion.Player(b, b.Lanes[0]);
        private static WeaponMotionFrame Frame(FiveLaneBattle b) => FiveLaneWeaponMotion.Sample(b, b.Lanes[0], 0, Body, .3, 16d / 9, Target);
        private static void Near(double expected, double actual) => Check.True(Math.Abs(expected - actual) < .00001);

        [Test] public void HalfBeatFollowupWaitsAtTheTargetAndOnlyActualInputChangesStroke()
        {
            var b = Battle(Phrase(new WeaponPhraseNote(0, 6), new WeaponPhraseNote(.5, 6)));
            Tap(b, 0); Check.Equal(AttackMotionMode.Combo, Motion(b).Mode);
            b.Advance(.35); Near(Target.X, Frame(b).Position.X); Near(Target.Y, Frame(b).Position.Y);
            Check.Equal(0, Motion(b).Stroke); b.Advance(.49); Check.Equal(0, Motion(b).Stroke);
            Tap(b, .5); Check.True(Motion(b).Continues); Check.Equal(-1, Motion(b).Direction);
            Near(Target.X, Frame(b).Position.X); Check.Equal(12m, b.TotalDamage);
            double contact = FiveLaneAttackMotion.PlayerContactBeats(b, b.Lanes[0]);
            b.Advance(1.15); Check.False(Motion(b).Active); Check.True(Frame(b).Position.X < .3);
            Near(contact, FiveLaneAttackMotion.PlayerContactBeats(b, b.Lanes[0]));
        }

        [Test] public void MissingTheFollowupCancelsThePoseAndNeverInventsAnAttack()
        {
            var b = Battle(Phrase(new WeaponPhraseNote(0, 6), new WeaponPhraseNote(.5, 6)));
            Tap(b, 0); b.Advance(.8);
            Check.True(b.Lanes[0].LastPerformedNote.Canceled); Check.Equal(0, Motion(b).Stroke);
            Check.Equal(6m, b.TotalDamage); Check.Equal(1, b.MissCount);
            b.Advance(1.1); Check.False(Motion(b).Active); Check.True(Frame(b).Position.X < .3);
        }

        [Test] public void IsolatedRepeatedTapsAlternateAndLongerGapsDoNotBecomeCombos()
        {
            var b = Battle(Phrase(new WeaponPhraseNote(0, 6), new WeaponPhraseNote(.501, 6)));
            Tap(b, 0); Check.Equal(AttackMotionMode.Single, Motion(b).Mode); Check.Equal(1, Motion(b).Direction);
            Tap(b, .501); Check.Equal(AttackMotionMode.Single, Motion(b).Mode);
            Check.False(Motion(b).Continues); Check.Equal(-1, Motion(b).Direction);
            foreach (int bpm in new[] { 60, 120, 240 })
            {
                var slow = Battle(Phrase(new WeaponPhraseNote(0, 10)), bpm);
                Tap(slow, 0); double duration = WeaponMotion.Duration(FiveLaneWeaponMotion.Style(slow.Lanes[0])) * bpm / 60;
                slow.Advance(duration * .85); Check.True(Motion(slow).Active);
                slow.Advance(duration + .01); Check.False(Motion(slow).Active);
            }
        }

        [Test] public void ComboFollowsTheWeaponAcrossItsLanesAndRepeatBoundary()
        {
            var p = new WeaponPhrase("staff", "Alternating", "", 1,
                new[] { new WeaponPhraseNote(0, 2), new WeaponPhraseNote(.5, 3, laneOffset: 1) }, repeat: true);
            var b = Battle(p); Tap(b, 0); Tap(b, .5, 1); Check.True(Motion(b).Continues);
            b.Advance(.9); Near(Target.X, Frame(b).Position.X);
            Tap(b, 1); Check.True(Motion(b).Continues); Check.Equal(2, Motion(b).Stroke); Check.Equal(7m, b.TotalDamage);
            var separate = new FiveLaneBattle(new[] { new WeaponState("dagger"), new WeaponState("dual-swords") },
                120, 32, Array.Empty<BeatAttack>(), new StageHealth(10000), 100, 100,
                phrases: new[] { Phrase(new WeaponPhraseNote(0, 1)),
                    new WeaponPhrase("dual-swords", "Single", "", 2, new[] { new WeaponPhraseNote(0, 1) }) });
            Tap(separate, 0); Tap(separate, .5, 1);
            Check.False(FiveLaneAttackMotion.Player(separate, separate.Lanes[1]).Continues);
            Check.Equal(AttackMotionMode.Single, FiveLaneAttackMotion.Player(separate, separate.Lanes[0]).Mode);
        }

        [Test] public void ConnectedHoldsKeepSpinPhaseAndDoNotAddDamageTicks()
        {
            var b = Battle(Phrase(new WeaponPhraseNote(0, 8, 1.25), new WeaponPhraseNote(1.25, 9, 1)));
            b.Press(0, 0); b.Advance(.1); Check.False(FiveLaneAttackMotion.SustainingPlayer(b, b.Lanes[0]));
            b.Advance(.3); var first = Frame(b); Check.True(FiveLaneAttackMotion.SustainingPlayer(b, b.Lanes[0]));
            Check.Equal(AttackMotionMode.Sustain, Motion(b).Mode); Check.Equal(0m, b.TotalDamage);
            b.Advance(.4); Check.True(Math.Abs(Frame(b).Rotation - first.Rotation) > 30);
            b.Advance(1.249999); var end = Frame(b); b.Advance(1.25); var joined = Frame(b);
            Near(end.Position.X, joined.Position.X); Near(end.Position.Y, joined.Position.Y);
            Check.True(Math.Abs(end.Rotation - joined.Rotation) < .01);
            Check.Equal(0d, b.Lanes[0].LastPerformedNote.SustainStartedAtBeat);
            Check.True(FiveLaneAttackMotion.SustainingPlayer(b, b.Lanes[0]));
            Check.True(b.Lanes[0].Holding); Check.Equal(8m, b.TotalDamage);
            b.Release(0, 2.25); Check.Equal(17m, b.TotalDamage); Check.Equal(2, b.PerfectCount);
            b.Advance(2.55); Check.False(Motion(b).Active);
        }

        [Test] public void HoldReleaseAndPauseStopTheMotionOnTheBattleClock()
        {
            var b = Battle(Phrase(new WeaponPhraseNote(0, 20, 2)));
            b.Press(0, 0); b.Advance(.8); var pose = Frame(b); b.Pause(); b.Advance(10);
            Near(pose.Rotation, Frame(b).Rotation); Near(pose.Position.X, Frame(b).Position.X);
            b.Resume(); b.Release(0, 1); Check.True(Motion(b).Canceled); Check.Equal(0m, b.TotalDamage);
            Check.False(FiveLaneAttackMotion.SustainingPlayer(b, b.Lanes[0]));
            b.Advance(1.3); Check.False(Motion(b).Active); Check.Equal(1, b.MissCount);
            var jumped = Battle(Phrase(new WeaponPhraseNote(0, 20, 2)));
            var stepped = Battle(Phrase(new WeaponPhraseNote(0, 20, 2)));
            jumped.Press(0, 0); stepped.Press(0, 0); jumped.Advance(1.7);
            for (int i = 1; i <= 17; i++) stepped.Advance(i / 10d);
            Near(Frame(jumped).Rotation, Frame(stepped).Rotation); Near(Frame(jumped).Position.Y, Frame(stepped).Position.Y);
        }

        [Test] public void CallsAndGuardHoldsNeverBecomeOffensiveSpins()
        {
            var call = Battle(Phrase(new WeaponPhraseNote(0, 50, 2, role: WeaponNoteRole.Call)));
            call.Press(0, 0); call.Advance(1); Check.False(Motion(call).Active); Check.Equal(0m, call.TotalDamage);
            foreach (string id in new[] { "heater-shield", "resonance-shield" })
            {
                var guard = Battle(WeaponPhraseCatalog.Find(id)); guard.Press(0, 0); guard.Advance(1);
                Check.False(Motion(guard).Active); Check.Equal("Base Layer.Guard", FiveLaneArtTimeline.Player(guard).State);
                Check.True(Frame(guard).Position.X > Body.X && Frame(guard).Position.X < Target.X);
            }
            var sequence = Battle(Phrase(new WeaponPhraseNote(0, 1), new WeaponPhraseNote(.5, 0, role: WeaponNoteRole.Call),
                new WeaponPhraseNote(1, 10, condition: PhraseNoteCondition.AllCalls)));
            Tap(sequence, 0); Tap(sequence, .5); Check.True(sequence.Lanes[0].LastPerformedNote.Canceled);
            Tap(sequence, 1); Check.False(Motion(sequence).Continues); Check.Equal(11m, sequence.TotalDamage);
        }

        [Test] public void BowPreparationAndSupportResponsesAppearInFrontOfThePlayer()
        {
            var bow = Battle(WeaponPhraseCatalog.Find("bow")); Tap(bow, 0); bow.Press(0, 1); bow.Advance(1.4);
            Near(FiveLaneWeaponMotion.ShieldPoint(Body, .3, 16d / 9).X, Frame(bow).Position.X);
            Check.False(Motion(bow).Active); Check.Equal(0m, bow.TotalDamage);
            bow.Release(0, 2); bow.Advance(2.2);
            Check.True(Frame(bow).Position.X > Body.X && Frame(bow).Position.X < .3); Check.True(Frame(bow).Scale > 1.1);
            var bell = Battle(WeaponPhraseCatalog.Find("spirit-bell")); Tap(bell, 0); Tap(bell, 1); bell.Advance(1.2);
            Check.True(Motion(bell).Active); Check.Equal(0m, bell.TotalDamage);
            Near(FiveLaneWeaponMotion.ShieldPoint(Body, .3, 16d / 9).X, Frame(bell).Position.X);
            Check.True(Frame(bell).Scale > 1.1); bell.Advance(2); Check.False(Motion(bell).Active);
        }

        [Test] public void EnemyCombosChangeStrokeAtTheNextBeatWithoutRestartingThePreviousOne()
        {
            var b = Battle(Phrase(new WeaponPhraseNote(0, 1)), 120,
                new BeatAttack("a", 1, 1), new BeatAttack("a", 1.5, 1), new BeatAttack("a", 2, 1), new BeatAttack("b", 1.25, 1));
            b.Advance(1.4); var first = FiveLaneAttackMotion.Enemy(b, "a");
            Check.Equal(AttackMotionMode.Combo, first.Mode); Check.Equal(0, first.Stroke);
            Check.Equal(AttackMotionMode.Single, FiveLaneAttackMotion.Enemy(b, "b").Mode);
            b.Advance(1.5); var second = FiveLaneAttackMotion.Enemy(b, "a");
            Check.Equal(1, second.Stroke); Check.Equal(-1, second.Direction); Check.True(second.Continues);
            b.Advance(1.9); Near(second.StartBeat, FiveLaneAttackMotion.Enemy(b, "a").StartBeat);
            b.Advance(2); Check.Equal(2, FiveLaneAttackMotion.Enemy(b, "a").Stroke);
            b.Advance(2.8); Check.False(FiveLaneAttackMotion.Enemy(b, "a").Active);
            Check.Equal(996m, b.PlayerHealth);
        }

        [Test] public void EnemyHoldPhaseAndSequenceSurviveCleanupPauseAndParry()
        {
            var b = Battle(Phrase(new WeaponPhraseNote(0, 1)), 120,
                new BeatAttack("a", 1, 1, 1.25), new BeatAttack("a", 2.25, 1, 5), new BeatAttack("a", 8, 1));
            b.Advance(6.8); var hold = FiveLaneAttackMotion.Enemy(b, "a");
            Check.False(b.Incoming.Any(a => a.Beat == 1));
            Check.Equal(AttackMotionMode.Sustain, hold.Mode); Check.Equal(1d, hold.SustainStartBeat);
            b.Pause(); var pose = FiveLaneAttackMotion.Body(hold, b.Beat, -1); b.Advance(30);
            Near(pose.Rotation, FiveLaneAttackMotion.Body(FiveLaneAttackMotion.Enemy(b, "a"), b.Beat, -1).Rotation);
            b.Resume(); b.Advance(8); Check.Equal(2, FiveLaneAttackMotion.Enemy(b, "a").Stroke);
            var blocked = Battle(WeaponPhraseCatalog.Find("heater-shield"), 120, new BeatAttack("a", 1, 5), new BeatAttack("a", 1.5, 5));
            blocked.Press(0, 1); Check.True(FiveLaneAttackMotion.Enemy(blocked, "a").Canceled);
            Check.False(FiveLaneAttackMotion.ContinuesEnemy(blocked, blocked.Incoming.Single(a => a.Beat == 1.5)));
        }
    }
}
