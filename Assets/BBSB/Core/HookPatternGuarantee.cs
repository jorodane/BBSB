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
            bool hasSignatures = false;
            foreach (var owner in plan.Monsters) foreach (var pattern in owner.Monster.Patterns) hasSignatures |= pattern.IsHook;
            if (hooks.Count == 0 || !missing && !hasSignatures) return plan;
            var choices = new List<List<List<PlannedAttack>>>();
            var random = new SeededRandom(seed);
            var acceptedById = new Dictionary<string, PlannedAttack>();
            foreach (var attack in plan.Attacks) acceptedById[attack.Id] = attack;
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
                        // Reuse a whole already accepted bundle when it matches, so
                        // reserving a signature does not withdraw and duplicate itself.
                        var existingBundle = new List<PlannedAttack>();
                        foreach (var proposed in bundle)
                            if (acceptedById.TryGetValue(proposed.Id, out var accepted)) existingBundle.Add(accepted);
                        if (existingBundle.Count == bundle.Count) bundle = existingBundle;
                        options.Add(bundle);
                    }
                // Prefer each species' longer signature when this score can fit it;
                // short hooks and older authored monsters retain the existing fallback.
                random.Shuffle(options);
                // Prefer the beginning of a hook, but retain alternate placements for dependency/rest conflicts.
                options.Sort((a, b) =>
                {
                    int signature = b.Exists(x => x.Pattern.IsHook).CompareTo(a.Exists(x => x.Pattern.IsHook));
                    return signature != 0 ? signature : a[0].ResponseStartTick.CompareTo(b[0].ResponseStartTick);
                });
                choices.Add(options);
            }
            var reserved = new List<PlannedAttack>();
            if (!Reserve(choices, hooks, plan.Stage.Music.TicksPerBar, 0, reserved, plan.CallOverlapTicks))
                throw new InvalidOperationException("This monster roster cannot provide a complete response in every hook of " + plan.Stage.Music.Id);

            var attacks = new List<PlannedAttack>(plan.Attacks);
            var withdrawals = new List<PlanWithdrawal>(plan.Withdrawals);
            var removed = new HashSet<PlannedAttack>();
            var grid = BattlePlanner.BeatGrid(plan.Stage);
            foreach (var existing in attacks) foreach (var required in reserved)
            {
                if (ReferenceEquals(existing, required)) continue;
                if (!BattlePlanner.Conflicts(existing, required, out int tick, plan.CallOverlapTicks)) continue;
                // Linked Calls and Responses are never cut into independently playable fragments.
                foreach (var member in attacks)
                    if (ReferenceEquals(member, existing) || existing.Chain != null && ReferenceEquals(member.Chain, existing.Chain))
                        if (removed.Add(member)) withdrawals.Add(new PlanWithdrawal(member, required.MonsterId, tick,
                            BattlePlanner.CountOccupied(attacks.FindAll(x => x.MonsterId == member.MonsterId), grid),
                            BattlePlanner.CountOccupied(reserved.FindAll(x => x.MonsterId == required.MonsterId), grid)));
                break;
            }
            attacks.RemoveAll(x => removed.Contains(x));
            foreach (var required in reserved) if (!attacks.Contains(required)) attacks.Add(required);
            var fills = new List<PlannedAttack>();
            foreach (var fill in plan.GapFills) if (!removed.Contains(fill)) fills.Add(fill);
            var owners = new List<MonsterPlan>();
            foreach (var owner in plan.Monsters)
            {
                var owned = attacks.FindAll(x => x.MonsterId == owner.InstanceId);
                if (owned.Count > 0) owners.Add(new MonsterPlan(owner.InstanceId, owner.Monster, owner.ProposedCount, owned,
                    BattlePlanner.CountOccupied(owned, grid)));
            }
            var result = new BattlePlan(plan.Stage, owners, withdrawals, fills, plan.CallOverlapTicks);
            foreach (var hook in hooks) if (!Covers(result, hook)) throw new InvalidOperationException("Missing reserved hook response.");
            return result;
        }

        private static bool Reserve(List<List<List<PlannedAttack>>> choices, List<MusicSection> hooks, int ticksPerBar, int index, List<PlannedAttack> reserved, int callOverlapTicks)
        {
            if (index == choices.Count) return true;
            int start = hooks[index].StartBar * ticksPerBar;
            int end = (hooks[index].StartBar + hooks[index].BarCount) * ticksPerBar;
            foreach (var attack in reserved)
                if (attack.ResponseStartTick >= start && attack.ResponseStartTick < end && attack.PhraseEndTick <= end)
                    return Reserve(choices, hooks, ticksPerBar, index + 1, reserved, callOverlapTicks);
            foreach (var bundle in choices[index])
            {
                bool compatible = true;
                foreach (var proposed in bundle) foreach (var previous in reserved)
                    if (BattlePlanner.Conflicts(proposed, previous, out _, callOverlapTicks)) compatible = false;
                if (!compatible) continue;
                int count = reserved.Count; reserved.AddRange(bundle);
                if (Reserve(choices, hooks, ticksPerBar, index + 1, reserved, callOverlapTicks)) return true;
                reserved.RemoveRange(count, reserved.Count - count);
            }
            return false;
        }
    }
}
