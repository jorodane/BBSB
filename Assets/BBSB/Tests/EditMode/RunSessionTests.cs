using System;
using System.Collections.Generic;
using System.Linq;
using BBSB.Core;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class RunSessionTests
    {
        [Test]
        public void ThousandMapsHaveReachableFourthStageBossAndNoDeadEnds()
        {
            for (int seed = 0; seed < 1000; seed++)
            {
                var map = MapGenerator.Generate(1, new SeededRandom(seed));
                Check.Equal(10, map.Nodes.Count);
                Check.Equal(1, map.Nodes.Count(x => x.Kind == StageKind.Boss));
                var reached = new HashSet<string>(map.Nodes.Where(x => x.Row == 0).Select(x => x.Id));
                foreach (var node in map.Nodes)
                {
                    Check.True(reached.Contains(node.Id), "Unreachable node at seed " + seed);
                    if (node.Row == 3) { Check.Equal(StageKind.Boss, node.Kind); Check.Equal(0, node.Next.Count); }
                    else
                    {
                        Check.True(node.Next.Count > 0);
                        foreach (var id in node.Next)
                        {
                            Check.Equal(node.Row + 1, map.Find(id).Row);
                            reached.Add(id);
                        }
                    }
                }
                foreach (StageKind kind in Enum.GetValues(typeof(StageKind))) Check.True(map.Nodes.Any(x => x.Kind == kind));
            }
        }

        [Test]
        public void SeedsReproduceMapsAndDifferentSeedsVaryThem()
        {
            string a = Fingerprint(MapGenerator.Generate(1, new SeededRandom(832)));
            Check.Equal(a, Fingerprint(MapGenerator.Generate(1, new SeededRandom(832))));
            Check.True(a != Fingerprint(MapGenerator.Generate(1, new SeededRandom(833))));
        }

        [Test]
        public void OnlyConnectedNextRowCanBeEntered()
        {
            var run = new RunSession(1);
            Check.False(run.Enter("missing"));
            Check.False(run.Enter(run.Map.Nodes.First(x => x.Row == 1).Id));
            var start = run.Map.Nodes.First(x => x.Kind == StageKind.Monster && x.Row == 0);
            Check.True(run.Enter(start.Id));
            Check.False(run.Enter(start.Id));
            Win(run); Check.False(run.Enter(start.Next[0])); // Must resolve reward first.
            run.SkipReward();
            foreach (var node in run.Map.Nodes)
                Check.Equal(start.Next.Contains(node.Id), run.CanEnter(node.Id));
        }

        [Test]
        public void BattleTicketRejectsDuplicateAndStaleResults()
        {
            var run = Stage(StageKind.Monster);
            string ticket = run.StageTicket; int gold = run.Gold;
            Check.False(run.ResolveBattle("wrong", true, run.Health));
            Check.False(run.ResolveBattle(ticket, true, run.MaxHealth + 1));
            Check.False(run.ResolveBattle(ticket, true, -1));
            Check.True(run.ResolveBattle(ticket, true, 80));
            Check.Equal(gold + 25, run.Gold);
            Check.False(run.ResolveBattle(ticket, true, 80));
            run.Restart(run.Seed);
            run.Enter(run.Map.Nodes.First(x => x.Row == 0 && x.Kind == StageKind.Monster).Id);
            Check.False(run.ResolveBattle(ticket, true, 80));
        }

        [Test]
        public void RewardsAreOneChoiceAndWeaponsRequireExplicitReplacement()
        {
            var run = Stage(StageKind.Monster); Win(run);
            var offer = run.Offers[0];
            Check.Equal(RewardKind.Weapon, offer.Content.Kind);
            Check.False(run.ChooseReward(0)); Check.False(run.ChooseReward(0, 5));
            Check.Equal(RunPhase.Reward, run.Phase); Check.Equal(5, run.Weapons.Count);
            Check.True(run.ChooseReward(0, 2));
            Check.Equal(offer.Content.Id, run.Weapons[2].DefinitionId);
            Check.Equal(5, run.Weapons.Count); Check.Equal(RunPhase.Map, run.Phase);
            Check.False(run.ChooseReward(0, 2)); Check.False(run.SkipReward());
        }

        [Test]
        public void ShopCannotOverspendDuplicatePurchaseOrChargeOnCancelledReplacement()
        {
            var run = Stage(StageKind.Shop);
            int initial = run.Gold;
            Check.False(run.Buy(0)); Check.Equal(initial, run.Gold);
            Check.True(run.Buy(1)); Check.Equal(initial - 25, run.Gold);
            Check.False(run.Buy(1)); Check.False(run.Buy(0, 0));
            Check.Equal(1, run.Items.Count);
            Check.True(run.LeaveService()); Check.False(run.Buy(2));
        }

        [Test]
        public void HealingItemsAreConsumedOnlyWhenUsefulAndCannotBeUsedDuringBattle()
        {
            var run = Stage(StageKind.Monster); run.ResolveBattle(run.StageTicket, true, 40);
            run.ChooseReward(1); Check.Equal(1, run.Items.Count);
            Check.True(run.UseItem(0)); Check.Equal(65, run.Health); Check.Equal(0, run.Items.Count);
            Check.False(run.UseItem(0));
            var full = Stage(StageKind.Shop); full.Buy(1);
            Check.False(full.UseItem(0)); Check.Equal(1, full.Items.Count);
            // Carry a potion to the next field's opening battle.
            FinishField(full); full.AdvanceField();
            full.Enter(full.Map.Nodes.First(x => x.Row == 0 && x.Kind == StageKind.Monster).Id);
            Check.False(full.UseItem(0));
        }

        [Test]
        public void RestAndUpgradeAreOneUseAndDoNotPermitRepeatedStageCompletion()
        {
            var rest = Stage(StageKind.Rest);
            Check.True(rest.Rest()); Check.Equal(rest.MaxHealth, rest.Health);
            Check.False(rest.Rest()); Check.True(rest.LeaveService()); Check.False(rest.LeaveService());
            var upgrade = Stage(StageKind.Upgrade);
            Check.True(upgrade.Upgrade(0)); Check.Equal(1, upgrade.Weapons[0].Level);
            Check.False(upgrade.Upgrade(1)); Check.False(upgrade.Upgrade(-1));
        }

        [Test]
        public void WeaponUpgradesCapAtThreeAcrossFields()
        {
            var run = new RunSession(9);
            for (int i = 0; i < 4; i++)
            {
                Reach(run, StageKind.Upgrade);
                Check.Equal(i < 3, run.Upgrade(0));
                FinishField(run); run.AdvanceField();
            }
            Check.Equal(3, run.Weapons[0].Level);
        }

        [Test]
        public void BossClearWaitsForRewardAndNextFieldPreservesRunState()
        {
            var run = Stage(StageKind.Boss); int health = run.Health; int gold = run.Gold;
            Check.Equal(3, run.ClearedStages); Win(run);
            Check.Equal(RunPhase.Reward, run.Phase); Check.False(run.AdvanceField());
            Check.Equal(gold + 60, run.Gold); run.ChooseReward(1);
            Check.Equal(RunPhase.FieldCleared, run.Phase); Check.Equal(4, run.ClearedStages);
            Check.True(run.AdvanceField()); Check.Equal(2, run.Map.Number);
            Check.Equal(health, run.Health); Check.Equal(1, run.Items.Count); Check.Equal(5, run.Weapons.Count);
            Check.Equal(0, run.Visited.Count); Check.Equal(null, run.CurrentNode);
            Check.False(run.AdvanceField());
        }

        [Test]
        public void DeathClearsAllProgressionAndRestartRestoresStarterState()
        {
            var run = Stage(StageKind.Shop); run.Buy(1); FinishField(run); run.AdvanceField();
            Reach(run, StageKind.Upgrade); run.Upgrade(0); run.LeaveService(); FinishField(run); run.AdvanceField();
            Reach(run, StageKind.Monster); string ticket = run.StageTicket;
            Check.True(run.ResolveBattle(ticket, false, 0));
            Check.Equal(RunPhase.GameOver, run.Phase); Check.Equal(0, run.Gold);
            Check.Equal(0, run.Items.Count); Check.Equal(0, run.Augments.Count); Check.Equal(0, run.Weapons.Count);
            Check.False(run.SkipReward()); Check.False(run.AdvanceField());
            run.Restart(555);
            Check.Equal(1, run.Map.Number); Check.Equal(100, run.Health); Check.Equal(100, run.MaxHealth);
            Check.Equal(60, run.Gold); Check.Equal(0, run.ClearedStages);
            Check.Equal(5, run.Weapons.Count); Check.True(run.Weapons.All(x => x.Level == 0));
        }

        [Test]
        public void ZeroHealthVictoryBecomesGameOver()
        {
            var run = Stage(StageKind.Monster);
            Check.True(run.ResolveBattle(run.StageTicket, true, 0));
            Check.Equal(RunPhase.GameOver, run.Phase); Check.Equal(0, run.Offers.Count);
        }

        [Test]
        public void AugmentsApplyAndDiscountUpdatesUnpurchasedShopOffers()
        {
            RunSession run = null;
            for (int seed = 1; seed < 500; seed++)
            {
                run = Stage(StageKind.Shop, seed, new RunRules(startingGold: 500));
                if (run.Offers[2].Content.Id == "bargain") break;
            }
            Check.Equal("bargain", run.Offers[2].Content.Id);
            int price = run.Offers[0].Price; Check.True(run.Buy(2));
            Check.Equal(price * 80 / 100, run.Offers[0].Price); Check.Equal(1, run.CountAugment("bargain"));
            for (int seed = 1; seed < 500; seed++)
            {
                run = Stage(StageKind.Monster, seed); Win(run);
                if (run.Offers[2].Content.Id == "vitality") break;
            }
            Check.Equal("vitality", run.Offers[2].Content.Id); run.ChooseReward(2);
            Check.Equal(120, run.MaxHealth); Check.Equal(120, run.Health);
            run.Abandon(); Check.Equal(100, run.MaxHealth); Check.Equal(0, run.Augments.Count);
        }

        [Test]
        public void ShoppingDoesNotChangeNextFieldMapForSameSeed()
        {
            var first = Stage(StageKind.Shop, 17); var second = Stage(StageKind.Monster, 17);
            first.Buy(1); FinishField(first); FinishField(second);
            first.AdvanceField(); second.AdvanceField();
            Check.Equal(Fingerprint(first.Map), Fingerprint(second.Map));
        }

        private static RunSession Stage(StageKind kind, int seed = 42, RunRules rules = null)
        { var run = new RunSession(seed, rules); Reach(run, kind); return run; }

        private static void Reach(RunSession run, StageKind kind)
        {
            var target = run.Map.Nodes.First(x => x.Kind == kind);
            var path = FindPath(run.Map, target.Id);
            foreach (var node in path)
            {
                Check.True(run.Enter(node.Id), "Cannot enter " + node.Id);
                if (node.Id == target.Id) return;
                FinishStage(run);
            }
        }

        private static List<StageNode> FindPath(FieldMap map, string target)
        {
            foreach (var start in map.Nodes.Where(x => x.Row == 0))
            {
                var path = Search(map, start, target);
                if (path != null) return path;
            }
            throw new Exception("Target unreachable");
        }

        private static List<StageNode> Search(FieldMap map, StageNode node, string target)
        {
            if (node.Id == target) return new List<StageNode> { node };
            foreach (var id in node.Next)
            {
                var path = Search(map, map.Find(id), target);
                if (path == null) continue;
                path.Insert(0, node); return path;
            }
            return null;
        }

        private static void FinishStage(RunSession run)
        {
            if (run.CurrentNode.IsBattle) { Win(run); Check.True(run.SkipReward()); }
            else Check.True(run.LeaveService());
        }

        private static void FinishField(RunSession run)
        {
            while (run.Phase != RunPhase.FieldCleared)
            {
                if (run.Phase == RunPhase.Stage) FinishStage(run);
                else if (run.Phase == RunPhase.Reward) run.SkipReward();
                else Check.True(run.Enter(run.Map.Nodes.First(x => run.CanEnter(x.Id)).Id));
            }
        }
        private static void Win(RunSession run) { Check.True(run.ResolveBattle(run.StageTicket, true, run.Health)); }
        private static string Fingerprint(FieldMap map)
        { return string.Join("|", map.Nodes.Select(x => x.Id + ":" + x.Kind + ":" + string.Join(",", x.Next))); }
    }

    internal static class Check
    {
        public static void True(bool value, string message = "Expected true") { if (!value) throw new Exception(message); }
        public static void False(bool value) { True(!value, "Expected false"); }
        public static void Equal<T>(T expected, T actual)
        { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual); }
    }
}
