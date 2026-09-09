using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public static class BattlePlanner
    {
        public static BattlePlan ForEncounter(MusicStage stage, int runSeed, StageNode node)
        {
            if (node == null || !node.IsBattle) throw new ArgumentException("A battle node is required.", nameof(node));
            return Generate(stage, node.Kind, Hash(runSeed, node.Id));
        }

        public static BattlePlan Generate(MusicStage stage, StageKind kind, int seed)
        {
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            if (kind != StageKind.Monster && kind != StageKind.Elite && kind != StageKind.Boss)
                throw new ArgumentException("Only battle stages have monster plans.", nameof(kind));
            var eligible = new List<(MonsterDefinition monster, List<PatternPlacement> candidates)>();
            foreach (var monster in MonsterCatalog.All)
            {
                var candidates = Candidates(stage, monster);
                if (candidates.Count > 0) eligible.Add((monster, candidates));
            }
            var random = new SeededRandom(Hash(seed, "roster"));
            random.Shuffle(eligible);
            int count = Math.Min(kind == StageKind.Monster ? 2 : 3, eligible.Count);
            var proposals = new List<MonsterProposal>();
            for (int i = 0; i < count; i++)
            {
                var entry = eligible[i];
                // Each monster submits independently; it never sees another monster's claimed slots.
                proposals.Add(Propose(entry.monster.Id, entry.monster, entry.candidates, new SeededRandom(Hash(seed, entry.monster.Id))));
            }
            return Resolve(stage, proposals, Hash(seed, "ties"));
        }

        private static List<PatternPlacement> Candidates(MusicStage stage, MonsterDefinition monster)
        {
            var result = new List<PatternPlacement>();
            foreach (var candidate in stage.FindPlacements(monster.Pattern))
                if (monster.ResponseTicks <= stage.Music.TotalTicks - candidate.StartTick) result.Add(candidate);
            return result;
        }

        public static MonsterProposal Propose(MusicStage stage, string instanceId, MonsterDefinition monster, int seed)
        {
            if (stage == null || monster == null) throw new ArgumentNullException(stage == null ? nameof(stage) : nameof(monster));
            return Propose(instanceId, monster, Candidates(stage, monster), new SeededRandom(seed));
        }

        private static MonsterProposal Propose(string id, MonsterDefinition monster, List<PatternPlacement> candidates, SeededRandom random)
        {
            var placements = new List<PatternPlacement>();
            long responseAfter = 0, callAfter = 0;
            foreach (var candidate in candidates)
            {
                if (candidate.StartTick < responseAfter || candidate.CueStartTick < callAfter) continue;
                // Increase chance smoothly with weight while retaining a chance to skip highlights.
                double chance = 1 - Math.Pow(1 - monster.ParticipationChance, candidate.Weight);
                if (random.Next(1000000) / 1000000.0 >= chance) continue;
                placements.Add(candidate);
                callAfter = (long)candidate.StartTick + monster.ResponseTicks;
                responseAfter = callAfter + monster.RestTicks;
            }
            // A selected sample monster should have a proposal, even if every random roll skipped.
            // This fallback guarantees participation before conflict resolution, not a highlight.
            if (placements.Count == 0 && candidates.Count > 0)
            {
                double total = 0; foreach (var candidate in candidates) total += candidate.Weight;
                double roll = random.Next(1000000) / 1000000.0 * total;
                var selected = candidates[candidates.Count - 1];
                foreach (var candidate in candidates) { roll -= candidate.Weight; if (roll < 0) { selected = candidate; break; } }
                placements.Add(selected);
            }
            return new MonsterProposal(id, monster, placements);
        }

        /// <summary>Remove whole conflicting bundles, recomputing each owner's occupied beats after every withdrawal.</summary>
        public static BattlePlan Resolve(MusicStage stage, IEnumerable<MonsterProposal> submitted, int tieSeed)
        {
            if (stage == null || submitted == null) throw new ArgumentNullException(stage == null ? nameof(stage) : nameof(submitted));
            var proposals = new List<MonsterProposal>(submitted);
            if (proposals.Exists(x => x == null)) throw new ArgumentException("Null proposal.", nameof(submitted));
            proposals.Sort((a, b) => string.CompareOrdinal(a.InstanceId, b.InstanceId));
            var active = new List<List<PlannedAttack>>();
            for (int i = 0; i < proposals.Count; i++)
            {
                var proposal = proposals[i];
                if (i > 0 && proposals[i - 1].InstanceId == proposal.InstanceId) throw new ArgumentException("Duplicate monster instance.");
                var attacks = new List<PlannedAttack>();
                foreach (var placement in proposal.Placements)
                {
                    if (placement.CueStartTick < 0 || proposal.Monster.ResponseTicks > stage.Music.TotalTicks - placement.StartTick)
                        throw new ArgumentException("An attack does not fit this song.");
                    foreach (var slot in placement.Slots)
                        if (slot.Index < 0 || slot.Index >= stage.Slots.Count || !ReferenceEquals(slot, stage.Slots[slot.Index]))
                            throw new ArgumentException("An attack must use this stage's slots.");
                    attacks.Add(new PlannedAttack(proposal, placement));
                }
                active.Add(attacks);
            }
            var beatGrid = BeatGrid(stage);
            var random = new SeededRandom(tieSeed);
            var withdrawals = new List<PlanWithdrawal>();
            while (true)
            {
                // Resolve the earliest physical conflict; stable instance ordering breaks search ties.
                int firstOwner = -1, secondOwner = -1, firstAttack = -1, secondAttack = -1, earliest = int.MaxValue;
                for (int a = 0; a < active.Count; a++) for (int b = a + 1; b < active.Count; b++)
                    for (int x = 0; x < active[a].Count; x++) for (int y = 0; y < active[b].Count; y++)
                        if (InputCompatibility.Conflict(active[a][x].Placement, active[b][y].Placement, out int tick) && tick < earliest)
                        { firstOwner = a; secondOwner = b; firstAttack = x; secondAttack = y; earliest = tick; }
                if (firstOwner < 0) break;
                int leftCount = CountOccupied(active[firstOwner], beatGrid), rightCount = CountOccupied(active[secondOwner], beatGrid);
                bool leftYields = leftCount == rightCount ? random.Next(2) == 0 : leftCount > rightCount;
                int loser = leftYields ? firstOwner : secondOwner, winner = leftYields ? secondOwner : firstOwner;
                int index = leftYields ? firstAttack : secondAttack;
                withdrawals.Add(new PlanWithdrawal(active[loser][index], proposals[winner].InstanceId, earliest,
                    leftYields ? leftCount : rightCount, leftYields ? rightCount : leftCount));
                active[loser].RemoveAt(index); // The Call and every Response step leave together. Never reinsert.
            }
            var monsters = new List<MonsterPlan>();
            for (int i = 0; i < proposals.Count; i++)
                if (active[i].Count > 0) monsters.Add(new MonsterPlan(proposals[i], active[i], CountOccupied(active[i], beatGrid)));
            return new BattlePlan(stage, monsters, withdrawals);
        }

        // Count occupied positions in this song, not slot alternatives, attack instances or Call pulses.
        // Held intervals occupy [start, end); an explicit release also occupies its ending position.
        private static SortedSet<int> BeatGrid(MusicStage stage)
        {
            var grid = new SortedSet<int>();
            foreach (var slot in stage.Slots)
            {
                grid.Add(slot.StartTick);
                if (slot.Touch.End == TouchTransition.Release) grid.Add(slot.EndTick);
            }
            return grid;
        }

        private static int CountOccupied(List<PlannedAttack> attacks, SortedSet<int> grid)
        {
            var occupied = new HashSet<int>();
            foreach (var attack in attacks) foreach (var slot in attack.Placement.Slots)
            {
                occupied.Add(slot.StartTick);
                if (slot.Touch.End == TouchTransition.Release) occupied.Add(slot.EndTick);
                if (slot.Touch.HoldThroughInterval)
                    foreach (int tick in grid.GetViewBetween(slot.StartTick, slot.EndTick))
                        if (tick < slot.EndTick) occupied.Add(tick);
            }
            return occupied.Count;
        }

        private static int Hash(int seed, string id)
        {
            unchecked
            {
                uint hash = (uint)seed ^ 0x9e3779b9u;
                foreach (char c in id) hash = (hash ^ c) * 16777619u;
                hash ^= hash >> 16; return (int)hash;
            }
        }
    }
}
