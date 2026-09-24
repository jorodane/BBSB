using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public sealed partial class FiveLaneBattle
    {
        private sealed class HoldDefense
        {
            public PhraseLane Lane;
            public int Slot;
            public IncomingBeatAttack Attack;
            public decimal Reduction;
            public bool Parry;
        }
        private readonly List<HoldDefense> holdDefenses = new List<HoldDefense>();

        private WeaponPhrase GuardPhrase(PhraseLane lane, int slot) => IsSplitShield(lane) ? ShieldAt(slot).Phrase : lane.Phrase;
        private decimal GuardReduction(PhraseLane lane, int slot)
        {
            decimal reduction = GuardPhrase(lane, slot).HoldDamageReduction;
            return reduction <= 0 ? 0 : Math.Min(1m, reduction * lane.EffectMultiplier + Bonuses.GuardBonus);
        }

        private double AttackDeadline(IncomingBeatAttack attack)
        {
            if (attack.Definition.IsHold && !attack.Started) return attack.Beat;
            return attack.ImpactSampled ? attack.NextImpactBeat + HalfMissWindow + Epsilon : attack.NextImpactBeat;
        }

        private double ParryBeat(IncomingBeatAttack attack)
        {
            if (!attack.Definition.IsHold) return attack.Beat;
            // The first entry uses the head judgment. A late entry uses the nearest
            // still-unresolved half-beat pulse; settled damage is never refunded.
            if (Beat <= attack.Beat + HalfMissWindow + Epsilon &&
                Math.Abs(Beat - attack.Beat) <= Math.Abs(Beat - attack.NextImpactBeat)) return attack.Beat;
            return attack.NextImpactBeat;
        }

        private void AttachActiveHolds(PhraseLane lane, int slot, RhythmGrade grade)
        {
            foreach (var attack in incoming)
                if (attack.State == IncomingAttackState.Pending && attack.Definition.IsHold &&
                    attack.Beat <= Beat && attack.EndBeat > Beat)
                    AttachHoldGuard(lane, slot, grade, attack);
        }

        private void AttachHoldGuard(PhraseLane lane, int slot, RhythmGrade grade, IncomingBeatAttack attack)
        {
            var phrase = GuardPhrase(lane, slot); var note = phrase.Notes[IsSplitShield(lane) ? 0 : lane.NextNote];
            if (note.IsCall || !heldInputs[slot] || !note.IsHold || phrase.HoldDamageReduction <= 0 || !CoversAttack(lane, slot, attack)) return;
            decimal reduction = GuardReduction(lane, slot) *
                (grade == RhythmGrade.Perfect ? 1m : .5m);
            BindHoldDefense(lane, slot, attack, reduction);
        }

        private void BindHoldDefense(PhraseLane lane, int slot, IncomingBeatAttack attack, decimal reduction, bool parry = false)
        {
            // Snapshot once per held contact. Repeated frames and subsequent ticks
            // cannot upgrade the entry grade or extend protection to another attack.
            if (!holdDefenses.Exists(x => ReferenceEquals(x.Lane, lane) && x.Slot == slot && ReferenceEquals(x.Attack, attack)))
                holdDefenses.Add(new HoldDefense { Lane = lane, Slot = slot, Attack = attack, Reduction = reduction, Parry = parry });
            var contact = IsSplitShield(lane) ? ShieldAt(slot) : null;
            if (contact != null) contact.GuardUntilBeat = Math.Max(contact.GuardUntilBeat, attack.EndBeat);
            else lane.GuardUntilBeat = Math.Max(lane.GuardUntilBeat, attack.EndBeat);
        }

        private decimal HoldReduction(IncomingBeatAttack attack)
        {
            foreach (var defense in holdDefenses)
                if (ReferenceEquals(defense.Attack, attack) && heldInputs[defense.Slot])
                    SetReduction(attack, defense.Reduction, defense.Lane, defense.Parry);
            return attack.Reduction;
        }

        private void ResolveImpact(IncomingBeatAttack attack)
        {
            decimal cumulative = attack.DamageBudget;
            if (attack.Definition.IsHold && attack.NextImpactBeat < attack.EndBeat - Epsilon)
                cumulative = attack.Definition.Damage * (decimal)((attack.PulseIndex + 1) * .5) / (decimal)attack.Definition.HoldBeats;
            decimal damage = cumulative - attack.DistributedDamage;
            attack.DistributedDamage = cumulative;
            decimal reduced = damage * attack.Reduction, taken = damage - reduced;
            AccumulateResonance(attack, reduced);
            if (attack.Reduction >= 1) TotalBlocked += reduced; else TotalReduced += reduced;
            attack.DamageTaken += taken; attack.LastPulseReduction = attack.Reduction;
            attack.LastPulseBeat = attack.NextImpactBeat;
            if (taken > 0)
            {
                LastHitBeat = Beat; PlayerHealth = Math.Max(0, PlayerHealth - taken);
                PlayerHealthChanged?.Invoke(PlayerHealth);
            }
            if (!attack.Definition.IsHold || attack.NextImpactBeat >= attack.EndBeat - Epsilon)
            {
                attack.State = attack.DamageTaken == 0 ? IncomingAttackState.Blocked : IncomingAttackState.Hit;
                attack.ResolvedBeat = Beat;
            }
            else
            {
                attack.PulseIndex++; attack.ImpactSampled = false; attack.Reduction = 0; attack.ReductionSource = null; attack.ReductionByParry = false;
            }
        }
    }
}
