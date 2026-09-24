using System;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class WeaponAttributeTests
    {
        private static void Tap(FiveLaneBattle b, double at, int slot = 0) { b.Press(slot, at); b.Release(slot, at); }
        private static FiveLaneBattle Battle(WeaponState weapon, WeaponPhraseSet patterns = null, int seed = 1) =>
            new FiveLaneBattle(new[] { weapon }, 120, 32, Array.Empty<BeatAttack>(), new StageHealth(100000), 30, 50,
                phraseSets: new[] { patterns ?? ShortWeaponPhrases.Set(weapon) }, rhythmSeed: seed);
        private static FiveLaneBattle Chaos(string id = "dagger", decimal chance = 1, int seed = 1)
        {
            var weapon = new WeaponState(id, WeaponAttribute.Chaos);
            return Battle(weapon, new WeaponPhraseSet(weapon, new[] { ShortWeaponPhrases.Find(id) },
                chaos: new ChaosRules(transitionChance: chance)), seed);
        }
        [Test] public void OpeningAlwaysSucceedsOnTheNearestAllowedAttributeGrid()
        {
            foreach (WeaponAttribute attribute in Enum.GetValues(typeof(WeaponAttribute)))
            foreach (double at in new[] { 0, .249, .25, .49, .5, .749, .75, 1.18, 1.42, 9.92 })
            {
                var b = Battle(new WeaponState("dagger", attribute)); Tap(b, at);
                double expected = attribute == WeaponAttribute.Light ? Math.Floor(at + .5) :
                    attribute == WeaponAttribute.Dark ? Math.Floor(at) + .5 : Math.Floor(at * 2 + .5) * .5;
                Check.Equal(expected + 2, b.Lanes[0].NextBeat);
                Check.Equal(1, b.PerfectCount); Check.Equal(0, b.MissCount); Check.Equal(0, b.HalfMissCount);
                Check.Equal(attribute == WeaponAttribute.Chaos ? 9m : 6m, b.TotalDamage);
                Tap(b, b.Lanes[0].NextBeat); Check.Equal(2, b.PerfectCount);
            }
        }
        [Test] public void LightAndDarkCannotChangePhaseByPlayingTheWrongNextBeat()
        {
            foreach (var attribute in new[] { WeaponAttribute.Light, WeaponAttribute.Dark })
            {
                var b = Battle(new WeaponState("dagger", attribute)); Tap(b, .49);
                Tap(b, b.Lanes[0].NextBeat - .5);
                Check.Equal(1, b.MissCount); Check.Equal(1, b.PerfectCount);
                b.Advance(b.Lanes[0].ReadyAtBeat); Tap(b, b.Beat);
                Check.Equal(2, b.PerfectCount);
                Check.Equal(attribute == WeaponAttribute.Light ? 0.0 : .5, b.Lanes[0].NextBeat % 1);
            }
        }
        [Test] public void DualSelectsEffectsByBothStartOffsetAndBeatSide()
        {
            var weapon = new WeaponState("staff", WeaponAttribute.Dual);
            WeaponPhrase Phrase(decimal amount, PhraseEffect effect) => new WeaponPhrase("staff", "test", "", 1,
                new[] { new WeaponPhraseNote(0, amount, effect: effect) });
            var set = new WeaponPhraseSet(weapon, new[] { Phrase(4, PhraseEffect.Heal), Phrase(7, PhraseEffect.Heal) },
                new[] { Phrase(10, PhraseEffect.Strike), Phrase(14, PhraseEffect.Strike) });
            for (int offset = 0; offset < 2; offset++)
            foreach (bool dark in new[] { false, true })
            {
                var b = Battle(weapon, set); decimal observed = 0; b.PlayerHealthChanged += value => observed = value;
                Tap(b, dark ? .49 : .1, offset);
                Check.Equal(offset, b.Lanes[0].StartOffset);
                Check.Equal(dark ? WeaponBeatSide.Dark : WeaponBeatSide.Light, b.Lanes[0].ActiveSide);
                Check.Equal(dark ? (offset == 0 ? 10m : 14m) : 0, b.TotalDamage);
                Check.Equal(dark ? 30m : offset == 0 ? 34m : 37m, b.PlayerHealth);
                Check.Equal(dark ? 0m : b.PlayerHealth, observed);
            }
        }
        [Test] public void HealingCapsAtMaximumAndRepeatsKeepTheChosenDualSide()
        {
            var weapon = new WeaponState("dagger", WeaponAttribute.Dual);
            var heal = new WeaponPhrase("dagger", "heal", "", 2, new[] { new WeaponPhraseNote(0, 16, effect: PhraseEffect.Heal) }, repeat: true);
            var set = new WeaponPhraseSet(weapon, new[] { heal }, new[] { ShortWeaponPhrases.Find("dagger") });
            var b = Battle(weapon, set); Tap(b, .1); Tap(b, 2.1);
            Check.Equal(50m, b.PlayerHealth); Check.Equal(20m, b.TotalHealed); Check.Equal(0m, b.TotalDamage);
            Tap(b, 3.5); Check.Equal(1, b.MissCount); b.Advance(5.5); Tap(b, 5.5);
            Check.Equal(WeaponBeatSide.Dark, b.Lanes[0].ActiveSide); Check.Equal(6m, b.TotalDamage);
        }
        [Test] public void ChaosRequiresSixActuallyMaintainedBeatsBeforeChoosingATransition()
        {
            foreach (string id in new[] { "dagger", "dual-swords" })
            {
                var b = Chaos(id); double interval = id == "dagger" ? 2 : 1;
                for (double at = 0; at < 6; at += interval)
                { Tap(b, at); Check.False(b.Lanes[0].IsTransition); }
                Tap(b, 6); Check.True(b.Lanes[0].IsTransition);
                Check.Equal(6 + interval, b.Lanes[0].StartBeat);
                Check.Equal(3, b.Lanes[0].Phrase.Notes.Count);
                double bridge = b.Lanes[0].StartBeat;
                Tap(b, bridge); Tap(b, bridge + .5); Tap(b, bridge + 1);
                Check.False(b.Lanes[0].IsTransition); Check.Equal(WeaponBeatSide.Dark, b.Lanes[0].ActiveSide);
                Check.Equal(bridge + 1.5, b.Lanes[0].NextBeat);
                double oppositeStart = b.Lanes[0].StartBeat;
                for (double at = oppositeStart; at < oppositeStart + 6; at += interval)
                { Tap(b, at); Check.False(b.Lanes[0].IsTransition); }
                Tap(b, oppositeStart + 6); Check.True(b.Lanes[0].IsTransition);
                Check.Equal(WeaponBeatSide.Dark, b.Lanes[0].ActiveSide);
            }
        }
        [Test] public void ChaosMissBreaksTheChainButKeepsRemainingTransitionNotesPlayable()
        {
            var b = Chaos(); for (double at = 0; at <= 6; at += 2) Tap(b, at);
            Tap(b, 8); b.Advance(8.75);
            Check.Equal(1, b.MissCount); Check.False(b.Lanes[0].CanRepeat);
            Check.True(b.Lanes[0].IsNoteVisible(2)); Tap(b, 9);
            Check.Equal(PhraseLanePhase.Cooldown, b.Lanes[0].Phase);
            Tap(b, 11); Check.False(b.Lanes[0].IsTransition);
            Check.Equal(1, b.Lanes[0].Cycle.Index); // First successful note schedules only the normal next cycle.
        }
        [Test] public void ChaosForecastUsesTheActualPlanWithoutRerollingOrBreakingHitNotes()
        {
            var a = Chaos("dual-swords", .5m, 137); var b = Chaos("dual-swords", .5m, 137);
            var timeline = new FiveLaneNoteTimeline();
            for (int hit = 0; hit < 40; hit++)
            {
                double at = hit == 0 ? 0 : a.Lanes[0].NextBeat;
                a.Advance(at); b.Advance(at); timeline.Refresh(a);
                var expected = timeline.Notes.FirstOrDefault(n => n.Beat == at && !n.IsPreview);
                for (int frame = 0; frame < 9; frame++) timeline.Refresh(a);
                Tap(a, at); Tap(b, at); timeline.Refresh(a);
                Check.Equal(b.Lanes[0].NextBeat, a.Lanes[0].NextBeat);
                Check.Equal(b.Lanes[0].IsTransition, a.Lanes[0].IsTransition);
                Check.Equal(b.TotalDamage, a.TotalDamage); Check.Equal(0, timeline.Broken.Count);
                Check.True(timeline.Notes.All(n => n.Beat <= a.Beat + 3));
                if (hit > 0) Check.Equal(at, expected.Beat);
                var forecast = timeline.Notes.Where(n => n.IsPreview).ToArray();
                var plan = a.Lanes[0].Cycle;
                foreach (var note in forecast)
                {
                    while (plan.StartBeat < note.CycleStart) plan = a.Lanes[0].NextCycle(plan);
                    Check.True(ReferenceEquals(note.Definition, plan.Phrase.Notes[note.Index]));
                }
            }
        }
        [Test] public void ChaosChanceIsSeededAndPauseDoesNotAccumulateMaintenance()
        {
            var firstTransitions = new System.Collections.Generic.HashSet<double>();
            for (int seed = 1; seed <= 32; seed++)
            {
                var b = Chaos("dual-swords", .25m, seed); Tap(b, 0); b.Pause(); b.Advance(100); b.Resume();
                Check.Equal(0.0, b.Beat);
                for (int at = 1; at <= 64 && !b.Lanes[0].IsTransition; at++) Tap(b, at);
                Check.True(b.Lanes[0].IsTransition); firstTransitions.Add(b.Lanes[0].StartBeat);
            }
            Check.True(firstTransitions.Count > 1);
            var never = Chaos(chance: 0);
            for (int at = 0; at <= 32; at += 2) { Tap(never, at); Check.False(never.Lanes[0].IsTransition); }
        }
        [Test] public void FourAttributesOfOneWeaponCanEquipTogetherButSameVariantCannot()
        {
            var equipment = new WeaponEquipment(); equipment.IncreaseCapacity(); equipment.IncreaseCapacity(); equipment.IncreaseCapacity();
            var weapons = Enum.GetValues(typeof(WeaponAttribute)).Cast<WeaponAttribute>().Select(a => new WeaponState("dagger", a)).ToArray();
            for (int i = 0; i < 4; i++) { equipment.Acquire(weapons[i]); Check.True(equipment.Equip(weapons[i], i)); }
            var extra = new WeaponState("dagger", WeaponRarity.Legendary, 3); equipment.Acquire(extra);
            Check.False(equipment.Equip(extra, 4)); Check.False(equipment.Swap(extra, weapons[1]));
            Check.True(equipment.Equip(extra, 0)); Check.Equal(5, equipment.Owned.Count); Check.Equal(4, equipment.Equipped.Count);
            Check.True(ReferenceEquals(extra, equipment.At(0).Weapon)); Check.True(equipment.Owned.Contains(weapons[0])); Check.Equal(3, extra.Level);
            var battle = new FiveLaneBattle(equipment.Equipped, 120, 32, Array.Empty<BeatAttack>(), new StageHealth(1000), 100, 100, placements: equipment.Placements);
            Check.Equal(4, battle.Lanes.Count); Check.Equal(WeaponAttribute.Chaos, battle.LaneAt(3).Weapon.Attribute);
        }
        [Test] public void DifferentWeaponsWithIdenticalRhythmsAreAllowedAndInvalidBattleDuplicatesAreRejected()
        {
            var a = new WeaponState("dagger"); var b = new WeaponState("dual-swords");
            var equipment = new WeaponEquipment(); equipment.Acquire(a); equipment.Acquire(b);
            Check.True(equipment.Equip(a, 0)); Check.True(equipment.Equip(b, 1));
            var p = new WeaponPhrase("dual-swords", "identical", "", 2, new[] { new WeaponPhraseNote(0, 6) }, repeat: true);
            var battle = new FiveLaneBattle(equipment.Equipped, 120, 32, Array.Empty<BeatAttack>(), new StageHealth(1000), 100, 100,
                new[] { ShortWeaponPhrases.Find("dagger"), p });
            Tap(battle, 0, 0); Tap(battle, 0, 1); Check.Equal(12m, battle.TotalDamage);
            Reject(() => new FiveLaneBattle(new[] { a, new WeaponState("dagger") }, 120, 32, Array.Empty<BeatAttack>(), new StageHealth(1000), 100, 100));
        }
        [Test] public void AttributesSurviveStarterBindingsAndRunSnapshots()
        {
            var character = new RunCharacterDefinition("dual", new[] {
                new StartingWeaponDefinition("light", "dagger", 0), new StartingWeaponDefinition("dark", "dagger", 4, WeaponAttribute.Dark) });
            var draft = new StartingLoadout(character); Check.True(draft.EquipWeaponAtCenter(1, 2));
            var saved = draft.Capture(); var run = new RunSession(3, useFiveLaneCombat: true, character: character, startingLoadout: saved);
            saved.bindings[1].attribute = WeaponAttribute.Light;
            Check.Equal(WeaponAttribute.Dark, run.Equipment.At(2).Weapon.Attribute);
            run.Abandon(); run.Restart(3); Check.Equal(WeaponAttribute.Dark, run.Equipment.At(2).Weapon.Attribute);
            Check.Equal(WeaponAttribute.Light, new StartingWeaponBinding().attribute);
        }
        [Test] public void BellUsesTheTargetsAllowedBeatAndForecastsTheSameScheduledStart()
        {
            var weapons = new[] { new WeaponState("spirit-bell"), new WeaponState("dagger", WeaponAttribute.Dark) };
            var b = new FiveLaneBattle(weapons, 120, 32, Array.Empty<BeatAttack>(), new StageHealth(1000), 100, 100);
            Tap(b, 0); Tap(b, 1); var start = b.ScheduledStarts.Single(); Check.Equal(2.5, start.Beat);
            var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(b);
            Check.Equal(2.5, timeline.Notes.Single(n => n.Slot == 1).Beat);
            b.Advance(2.5); Check.Equal(0m, b.TotalDamage); Check.Equal(WeaponBeatSide.Dark, b.LaneAt(1).ActiveSide);
            Tap(b, 2.5, 1); Check.Equal(6m, b.TotalDamage); Check.Equal(4.5, b.LaneAt(1).NextBeat);
        }
        [Test] public void ChaosRejectsNonRepeatingBasesAndBridgesThatCannotSwitchPhase()
        {
            Reject(() => WeaponPhraseSet.Uniform(new WeaponState("shield", WeaponAttribute.Chaos)));
            var weapon = new WeaponState("dagger", WeaponAttribute.Chaos); var normal = ShortWeaponPhrases.Find("dagger");
            Reject(() => new WeaponPhraseSet(weapon, new[] { normal }, lightTransitions: new[] { normal }));
            Reject(() => new ChaosRules(minimumBeats: 5)); Reject(() => new ChaosRules(effectMultiplier: 1));
        }
        [Test] public void AttributeRollsOfferChaosForAllNonShieldNonExclusiveWeapons()
        {
            foreach (var phrase in WeaponPhraseCatalog.All)
            {
                var random = new SeededRandom(98); var attributes = new System.Collections.Generic.HashSet<WeaponAttribute>();
                for (int i = 0; i < 128; i++) attributes.Add(WeaponAttributes.Roll(phrase.WeaponId, random));
                var exclusive = WeaponCatalog.Find(phrase.WeaponId).ExclusiveAttribute;
                Check.Equal(exclusive.HasValue ? 1 : WeaponAttributes.SupportsChaos(phrase.WeaponId) ? 4 : 3, attributes.Count);
                Check.Equal(exclusive.HasValue ? exclusive == WeaponAttribute.Chaos : WeaponAttributes.SupportsChaos(phrase.WeaponId),
                    attributes.Contains(WeaponAttribute.Chaos));
            }
        }
        [Test] public void ChaosScalesHealingAndGuardWithoutChangingTheirTiming()
        {
            var weapon = new WeaponState("dagger", WeaponRarity.Common, 2, attribute: WeaponAttribute.Chaos);
            var healing = new WeaponPhrase("dagger", "heal", "", 2, new[] { new WeaponPhraseNote(0, 5, effect: PhraseEffect.Heal) }, repeat: true);
            var b = Battle(weapon, WeaponPhraseSet.Uniform(weapon, healing));
            Tap(b, 0); Check.Equal(41.25m, b.PlayerHealth); Check.Equal(2.0, b.Lanes[0].NextBeat);
            Tap(b, 2.2); Check.Equal(46.875m, b.PlayerHealth); Check.Equal(1, b.HalfMissCount); Check.Equal(0m, b.TotalDamage);
            var shield = new WeaponState("heater-shield", WeaponAttribute.Chaos);
            var guard = new WeaponPhrase("heater-shield", "guard", "", 2, new[] { new WeaponPhraseNote(0, 0, 2) }, repeat: true, holdDamageReduction: .5m);
            var bridge = new WeaponPhrase("heater-shield", "bridge", "", 1.5, new[] { new WeaponPhraseNote(0, 0), new WeaponPhraseNote(.5, 0) }, repeat: true);
            var patterns = new WeaponPhraseSet(shield, new[] { guard }, lightTransitions: new[] { bridge }, darkTransitions: new[] { bridge });
            var defense = new FiveLaneBattle(new[] { shield }, 120, 32, new[] { new BeatAttack("enemy", .5, 20) }, new StageHealth(100), 100, 100, phraseSets: new[] { patterns });
            defense.Press(0, 0); defense.Advance(.75);
            Check.Equal(15m, defense.TotalReduced); Check.Equal(95m, defense.PlayerHealth); Check.True(defense.Lanes[0].Holding);
        }
        [Test] public void AnAuthoredChaosBridgeOverridesOnlyItsOwnStartingOffset()
        {
            var weapon = new WeaponState("dagger", WeaponRarity.Common, requiredLanes: 2, attribute: WeaponAttribute.Chaos);
            var normal = ShortWeaponPhrases.Find("dagger");
            var custom = new WeaponPhrase("dagger", "bridge", "", 2.5,
                new[] { new WeaponPhraseNote(0, 2), new WeaponPhraseNote(.5, 3, laneOffset: 1), new WeaponPhraseNote(2, 4) }, repeat: true);
            var patterns = new WeaponPhraseSet(weapon, new[] { normal, normal }, lightTransitions: new[] { custom, null });
            Check.True(ReferenceEquals(custom, patterns.For(0, WeaponBeatSide.Light, true)));
            Check.Equal(1.5, patterns.For(1, WeaponBeatSide.Light, true).LengthBeats);
            Check.Equal(1.5, patterns.For(0, WeaponBeatSide.Dark, true).LengthBeats);
        }
        private static void Reject(Action action)
        { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check.True(rejected); }
    }
}
