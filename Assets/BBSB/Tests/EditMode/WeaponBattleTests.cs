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
        public void StartingWeaponsCoverEveryGestureOnceAtBasicGradeAndAlwaysDealDamage()
        {
            var run = new RunSession(73);
            for (int cycle = 0; cycle < 2; cycle++)
            {
                Check.Equal(5, run.Weapons.Select(w => w.DefinitionId).Distinct().Count());
                Check.False(run.Weapons.Any(w => w.DefinitionId == "shield"));
                foreach (GestureKind kind in Enum.GetValues(typeof(GestureKind)))
                {
                    var supported = run.Weapons.Select(w => WeaponCatalog.Find(w.DefinitionId).ActionFor(kind, w.Rarity)).Where(a => a != null).ToArray();
                    Check.Equal(1, supported.Length); Check.True(supported.Any(a => a.Damage > 0));
                    var monster = MonsterCatalog.All.First(m => m.Patterns.Any(p => p.Pattern.Steps.Count == 1 && p.Pattern.Steps[0].Kind == kind));
                    var pattern = monster.Patterns.First(p => p.Pattern.Steps.Count == 1 && p.Pattern.Steps[0].Kind == kind);
                    var preview = new MonsterPreview(monster, pattern);
                    var loadout = new WeaponLoadout(preview.Round.Plan, run.Weapons);
                    Check.Equal(1, loadout.RespondingSlots(loadout.Patterns[0]).Count);
                    var round = new RhythmRound(preview.Round.Plan, combat: new WeaponBattle(loadout, new StageHealth(1000), 100, 100, true));
                    var note = round.Notes[0]; double at = note.StartSeconds;
                    if (kind == GestureKind.Flick)
                    { round.Press(at - .08, 0, 0); round.Move(at - .02, .1, 0); round.Release(at, .2, 0); }
                    else
                    {
                        round.Press(at, 0, 0);
                        if (kind == GestureKind.Shake) { round.Move(at + .03, .1, 0); round.Move(at + .06, 0, 0); }
                        else if (kind == GestureKind.Dive) round.Release(note.EndSeconds, 0, 0);
                        else round.Advance(note.EndSeconds);
                    }
                    Check.Equal(1, round.PerfectCount); Check.Equal(1, round.Combat.Activations.Count);
                    Check.True(round.Combat.TotalDamage > 0);
                }
                run.Restart(19);
            }
        }

        [Test]
        public void InitialWeaponLevelRespectsTheUpgradeLimit()
        {
            Check.Equal(0, new WeaponState("spear").Level);
            for (int level = 0; level <= RunRules.MaximumUpgrade; level++)
                Check.Equal(level, new WeaponState("spear", level).Level);
            foreach (int level in new[] { -1, RunRules.MaximumUpgrade + 1 })
            {
                bool rejected = false;
                try { new WeaponState("spear", level); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                Check.True(rejected);
            }
        }

        [Test]
        public void RarityControlsActionsWhileEveryUpgradeScalesOnlyTheirEffects()
        {
            var damageTable = new Dictionary<string, decimal[]> {
                { "sword", new[] { 12m, 18m, 24m } }, { "shield", new[] { 0m, 8m } }, { "spear", new[] { 20m, 16m, 24m } },
                { "round-shield", new[] { 0m, 8m } }, { "tower-shield", new[] { 0m, 10m } },
                { "heater-shield", new[] { 0m, 8m } },
                { "wide-shield", new[] { 0m, 8m } }, { "resonance-shield", new[] { 0m, 0m } },
                { "dual-swords", new[] { 6m, 12m, 20m } },
                { "staff", new[] { 8m, 18m, 16m } }, { "spirit-bell", new[] { 0m, 8m, 0m } },
                { "hammer", new[] { 26m, 36m, 20m } }, { "dagger", new[] { 14m, 5m, 12m } }, { "greatsword", new[] { 26m, 36m, 18m } },
                { "bell", new[] { 8m, 0m, 8m } }, { "blade", new[] { 16m, 12m, 20m } },
                { "bow", new[] { 10m, 30m, 18m } }, { "crossbow", new[] { 22m, 12m, 26m } }, { "wand", new[] { 18m, 28m, 12m } } };
            Check.Equal(32, WeaponCatalog.All.Count);
            foreach (var entry in WeaponExpansion.All)
                damageTable.Add(entry.Definition.Id, entry.Definition.Actions.Select(a => a.Damage).ToArray());
            foreach (var weapon in WeaponCatalog.All)
            {
                bool shield = weapon.Kind == WeaponKind.Shield;
                Check.Equal(shield ? 2 : 3, weapon.Actions.Count);
                Check.Equal(weapon.Actions.Count, weapon.Actions.Select(a => a.Kind).Distinct().Count());
                foreach (var rarity in WeaponRarities.All)
                for (int level = 0; level <= 3; level++)
                {
                    int count = shield ? (rarity == WeaponRarity.Legendary ? 2 : 1) : new[] { 1, 2, 2, 3 }[(int)rarity];
                    Check.Equal(count, weapon.ActionsAt(rarity).Count);
                    for (int i = 0; i < weapon.Actions.Count; i++)
                    {
                        var action = weapon.Actions[i];
                        var step = new PatternStep(action.Kind, 0, action.Kind == GestureKind.Hold || action.Kind == GestureKind.Dive ? 8 : 0);
                        var plan = Plan(step);
                        var loadout = new WeaponLoadout(plan, new[] { new WeaponState(weapon.Id, rarity, level) });
                        var round = Round(plan, loadout); Perform(round, new[] { step }, 2);
                        decimal expected = i < count ? damageTable[weapon.Id][i] * (1 + .25m * level) * plan.Attacks[0].JudgmentWeight : 0;
                        Check.Equal(expected, round.Combat.TotalDamage); Check.Equal(1000 - expected, round.Combat.EnemyHealth.Current);
                        Check.Equal(1, round.PerfectCount); Check.Equal(1, round.Notes.Count); Check.Equal(0m, round.TotalDamageTaken);
                        Check.Equal(i < count ? 1 : 0, round.WeaponResults.Count);
                        Check.Equal(i < count ? 1 : 0, round.Combat.Bindings.Count);
                        Check.Equal(i < count ? action.Guard * (1 + .25m * level) * plan.Attacks[0].JudgmentWeight : 0, round.Combat.GuardAt(round.ElapsedSeconds));
                    }
                }
            }
        }

        [Test]
        public void HalfMissUsesTheUnlockedActionAndPrecisionSpearDoesNotFire()
        {
            foreach (var item in new[] { ("sword", GestureKind.Tap, 0, 6m), ("spear", GestureKind.Tap, 0, 0m),
                ("spear", GestureKind.Flick, 1, 10m), ("greatsword", GestureKind.Dive, 1, 16.25m),
                ("shield", GestureKind.Hold, 0, 0m), ("blade", GestureKind.Flick, 0, 8m) })
            {
                var step = new PatternStep(item.Item2, 0, item.Item2 == GestureKind.Hold || item.Item2 == GestureKind.Dive ? 8 : 0);
                var plan = Plan(step); var loadout = new WeaponLoadout(plan, new[] { new WeaponState(item.Item1, item.Item3 == 0 ? WeaponRarity.Common : WeaponRarity.Rare, item.Item3) });
                var round = Round(plan, loadout); Perform(round, new[] { step }, 2, .1);
                Check.Equal(item.Item4 * plan.Attacks[0].JudgmentWeight, round.Combat.TotalDamage); Check.Equal(1, round.HalfMissCount);
                Check.Equal(item.Item1 == "shield" ? 4m * plan.Attacks[0].JudgmentWeight : 0m, round.Combat.GuardAt(round.ElapsedSeconds));
            }
        }

        [Test]
        public void SharedInputTriggersEveryEquippedCopyWithoutDuplicatingMonsterDamageOrBodyMotion()
        {
            var plan = Plan(new PatternStep(GestureKind.Tap, 0));
            var loadout = Loadout(plan, "spear", "spear", "spear", "spear", "spear");
            var round = Round(plan, loadout, 20); round.Press(2, 0, 0);
            Check.Equal(200m, round.Combat.TotalDamage); Check.Equal(5, round.Combat.Activations.Count);
            Check.True(round.Combat.Victory); Check.Equal(0m, round.Combat.EnemyHealth.Current);
            Check.Equal(1, round.Notes.Count); Check.Equal(1, round.Results.Count);
            Check.Equal(5, round.WeaponResults.Count);
            Check.True(round.WeaponResults.All(x => ReferenceEquals(x.Source, round.Results[0])));
            Check.True(round.Combat.Activations.All(x => x.AtSeconds == 2));
            Check.Equal(1, round.PerfectCount); Check.Equal(100.0, round.ScorePercent);
            var motion = new PlayerMotionTimeline(1); motion.Evaluate(round);
            Check.Equal(1, motion.PunchSelections);
            round.Release(2.001, 0, 0); round.Advance(2.1);
            Check.Equal(200m, round.Combat.TotalDamage);
        }

        [Test]
        public void AutomaticResponsesCoverEveryMatchingStepWithoutAddingAnInput()
        {
            var plan = Plan(new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 4));
            var loadout = new WeaponLoadout(plan, new[] { new WeaponState("sword"), new WeaponState("dagger", WeaponRarity.Rare, 1) });
            var round = Round(plan, loadout); var unarmed = new RhythmRound(plan);
            Check.Equal(unarmed.ResponseNoteCount, round.ResponseNoteCount);
            Check.True(round.Notes.Select(x => (x.Attack.Id, x.StepIndex, x.StartTick, x.Step.Kind, x.Step.DurationTicks))
                .SequenceEqual(unarmed.Notes.Select(x => (x.Attack.Id, x.StepIndex, x.StartTick, x.Step.Kind, x.Step.DurationTicks))));
            Perform(round, plan.Attacks[0].Placement.Pattern.Steps, 2);
            Check.Equal(2, round.PerfectCount); Check.Equal(0, round.MissCount);
            Check.Equal(4, round.WeaponResults.Count); Check.Equal(39m, round.Combat.TotalDamage);
            Check.False(round.FreeInput.Kind.HasValue);
        }

        [Test]
        public void SharedMissReachesEveryWeaponButDamagesAndReactsOnlyOnce()
        {
            var plan = Plan(new PatternStep(GestureKind.Tap, 0));
            var loadout = Loadout(plan, "spear", "spear", "spear", "spear", "spear");
            var round = Round(plan, loadout); var motion = new PlayerMotionTimeline(3);
            round.Advance(2.2); var frame = motion.Evaluate(round);
            Check.Equal(8m, round.TotalDamageTaken); Check.Equal(1, round.MissCount);
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
            var round = Round(plan, loadout); round.Press(2.1, 0, 0); round.Release(2.5, 0, 0);
            Check.Equal(1, round.HalfMissCount); Check.Equal(5, round.Combat.Activations.Count);
            Check.True(round.WeaponResults.All(x => x.Grade == RhythmGrade.HalfMiss && ReferenceEquals(x.Source, round.Results[0])));
            Check.Equal(4m, round.Combat.TotalBlocked); Check.Equal(0m, round.TotalDamageTaken);
            Check.Equal(56m, round.Combat.GuardAt(2.5)); // Five shields at half strength, one incoming hit.
        }

        [Test]
        public void OneWeaponAnswersEveryMatchingStepAndEveryOccurrenceOfThePhrase()
        {
            var plan = Plan(new[] { new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 4), new PatternStep(GestureKind.Tap, 8) }, new[] { 16, 48 });
            var loadout = Loadout(plan, "sword"); var round = Round(plan, loadout);
            Check.Equal(1, loadout.Patterns.Count); Check.Equal(6, round.Notes.Count); Check.Equal(6, round.Combat.Bindings.Count);
            Check.True(round.Combat.Bindings.Select(x => x.Note.StartTick).SequenceEqual(new[] { 16, 20, 24, 48, 52, 56 }));
            Perform(round, plan.Attacks[0].Placement.Pattern.Steps, 2); Perform(round, plan.Attacks[0].Placement.Pattern.Steps, 6);
            Check.Equal(48.0024m, round.Combat.TotalDamage); Check.Equal(6, round.PerfectCount); Check.Equal(6, round.Combat.Activations.Count);
        }

        [Test]
        public void WeaponsUseTheAuthoredDiveShakeAndFlickJudgmentsWithoutAddingGestures()
        {
            var plan = Plan(new PatternStep(GestureKind.Dive, 0, 4), new PatternStep(GestureKind.Shake, 0),
                new PatternStep(GestureKind.Flick, 4)); var source = plan.Attacks[0];
            var loadout = Loadout(plan, "dagger", "bell", "blade");
                        var round = Round(plan, loadout);
            round.Press(2, 0, 0); round.Move(2.04, .1, 0); round.Move(2.08, 0, 0);
            round.Move(2.25, 0, 0); round.Move(2.34, 0, 0); round.Move(2.42, .08, 0); round.Release(2.5, .16, 0);
            Check.Equal(3, round.PerfectCount); Check.Equal(0m, round.TotalDamageTaken);
            Check.Equal(3, round.Notes.Count); Check.Equal(3, round.Results.Count); Check.Equal(3, round.Combat.Activations.Count);
            Check.True(round.WeaponResults.All(x => round.Results.Contains(x.Source)));
            Check.Equal(30.0015m, round.Combat.TotalDamage); // Bell 8, dagger 14 * 1.5, blade 16; each at the rounded 0.6667 phrase weight.
        }

        [Test]
        public void EveryMatchingMonsterGetsAllSupportedWeaponsWithoutReservingAnySlot()
        {
            foreach (int count in new[] { 2, 3, 4, 5, 6 })
            {
                var stage = Stage();
                var plan = BattlePlanner.Resolve(stage, Enumerable.Range(0, count).Select(i =>
                    Proposal(stage, "target-" + i, new[] { new PatternStep(GestureKind.Tap, 0) }, new[] { 16 })), 1);
                var loadout = Loadout(plan, "sword", "spear", "hammer", "sword", "spear");
                Check.Equal(count, loadout.Patterns.Count);
                foreach (var pattern in loadout.Patterns) Check.Equal(5, loadout.RespondingSlots(pattern).Count);
                var round = Round(plan, loadout, 100000); round.Press(2, 0, 0);
                Check.Equal(count, round.PerfectCount); Check.Equal(count * 5, round.Combat.Activations.Count);
                Check.Equal(count * 180m, round.Combat.TotalDamage);
                Check.Equal(count * 5, round.Combat.Bindings.Select(b => (b.Note, b.Slot)).Distinct().Count());
            }
        }

        [Test]
        public void LoadoutAndActiveRoundSnapshotEquipmentAndUnlockedActions()
        {
            var plan = Plan(new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Flick, 4));
            var equipment = new[] { new WeaponState("sword") }; var loadout = new WeaponLoadout(plan, equipment);
            equipment[0] = new WeaponState("sword", WeaponRarity.Legendary, 3); Check.Equal(0, loadout.Equipment[0].Level);
            var round = Round(plan, loadout);
            Check.False(ReferenceEquals(loadout.Equipment[0], round.Combat.Loadout.Equipment[0]));
            Check.Equal(1, round.Combat.Bindings.Count); Check.Equal(0, round.Combat.Loadout.Equipment[0].Level); Check.Equal(WeaponRarity.Common, round.Combat.Loadout.Equipment[0].Rarity);
            Perform(round, plan.Attacks[0].Placement.Pattern.Steps, 2); Check.Equal(12m, round.Combat.TotalDamage);
            var replay = Round(plan, new WeaponLoadout(plan, equipment));
            Check.Equal(2, replay.Combat.Bindings.Count); Perform(replay, plan.Attacks[0].Placement.Pattern.Steps, 2);
            Check.Equal(52.5m, replay.Combat.TotalDamage);
        }

        [Test]
        public void OverlappingSourcePhrasesAreRemovedBeforeBindingWeaponActions()
        {
            var plan = Plan(new[] { new PatternStep(GestureKind.Flick, 0) }, new[] { 16, 18 });
            Check.Equal(1, plan.Attacks.Count); Check.True(plan.Withdrawals.Any(x => x.IsSelfConflict));
            var loadout = Loadout(plan, "blade"); var source = plan.Attacks[0];
            var round = Round(plan, loadout); Check.Equal(1, round.Notes.Count); Check.Equal(1, round.Combat.Bindings.Count);
        }

        [Test]
        public void EachSupportedActionTriggersOnceAndDaggerComboResetsOnItsOwnMiss()
        {
            var step = new PatternStep(GestureKind.Tap, 0);
            var plan = Plan(new[] { step }, new[] { 16, 36, 56, 76 });
            var loadout = new WeaponLoadout(plan, new[] { new WeaponState("dagger", WeaponRarity.Rare, 1) }); var round = Round(plan, loadout);
            Perform(round, new[] { step }, 2); Perform(round, new[] { step }, 4.5);
            Check.Equal(30m, round.Combat.TotalDamage);
            round.Advance(7.2); Perform(round, new[] { step }, 9.5);
            Check.Equal(42.5m, round.Combat.TotalDamage); Check.Equal(1, round.MissCount);
            Check.Equal(3, round.Combat.Activations.Count);
        }

        [Test]
        public void ShieldAbsorbsOnlyAfterActivationAndExpiresOnSongTime()
        {
            var plan = Plan(new PatternStep(GestureKind.Hold, 0, 4), new PatternStep(GestureKind.Tap, 8));
            var loadout = Loadout(plan, "shield"); var round = Round(plan, loadout); round.Press(2, 0, 0); round.Release(2.5, 0, 0); round.Advance(3.2);
            Check.Equal(0m, round.TotalDamageTaken); Check.Equal(4m, round.Combat.TotalBlocked); Check.Equal(8m, round.Combat.GuardAt(3.2));
            // A fully absorbed hit uses a shield effect, never a damage stop or shake.
            Check.True(!BattleHitFeedback.Player(round, round.Results.Last().JudgedAtSeconds).Active);
            Check.Equal(0m, round.Combat.GuardAt(4.51));
            round.Suspend(); round.Advance(100); Check.Equal(3.2, round.ElapsedSeconds); Check.Equal(8m, round.Combat.GuardAt(round.ElapsedSeconds));
        }

        [Test]
        public void LongFrameResolvesEarlierIncomingDamageBeforeALaterShield()
        {
            var plan = Plan(new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Hold, 2, 4));
            var loadout = Loadout(plan, "shield"); var round = Round(plan, loadout); round.Press(2.25, 0, 0); round.Advance(3);
            Check.Equal(4m, round.TotalDamageTaken); Check.Equal(0m, round.Combat.TotalBlocked);
            Check.Equal(12m, round.Combat.GuardAt(3));
        }

        [Test]
        public void SharedHealthVictoryStopsNewPhrasesButAllowsTheActiveFinalInputAndFinale()
        {
            var plan = Plan(new[] { new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 4) }, new[] { 16, 48 });
            var loadout = Loadout(plan, "sword"); var round = Round(plan, loadout, 8); round.Press(2, 0, 0); round.Release(2.001, 0, 0);
            Check.True(round.Combat.Victory); Check.False(round.Finished); Check.Equal(0m, round.Combat.EnemyHealth.Current);
            round.Press(2.5, 0, 0); round.Release(2.501, 0, 0);
            Check.True(round.Combat.FinaleSuccess); Check.Equal(24m, round.Combat.TotalDamage);
            round.Advance(100); Check.True(round.Finished); Check.False(round.Aborted);
            Check.Equal(2, round.Results.Count); Check.Equal(0, round.MissCount); Check.Equal(0m, round.TotalDamageTaken);
        }

        [Test]
        public void MissingTheOverkillFinalInputStillWinsWithoutIncomingDamage()
        {
            var plan = Plan(new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 4));
            var loadout = Loadout(plan, "sword"); var round = Round(plan, loadout, 8); round.Press(2, 0, 0); round.Release(2.001, 0, 0); round.Advance(100);
            Check.True(round.Combat.Victory); Check.False(round.Combat.FinaleSuccess); Check.True(round.Finished);
            Check.Equal(1, round.MissCount); Check.Equal(0m, round.TotalDamageTaken);
        }

        [Test]
        public void EarlierMissDoesNotDisqualifyTheSuccessfulFinale()
        {
            var plan = Plan(new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 4));
            var loadout = Loadout(plan, "sword"); var round = Round(plan, loadout, 8); round.Press(2.5, 0, 0); round.Release(2.501, 0, 0);
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
            var loadout = Loadout(plan, "spear"); var round = Round(plan, loadout, 20); round.Press(2, 0, 0); round.Release(2.001, 0, 0);
            Check.True(round.Combat.Victory); Check.False(round.Combat.FinaleSuccess);
            round.Press(3, 0, 0); round.Release(3.001, 0, 0);
            Check.True(round.Combat.FinaleSuccess); Check.Equal(3, round.PerfectCount);
            Check.Equal(80m, round.Combat.TotalDamage); Check.Equal(0m, round.TotalDamageTaken);
        }

        [Test]
        public void PracticeCannotChangeRunHealthEnemyHealthRewardsOrEquipment()
        {
            var run = Session(); var plan = run.BattlePlan;
            string ticket = run.StageTicket; decimal health = run.Health, enemy = run.EnemyHealth.Current; int gold = run.Gold;
            var weapons = run.Weapons.Select(w => (w.DefinitionId, w.Rarity, w.Level)).ToArray();
            var practice = WeaponPractice.Create(plan, run.BattleLoadout, plan.Attacks[0], run.EnemyHealth.Maximum, run.MaxHealth);
            Check.Equal(plan.Attacks[0].Placement.Pattern.Steps.Count, practice.Notes.Count);
            Check.True(practice.Combat.Bindings.All(x => practice.Notes.Contains(x.Note)));
            practice.Advance(100);
            Check.True(practice.Combat.IsPractice); Check.True(practice.TotalDamageTaken > 0);
            Check.Equal(health, run.Health); Check.Equal(enemy, run.EnemyHealth.Current); Check.Equal(gold, run.Gold);
            Check.Equal(RunPhase.Stage, run.Phase); Check.Equal(ticket, run.StageTicket); Check.Equal(0, run.Offers.Count);
            Check.True(run.ActiveRhythmRound == null); Check.True(weapons.SequenceEqual(run.Weapons.Select(w => (w.DefinitionId, w.Rarity, w.Level))));
            Check.Equal(1, practice.Plan.Attacks.Count); Check.Equal(plan.Stage.Music.Bpm, practice.Plan.Stage.Music.Bpm);
        }

        [Test]
        public void RepeatedPracticeClearsInputDamageAndWeaponStateWithoutChangingEquipment()
        {
            var plan = Plan(new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 4));
            var loadout = Loadout(plan, "sword"); var round = new RhythmRound(plan, combat: new WeaponBattle(loadout, new StageHealth(1), 1, 1, true));
            for (int cycle = 0; cycle < 100; cycle++)
            {
                Check.Equal(0, round.Results.Count); Check.Equal(0, round.Combat.Activations.Count);
                Check.Equal(0, round.Calls.Count); Check.Equal(0.0, round.ElapsedSeconds);
                Check.Equal(1m, round.Combat.EnemyHealth.Current); Check.Equal(1m, round.Combat.PlayerHealth);
                Check.False(round.IsDown); Check.False(round.Finished);
                round.Press(round.Notes[0].StartSeconds, 0, 0);
                Check.Equal(1, round.PerfectCount); Check.Equal(0m, round.Combat.EnemyHealth.Current);
                Check.False(round.Combat.Victory); Check.False(round.Finished);
                var old = round; old.Suspend(); round = old.RepeatPractice();
                Check.True(ReferenceEquals(plan, round.Plan));
                Check.False(ReferenceEquals(old.Notes[0], round.Notes[0]));
                Check.Equal(old.Combat.Loadout.Equipment[0].Level, round.Combat.Loadout.Equipment[0].Level);
                Check.True(round.Combat.Bindings.All(x => round.Notes.Contains(x.Note)));
                round.Advance(plan.Stage.Music.DurationSeconds + 1);
                Check.Equal(0m, round.Combat.PlayerHealth); Check.True(round.Finished);
                round = round.RepeatPractice();
            }
        }

        [Test]
        public void AutomaticPracticeRepeatPreservesContactBeforeFinishingTheOldRound()
        {
            var plan = Plan(new PatternStep(GestureKind.Tap, 0));
            var loadout = Loadout(plan, "sword");             foreach (double loops in new[] { 1.0, 3.0 })
            {
                var previous = new RhythmRound(plan, combat: new WeaponBattle(loadout, new StageHealth(100), 100, 100, true));
                previous.Press(previous.Notes[0].StartSeconds, 0, 0); previous.Release(previous.ElapsedSeconds + .001, 0, 0);
                Check.True(previous.Combat.Activations.Count > 0);
                double duration = plan.Stage.Music.DurationSeconds;
                previous.Press(duration - .3, .4, .6); previous.Move(duration - .01, .4, .6);
                var next = previous.RepeatPractice(loops * duration);
                previous.Advance(duration + previous.HalfMissWindow + .001);
                Check.True(previous.Finished); Check.False(previous.IsDown);
                next.Move(.01, .4, .6);
                Check.True(next.IsDown); Check.True(next.FreeInput.IsHeld);
                Check.Equal(GestureKind.Hold, next.FreeInput.Kind.Value);
                Check.Equal(0, next.Results.Count); Check.Equal(0, next.Calls.Count); Check.Equal(0, next.Combat.Activations.Count);
                Check.Equal(100m, next.Combat.PlayerHealth); Check.Equal(100m, next.Combat.EnemyHealth.Current);
                Check.Equal(loadout.Equipment[0].Level, next.Combat.Loadout.Equipment[0].Level);
                Check.True(next.Combat.Bindings.All(x => next.Notes.Contains(x.Note)));
                next.Release(.02, .4, .6); Check.False(next.IsDown);
                Check.Equal(GestureKind.Hold, next.FreeInput.Kind.Value);
                next.Press(next.Notes[0].StartSeconds, 0, 0); Check.Equal(1, next.PerfectCount);
            }
        }

        [Test]
        public void RealEnemyHealthPersistsWhenReturningToPreparationAndResetsForNewStage()
        {
            var run = Session(); var round = run.StartRhythmRound();
            var binding = round.Combat.Bindings.Where(b => b.Action.Damage > 0).OrderBy(b => b.Note.StartSeconds).First();
            PerformNote(round, binding.Note); Check.True(round.Combat.TotalDamage > 0);
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
            foreach (WeaponAttackStyle kind in Enum.GetValues(typeof(WeaponAttackStyle)))
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
            Check.Equal(Enum.GetValues(typeof(WeaponAttackStyle)).Length, styles.Count);
        }

        [Test]
        public void EveryAuthoredPatternUsesEveryUnlockedResponseWithoutChangingMonsterNotes()
        {
            foreach (var monster in MonsterCatalog.All)
            foreach (var pattern in monster.Patterns)
            foreach (var weapon in WeaponCatalog.All)
            foreach (var rarity in WeaponRarities.All)
            for (int level = 0; level <= 3; level++)
            {
                var preview = new MonsterPreview(monster, pattern); var plan = preview.Round.Plan;
                var loadout = new WeaponLoadout(plan, new[] { new WeaponState(weapon.Id, rarity, level) }); var round = Round(plan, loadout);
                var supported = new HashSet<GestureKind>(weapon.ActionsAt(rarity).Select(a => a.Kind));
                var expected = preview.Round.Notes.Where(n => supported.Contains(n.Step.Kind)).ToArray();
                Check.Equal(preview.Round.Notes.Count, round.Notes.Count); Check.Equal(expected.Length, round.Combat.Bindings.Count);
                Check.Equal(expected.Length, round.Combat.Bindings.Select(b => b.Note).Distinct().Count());
                Check.True(round.Combat.Bindings.All(b => supported.Contains(b.Note.Step.Kind) && b.Action.Kind == b.Note.Step.Kind));
                Check.True(round.Notes.Select(n => (n.StartTick, n.EndTick, n.Step.Kind)).SequenceEqual(preview.Round.Notes.Select(n => (n.StartTick, n.EndTick, n.Step.Kind))));
            }
        }

        [Test]
        public void SustainsOfAnyLengthKeepTheirEndTimeAndActivateOnlyOnCompletion()
        {
            foreach (var weapon in WeaponCatalog.All)
            foreach (var action in weapon.Actions.Where(a => a.Kind == GestureKind.Hold || a.Kind == GestureKind.Dive))
            foreach (int duration in new[] { 1, 2, 3, 4, 6, 8, 12, 16, 24 })
            {
                var step = new PatternStep(action.Kind, 0, duration);
                var plan = Plan(step); var loadout = new WeaponLoadout(plan, new[] { new WeaponState(weapon.Id, WeaponRarity.Legendary, 3) });
                var round = Round(plan, loadout); var note = round.Notes.Single();
                Check.Equal(duration, note.EndTick - note.StartTick);
                round.Press(note.StartSeconds, 0, 0); round.Advance(note.EndSeconds - .001);
                Check.Equal(0, round.Combat.Activations.Count); round.Release(note.EndSeconds, 0, 0);
                Check.Equal(1, round.PerfectCount); Check.Equal(1, round.Combat.Activations.Count);
                Check.Equal((action.Damage + action.PerfectBonus) * 1.75m * plan.Attacks[0].JudgmentWeight, round.Combat.TotalDamage);
                Check.Equal(action.Guard * 1.75m * plan.Attacks[0].JudgmentWeight, round.Combat.GuardAt(note.EndSeconds));
                var failed = Round(plan, loadout); failed.Advance(note.EndSeconds + .2);
                Check.Equal(0, failed.Combat.Activations.Count);
            }
        }

        [Test]
        public void MaximumShieldAnswersBothCoincidentActionsAndPracticeKeepsBoth()
        {
            var plan = Plan(new PatternStep(GestureKind.Hold, 0, 8), new PatternStep(GestureKind.Shake, 0));
            foreach (var rarity in WeaponRarities.All)
            for (int level = 0; level <= 3; level++)
            {
                var loadout = new WeaponLoadout(plan, new[] { new WeaponState("shield", rarity, level) });
                var practice = WeaponPractice.Create(plan, loadout, plan.Attacks[0], 1000, 100);
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    Check.Equal(rarity == WeaponRarity.Legendary ? 2 : 1, practice.Combat.Bindings.Count);
                    Check.True(practice.Combat.Bindings.Any(b => b.Action.Kind == GestureKind.Hold));
                    Check.Equal(rarity == WeaponRarity.Legendary, practice.Combat.Bindings.Any(b => b.Action.Kind == GestureKind.Shake));
                    Check.Equal(1, practice.Combat.Loadout.RespondingSlots(practice.Plan.Attacks[0]).Count);
                    Check.True(practice.Combat.Bindings.All(b => practice.Notes.Contains(b.Note)));
                    practice = practice.RepeatPractice();
                }
            }
        }

        [Test]
        public void OneBinaryShakeAppliesEveryUnlockedEffectInOrderAndOutwardMotionAloneAppliesNone()
        {
            var plan = Plan(new PatternStep(GestureKind.Shake, 0));
            var loadout = new WeaponLoadout(plan, new[] { new WeaponState("bell"), new WeaponState("blade", WeaponRarity.Rare, 1), new WeaponState("shield", WeaponRarity.Legendary, 3) });
            var round = Round(plan, loadout);
            round.Press(2, 0, 0); round.Move(2.04, .1, 0); round.Move(2.08, 0, 0);
            Check.Equal(1, round.PerfectCount); Check.Equal(0, round.HalfMissCount);
            Check.Equal(89m, round.Combat.TotalDamage); Check.Equal(21m, round.Combat.GuardAt(2.08));
            Check.True(round.Combat.Activations.Select(a => a.Slot).SequenceEqual(new[] { 2, 0, 1 }));
            Check.True(round.WeaponResults.All(r => ReferenceEquals(r.Source, round.Results.Single())));
            var failed = Round(plan, loadout);
            failed.Press(2, 0, 0); failed.Move(2.04, .1, 0); failed.Advance(2.121);
            Check.Equal(1, failed.MissCount); Check.Equal(0, failed.HalfMissCount);
            Check.Equal(3, failed.WeaponResults.Count); Check.Equal(0, failed.Combat.Activations.Count);
            Check.Equal(8m, failed.TotalDamageTaken);
        }

        [Test]
        public void SiblingPatternsAllUseTheSameEquippedWeaponsAndRemainStableInPractice()
        {
            var stage = Stage(); var monster = MonsterCatalog.All.Single(m => m.Id == "tap-slime");
            var choices = monster.Patterns.Take(2).Select((p, i) => stage.FindPlacements(p.Pattern).Single(x => x.StartTick == 16 + i * 32));
            var plan = BattlePlanner.Resolve(stage, new[] { new MonsterProposal("slime", monster, choices) }, 1);
            var loadout = Loadout(plan, "sword", "spear", "hammer", "sword", "spear");
            Check.Equal(2, loadout.Patterns.Count);
            foreach (var selected in loadout.Patterns)
            {
                var slots = loadout.RespondingSlots(selected).ToArray(); Check.Equal(5, slots.Length);
                var practice = WeaponPractice.Create(plan, loadout, selected, 10000, 100);
                Check.Equal(practice.Notes.Count * 5, practice.Combat.Bindings.Count);
                Perform(practice, selected.Placement.Pattern.Steps, practice.Notes[0].StartSeconds);
                Check.True(practice.Combat.Activations.Count > 0);
                Check.True(slots.SequenceEqual(loadout.RespondingSlots(selected)));
            }
        }

        [Test]
        public void LockedAndUnsupportedActionsAreNotShownOrBoundAndMixedPatternsShowEachSlotOnce()
        {
            var stage = Stage();
            var plan = BattlePlanner.Resolve(stage, new[] {
                Proposal(stage, "tap", new[] { new PatternStep(GestureKind.Tap, 0) }, new[] { 16 }),
                Proposal(stage, "flick", new[] { new PatternStep(GestureKind.Flick, 0) }, new[] { 48 }) }, 1);
            var loadout = new WeaponLoadout(plan, new[] { new WeaponState("sword", WeaponRarity.Rare, 1), new WeaponState("spear"), new WeaponState("shield") });
            Check.True(loadout.RespondingSlots(loadout.Patterns.First(p => p.MonsterId == "tap")).SequenceEqual(new[] { 0, 1 }));
            Check.True(loadout.RespondingSlots(loadout.Patterns.First(p => p.MonsterId == "flick")).SequenceEqual(new[] { 0 }));
            Check.Equal(3, Round(plan, loadout).Combat.Bindings.Count);
            var mixed = Plan(new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 4), new PatternStep(GestureKind.Flick, 8));
            var one = new WeaponLoadout(mixed, new[] { new WeaponState("sword", WeaponRarity.Rare, 1) });
            Check.Equal(1, one.RespondingSlots(one.Patterns[0]).Count); Check.Equal(3, Round(mixed, one).Combat.Bindings.Count);
        }

        [Test]
        public void UnsupportedEquipmentStillAllowsEveryMonsterInputAndPractice()
        {
            var plan = Plan(new PatternStep(GestureKind.Flick, 0)); var loadout = Loadout(plan, "shield");
            Check.Equal(0, loadout.RespondingSlots(loadout.Patterns[0]).Count);
            var round = Round(plan, loadout); Perform(round, plan.Attacks[0].Placement.Pattern.Steps, 2);
            Check.Equal(1, round.PerfectCount); Check.Equal(0, round.Combat.Bindings.Count); Check.Equal(0m, round.Combat.TotalDamage);
            var practice = WeaponPractice.Create(plan, loadout, plan.Attacks[0], 1000, 100);
            Check.Equal(1, practice.Notes.Count); Check.Equal(0, practice.Combat.Bindings.Count);
            Check.False(loadout.RespondsTo(-1, loadout.Patterns[0])); Check.False(loadout.RespondsTo(5, loadout.Patterns[0]));
        }

        private static void PerformNote(RhythmRound round, ResponseNote note)
        {
            double at = note.StartSeconds;
            if (note.Step.Kind == GestureKind.Flick)
            { round.Press(at - .08, 0, 0); round.Move(at - .02, .1, 0); round.Release(at, .2, 0); }
            else
            {
                round.Press(at, 0, 0);
                if (note.Step.Kind == GestureKind.Shake) { round.Move(at + .03, .1, 0); round.Move(at + .06, 0, 0); }
                else if (note.Step.Kind == GestureKind.Hold || note.Step.Kind == GestureKind.Dive) round.Release(note.EndSeconds, 0, 0);
                else round.Release(at + .001, 0, 0);
            }
        }

        private static RunSession Session()
        {
            var run = new RunSession(73, new RunRules(startingHealth: 10000));
            Check.True(run.Enter(run.Map.Nodes.First(x => run.CanEnter(x.Id)).Id)); return run;
        }
        private static RhythmRound Round(BattlePlan plan, WeaponLoadout loadout, decimal health = 1000) =>
            new RhythmRound(plan, combat: new WeaponBattle(loadout, new StageHealth(health), 100, 100));
        private static WeaponLoadout Loadout(BattlePlan plan, params string[] ids) =>
            new WeaponLoadout(plan, ids.Select(id => new WeaponState(id)).ToArray());
        private static BattlePlan Plan(params PatternStep[] steps) => Plan(steps, new[] { 16 });
        private static BattlePlan Plan(PatternStep[] steps, int[] starts)
        {
            var stage = Stage(); return BattlePlanner.Resolve(stage, new[] { Proposal(stage, "target", steps, starts) }, 1);
        }
        private static MonsterProposal Proposal(MusicStage stage, string id, PatternStep[] steps, int[] starts)
        {
            var pattern = new RhythmPattern(id, 4, steps);
            var monster = new MonsterDefinition(id, id, "", pattern, new[] { new CallSignal(0, "CALL") }, Math.Max(12, pattern.EndOffsetTick), 4, 1);
            return new MonsterProposal(id, monster, stage.FindPlacements(pattern).Where(p => starts.Contains(p.StartTick)));
        }
        private static MusicStage Stage()
        {
            var slots = new List<SlotTemplate>();
            for (int tick = 0; tick < 16; tick++)
            {
                slots.Add(new SlotTemplate(GestureKind.Tap, tick)); slots.Add(new SlotTemplate(GestureKind.Flick, tick));
                slots.Add(new SlotTemplate(GestureKind.Shake, tick));
                foreach (var kind in new[] { GestureKind.Hold, GestureKind.Dive })
                    foreach (int duration in new[] { 1, 2, 3, 4, 6, 8, 12, 16, 24 }) slots.Add(new SlotTemplate(kind, tick, duration));
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
                        round.Press(at - .04, 0, 0); round.Move(at, .1, 0); round.Move(at + .04, 0, 0); round.Release(at + .04, 0, 0); break;
                }
            }
        }
    }
}
