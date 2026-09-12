using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    /// <summary>Owns one disposable run and applies incoming damage from its active performance.</summary>
    public sealed class RunSession
    {
        private readonly RunRules rules;
        private SeededRandom mapRandom;
        private SeededRandom rewardRandom;
        private readonly List<WeaponState> weapons = new List<WeaponState>();
        private readonly List<string> items = new List<string>();
        private readonly List<string> augments = new List<string>();
        private readonly List<string> visited = new List<string>();
        private readonly List<Offer> offers = new List<Offer>();
        private bool claimedService;

        public int Seed { get; private set; }
        public decimal Health { get; private set; }
        public int MaxHealth { get; private set; }
        public int Gold { get; private set; }
        public int ClearedStages { get; private set; }
        public RunPhase Phase { get; private set; }
        public FieldMap Map { get; private set; }
        public StageNode CurrentNode { get; private set; }
        public string StageTicket { get; private set; }
        public MusicStage BattleMusic { get; private set; }
        public BattlePlan BattlePlan { get; private set; }
        public StageHealth EnemyHealth { get; private set; }
        public WeaponArrangement BattleLoadout { get; private set; }
        public RhythmRound ActiveRhythmRound { get; private set; }
        public bool ServiceClaimed => claimedService;
        public IReadOnlyList<WeaponState> Weapons { get; }
        public IReadOnlyList<string> Items { get; }
        public IReadOnlyList<string> Augments { get; }
        public IReadOnlyList<string> Visited { get; }
        public IReadOnlyList<Offer> Offers { get; }

        public RunSession(int seed, RunRules rules = null)
        {
            this.rules = rules ?? new RunRules();
            Weapons = weapons.AsReadOnly(); Items = items.AsReadOnly(); Augments = augments.AsReadOnly();
            Visited = visited.AsReadOnly(); Offers = offers.AsReadOnly();
            Restart(seed);
        }

        public void Restart(int seed)
        {
            StopActiveRound();
            Seed = seed;
            mapRandom = new SeededRandom(seed);
            rewardRandom = new SeededRandom(unchecked(seed ^ (int)0xa511e9b3u));
            Health = MaxHealth = rules.StartingHealth; Gold = rules.StartingGold; ClearedStages = 0;
            weapons.Clear(); items.Clear(); augments.Clear(); visited.Clear(); offers.Clear();
            foreach (var id in new[] { "sword", "shield", "spear", "hammer", "dagger" })
                weapons.Add(new WeaponState(id));
            CurrentNode = null; StageTicket = null; BattleMusic = null; BattlePlan = null; EnemyHealth = null; BattleLoadout = null; claimedService = false;
            Map = MapGenerator.Generate(1, mapRandom); Phase = RunPhase.Map;
        }

        public bool CanEnter(string nodeId)
        {
            if (Phase != RunPhase.Map || Map.Find(nodeId) == null) return false;
            if (CurrentNode == null) return Map.Find(nodeId).Row == 0;
            foreach (var next in CurrentNode.Next) if (next == nodeId) return true;
            return false;
        }

        public bool Enter(string nodeId)
        {
            if (!CanEnter(nodeId)) return false;
            CurrentNode = Map.Find(nodeId); Phase = RunPhase.Stage;
            CurrentNode.Reveal();
            StageTicket = Guid.NewGuid().ToString("N"); claimedService = false; offers.Clear();
            BattleMusic = CurrentNode.IsBattle
                ? MusicCatalog.ForEncounter(Seed, Map.Number, CurrentNode.Row, CurrentNode.Column) : null;
            BattlePlan = CurrentNode.IsBattle ? BattlePlanner.ForEncounter(BattleMusic, Seed, CurrentNode, Map.Number) : null;
            if (BattlePlan != null)
            {
                decimal maximum = (160 + (Map.Number - 1) * 50 + CurrentNode.Row * 20) *
                    (CurrentNode.Kind == StageKind.Boss ? 2.5m : CurrentNode.Kind == StageKind.Elite ? 1.5m : 1m);
                EnemyHealth = new StageHealth(maximum); BattleLoadout = new WeaponArrangement(BattlePlan, Weapons);
            }
            if (CurrentNode.Kind == StageKind.Shop) GenerateOffers(true);
            return true;
        }

        // A ticket prevents stale/duplicate battle callbacks from granting rewards to another stage/run.
        // Call only after Overkill/finale finishes. Victory already accounts for shared enemy HP.
        public bool ResolveBattle(string stageTicket, bool victory, decimal remainingPlayerHealth)
        {
            if (Phase != RunPhase.Stage || CurrentNode == null || !CurrentNode.IsBattle ||
                stageTicket != StageTicket || remainingPlayerHealth < 0 || remainingPlayerHealth > MaxHealth)
                return false;
            StopActiveRound();
            Health = remainingPlayerHealth;
            if (!victory || Health == 0) { EndRun(); return true; }
            CompleteStage();
            Gold += CurrentNode.Kind == StageKind.Boss ? 60 : CurrentNode.Kind == StageKind.Elite ? 40 : 25;
            GenerateOffers(false); Phase = RunPhase.Reward;
            return true;
        }

        public RhythmRound StartRhythmRound()
        {
            if (Phase != RunPhase.Stage || BattlePlan == null || ActiveRhythmRound != null || Health <= 0) return null;
            ActiveRhythmRound = new RhythmRound(BattlePlan, combat: new WeaponBattle(BattleLoadout, EnemyHealth, Health, MaxHealth));
            ActiveRhythmRound.ResultJudged += ApplyRhythmDamage;
            return ActiveRhythmRound;
        }

        public bool CloseRhythmRound(RhythmRound round)
        {
            if (round == null || round != ActiveRhythmRound) return false;
            StopActiveRound(); return true;
        }

        private void ApplyRhythmDamage(RhythmResult result)
        {
            if (Phase != RunPhase.Stage || ActiveRhythmRound == null) return;
            Health = Math.Max(0m, Health - result.DamageTaken);
            if (Health == 0) EndRun();
        }

        private void StopActiveRound()
        {
            if (ActiveRhythmRound == null) return;
            ActiveRhythmRound.ResultJudged -= ApplyRhythmDamage;
            ActiveRhythmRound.Stop(); ActiveRhythmRound = null;
        }

        public bool Rest()
        {
            if (!IsService(StageKind.Rest) || claimedService) return false;
            Health = Math.Min(MaxHealth, Health + RestAmount);
            claimedService = true;
            return true;
        }

        public int RestAmount => Math.Max(1, MaxHealth * rules.RestPercent / 100) + CountAugment("recovery") * 10;

        public bool Upgrade(int slot)
        {
            if (!IsService(StageKind.Upgrade) || claimedService || !ValidSlot(slot) ||
                weapons[slot].Level >= RunRules.MaximumUpgrade) return false;
            weapons[slot].Level++; claimedService = true;
            return true;
        }

        public bool LeaveService()
        {
            if (Phase != RunPhase.Stage || CurrentNode == null || CurrentNode.IsBattle) return false;
            CompleteStage(); offers.Clear(); Phase = RunPhase.Map;
            return true;
        }

        public bool ChooseReward(int index, int replaceSlot = -1)
        {
            if (Phase != RunPhase.Reward || index < 0 || index >= offers.Count) return false;
            if (!Grant(offers[index].Content, replaceSlot)) return false;
            FinishReward(); return true;
        }

        public bool SkipReward()
        {
            if (Phase != RunPhase.Reward) return false;
            FinishReward(); return true;
        }

        public bool Buy(int index, int replaceSlot = -1)
        {
            if (!IsService(StageKind.Shop) || index < 0 || index >= offers.Count) return false;
            var offer = offers[index];
            if (offer.Purchased || Gold < offer.Price || !Grant(offer.Content, replaceSlot)) return false;
            Gold -= offer.Price; offer.Purchased = true;
            foreach (var remaining in offers)
                if (!remaining.Purchased) remaining.Price = DiscountedPrice(remaining.Content);
            return true;
        }

        public bool UseItem(int index)
        {
            // Consumption is outside combat for now; the battle adapter will own combat item timing.
            if ((Phase != RunPhase.Map && Phase != RunPhase.Reward && Phase != RunPhase.FieldCleared &&
                 !(Phase == RunPhase.Stage && CurrentNode != null && !CurrentNode.IsBattle)) ||
                index < 0 || index >= items.Count || Health >= MaxHealth) return false;
            if (items[index] != "potion") return false;
            Health = Math.Min(MaxHealth, Health + 25); items.RemoveAt(index); return true;
        }

        public bool AdvanceField()
        {
            if (Phase != RunPhase.FieldCleared) return false;
            Map = MapGenerator.Generate(Map.Number + 1, mapRandom);
            CurrentNode = null; StageTicket = null; BattleMusic = null; BattlePlan = null; EnemyHealth = null; BattleLoadout = null; visited.Clear(); claimedService = false;
            Phase = RunPhase.Map;
            return true;
        }

        public void Abandon() { if (Phase != RunPhase.GameOver) EndRun(); }

        public int CountAugment(string id)
        {
            int count = 0; foreach (var augment in augments) if (augment == id) count++;
            return count;
        }

        private void GenerateOffers(bool shop)
        {
            offers.Clear();
            foreach (RewardKind kind in Enum.GetValues(typeof(RewardKind)))
            {
                var content = ContentCatalog.Pick(kind, rewardRandom);
                offers.Add(new Offer(content, shop ? DiscountedPrice(content) : 0));
            }
        }

        private int DiscountedPrice(ContentDefinition content)
        { return content.Price * (100 - Math.Min(60, CountAugment("bargain") * 20)) / 100; }

        private bool Grant(ContentDefinition content, int slot)
        {
            switch (content.Kind)
            {
                case RewardKind.Weapon:
                    if (!ValidSlot(slot)) return false;
                    weapons[slot] = new WeaponState(content.Id);
                    break;
                case RewardKind.Item: items.Add(content.Id); break;
                case RewardKind.Augment:
                    augments.Add(content.Id);
                    if (content.Id == "vitality") { MaxHealth += 20; Health = Math.Min(MaxHealth, Health + 20); }
                    break;
            }
            return true;
        }

        private bool ValidSlot(int slot) => slot >= 0 && slot < weapons.Count;
        private bool IsService(StageKind kind) => Phase == RunPhase.Stage && CurrentNode != null && CurrentNode.Kind == kind;
        private void CompleteStage() { visited.Add(CurrentNode.Id); ClearedStages++; StageTicket = null; BattleMusic = null; BattlePlan = null; EnemyHealth = null; BattleLoadout = null; }
        private void FinishReward()
        {
            offers.Clear();
            Phase = CurrentNode.Kind == StageKind.Boss ? RunPhase.FieldCleared : RunPhase.Map;
        }
        private void EndRun()
        {
            StopActiveRound();
            // Retain only the reached field/stage count for the result screen. No inventory survives.
            Health = 0; MaxHealth = rules.StartingHealth; Gold = 0;
            weapons.Clear(); items.Clear(); augments.Clear(); offers.Clear();
            visited.Clear(); StageTicket = null; BattleMusic = null; BattlePlan = null; EnemyHealth = null; BattleLoadout = null; claimedService = false; Phase = RunPhase.GameOver;
        }
    }
}
