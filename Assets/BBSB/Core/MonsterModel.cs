using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public sealed class CallSignal
    {
        public int OffsetTick { get; }
        public string Label { get; }
        public CallSound Sound { get; }
        public CallMotion Motion { get; }
        public CallSignal(int offsetTick, string label, CallSound sound = CallSound.Wood, CallMotion motion = CallMotion.Hop)
        {
            if (offsetTick < 0 || string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Invalid Call signal.");
            if (!Enum.IsDefined(typeof(CallSound), sound) || !Enum.IsDefined(typeof(CallMotion), motion))
                throw new ArgumentException("Unknown Call sound or motion.");
            OffsetTick = offsetTick; Label = label; Sound = sound; Motion = motion;
        }
    }

    public sealed class MonsterPatternDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public RhythmPattern Pattern { get; }
        public IReadOnlyList<CallSignal> Call { get; }
        // A phrase includes the last instantaneous beat's space; RestTicks follows that phrase.
        public int ResponseTicks { get; }
        public int RestTicks { get; }
        public int CueAlignmentTicks { get; }
        public double ParticipationChance { get; }
        public MonsterPatternDefinition(string name, string description, RhythmPattern pattern,
            IEnumerable<CallSignal> call, int responseTicks, int restTicks, double participationChance, int cueAlignmentTicks = 1)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A pattern needs a name.");
            if (pattern == null || call == null) throw new ArgumentNullException(pattern == null ? nameof(pattern) : nameof(call));
            if (responseTicks <= 0 || responseTicks < pattern.EndOffsetTick || restTicks < 0 || responseTicks > int.MaxValue - restTicks)
                throw new ArgumentException("Invalid response/rest timing.");
            if (double.IsNaN(participationChance) || participationChance <= 0 || participationChance > 1)
                throw new ArgumentOutOfRangeException(nameof(participationChance));
            if (cueAlignmentTicks <= 0) throw new ArgumentOutOfRangeException(nameof(cueAlignmentTicks));
            var signals = new List<CallSignal>(call);
            if (signals.Count == 0 || signals.Exists(x => x == null)) throw new ArgumentException("A monster needs Call signals.");
            signals.Sort((a, b) => a.OffsetTick.CompareTo(b.OffsetTick));
            for (int i = 0; i < signals.Count; i++)
                if (signals[i].OffsetTick >= pattern.CueLeadTicks || (i > 0 && signals[i - 1].OffsetTick == signals[i].OffsetTick))
                    throw new ArgumentException("Call signals must be distinct and precede the Response.");
            if (signals[0].OffsetTick != 0) throw new ArgumentException("The first Call signal must mark the cue start.");
            if (!InputCompatibility.IsPlayable(pattern)) throw new ArgumentException("The monster's pattern contains conflicting touch requirements.");
            Id = pattern.Id; Name = name; Description = description ?? ""; Pattern = pattern; Call = signals.AsReadOnly();
            ResponseTicks = responseTicks; RestTicks = restTicks; ParticipationChance = participationChance;
            CueAlignmentTicks = cueAlignmentTicks;
        }
    }

    public sealed class MonsterDefinition
    {
        public string Id { get; }
        public string ArtId { get; }
        public string Name { get; }
        public string Description { get; }
        public GestureKind MainGesture { get; }
        public double EncounterWeight { get; }
        public IReadOnlyList<MonsterPatternDefinition> Patterns { get; }
        public int DamagePerNote { get; }

        public MonsterDefinition(string id, string name, string description, GestureKind mainGesture,
            IEnumerable<MonsterPatternDefinition> patterns, double encounterWeight = 1, int damagePerNote = 4, string artId = null)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A monster needs an ID and name.");
            if (!Enum.IsDefined(typeof(GestureKind), mainGesture)) throw new ArgumentOutOfRangeException(nameof(mainGesture));
            if (patterns == null) throw new ArgumentNullException(nameof(patterns));
            SlotTemplate.ValidateWeight(encounterWeight);
            if (damagePerNote < 0) throw new ArgumentOutOfRangeException(nameof(damagePerNote));
            var copy = new List<MonsterPatternDefinition>(patterns);
            var ids = new HashSet<string>();
            if (copy.Count == 0) throw new ArgumentException("A monster needs patterns.");
            foreach (var pattern in copy)
                if (pattern == null || !ids.Add(pattern.Id)) throw new ArgumentException("Monster pattern IDs must be distinct.");
            for (int i = 0; i < copy.Count; i++)
                for (int j = i + 1; j < copy.Count; j++) CallReadability.Validate(copy[i], copy[j]);
            Id = id; ArtId = artId ?? id; Name = name; Description = description ?? ""; MainGesture = mainGesture;
            Patterns = copy.AsReadOnly(); EncounterWeight = encounterWeight; DamagePerNote = damagePerNote;
        }

        // Single-pattern authored fixtures and integrations can still use the original constructor.
        public MonsterDefinition(string id, string name, string description, RhythmPattern pattern,
            IEnumerable<CallSignal> call, int responseTicks, int restTicks, double participationChance, int damagePerNote = 4)
            : this(id, name, description, pattern == null ? GestureKind.Tap : pattern.Steps[0].Kind,
                new[] { new MonsterPatternDefinition(name, description, pattern, call, responseTicks, restTicks, participationChance) },
                damagePerNote: damagePerNote) { }

        public MonsterPatternDefinition FindPattern(RhythmPattern pattern)
        {
            foreach (var entry in Patterns) if (ReferenceEquals(entry.Pattern, pattern)) return entry;
            throw new ArgumentException("The pattern must belong to this monster.", nameof(pattern));
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
            var seen = new HashSet<(string, int)>();
            foreach (var placement in copy)
            {
                if (placement == null) throw new ArgumentException("Null placement.");
                monster.FindPattern(placement.Pattern);
                if (!seen.Add((placement.Pattern.Id, placement.StartTick))) throw new ArgumentException("Duplicate pattern occurrence.");
            }
            // Patterns submit independently. Timing conflicts, including self-overlaps, belong to the resolver.
            copy.Sort((a, b) => a.StartTick != b.StartTick ? a.StartTick.CompareTo(b.StartTick) : string.CompareOrdinal(a.Pattern.Id, b.Pattern.Id));
            InstanceId = instanceId; Monster = monster; Placements = copy.AsReadOnly();
        }
    }

    public sealed class ScheduledCall
    {
        public string AttackId { get; }
        public string MonsterId { get; }
        public int Tick { get; }
        public string Label { get; }
        public CallSound Sound { get; }
        public CallMotion Motion { get; }
        internal ScheduledCall(string attackId, string monsterId, int tick, CallSignal signal)
        { AttackId = attackId; MonsterId = monsterId; Tick = tick; Label = signal.Label; Sound = signal.Sound; Motion = signal.Motion; }
    }

    public sealed class PlannedAttack
    {
        public string Id { get; }
        public string MonsterId { get; }
        public MonsterDefinition Monster { get; }
        public PatternPlacement Placement { get; }
        public MonsterPatternDefinition Pattern { get; }
        public int CallStartTick => Placement.CueStartTick;
        public int ResponseStartTick => Placement.StartTick;
        public int ResponseEndTick => Placement.EndTick;
        public int PhraseEndTick => ResponseStartTick + Pattern.ResponseTicks;
        public IReadOnlyList<ScheduledCall> Call { get; }
        internal PlannedAttack(MonsterProposal proposal, PatternPlacement placement)
            : this(proposal.InstanceId, proposal.Monster, placement) { }

        internal PlannedAttack(string instanceId, MonsterDefinition monster, PatternPlacement placement)
        {
            MonsterId = instanceId; Monster = monster; Placement = placement;
            Pattern = Monster.FindPattern(placement.Pattern);
            Id = MonsterId + "/" + Pattern.Id + "@" + placement.StartTick;
            var call = new List<ScheduledCall>();
            foreach (var signal in Pattern.Call) call.Add(new ScheduledCall(Id, MonsterId, CallStartTick + signal.OffsetTick, signal));
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
            : this(proposal.InstanceId, proposal.Monster, proposal.Placements.Count, attacks, occupiedBeatCount) { }

        internal MonsterPlan(string instanceId, MonsterDefinition monster, int proposedCount, List<PlannedAttack> attacks, int occupiedBeatCount)
        {
            InstanceId = instanceId; Monster = monster; ProposedCount = proposedCount;
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
        public bool IsSelfConflict => Attack.MonsterId == KeptMonsterId;
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
        public IReadOnlyList<PlannedAttack> GapFills { get; }
        internal BattlePlan(MusicStage stage, List<MonsterPlan> monsters, List<PlanWithdrawal> withdrawals, List<PlannedAttack> gapFills = null)
        {
            Stage = stage; Monsters = monsters.AsReadOnly(); Withdrawals = withdrawals.AsReadOnly();
            GapFills = (gapFills ?? new List<PlannedAttack>()).AsReadOnly();
            var attacks = new List<PlannedAttack>(); var calls = new List<ScheduledCall>();
            foreach (var monster in monsters) foreach (var attack in monster.Attacks) { attacks.Add(attack); calls.AddRange(attack.Call); }
            attacks.Sort((a, b) => a.ResponseStartTick != b.ResponseStartTick ? a.ResponseStartTick.CompareTo(b.ResponseStartTick) : string.CompareOrdinal(a.Id, b.Id));
            calls.Sort((a, b) => a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick) : string.CompareOrdinal(a.AttackId, b.AttackId));
            Attacks = attacks.AsReadOnly(); Calls = calls.AsReadOnly();
        }
    }
}
