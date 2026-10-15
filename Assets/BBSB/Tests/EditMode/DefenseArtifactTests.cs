using System;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class DefenseArtifactTests
    {
        private static FiveLaneBattle Battle(string id = "dagger", BeatAttack[] attacks = null, WeaponPhrase phrase = null) =>
            new FiveLaneBattle(new[] { new WeaponState(id), new WeaponState("heater-shield") }, 120, 64,
                attacks ?? Array.Empty<BeatAttack>(), new StageHealth(10000), 100, 100, separateHands: true,
                phrases: phrase == null ? null : new[] { phrase, WeaponPhraseCatalog.Find("heater-shield") });
        private static void Tap(FiveLaneBattle battle, int slot, double at)
        { battle.Press(slot, at); battle.Release(slot, at); }

        [Test] public void PulseWeaponsExecuteOffGridOnceAndReuseTheirFormerCadenceAsCooldown()
        {
            foreach (string id in new[] { "dagger", "dual-swords" })
            {
                var b = Battle(id); double cadence = WeaponPhraseCatalog.Find(id).LengthBeats;
                b.Press(0, .37); Check.Equal(6m, b.TotalDamage);
                Check.Equal(0, b.Combo); Check.Equal(0, b.PerfectCount); Check.Equal(0, b.MissCount);
                Check.Equal(.37 + cadence, b.LaneAt(0).ReadyAtBeat);
                b.Advance(.37 + cadence + .1); b.Press(0, b.Beat);
                Check.Equal(6m, b.TotalDamage); // OS repeat / holding does not call again.
                b.Release(0, b.Beat); Tap(b, 0, b.Beat);
                Check.Equal(12m, b.TotalDamage); Check.Equal(0, b.MissCount);
            }
        }

        [Test] public void SingleActionCooldownDoesNotDependOnVisualPhraseLength()
        {
            var phrase = new WeaponPhrase("dagger", "Action", "", 12, new[] { new WeaponPhraseNote(0, 7) },
                completionCooldownBeats: .5, attackOnCall: true, cooldownFromLastInput: true);
            var b = Battle(phrase: phrase); Tap(b, 0, .3);
            Check.Equal(.8, b.LaneAt(0).ReadyAtBeat); Tap(b, 0, .8); Check.Equal(14m, b.TotalDamage);
        }

        [Test] public void ImmediateFirstActionDoesNotAutoplayItsFollowup()
        {
            var phrase = new WeaponPhrase("dagger", "Two actions", "", 4,
                new[] { new WeaponPhraseNote(0, 7), new WeaponPhraseNote(1, 9) },
                completionCooldownBeats: 2, attackOnCall: true, cooldownFromLastInput: true);
            var b = Battle(phrase: phrase); b.Press(0, .3); b.Advance(1.6);
            Check.Equal(7m, b.TotalDamage); Check.Equal(1, b.MissCount);
        }

        [Test] public void FramesAndAddedRhythmsPreserveTheActionAndKeepTheirOwnInputs()
        {
            var weapon = new WeaponState("dagger"); var source = WeaponPhraseSet.Uniform(weapon, separateHands: true);
            weapon.SetNoteBindings(new[] { new WeaponNoteBinding(new NotePartState(1, "frame-power"), 0),
                new WeaponNoteBinding(new NotePartState(2, "inject-tap"), 0) });
            var phrase = WeaponNoteAssembly.Apply(weapon, source).LightStarts[0];
            Check.True(phrase.AttackOnCall); Check.True(phrase.CooldownFromLastInput);
            Check.Equal(2, phrase.Notes.Count); Check.Equal(9m, phrase.Notes[0].Damage);
            var b = Battle(phrase: phrase); Tap(b, 0, .3); Tap(b, 0, .8);
            Check.Equal(2.8, b.LaneAt(0).ReadyAtBeat); Check.Equal(1, b.PerfectCount);
            weapon.SetNoteBindings(new[] { new WeaponNoteBinding(new NotePartState(3, "inject-hold"), 0) });
            var hold = WeaponNoteAssembly.Apply(weapon, source).LightStarts[0];
            Check.True(!hold.AttackOnCall); Check.Equal(1d, hold.FirstNoteDelayBeats); Check.True(hold.Notes[0].IsHold);
        }

        [Test] public void StaffChordsKeepAllDirectionsWhileAttackRunsIndependently()
        {
            foreach (int mask in new[] { 1, 2, 4, 8, 9, 6, 11, 15 })
            {
                var attacks = Enumerable.Range(0, 4).Where(d => (mask & 1 << d) != 0)
                    .Select(d => new BeatAttack("e" + d, 1, 5, defenseDirection: d)).ToArray();
                var b = Battle(attacks: attacks);
                for (int d = 0; d < 4; d++) if ((mask & 1 << d) != 0) b.Press(4 + d, 1);
                Tap(b, 0, 1); b.Advance(1.25);
                Check.Equal(mask, DefenseArtifactMotion.Mask(b)); Check.Equal(100m, b.PlayerHealth);
                Check.Equal(6m, b.TotalDamage); Check.Equal(attacks.Length * 5m, b.TotalBlocked);
                var frame = DefenseArtifactMotion.Sample(b);
                Check.True(!double.IsNaN(frame.Rotation));
                b.Pause(); Check.Equal(mask, DefenseArtifactMotion.Mask(b));
            }
        }

        [Test] public void EveryProjectileStartsAtItsMonsterAndArrivesAtItsOwnStaffContact()
        {
            var source = new BattlePathPoint(700, 400); var body = new BattlePathPoint(180, 300);
            for (int d = 0; d < 4; d++)
            {
                var start = DefenseArtifactMotion.Projectile(source, body, 200, d, 0);
                var end = DefenseArtifactMotion.Projectile(source, body, 200, d, 1);
                var offset = DefenseArtifactMotion.Contact(d);
                Check.Equal(source.X, start.X); Check.Equal(source.Y, start.Y);
                Check.Equal(body.X + offset.X * 200, end.X); Check.Equal(body.Y + offset.Y * 200, end.Y);
            }
        }

        [Test] public void LegacyAndChaosPulsePatternsRemainAvailable()
        {
            Check.True(WeaponPhraseSet.Uniform(new WeaponState("dagger")).LightStarts[0].Repeat);
            var chaos = WeaponPhraseSet.Uniform(new WeaponState("dagger", attribute: WeaponAttribute.Chaos), separateHands: true);
            Check.True(chaos.LightStarts[0].Repeat); Check.True(!chaos.LightStarts[0].AttackOnCall);
            var drum = WeaponPhraseSet.Uniform(new WeaponState("war-drum"), separateHands: true).LightStarts[0];
            Check.True(drum.AttackOnCall); Check.Equal(PhraseEffect.AdvanceRear, drum.Notes[0].Effect);
            Check.Equal(0m, drum.Notes[0].Damage); Check.Equal(8d, drum.CompletionCooldownBeats);
        }

        [Test] public void LegacyDrumPartsAreKeptAndCrossInjectionNoLongerRequiresLaneOwnership()
        {
            var drum = new WeaponState("war-drum");
            drum.SetNoteBindings(new[] { new WeaponNoteBinding(new NotePartState(1, "frame-power"), 0) });
            var legacy = WeaponNoteAssembly.Apply(drum, WeaponPhraseSet.Uniform(drum, separateHands: true), separateHands: true);
            Check.True(!legacy.LightStarts[0].AttackOnCall); Check.Equal(8, legacy.LightStarts[0].Notes.Count);
            Check.Equal(9m, legacy.LightStarts[0].Notes[0].Damage);
            var dagger = new WeaponState("dagger");
            dagger.SetNoteBindings(new[] { new WeaponNoteBinding(new NotePartState(2, "inject-cross"), 0) });
            var source = WeaponPhraseSet.Uniform(dagger, separateHands: true);
            var folded = WeaponNoteAssembly.Apply(dagger, source, separateHands: true).LightStarts[0];
            Check.Equal(2, folded.Notes.Count); Check.Equal(0, folded.Notes[1].LaneOffset);
            Check.Equal(4.8m, folded.Notes[1].Damage);
            var demo = new NoteWorkshopPlayback(source.LightStarts[0], 1, 0);
            Check.Equal(2d, demo.LoopBeats); Check.Equal(NoteDemoInput.Press, demo.InputAt(2, 0));
        }
    }
}
