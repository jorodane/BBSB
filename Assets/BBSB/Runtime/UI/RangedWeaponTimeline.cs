using System;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    public enum RangedWeaponPose { Idle, Prepare, Release }

    public readonly struct RangedWeaponFrame
    {
        public RangedWeaponPose Pose { get; }
        public double Tension { get; }
        public PlannedAttack Target { get; }
        public RangedWeaponFrame(RangedWeaponPose pose, double tension, PlannedAttack target)
        { Pose = pose; Tension = tension; Target = target; }
    }

    // Preparation observes the original note; only a real weapon activation releases a shot.
    public static class RangedWeaponTimeline
    {
        public const double ReleaseSeconds = .09;

        public static RangedWeaponFrame Evaluate(WeaponBattle combat, int slot, double seconds, double beat)
        {
            if (!(beat > 0) || double.IsNaN(seconds) || combat == null || slot < 0 || slot >= combat.Loadout.Equipment.Count ||
                !WeaponCatalog.Find(combat.Loadout.Equipment[slot].DefinitionId).IsRanged) return default;
            double release = Math.Min(ReleaseSeconds, beat * .3);
            for (int i = combat.Activations.Count - 1; i >= 0; i--)
            {
                var activation = combat.Activations[i]; double age = seconds - activation.AtSeconds;
                if (age >= release) break;
                if (activation.Slot == slot && age >= 0)
                    return new RangedWeaponFrame(RangedWeaponPose.Release, 1 - age / release, activation.Target);
            }
            ResponseNote selected = null; double selectedAt = double.PositiveInfinity;
            foreach (var binding in combat.Bindings)
            {
                var note = binding.Note;
                if (binding.Slot != slot || note.Result != null || !combat.Allows(note.Attack)) continue;
                double at = note.Step.Kind == GestureKind.Hold || note.Step.Kind == GestureKind.Dive ? note.EndSeconds : note.StartSeconds;
                double lead = Math.Min(.20, beat * .45);
                bool held = note.State == ResponseState.Holding && (note.Step.Kind == GestureKind.Hold || note.Step.Kind == GestureKind.Dive);
                if (seconds < note.StartSeconds - lead || seconds > at + beat * .24 || (!held && seconds < at - lead)) continue;
                if (at < selectedAt) { selected = note; selectedAt = at; }
            }
            if (selected == null) return default;
            double start = selected.State == ResponseState.Holding ? selected.StartSeconds : selectedAt - Math.Min(.20, beat * .45);
            double tension = Math.Max(.12, Math.Min(1, (seconds - start) / Math.Max(.001, selectedAt - start)));
            return new RangedWeaponFrame(RangedWeaponPose.Prepare, tension, selected.Attack);
        }

        public static BattlePathPoint Projectile(BattlePathPoint from, BattlePathPoint to, double progress, int lane = 0)
        {
            double p = Math.Max(0, Math.Min(1, progress));
            return new BattlePathPoint(from.X + (to.X - from.X) * p,
                from.Y + (to.Y - from.Y) * p + Math.Sin(Math.PI * p) * lane * .055);
        }
    }
}
