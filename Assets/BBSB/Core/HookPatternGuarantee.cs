using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    /// <summary>Reserve complete, compatible monster phrases in every authored hook, after arbitration.</summary>
    public static class HookPatternGuarantee
    {
        public static bool Covers(BattlePlan plan, MusicSection hook)
        {
            int start = hook.StartBar * plan.Stage.Music.TicksPerBar;
            int end = (hook.StartBar + hook.BarCount) * plan.Stage.Music.TicksPerBar;
            foreach (var attack in plan.Attacks)
                if (attack.ResponseStartTick >= start && attack.ResponseStartTick < end && attack.PhraseEndTick <= end) return true;
            return false;
        }

        public static bool Eligible(MusicStage stage, IReadOnlyList<PatternChain> candidates)
        {
            foreach (var hook in stage.Music.Sections)
            {
                if (!hook.IsHook) continue;
                bool found = false;
                foreach (var chain in candidates) if (FitsHook(stage, chain, hook)) { found = true; break; }
                if (!found) return false;
            }
            return candidates.Count > 0;
        }

        private static bool FitsHook(MusicStage stage, PatternChain chain, MusicSection hook)
        {
            int start = hook.StartBar * stage.Music.TicksPerBar;
            int end = (hook.StartBar + hook.BarCount) * stage.Music.TicksPerBar;
            foreach (var placement in chain.Placements)
                if (placement.StartTick >= start && placement.StartTick < end &&
                    placement.StartTick + chain.Monster.FindPattern(placement.Pattern).ResponseTicks <= end) return true;
            return false;
        }

        public static BattlePlan Ensure(BattlePlan plan, int seed)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var hooks = new List<MusicSection>(); bool missing = false;
            foreach (var section in plan.Stage.Music.Sections)
                if (section.IsHook) { hooks.Add(section); missing |= !Covers(plan, section); }
            if (!missing) return plan;
            var choices = new List<List<List<PlannedAttack>>>();
            var random = new SeededRandom(seed);
            foreach (var hook in hooks)
            {
                var options = new List<List<PlannedAttack>>();
                foreach (var owner in plan.Monsters)
                    foreach (var chain in owner.Monster.PatternPlanner.Candidates(plan.Stage, owner.Monster))
                    {
                        if (!FitsHook(plan.Stage, chain, hook)) continue;
                        var bundle = new List<PlannedAttack>();
                        foreach (var placement in chain.Placements)
                            bundle.Add(new PlannedAttack(owner.InstanceId, owner.Monster, placement, chain));
                        options.Add(bundle);
                    }
                random.Shuffle(options);
                // Prefer the beginning of a hook, but retain alternate placements for dependency/rest conflicts.
                options.Sort((a, b) => a[0].ResponseStartTick.CompareTo(b[0].ResponseStartTick));
                choices.Add(options);
            }
            var reserved = new List<PlannedAttack>();
            if (!Reserve(choices, hooks, plan.Stage.Music.TicksPerBar, 0, reserved))
                throw new InvalidOperationException("This monster roster cannot provide a complete response in every hook of " + plan.Stage.Music.Id);

            var attacks = new List<PlannedAttack>(plan.Attacks);
            var withdrawals = new List<PlanWithdrawal>(plan.Withdrawals);
            var removed = new HashSet<PlannedAttack>();
            var grid = BattlePlanner.BeatGrid(plan.Stage);
            foreach (var existing in attacks) foreach (var required in reserved)
            {
                if (!BattlePlanner.Conflicts(existing, required, out int tick)) continue;
                // Linked Calls and Responses are never cut into independently playable fragments.
                foreach (var member in attacks)
                    if (ReferenceEquals(member, existing) || existing.Chain != null && ReferenceEquals(member.Chain, existing.Chain))
                        if (removed.Add(member)) withdrawals.Add(new PlanWithdrawal(member, required.MonsterId, tick,
                            BattlePlanner.CountOccupied(attacks.FindAll(x => x.MonsterId == member.MonsterId), grid),
                            BattlePlanner.CountOccupied(reserved.FindAll(x => x.MonsterId == required.MonsterId), grid)));
                break;
            }
            attacks.RemoveAll(x => removed.Contains(x)); attacks.AddRange(reserved);
            var fills = new List<PlannedAttack>();
            foreach (var fill in plan.GapFills) if (!removed.Contains(fill)) fills.Add(fill);
            var owners = new List<MonsterPlan>();
            foreach (var owner in plan.Monsters)
            {
                var owned = attacks.FindAll(x => x.MonsterId == owner.InstanceId);
                if (owned.Count > 0) owners.Add(new MonsterPlan(owner.InstanceId, owner.Monster, owner.ProposedCount, owned,
                    BattlePlanner.CountOccupied(owned, grid)));
            }
            var result = new BattlePlan(plan.Stage, owners, withdrawals, fills);
            foreach (var hook in hooks) if (!Covers(result, hook)) throw new InvalidOperationException("Missing reserved hook response.");
            return result;
        }

        private static bool Reserve(List<List<List<PlannedAttack>>> choices, List<MusicSection> hooks, int ticksPerBar, int index, List<PlannedAttack> reserved)
        {
            if (index == choices.Count) return true;
            int start = hooks[index].StartBar * ticksPerBar;
            int end = (hooks[index].StartBar + hooks[index].BarCount) * ticksPerBar;
            foreach (var attack in reserved)
                if (attack.ResponseStartTick >= start && attack.ResponseStartTick < end && attack.PhraseEndTick <= end)
                    return Reserve(choices, hooks, ticksPerBar, index + 1, reserved);
            foreach (var bundle in choices[index])
            {
                bool compatible = true;
                foreach (var proposed in bundle) foreach (var previous in reserved)
                    if (BattlePlanner.Conflicts(proposed, previous, out _)) compatible = false;
                if (!compatible) continue;
                int count = reserved.Count; reserved.AddRange(bundle);
                if (Reserve(choices, hooks, ticksPerBar, index + 1, reserved)) return true;
                reserved.RemoveRange(count, reserved.Count - count);
            }
            return false;
        }
    }
}
