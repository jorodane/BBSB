using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public enum GestureKind { Tap, Hold, Dive, Flick, Shake }
    public enum TouchTransition { None, Press, Release }
    public enum GestureMotion { None, Flick, Shake }

    // A tick is a quarter of a beat, not a frame or an audio sample.
    public static class RhythmTime
    {
        public const int TicksPerBeat = 4;
        public static double Seconds(int ticks, double bpm) => ticks * 60.0 / (TicksPerBeat * bpm);
    }

    public readonly struct TouchRequirement
    {
        public TouchTransition Start { get; }
        public TouchTransition End { get; }
        public bool HoldThroughInterval { get; }
        public GestureMotion Motion { get; }

        private TouchRequirement(TouchTransition start, TouchTransition end, bool held, GestureMotion motion)
        { Start = start; End = end; HoldThroughInterval = held; Motion = motion; }

        public static TouchRequirement For(GestureKind kind)
        {
            switch (kind)
            {
                case GestureKind.Tap: return new TouchRequirement(TouchTransition.Press, TouchTransition.None, false, GestureMotion.None);
                case GestureKind.Hold: return new TouchRequirement(TouchTransition.Press, TouchTransition.None, true, GestureMotion.None);
                case GestureKind.Dive: return new TouchRequirement(TouchTransition.Press, TouchTransition.Release, true, GestureMotion.None);
                case GestureKind.Flick: return new TouchRequirement(TouchTransition.None, TouchTransition.Release, false, GestureMotion.Flick);
                // Shake can begin within an existing hold; it does not demand another press.
                case GestureKind.Shake: return new TouchRequirement(TouchTransition.None, TouchTransition.None, true, GestureMotion.Shake);
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }

    public sealed class PatternStep
    {
        public GestureKind Kind { get; }
        public int OffsetTick { get; }
        public int DurationTicks { get; }
        public TouchRequirement Touch => TouchRequirement.For(Kind);

        public PatternStep(GestureKind kind, int offsetTick, int durationTicks = 0)
        {
            Validate(kind, offsetTick, durationTicks);
            Kind = kind; OffsetTick = offsetTick; DurationTicks = durationTicks;
        }

        internal static void Validate(GestureKind kind, int tick, int duration)
        {
            if (!Enum.IsDefined(typeof(GestureKind), kind) || tick < 0 || duration < 0 || tick > int.MaxValue - duration)
                throw new ArgumentOutOfRangeException(nameof(tick), "Invalid gesture or timing.");
            bool sustained = kind == GestureKind.Hold || kind == GestureKind.Dive;
            if (sustained ? duration == 0 : duration != 0)
                throw new ArgumentException("Hold and Dive need a duration; Tap, Flick and Shake target one instant.");
        }
    }

    public sealed class RhythmPattern
    {
        public string Id { get; }
        public int CueLeadTicks { get; }
        public int EndOffsetTick { get; }
        public IReadOnlyList<PatternStep> Steps { get; }

        public RhythmPattern(string id, int cueLeadTicks, IEnumerable<PatternStep> steps)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A pattern needs an ID.", nameof(id));
            if (cueLeadTicks <= 0) throw new ArgumentOutOfRangeException(nameof(cueLeadTicks), "Every pattern needs a Call before its Response.");
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            var copy = new List<PatternStep>(steps);
            if (copy.Count == 0 || copy.Exists(x => x == null)) throw new ArgumentException("A pattern needs non-null steps.", nameof(steps));
            copy.Sort((a, b) =>
            {
                int order = a.OffsetTick.CompareTo(b.OffsetTick);
                if (order == 0) order = a.Kind.CompareTo(b.Kind);
                return order != 0 ? order : a.DurationTicks.CompareTo(b.DurationTicks);
            });
            if (copy[0].OffsetTick != 0) throw new ArgumentException("Pattern timing must start at offset zero.", nameof(steps));
            var seen = new HashSet<(int, GestureKind, int)>();
            foreach (var step in copy)
            {
                if (!seen.Add((step.OffsetTick, step.Kind, step.DurationTicks))) throw new ArgumentException("Duplicate pattern step.", nameof(steps));
                EndOffsetTick = Math.Max(EndOffsetTick, step.OffsetTick + step.DurationTicks);
            }
            Id = id; CueLeadTicks = cueLeadTicks; Steps = copy.AsReadOnly();
        }
    }

    public sealed class SlotTemplate
    {
        public GestureKind Kind { get; }
        public int OffsetTick { get; }
        public int DurationTicks { get; }
        public double Weight { get; }

        public SlotTemplate(GestureKind kind, int offsetTick, int durationTicks = 0, double weight = 1)
        {
            PatternStep.Validate(kind, offsetTick, durationTicks);
            ValidateWeight(weight);
            Kind = kind; OffsetTick = offsetTick; DurationTicks = durationTicks; Weight = weight;
        }

        internal static void ValidateWeight(double weight)
        {
            if (double.IsNaN(weight) || double.IsInfinity(weight) || weight <= 0)
                throw new ArgumentOutOfRangeException(nameof(weight));
        }
    }

    public sealed class MusicSection
    {
        public string Name { get; }
        public int StartBar { get; }
        public int BarCount { get; }
        public double WeightMultiplier { get; }
        public bool AllowsResponse { get; }

        public MusicSection(string name, int startBar, int barCount, double weightMultiplier, bool allowsResponse = true)
        {
            if (string.IsNullOrWhiteSpace(name) || startBar < 0 || barCount <= 0 || startBar > int.MaxValue - barCount)
                throw new ArgumentException("Invalid music section.");
            SlotTemplate.ValidateWeight(weightMultiplier);
            Name = name; StartBar = startBar; BarCount = barCount; WeightMultiplier = weightMultiplier; AllowsResponse = allowsResponse;
        }
    }

    /// <summary>Mock score configuration. Later audio assets can refer to the same stable music ID.</summary>
    public sealed class MusicDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public double Bpm { get; }
        public int BeatsPerBar { get; }
        public int BarCount { get; }
        public int TicksPerBar => BeatsPerBar * RhythmTime.TicksPerBeat;
        public int TotalTicks => BarCount * TicksPerBar;
        public double DurationSeconds => RhythmTime.Seconds(TotalTicks, Bpm);
        public IReadOnlyList<MusicSection> Sections { get; }
        public IReadOnlyList<IReadOnlyList<SlotTemplate>> BarCycle { get; }

        public MusicDefinition(string id, string name, double bpm, int beatsPerBar,
            IEnumerable<MusicSection> sections, IEnumerable<IEnumerable<SlotTemplate>> barCycle)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Music needs an ID and name.");
            if (double.IsNaN(bpm) || double.IsInfinity(bpm) || bpm <= 0) throw new ArgumentOutOfRangeException(nameof(bpm));
            if (beatsPerBar <= 0 || beatsPerBar > int.MaxValue / RhythmTime.TicksPerBeat) throw new ArgumentOutOfRangeException(nameof(beatsPerBar));
            if (sections == null || barCycle == null) throw new ArgumentNullException(sections == null ? nameof(sections) : nameof(barCycle));
            Id = id; Name = name; Bpm = bpm; BeatsPerBar = beatsPerBar;
            var sectionCopy = new List<MusicSection>(sections);
            foreach (var section in sectionCopy)
            {
                if (section == null || section.StartBar != BarCount) throw new ArgumentException("Sections must cover the song in order without gaps or overlaps.", nameof(sections));
                BarCount = checked(BarCount + section.BarCount);
            }
            if (BarCount == 0 || BarCount > int.MaxValue / TicksPerBar) throw new ArgumentException("Invalid song length.", nameof(sections));
            Sections = sectionCopy.AsReadOnly();
            var cycles = new List<IReadOnlyList<SlotTemplate>>();
            foreach (var bar in barCycle)
            {
                if (bar == null) throw new ArgumentException("A template bar cannot be null.", nameof(barCycle));
                var copy = new List<SlotTemplate>(bar);
                var seen = new HashSet<(int, GestureKind, int)>();
                foreach (var slot in copy)
                {
                    if (slot == null || slot.OffsetTick >= TicksPerBar) throw new ArgumentException("Slot starts must be within a template bar.", nameof(barCycle));
                    if (!seen.Add((slot.OffsetTick, slot.Kind, slot.DurationTicks))) throw new ArgumentException("Duplicate slot template.", nameof(barCycle));
                }
                cycles.Add(copy.AsReadOnly());
            }
            if (cycles.Count == 0) throw new ArgumentException("Music needs a bar template cycle.", nameof(barCycle));
            BarCycle = cycles.AsReadOnly();
        }

        public MusicSection SectionAtBar(int bar)
        {
            if (bar < 0 || bar >= BarCount) throw new ArgumentOutOfRangeException(nameof(bar));
            foreach (var section in Sections) if (bar < section.StartBar + section.BarCount) return section;
            throw new InvalidOperationException("Missing section.");
        }
    }

    /// <summary>An allowed placement option, not an occupied input. Overlapping options are intentional.</summary>
    public sealed class MusicSlot
    {
        public int Index { get; }
        public GestureKind Kind { get; }
        public int StartTick { get; }
        public int DurationTicks { get; }
        public int EndTick => StartTick + DurationTicks;
        public double Weight { get; }
        public TouchRequirement Touch => TouchRequirement.For(Kind);

        internal MusicSlot(int index, GestureKind kind, int startTick, int durationTicks, double weight)
        { Index = index; Kind = kind; StartTick = startTick; DurationTicks = durationTicks; Weight = weight; }
    }

    public sealed class PatternPlacement
    {
        public RhythmPattern Pattern { get; }
        public int StartTick { get; }
        public int EndTick => StartTick + Pattern.EndOffsetTick;
        public int CueStartTick => StartTick - Pattern.CueLeadTicks;
        // Mean slot weight: relative likelihood for a future planner, not a probability or guarantee.
        public double Weight { get; }
        public IReadOnlyList<MusicSlot> Slots { get; }

        internal PatternPlacement(RhythmPattern pattern, int startTick, List<MusicSlot> slots)
        {
            Pattern = pattern; StartTick = startTick; Slots = slots.AsReadOnly();
            foreach (var slot in slots) Weight += slot.Weight / slots.Count;
        }
    }
}
