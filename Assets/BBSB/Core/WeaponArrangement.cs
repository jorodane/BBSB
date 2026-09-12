using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public sealed class WeaponPlacement
    {
        public int Slot { get; }
        public string MonsterId { get; }
        public string PatternId { get; }
        public int OffsetTick { get; }
        public WeaponPlacement(int slot, string monsterId, string patternId, int offsetTick)
        { Slot = slot; MonsterId = monsterId; PatternId = patternId; OffsetTick = offsetTick; }
        public bool Matches(PlannedAttack attack) => attack.MonsterId == MonsterId && attack.Pattern.Id == PatternId;
    }

    /// <summary>One assignment per equipped slot, repeated for every occurrence of its monster pattern.</summary>
    public sealed class WeaponArrangement
    {
        private readonly List<WeaponState> equipment = new List<WeaponState>();
        private readonly List<WeaponPlacement> placements = new List<WeaponPlacement>();
        private readonly List<PlannedAttack> patterns = new List<PlannedAttack>();
        public IReadOnlyList<WeaponState> Equipment { get; }
        public IReadOnlyList<WeaponPlacement> Placements { get; }
        public IReadOnlyList<PlannedAttack> Patterns { get; }

        public WeaponArrangement(BattlePlan plan, IReadOnlyList<WeaponState> weapons)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (weapons == null || weapons.Count > RunRules.WeaponSlots) throw new ArgumentException("Invalid equipment.");
            foreach (var weapon in weapons)
            {
                WeaponCatalog.Find(weapon.DefinitionId);
                equipment.Add(new WeaponState(weapon.DefinitionId) { Level = weapon.Level });
            }
            foreach (var attack in plan.Attacks)
                if (!patterns.Exists(p => p.MonsterId == attack.MonsterId && p.Pattern.Id == attack.Pattern.Id)) patterns.Add(attack);
            Equipment = equipment.AsReadOnly(); Placements = placements.AsReadOnly(); Patterns = patterns.AsReadOnly();
        }

        public WeaponPlacement At(int slot) => placements.Find(p => p.Slot == slot);
        public void Remove(int slot) => placements.RemoveAll(p => p.Slot == slot);
        public bool TryPlace(int slot, string monsterId, string patternId, int offset, out string reason)
        {
            if (!CanPlace(slot, monsterId, patternId, offset, out reason)) return false;
            Remove(slot); placements.Add(new WeaponPlacement(slot, monsterId, patternId, offset));
            placements.Sort((a, b) => a.Slot.CompareTo(b.Slot)); return true;
        }

        public bool CanPlace(int slot, string monsterId, string patternId, int offset, out string reason)
        {
            reason = "";
            if (slot < 0 || slot >= equipment.Count || offset < 0) { reason = "무기를 선택해"; return false; }
            var source = patterns.Find(p => p.MonsterId == monsterId && p.Pattern.Id == patternId);
            if (source == null) { reason = "이 스테이지에 없는 패턴이야"; return false; }
            var pattern = WeaponCatalog.Find(equipment[slot].DefinitionId).Pattern;
            if (offset >= source.Pattern.ResponseTicks || offset + pattern.EndOffsetTick > source.Pattern.ResponseTicks)
            { reason = "무기 패턴이 Response 구간을 벗어나"; return false; }
            // Weapons subscribe to existing Response judgments. Other equipped weapons never
            // occupy or consume these positions, and placement cannot add a new player input.
            foreach (var step in pattern.Steps)
                if (MatchingStep(source.Placement.Pattern, step, offset) < 0)
                { reason = "이 위치에는 무기 패턴에 맞는 몬스터 박자가 없어"; return false; }

            return true;
        }

        internal static int MatchingStep(RhythmPattern source, PatternStep required, int offset)
        {
            for (int i = 0; i < source.Steps.Count; i++)
            {
                var step = source.Steps[i];
                if (step.OffsetTick == (long)offset + required.OffsetTick && step.Kind == required.Kind &&
                    step.DurationTicks == required.DurationTicks) return i;
            }
            return -1;
        }

        public IReadOnlyList<int> ValidOffsets(int slot, PlannedAttack pattern)
        {
            var result = new List<int>();
            for (int tick = 0; tick < pattern.Pattern.ResponseTicks; tick++)
                if (CanPlace(slot, pattern.MonsterId, pattern.Pattern.Id, tick, out _)) result.Add(tick);
            return result.AsReadOnly();
        }

        // Prefer existing input edges. Automatic placement is explicit; manual edits are never overwritten.
        public void AutoArrange()
        {
            placements.Clear();
            for (int slot = 0; slot < equipment.Count; slot++)
            {
                bool done = false;
                foreach (var pattern in patterns)
                {
                    foreach (var step in pattern.Pattern.Pattern.Steps)
                        if (TryPlace(slot, pattern.MonsterId, pattern.Pattern.Id, step.OffsetTick, out _)) { done = true; break; }
                    if (done) break;
                }
            }
        }

        internal WeaponArrangement Snapshot(BattlePlan target)
        {
            var copy = new WeaponArrangement(target, Equipment);
            foreach (var p in placements) copy.TryPlace(p.Slot, p.MonsterId, p.PatternId, p.OffsetTick, out _);
            return copy;
        }

        internal static PatternPlacement PlacePattern(RhythmPattern pattern, int start) =>
            new PatternPlacement(pattern, start, new List<MusicSlot>());
    }
}
