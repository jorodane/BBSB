using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public sealed class BeatShiftPlanner : IMonsterPatternPlanner
    {
        public int SteadyCallsPerPhase { get; }
        public bool UsePatternProbabilities { get; }
        public BeatShiftPlanner(int steadyCallsPerPhase = 3, bool usePatternProbabilities = false)
        {
            if (steadyCallsPerPhase < 1 || steadyCallsPerPhase > 16) throw new ArgumentOutOfRangeException(nameof(steadyCallsPerPhase));
            SteadyCallsPerPhase = steadyCallsPerPhase; UsePatternProbabilities = usePatternProbabilities;
        }

        public void Validate(IReadOnlyList<MonsterPatternDefinition> patterns)
        {
            if (patterns.Count < 2) throw new ArgumentException("A beat-shift loop needs a steady Tap and a two-Tap transition.");
            int steady = 0, shift = 0;
            foreach (var pattern in patterns)
            {
                if (patterns.Count > 2 && !IsCadence(pattern)) continue;
                if (pattern.Pattern.CueLeadTicks != 4 || pattern.ResponseTicks != 4 || pattern.RestTicks != 0 ||
                    pattern.SilentWaitTicks != 0 || pattern.CueAlignmentTicks != 2 || pattern.Call.Count != 1)
                    throw new ArgumentException("Linked beat cues need a one-beat lead, half-beat alignment and no per-pattern rest.");
                foreach (var step in pattern.Pattern.Steps)
                    if (step.Kind != GestureKind.Tap) throw new ArgumentException("Beat-shift loops use Tap only.");
                if (pattern.Pattern.Steps.Count == 1) steady++;
                else if (pattern.Pattern.Steps.Count == 2 && pattern.Pattern.Steps[1].OffsetTick == 2) shift++;
                else throw new ArgumentException("A transition must finish half a beat after its first Tap.");
            }
            if (steady != 1 || shift != 1) throw new ArgumentException("A beat-shift loop needs both pattern types.");
        }

        private static bool IsCadence(MonsterPatternDefinition p) => p.Pattern.CueLeadTicks == 4 &&
            p.ResponseTicks == 4 && p.RestTicks == 0 && p.SilentWaitTicks == 0 && p.CueAlignmentTicks == 2 && p.Call.Count == 1 &&
            p.Pattern.Steps[0].Kind == GestureKind.Tap && (p.Pattern.Steps.Count == 1 ||
            p.Pattern.Steps.Count == 2 && p.Pattern.Steps[1].Kind == GestureKind.Tap && p.Pattern.Steps[1].OffsetTick == 2);
        private static MonsterPatternDefinition Cadence(MonsterDefinition monster, int steps)
        {
            foreach (var pattern in monster.Patterns) if (IsCadence(pattern) && pattern.Pattern.Steps.Count == steps) return pattern;
            throw new ArgumentException("Missing beat-shift cadence.");
        }
        public IReadOnlyList<PatternChain> Candidates(MusicStage stage, MonsterDefinition monster)
        {
            var result = new List<PatternChain>(Candidates(stage, monster, null));
            foreach (var pattern in monster.Patterns) if (!IsCadence(pattern))
                foreach (var placement in BattlePlanner.Candidates(stage, pattern))
                    result.Add(new PatternChain(pattern.Id, monster, new[] { placement }, pattern.RestTicks));
            return result;
        }

        private IReadOnlyList<PatternChain> Candidates(MusicStage stage, MonsterDefinition monster, int? seed)
        {
            var result = new List<PatternChain>();
            var steady = Cadence(monster, 1);
            var shift = Cadence(monster, 2);
            var steadyAt = Index(stage, steady); var shiftAt = Index(stage, shift);
            // One uninterrupted run per available span. Every next Call coincides with the
            // previous final Tap; extending the run never inserts a new count-in or rest.
            foreach (var first in BattlePlanner.Candidates(stage, steady))
            {
                if (first.CueStartTick % RhythmTime.TicksPerBeat != 0) continue;
                var random = new SeededRandom(BattlePlanner.Hash(seed ?? 0, "beat-shift@" + first.CueStartTick));
                var placements = new List<PatternPlacement>(); int call = first.CueStartTick, steadyCount = 0;
                int transitions = 0, returnedSteady = 0;
                while (true)
                {
                    bool transition = steadyCount >= SteadyCallsPerPhase &&
                        (!seed.HasValue || random.Next(1000000) / 1000000.0 < shift.ParticipationChance);
                    PatternPlacement placement;
                    if (!transition || !shiftAt.TryGetValue(call + 4, out placement) || !steadyAt.ContainsKey(placement.EndTick + 4))
                    {
                        transition = false;
                        if (!steadyAt.TryGetValue(call + 4, out placement)) break;
                    }
                    var definition = transition ? shift : steady;
                    int end = placement.StartTick + definition.ResponseTicks;
                    bool crossesBreak = false;
                    foreach (var section in stage.Music.Sections)
                        if (!section.AllowsResponse && first.StartTick < (section.StartBar + section.BarCount) * stage.Music.TicksPerBar &&
                            end > section.StartBar * stage.Music.TicksPerBar) { crossesBreak = true; break; }
                    if (crossesBreak) break;
                    placements.Add(placement); call = placement.EndTick;
                    steadyCount = transition ? 0 : steadyCount + 1;
                    if (transition) transitions++;
                    else if (transitions == 2) returnedSteady++;
                }
                // Keep the original teaching minimum: establish both phases and return to main beats.
                if (returnedSteady >= SteadyCallsPerPhase)
                    result.Add(new PatternChain("beat-shift-loop", monster, placements, 0,
                    overlapCallsAndResponses: true));
            }
            return result;
        }

        public MonsterProposal Propose(MusicStage stage, string instanceId, MonsterDefinition monster, int seed)
        {
            var candidates = Candidates(stage, monster, UsePatternProbabilities ? (int?)seed : null);
            var result = new List<PatternChain>();
            var steady = Cadence(monster, 1);
            var random = new SeededRandom(BattlePlanner.Hash(seed, "beat-shift-entry"));
            long callAfter = 0;
            foreach (var candidate in candidates)
            {
                if (candidate.CallStartTick < callAfter) continue;
                if (UsePatternProbabilities && random.Next(1000000) / 1000000.0 >= steady.ParticipationChance) continue;
                result.Add(candidate);
                // Later candidates in the same span are suffixes, not separate random bursts.
                callAfter = candidate.PhraseEndTick;
            }
            // As with independent patterns, a playable species gets one fallback phrase.
            if (result.Count == 0)
            {
                var fallback = Candidates(stage, monster, null);
                if (fallback.Count > 0) result.Add(fallback[0]);
            }
            // Independent signatures compete as whole phrases with the cadence;
            // the old linked sequence still cannot be cut into isolated taps.
            if (monster.Patterns.Count > 2)
                foreach (var placement in IndependentPatternPlanner.Instance.Propose(stage, instanceId, monster, seed).Placements)
                {
                    var pattern = monster.FindPattern(placement.Pattern);
                    if (!IsCadence(pattern)) result.Add(new PatternChain(pattern.Id, monster, new[] { placement }, pattern.RestTicks));
                }
            return new MonsterProposal(instanceId, monster, result);
        }

        private static Dictionary<int, PatternPlacement> Index(MusicStage stage, MonsterPatternDefinition pattern)
        {
            var result = new Dictionary<int, PatternPlacement>();
            foreach (var placement in BattlePlanner.Candidates(stage, pattern)) result.Add(placement.StartTick, placement);
            return result;
        }
    }
}
