using System;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    public enum MonsterAnimationSituation { Idle, Call, Attack, Recover, Hit, Perfect, HalfMiss, Miss, Defeated }
    public readonly struct MonsterAnimationFrame
    {
        public MonsterAnimationSituation Situation { get; }
        public PlannedAttack Attack { get; }
        public int CallOffsetTick { get; }
        public double AgeBeats { get; }
        public MonsterAnimationFrame(MonsterAnimationSituation situation, double ageBeats, PlannedAttack attack = null, int callOffsetTick = 0)
        { Situation = situation; AgeBeats = Math.Max(0, ageBeats); Attack = attack; CallOffsetTick = callOffsetTick; }
    }
    /// <summary>Animation is sampled from song time, never used as the input/damage clock.</summary>
    public static class MonsterAnimationTimeline
    {
        public static MonsterAnimationFrame Evaluate(RhythmRound round, MonsterPlan monster, double seconds)
        {
            double beat = round.BeatSeconds;
            if (round.Combat != null && round.Combat.Victory && seconds >= round.Combat.FinaleAtSeconds)
                return new MonsterAnimationFrame(MonsterAnimationSituation.Defeated, (seconds - round.Combat.FinaleAtSeconds) / beat);
            var frame = new MonsterAnimationFrame(MonsterAnimationSituation.Idle, seconds / beat);
            double latest = double.NegativeInfinity; int priority = -1;
            void Select(MonsterAnimationSituation situation, double at, double until, int order, PlannedAttack attack = null, int offset = 0)
            {
                if (seconds < at || seconds >= until || at < latest || (at == latest && order < priority)) return;
                latest = at; priority = order;
                frame = new MonsterAnimationFrame(situation, (seconds - at) / beat, attack, offset);
            }
            foreach (var attack in monster.Attacks)
            {
                if (round.Combat != null && !round.Combat.Allows(attack)) continue;
                for (int i = 0; i < attack.Call.Count; i++)
                {
                    double at = attack.Call[i].Tick * beat / 4;
                    double until = (i + 1 < attack.Call.Count ? attack.Call[i + 1].Tick : attack.ResponseStartTick) * beat / 4;
                    Select(MonsterAnimationSituation.Call, at, until, 3, attack, attack.Call[i].Tick - attack.CallStartTick);
                }
                foreach (var step in attack.Placement.Pattern.Steps)
                {
                    double at = (attack.ResponseStartTick + step.OffsetTick) * beat / 4;
                    double end = at + Math.Max(.5, step.DurationTicks / 4.0) * beat;
                    Select(MonsterAnimationSituation.Attack, at, end, 1, attack);
                    Select(MonsterAnimationSituation.Recover, end, end + beat * .5, 0, attack);
                }
            }
            foreach (var result in round.Results)
            {
                if (result.Note.Attack.MonsterId != monster.InstanceId) continue;
                var situation = result.Grade == RhythmGrade.Perfect ? MonsterAnimationSituation.Perfect :
                    result.Grade == RhythmGrade.HalfMiss ? MonsterAnimationSituation.HalfMiss : MonsterAnimationSituation.Miss;
                Select(situation, result.JudgedAtSeconds, result.JudgedAtSeconds + beat * .5, 2, result.Note.Attack);
            }
            var hit = BattleHitFeedback.Monster(round.Combat, monster.InstanceId, seconds, beat);
            if (hit.Active) Select(MonsterAnimationSituation.Hit, hit.At, hit.At + BattleHitFeedback.ReactionSeconds, 4);
            return frame;
        }
    }
}
