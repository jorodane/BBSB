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
                if (monster.PatternPlanner.Candidates(stage, monster).Count > 0) eligible.Add(monster);
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
            var resolved = Resolve(stage, proposals, Hash(seed, "ties"));
            return BattleGapFiller.Fill(resolved, Hash(seed, "gap-fill"));
        }

        internal static List<PatternPlacement> Candidates(MusicStage stage, MonsterPatternDefinition pattern)
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
            return monster.PatternPlanner.Propose(stage, instanceId, monster, seed);
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
                var lost = active[loser][index];
                var removed = lost.Chain == null ? new List<PlannedAttack> { lost } : active[loser].FindAll(x => ReferenceEquals(x.Chain, lost.Chain));
                foreach (var attack in removed)
                {
                    withdrawals.Add(new PlanWithdrawal(attack, proposals[winner].InstanceId, earliest,
                        leftYields ? leftCount : rightCount, leftYields ? rightCount : leftCount));
                    active[loser].Remove(attack); // Dependent phase changes and their following Calls leave together.
                }
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
            if (left.Chain != null && ReferenceEquals(left.Chain, right.Chain))
                return InputCompatibility.Conflict(left.Placement, right.Placement, out tick);
            // A single actor cannot cue a second pattern during its current Call/Response.
            // As before, the next Call may use the rest, but its Response must wait until rest ends.
            int leftStart = left.Chain?.CallStartTick ?? left.CallStartTick, rightStart = right.Chain?.CallStartTick ?? right.CallStartTick;
            if (leftStart > rightStart) { var swap = left; left = right; right = swap; rightStart = leftStart; }
            int end = left.Chain?.PhraseEndTick ?? left.PhraseEndTick;
            int rest = left.Chain?.RestTicks ?? left.Pattern.RestTicks;
            int response = right.Chain == null ? right.ResponseStartTick : right.Chain.Placements[0].StartTick;
            if (rightStart < end || response < (long)end + rest)
            { tick = rightStart; return true; }
            tick = -1; return false;
        }

        // Count occupied positions in this song, not slot alternatives, attack instances or Call pulses.
        // Held intervals occupy [start, end); an explicit release also occupies its ending position.
        internal static SortedSet<int> BeatGrid(MusicStage stage)
        {
            var grid = new SortedSet<int>();
            foreach (var slot in stage.Slots)
            {
                grid.Add(slot.StartTick);
                if (slot.Touch.End == TouchTransition.Release) grid.Add(slot.EndTick);
            }
            return grid;
        }

        internal static int CountOccupied(List<PlannedAttack> attacks, SortedSet<int> grid)
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

        internal static int Hash(int seed, string id)
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
