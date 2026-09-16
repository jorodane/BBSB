using System;
using System.Collections.Generic;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    public readonly struct TrackNote
    {
        public int Slot { get; }
        public int Index { get; }
        public double CycleStart { get; }
        public WeaponPhraseNote Definition { get; }
        public bool IsPreview { get; }
        public bool ReleaseParry { get; }
        public double Beat => CycleStart + Definition.Beat;
        public double EndBeat => Beat + Definition.HoldBeats;
        internal TrackNote(int slot, int index, double cycleStart, WeaponPhrase phrase, bool preview)
        {
            Slot = slot; Index = index; CycleStart = cycleStart; Definition = phrase.Notes[index];
            IsPreview = preview; ReleaseParry = Definition.IsParry && phrase.ParryInput == ParryInputEdge.KeyUp;
        }
        internal bool SameNote(TrackNote other) => Slot == other.Slot && Index == other.Index &&
            Math.Abs(CycleStart - other.CycleStart) < .000001;
    }

    public readonly struct BrokenTrackNote
    {
        public const double LifetimeBeats = .5;
        public TrackNote Note { get; }
        public double BrokenAt { get; }
        public double HeadDistance { get; }
        public double TailDistance { get; }
        public bool TailVisible { get; }
        internal BrokenTrackNote(TrackNote note, double beat)
        {
            Note = note; BrokenAt = beat;
            HeadDistance = SteppedNoteTrack.Distance(note.Beat, beat);
            TailDistance = SteppedNoteTrack.Distance(note.EndBeat, beat);
            TailVisible = SteppedNoteTrack.InHorizon(note.EndBeat - beat);
        }
        public double Progress(double beat) => Math.Max(0, Math.Min(1, (beat - BrokenAt) / LifetimeBeats));
    }

    // Presentation-only projection. Ghost notes never enter the battle's note states,
    // input selection, deadlines, damage, or combo calculation.
    public sealed class FiveLaneNoteTimeline
    {
        public const int MaxNotesPerLane = 128;
        private readonly List<TrackNote> notes = new List<TrackNote>();
        private readonly List<TrackNote> previous = new List<TrackNote>();
        private readonly List<BrokenTrackNote> broken = new List<BrokenTrackNote>();
        private FiveLaneBattle source;
        public IReadOnlyList<TrackNote> Notes { get; }
        public IReadOnlyList<BrokenTrackNote> Broken { get; }
        public FiveLaneNoteTimeline()
        { Notes = notes.AsReadOnly(); Broken = broken.AsReadOnly(); }

        public void Refresh(FiveLaneBattle battle)
        {
            if (!ReferenceEquals(source, battle))
            { source = battle; notes.Clear(); previous.Clear(); broken.Clear(); }
            if (battle == null) return;
            previous.Clear(); previous.AddRange(notes); notes.Clear();
            broken.RemoveAll(note => note.Progress(battle.Beat) >= 1);
            for (int slot = 0; slot < battle.Lanes.Count; slot++) Collect(battle, slot);
            foreach (var old in previous)
            {
                if (old.EndBeat < battle.Beat - battle.HalfMissWindow || Contains(old) || !Canceled(battle, old)) continue;
                // Only notes the player could actually see become fragments. A preview
                // promoted to a live note keeps its identity and does not burst.
                if (broken.Count == MaxNotesPerLane * RunRules.WeaponSlots) broken.RemoveAt(0);
                broken.Add(new BrokenTrackNote(old, battle.Beat));
            }
        }

        private void Collect(FiveLaneBattle battle, int slot)
        {
            var lane = battle.Lanes[slot];
            if (lane.Phase != PhraseLanePhase.Playing) return;
            int first = notes.Count;
            for (int i = 0; i < lane.Phrase.Notes.Count && notes.Count - first < MaxNotesPerLane; i++)
            {
                var state = lane.NoteStates[i];
                if (state == PhraseNoteState.Pending || state == PhraseNoteState.Holding || state == PhraseNoteState.Locked)
                    Add(battle, new TrackNote(slot, i, lane.StartBeat, lane.Phrase, state == PhraseNoteState.Locked));
            }
            if (!lane.CanRepeat) return;
            double start = lane.StartBeat;
            // Repeated addition matches the battle's cycle clock, including authored
            // fractional lengths. The budget also bounds tiny custom phrase lengths.
            for (int cycle = 0; cycle < MaxNotesPerLane && notes.Count - first < MaxNotesPerLane; cycle++)
            {
                double next = start + lane.Phrase.LengthBeats;
                if (next <= start || next > battle.Beat + SteppedNoteTrack.LookAheadBeats) break;
                start = next;
                for (int i = 0; i < lane.Phrase.Notes.Count && notes.Count - first < MaxNotesPerLane; i++)
                    Add(battle, new TrackNote(slot, i, start, lane.Phrase, true));
            }
        }
        private void Add(FiveLaneBattle battle, TrackNote note)
        {
            if (note.Beat <= battle.Beat + SteppedNoteTrack.LookAheadBeats && note.EndBeat >= battle.Beat - battle.HalfMissWindow)
                notes.Add(note);
        }
        private bool Contains(TrackNote note)
        {
            foreach (var current in notes) if (current.SameNote(note)) return true;
            return false;
        }
        private static bool Canceled(FiveLaneBattle battle, TrackNote note)
        {
            var lane = battle.Lanes[note.Slot];
            if (note.CycleStart > lane.StartBeat + .000001) return !lane.CanRepeat;
            if (Math.Abs(note.CycleStart - lane.StartBeat) > .000001) return false;
            var state = lane.NoteStates[note.Index];
            return state == PhraseNoteState.Skipped || state == PhraseNoteState.Missed;
        }
    }
}
