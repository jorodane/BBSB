using System;
using System.Collections.Generic;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    public sealed class NotePartStack
    {
        public NotePartState Part { get; }
        public int Count { get; internal set; }
        internal NotePartStack(NotePartState part) { Part = part; Count = 1; }
    }

    // Instance IDs, rather than filtered grid indices, remain valid after a card disappears.
    public static class NoteWorkshopModel
    {
        public static IReadOnlyList<NotePartStack> FreeParts(RunSession session)
        {
            var stacks = new List<NotePartStack>();
            foreach (var part in session.NoteParts)
            {
                if (session.NotePartOwner(part.InstanceId) != null) continue;
                var stack = stacks.Find(x => x.Part.Definition.Id == part.Definition.Id);
                if (stack == null) stacks.Add(new NotePartStack(part)); else stack.Count++;
            }
            return stacks.AsReadOnly();
        }

        public static int PartIndex(RunSession session, int instanceId)
        {
            for (int i = 0; i < session.NoteParts.Count; i++)
                if (session.NoteParts[i].InstanceId == instanceId) return i;
            return -1;
        }

        public static bool TryAttach(RunSession session, int instanceId, int weaponIndex, int noteIndex,
            WeaponPhraseSet source, out string reason, bool previewOnly = false)
        {
            reason = "지금은 이 부품을 옮길 수 없어.";
            if (weaponIndex < 0 || weaponIndex >= session.OwnedWeapons.Count) return false;
            var owner = session.NotePartOwner(instanceId);
            // A stale drag must never take a part that another weapon now owns.
            if (owner != null && !ReferenceEquals(owner, session.OwnedWeapons[weaponIndex])) return false;
            return session.TryAttachNotePart(PartIndex(session, instanceId), weaponIndex, noteIndex,
                out reason, source, previewOnly);
        }

        public static bool Remove(RunSession session, int instanceId, WeaponState weapon) =>
            ReferenceEquals(session.NotePartOwner(instanceId), weapon) &&
            session.RemoveNotePart(PartIndex(session, instanceId));
    }

    public enum NoteDemoInput { Idle, Press, Hold, Release, Invoke, Call, Ready }

    // An all-success illustration of the selected phrase. No RunSession or battle reference:
    // conditional notes are demonstrated, but never cause damage, rewards or real input.
    public sealed class NoteWorkshopPlayback
    {
        private sealed class Input
        {
            internal double Start, End;
            internal int Lane;
            internal bool RequiresRelease, IsCall;
        }
        private readonly List<Input> inputs = new List<Input>();
        private readonly HashSet<WeaponPhraseNote> connected = new HashSet<WeaponPhraseNote>();
        private readonly bool connectsCycles;
        private const double FlashBeats = .18;
        public WeaponPhrase Phrase { get; }
        public int Width { get; }
        public int Offset { get; }
        public int CyclesPerLoop { get; }
        public double LoopBeats { get; }
        public double FirstNoteBeat { get; }
        private readonly bool continuous;
        private readonly double sectionBeats, executionEnd;
        private readonly WeaponPhraseNote chosenShot;
        private readonly double chosenShotBeat;

        public NoteWorkshopPlayback(WeaponPhrase phrase, int width, int offset)
        {
            if (phrase == null) throw new ArgumentNullException(nameof(phrase));
            if (width < 1 || offset < 0 || offset >= width) throw new ArgumentOutOfRangeException(nameof(width));
            Phrase = phrase; Width = width; Offset = offset;
            CyclesPerLoop = phrase.Repeat && phrase.MaximumCycles > 0 ? phrase.MaximumCycles : 1;
            FirstNoteBeat = Math.Ceiling(phrase.FirstNoteDelayBeats - .000001);
            continuous = phrase.Repeat && phrase.MaximumCycles == 0;
            foreach (var note in phrase.Notes)
                if (note.IsOptional) { chosenShot = note; chosenShotBeat = note.Beat + 2 * note.OpportunityIntervalBeats; break; }
            sectionBeats = phrase.LengthBeats + (chosenShot == null ? 0 : Math.Ceiling(2 * chosenShot.OpportunityIntervalBeats));
            for (int index = 0; index < phrase.Notes.Count; index++)
            {
                var note = phrase.Notes[index];
                if (note.IsChargedRelease || note.IsOptional && !ReferenceEquals(note, chosenShot)) continue;
                double at = note.IsOptional ? chosenShotBeat : note.Beat;
                var previous = inputs.Count == 0 ? null : inputs[inputs.Count - 1];
                bool release = note.IsParry && phrase.ParriesOnKeyUp || phrase.ChargedResponseFor(index) >= 0;
                if (previous != null && !previous.RequiresRelease && previous.End > previous.Start &&
                    previous.Lane == Lane(note) && Math.Abs(previous.End - note.Beat) < .000001 &&
                    (note.ConnectFromPrevious || note.IsHold))
                {
                    previous.End = note.Beat + note.HoldBeats; previous.RequiresRelease = release;
                    connected.Add(note);
                }
                else inputs.Add(new Input { Start = at, End = at + note.HoldBeats, Lane = Lane(note), RequiresRelease = release, IsCall = note.IsCall });
            }
            var first = inputs[0]; var last = inputs[inputs.Count - 1];
            executionEnd = phrase.HasFlexibleResponse ? last.End : phrase.LengthBeats;
            LoopBeats = continuous ? sectionBeats * CyclesPerLoop :
                sectionBeats * (CyclesPerLoop - 1) + executionEnd + Math.Max(1, phrase.CompletionCooldownBeats) + FirstNoteBeat;
            connectsCycles = first.End > first.Start && last.End > last.Start && !last.RequiresRelease &&
                first.Lane == last.Lane && Math.Abs(last.End - phrase.LengthBeats) < .000001;
        }

        public int Lane(WeaponPhraseNote note) => (Offset + note.LaneOffset) % Width;

        public bool ConnectsFromPrevious(WeaponPhraseNote note, double start) => connected.Contains(note) ||
            (ReferenceEquals(note, Phrase.Notes[0]) && connectsCycles && start > FirstNoteBeat &&
             ((start - FirstNoteBeat) % LoopBeats > .000001 || continuous));

        public double PatternBeat(double beat)
        {
            if (beat < FirstNoteBeat) return -1;
            double local = (beat - FirstNoteBeat) % LoopBeats;
            return local < sectionBeats * (CyclesPerLoop - 1) + executionEnd ? local % sectionBeats : -1;
        }

        public double PreparationRemaining(double beat) => beat < 0 ? 0 :
            Math.Max(0, FirstNoteBeat - (continuous ? beat : beat % LoopBeats));

        public NoteDemoInput InputAt(double beat, int lane)
        {
            if (beat < 0 || lane < 0 || lane >= Width) return NoteDemoInput.Idle;
            if (FirstNoteBeat > 0 && lane == Offset && (continuous ? beat : beat % LoopBeats) < FlashBeats)
                return NoteDemoInput.Invoke;
            if (beat < FirstNoteBeat) return NoteDemoInput.Idle;
            var result = NoteDemoInput.Idle;
            long block = (long)Math.Floor((beat - FirstNoteBeat) / LoopBeats);
            for (long b = Math.Max(0, block - 1); b <= block; b++)
                for (int cycle = 0; cycle < CyclesPerLoop; cycle++)
                    foreach (var input in inputs)
                    {
                        if (input.Lane != lane) continue;
                        double origin = FirstNoteBeat + b * LoopBeats + cycle * sectionBeats;
                        double start = origin + input.Start;
                        double end = start + input.End - input.Start;
                        bool joinsPrevious = connectsCycles && ReferenceEquals(input, inputs[0]) &&
                            (cycle > 0 || b > 0 && LoopBeats == Phrase.LengthBeats * CyclesPerLoop);
                        bool joinsNext = connectsCycles && ReferenceEquals(input, inputs[inputs.Count - 1]) &&
                            (cycle + 1 < CyclesPerLoop || LoopBeats == Phrase.LengthBeats * CyclesPerLoop);
                        if (!joinsPrevious && beat >= start && beat < start + Math.Min(FlashBeats, end > start ? end - start : FlashBeats))
                            return input.IsCall ? NoteDemoInput.Call : NoteDemoInput.Press;
                        if (end > start && beat >= start && beat < end) return NoteDemoInput.Hold;
                        if (!joinsNext && end > start && beat >= end && beat < end + FlashBeats) result = NoteDemoInput.Release;
                        if (chosenShot != null && lane == Lane(chosenShot) && beat >= origin + chosenShot.Beat && beat < origin + chosenShotBeat)
                            result = NoteDemoInput.Ready;
                    }
            return result;
        }

        public void VisitNotes(double from, double to, Action<WeaponPhraseNote, double, double, int> visit)
        {
            if (to < 0 || to < from) return;
            long first = Math.Max(0, (long)Math.Floor((from - FirstNoteBeat - sectionBeats * CyclesPerLoop) / LoopBeats));
            long last = Math.Max(0, (long)Math.Floor((to - FirstNoteBeat) / LoopBeats));
            for (long block = first; block <= last; block++)
                for (int cycle = 0; cycle < CyclesPerLoop; cycle++)
                    foreach (var note in Phrase.Notes)
                    {
                        double origin = FirstNoteBeat + block * LoopBeats + cycle * sectionBeats;
                        if (note.IsOptional)
                        {
                            for (int opportunity = 0; opportunity < FiveLaneNoteTimeline.MaxNotesPerLane; opportunity++)
                            {
                                double at = note.Beat + opportunity * note.OpportunityIntervalBeats;
                                if (at > chosenShotBeat) break;
                                if (origin + chosenShotBeat < from) break;
                                if (origin + at + note.HoldBeats >= from && origin + at <= to)
                                    visit(note, origin + at, origin + at + note.HoldBeats, Lane(note));
                            }
                            continue;
                        }
                        double start = origin + (note.IsChargedRelease ?
                            Phrase.Notes[note.Prerequisite].Beat + Phrase.Notes[note.Prerequisite].HoldBeats : note.Beat);
                        double end = start + note.HoldBeats;
                        if (end >= from && start <= to) visit(note, start, end, Lane(note));
                    }
        }
    }
}
