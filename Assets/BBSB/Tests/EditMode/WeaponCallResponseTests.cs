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
    public sealed class WeaponCallResponseTests
    {
        private static FiveLaneBattle Battle(WeaponState weapon, WeaponPhrase phrase = null, int bpm = 120,
            params BeatAttack[] attacks) => new FiveLaneBattle(new[] { weapon }, bpm, 64, attacks,
                new StageHealth(10000), 100, 100, phrases: phrase == null ? null : new[] { phrase });
        private static void Tap(FiveLaneBattle b, double beat) { b.Press(0, beat); b.Release(0, beat); }
        private static WeaponPhrase Sequence() => new WeaponPhrase("dagger", "Three calls", "", 4,
            new[] { new WeaponPhraseNote(0, 99, role: WeaponNoteRole.Call),
                new WeaponPhraseNote(1, 99, role: WeaponNoteRole.Call),
                new WeaponPhraseNote(2, 99, role: WeaponNoteRole.Call),
                new WeaponPhraseNote(3, 40, condition: PhraseNoteCondition.AllCalls) }, firstNoteDelayBeats: 2);

        [Test] public void InvocationNeverHitsAndTheInjectedHalfBeatFollowsTheJudgedResponse()
        {
            foreach (int bpm in new[] { 72, 120, 200 })
            foreach (double input in new[] { 0d, .13, .49, .76 })
            foreach (var attribute in new[] { WeaponAttribute.Light, WeaponAttribute.Dark, WeaponAttribute.Dual })
            {
                var weapon = new WeaponState("dagger", attribute);
                weapon.SetNoteBindings(new[] { new WeaponNoteBinding(new NotePartState(1, "inject-tap"), 0) });
                var b = Battle(weapon, bpm: bpm); var lane = b.Lanes[0]; Tap(b, input);
                double first = lane.NextBeat;
                Check.True(first >= input + 1 - .000001); Check.Equal(0m, b.TotalDamage);
                Check.Equal(0, b.PerfectCount); Check.Equal(0, b.Combo); Check.Equal("CALL", lane.Feedback);
                Tap(b, input + .05); Check.Equal(first, lane.NextBeat); Check.Equal(0, b.MissCount);
                Tap(b, first); Check.Equal(6m, b.TotalDamage); Check.Equal(first + .5, lane.NextBeat);
                Tap(b, first + .5); Check.Equal(9.6m, b.TotalDamage); Check.Equal(first + 2, lane.NextBeat);
                Tap(b, first + 2); Check.Equal(first + 2.5, lane.NextBeat); Check.Equal(0, b.MissCount);
            }
        }

        [Test] public void HoldingTheInvocationCannotAutoPressTheFirstResponse()
        {
            var b = Battle(new WeaponState("dagger")); b.Press(0, .2);
            double first = b.Lanes[0].NextBeat;
            b.Press(0, first); Check.Equal(0m, b.TotalDamage);
            b.Advance(first + b.HalfMissWindow + .01);
            Check.Equal(1, b.MissCount); Check.Equal(0, b.PerfectCount);
        }

        [Test] public void FirstResponseUsesTheNormalTimingWindowAndPauseKeepsItsSchedule()
        {
            var b = Battle(new WeaponState("dual-swords")); Tap(b, 0);
            b.Pause(); b.Advance(100); Check.Equal(0d, b.Beat); Check.Equal(1d, b.Lanes[0].NextBeat);
            b.Resume(); Tap(b, 1.2); Check.Equal(1, b.HalfMissCount); Check.Equal(3m, b.TotalDamage);
            Check.Equal(2d, b.Lanes[0].NextBeat);
        }

        [Test] public void ThreeSuccessfulCallsUnlockExactlyOneResponse()
        {
            var b = Battle(new WeaponState("dagger"), Sequence()); Tap(b, 0);
            Check.Equal(2d, b.Lanes[0].NextBeat); Check.Equal(PhraseNoteState.Locked, b.Lanes[0].NoteStates[3]);
            Tap(b, 2); Tap(b, 3.2); Check.Equal(PhraseNoteState.Locked, b.Lanes[0].NoteStates[3]);
            Tap(b, 4); Check.Equal(PhraseNoteState.Pending, b.Lanes[0].NoteStates[3]);
            Check.Equal(0m, b.TotalDamage); Check.Equal(3, b.Combo);
            Tap(b, 5); Check.Equal(40m, b.TotalDamage); Check.Equal(PhraseLanePhase.Cooldown, b.Lanes[0].Phase);
        }

        [Test] public void AnyMissedCallSkipsTheFinalResponseAndBreaksItsForecast()
        {
            for (int miss = 0; miss < 3; miss++)
            {
                var b = Battle(new WeaponState("dagger"), Sequence()); Tap(b, 0);
                var timeline = new FiveLaneNoteTimeline();
                for (int i = 0; i < 3; i++)
                {
                    b.Advance(2 + i); timeline.Refresh(b);
                    if (i == miss) b.Advance(2 + i + b.HalfMissWindow + .01); else Tap(b, 2 + i);
                    timeline.Refresh(b);
                }
                Check.Equal(PhraseNoteState.Skipped, b.Lanes[0].NoteStates[3]);
                Check.Equal(0m, b.TotalDamage); Check.Equal(1, b.MissCount);
                Check.Equal(0, timeline.Notes.Count);
            }
        }

        [Test] public void CallHoldDoesNotGuardHealOrTriggerAFinisher()
        {
            var p = new WeaponPhrase("dagger", "Preparation", "", 2,
                new[] { new WeaponPhraseNote(0, 100, 2, role: WeaponNoteRole.Call) }, firstNoteDelayBeats: 1,
                holdDamageReduction: 1, finisherEvery: 1, finisherDamage: 100);
            var b = Battle(new WeaponState("dagger"), p, 120, new BeatAttack("a", 2, 10), new BeatAttack("b", 2, 10, .5));
            Tap(b, 0); b.Press(0, 1); Check.False(FiveLaneArtTimeline.Guarding(b.Lanes[0])); b.Release(0, 3);
            Check.Equal(0m, b.TotalDamage); Check.Equal(0m, b.TotalReduced); Check.Equal(0m, b.TotalHealed);
            Check.Equal(80m, b.PlayerHealth);
        }

        [Test] public void InjectionPreservesCallRolesAndJoinsTheAllCallsRequirement()
        {
            var weapon = new WeaponState("dagger");
            weapon.SetNoteBindings(new[] { new WeaponNoteBinding(new NotePartState(1, "inject-tap"), 0) });
            var b = Battle(weapon, Sequence()); var p = b.Lanes[0].Phrase;
            Check.Equal(2d, p.FirstNoteDelayBeats); Check.True(p.Notes[1].IsCall); Check.Equal(0m, p.Notes[1].Damage);
            Tap(b, 0); Tap(b, 2); b.Advance(2.8); Tap(b, 3); Tap(b, 4);
            Check.Equal(PhraseNoteState.Skipped, b.Lanes[0].NoteStates[4]); Check.Equal(0m, b.TotalDamage);
        }

        [Test] public void ShieldsRemainImmediateAndTheDemoShowsInvocationBeforeNotes()
        {
            foreach (var definition in WeaponCatalog.All)
                Check.Equal(definition.Kind == WeaponKind.Shield ? 0d : 1d, WeaponPhraseCatalog.Find(definition.Id).FirstNoteDelayBeats);
            var b = Battle(new WeaponState("heater-shield"), null, 120, new BeatAttack("a", 1, 10));
            b.Press(0, 1); b.Advance(1.25); Check.Equal(10m, b.TotalBlocked); Check.Equal(100m, b.PlayerHealth);
            var demo = new NoteWorkshopPlayback(Sequence(), 1, 0);
            Check.Equal(NoteDemoInput.Invoke, demo.InputAt(0, 0)); Check.Equal(-1d, demo.PatternBeat(1));
            Check.Equal(NoteDemoInput.Call, demo.InputAt(2, 0)); Check.Equal(NoteDemoInput.Press, demo.InputAt(5, 0));
            var times = new List<double>(); demo.VisitNotes(0, 6, (n, s, e, l) => times.Add(s));
            Check.True(times.SequenceEqual(new[] { 2d, 3, 4, 5 }));
            Check.Equal(NoteDemoInput.Invoke, demo.InputAt(demo.LoopBeats, 0));
            Check.Equal(2d, demo.PreparationRemaining(demo.LoopBeats));
            Check.Equal(NoteDemoInput.Call, demo.InputAt(demo.LoopBeats + 2, 0));
        }

        [Test] public void BellReservesALongerPreparationOnceAndCannotJudgeItForThePlayer()
        {
            var bell = WeaponPhraseCatalog.Find("spirit-bell");
            var target = WeaponPhraseCatalog.Find("dagger").WithFirstNoteDelay(3);
            var b = new FiveLaneBattle(new[] { new WeaponState("spirit-bell"), new WeaponState("dagger") },
                120, 64, Array.Empty<BeatAttack>(), new StageHealth(1000), 100, 100, phrases: new[] { bell, target });
            Tap(b, 0); Tap(b, 1); var start = b.ScheduledStarts.Single();
            Check.Equal(4d, start.Beat); b.Advance(start.Beat);
            Check.Equal(4d, b.Lanes[1].NextBeat); Check.Equal(0m, b.TotalDamage);
            b.Press(1, 4); b.Release(1, 4); Check.Equal(6m, b.TotalDamage); Check.Equal(6d, b.Lanes[1].NextBeat);
        }

        [Test] public void OnlyAResponseLaunchesThePhysicalWeaponAndGuardMovesAheadOfThePlayer()
        {
            var b = Battle(new WeaponState("dagger"), Sequence()); Tap(b, 0); Tap(b, 2);
            var body = new BattlePathPoint(.15, .70); var target = new BattlePathPoint(.5, .70);
            var lane = b.Lanes[0];
            var home = WeaponFormation.Sample(0, b.Beat * .5, body, .3, 16d / 9);
            var frame = FiveLaneWeaponMotion.Sample(b, lane, 0, body, .3, 16d / 9, target);
            Check.Equal(home.Position.X, frame.Position.X); Check.Equal(home.Position.Y, frame.Position.Y);
            Tap(b, 3); Tap(b, 4); Tap(b, 5);
            b.Advance(5 + WeaponMotion.ImpactSeconds(FiveLaneWeaponMotion.Style(lane)) * 2);
            frame = FiveLaneWeaponMotion.Sample(b, lane, 0, body, .3, 16d / 9, target);
            Check.True(Math.Abs(target.X - frame.Position.X) < .000001);
            Check.True(Math.Abs(target.Y - frame.Position.Y) < .000001);
            b.Advance(7); frame = FiveLaneWeaponMotion.Sample(b, lane, 0, body, .3, 16d / 9, target);
            Check.True(frame.Position.X < .3);
            var shield = Battle(new WeaponState("heater-shield")); shield.Press(0, 0);
            frame = FiveLaneWeaponMotion.Sample(shield, shield.Lanes[0], 0, body, .3, 16d / 9, target);
            Check.True(frame.Position.X > body.X && frame.Position.X < target.X); Check.Equal(body.Y, frame.Position.Y);
        }
    }
}
