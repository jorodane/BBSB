using System;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    public enum MonsterAttackPhase { Hidden, Spawn, Wait, Travel, Contact, Perfect, HalfMiss, Miss }

    public readonly struct MonsterAttackFrame
    {
        public MonsterAttackPhase Phase { get; }
        public double Progress { get; }
        public double Lift { get; }
        public double PhaseAge { get; }
        public double AnimationBeat { get; }
        public double ReactionProgress { get; }
        public double ShakeProgress { get; }
        public bool Shielded { get; }
        public bool Visible => Phase != MonsterAttackPhase.Hidden;
        public bool IsReaction => Phase >= MonsterAttackPhase.Perfect;
        public string ImageName => Phase == MonsterAttackPhase.HalfMiss ? "half-miss" : Phase.ToString().ToLowerInvariant();

        internal MonsterAttackFrame(MonsterAttackPhase phase, double progress, double lift, double age,
            double beat, double reaction = 0, double shake = 0, bool shielded = false)
        { Phase = phase; Progress = progress; Lift = lift; PhaseAge = age; AnimationBeat = beat;
            ReactionProgress = reaction; ShakeProgress = shake; Shielded = shielded; }
    }

    /// <summary>Samples the music clock; never emits Calls, resolves inputs or changes combat state.</summary>
    public static class MonsterAttackTimeline
    {
        public const double ReactionSeconds = .32;

        public static MonsterAttackFrame Evaluate(ResponseNote note, MonsterAttackDefinition art,
            double seconds, double beatSeconds, double halfMissWindow)
        {
            double spawn = art.SpawnSeconds(note, beatSeconds);
            double start = note.StartSeconds, end = note.EndSeconds;
            if (seconds < spawn) return default;
            double animation = (seconds - spawn) / beatSeconds;
            if (note.Result != null)
            {
                // An early miss must not destroy a shot before its authored attack; sustained hazards keep their full interval.
                double reacted = Math.Max(end, note.Result.JudgedAtSeconds);
                if (note.Step.Kind == GestureKind.Tap && note.Result.Grade != RhythmGrade.Miss)
                    reacted = Math.Max(reacted, note.Result.JudgedAtSeconds + PlayerMotionTimeline.TapPreparationDuration(beatSeconds));
                double age = seconds - reacted;
                if (age >= ReactionSeconds) return default;
                if (age >= 0)
                {
                    var phase = note.Result.Grade == RhythmGrade.Perfect ? MonsterAttackPhase.Perfect :
                        note.Result.Grade == RhythmGrade.HalfMiss ? MonsterAttackPhase.HalfMiss : MonsterAttackPhase.Miss;
                    return new MonsterAttackFrame(phase, 1, 0, age, animation, age / ReactionSeconds,
                        note.ShakeProgress, note.Result.BlockedDamage > 0);
                }
            }
            if (seconds >= start)
            {
                if (note.Result == null && seconds > end + halfMissWindow) return default;
                return new MonsterAttackFrame(MonsterAttackPhase.Contact, 1, 0, seconds - start, animation,
                    shake: note.ShakeProgress);
            }
            if (art.Motion == MonsterAttackMotion.Materialize) return default;

            double duration = Math.Max(.000001, start - spawn);
            double t = Clamp((seconds - spawn) / duration), progress = t, lift = 0;
            double phaseAge = seconds - spawn;
            var state = phaseAge < Math.Min(beatSeconds * .16, duration * .16) ? MonsterAttackPhase.Spawn : MonsterAttackPhase.Travel;
            switch (art.Motion)
            {
                case MonsterAttackMotion.WaitRush:
                    double launch = start - Math.Min(duration, art.RushTicks * beatSeconds / RhythmTime.TicksPerBeat);
                    progress = Clamp((seconds - launch) / Math.Max(.000001, start - launch));
                    if (seconds < launch) { state = state == MonsterAttackPhase.Spawn ? state : MonsterAttackPhase.Wait; }
                    else { state = MonsterAttackPhase.Travel; phaseAge = seconds - launch; }
                    break;
                case MonsterAttackMotion.Walk:
                    double walked = (seconds - spawn) / beatSeconds;
                    double whole = Math.Floor(walked), part = walked - whole;
                    // Feet rest during the first part of each beat, then land exactly on the next beat.
                    double stride = Smooth((part - .40) / .60);
                    progress = Clamp((whole + stride) * beatSeconds / duration);
                    lift = Math.Sin(stride * Math.PI) * .055;
                    break;
                case MonsterAttackMotion.Relay:
                    int index = 0;
                    var calls = note.Attack.Call;
                    for (int i = 1; i < calls.Count; i++)
                        if (seconds >= calls[i].Tick * beatSeconds / RhythmTime.TicksPerBeat) index = i;
                    double a = calls[index].Tick * beatSeconds / RhythmTime.TicksPerBeat;
                    double b = index + 1 < calls.Count ? calls[index + 1].Tick * beatSeconds / RhythmTime.TicksPerBeat : start;
                    double fraction = Clamp((seconds - a) / Math.Max(.000001, b - a));
                    progress = (index + Smooth(fraction)) / calls.Count;
                    lift = Math.Sin(fraction * Math.PI) * art.Arc;
                    phaseAge = seconds - a;
                    state = fraction < .18 ? MonsterAttackPhase.Spawn : MonsterAttackPhase.Travel;
                    break;
                case MonsterAttackMotion.Lob: lift = Math.Sin(t * Math.PI) * art.Arc; break;
                case MonsterAttackMotion.Extend: progress = Smooth(t); break;
            }
            return new MonsterAttackFrame(state, progress, lift, phaseAge, animation);
        }

        public static double Clamp(double value) => Math.Max(0, Math.Min(1, value));
        private static double Smooth(double value) { double t = Clamp(value); return t * t * (3 - 2 * t); }
    }
}
