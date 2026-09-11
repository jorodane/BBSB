using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    /// <summary>Add complete, compatible phrases after conflict resolution without moving accepted attacks.</summary>
    public static class BattleGapFiller
    {
        public const int MinimumGapTicks = 4 * RhythmTime.TicksPerBeat;

        private sealed class CandidatePattern
        {
            public MonsterPlan Owner;
            public string Key;
            public List<PatternChain> Chains = new List<PatternChain>();
            public int Occurrences;
        }

        public static BattlePlan Fill(BattlePlan plan, int seed)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var attacks = new List<PlannedAttack>(plan.Attacks);
            var fills = new List<PlannedAttack>(plan.GapFills);
            var candidates = new List<CandidatePattern>();
            foreach (var owner in plan.Monsters)
            {
                var options = new Dictionary<string, CandidatePattern>();
                foreach (var chain in owner.Monster.PatternPlanner.Candidates(plan.Stage, owner.Monster))
                {
                    if (!options.TryGetValue(chain.Key, out var option))
                    { option = new CandidatePattern { Owner = owner, Key = chain.Key }; options.Add(chain.Key, option); }
                    option.Chains.Add(chain);
                }
                foreach (var option in options.Values)
                {
                    var counted = new HashSet<PatternChain>();
                    foreach (var attack in owner.Attacks)
                        if ((attack.Chain?.Key ?? attack.Pattern.Id) == option.Key && (attack.Chain == null || counted.Add(attack.Chain)))
                            option.Occurrences++;
                    option.Chains.Sort((a, b) => a.CallStartTick.CompareTo(b.CallStartTick));
                    candidates.Add(option);
                }
            }
            // Random ties must not depend on catalog order or how the proposal list was enumerated.
            candidates.Sort((a, b) =>
            {
                int owner = string.CompareOrdinal(a.Owner.InstanceId, b.Owner.InstanceId);
                return owner != 0 ? owner : string.CompareOrdinal(a.Key, b.Key);
            });
            var random = new SeededRandom(seed);
            while (true)
            {
                bool added = false;
                foreach (var gap in Gaps(plan.Stage, attacks))
                {
                    var choices = new List<(CandidatePattern candidate, List<PlannedAttack> attacks)>();
                    int shortest = int.MaxValue, mostFrequent = -1;
                    foreach (var candidate in candidates)
                    {
                        var bundle = FirstFit(candidate, gap, attacks);
                        if (bundle == null) continue;
                        int length = bundle[bundle.Count - 1].PhraseEndTick - bundle[0].CallStartTick;
                        if (length > shortest || (length == shortest && candidate.Occurrences < mostFrequent)) continue;
                        if (length < shortest || candidate.Occurrences > mostFrequent)
                        { choices.Clear(); shortest = length; mostFrequent = candidate.Occurrences; }
                        choices.Add((candidate, bundle));
                    }
                    if (choices.Count == 0) continue; // Leave an unfillable gap and still try the next one.
                    var choice = choices[choices.Count == 1 ? 0 : random.Next(choices.Count)];
                    attacks.AddRange(choice.attacks); fills.AddRange(choice.attacks); choice.candidate.Occurrences++;
                    added = true;
                    break; // Recompute both the remaining gaps and popularity after every insertion.
                }
                // Every insertion occupies a positive interval of a finite song, so this also terminates on sparse scores.
                if (!added) break;
            }
            if (fills.Count == plan.GapFills.Count) return plan;
            attacks.Sort((a, b) => a.ResponseStartTick != b.ResponseStartTick ? a.ResponseStartTick.CompareTo(b.ResponseStartTick) :
                string.CompareOrdinal(a.Id, b.Id));
            var grid = BattlePlanner.BeatGrid(plan.Stage); var monsters = new List<MonsterPlan>();
            foreach (var owner in plan.Monsters)
            {
                var owned = attacks.FindAll(x => x.MonsterId == owner.InstanceId);
                monsters.Add(new MonsterPlan(owner.InstanceId, owner.Monster, owner.ProposedCount, owned,
                    BattlePlanner.CountOccupied(owned, grid)));
            }
            return new BattlePlan(plan.Stage, monsters, new List<PlanWithdrawal>(plan.Withdrawals), fills);
        }

        private static List<PlannedAttack> FirstFit(CandidatePattern candidate, (int start, int end) gap, List<PlannedAttack> attacks)
        {
            List<PlannedAttack> best = null; int shortest = int.MaxValue;
            foreach (var chain in candidate.Chains)
            {
                if (chain.CallStartTick < gap.start) continue;
                if (chain.CallStartTick >= gap.end) break;
                if (chain.PhraseEndTick > gap.end) continue;
                int length = chain.PhraseEndTick - chain.CallStartTick;
                if (length >= shortest) continue; // Equal lengths keep the earliest available placement.
                var bundle = new List<PlannedAttack>();
                foreach (var placement in chain.Placements)
                    bundle.Add(new PlannedAttack(candidate.Owner.InstanceId, candidate.Owner.Monster, placement, chain));
                bool fits = true;
                foreach (var proposed in bundle)
                {
                    foreach (var existing in attacks)
                        if (BattlePlanner.Conflicts(proposed, existing, out _)) { fits = false; break; }
                    if (!fits) break;
                }
                if (fits) { best = bundle; shortest = length; }
            }
            return best;
        }

        private static List<(int start, int end)> Gaps(MusicStage stage, List<PlannedAttack> attacks)
        {
            var occupied = new List<(int start, int end)>();
            // A phrase's internal rests, long Holds, and the silence while identifying a Call are intentional.
            foreach (var attack in attacks)
                occupied.Add((attack.Chain?.CallStartTick ?? attack.CallStartTick, attack.Chain?.PhraseEndTick ?? attack.PhraseEndTick));
            foreach (var section in stage.Music.Sections)
                if (!section.AllowsResponse) occupied.Add((section.StartBar * stage.Music.TicksPerBar,
                    (section.StartBar + section.BarCount) * stage.Music.TicksPerBar));
            occupied.Sort((a, b) => a.start != b.start ? a.start.CompareTo(b.start) : a.end.CompareTo(b.end));
            var result = new List<(int start, int end)>(); int cursor = 0;
            foreach (var interval in occupied)
            {
                if (interval.start - cursor >= MinimumGapTicks) result.Add((cursor, interval.start));
                cursor = Math.Max(cursor, interval.end);
            }
            if (stage.Music.TotalTicks - cursor >= MinimumGapTicks) result.Add((cursor, stage.Music.TotalTicks));
            return result;
        }
    }
}
