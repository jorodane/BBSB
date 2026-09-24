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
    public sealed class BattleAdjustmentsTests
    {
        private static WeaponNoteBinding Part(int id, string part, int note) => new WeaponNoteBinding(new NotePartState(id, part), note);
        private static FiveLaneBattle Play(WeaponState weapon, WeaponPhrase phrase = null) => new FiveLaneBattle(
            new[] { weapon }, 120, 32, Array.Empty<BeatAttack>(), new StageHealth(10000), 80, 100,
            phrases: phrase == null ? null : new[] { phrase });

        [Test] public void CountInUsesFourConfigurableWordsWithTheLastWordAtSongZero()
        {
            var count = new BattleCountIn();
            Check.Equal("", count.WordAt(-3.001));
            Check.Equal("Beat", count.WordAt(-3)); Check.Equal("Block", count.WordAt(-2));
            Check.Equal("Shake", count.WordAt(-1)); Check.Equal("Shake", count.WordAt(-.001));
            Check.Equal(3, count.IndexAt(0)); Check.Equal("Beat", count.WordAt(0));
            Check.Equal("Beat", count.WordAt(.999)); Check.Equal("", count.WordAt(1));
            var words = new[] { "Bounce", "Block", "Swing", "Begin" };
            var custom = new BattleCountIn(words); words[3] = "changed";
            Check.Equal("Begin", custom.WordAt(0)); Check.Equal("Bounce", custom.WordAt(-3));
        }

        [Test] public void AttackWindowsProtectThreeBeatsAndThreeSecondsAtEveryTempoAndLoop()
        {
            foreach (double bpm in new[] { 60.0, 120, 173, 240 })
            {
                var window = new MonsterAttackWindow(32, bpm);
                double end = 32 - 3 * bpm / 60;
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    double start = cycle * 32;
                    foreach (double offset in new[] { 0.0, .5, 1, 2.5, 2.999, end, 31.999 })
                        Check.Equal(null, window.Place(new BeatAttack("enemy", 0, 10), start + offset));
                    Check.True(window.Place(new BeatAttack("enemy", 0, 10), start + 3) != null);
                    var hold = window.Place(new BeatAttack("enemy", 0, 20, 10), start + end - 2);
                    Check.True(hold.EndBeat < start + end);
                    Check.Equal(1.5, hold.EndBeat - hold.Beat);
                    Check.Equal(3m, hold.DamageBudget);
                }
            }
            Check.Equal(null, new MonsterAttackWindow(4, 120).Place(new BeatAttack("short", 0, 10), 3));
        }

        [Test] public void ProtectedAttacksNeverEnterForecastOrDealDamageAcrossLongFrames()
        {
            FiveLaneBattle Make() => new FiveLaneBattle(new[] { new WeaponState("dagger") }, 120, 16,
                new[] { new BeatAttack("opening", 0, 100), new BeatAttack("edge", 3, 5),
                    new BeatAttack("tail", 8, 40, 8), new BeatAttack("outro", 10, 100) },
                new StageHealth(10000), 10000, 10000, attackWindow: new MonsterAttackWindow(16, 120));
            var fast = Make(); var slow = Make();
            Check.True(fast.Incoming.All(a => a.Definition.MonsterId != "opening" && a.Definition.MonsterId != "outro"));
            fast.Advance(48);
            for (int step = 1; step <= 192; step++) slow.Advance(step * .25);
            Check.Equal(9962.5m, fast.PlayerHealth); Check.Equal(fast.PlayerHealth, slow.PlayerHealth);
            Check.True(fast.Incoming.All(a => a.Beat % 16 >= 3 && a.EndBeat % 16 < 10));
        }

        [Test] public void RealRunStartsTheFrontPatternAfterThreeBeatsAndResumingKeepsItsClock()
        {
            var run = new RunSession(31, useFiveLaneCombat: true);
            Check.True(run.Enter(run.Map.Nodes.First(n => run.CanEnter(n.Id)).Id));
            var battle = run.StartFiveLaneBattle();
            Check.True(battle.AttackWindow != null); Check.True(battle.Formation != null);
            Check.True(battle.Incoming.All(a => a.Beat >= 3));
            battle.Advance(2.99); Check.Equal(100m, battle.PlayerHealth);
            Check.True(battle.Incoming.Any()); Check.True(battle.Incoming.All(a => a.Beat >= 3));
            var attacks = battle.Incoming.ToArray(); battle.Pause();
            Check.True(ReferenceEquals(battle, run.StartFiveLaneBattle())); Check.Equal(2.99, battle.Beat);
            Check.True(attacks.All(a => battle.Incoming.Contains(a)));
        }

        [Test] public void CrownCoversAnOccupiedBeatAndKeepsEveryDamageEffectAndSocket()
        {
            var weapon = new WeaponState("sword");
            var original = new WeaponPhrase("sword", "crown", "", 2, new[] {
                new WeaponPhraseNote(0, 10), new WeaponPhraseNote(.25, 20, prerequisite: 0, condition: PhraseNoteCondition.Hit),
                new WeaponPhraseNote(.5, 30, prerequisite: 1, condition: PhraseNoteCondition.Hit) });
            weapon.SetNoteBindings(new[] { Part(1, "inject-hold", 0), Part(2, "frame-mend", 1), Part(3, "frame-pierce", 2) });
            var composed = WeaponNoteAssembly.Apply(weapon, WeaponPhraseSet.Uniform(weapon, original)).LightStarts[0];
            Check.Equal(3, composed.Notes.Count); Check.False(original.Notes[0].IsHold);
            Check.Equal(.25, composed.Notes[0].HoldBeats); Check.Equal(.25, composed.Notes[1].HoldBeats);
            Check.True(composed.Notes[1].ConnectFromPrevious); Check.True(composed.Notes[2].ConnectFromPrevious);
            Check.Equal(1, composed.Notes[2].Prerequisite); Check.Equal(WeaponAttackTarget.Rear, composed.Notes[2].Target);
            Check.Equal(2, composed.Notes[2].BaseNoteIndex);
            var battle = Play(weapon, original); battle.Press(0, 0); battle.Advance(.5);
            Check.Equal(64m, battle.TotalDamage); Check.Equal(82m, battle.PlayerHealth);
            Check.Equal(0, battle.MissCount); Check.Equal(3, battle.PerfectCount);
        }

        [Test] public void ConsecutiveCrownsJoinWithoutRepressAndEarlyReleaseBreaksTheRemainingHold()
        {
            var weapon = new WeaponState("rapier");
            weapon.SetNoteBindings(new[] { Part(1, "inject-hold", 0), Part(2, "inject-hold", 1) });
            var battle = Play(weapon); battle.Press(0, 0); battle.Release(0, 0); battle.Press(0, 1); battle.Advance(1.5);
            Check.True(battle.Lanes[0].Holding); Check.Equal(2.0, battle.Lanes[0].HoldEndBeat);
            var timeline = new FiveLaneNoteTimeline(); timeline.Refresh(battle);
            Check.True(timeline.Notes.Any(n => n.Definition.ConnectFromPrevious && n.EndBeat == 2));
            battle.Pause(); battle.Advance(10); Check.Equal(1.5, battle.Beat);
            Check.Equal(0, battle.RequiredHeldSlots.Single()); battle.Resume(); battle.Release(0, 2);
            Check.Equal(22m, battle.TotalDamage); Check.Equal(0, battle.MissCount);
            var broken = Play(weapon); broken.Press(0, 0); broken.Release(0, 0); broken.Press(0, 1); broken.Release(0, 1.75);
            Check.Equal(1, broken.MissCount); Check.Equal(8.4m, broken.TotalDamage);
            Check.False(broken.Lanes[0].Holding); Check.False(broken.Lanes[0].CanRepeat);
        }

        [Test] public void EarlierMissStillAllowsTheLaterConnectedHoldsToBePlayedContinuously()
        {
            var weapon = new WeaponState("sword");
            var phrase = new WeaponPhrase("sword", "recovery", "", 4, new[] {
                new WeaponPhraseNote(0, 1), new WeaponPhraseNote(1, 1),
                new WeaponPhraseNote(2, 10), new WeaponPhraseNote(2.5, 10) });
            weapon.SetNoteBindings(new[] { Part(1, "inject-hold", 2), Part(2, "inject-hold", 3) });
            var battle = Play(weapon, phrase); battle.Press(0, 0); battle.Release(0, 0);
            battle.Advance(1.3); Check.Equal(1, battle.MissCount);
            battle.Press(0, 2); battle.Release(0, 3);
            Check.Equal(29m, battle.TotalDamage); Check.Equal(1, battle.MissCount);
            Check.Equal(3, battle.PerfectCount);
        }

        [Test] public void TouchingAuthoredHoldsAndRepeatBoundariesUseOneContinuousContact()
        {
            var phrase = new WeaponPhrase("dagger", "joined", "", 1, new[] {
                new WeaponPhraseNote(0, 3, .5), new WeaponPhraseNote(.5, 4, .5) }, repeat: true);
            var battle = Play(new WeaponState("dagger"), phrase);
            battle.Press(0, 0); battle.Advance(3.25);
            Check.Equal(21m, battle.TotalDamage); Check.Equal(0, battle.MissCount);
            Check.True(battle.Lanes[0].Holding); Check.Equal(3.5, battle.Lanes[0].HoldEndBeat);
        }

        [Test] public void CrownsAllowAllOccupiedHalfBeatsAndDifferentLinesStillNeedTheirOwnInput()
        {
            foreach (var definition in WeaponCatalog.All)
            {
                var weapon = new WeaponState(definition.Id);
                var source = WeaponPhraseSet.Uniform(weapon);
                for (int i = 0; i < source.LightStarts[0].Notes.Count; i++)
                {
                    // A part must have an attack socket in every authored start variant.
                    if (source.LightStarts.Concat(source.DarkStarts).Any(p => i >= p.Notes.Count || p.Notes[i].IsHold ||
                        p.Notes[i].Effect != PhraseEffect.Strike || p.Notes[i].Damage <= 0)) continue;
                    weapon.SetNoteBindings(new[] { Part(1, "inject-hold", i) });
                    WeaponNoteAssembly.Apply(weapon, source);
                }
            }
            var chain = new WeaponState("chain-sickle"); chain.SetNoteBindings(new[] { Part(1, "inject-hold", 0), Part(2, "inject-hold", 1) });
            var battle = Play(chain); battle.Press(0, 0); battle.Release(0, 0); battle.Press(0, 1); battle.Advance(1.5);
            Check.False(battle.Lanes[0].Holding); Check.Equal(1, battle.Lanes[0].SlotForNote(battle.Lanes[0].NextNote));
            battle.Press(1, 1.5); battle.Advance(2); Check.Equal(25.2m, battle.TotalDamage); Check.Equal(0, battle.MissCount);
        }

        private static FiveLaneBattle Shields(string[] ids, BeatAttack[] attacks) => new FiveLaneBattle(
            ids.Select(id => new WeaponState(id)).ToArray(), 120, 32, attacks, new StageHealth(1000), 1000, 1000);

        [Test] public void SeparateShieldsSplitFrontAndRearJustLikeOneWideShield()
        {
            var battle = Shields(new[] { "heater-shield", "shield" }, new[] {
                new BeatAttack("front", 1, 10), new BeatAttack("rear", 1, 20, rank: AttackRank.Rear) });
            Check.Equal(0, battle.Incoming[0].ShieldSlot); Check.Equal(1, battle.Incoming[1].ShieldSlot);
            battle.Press(0, 1); battle.Press(1, 1); battle.Advance(1.25);
            Check.Equal(30m, battle.TotalBlocked); Check.Equal(1000m, battle.PlayerHealth);
        }

        [Test] public void ThreeShieldLinesRoundRobinFrontAttacksAndReserveTheRightmostForRear()
        {
            var battle = Shields(new[] { "wide-shield", "heater-shield" }, new[] {
                new BeatAttack("front-a", 1, 10), new BeatAttack("rear", 1.5, 10, rank: AttackRank.Rear),
                new BeatAttack("front-b", 2, 10, 3), new BeatAttack("front-c", 3, 10), new BeatAttack("front-d", 4, 10) });
            Check.Equal("0,2,1,0,1", string.Join(",", battle.Incoming.Select(a => a.ShieldSlot)));
            Check.Equal(AttackRank.Front, battle.ShieldAt(1).Rank);
            var routedHold = battle.Incoming.Single(a => a.Definition.IsHold);
            battle.Press(1, 2); battle.Advance(3.25);
            Check.Equal(1, routedHold.ShieldSlot); Check.Equal(0m, routedHold.DamageTaken);
            Check.False(battle.CoversAttack(battle.LaneAt(0), 0, routedHold));
            battle.Pause(); battle.Advance(100); battle.Resume(); battle.Advance(5.25);
            Check.Equal(0m, routedHold.DamageTaken); Check.Equal(1, routedHold.ShieldSlot);
        }

        [Test] public void ShieldOrderingUsesPhysicalPositionsIncludingExtensionsNotInventoryOrder()
        {
            var wide = new WeaponState("wide-shield"); var heater = new WeaponState("heater-shield");
            var battle = new FiveLaneBattle(new[] { heater, wide }, 120, 32,
                new[] { new BeatAttack("front", 1, 10), new BeatAttack("front2", 2, 10), new BeatAttack("rear", 3, 10, rank: AttackRank.Rear) },
                new StageHealth(1000), 1000, 1000,
                placements: new[] { new WeaponPlacement(heater, new[] { 4 }), new WeaponPlacement(wide, new[] { BattleInputLayout.Left, 0 }) },
                extensions: InputExtensions.All);
            Check.Equal("5,0,4", string.Join(",", battle.Incoming.Select(a => a.ShieldSlot)));
        }

        [Test] public void ShopAllowsEveryAffordablePurchaseUntilExplicitlyLeaving()
        {
            int seed = Enumerable.Range(0, 100).First(s => MapGenerator.Generate(1, new SeededRandom(s)).Nodes.Any(n => n.Row == 1 && n.Kind == StageKind.Shop));
            var run = new RunSession(seed, new RunRules(startingGold: 1000), useFiveLaneCombat: true);
            var shop = run.Map.Nodes.First(n => n.Row == 1 && n.Kind == StageKind.Shop);
            run.Enter(run.Map.Nodes.First(n => n.Row == 0 && n.Next.Contains(shop.Id)).Id);
            run.ResolveBattle(run.StageTicket, true, run.Health); run.SkipReward(); Check.True(run.Enter(shop.Id));
            int cleared = run.ClearedStages; var offers = run.Offers.ToArray();
            for (int i = 0; i < offers.Length; i++)
            {
                int expected = run.Gold - offers[i].Price;
                Check.True(run.Buy(i)); Check.False(run.Buy(i)); Check.Equal(expected, run.Gold);
                Check.Equal(RunPhase.Stage, run.Phase); Check.True(ReferenceEquals(shop, run.CurrentNode));
                Check.Equal(cleared, run.ClearedStages); Check.Equal(offers.Length, run.Offers.Count);
            }
            Check.True(run.LeaveService()); Check.Equal(RunPhase.Map, run.Phase); Check.Equal(cleared + 1, run.ClearedStages);
        }
    }
}
