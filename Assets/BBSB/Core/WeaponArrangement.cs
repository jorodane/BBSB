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
        private readonly BattlePlan plan;
        private readonly List<WeaponState> equipment = new List<WeaponState>();
        private readonly List<WeaponPlacement> placements = new List<WeaponPlacement>();
        private readonly List<PlannedAttack> patterns = new List<PlannedAttack>();
        public IReadOnlyList<WeaponState> Equipment { get; }
        public IReadOnlyList<WeaponPlacement> Placements { get; }
        public IReadOnlyList<PlannedAttack> Patterns { get; }

        public WeaponArrangement(BattlePlan plan, IReadOnlyList<WeaponState> weapons)
        {
            this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
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
            foreach (var attack in plan.Attacks)
            {
                if (attack.MonsterId != monsterId || attack.Pattern.Id != patternId) continue;
                var candidate = PlacePattern(pattern, attack.ResponseStartTick + offset);
                foreach (var other in plan.Attacks)
                {
                    if (other != attack && other.MonsterId == monsterId && other.Pattern.Id == patternId &&
                        InputCompatibility.PhysicalConflict(candidate, PlacePattern(pattern, other.ResponseStartTick + offset), out _))
                    { reason = "같은 무기의 다음 발동과 입력이 겹쳐"; return false; }
                    if (InputCompatibility.PhysicalConflict(candidate, other.Placement, out _))
                    { reason = "몬스터 입력과 동시에 수행할 수 없어"; return false; }
                    foreach (var placed in placements)
                    {
                        if (placed.Slot == slot || !placed.Matches(other)) continue;
                        var otherPattern = WeaponCatalog.Find(equipment[placed.Slot].DefinitionId).Pattern;
                        if (InputCompatibility.PhysicalConflict(candidate, PlacePattern(otherPattern, other.ResponseStartTick + placed.OffsetTick), out _))
                        { reason = "이미 배치한 무기의 입력과 충돌해"; return false; }
                    }
                }
            }
            return true;
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
