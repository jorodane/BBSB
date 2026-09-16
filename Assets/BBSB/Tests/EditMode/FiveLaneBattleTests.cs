using System;
using System.Linq;
using BBSB.Core;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class FiveLaneBattleTests
    {
        private static readonly string[] Equipment = { "sword", "hammer", "shield", "bow", "dagger" };
        private static FiveLaneBattle Battle(BeatAttack[] attacks = null, decimal health = 1000, decimal enemy = 10000,
            double bpm = 120, double loop = 32, string shield = "shield", WeaponPhrase shieldPhrase = null)
        {
            var weapons = Equipment.Select((id, slot) => new WeaponState(slot == 2 ? shield : id)).ToArray();
            var phrases = shieldPhrase == null ? null : weapons.Select((w, slot) =>
                slot == 2 ? shieldPhrase : WeaponPhraseCatalog.Find(w.DefinitionId)).ToArray();
            return new FiveLaneBattle(weapons, bpm, loop, attacks ?? Array.Empty<BeatAttack>(),
                new StageHealth(enemy), health, health, phrases);
        }
        private static void Tap(FiveLaneBattle battle, int slot, double at) { battle.Press(slot, at); battle.Release(slot, at); }
        private static void Hammer(FiveLaneBattle battle, double start)
        { Tap(battle, 1, start); Tap(battle, 1, start + 1.5); Tap(battle, 1, start + 3); }

        [Test] public void FirstInputChoosesHalfBeatPhaseAndNotesContinueOnTheirOwnLane()
        {
            var b = Battle(); Tap(b, 0, .5); Tap(b, 4, 1); Tap(b, 0, 1.5);
            Check.Equal(.5, b.Lanes[0].StartBeat); Check.Equal(1.0, b.Lanes[4].StartBeat);
            Check.Equal(2, b.Lanes[0].NextNote); Check.Equal(PhraseLanePhase.Ready, b.Lanes[1].Phase);
            Tap(b, 4, 1.5); Tap(b, 4, 2); Tap(b, 0, 2.5);
            Check.Equal(4.5, b.Lanes[0].StartBeat); Check.Equal(3.0, b.Lanes[4].StartBeat);
        }
        [Test] public void HalfMissKeepsPhraseWhileAMissOnlyCoolsItsOwnWeapon()
        {
            var b = Battle(); Tap(b, 0, 0); Tap(b, 1, 0); Tap(b, 0, 1.20);
            Check.Equal(1, b.HalfMissCount); Check.Equal(PhraseLanePhase.Playing, b.Lanes[0].Phase);
            b.Advance(1.75);
            Check.Equal(PhraseLanePhase.Cooldown, b.Lanes[1].Phase);
            Check.Equal(PhraseLanePhase.Playing, b.Lanes[0].Phase); Check.Equal(1000m, b.PlayerHealth);
        }
        [Test] public void InputSpamCannotDamageOrRestartDuringCooldown()
        {
            var b = Battle(); Tap(b, 0, 0); Tap(b, 0, .5);
            Check.Equal(PhraseLanePhase.Cooldown, b.Lanes[0].Phase);
            decimal damage = b.TotalDamage;
            for (double at = .6; at < 2.5; at += .1) Tap(b, 0, at);
            Check.Equal(damage, b.TotalDamage); Check.Equal(1, b.MissCount);
            b.Advance(2.5); Tap(b, 0, 2.5); Check.Equal(damage + 8, b.TotalDamage);
        }
        [Test] public void HoldingKeyDoesNotAutomaticallyPlayRepeatedTapNotes()
        {
            var b = Battle(); b.Press(0, 0); b.Press(0, 1); b.Advance(1.25);
            Check.Equal(8m, b.TotalDamage); Check.Equal(1, b.MissCount);
        }
        [Test] public void BowRequiresSustainBeforeItsSeparateShot()
        {
            var b = Battle(); b.Press(3, 0); b.Advance(.9); Check.True(b.Lanes[3].Holding);
            Check.Equal(0m, b.TotalDamage); b.Release(3, 1); Check.False(b.Lanes[3].Holding);
            Tap(b, 3, 2); Check.Equal(28m, b.TotalDamage); Check.Equal(2, b.PerfectCount);
        }
        [Test] public void EarlyReleaseCancelsHoldAndTheFollowingShot()
        {
            var b = Battle(); b.Press(3, 0); b.Release(3, .9); Tap(b, 3, 2);
            Check.Equal(0m, b.TotalDamage); Check.Equal(1, b.MissCount); Check.False(b.Lanes[3].Holding);
        }
        [Test] public void PauseFreezesHealthHoldsMissesAndCooldowns()
        {
            var b = Battle(new[] { new BeatAttack("enemy", 1, 10) });
            b.Press(3, 0); b.Advance(.5); b.Pause(); b.Advance(20); b.Release(3, 20); b.Press(0, 20);
            Check.Equal(.5, b.Beat); Check.Equal(1000m, b.PlayerHealth); Check.True(b.Lanes[3].Holding);
            b.Resume(); b.Release(3, 1); Check.Equal(1, b.PerfectCount); Check.Equal(0, b.MissCount);
        }
        [Test] public void BucklerBlocksOnlyItsOpeningBeatAndUnlocksThreeCounters()
        {
            var b = Battle(new[] { new BeatAttack("a", 2, 10), new BeatAttack("b", 2, 6), new BeatAttack("a", 3, 8) });
            Tap(b, 2, 2); Check.Equal(16m, b.TotalBlocked); Check.Equal("PARRY", b.Lanes[2].Feedback);
            Tap(b, 2, 3); b.Advance(3.25); Check.Equal(992m, b.PlayerHealth);
            Tap(b, 2, 3.5); Tap(b, 2, 4); Check.Equal(19m, b.TotalDamage);
            b.Advance(4.5); Check.Equal(PhraseLanePhase.Ready, b.Lanes[2].Phase);
        }
        [Test] public void FailedParryDealsNoDamageAndNeverOpensCounters()
        {
            var b = Battle(new[] { new BeatAttack("enemy", 2, 10) }); Tap(b, 2, 1);
            Check.Equal(PhraseLanePhase.Cooldown, b.Lanes[2].Phase); Check.Equal(0m, b.TotalDamage);
            b.Advance(2.25); Check.Equal(990m, b.PlayerHealth); Check.Equal(0m, b.TotalBlocked);
        }
        [Test] public void LateHalfMissParryStillFullyBlocksBeforeImpactDeadline()
        {
            var b = Battle(new[] { new BeatAttack("enemy", 2, 10) }); Tap(b, 2, 2.2); b.Advance(2.3);
            Check.Equal(1, b.HalfMissCount); Check.Equal(1000m, b.PlayerHealth); Check.Equal(10m, b.TotalBlocked);
            Check.Equal(2.0, b.Lanes[2].StartBeat);
        }
        [Test] public void BucklerCanParryAnAuthoredQuarterBeatExactly()
        {
            var b = Battle(new[] { new BeatAttack("enemy", 2.25, 10) }); Tap(b, 2, 2.25); b.Advance(2.5);
            Check.Equal(1, b.PerfectCount); Check.Equal(1000m, b.PlayerHealth); Check.Equal(10m, b.TotalBlocked);
        }
        [Test] public void DefenseWindowBoundaryIsInclusive()
        {
            var b = Battle(new[] { new BeatAttack("enemy", 2, 10) }); Tap(b, 2, 2 + b.HalfMissWindow);
            b.Advance(2.5); Check.Equal(1000m, b.PlayerHealth);
        }
        [Test] public void BuiltInShieldsHaveParryNotesAndRoundShieldUsesTwoFastCounters()
        {
            Check.Equal(3, WeaponPhraseCatalog.ShieldIds.Count);
            foreach (string id in WeaponPhraseCatalog.ShieldIds)
            {
                var phrase = WeaponPhraseCatalog.Find(id);
                Check.Equal(WeaponKind.Shield, WeaponCatalog.Find(id).Kind);
                Check.True(phrase.Notes.Any(n => n.IsParry));
                Check.Equal(RewardKind.Weapon, ContentCatalog.Find(id).Kind);
            }
            var b = Battle(new[] { new BeatAttack("enemy", 2, 10) }, shield: "round-shield");
            b.Press(2, 2); Check.Equal(10m, b.TotalBlocked); b.Release(2, 2);
            Tap(b, 2, 2.5); Tap(b, 2, 3.5); b.Advance(4);
            Check.Equal(16m, b.TotalDamage); Check.Equal(PhraseLanePhase.Ready, b.Lanes[2].Phase);
        }
        [Test] public void TowerShieldBlocksOnlyAtTimedReleaseAndUnlocksHeavyCounter()
        {
            var b = Battle(new[] { new BeatAttack("a", 0, 5), new BeatAttack("a", 1.5, 10),
                new BeatAttack("a", 2.5, 7) }, shield: "tower-shield");
            b.Press(2, 0); Check.Equal(0m, b.TotalBlocked);
            b.Advance(1.5); Check.True(b.Lanes[2].WaitingForParryRelease); Check.Equal(0, b.PerfectCount);
            Check.Equal(995m, b.PlayerHealth); b.Release(2, 1.5);
            Check.Equal(10m, b.TotalBlocked); Check.Equal("PARRY", b.Lanes[2].Feedback);
            Tap(b, 2, 2.5); b.Advance(2.75);
            Check.Equal(30m, b.TotalDamage); Check.Equal(988m, b.PlayerHealth);
        }
        [Test] public void TowerShieldCannotAutoParryOrParryByReleasingEarly()
        {
            foreach (bool releaseEarly in new[] { false, true })
            {
                var b = Battle(new[] { new BeatAttack("enemy", 1.5, 10) }, shield: "tower-shield");
                b.Press(2, 0); if (releaseEarly) b.Release(2, 1);
                b.Advance(1.75); b.Release(2, 1.75); Tap(b, 2, 2.5);
                Check.Equal(0m, b.TotalBlocked); Check.Equal(990m, b.PlayerHealth);
                Check.Equal(0m, b.TotalDamage); Check.Equal(1, b.MissCount);
                Check.Equal(PhraseLanePhase.Cooldown, b.Lanes[2].Phase);
            }
        }
        [Test] public void LateReleaseParryAtWindowBoundaryFullyBlocksWithHalfMissGrade()
        {
            var b = Battle(new[] { new BeatAttack("enemy", 1.5, 10) }, shield: "tower-shield");
            b.Press(2, 0); b.Release(2, 1.5 + b.HalfMissWindow); b.Advance(1.8);
            Check.Equal(10m, b.TotalBlocked); Check.Equal(1000m, b.PlayerHealth);
            Check.Equal(1, b.HalfMissCount); Check.Equal(1, b.Lanes[2].NextNote);
        }
        [Test] public void AuthoredKeyDownParryHoldDoesNotParryAgainOnRelease()
        {
            var phrase = new WeaponPhrase("shield", "test", "test", 2,
                new[] { new WeaponPhraseNote(0, 0, 1, PhraseEffect.Parry), new WeaponPhraseNote(1.5, 5) }, repeat: false);
            var b = Battle(new[] { new BeatAttack("enemy", 2, 10), new BeatAttack("enemy", 3, 7) }, shieldPhrase: phrase);
            b.Press(2, 2); Check.Equal(10m, b.TotalBlocked); Check.True(b.Lanes[2].Holding);
            b.Release(2, 3); b.Advance(3.25);
            Check.Equal(10m, b.TotalBlocked); Check.Equal(993m, b.PlayerHealth);
            Check.Equal(1, b.PerfectCount); Check.Equal(1, b.Lanes[2].NextNote);
        }
        [Test] public void PausePreservesReleaseParryUntilThePlayerLetsGoAfterResume()
        {
            var b = Battle(new[] { new BeatAttack("enemy", 1.5, 10) }, shield: "tower-shield");
            b.Press(2, 0); b.Advance(1); b.Pause(); b.Release(2, 20); b.Advance(20);
            Check.Equal(0m, b.TotalBlocked); Check.True(b.Lanes[2].WaitingForParryRelease);
            b.Resume(); b.Release(2, 1.5);
            Check.Equal(10m, b.TotalBlocked); Check.Equal(1, b.PerfectCount); Check.Equal(0, b.MissCount);
        }
        [Test] public void ReleaseParryRequiresAHoldAtEveryParryNote()
        {
            foreach (var notes in new[] {
                new[] { new WeaponPhraseNote(0, 0, effect: PhraseEffect.Parry) },
                new[] { new WeaponPhraseNote(0, 0, 1, PhraseEffect.Parry), new WeaponPhraseNote(2, 0, effect: PhraseEffect.Parry) } })
            {
                bool rejected = false;
                try { new WeaponPhrase("shield", "test", "test", 4, notes, parryInput: ParryInputEdge.KeyUp); }
                catch (ArgumentException) { rejected = true; }
                Check.True(rejected);
            }
        }
        [Test] public void ShieldCanParryLaterWithoutTurningItsOpeningStrikeIntoAParry()
        {
            var phrase = new WeaponPhrase("shield", "test", "test", 4,
                new[] { new WeaponPhraseNote(0, 3), new WeaponPhraseNote(1.5, 0, effect: PhraseEffect.Parry),
                    new WeaponPhraseNote(3, 7) }, repeat: false);
            var b = Battle(new[] { new BeatAttack("enemy", 0, 4), new BeatAttack("enemy", 1.5, 10) }, shieldPhrase: phrase);
            Tap(b, 2, 0); Check.Equal(0m, b.TotalBlocked); Check.Equal(3m, b.TotalDamage);
            Tap(b, 2, 1.5); Check.Equal(10m, b.TotalBlocked); Check.Equal("PARRY", b.Lanes[2].Feedback);
            Tap(b, 2, 3); Check.Equal(10m, b.TotalDamage); Check.Equal(996m, b.PlayerHealth);
        }
        [Test] public void RepeatingTresilloParriesEveryAuthoredNoteWithoutShiftingItsRhythm()
        {
            var phrase = new WeaponPhrase("shield", "test", "test", 4,
                new[] { 0.0, 1.5, 3.0 }.Select(at => new WeaponPhraseNote(at, 0, effect: PhraseEffect.Parry)));
            var b = Battle(new[] { 0.0, 1.5, 3.0, 4.125, 5.5, 7.0 }.Select(at => new BeatAttack("enemy", at, 10)).ToArray(),
                shieldPhrase: phrase);
            Tap(b, 2, 0); Tap(b, 2, 1.7); Check.Equal(1, b.HalfMissCount); Tap(b, 2, 3);
            Check.Equal(4.0, b.Lanes[2].StartBeat);
            Tap(b, 2, 4.125); Check.Equal(4.0, b.Lanes[2].StartBeat);
            Tap(b, 2, 5.5); Tap(b, 2, 7);
            Check.Equal(60m, b.TotalBlocked); Check.Equal(1000m, b.PlayerHealth);
            Check.Equal(2, b.Lanes[2].CompletedPhrases); Check.Equal(8.0, b.Lanes[2].StartBeat);
        }
        [Test] public void LaterParryCannotIgnoreTheWeaponRhythmToBlockAnOffBeatAttack()
        {
            var phrase = new WeaponPhrase("shield", "test", "test", 4,
                new[] { new WeaponPhraseNote(0, 0, effect: PhraseEffect.Parry), new WeaponPhraseNote(1.5, 0, effect: PhraseEffect.Parry) });
            var b = Battle(new[] { new BeatAttack("enemy", 0, 10), new BeatAttack("enemy", 1, 10) }, shieldPhrase: phrase);
            Tap(b, 2, 0); Tap(b, 2, 1); b.Advance(1.3);
            Check.Equal(10m, b.TotalBlocked); Check.Equal(990m, b.PlayerHealth);
            Check.Equal(1, b.MissCount); Check.Equal(PhraseLanePhase.Cooldown, b.Lanes[2].Phase);
        }
        [Test] public void MultipleReleaseParriesUseTheirOwnHoldLengthsAcrossPhraseRepeats()
        {
            var phrase = new WeaponPhrase("shield", "test", "test", 5,
                new[] { new WeaponPhraseNote(0, 3), new WeaponPhraseNote(1, 0, .5, PhraseEffect.Parry),
                    new WeaponPhraseNote(2.5, 0, 1, PhraseEffect.Parry), new WeaponPhraseNote(4, 7) },
                parryInput: ParryInputEdge.KeyUp);
            var b = Battle(new[] { 1.5, 3.5, 6.5, 8.5 }.Select(at => new BeatAttack("enemy", at, 10)).ToArray(), shieldPhrase: phrase);
            for (int cycle = 0; cycle < 2; cycle++)
            {
                double start = cycle * 5;
                Tap(b, 2, start); b.Press(2, start + 1); Check.True(b.Lanes[2].WaitingForParryRelease);
                b.Release(2, start + 1.5); b.Press(2, start + 2.5); b.Release(2, start + 3.5);
                Check.Equal("PARRY", b.Lanes[2].Feedback); Tap(b, 2, start + 4);
            }
            Check.Equal(40m, b.TotalBlocked); Check.Equal(20m, b.TotalDamage);
            Check.Equal(2, b.Lanes[2].CompletedPhrases); Check.Equal(0, b.MissCount);
        }
        [Test] public void StartingShieldChoiceIsLockedAfterBattleStartsIncludingPause()
        {
            var run = new RunSession(31, useFiveLaneCombat: true);
            Check.False(run.SelectStartingShield("tower-shield"));
            run.Enter(run.Map.Nodes.First(n => run.CanEnter(n.Id)).Id);
            Check.True(run.CanSelectStartingShield); Check.False(run.SelectStartingShield("sword"));
            Check.True(run.SelectStartingShield("round-shield")); Check.True(run.SelectStartingShield("tower-shield"));
            Check.Equal("tower-shield", run.Weapons[2].DefinitionId);
            var b = run.StartFiveLaneBattle(); b.Pause();
            Check.Equal("tower-shield", b.Lanes[2].Weapon.DefinitionId);
            Check.False(run.CanSelectStartingShield); Check.False(run.SelectStartingShield("shield"));
            Check.True(ReferenceEquals(b, run.StartFiveLaneBattle()));
        }
        [Test] public void ThirdTresilloFinishesAndInterruptsAttacksUntilGroggyEnds()
        {
            var b = Battle(new[] { new BeatAttack("a", 12, 10), new BeatAttack("a", 15, 10) });
            Hammer(b, 0); Hammer(b, 4); Check.False(b.IsGroggy); Hammer(b, 8);
            Check.Equal(3, b.Lanes[1].CompletedPhrases); Check.Equal(116m, b.TotalDamage);
            Check.Equal(15.0, b.GroggyUntilBeat); Check.True(b.IsGroggy);
            b.Advance(14); Check.Equal(1000m, b.PlayerHealth);
            b.Advance(15.25); Check.Equal(990m, b.PlayerHealth); Check.False(b.IsGroggy);
        }
        [Test] public void HammerMissResetsThreePhraseProgress()
        {
            var b = Battle(); Hammer(b, 0); b.Advance(4.25);
            Check.Equal(0, b.Lanes[1].CompletedPhrases); b.Advance(7.5); Hammer(b, 7.5);
            Check.Equal(1, b.Lanes[1].CompletedPhrases); Check.False(b.IsGroggy);
        }
        [Test] public void SongLoopsKeepApplyingEnemyAttacksAndBoundHistory()
        {
            var b = Battle(new[] { new BeatAttack("enemy", 1, 2) }, loop: 4);
            b.Advance(101.25); Check.Equal(948m, b.PlayerHealth); Check.True(b.Incoming.Count < 6);
        }
        [Test] public void LargeFramesResolveHoldsAndEnemyImpactsInChronologicalOrder()
        {
            var b = Battle(new[] { new BeatAttack("enemy", .5, 1000) }); b.Press(3, 0); b.Advance(4);
            Check.True(b.Finished); Check.Equal(0, b.PerfectCount); Check.Equal(0m, b.TotalDamage);
        }
        [Test] public void VictoryAndDeathStopAllSubsequentDamage()
        {
            var win = Battle(new[] { new BeatAttack("enemy", 1, 1000) }, enemy: 8);
            Tap(win, 0, 0); Check.True(win.Victory); win.Advance(10); Check.Equal(1000m, win.PlayerHealth);
            var lose = Battle(new[] { new BeatAttack("enemy", 1, 1000) }); lose.Advance(2);
            Tap(lose, 0, 2); Check.Equal(0m, lose.TotalDamage); Check.Equal(0, lose.MissCount);
        }
        [Test] public void FineAndCoarseAdvancementProduceTheSameOutcome()
        {
            var attacks = new[] { new BeatAttack("a", 1, 5), new BeatAttack("b", 2, 7) };
            var fine = Battle(attacks); var coarse = Battle(attacks);
            Tap(fine, 0, 0); Tap(coarse, 0, 0); fine.Press(3, 0); coarse.Press(3, 0);
            for (int step = 1; step <= 50; step++) fine.Advance(step / 10.0);
            coarse.Advance(5);
            Check.Equal(fine.PlayerHealth, coarse.PlayerHealth); Check.Equal(fine.MissCount, coarse.MissCount);
            Check.Equal(fine.PerfectCount, coarse.PerfectCount); Check.Equal(fine.Lanes[0].Phase, coarse.Lanes[0].Phase);
        }
        [Test] public void EveryExistingWeaponHasAValidIndependentTapHoldPhrase()
        {
            foreach (var weapon in WeaponCatalog.All)
            {
                var phrase = WeaponPhraseCatalog.Find(weapon.Id); Check.Equal(weapon.Id, phrase.WeaponId);
                Check.True(phrase.Notes.Count > 0); Check.Equal(0.0, phrase.Notes[0].Beat);
                Check.True(phrase.LengthBeats > phrase.Notes.Last().Beat + phrase.Notes.Last().HoldBeats);
            }
        }
        [Test] public void InvalidClockAndLoadoutAreRejectedWithoutMutatingBattle()
        {
            var b = Battle(); Tap(b, 0, 1);
            foreach (double time in new[] { double.NaN, double.PositiveInfinity, .5 })
            {
                bool rejected = false; try { b.Advance(time); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Check.True(rejected); Check.Equal(1.0, b.Beat);
            }
            bool invalid = false;
            try { new FiveLaneBattle(Array.Empty<WeaponState>(), 120, 32, Array.Empty<BeatAttack>(), new StageHealth(100), 100, 100); }
            catch (ArgumentException) { invalid = true; }
            Check.True(invalid);
        }
        [Test] public void NewSessionUsesFiveWeaponsAndPreservesBattleIdentityOnResume()
        {
            var run = new RunSession(31, useFiveLaneCombat: true);
            Check.Equal(string.Join(",", Equipment), string.Join(",", run.Weapons.Select(w => w.DefinitionId)));
            run.Enter(run.Map.Nodes.First(n => run.CanEnter(n.Id)).Id);
            var b = run.StartFiveLaneBattle(); Check.True(b != null); Check.True(run.StartRhythmRound() == null);
            b.Press(0, 0); b.Release(0, 0); b.Advance(1.25); b.Pause();
            var again = run.StartFiveLaneBattle(); Check.True(ReferenceEquals(b, again));
            Check.Equal(1.25, again.Beat); Check.Equal(PhraseLanePhase.Cooldown, again.Lanes[0].Phase);
        }
        [Test] public void SessionDamageAndTicketsCannotLeakIntoTheNextStage()
        {
            var run = new RunSession(31, useFiveLaneCombat: true);
            run.Enter(run.Map.Nodes.First(n => run.CanEnter(n.Id)).Id);
            string ticket = run.StageTicket; var b = run.StartFiveLaneBattle();
            var first = b.Incoming.FirstOrDefault();
            if (first == null) { b.Advance(8); first = b.Incoming.First(); }
            b.Advance(first.Beat + b.HalfMissWindow + .01);
            Check.Equal(b.PlayerHealth, run.Health); Check.True(run.Health < run.MaxHealth);
            decimal health = run.Health; Check.True(run.ResolveBattle(ticket, true, health));
            Check.True(b.Aborted); Check.True(run.PhraseBattle == null); Check.False(run.ResolveBattle(ticket, true, health));
            b.Advance(100); Check.Equal(health, run.Health);
            run.SkipReward(); run.Enter(run.Map.Nodes.First(n => run.CanEnter(n.Id)).Id);
            if (run.CurrentNode.IsBattle) Check.False(ReferenceEquals(b, run.StartFiveLaneBattle()));
        }
    }
}
