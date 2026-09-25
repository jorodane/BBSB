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
    public sealed class IntegratedCombatTests
    {
        private static WeaponNoteBinding Binding(int id, string part, int note) => new WeaponNoteBinding(new NotePartState(id, part), note);
        private static BattlePlan Plan(bool trickster = false)
        {
            var firstPattern = new MonsterPatternDefinition("긴 압박", "", new RhythmPattern("long", 4,
                new[] { new PatternStep(GestureKind.Hold, 0, 32), new PatternStep(GestureKind.Tap, 36) }),
                new[] { new CallSignal(0, "시작") }, 40, 4, 1);
            var shortPattern = new MonsterPatternDefinition("짧은 개입", "", new RhythmPattern("short", 4,
                new[] { new PatternStep(GestureKind.Tap, 0) }), new[] { new CallSignal(0, "시작") }, 16, 4, 1);
            var a = new MonsterDefinition("fixture-front", "선봉", "", GestureKind.Hold, new[] { firstPattern });
            var b = new MonsterDefinition(trickster ? "seesaw-goblin" : "fixture-rear", "후열", "", GestureKind.Tap, new[] { shortPattern });
            var monsters = new List<MonsterPlan> {
                new MonsterPlan("front", a, 0, new List<PlannedAttack>(), 0),
                new MonsterPlan("rear", b, 0, new List<PlannedAttack>(), 0) };
            return new BattlePlan(MusicStage.Generate(MusicCatalog.All[0]), monsters, new List<PlanWithdrawal>());
        }
        private static FiveLaneBattle FormationBattle(bool trickster = false, WeaponPhrase phrase = null)
        {
            var health = new StageHealth(200);
            var formation = new EncounterFormation(Plan(trickster), health, new FormationRules(rotationBeats: 100));
            return new FiveLaneBattle(new[] { new WeaponState("dagger") }, 120, 128, Array.Empty<BeatAttack>(), health, 10000, 10000,
                phrases: phrase == null ? null : new[] { phrase }, formation: formation);
        }
        private static FiveLaneBattle ShieldBattle(string[] weapons, params BeatAttack[] attacks) => new FiveLaneBattle(
            weapons.Select(id => new WeaponState(id)).ToArray(), 120, 32, attacks, new StageHealth(10000), 1000, 1000);

        [Test] public void FramesAndInjectedNotesPreserveTheBaseAndRemapEveryPrerequisite()
        {
            var weapon = new WeaponState("bow"); var source = WeaponPhraseSet.Uniform(weapon);
            weapon.SetNoteBindings(new[] { Binding(1, "frame-power", 1), Binding(2, "inject-tap", 1) });
            var result = WeaponNoteAssembly.Apply(weapon, source).LightStarts[0];
            Check.Equal(6, source.LightStarts[0].Notes.Count); Check.Equal(7, result.Notes.Count);
            Check.Equal(36m, result.Notes[1].Damage); Check.Equal(24m, source.LightStarts[0].Notes[1].Damage);
            Check.True(result.Notes[2].IsInjected); Check.Equal(1, result.Notes[2].Prerequisite);
            Check.Equal(3, result.Notes[4].Prerequisite); Check.Equal(5, result.Notes[6].Prerequisite);
            var battle = new FiveLaneBattle(new[] { weapon }, 120, 32, Array.Empty<BeatAttack>(), new StageHealth(1000), 100, 100);
            weapon.SetNoteBindings(Array.Empty<WeaponNoteBinding>());
            Check.Equal(2, battle.Lanes[0].Weapon.NoteBindings.Count); Check.Equal(7, battle.Lanes[0].Phrase.Notes.Count);
            battle.Press(0, 0); battle.Release(0, 0); battle.Press(0, 1); battle.Release(0, 2);
            battle.Press(0, 2.5); battle.Release(0, 2.5); Check.Equal(50.4m, battle.TotalDamage);
            Check.Equal(0, battle.MissCount);
        }
        [Test] public void ExtraTapRejectsOccupiedBeatsWhileHoldTransformationKeepsEveryVariant()
        {
            var staff = new WeaponState("staff");
            staff.SetNoteBindings(new[] { Binding(1, "inject-cross", 2) });
            var built = WeaponNoteAssembly.Apply(staff, WeaponPhraseSet.Uniform(staff));
            foreach (var phrase in built.LightStarts.Concat(built.DarkStarts))
            {
                var extra = phrase.Notes.Single(n => n.IsInjected);
                Check.Equal((phrase.Notes[2].LaneOffset + 1) % 2, extra.LaneOffset);
            }
            var dagger = new WeaponState("dagger"); dagger.SetNoteBindings(new[] { Binding(2, "inject-hold", 0) });
            Check.Equal(.5, WeaponNoteAssembly.Apply(dagger, WeaponPhraseSet.Uniform(dagger)).LightStarts[0].Notes[0].HoldBeats);
            var sword = new WeaponState("sword"); sword.SetNoteBindings(new[] { Binding(3, "inject-tap", 2) });
            bool rejected = false;
            try { WeaponNoteAssembly.Apply(sword, WeaponPhraseSet.Uniform(sword)); } catch (ArgumentException) { rejected = true; }
            Check.True(rejected); Check.Equal(1, WeaponPhraseCatalog.Find("dagger").Notes.Count);
        }
        [Test] public void ActualRewardsOwnPartsAndMovingOrRemovingThemNeverDeletesBaseNotes()
        {
            var run = new RunSession(31, useFiveLaneCombat: true);
            run.Enter(run.Map.Nodes.First(n => run.CanEnter(n.Id)).Id); run.ResolveBattle(run.StageTicket, true, 100);
            Check.Equal(4, run.Offers.Count); Check.True(run.ChooseReward(2)); Check.Equal(1, run.NoteParts.Count);
            var item = run.NoteParts[0]; Check.True(run.TryAttachNotePart(0, 0, 0, out _));
            var before = run.OwnedWeapons[0].NoteBindings[0];
            Check.False(run.TryAttachNotePart(0, 0, 99, out _)); Check.True(ReferenceEquals(before, run.OwnedWeapons[0].NoteBindings[0]));
            run.Equipment.Acquire(new WeaponState("sword"));
            Check.True(run.TryAttachNotePart(0, 2, 0, out _)); Check.Equal(0, run.OwnedWeapons[0].NoteBindings.Count);
            Check.True(ReferenceEquals(run.OwnedWeapons[2], run.NotePartOwner(item.InstanceId)));
            Check.True(run.RemoveNotePart(0)); Check.Equal(1, run.NoteParts.Count); Check.Equal(null, run.NotePartOwner(item.InstanceId));
            run.Abandon(); run.Restart(31); Check.Equal(0, run.NoteParts.Count);
            Check.True(run.OwnedWeapons.All(w => w.NoteBindings.Count == 0));
        }
        [Test] public void SwappingPreservesVisibleAttacksAndCutsOnlyTheHiddenHoldTail()
        {
            var battle = FormationBattle(); battle.Advance(1.2);
            var hold = battle.Incoming.Single(a => a.Definition.MonsterId == "front" && a.Definition.IsHold);
            decimal original = hold.Definition.Damage;
            Check.True(battle.RequestVanguardChange("rear")); Check.Equal(5.0, battle.Formation.SwitchAtBeat);
            Check.True(battle.Incoming.Contains(hold)); Check.Equal(5.0, hold.EndBeat);
            Check.Equal(original * 5 / 8, hold.DamageBudget);
            battle.Advance(5.25); Check.Equal("rear", battle.Formation.Front.InstanceId);
            Check.Equal(hold.DamageBudget, hold.DamageTaken); Check.Equal(5.0, battle.Formation.Find("front").FrontCursor);
            Check.False(battle.Incoming.Any(a => a.Definition.MonsterId == "front" && a.Definition.Rank == AttackRank.Front && a.Beat >= 5));
        }
        [Test] public void TricksterReturnsToTheInterruptedCursorAndReannouncesTheRemainingHold()
        {
            var battle = FormationBattle(true); battle.Advance(1);
            Check.True(battle.Formation.RequestSwitch("rear", battle.Beat, FormationChangeReason.Intrusion));
            battle.Advance(5); Check.Equal("rear", battle.Formation.Front.InstanceId);
            Check.Equal(FormationChangeReason.Return, battle.Formation.ChangeReason); Check.Equal(9.0, battle.Formation.SwitchAtBeat);
            battle.Advance(9); Check.Equal("front", battle.Formation.Front.InstanceId);
            var resumed = battle.Incoming.Single(a => a.Definition.MonsterId == "front" && a.Beat == 9 && a.Definition.IsHold);
            Check.Equal(12.0, resumed.EndBeat); Check.Equal(3.0, resumed.Definition.HoldBeats);
        }
        [Test] public void IndividualHealthAndRearTargetingUseOneActualDamageTarget()
        {
            var phrase = new WeaponPhrase("dagger", "후열 시험", "", 2,
                new[] { new WeaponPhraseNote(0, 10, target: WeaponAttackTarget.Rear) });
            var battle = FormationBattle(phrase: phrase); battle.Press(0, 0); battle.Release(0, 0);
            Check.Equal(100m, battle.Formation.Find("front").Health.Current);
            Check.Equal(90m, battle.Formation.Find("rear").Health.Current);
            Check.Equal(190m, battle.EnemyHealth.Current); Check.Equal("rear", battle.Lanes[0].LastDamageMonsterId);
            battle.Formation.Damage(1000, WeaponAttackTarget.Front, .5); Check.False(battle.Victory);
            battle.Formation.Damage(1000, WeaponAttackTarget.Front, .5); Check.True(battle.Victory);
        }
        [Test] public void FormationLongFramesAndPauseHaveTheSameStateAsSmallSteps()
        {
            var large = FormationBattle(true); var small = FormationBattle(true);
            large.Advance(1); small.Advance(1);
            large.RequestVanguardChange("rear"); small.RequestVanguardChange("rear");
            large.Pause(); large.Advance(100); Check.Equal(1.0, large.Beat); large.Resume();
            large.Advance(30);
            for (int tick = 5; tick <= 120; tick++) small.Advance(tick * .25);
            Check.Equal(large.PlayerHealth, small.PlayerHealth); Check.Equal(large.Formation.Front.InstanceId, small.Formation.Front.InstanceId);
            Check.Equal(large.Incoming.Count, small.Incoming.Count);
            Check.Equal(string.Join("|", large.Incoming.Select(a => a.Definition.MonsterId + "/" + a.Beat)),
                string.Join("|", small.Incoming.Select(a => a.Definition.MonsterId + "/" + a.Beat)));
        }
        [Test] public void WideShieldContactsDefendTheirOwnRanksAndKeepIndependentCooldowns()
        {
            var attacks = new[] { new BeatAttack("front", 1, 10), new BeatAttack("rear", 1, 10, rank: AttackRank.Rear) };
            var one = ShieldBattle(new[] { "wide-shield" }, attacks); one.Press(0, 1); one.Advance(1.25);
            Check.Equal(990m, one.PlayerHealth); Check.Equal(10m, one.TotalBlocked);
            var both = ShieldBattle(new[] { "wide-shield" }, attacks); both.Press(0, 1); both.Press(1, 1); both.Advance(1.25);
            Check.Equal(1000m, both.PlayerHealth); Check.Equal(20m, both.TotalBlocked);
            both.Release(0, 1.3); Check.Equal(PhraseLanePhase.Ready, both.ShieldAt(0).Phase);
            Check.Equal(PhraseLanePhase.Playing, both.ShieldAt(1).Phase); Check.True(both.RequiresHeldInput(1));
            Check.False(both.RequiresHeldInput(0));
        }
        [Test] public void WideShieldHoldsContinueAndTheirForecastsRequireBothContactsAfterPause()
        {
            var battle = ShieldBattle(new[] { "wide-shield" }, new BeatAttack("front", 1, 16, 4), new BeatAttack("rear", 1, 20, 5, AttackRank.Rear));
            battle.Press(0, 1); battle.Press(1, 1); battle.Advance(3);
            Check.Equal(5.0, battle.ShieldAt(0).EndBeat); Check.Equal(6.0, battle.ShieldAt(1).EndBeat);
            var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(battle);
            Check.Equal(2, timeline.Notes.Count); Check.Equal(2, battle.RequiredHeldSlots.Count());
            battle.Pause(); battle.Advance(20); Check.Equal(3.0, battle.Beat); battle.Resume();
            battle.Release(0, 5); battle.Release(1, 6); battle.Advance(6.25);
            Check.Equal(1000m, battle.PlayerHealth); Check.Equal(36m, battle.TotalBlocked);
        }
        [Test] public void ResonanceComesOnlyFromActualHeldProtectionAndBoostsTheMatchingAttack()
        {
            var battle = ShieldBattle(new[] { "resonance-shield", "dagger" }, new BeatAttack("front", 1, 48, 4));
            battle.Press(0, 0); battle.Advance(3.25);
            Check.Equal(12m, battle.LightResonance); Check.Equal(0m, battle.DarkResonance); Check.Equal(988m, battle.PlayerHealth);
            battle.Press(1, 3.25); battle.Release(1, 3.25);
            Check.Equal(0m, battle.TotalDamage); battle.Release(0, 3.25);
            battle.Press(1, battle.Lanes[1].NextBeat); battle.Release(1, battle.Beat);
            Check.Equal(9m, battle.TotalDamage); Check.Equal(0m, battle.LightResonance);
            var parry = ShieldBattle(new[] { "heater-shield", "resonance-shield" }, new BeatAttack("front", 1, 48, 4));
            parry.Press(1, 0); parry.Press(0, 1); parry.Advance(5.25);
            Check.Equal(0m, parry.LightResonance); Check.Equal(48m, parry.TotalBlocked);
        }
        [Test] public void GlobalAugmentsAffectDamageAndGuardButNeverShortenThePatternItself()
        {
            var phrase = new WeaponPhrase("dagger", "긴 쉼", "", 8, new[] { new WeaponPhraseNote(0, 10) }, completionCooldownBeats: 1);
            var battle = new FiveLaneBattle(new[] { new WeaponState("dagger") }, 120, 32, Array.Empty<BeatAttack>(),
                new StageHealth(1000), 100, 100, new[] { phrase }, bonuses: new CombatBonuses(force: 2, tempo: 3));
            battle.Press(0, 0); battle.Release(0, 0);
            Check.Equal(12m, battle.TotalDamage); Check.Equal(9.0, battle.Lanes[0].ReadyAtBeat);
            var guard = new FiveLaneBattle(new[] { new WeaponState("heater-shield") }, 120, 32,
                new[] { new BeatAttack("enemy", 1, 10) }, new StageHealth(1000), 100, 100, bonuses: new CombatBonuses(guard: 1));
            guard.Press(0, 0); guard.Advance(1.25); Check.Equal(95.5m, guard.PlayerHealth);
        }
        [Test] public void BellRecoversOnlyTheReservedShieldHalfAndNeverCreatesContact()
        {
            FiveLaneBattle Create()
            {
                var b = new FiveLaneBattle(new[] { new WeaponState("spirit-bell"), new WeaponState("wide-shield") },
                    120, 32, new[] { new BeatAttack("enemy", 1, 10) }, new StageHealth(10000), 1000, 1000,
                    phrases: new[] { WeaponPhraseCatalog.Find("spirit-bell").WithFirstNoteDelay(0), WeaponPhraseCatalog.Find("wide-shield") });
                b.Press(1, 0); b.Release(1, .1); b.Press(0, .2); b.Release(0, .2); return b;
            }
            var untouched = Create(); untouched.Advance(1.25);
            Check.Equal(990m, untouched.PlayerHealth); Check.False(untouched.RequiresHeldInput(1));
            var early = Create(); early.Press(1, .8); early.Advance(1.25);
            Check.Equal(1000m, early.PlayerHealth); Check.Equal(PhraseLanePhase.Playing, early.ShieldAt(1).Phase);
            Check.Equal(PhraseLanePhase.Ready, early.ShieldAt(2).Phase);
        }
        [Test] public void AFinisherStaggersOnlyItsActualTargetInAFormation()
        {
            var phrase = new WeaponPhrase("dagger", "마무리", "", 1, new[] { new WeaponPhraseNote(0, 1) },
                finisherEvery: 1, finisherDamage: 1, groggyBeats: 4);
            var battle = FormationBattle(phrase: phrase); battle.Press(0, 0);
            Check.Equal(4.0, battle.Formation.Find("front").GroggyUntilBeat);
            Check.Equal(0.0, battle.Formation.Find("rear").GroggyUntilBeat);
            Check.Equal(IncomingAttackState.Interrupted, battle.Incoming.First(a => a.Definition.MonsterId == "front").State);
        }
        [Test] public void WideShieldUsesEachAuthoredGuardAndReceivesAdjacentCooldownRecovery()
        {
            var weapon = new WeaponState("wide-shield", attribute: WeaponAttribute.Dual);
            WeaponPhrase Guard(decimal reduction) => new WeaponPhrase("wide-shield", "guard", "", 2,
                new[] { new WeaponPhraseNote(0, 0, 2, PhraseEffect.Parry) }, holdDamageReduction: reduction, completionCooldownBeats: 2);
            var starts = new[] { Guard(.25m), Guard(.75m) };
            var patterns = new WeaponPhraseSet(weapon, starts, starts);
            var battle = new FiveLaneBattle(new[] { weapon }, 120, 32,
                new[] { new BeatAttack("front", 1, 20, 3), new BeatAttack("rear", 1, 20, 3, AttackRank.Rear) },
                new StageHealth(1000), 100, 100, phraseSets: new[] { patterns });
            battle.Press(0, 0); battle.Press(1, 0); battle.Advance(4.25);
            Check.True(Math.Abs(80m - battle.PlayerHealth) < .000000001m); Check.Equal(6.0, battle.ShieldAt(0).ReadyAtBeat);
            Check.Equal(6.0, battle.ShieldAt(1).ReadyAtBeat);
            var support = ShieldBattle(new[] { "tuning-fork", "wide-shield" });
            support.Press(2, 0); support.Release(2, .1);
            support.Press(3, .1); support.Release(3, .2);
            support.Press(0, .3); support.Release(0, .3); support.Press(0, 2); support.Release(0, 2); support.Press(1, 3);
            Check.Equal(PhraseLanePhase.Ready, support.ShieldAt(2).Phase);
            Check.Equal(PhraseLanePhase.Ready, support.ShieldAt(3).Phase);
            Check.False(support.RequiresHeldInput(2)); Check.False(support.RequiresHeldInput(3));
        }
    }
}
