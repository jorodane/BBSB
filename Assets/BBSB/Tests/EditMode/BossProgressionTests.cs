using System;
using System.Collections.Generic;
using System.Linq;
using BBSB.Core;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class BossProgressionTests
    {
        private static RunSession Run(int seed = 31) => new RunSession(seed, useFiveLaneCombat: true);
        private static List<StageNode> Path(FieldMap map, StageNode node, StageKind kind)
        {
            if (node.Kind == kind) return new List<StageNode> { node };
            foreach (string id in node.Next)
            {
                var path = Path(map, map.Find(id), kind);
                if (path == null) continue;
                path.Insert(0, node); return path;
            }
            return null;
        }
        private static void Arrive(RunSession run, StageKind kind)
        {
            var path = run.Map.Nodes.Where(n => n.Row == 0).Select(n => Path(run.Map, n, kind)).First(p => p != null);
            foreach (var node in path)
            {
                Check.True(run.Enter(node.Id));
                if (node.Kind == kind) return;
                if (!node.IsBattle) { Check.True(run.LeaveService()); continue; }
                Check.True(run.ResolveBattle(run.StageTicket, true, run.Health));
                Check.True(run.SkipReward());
            }
        }
        private static void DefeatBoss(RunSession run)
        { Arrive(run, StageKind.Boss); Check.True(run.ResolveBattle(run.StageTicket, true, run.Health)); }
        private static string Offers(RunSession run) => string.Join("|", run.Offers.Select(o => o.Content.Id + "/" + o.Attribute + "/" + o.Rarity));

        [Test] public void AllSevenLinesAreOpenInTheDraftLiveBattleSavedLayoutAndRestart()
        {
            foreach (int edge in new[] { -1, 4 })
            {
                var character = new RunCharacterDefinition("edge-player", new[] {
                    new StartingWeaponDefinition("staff", "staff", edge), new StartingWeaponDefinition("shield", "heater-shield", 2) });
                var draft = new StartingLoadout(character); Check.Equal(7, draft.Equipment.AvailableLaneCount);
                var restored = new StartingLoadout(character, draft.Capture());
                int slot = edge < 0 ? BattleInputLayout.Left : BattleInputLayout.Right;
                Check.Equal("staff", restored.Equipment.At(slot).Weapon.DefinitionId);
                var run = new RunSession(31, useFiveLaneCombat: true, character: character, startingLoadout: restored.Capture());
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    Check.Equal(2, run.Equipment.Capacity); Check.Equal(7, run.Equipment.AvailableLaneCount);
                    Check.True(run.Enter(run.Map.Nodes.First(n => run.CanEnter(n.Id)).Id));
                    var battle = run.StartFiveLaneBattle(); Check.True(battle != null);
                    foreach (int key in BattleInputLayout.DisplayOrder) Check.True(battle.IsInputAvailable(key));
                    battle.Press(slot, 0); battle.Release(slot, 0); Check.Equal(0, battle.PerfectCount);
                    battle.Press(slot, 1); battle.Release(slot, 1); Check.Equal(1, battle.PerfectCount);
                    run.Abandon(); run.Restart(31);
                    Check.Equal(edge, run.Equipment.PlacementOf(run.OwnedWeapons[0]).Offset);
                }
            }
        }

        [Test] public void OrdinaryEliteAndShopOffersContainItemsAugmentsAndNoteParts()
        {
            foreach (var kind in new[] { StageKind.Monster, StageKind.Elite, StageKind.Shop })
            {
                var run = Run(); Arrive(run, kind);
                if (kind != StageKind.Shop) Check.True(run.ResolveBattle(run.StageTicket, true, run.Health));
                Check.Equal(4, run.Offers.Count);
                Check.True(run.Offers.All(o => o.Content.Kind != RewardKind.Weapon));
                Check.Equal(2, run.OwnedWeapons.Count); Check.Equal(2, run.Equipment.Capacity);
                Check.False(run.IsBossWeaponReward);
                if (kind == StageKind.Shop)
                {
                    int gold = run.Gold; int price = run.Offers[0].Price;
                    Check.True(run.Buy(0)); Check.Equal(gold - price, run.Gold); Check.False(run.Buy(0));
                    Check.Equal(2, run.OwnedWeapons.Count); Check.Equal(1, run.Items.Count);
                }
                else Check.True(run.SkipReward());
            }
        }

        [Test] public void BossOffersThreeDistinctNewTypesAndRequiresExactlyOneChoice()
        {
            for (int choice = 0; choice < 3; choice++)
            {
                var run = Run(31 + choice); DefeatBoss(run);
                Check.True(run.IsBossWeaponReward); Check.Equal(3, run.Offers.Count);
                Check.Equal(3, run.Offers.Select(o => o.Content.Id).Distinct().Count());
                Check.True(run.Offers.All(o => o.Content.Kind == RewardKind.Weapon && o.Price == 0));
                Check.True(run.Offers.All(o => run.OwnedWeapons.All(w => w.DefinitionId != o.Content.Id)));
                Check.Equal(3, run.Equipment.Capacity); Check.True(run.EquipmentCapacityIncreased);
                string fingerprint = Offers(run); var selected = run.Offers[choice];
                Check.False(run.SkipReward()); Check.False(run.AdvanceField());
                Check.False(run.ChooseReward(-1)); Check.False(run.ChooseReward(3)); Check.Equal(fingerprint, Offers(run));
                int gold = run.Gold; var placements = run.Equipment.Placements.ToArray();
                Check.True(run.ChooseReward(choice)); Check.Equal(3, run.OwnedWeapons.Count);
                var acquired = run.OwnedWeapons[2];
                Check.Equal(selected.Content.Id, acquired.DefinitionId); Check.Equal(selected.Rarity, acquired.Rarity);
                Check.Equal(selected.Attribute, acquired.Attribute); Check.Equal(0, acquired.Level); Check.Equal(gold, run.Gold);
                Check.True(placements.SequenceEqual(run.Equipment.Placements)); Check.Equal(2, run.Weapons.Count);
                Check.Equal(RunPhase.FieldCleared, run.Phase); Check.Equal(0, run.Offers.Count);
                Check.False(run.ChooseReward(choice)); Check.False(run.SkipReward()); Check.False(run.IsBossWeaponReward);
            }
        }

        [Test] public void NextFieldPreservesTheNewWeaponPlacementUpgradesAndRunResources()
        {
            var run = Run(); run.OwnedWeapons[0].Level = 2;
            run.EquipWeapon(1, 4);
            // Keep both ordinary reward kinds in the same run before the boss.
            var path = run.Map.Nodes.Where(n => n.Row == 0).Select(n => Path(run.Map, n, StageKind.Boss)).First(p => p != null);
            int rewards = 0;
            foreach (var node in path)
            {
                Check.True(run.Enter(node.Id));
                if (!node.IsBattle) { Check.True(run.LeaveService()); continue; }
                Check.True(run.ResolveBattle(run.StageTicket, true, 60));
                if (run.IsBossWeaponReward) break;
                Check.True(run.ChooseReward(rewards++ % 2));
            }
            var acquiredOffer = run.Offers[0]; Check.True(run.ChooseReward(0));
            Check.True(run.EquipWeaponAtCenter(2, 1 + (WeaponCatalog.Find(acquiredOffer.Content.Id).RequiredLanes - 1) * .5));
            var owned = run.OwnedWeapons.ToArray(); var placement = run.Equipment.Placements.ToArray();
            var items = run.Items.ToArray(); var augments = run.Augments.ToArray();
            decimal health = run.Health; int maximum = run.MaxHealth, gold = run.Gold, cleared = run.ClearedStages;
            Check.True(run.AdvanceField()); Check.Equal(2, run.Map.Number); Check.Equal(RunPhase.Map, run.Phase);
            Check.Equal(health, run.Health); Check.Equal(maximum, run.MaxHealth); Check.Equal(gold, run.Gold);
            Check.Equal(cleared, run.ClearedStages); Check.Equal(0, run.Visited.Count); Check.Equal(null, run.CurrentNode);
            Check.True(owned.SequenceEqual(run.OwnedWeapons)); Check.True(placement.SequenceEqual(run.Equipment.Placements));
            Check.True(items.SequenceEqual(run.Items)); Check.True(augments.SequenceEqual(run.Augments));
            Check.Equal(2, run.OwnedWeapons[0].Level); Check.Equal(3, run.Equipment.Capacity); Check.Equal(7, run.Equipment.AvailableLaneCount);
            Check.False(run.AdvanceField());
            Check.True(run.Enter(run.Map.Nodes.First(n => run.CanEnter(n.Id)).Id));
            var battle = run.StartFiveLaneBattle(); Check.Equal(run.Weapons.Count, battle.Lanes.Count);
            Check.Equal(InputExtensions.All, battle.Extensions);
        }

        [Test] public void BossOffersRemainDeterministicAndHaveAFallbackAfterEveryWeaponIsOwned()
        {
            var a = Run(51); var b = Run(51); DefeatBoss(a); DefeatBoss(b);
            Check.Equal(Offers(a), Offers(b));
            var complete = Run(51);
            foreach (var weapon in WeaponCatalog.All)
                if (complete.OwnedWeapons.All(w => w.DefinitionId != weapon.Id)) complete.Equipment.Acquire(new WeaponState(weapon.Id));
            DefeatBoss(complete);
            Check.Equal(3, complete.Offers.Count); Check.Equal(3, complete.Offers.Select(o => o.Content.Id).Distinct().Count());
            Check.True(complete.ChooseReward(0)); Check.Equal(WeaponCatalog.All.Count + 1, complete.OwnedWeapons.Count);
        }

        [Test] public void DefeatAndStaleBossCallbacksCannotGrantAWeaponOrCapacity()
        {
            var run = Run(); Arrive(run, StageKind.Boss); string ticket = run.StageTicket;
            Check.False(run.ResolveBattle("stale", true, run.Health)); Check.Equal(2, run.Equipment.Capacity);
            Check.True(run.ResolveBattle(ticket, false, 0)); Check.Equal(RunPhase.GameOver, run.Phase);
            Check.Equal(0, run.Offers.Count); Check.Equal(0, run.OwnedWeapons.Count); Check.Equal(2, run.Equipment.Capacity);
            Check.False(run.ChooseReward(0)); Check.False(run.AdvanceField());
            run.Restart(31); Check.False(run.ResolveBattle(ticket, true, 100));
            Check.Equal(2, run.OwnedWeapons.Count); Check.Equal(2, run.Weapons.Count);
            Check.Equal(InputExtensions.All, run.Equipment.Extensions);
        }
    }
}
