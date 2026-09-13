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
        public GestureKind Kind { get; }
        public WeaponPlacement(int slot, string monsterId, string patternId, int offsetTick, GestureKind kind)
        { Slot = slot; MonsterId = monsterId; PatternId = patternId; OffsetTick = offsetTick; Kind = kind; }
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
                equipment.Add(new WeaponState(weapon.DefinitionId, weapon.Level));
            }
            foreach (var attack in plan.Attacks)
                if (!patterns.Exists(p => p.MonsterId == attack.MonsterId && p.Pattern.Id == attack.Pattern.Id)) patterns.Add(attack);
            Equipment = equipment.AsReadOnly(); Placements = placements.AsReadOnly(); Patterns = patterns.AsReadOnly();
        }

        public WeaponPlacement At(int slot) => placements.Find(p => p.Slot == slot);
        public void Remove(int slot) => placements.RemoveAll(p => p.Slot == slot);
        public bool TryPlace(int slot, string monsterId, string patternId, int offset, out string reason, GestureKind? kind = null)
        {
            if (!CanPlace(slot, monsterId, patternId, offset, out reason, kind)) return false;
            var source = patterns.Find(p => p.MonsterId == monsterId && p.Pattern.Id == patternId);
            int index = MatchingStep(source.Placement.Pattern, WeaponCatalog.Find(equipment[slot].DefinitionId), offset, kind);
            Remove(slot); placements.Add(new WeaponPlacement(slot, monsterId, patternId, offset, source.Placement.Pattern.Steps[index].Kind));
            placements.Sort((a, b) => a.Slot.CompareTo(b.Slot)); return true;
        }

        public bool CanPlace(int slot, string monsterId, string patternId, int offset, out string reason, GestureKind? kind = null)
        {
            reason = "";
            if (slot < 0 || slot >= equipment.Count || offset < 0) { reason = "무기를 선택해"; return false; }
            var source = patterns.Find(p => p.MonsterId == monsterId && p.Pattern.Id == patternId);
            if (source == null) { reason = "이 스테이지에 없는 패턴이야"; return false; }
            var weapon = WeaponCatalog.Find(equipment[slot].DefinitionId);
            int index = MatchingStep(source.Placement.Pattern, weapon, offset, kind);
            if (index < 0)
            { reason = index == -2 ? "같은 박자에 행동이 여러 개야. 배치할 행동을 골라줘" : "이 무기가 지원하는 행동 칸에 배치해"; return false; }
            return true;
        }

        // One existing action, regardless of its duration or the spacing of neighboring notes.
        internal static int MatchingStep(RhythmPattern source, WeaponDefinition weapon, int offset, GestureKind? kind = null)
        {
            int found = -1;
            for (int i = 0; i < source.Steps.Count; i++)
            {
                var step = source.Steps[i];
                if (step.OffsetTick != offset || weapon.ActionFor(step.Kind) == null || (kind.HasValue && kind.Value != step.Kind)) continue;
                if (found >= 0) return -2;
                found = i;
            }
            return found;
        }

        public IReadOnlyList<int> ValidOffsets(int slot, PlannedAttack pattern)
        {
            var result = new List<int>();
            foreach (var step in pattern.Placement.Pattern.Steps)
                if (!result.Contains(step.OffsetTick) &&
                    CanPlace(slot, pattern.MonsterId, pattern.Pattern.Id, step.OffsetTick, out _, step.Kind)) result.Add(step.OffsetTick);
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
                        if (TryPlace(slot, pattern.MonsterId, pattern.Pattern.Id, step.OffsetTick, out _, step.Kind)) { done = true; break; }
                    if (done) break;
                }
            }
        }

        internal WeaponArrangement Snapshot(BattlePlan target)
        {
            var copy = new WeaponArrangement(target, Equipment);
            foreach (var p in placements) copy.TryPlace(p.Slot, p.MonsterId, p.PatternId, p.OffsetTick, out _, p.Kind);
            return copy;
        }

        internal static PatternPlacement PlacePattern(RhythmPattern pattern, int start) =>
            new PatternPlacement(pattern, start, new List<MusicSlot>());
    }
}
