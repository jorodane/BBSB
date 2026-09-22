using System;

namespace BBSB.Core
{
    public sealed partial class FiveLaneBattle
    {
        private double formationGeneratedThrough = -Epsilon;
        public EncounterFormation Formation { get; }
        public bool RequestVanguardChange(string monsterId)
        {
            if (IsPaused || Finished || Formation == null) return false;
            return Formation.RequestSwitch(monsterId, Beat);
        }
        private void CutFutureAttacks(string monsterId, double boundary)
        {
            foreach (var attack in incoming)
            {
                if (attack.Definition.MonsterId != monsterId || attack.State != IncomingAttackState.Pending) continue;
                if (attack.Beat >= boundary)
                { attack.State = IncomingAttackState.Interrupted; attack.ResolvedBeat = Beat; }
                else if (attack.Definition.IsHold && attack.EndBeat > boundary) attack.CutoffBeat = boundary;
            }
            foreach (var lane in lanes)
            {
                if (!lane.Holding) continue;
                double until = double.NegativeInfinity;
                foreach (var defense in holdDefenses)
                    if (ReferenceEquals(defense.Lane, lane) && heldInputs[defense.Slot] && defense.Attack.State == IncomingAttackState.Pending)
                        until = Math.Max(until, defense.Attack.EndBeat);
                lane.GuardUntilBeat = until;
            }
            foreach (var contact in shieldContacts)
            {
                if (contact.Phase != PhraseLanePhase.Playing) continue;
                contact.GuardUntilBeat = double.NegativeInfinity;
                foreach (var defense in holdDefenses)
                    if (defense.Slot == contact.Slot && heldInputs[defense.Slot] && defense.Attack.State == IncomingAttackState.Pending)
                        contact.GuardUntilBeat = Math.Max(contact.GuardUntilBeat, defense.Attack.EndBeat);
            }
        }
    }
}
