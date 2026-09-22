using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public sealed class ShieldContact
    {
        public PhraseLane Lane { get; }
        public int Slot { get; }
        public AttackRank Rank { get; internal set; }
        public PhraseLanePhase Phase { get; internal set; }
        public double StartBeat { get; internal set; }
        public double GuardUntilBeat { get; internal set; }
        public double ReadyAtBeat { get; internal set; }
        public double LastJudgedBeat { get; internal set; } = double.NegativeInfinity;
        public WeaponPhrase Phrase { get; internal set; }
        public RhythmGrade Grade { get; internal set; }
        public bool ParryRecovered { get; internal set; }
        public string Feedback { get; internal set; } = "READY";
        public double EndBeat => Math.Max(StartBeat + Phrase.Notes[0].HoldBeats, GuardUntilBeat);
        internal ShieldContact(PhraseLane lane, int slot)
        { Lane = lane; Slot = slot; Rank = lane.Placement.OffsetOf(slot) == 0 ? AttackRank.Front : AttackRank.Rear; Phrase = lane.Phrase; }
    }

    public sealed partial class FiveLaneBattle
    {
        public const decimal ResonanceThreshold = 12, ResonanceCapacity = 24, ResonanceAttackMultiplier = 1.5m;
        private readonly List<ShieldContact> shieldContacts = new List<ShieldContact>();
        private readonly List<int> shieldSlots = new List<int>();
        private int nextFrontShield;
        public IReadOnlyList<ShieldContact> ShieldContacts => shieldContacts.AsReadOnly();
        public decimal LightResonance { get; private set; }
        public decimal DarkResonance { get; private set; }
        public ShieldContact ShieldAt(int slot) => shieldContacts.Find(x => x.Slot == slot);
        public static bool IsSplitShield(PhraseLane lane) => lane != null && lane.Weapon.DefinitionId == "wide-shield";
        public AttackRank ShieldRankAt(int slot) => shieldSlots.Count > 1 && slot == shieldSlots[shieldSlots.Count - 1] ?
            AttackRank.Rear : AttackRank.Front;
        private void InitializeShieldRouting()
        {
            foreach (int slot in BattleInputLayout.DisplayOrder)
                if (LaneAt(slot) != null && WeaponCatalog.Find(LaneAt(slot).Weapon.DefinitionId).Kind == WeaponKind.Shield)
                    shieldSlots.Add(slot);
            foreach (var contact in shieldContacts) contact.Rank = ShieldRankAt(contact.Slot);
        }
        private void RouteShieldAttack(IncomingBeatAttack attack)
        {
            if (shieldSlots.Count == 0) return;
            if (attack.Definition.Rank == AttackRank.Rear)
            {
                if (shieldSlots.Count > 1) attack.ShieldSlot = shieldSlots[shieldSlots.Count - 1];
                return;
            }
            int frontCount = Math.Max(1, shieldSlots.Count - 1);
            attack.ShieldSlot = shieldSlots[nextFrontShield];
            nextFrontShield = (nextFrontShield + 1) % frontCount;
        }
        public IEnumerable<int> RequiredHeldSlots
        {
            get
            {
                foreach (var lane in lanes) if (lane.Holding) yield return lane.HoldingSlot;
                foreach (var contact in shieldContacts) if (contact.Phase == PhraseLanePhase.Playing) yield return contact.Slot;
            }
        }
        public bool RequiresHeldInput(int slot)
        { foreach (int required in RequiredHeldSlots) if (required == slot) return true; return false; }
        public bool CoversAttack(PhraseLane lane, int slot, IncomingBeatAttack attack)
        {
            if (lane == null || attack == null || !ReferenceEquals(LaneAt(slot), lane)) return false;
            // The physical destination is committed once, together with the preview.
            // Non-shield parry weapons retain their authored front-rank behavior.
            return WeaponCatalog.Find(lane.Weapon.DefinitionId).Kind == WeaponKind.Shield ?
                attack.ShieldSlot == slot : attack.Definition.Rank == AttackRank.Front;
        }
        private void PressSplitShield(PhraseLane lane, int slot)
        {
            var contact = ShieldAt(slot);
            if (contact.Phase != PhraseLanePhase.Ready) return;
            contact.StartBeat = WeaponAttributes.SnapStart(lane.Weapon.Attribute, Beat);
            contact.Phrase = lane.Patterns.For(lane.Placement.OffsetOf(slot), WeaponAttributes.SideAt(contact.StartBeat));
            contact.GuardUntilBeat = double.NegativeInfinity;
            contact.Phase = PhraseLanePhase.Playing; contact.Grade = RhythmGrade.Perfect; contact.ParryRecovered = false;
            bool parried = TryParry(lane, out var grade, slot);
            if (parried) { contact.Grade = grade; contact.ParryRecovered = true; }
            contact.Feedback = parried ? "PARRY / HOLD" : "GUARD"; contact.LastJudgedBeat = Beat;
            AttachActiveHolds(lane, slot, contact.Grade);
        }
        private void EndSplitShield(ShieldContact contact, bool complete)
        {
            if (contact.Phase != PhraseLanePhase.Playing) return;
            if (complete)
            {
                if (contact.Grade == RhythmGrade.Perfect) PerfectCount++; else HalfMissCount++;
                Combo++; contact.Lane.Activations++;
                decimal bonus = contact.Phrase.Notes[0].BonusHealing * (contact.Grade == RhythmGrade.Perfect ? 1 : .5m) *
                    (1m + .25m * contact.Lane.Weapon.Level);
                decimal healed = Math.Min(PlayerMaximum - PlayerHealth, bonus);
                PlayerHealth += healed; TotalHealed += healed; if (healed > 0) PlayerHealthChanged?.Invoke(PlayerHealth);
            }
            double cooldown = contact.Phrase.CompletionCooldownBeats;
            contact.ReadyAtBeat = contact.ParryRecovered ? Beat : Beat + Math.Max(Math.Min(1, cooldown), cooldown - Bonuses.CooldownReduction);
            contact.Phase = contact.ParryRecovered ? PhraseLanePhase.Ready : PhraseLanePhase.Cooldown;
            contact.Feedback = complete ? "HOLD OK" : "GUARD END"; contact.LastJudgedBeat = Beat;
            // The final impact is sampled after hold completion at the same beat.
            // Keep the held contact's snapshot until release or attack resolution.
            if (!complete) holdDefenses.RemoveAll(x => x.Slot == contact.Slot && ReferenceEquals(x.Lane, contact.Lane));
        }
        private double ShieldDeadline(ShieldContact contact) => contact.Phase == PhraseLanePhase.Playing ? contact.EndBeat :
            contact.Phase == PhraseLanePhase.Cooldown ? contact.ReadyAtBeat : double.PositiveInfinity;
        private void AdvanceShields()
        {
            foreach (var contact in shieldContacts)
            {
                if (ShieldDeadline(contact) > Beat) continue;
                if (contact.Phase == PhraseLanePhase.Playing) EndSplitShield(contact, true);
                else { contact.Phase = PhraseLanePhase.Ready; contact.Feedback = "READY"; }
            }
        }
        private void SetReduction(IncomingBeatAttack attack, decimal reduction, PhraseLane source, bool parry = false)
        {
            if (reduction < attack.Reduction || reduction == attack.Reduction && attack.ReductionSource != null) return;
            attack.Reduction = reduction; attack.ReductionSource = source; attack.ReductionByParry = parry;
        }
        private void SampleTapGuard(IncomingBeatAttack attack)
        {
            foreach (var lane in lanes)
                if (lane.Holding && CoversAttack(lane, lane.HoldingSlot, attack)) SetReduction(attack, GuardReduction(lane, lane.HoldingSlot), lane);
            foreach (var contact in shieldContacts)
                if (contact.Phase == PhraseLanePhase.Playing && Beat < contact.EndBeat && CoversAttack(contact.Lane, contact.Slot, attack))
                    SetReduction(attack, GuardReduction(contact.Lane, contact.Slot), contact.Lane);
        }
        private void AccumulateResonance(IncomingBeatAttack attack, decimal reduced)
        {
            if (reduced <= 0 || attack.ReductionByParry || attack.ReductionSource?.Weapon.DefinitionId != "resonance-shield") return;
            if (WeaponAttributes.SideAt(attack.Beat) == WeaponBeatSide.Light) LightResonance = Math.Min(ResonanceCapacity, LightResonance + reduced);
            else DarkResonance = Math.Min(ResonanceCapacity, DarkResonance + reduced);
        }
        private decimal ConsumeResonance(WeaponBeatSide side)
        {
            if (side == WeaponBeatSide.Light && LightResonance >= ResonanceThreshold)
            { LightResonance -= ResonanceThreshold; return ResonanceAttackMultiplier; }
            if (side == WeaponBeatSide.Dark && DarkResonance >= ResonanceThreshold)
            { DarkResonance -= ResonanceThreshold; return ResonanceAttackMultiplier; }
            return 1;
        }
    }
}
