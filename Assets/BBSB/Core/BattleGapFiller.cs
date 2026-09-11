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
            public MonsterPatternDefinition Pattern;
            public List<PatternPlacement> Placements;
            public int Occurrences;
        }

        public static BattlePlan Fill(BattlePlan plan, int seed)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var attacks = new List<PlannedAttack>(plan.Attacks);
            var fills = new List<PlannedAttack>(plan.GapFills);
            var candidates = new List<CandidatePattern>();
            foreach (var owner in plan.Monsters) foreach (var pattern in owner.Monster.Patterns)
            {
                int count = 0; foreach (var attack in owner.Attacks) if (attack.Pattern == pattern) count++;
                candidates.Add(new CandidatePattern { Owner = owner, Pattern = pattern, Occurrences = count,
                    Placements = BattlePlanner.Candidates(plan.Stage, pattern) });
            }
            // Random ties must not depend on catalog order or how the proposal list was enumerated.
            candidates.Sort((a, b) =>
            {
                int owner = string.CompareOrdinal(a.Owner.InstanceId, b.Owner.InstanceId);
                return owner != 0 ? owner : string.CompareOrdinal(a.Pattern.Id, b.Pattern.Id);
            });
            var random = new SeededRandom(seed);
            while (true)
            {
                bool added = false;
                foreach (var gap in Gaps(plan.Stage, attacks))
                {
                    var choices = new List<(CandidatePattern candidate, PlannedAttack attack)>();
                    int shortest = int.MaxValue, mostFrequent = -1;
                    foreach (var candidate in candidates)
                    {
                        var attack = FirstFit(candidate, gap, attacks);
                        if (attack == null) continue;
                        int length = attack.PhraseEndTick - attack.CallStartTick;
                        if (length > shortest || (length == shortest && candidate.Occurrences < mostFrequent)) continue;
                        if (length < shortest || candidate.Occurrences > mostFrequent)
                        { choices.Clear(); shortest = length; mostFrequent = candidate.Occurrences; }
                        choices.Add((candidate, attack));
                    }
                    if (choices.Count == 0) continue; // Leave an unfillable gap and still try the next one.
                    var choice = choices[choices.Count == 1 ? 0 : random.Next(choices.Count)];
                    attacks.Add(choice.attack); fills.Add(choice.attack); choice.candidate.Occurrences++;
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

        private static PlannedAttack FirstFit(CandidatePattern candidate, (int start, int end) gap, List<PlannedAttack> attacks)
        {
            foreach (var placement in candidate.Placements)
            {
                if (placement.CueStartTick < gap.start) continue;
                if (placement.CueStartTick >= gap.end) break;
                if (candidate.Pattern.ResponseTicks > gap.end - placement.StartTick) continue;
                var proposed = new PlannedAttack(candidate.Owner.InstanceId, candidate.Owner.Monster, placement);
                bool fits = true;
                foreach (var existing in attacks)
                    if (BattlePlanner.Conflicts(proposed, existing, out _)) { fits = false; break; }
                if (fits) return proposed;
            }
            return null;
        }

        private static List<(int start, int end)> Gaps(MusicStage stage, List<PlannedAttack> attacks)
        {
            var occupied = new List<(int start, int end)>();
            // A phrase's internal rests, long Holds, and the silence while identifying a Call are intentional.
            foreach (var attack in attacks) occupied.Add((attack.CallStartTick, attack.PhraseEndTick));
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
