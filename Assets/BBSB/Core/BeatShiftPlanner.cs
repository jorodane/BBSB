using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public sealed class BeatShiftPlanner : IMonsterPatternPlanner
    {
        public int SteadyCallsPerPhase { get; }
        public BeatShiftPlanner(int steadyCallsPerPhase = 3)
        {
            if (steadyCallsPerPhase < 1 || steadyCallsPerPhase > 16) throw new ArgumentOutOfRangeException(nameof(steadyCallsPerPhase));
            SteadyCallsPerPhase = steadyCallsPerPhase;
        }

        public void Validate(IReadOnlyList<MonsterPatternDefinition> patterns)
        {
            if (patterns.Count != 2) throw new ArgumentException("A beat-shift loop needs a steady Tap and a two-Tap transition.");
            int steady = 0, shift = 0;
            foreach (var pattern in patterns)
            {
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

        public IReadOnlyList<PatternChain> Candidates(MusicStage stage, MonsterDefinition monster)
        {
            var result = new List<PatternChain>();
            var steady = monster.Patterns[monster.Patterns[0].Pattern.Steps.Count == 1 ? 0 : 1];
            var shift = monster.Patterns[monster.Patterns[0].Pattern.Steps.Count == 2 ? 0 : 1];
            var steadyAt = Index(stage, steady); var shiftAt = Index(stage, shift);
            // Start on a main beat, establish it, shift offbeat, then establish the returned main beat.
            // Every next Call coincides with the previous pattern's final Tap, including each early Tap.
            foreach (var first in BattlePlanner.Candidates(stage, steady))
            {
                if (first.CueStartTick % RhythmTime.TicksPerBeat != 0) continue;
                var placements = new List<PatternPlacement>(); int call = first.CueStartTick; bool fits = true;
                for (int phase = 0; phase < 3 && fits; phase++)
                {
                    for (int beat = 0; beat < SteadyCallsPerPhase; beat++)
                    {
                        if (!steadyAt.TryGetValue(call + 4, out var placement)) { fits = false; break; }
                        placements.Add(placement); call = placement.EndTick;
                    }
                    if (phase == 2 || !fits) continue;
                    if (!shiftAt.TryGetValue(call + 4, out var transition)) { fits = false; break; }
                    placements.Add(transition); call = transition.EndTick;
                }
                if (!fits) continue;
                int end = placements[placements.Count - 1].StartTick + steady.ResponseTicks;
                // The chain may count in during an intro, but it cannot bridge a response-free break.
                int firstResponse = placements[0].StartTick;
                foreach (var section in stage.Music.Sections)
                    if (!section.AllowsResponse && firstResponse < (section.StartBar + section.BarCount) * stage.Music.TicksPerBar &&
                        end > section.StartBar * stage.Music.TicksPerBar) { fits = false; break; }
                if (fits) result.Add(new PatternChain("beat-shift-loop", monster, placements, RhythmTime.TicksPerBeat,
                    overlapCallsAndResponses: true));
            }
            return result;
        }

        public MonsterProposal Propose(MusicStage stage, string instanceId, MonsterDefinition monster, int seed)
        {
            var candidates = Candidates(stage, monster); var result = new List<PatternChain>();
            var random = new SeededRandom(seed); long callAfter = 0;
            var steady = monster.Patterns[monster.Patterns[0].Pattern.Steps.Count == 1 ? 0 : 1];
            foreach (var candidate in candidates)
            {
                if (candidate.CallStartTick < callAfter) continue;
                double chance = 1 - Math.Pow(1 - steady.ParticipationChance, candidate.Placements[0].Weight);
                if (random.Next(1000000) / 1000000.0 >= chance) continue;
                result.Add(candidate);
                // The next one-beat Call can use the final one-beat rest.
                callAfter = candidate.PhraseEndTick;
            }
            if (result.Count == 0 && candidates.Count > 0) result.Add(candidates[random.Next(candidates.Count)]);
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
