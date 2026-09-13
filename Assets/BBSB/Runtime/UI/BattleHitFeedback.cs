using System;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    public readonly struct BattleHitFrame
    {
        public double At { get; }
        public double Age { get; }
        public double Strength { get; }
        public double StopSeconds { get; }
        public bool Active => Strength > 0;
        public bool Stopped => Active && Age < StopSeconds;
        public double Envelope => Active ? Math.Pow(Math.Max(0, 1 - Math.Max(0, Age - StopSeconds) /
            (BattleHitFeedback.ReactionSeconds - StopSeconds)), 2) * Strength : 0;
        public double Shake => Math.Sin(Math.Max(0, Age - StopSeconds) * 105) * Envelope;
        public double Recoil => Math.Sin(Math.Min(1, Math.Max(0, Age - StopSeconds) / .18) * Math.PI) * Envelope;
        internal BattleHitFrame(double at, double age, double strength, double stop)
        { At = at; Age = age; Strength = strength; StopSeconds = stop; }
        public double PoseSeconds(double songSeconds) => Stopped ? At : songSeconds;
        public double PoseSeconds(double songSeconds, MonsterPlan plan, double beat)
        {
            if (!Stopped) return songSeconds;
            // A new enemy tell always interrupts a frozen body; calls and notes keep their real timing.
            foreach (var attack in plan.Attacks)
            foreach (var call in attack.Call)
            {
                double at = call.Tick * beat / RhythmTime.TicksPerBeat;
                if (at > At && at <= songSeconds) return songSeconds;
            }
            return At;
        }
    }

    // Pure presentation. Never touches timeScale, audio, note deadlines, health or input.
    public static class BattleHitFeedback
    {
        public const double ReactionSeconds = .28;
        public const double MaximumStopSeconds = .065;
        public static BattleHitFrame Sample(double at, double seconds, double strength, double beat)
        {
            double age = seconds - at;
            if (!(age >= 0 && age < ReactionSeconds) || !(strength > 0) || !(beat > 0)) return default;
            double amount = Math.Min(1, strength);
            // At fast tempos the next half-beat always gets a clean new cue.
            double stop = Math.Min(MaximumStopSeconds, Math.Min(beat * .18, .025 + .04 * amount));
            return new BattleHitFrame(at, age, amount, stop);
        }
        private static BattleHitFrame Stronger(BattleHitFrame a, BattleHitFrame b)
            => !a.Active || (b.Active && b.Envelope > a.Envelope) ? b : a;

        public static BattleHitFrame Monster(WeaponBattle combat, string monster, double seconds, double beat)
        {
            if (combat == null) return default;
            BattleHitFrame result = default;
            for (int i = combat.Activations.Count - 1; i >= 0; i--)
            {
                var hit = combat.Activations[i];
                if (seconds - hit.AtSeconds > 1.1) break;
                if (hit.Damage <= 0 || hit.Target.MonsterId != monster) continue;
                var sample = Sample(hit.AtSeconds + WeaponMotion.ImpactSeconds(hit.Action.Motion), seconds,
                    Math.Min(1, .3 + (double)hit.Damage / 55), beat);
                // Simultaneous weapons use the strongest feedback, never additive stops or shake.
                result = Stronger(result, sample);
            }
            return result;
        }
        public static BattleHitFrame Player(RhythmRound round, double seconds)
        {
            BattleHitFrame result = default;
            for (int i = round.Results.Count - 1; i >= 0; i--)
            {
                var hit = round.Results[i];
                if (seconds - hit.JudgedAtSeconds >= ReactionSeconds) break;
                if (hit.DamageTaken <= 0) continue;
                result = Stronger(result, Sample(hit.JudgedAtSeconds, seconds,
                    hit.Grade == RhythmGrade.Miss ? .9 : .48, round.BeatSeconds));
            }
            return result;
        }
    }
}
