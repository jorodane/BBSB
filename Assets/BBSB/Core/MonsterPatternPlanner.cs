using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    /// <summary>A monster may evaluate all its patterns together before submitting its own placement plan.</summary>
    public interface IMonsterPatternPlanner
    {
        void Validate(IReadOnlyList<MonsterPatternDefinition> patterns);
        IReadOnlyList<PatternChain> Candidates(MusicStage stage, MonsterDefinition monster);
        MonsterProposal Propose(MusicStage stage, string instanceId, MonsterDefinition monster, int seed);
    }

    /// <summary>An atomic placement option. Multi-pattern chains retain their dependencies during arbitration and filling.</summary>
    public sealed class PatternChain
    {
        public string Key { get; }
        public MonsterDefinition Monster { get; }
        public IReadOnlyList<PatternPlacement> Placements { get; }
        public int CallStartTick => Placements[0].CueStartTick;
        public int PhraseEndTick { get; }
        public int RestTicks { get; }
        public bool OverlapCallsAndResponses { get; }

        public PatternChain(string key, MonsterDefinition monster, IEnumerable<PatternPlacement> placements, int restTicks,
            bool overlapCallsAndResponses = false)
        {
            if (string.IsNullOrWhiteSpace(key) || restTicks < 0) throw new ArgumentException("Invalid sequence identity or rest.");
            if (monster == null || placements == null) throw new ArgumentNullException(monster == null ? nameof(monster) : nameof(placements));
            var copy = new List<PatternPlacement>(placements);
            if (copy.Count == 0 || copy.Exists(x => x == null)) throw new ArgumentException("A sequence needs complete placements.");
            copy.Sort((a, b) => a.CueStartTick.CompareTo(b.CueStartTick));
            for (int i = 0; i < copy.Count; i++)
            {
                var current = copy[i]; var definition = monster.FindPattern(current.Pattern);
                long end = (long)current.StartTick + definition.ResponseTicks;
                if (end > int.MaxValue || current.CueStartTick < 0 || current.CueStartTick % definition.CueAlignmentTicks != 0)
                    throw new ArgumentException("Invalid sequence timing.");
                PhraseEndTick = Math.Max(PhraseEndTick, (int)end);
                if (i == 0) continue;
                var previous = copy[i - 1]; var previousDefinition = monster.FindPattern(previous.Pattern);
                int previousEnd = previous.StartTick + previousDefinition.ResponseTicks;
                int earliestCall = overlapCallsAndResponses ? previous.EndTick : previousEnd;
                if (current.CueStartTick <= previous.CueStartTick || current.CueStartTick < earliestCall ||
                    current.StartTick < (long)previousEnd + previousDefinition.RestTicks || InputCompatibility.Conflict(previous, current, out _))
                    throw new ArgumentException("Linked Calls and Responses must preserve timing and touch requirements.");
            }
            if (copy.Count == 1 && key != copy[0].Pattern.Id)
                throw new ArgumentException("A single-pattern option uses its pattern ID as its key.");
            int finalRest = monster.FindPattern(copy[copy.Count - 1].Pattern).RestTicks;
            if (restTicks < finalRest || (copy.Count == 1 && restTicks != finalRest))
                throw new ArgumentException("A sequence must preserve its final pattern's rest; single patterns use their authored rest.");
            Key = key; Monster = monster; Placements = copy.AsReadOnly(); RestTicks = restTicks;
            OverlapCallsAndResponses = overlapCallsAndResponses;
        }
    }

    /// <summary>The default strategy preserves independent proposals and per-pattern seeded randomness.</summary>
    public sealed class IndependentPatternPlanner : IMonsterPatternPlanner
    {
        public static IndependentPatternPlanner Instance { get; } = new IndependentPatternPlanner();
        public void Validate(IReadOnlyList<MonsterPatternDefinition> patterns) { }

        public IReadOnlyList<PatternChain> Candidates(MusicStage stage, MonsterDefinition monster)
        {
            var result = new List<PatternChain>();
            foreach (var pattern in monster.Patterns)
                foreach (var placement in BattlePlanner.Candidates(stage, pattern))
                    result.Add(new PatternChain(pattern.Id, monster, new[] { placement }, pattern.RestTicks));
            return result;
        }

        public MonsterProposal Propose(MusicStage stage, string instanceId, MonsterDefinition monster, int seed)
        {
            var placements = new List<PatternPlacement>();
            foreach (var pattern in monster.Patterns)
                placements.AddRange(ProposePattern(pattern, BattlePlanner.Candidates(stage, pattern),
                    new SeededRandom(BattlePlanner.Hash(seed, pattern.Id))));
            return new MonsterProposal(instanceId, monster, placements);
        }

        private static List<PatternPlacement> ProposePattern(MonsterPatternDefinition pattern, List<PatternPlacement> candidates, SeededRandom random)
        {
            var placements = new List<PatternPlacement>(); long responseAfter = 0, callAfter = 0;
            foreach (var candidate in candidates)
            {
                if (candidate.StartTick < responseAfter || candidate.CueStartTick < callAfter) continue;
                double chance = 1 - Math.Pow(1 - pattern.ParticipationChance, candidate.Weight);
                if (random.Next(1000000) / 1000000.0 >= chance) continue;
                placements.Add(candidate); callAfter = (long)candidate.StartTick + pattern.ResponseTicks;
                responseAfter = callAfter + pattern.RestTicks;
            }
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
    }
}
