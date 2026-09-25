using System;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    public enum AttackMotionMode { Single, Combo, Sustain }

    public readonly struct AttackMotionState
    {
        public bool Active { get; }
        public AttackMotionMode Mode { get; }
        public double StartBeat { get; }
        public double EndBeat { get; }
        public double SustainStartBeat { get; }
        public double ContactBeats { get; }
        public double RecoverAtBeat { get; }
        public int Stroke { get; }
        public bool Continues { get; }
        public bool Canceled { get; }
        public int Direction => (Stroke & 1) == 0 ? 1 : -1;
        public AttackMotionState(AttackMotionMode mode, double start, double end, double sustainStart,
            double contact, double recover, int stroke, bool continues, bool canceled = false)
        {
            Active = true; Mode = mode; StartBeat = start; EndBeat = end; SustainStartBeat = sustainStart;
            ContactBeats = contact; RecoverAtBeat = recover; Stroke = stroke; Continues = continues; Canceled = canceled;
        }
    }

    // Player and enemy attacks share note-based classification and recovery, in song beats.
    public static class FiveLaneAttackMotion
    {
        public const double ComboGapBeats = .5, RecoveryBeats = .28;
        public static bool Connects(double end, double next) => next >= end - .000001 && next - end <= ComboGapBeats + .000001;
        public static AttackMotionMode Classify(bool hold, bool next, bool previous) =>
            hold ? AttackMotionMode.Sustain : next || previous ? AttackMotionMode.Combo : AttackMotionMode.Single;

        public static bool ContinuesEnemy(FiveLaneBattle battle, IncomingBeatAttack attack)
        {
            IncomingBeatAttack previous = null;
            foreach (var candidate in battle.Incoming)
                if (candidate.Definition.MonsterId == attack.Definition.MonsterId && candidate.Beat < attack.Beat &&
                    (previous == null || candidate.Beat > previous.Beat)) previous = candidate;
            return previous != null && previous.State != IncomingAttackState.Blocked &&
                previous.State != IncomingAttackState.Interrupted && Connects(previous.EndBeat, attack.Beat);
        }

        public static AttackMotionState Player(FiveLaneBattle battle, PhraseLane lane)
        {
            var played = lane.LastPerformedNote;
            if (played == null || battle.Beat < played.StartedAtBeat) return default;
            bool next = Connects(played.EndBeat, played.ExpectedNextBeat);
            bool continues = played.ChainIndex > 0;
            var mode = Classify(played.Definition.IsHold, next, continues);
            bool local = FiveLaneWeaponMotion.Local(lane);
            double contact = PlayerContactBeats(battle, lane);
            bool waiting = !played.Canceled && lane.Phase == PhraseLanePhase.Playing &&
                lane.InvokedAtBeat == played.InvocationBeat && next &&
                (lane.Holding || Math.Abs(lane.NextBeat - played.ExpectedNextBeat) < .000001);
            double recovery = played.Definition.IsHold ? played.EndBeat : played.StartedAtBeat + contact + .1;
            if (mode == AttackMotionMode.Single && !local)
                recovery = played.StartedAtBeat + WeaponMotion.Duration(FiveLaneWeaponMotion.Style(lane)) * battle.Bpm / 60 - RecoveryBeats;
            if (waiting || played.Canceled && next)
                recovery = Math.Max(recovery, played.ExpectedNextBeat + battle.HalfMissWindow);
            if (played.Canceled) recovery = Math.Min(recovery, played.CanceledAtBeat);
            if (battle.Beat > recovery + RecoveryBeats) return default;
            return new AttackMotionState(mode, played.StartedAtBeat, played.EndBeat, played.SustainStartedAtBeat,
                contact, recovery, played.Ordinal, continues, played.Canceled);
        }

        // Effects may outlive the moving weapon. Their contact time must not change
        // when its pose retires, or a completed slash could flash a second time.
        public static double PlayerContactBeats(FiveLaneBattle battle, PhraseLane lane)
        {
            var played = lane.LastPerformedNote;
            if (played == null) return 0;
            var mode = Classify(played.Definition.IsHold, Connects(played.EndBeat, played.ExpectedNextBeat), played.ChainIndex > 0);
            return FiveLaneWeaponMotion.Local(lane) ? .18 : mode == AttackMotionMode.Single ?
                WeaponMotion.ImpactSeconds(FiveLaneWeaponMotion.Style(lane)) * battle.Bpm / 60 : .22;
        }

        public static bool SustainingPlayer(FiveLaneBattle battle, PhraseLane lane)
        {
            var state = Player(battle, lane);
            return state.Active && !state.Canceled && state.Mode == AttackMotionMode.Sustain && lane.Holding &&
                battle.Beat < state.EndBeat && (state.Continues || battle.Beat >= state.StartBeat + state.ContactBeats);
        }

        public static IncomingBeatAttack CurrentEnemyAttack(FiveLaneBattle battle, string id)
        {
            IncomingBeatAttack current = null;
            foreach (var attack in battle.Incoming)
            {
                if (attack.Definition.MonsterId != id ||
                    battle.Beat < attack.Beat - FiveLaneArtTimeline.RushBeats) continue;
                // A linked strike starts at its own beat; its anticipation must not
                // restart the previous stroke or change an active Hold's spin phase.
                if (battle.Beat < attack.Beat &&
                    (attack.State == IncomingAttackState.Interrupted || ContinuesEnemy(battle, attack))) continue;
                if (current == null || attack.Beat > current.Beat) current = attack;
            }
            return current;
        }

        public static AttackMotionState Enemy(FiveLaneBattle battle, string id)
        {
            var monster = battle.Formation?.Find(id);
            if (monster != null && (monster.Health.Defeated || battle.Beat < monster.GroggyUntilBeat) ||
                monster == null && battle.IsGroggy) return default;
            var attack = CurrentEnemyAttack(battle, id); if (attack == null) return default;
            IncomingBeatAttack next = null;
            foreach (var candidate in battle.Incoming)
            {
                if (candidate.Definition.MonsterId != id || candidate.State == IncomingAttackState.Interrupted) continue;
                if (candidate.Beat > attack.Beat && (next == null || candidate.Beat < next.Beat)) next = candidate;
            }
            bool before = ContinuesEnemy(battle, attack);
            bool after = next != null && Connects(attack.EndBeat, next.Beat);
            var mode = Classify(attack.Definition.IsHold, after, before);
            double start = before ? attack.Beat : attack.Beat - FiveLaneArtTimeline.RushBeats;
            double end = attack.EndBeat;
            bool canceled = attack.State == IncomingAttackState.Blocked || attack.State == IncomingAttackState.Interrupted;
            double recovery = Math.Max(end + .12, start + FiveLaneArtTimeline.RushBeats + .08);
            if (after) recovery = Math.Max(recovery, next.Beat);
            if (canceled) recovery = Math.Min(recovery, attack.ResolvedBeat);
            if (battle.Beat > recovery + RecoveryBeats) return default;
            return new AttackMotionState(mode, start, end, attack.SustainStartBeat, FiveLaneArtTimeline.RushBeats,
                recovery, attack.SequenceIndex, before, canceled);
        }

        public static double Recovery(AttackMotionState state, double beat) => state.Active ?
            WeaponFormation.Smooth((beat - state.RecoverAtBeat) / RecoveryBeats) : 1;

        public static WeaponMotionFrame Sample(AttackMotionState state, double beat, WeaponMotionFrame launch,
            WeaponMotionFrame home, BattlePathPoint target, double height, double aspect, bool local)
        {
            if (!state.Active) return home;
            double age = Math.Max(0, Math.Min(beat, state.RecoverAtBeat) - state.StartBeat);
            double approach = state.Continues ? 1 : WeaponFormation.Smooth(age / Math.Max(.001, state.ContactBeats));
            double x = launch.Position.X + (target.X - launch.Position.X) * approach;
            double y = launch.Position.Y + (target.Y - launch.Position.Y) * approach;
            double phase = Math.Min(1, age / Math.Max(.001, state.ContactBeats));
            double rotation = local ? Math.Sin(phase * Math.PI * 2) * 20 :
                state.Direction * (75 - 150 * WeaponFormation.Smooth(phase));
            double scale = 1.15;
            if (state.Mode == AttackMotionMode.Sustain)
            {
                double sustain = Math.Max(0, Math.Min(beat, Math.Min(state.EndBeat, state.RecoverAtBeat)) - state.SustainStartBeat);
                double turn = sustain * Math.PI * 4;
                x += Math.Sin(turn) * height * .065 / aspect * approach;
                y += Math.Cos(turn) * height * .05 * approach;
                rotation = local ? Math.Sin(turn) * 24 : sustain * 720;
                scale += Math.Sin(turn) * .07;
            }
            else if (state.Mode == AttackMotionMode.Combo)
            {
                y += state.Direction * Math.Sin(phase * Math.PI) * height * .12;
                x += Math.Sin(phase * Math.PI * 2) * height * .06 / aspect;
            }
            double recover = Recovery(state, beat);
            // Take the short route home after a spin, rather than unwinding every revolution.
            double returnRotation = (home.Rotation - rotation) % 360;
            if (returnRotation > 180) returnRotation -= 360;
            if (returnRotation < -180) returnRotation += 360;
            return new WeaponMotionFrame(new BattlePathPoint(x + (home.Position.X - x) * recover,
                y + (home.Position.Y - y) * recover), rotation + returnRotation * recover,
                scale + (home.Scale - scale) * recover);
        }

        // Small body motion; weapons/projectiles carry the larger swing or spin.
        // Position is measured in body heights, with facing +1 for the player, -1 for enemies.
        public static WeaponMotionFrame Body(AttackMotionState state, double beat, int facing)
        {
            if (!state.Active) return new WeaponMotionFrame(new BattlePathPoint(0, 0), 0, 1);
            double age = Math.Max(0, Math.Min(beat, state.RecoverAtBeat) - state.StartBeat);
            double phase = Math.Min(1, age / Math.Max(.001, state.ContactBeats));
            double reach = state.Continues ? 1 : WeaponFormation.Smooth(phase);
            double pulse = state.Mode == AttackMotionMode.Sustain ?
                Math.Sin(Math.Max(0, Math.Min(beat, Math.Min(state.EndBeat, state.RecoverAtBeat)) - state.SustainStartBeat) * Math.PI * 4) :
                state.Direction * Math.Sin(phase * Math.PI);
            double keep = 1 - Recovery(state, beat);
            return new WeaponMotionFrame(new BattlePathPoint(facing * .16 * reach * keep, .025 * pulse * keep),
                (-facing * 5 * reach + pulse * 4) * keep, 1 + .025 * Math.Abs(pulse) * keep);
        }
    }
}
