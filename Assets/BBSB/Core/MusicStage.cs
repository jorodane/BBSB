using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    /// <summary>Immutable score for one encounter; retain it when a song loops back to preparation.</summary>
    public sealed class MusicStage
    {
        private readonly Dictionary<(int, GestureKind, int), MusicSlot> lookup;
        public MusicDefinition Music { get; }
        public IReadOnlyList<MusicSlot> Slots { get; }

        private MusicStage(MusicDefinition music, List<MusicSlot> slots)
        {
            Music = music; Slots = slots.AsReadOnly();
            lookup = new Dictionary<(int, GestureKind, int), MusicSlot>();
            foreach (var slot in slots) lookup.Add((slot.StartTick, slot.Kind, slot.DurationTicks), slot);
        }

        public static MusicStage Generate(MusicDefinition music)
        {
            if (music == null) throw new ArgumentNullException(nameof(music));
            var generated = new List<(SlotTemplate template, int tick, double weight)>();
            for (int bar = 0; bar < music.BarCount; bar++)
            {
                var section = music.SectionAtBar(bar);
                if (!section.AllowsResponse) continue;
                foreach (var template in music.BarCycle[bar % music.BarCycle.Count])
                {
                    int tick = bar * music.TicksPerBar + template.OffsetTick;
                    if (template.DurationTicks > music.TotalTicks - tick) continue; // Never truncate a held gesture.
                    int end = tick + template.DurationTicks;
                    bool allowed = true;
                    // A hold ending on a section boundary does not occupy the next section.
                    for (int coveredBar = bar + 1; coveredBar * (long)music.TicksPerBar < end; coveredBar++)
                        if (!music.SectionAtBar(coveredBar).AllowsResponse) { allowed = false; break; }
                    if (!allowed) continue;
                    double weight = template.Weight * section.WeightMultiplier;
                    SlotTemplate.ValidateWeight(weight);
                    generated.Add((template, tick, weight));
                }
            }
            generated.Sort((a, b) =>
            {
                int order = a.tick.CompareTo(b.tick);
                if (order == 0) order = a.template.Kind.CompareTo(b.template.Kind);
                return order != 0 ? order : a.template.DurationTicks.CompareTo(b.template.DurationTicks);
            });
            var slots = new List<MusicSlot>(generated.Count);
            foreach (var entry in generated)
                slots.Add(new MusicSlot(slots.Count, entry.template.Kind, entry.tick, entry.template.DurationTicks, entry.weight));
            return new MusicStage(music, slots);
        }

        /// <summary>
        /// Enumerates complete fits in chronological order. Timing and durations must match exactly.
        /// Cues occupy [CueStartTick, StartTick), without becoming player-input slots.
        /// This query does not select attacks, consume slots or arbitrate monster/touch conflicts.
        /// </summary>
        public IEnumerable<PatternPlacement> FindPlacements(RhythmPattern pattern)
        {
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));
            var first = pattern.Steps[0];
            foreach (var anchor in Slots)
            {
                if (anchor.Kind != first.Kind || anchor.DurationTicks != first.DurationTicks ||
                    anchor.StartTick < pattern.CueLeadTicks || pattern.EndOffsetTick > Music.TotalTicks - anchor.StartTick)
                    continue;
                var matches = new List<MusicSlot>(pattern.Steps.Count);
                foreach (var step in pattern.Steps)
                {
                    if (!lookup.TryGetValue((anchor.StartTick + step.OffsetTick, step.Kind, step.DurationTicks), out var slot)) break;
                    matches.Add(slot);
                }
                if (matches.Count == pattern.Steps.Count) yield return new PatternPlacement(pattern, anchor.StartTick, matches);
            }
        }
    }
}
