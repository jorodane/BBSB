using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public static class MusicCatalog
    {
        // These are mock scores, not recordings or audio-analysis results. All offsets are ticks.
        public static IReadOnlyList<MusicDefinition> All { get; } = Array.AsReadOnly(new[]
        {
            Define("steady-pulse", "Steady Pulse", 96, 3, Bar(new[] { 0, 4, 8, 12 }, 8, new[] { 0, 8 }, new[] { 4, 12 }, 4, new[] { 0, 8 })),
            Define("offbeat-spark", "Offbeat Spark", 124, 3.5, Bar(new[] { 0, 2, 6, 8, 10, 14 }, 4, new[] { 0, 8 }, new[] { 0, 8 }, 4, new[] { 6, 14 })),
            Define("deep-current", "Deep Current", 80, 2.5, Bar(new[] { 0, 8 }, 16, new[] { 0 }, new[] { 4, 12 }, 4, new[] { 0, 8 })),
            Define("rapid-drive", "Rapid Drive", 168, 4, Bar(new[] { 0, 2, 4, 6, 8, 10, 12, 14 }, 4, new[] { 0, 8 }, new[] { 4, 12 }, 2, new[] { 6, 14 })),
            Define("switchback", "Switchback", 112, 3,
                Bar(new[] { 0, 4, 8, 12 }, 8, new[] { 0, 8 }, new[] { 4, 12 }, 4, new[] { 0, 8 }),
                Bar(new[] { 2, 6, 10, 14 }, 4, new[] { 2, 10 }, new[] { 2, 10 }, 4, new[] { 6, 14 }))
        });

        /// <summary>Independent of map/reward draws and entry order. Re-entering preparation never rerolls.</summary>
        public static MusicStage ForEncounter(int runSeed, int field, int row, int column)
        {
            if (field < 1 || row < 0 || column < 0) throw new ArgumentOutOfRangeException(nameof(field));
            uint seed = unchecked((uint)runSeed ^ 0xd1b54a35u);
            seed = Mix(seed, field); seed = Mix(seed, row); seed = Mix(seed, column);
            var random = new SeededRandom(unchecked((int)seed));
            return MusicStage.Generate(All[random.Next(All.Count)]);
        }

        private static uint Mix(uint seed, int value)
        {
            unchecked
            {
                seed = (seed ^ (uint)value) * 0x85ebca6bu;
                seed ^= seed >> 16;
                return seed;
            }
        }

        private static MusicDefinition Define(string id, string name, double bpm, double highlightWeight, params SlotTemplate[][] cycle)
        {
            return new MusicDefinition(id, name, bpm, 4, new[]
            {
                new MusicSection("INTRO", 0, 1, 1, false),
                new MusicSection("GROOVE", 1, 6, 1),
                new MusicSection("HIGHLIGHT", 7, 4, highlightWeight),
                new MusicSection("OUTRO", 11, 5, 1.25)
            }, cycle);
        }

        private static SlotTemplate[] Bar(int[] taps, int holdTicks, int[] holds, int[] shakes, int shakeTicks, int[] flicks)
        {
            var slots = new List<SlotTemplate>();
            foreach (int tick in taps) slots.Add(new SlotTemplate(GestureKind.Tap, tick));
            foreach (int tick in holds)
            {
                slots.Add(new SlotTemplate(GestureKind.Hold, tick, holdTicks));
                slots.Add(new SlotTemplate(GestureKind.Dive, tick, holdTicks));
            }
            foreach (int tick in shakes) slots.Add(new SlotTemplate(GestureKind.Shake, tick, shakeTicks));
            foreach (int tick in flicks) slots.Add(new SlotTemplate(GestureKind.Flick, tick));
            return slots.ToArray();
        }
    }
}
