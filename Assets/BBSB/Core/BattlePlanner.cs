using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public static class BattlePlanner
    {
        public static BattlePlan ForEncounter(MusicStage stage, int runSeed, StageNode node, int field)
        {
            if (node == null || !node.IsBattle) throw new ArgumentException("A battle node is required.", nameof(node));
            return Generate(stage, node.Kind, Hash(runSeed, node.Id), field);
        }

        public static BattlePlan Generate(MusicStage stage, StageKind kind, int seed, int field = 1)
        {
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            if (field < 1) throw new ArgumentOutOfRangeException(nameof(field));
            if (kind != StageKind.Monster && kind != StageKind.Elite && kind != StageKind.Boss)
                throw new ArgumentException("Only battle stages have monster plans.", nameof(kind));
            var eligible = new List<MonsterDefinition>();
            foreach (var monster in MonsterCatalog.All)
            {
                foreach (var pattern in monster.Patterns)
                    if (Candidates(stage, pattern).Count > 0) { eligible.Add(monster); break; }
            }
            var random = new SeededRandom(Hash(seed, "roster"));
            // All encounter kinds introduce one opponent per field, capped at three.
            int count = Math.Min(Math.Min(field, 3), eligible.Count);
            var proposals = new List<MonsterProposal>();
            for (int i = 0; i < count; i++)
            {
                double weight = 0; foreach (var monster in eligible) weight += monster.EncounterWeight;
                double roll = random.Next(1000000) / 1000000.0 * weight;
                int selected = eligible.Count - 1;
                for (int j = 0; j < eligible.Count; j++)
                { roll -= eligible[j].EncounterWeight; if (roll < 0) { selected = j; break; } }
                var entry = eligible[selected]; eligible.RemoveAt(selected);
                // Each monster submits independently; it never sees another monster's claimed slots.
                proposals.Add(Propose(stage, entry.Id, entry, Hash(seed, entry.Id)));
            }
            return Resolve(stage, proposals, Hash(seed, "ties"));
        }

        private static List<PatternPlacement> Candidates(MusicStage stage, MonsterPatternDefinition pattern)
        {
            var result = new List<PatternPlacement>();
            foreach (var candidate in stage.FindPlacements(pattern.Pattern))
                if (candidate.CueStartTick % pattern.CueAlignmentTicks == 0 &&
                    pattern.ResponseTicks <= stage.Music.TotalTicks - candidate.StartTick) result.Add(candidate);
            return result;
        }

        public static MonsterProposal Propose(MusicStage stage, string instanceId, MonsterDefinition monster, int seed)
        {
            if (stage == null || monster == null) throw new ArgumentNullException(stage == null ? nameof(stage) : nameof(monster));
            var placements = new List<PatternPlacement>();
            foreach (var pattern in monster.Patterns)
                placements.AddRange(ProposePattern(pattern, Candidates(stage, pattern), new SeededRandom(Hash(seed, pattern.Id))));
            return new MonsterProposal(instanceId, monster, placements);
        }

        private static List<PatternPlacement> ProposePattern(MonsterPatternDefinition pattern, List<PatternPlacement> candidates, SeededRandom random)
        {
            var placements = new List<PatternPlacement>();
            long responseAfter = 0, callAfter = 0;
            foreach (var candidate in candidates)
            {
                if (candidate.StartTick < responseAfter || candidate.CueStartTick < callAfter) continue;
                // Increase chance smoothly with weight while retaining a chance to skip highlights.
                double chance = 1 - Math.Pow(1 - pattern.ParticipationChance, candidate.Weight);
                if (random.Next(1000000) / 1000000.0 >= chance) continue;
                placements.Add(candidate);
                callAfter = (long)candidate.StartTick + pattern.ResponseTicks;
                responseAfter = callAfter + pattern.RestTicks;
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
            return placements;
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
                    var pattern = proposal.Monster.FindPattern(placement.Pattern);
                    if (placement.CueStartTick < 0 || placement.CueStartTick % pattern.CueAlignmentTicks != 0 ||
                        pattern.ResponseTicks > stage.Music.TotalTicks - placement.StartTick)
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
                // Resolve the earliest gesture or physical conflict; stable instance ordering breaks search ties.
                int firstOwner = -1, secondOwner = -1, firstAttack = -1, secondAttack = -1, earliest = int.MaxValue;
                for (int a = 0; a < active.Count; a++) for (int b = a; b < active.Count; b++)
                    for (int x = 0; x < active[a].Count; x++) for (int y = a == b ? x + 1 : 0; y < active[b].Count; y++)
                        if (Conflicts(active[a][x], active[b][y], out int tick) && tick < earliest)
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

        public static bool Conflicts(PlannedAttack left, PlannedAttack right, out int tick)
        {
            if (left == null || right == null) throw new ArgumentNullException(left == null ? nameof(left) : nameof(right));
            if (left.MonsterId != right.MonsterId)
                return InputCompatibility.Conflict(left.Placement, right.Placement, out tick);
            // A single actor cannot cue a second pattern during its current Call/Response.
            // As before, the next Call may use the rest, but its Response must wait until rest ends.
            if (left.CallStartTick > right.CallStartTick) { var swap = left; left = right; right = swap; }
            if (right.CallStartTick < left.PhraseEndTick || right.ResponseStartTick < (long)left.PhraseEndTick + left.Pattern.RestTicks)
            { tick = right.CallStartTick; return true; }
            tick = -1; return false;
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
