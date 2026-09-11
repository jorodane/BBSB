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
        public void ThousandMapsHaveFourMonsterEntrancesAndAReachableSixthStageBoss()
        {
            for (int seed = 0; seed < 1000; seed++)
            {
                var map = MapGenerator.Generate(1, new SeededRandom(seed));
                Check.Equal(21, map.Nodes.Count);
                Check.Equal(1, map.Nodes.Count(x => x.Kind == StageKind.Boss));
                var opening = map.Nodes.Where(x => x.Row == 0).ToArray();
                Check.Equal(4, opening.Length);
                Check.True(opening.All(x => x.Kind == StageKind.Monster && x.IsRevealed && !x.IsMystery));
                var reached = new HashSet<string>(map.Nodes.Where(x => x.Row == 0).Select(x => x.Id));
                foreach (var node in map.Nodes)
                {
                    Check.True(reached.Contains(node.Id), "Unreachable node at seed " + seed);
                    if (node.Row == 5)
                    { Check.Equal(StageKind.Boss, node.MapKind); Check.False(node.IsMystery); Check.Equal(0, node.Next.Count); }
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
                foreach (StageKind kind in Enum.GetValues(typeof(StageKind)))
                    Check.True(map.Nodes.Any(x => x.Kind == kind || x.MapKind == kind));
                for (int row = 1; row < FieldMap.StageCount - 1; row++)
                    Check.Equal(1, map.Nodes.Count(x => x.Row == row && x.MapKind == StageKind.Mystery));
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
        public void AllFourEntrancesBeginBattlesAndResetForEachField()
        {
            for (int column = 0; column < FieldMap.Width; column++)
            {
                var run = new RunSession(73);
                for (int field = 1; field <= 2; field++)
                {
                    Check.Equal(4, run.Map.Nodes.Count(x => run.CanEnter(x.Id)));
                    var start = run.Map.Nodes.Single(x => x.Row == 0 && x.Column == column);
                    Check.True(run.Enter(start.Id));
                    Check.Equal(StageKind.Monster, run.CurrentNode.Kind);
                    Check.True(run.BattleMusic != null && run.BattlePlan != null);
                    FinishField(run);
                    Check.Equal(field * 6, run.ClearedStages);
                    Check.True(run.AdvanceField());
                    Check.True(run.Map.Nodes.Where(x => x.IsMystery).All(x => !x.IsRevealed));
                }
            }
        }

        [Test]
        public void MysteryRegionsStayHiddenUntilSuccessfulEntry()
        {
            var run = new RunSession(73);
            var target = run.Map.Nodes.First(x => x.IsMystery);
            var kind = target.Kind;
            string links = string.Join(",", target.Next);
            Check.False(run.Enter(target.Id));
            Check.False(target.IsRevealed);
            Check.Equal(StageKind.Mystery, target.MapKind);
            foreach (var node in run.Map.Nodes) run.CanEnter(node.Id);
            Check.False(target.IsRevealed);
            Reach(run, target);
            Check.True(target.IsRevealed);
            Check.Equal(kind, target.MapKind);
            Check.Equal(kind, target.Kind);
            Check.Equal(links, string.Join(",", target.Next));
            Check.True(run.Map.Nodes.Where(x => x.IsMystery && x != target).All(x => !x.IsRevealed));
            FinishStage(run);
            Check.Equal(kind, target.MapKind);
            Check.True(run.Visited.Contains(target.Id));
            Check.False(run.Enter(target.Id));
        }

        [Test]
        public void MysteryOutcomesUseAllNormalBattleAndServiceFlows()
        {
            var seen = new HashSet<StageKind>();
            for (int seed = 0; seed < 100 && seen.Count < 5; seed++)
            {
                var map = MapGenerator.Generate(1, new SeededRandom(seed));
                foreach (var hidden in map.Nodes.Where(x => x.IsMystery))
                {
                    if (!seen.Add(hidden.Kind)) continue;
                    var run = new RunSession(seed);
                    var target = run.Map.Find(hidden.Id);
                    Check.Equal(StageKind.Mystery, target.MapKind);
                    Reach(run, target);
                    Check.Equal(hidden.Kind, run.CurrentNode.MapKind);
                    if (target.IsBattle)
                    {
                        Check.True(run.BattleMusic != null && run.BattlePlan != null);
                        int gold = run.Gold;
                        Win(run);
                        Check.Equal(gold + (target.Kind == StageKind.Elite ? 40 : 25), run.Gold);
                        Check.Equal(RunPhase.Reward, run.Phase);
                        Check.True(run.SkipReward());
                    }
                    else
                    {
                        Check.True(run.BattleMusic == null && run.BattlePlan == null);
                        if (target.Kind == StageKind.Shop)
                        {
                            Check.Equal(3, run.Offers.Count);
                            Check.True(run.Buy(1)); Check.Equal(1, run.Items.Count);
                        }
                        else if (target.Kind == StageKind.Rest)
                        { Check.True(run.Rest()); Check.True(run.ServiceClaimed); }
                        else
                        {
                            Check.Equal(StageKind.Upgrade, target.Kind);
                            Check.True(run.Upgrade(0)); Check.Equal(1, run.Weapons[0].Level);
                        }
                        Check.True(run.LeaveService());
                    }
                    Check.Equal(RunPhase.Map, run.Phase);
                    Check.True(run.Visited.Contains(target.Id));
                    Check.Equal(hidden.Kind, target.MapKind);
                }
            }
            Check.Equal(5, seen.Count);
            Check.False(seen.Contains(StageKind.Boss)); Check.False(seen.Contains(StageKind.Mystery));
        }

        [Test]
        public void RestartHidesMysteryRegionsWithoutRerollingTheirOutcomes()
        {
            var run = new RunSession(73);
            string before = Fingerprint(run.Map);
            var target = run.Map.Nodes.First(x => x.IsMystery);
            Reach(run, target); FinishStage(run);
            Check.True(target.IsRevealed);
            run.Restart(73);
            Check.Equal(before, Fingerprint(run.Map));
            Check.Equal(target.Kind, run.Map.Find(target.Id).Kind);
            Check.True(run.Map.Nodes.Where(x => x.IsMystery).All(x => x.MapKind == StageKind.Mystery));
            Check.Equal(0, run.Visited.Count);
        }

        [Test]
        public void AllRoutesLimitShopsAndRestsIndependentlyAcrossMerges()
        {
            bool sawShopAndRestTogether = false;
            bool sawMultipleShopsOnDifferentRoutes = false;
            bool sawHiddenShop = false, sawHiddenRest = false;
            foreach (int seed in MapSeeds())
            {
                var random = new SeededRandom(seed);
                for (int field = 1; field <= 4; field++)
                {
                    var map = MapGenerator.Generate(field, random);
                    sawMultipleShopsOnDifferentRoutes |= map.Nodes.Count(x => x.Kind == StageKind.Shop) > 1;
                    sawHiddenShop |= map.Nodes.Any(x => x.Kind == StageKind.Shop && x.IsMystery);
                    sawHiddenRest |= map.Nodes.Any(x => x.Kind == StageKind.Rest && x.IsMystery);
                    foreach (var start in map.Nodes.Where(x => x.Row == 0))
                        InspectRoutes(map, start, 0, 0, ref sawShopAndRestTogether);
                }
            }
            Check.True(sawShopAndRestTogether, "The limits are one of EACH type, not one shop/rest combined.");
            Check.True(sawMultipleShopsOnDifferentRoutes, "Service limits are per route, not per field.");
            Check.True(sawHiddenShop && sawHiddenRest, "Route limits must also cover hidden service outcomes.");
        }

        [Test]
        public void PruningRemovesStraightRailsAndPreservesMeaningfulBranches()
        {
            var topologies = new HashSet<string>();
            foreach (int seed in MapSeeds())
            {
                var map = MapGenerator.Generate(1, new SeededRandom(seed));
                topologies.Add(string.Join("|", map.Nodes.Select(x => x.Id + ":" + string.Join(",", x.Next))));
                Check.True(map.Nodes.Any(x => x.Row < FieldMap.StageCount - 2 && x.Next.Count > 1), "No branch before the boss.");
                for (int row = 0; row < FieldMap.StageCount - 2; row++)
                {
                    var layer = map.Nodes.Where(x => x.Row == row).ToArray();
                    Check.True(layer.Any(x => !x.Next.Any(id => map.Find(id).Column == x.Column)),
                        "All four straight rails survived pruning.");
                    Check.True(layer.Sum(x => x.Next.Count) <= FieldMap.Width + 1, "Too many candidate links survived pruning.");
                }
            }
            Check.True(topologies.Count > 20, "Different seeds should change links as well as node types.");
        }

        [Test]
        public void ShopAndRestLimitsStartFreshInNextField()
        {
            foreach (var kind in new[] { StageKind.Shop, StageKind.Rest })
            {
                var run = Stage(kind);
                FinishField(run); Check.True(run.AdvanceField());
                Reach(run, kind);
                Check.Equal(kind, run.CurrentNode.Kind);
            }
        }

        private static IEnumerable<int> MapSeeds()
        {
            for (int seed = 0; seed < 1000; seed++) yield return seed;
            yield return -1; yield return int.MinValue; yield return int.MaxValue;
        }

        private static void InspectRoutes(FieldMap map, StageNode node, int shops, int rests, ref bool sawBoth)
        {
            shops += node.Kind == StageKind.Shop ? 1 : 0;
            rests += node.Kind == StageKind.Rest ? 1 : 0;
            Check.True(shops <= 1, "Repeated shop on route through " + node.Id);
            Check.True(rests <= 1, "Repeated rest on route through " + node.Id);
            if (node.Kind == StageKind.Boss) { sawBoth |= shops == 1 && rests == 1; return; }
            Check.True(node.Next.Count > 0, "Pruning produced a dead end.");
            foreach (var id in node.Next) InspectRoutes(map, map.Find(id), shops, rests, ref sawBoth);
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
            // Account for the mandatory opening battle: arrive with exactly the weapon price,
            // then a potion must leave too little to buy that weapon.
            int seed = Enumerable.Range(0, 100).First(x => MapGenerator.Generate(1, new SeededRandom(x))
                .Nodes.Any(node => node.Row == 1 && node.Kind == StageKind.Shop));
            int weaponPrice = Stage(StageKind.Shop, seed).Offers[0].Price;
            var run = Stage(StageKind.Shop, seed, new RunRules(startingGold: weaponPrice - 25));
            int initial = run.Gold;
            Check.Equal(weaponPrice, initial);
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
            var run = Stage(StageKind.Boss); decimal health = run.Health; int gold = run.Gold;
            Check.Equal(5, run.ClearedStages); Win(run);
            Check.Equal(RunPhase.Reward, run.Phase); Check.False(run.AdvanceField());
            Check.Equal(gold + 60, run.Gold); run.ChooseReward(1);
            Check.Equal(RunPhase.FieldCleared, run.Phase); Check.Equal(6, run.ClearedStages);
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
        { Reach(run, run.Map.Nodes.First(x => x.Kind == kind)); }

        private static void Reach(RunSession run, StageNode target)
        {
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
        { return string.Join("|", map.Nodes.Select(x => x.Id + ":" + x.Kind + ":" + x.IsMystery + ":" + string.Join(",", x.Next))); }
    }

    internal static class Check
    {
        public static void True(bool value, string message = "Expected true") { if (!value) throw new Exception(message); }
        public static void False(bool value) { True(!value, "Expected false"); }
        public static void Equal<T>(T expected, T actual)
        { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual); }
    }
}
