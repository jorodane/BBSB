using System;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    /// <summary>Read-only, allocation-free next Response actions for already announced patterns.</summary>
    public static class ResponsePromptTimeline
    {
        public static int Fill(RhythmRound round, string monsterId, double seconds, GestureKind[] output)
        {
            if (output == null || output.Length < 5) throw new ArgumentException("Five action slots are required.", nameof(output));
            if (round == null || round.Finished || round.Aborted || double.IsNaN(seconds) || double.IsInfinity(seconds)) return 0;
            int count = 0;
            // Independent/linked patterns may have a new Call while an older Response is still held.
            foreach (var attack in round.Plan.Attacks)
            {
                if (attack.MonsterId != monsterId || seconds < attack.CallStartTick * round.BeatSeconds / RhythmTime.TicksPerBeat ||
                    (round.Combat != null && !round.Combat.Allows(attack))) continue;
                double next = double.PositiveInfinity;
                foreach (var note in round.Notes)
                    if (ReferenceEquals(note.Attack, attack) && note.State != ResponseState.Resolved &&
                        seconds <= note.EndSeconds + round.HalfMissWindow)
                        next = Math.Min(next, note.StartSeconds);
                foreach (var note in round.Notes)
                {
                    if (!ReferenceEquals(note.Attack, attack) || note.State == ResponseState.Resolved ||
                        seconds > note.EndSeconds + round.HalfMissWindow ||
                        (note.StartSeconds != next && note.State != ResponseState.Holding)) continue;
                    bool found = false;
                    for (int i = 0; i < count; i++) if (output[i] == note.Step.Kind) found = true;
                    if (!found && count < output.Length) output[count++] = note.Step.Kind;
                }
            }
            return count;
        }
    }
}
