using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public sealed class CallSignal
    {
        public int OffsetTick { get; }
        public string Label { get; }
        public CallSignal(int offsetTick, string label)
        {
            if (offsetTick < 0 || string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Invalid Call signal.");
            OffsetTick = offsetTick; Label = label;
        }
    }

    public sealed class MonsterDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public RhythmPattern Pattern { get; }
        public IReadOnlyList<CallSignal> Call { get; }
        // A phrase includes the last instantaneous beat's space; RestTicks follows that phrase.
        public int ResponseTicks { get; }
        public int RestTicks { get; }
        public double ParticipationChance { get; }
        // Base damage for each resolved response note, including sustained gestures.
        public int DamagePerNote { get; }

        public MonsterDefinition(string id, string name, string description, RhythmPattern pattern,
            IEnumerable<CallSignal> call, int responseTicks, int restTicks, double participationChance, int damagePerNote = 4)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A monster needs an ID and name.");
            if (pattern == null || call == null) throw new ArgumentNullException(pattern == null ? nameof(pattern) : nameof(call));
            if (responseTicks <= 0 || responseTicks < pattern.EndOffsetTick || restTicks < 0 || responseTicks > int.MaxValue - restTicks)
                throw new ArgumentException("Invalid response/rest timing.");
            if (double.IsNaN(participationChance) || participationChance <= 0 || participationChance > 1)
                throw new ArgumentOutOfRangeException(nameof(participationChance));
            if (damagePerNote < 0) throw new ArgumentOutOfRangeException(nameof(damagePerNote));
            var signals = new List<CallSignal>(call);
            if (signals.Count == 0 || signals.Exists(x => x == null)) throw new ArgumentException("A monster needs Call signals.");
            signals.Sort((a, b) => a.OffsetTick.CompareTo(b.OffsetTick));
            for (int i = 0; i < signals.Count; i++)
                if (signals[i].OffsetTick >= pattern.CueLeadTicks || (i > 0 && signals[i - 1].OffsetTick == signals[i].OffsetTick))
                    throw new ArgumentException("Call signals must be distinct and precede the Response.");
            if (signals[0].OffsetTick != 0) throw new ArgumentException("The first Call signal must mark the cue start.");
            if (!InputCompatibility.IsPlayable(pattern)) throw new ArgumentException("The monster's pattern contains conflicting touch requirements.");
            Id = id; Name = name; Description = description ?? ""; Pattern = pattern; Call = signals.AsReadOnly();
            ResponseTicks = responseTicks; RestTicks = restTicks; ParticipationChance = participationChance;
            DamagePerNote = damagePerNote;
        }
    }

    public sealed class MonsterProposal
    {
        public string InstanceId { get; }
        public MonsterDefinition Monster { get; }
        public IReadOnlyList<PatternPlacement> Placements { get; }
        public MonsterProposal(string instanceId, MonsterDefinition monster, IEnumerable<PatternPlacement> placements)
        {
            if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("An instance needs an ID.");
            if (monster == null || placements == null) throw new ArgumentNullException(monster == null ? nameof(monster) : nameof(placements));
            var copy = new List<PatternPlacement>(placements);
            if (copy.Exists(x => x == null || x.Pattern != monster.Pattern)) throw new ArgumentException("Proposal patterns must belong to the monster.");
            copy.Sort((a, b) => a.StartTick.CompareTo(b.StartTick));
            for (int i = 1; i < copy.Count; i++)
            {
                long phraseEnd = (long)copy[i - 1].StartTick + monster.ResponseTicks;
                if (copy[i].StartTick < phraseEnd + monster.RestTicks || copy[i].CueStartTick < phraseEnd)
                    throw new ArgumentException("A monster must finish its Response and rest before repeating.");
            }
            InstanceId = instanceId; Monster = monster; Placements = copy.AsReadOnly();
        }
    }

    public sealed class ScheduledCall
    {
        public string AttackId { get; }
        public string MonsterId { get; }
        public int Tick { get; }
        public string Label { get; }
        internal ScheduledCall(string attackId, string monsterId, int tick, string label)
        { AttackId = attackId; MonsterId = monsterId; Tick = tick; Label = label; }
    }

    public sealed class PlannedAttack
    {
        public string Id { get; }
        public string MonsterId { get; }
        public MonsterDefinition Monster { get; }
        public PatternPlacement Placement { get; }
        public int CallStartTick => Placement.CueStartTick;
        public int ResponseStartTick => Placement.StartTick;
        public int ResponseEndTick => Placement.EndTick;
        public IReadOnlyList<ScheduledCall> Call { get; }
        internal PlannedAttack(MonsterProposal proposal, PatternPlacement placement)
        {
            MonsterId = proposal.InstanceId; Monster = proposal.Monster; Placement = placement;
            Id = MonsterId + "@" + placement.StartTick;
            var call = new List<ScheduledCall>();
            foreach (var signal in Monster.Call) call.Add(new ScheduledCall(Id, MonsterId, CallStartTick + signal.OffsetTick, signal.Label));
            Call = call.AsReadOnly();
        }
    }

    public sealed class MonsterPlan
    {
        public string InstanceId { get; }
        public MonsterDefinition Monster { get; }
        public IReadOnlyList<PlannedAttack> Attacks { get; }
        public int ProposedCount { get; }
        public int OccupiedBeatCount { get; }
        internal MonsterPlan(MonsterProposal proposal, List<PlannedAttack> attacks, int occupiedBeatCount)
        {
            InstanceId = proposal.InstanceId; Monster = proposal.Monster; ProposedCount = proposal.Placements.Count;
            Attacks = attacks.AsReadOnly(); OccupiedBeatCount = occupiedBeatCount;
        }
    }

    public sealed class PlanWithdrawal
    {
        public PlannedAttack Attack { get; }
        public string KeptMonsterId { get; }
        public int ConflictTick { get; }
        public int YieldingOccupiedBeats { get; }
        public int KeptOccupiedBeats { get; }
        internal PlanWithdrawal(PlannedAttack attack, string keptMonsterId, int tick, int yieldingBeats, int keptBeats)
        { Attack = attack; KeptMonsterId = keptMonsterId; ConflictTick = tick; YieldingOccupiedBeats = yieldingBeats; KeptOccupiedBeats = keptBeats; }
    }

    /// <summary>One immutable plan shared by preparation and the upcoming battle player. No individual HP.</summary>
    public sealed class BattlePlan
    {
        public MusicStage Stage { get; }
        public IReadOnlyList<MonsterPlan> Monsters { get; }
        public IReadOnlyList<PlannedAttack> Attacks { get; }
        public IReadOnlyList<ScheduledCall> Calls { get; }
        public IReadOnlyList<PlanWithdrawal> Withdrawals { get; }
        internal BattlePlan(MusicStage stage, List<MonsterPlan> monsters, List<PlanWithdrawal> withdrawals)
        {
            Stage = stage; Monsters = monsters.AsReadOnly(); Withdrawals = withdrawals.AsReadOnly();
            var attacks = new List<PlannedAttack>(); var calls = new List<ScheduledCall>();
            foreach (var monster in monsters) foreach (var attack in monster.Attacks) { attacks.Add(attack); calls.AddRange(attack.Call); }
            attacks.Sort((a, b) => a.ResponseStartTick != b.ResponseStartTick ? a.ResponseStartTick.CompareTo(b.ResponseStartTick) : string.CompareOrdinal(a.Id, b.Id));
            calls.Sort((a, b) => a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick) : string.CompareOrdinal(a.AttackId, b.AttackId));
            Attacks = attacks.AsReadOnly(); Calls = calls.AsReadOnly();
        }
    }
}
