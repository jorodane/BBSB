using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public enum IncomingAttackState { Pending, Blocked, Interrupted, Hit }
    public enum ScheduledStartState { Pending, Started, Skipped }

    // A bell reserves a pattern on the contacted physical line. It never fabricates
    // player input: taps, held contacts and parry timing still belong to the player.
    public sealed class ScheduledPhraseStart
    {
        public PhraseLane Target { get; }
        public int Slot { get; }
        public int StartOffset { get; }
        public double Beat { get; }
        public WeaponPhrase Phrase => Target.Patterns.For(StartOffset, WeaponAttributes.SideAt(Beat));
        public ScheduledStartState State { get; internal set; }
        public int SlotForNote(int index) => Target.InputSlots[(StartOffset + Phrase.Notes[index].LaneOffset) % Target.InputSlots.Count];
        internal ScheduledPhraseStart(PhraseLane target, int slot, double beat)
        {
            Target = target; Slot = slot; StartOffset = target.Placement.OffsetOf(slot);
            double first = target.PlanStartBeat(beat);
            var phrase = target.Patterns.For(StartOffset, WeaponAttributes.SideAt(first));
            // The bell already provides one beat of lead. Longer preparations still apply.
            Beat = first + Math.Max(0, Math.Ceiling(beat - 1 + phrase.FirstNoteDelayBeats - first - .000001));
        }
    }
    public sealed class BeatAttack
    {
        public string MonsterId { get; }
        public double Beat { get; }
        public decimal Damage { get; }
        public double HoldBeats { get; }
        public AttackRank Rank { get; }
        public bool IsHold => HoldBeats > 0;
        // Damage is the complete attack budget, distributed over its half-beat ticks.
        public BeatAttack(string monsterId, double beat, decimal damage, double holdBeats = 0, AttackRank rank = AttackRank.Front)
        {
            if (!WeaponPhraseNote.Finite(beat) || beat < 0 || damage < 0 ||
                !WeaponPhraseNote.Finite(holdBeats) || holdBeats < 0 || !Enum.IsDefined(typeof(AttackRank), rank)) throw new ArgumentOutOfRangeException(nameof(beat));
            MonsterId = monsterId; Beat = beat; Damage = damage; HoldBeats = holdBeats; Rank = rank;
        }
    }
    public sealed class IncomingBeatAttack
    {
        public BeatAttack Definition { get; }
        public double Beat { get; }
        public int SequenceIndex { get; internal set; }
        public double SustainStartBeat { get; internal set; }
        public int ShieldSlot { get; internal set; } = -1;
        internal double CutoffBeat = double.PositiveInfinity;
        public double EndBeat => Math.Min(Beat + Definition.HoldBeats, CutoffBeat);
        public double NextImpactBeat => Definition.IsHold ? Math.Min(EndBeat, Beat + (PulseIndex + 1) * .5) : Beat;
        public decimal DamageBudget => Definition.IsHold && CutoffBeat < Beat + Definition.HoldBeats ?
            Definition.Damage * (decimal)(EndBeat - Beat) / (decimal)Definition.HoldBeats : Definition.Damage;
        public double LastPulseBeat { get; internal set; } = double.NegativeInfinity;
        public double LastParryBeat { get; internal set; } = double.NegativeInfinity;
        public decimal DamageTaken { get; internal set; }
        public decimal LastPulseReduction { get; internal set; }
        public IncomingAttackState State { get; internal set; }
        public double ResolvedBeat { get; internal set; } = double.NegativeInfinity;
        internal bool ImpactSampled;
        internal bool Started;
        internal int PulseIndex;
        internal decimal DistributedDamage;
        internal decimal Reduction;
        internal PhraseLane ReductionSource;
        internal bool ReductionByParry;
        internal IncomingBeatAttack(BeatAttack definition, double beat) { Definition = definition; Beat = beat; SustainStartBeat = beat; }
    }
    public sealed class PhraseLane
    {
        public WeaponState Weapon { get; }
        public WeaponPhrase Phrase { get; private set; }
        public WeaponPhraseSet Patterns { get; }
        public WeaponPlacement Placement { get; }
        public ScheduledPhraseStart ScheduledOrigin { get; internal set; }
        public IReadOnlyList<int> InputSlots => Placement.Slots;
        public int StartOffset { get; private set; }
        public int Slot => InputSlots[StartOffset];
        public int HoldingSlot { get; internal set; } = -1;
        public int SlotForNote(int index) => InputSlots[(StartOffset + Phrase.Notes[index].LaneOffset) % InputSlots.Count];
        public PhraseLanePhase Phase { get; internal set; }
        public WeaponRhythmCycle Cycle { get; private set; }
        public double StartBeat => Cycle.StartBeat;
        public WeaponBeatSide ActiveSide => Cycle.Side;
        public bool IsTransition => Cycle.IsTransition;
        public decimal EffectMultiplier => Weapon.Attribute == WeaponAttribute.Chaos ? Patterns.Chaos.EffectMultiplier : 1m;
        private readonly int rhythmSeed;
        private int activationSequence;
        private int CycleSeed => unchecked(rhythmSeed + activationSequence * 16777619);
        public WeaponRhythmCycle NextCycle(WeaponRhythmCycle cycle)
        {
            if (cycle.Index == Cycle.Index && cycle.StartBeat == Cycle.StartBeat && ProjectedTimingShift > 0)
                cycle = new WeaponRhythmCycle(cycle.Phrase, cycle.Side, cycle.StartBeat + ProjectedTimingShift,
                    cycle.StableSinceBeat, cycle.Index, cycle.IsTransition, cycle.BaseIndex);
            return cycle.Next(Patterns, Weapon.Attribute, StartOffset, CycleSeed);
        }
        public int NextNote { get; internal set; }
        public bool Holding { get; internal set; }
        public double HoldStartedAtBeat { get; internal set; }
        public bool Charging => Holding && Phrase.ChargedResponseFor(NextNote) >= 0;
        public bool Loaded => Phase == PhraseLanePhase.Playing && !Holding && Phrase.Notes[NextNote].IsOptional;
        public double ChargeFraction(double beat) => Charging ? Math.Max(0, Math.Min(1,
            (beat - HoldStartedAtBeat) / Phrase.Notes[NextNote].HoldBeats)) : 0;
        internal long[] opportunitySteps;
        private double[] responseTimingOffsets;
        public long OpportunityStep(int index) => opportunitySteps[index];
        public bool SustainingGuard => Holding && GuardUntilBeat > NextBeat;
        public double HoldEndBeat => Charging ? HoldStartedAtBeat + Phrase.Notes[NextNote].HoldBeats :
            Math.Max(NextBeat + Phrase.Notes[NextNote].HoldBeats, GuardUntilBeat);
        public bool WaitingForParryRelease => Holding && GuardUntilBeat <= NextBeat + Phrase.Notes[NextNote].HoldBeats &&
            Phrase.Notes[NextNote].IsParry && Phrase.ParriesOnKeyUp;
        public double TimingShiftBeats { get; internal set; }
        public double GuardUntilBeat { get; internal set; } = double.NegativeInfinity;
        public double ProjectedTimingShift => TimingShiftBeats + (Holding ?
            Math.Max(0, GuardUntilBeat - NextBeat - Phrase.Notes[NextNote].HoldBeats) : 0);
        public double NoteBeat(int index) => StartBeat + Phrase.Notes[index].Beat + responseTimingOffsets[index] +
            opportunitySteps[index] * Phrase.Notes[index].OpportunityIntervalBeats +
            (Holding && index > NextNote ? ProjectedTimingShift : TimingShiftBeats);
        public bool InputHeld { get; internal set; }
        public int CompletedPhrases { get; internal set; }
        public double ReadyAtBeat { get; internal set; }
        public double LastJudgedBeat { get; internal set; } = double.NegativeInfinity;
        public RhythmGrade LastGrade { get; internal set; }
        public string Feedback { get; internal set; } = "READY";
        public decimal DamageDealt { get; internal set; }
        public decimal PendingDamageMultiplier { get; internal set; } = 1m;
        public double DamageBoostUntilBeat { get; internal set; } = double.NegativeInfinity;
        public double LastDamageBeat { get; internal set; } = double.NegativeInfinity;
        public string LastDamageMonsterId { get; internal set; }
        public int Activations { get; internal set; }
        public double InvokedAtBeat { get; internal set; } = double.NegativeInfinity;
        public PerformedWeaponNote LastPerformedNote { get; internal set; }
        internal int performedNotes;
        internal RhythmGrade HoldGrade;
        internal bool FailedCycle;
        internal bool ParryRecoveredCooldown;
        internal PhraseNoteState[] states;
        internal bool[] parried;
        public IReadOnlyList<PhraseNoteState> NoteStates { get; private set; }
        public bool CanRepeat => Phase == PhraseLanePhase.Playing && Cycle.CanContinue && !FailedCycle;
        public bool IsNoteVisible(int index) => Phase == PhraseLanePhase.Playing &&
            (states[index] == PhraseNoteState.Pending || states[index] == PhraseNoteState.Holding);
        public double NextBeat => NoteBeat(NextNote);
        internal PhraseLane(WeaponState weapon, WeaponPlacement placement, WeaponPhraseSet patterns, int seed)
        {
            Weapon = weapon.Copy();
            Placement = placement; Patterns = patterns; rhythmSeed = seed; SelectStart(0, WeaponAttributes.SnapStart(weapon.Attribute, 0));
        }
        internal double PlanStartBeat(double at)
        {
            var attribute = Weapon.Attribute;
            if (Patterns.RandomizeChaosSections)
            {
                int seed = unchecked(rhythmSeed + (activationSequence + 1) * 16777619);
                attribute = WeaponRhythmCycle.RandomSide(seed, 0) == WeaponBeatSide.Light ? WeaponAttribute.Light : WeaponAttribute.Dark;
            }
            return WeaponAttributes.SnapStart(attribute, at);
        }
        internal void SelectStart(int offset, double at, bool reserved = false)
        {
            double invokedAt = at;
            if (!reserved) at = PlanStartBeat(at);
            StartOffset = offset; activationSequence++;
            var side = WeaponAttributes.SideAt(at);
            var phrase = Patterns.For(offset, side);
            // Keep the chosen Light/Dark grid, and guarantee the configured lead.
            // A reservation already names the first judged beat, so do not delay it twice.
            if (!reserved && phrase.FirstNoteDelayBeats > 0)
                at += Math.Max(0, Math.Ceiling(invokedAt + phrase.FirstNoteDelayBeats - at - .000001));
            SelectCycle(new WeaponRhythmCycle(phrase, side, at, at, 0));
        }
        internal void SelectCycle(WeaponRhythmCycle cycle)
        {
            Cycle = cycle; Phrase = cycle.Phrase;
            TimingShiftBeats = 0; GuardUntilBeat = double.NegativeInfinity;
            states = new PhraseNoteState[Phrase.Notes.Count]; parried = new bool[states.Length];
            opportunitySteps = new long[states.Length];
            responseTimingOffsets = new double[states.Length];
            NoteStates = Array.AsReadOnly(states);
        }
        internal void PlaceReleaseFollowups(double actualBeat)
        {
            double offset = actualBeat - NextBeat;
            for (int i = NextNote + 1; i < Phrase.Notes.Count; i++)
                if (Phrase.Notes[i].IsInjected && Phrase.Notes[i].Prerequisite == NextNote)
                    responseTimingOffsets[i] += offset;
        }
    }

    /// <summary>Continuous five-lane combat. Input, notes, impacts and cooldowns share one musical clock.</summary>
    public sealed partial class FiveLaneBattle
    {
        private const double Epsilon = .000001;
        private readonly List<PhraseLane> lanes = new List<PhraseLane>();
        private readonly PhraseLane[] inputs = new PhraseLane[BattleInputLayout.LaneCount];
        private readonly bool[] heldInputs = new bool[BattleInputLayout.LaneCount];
        public InputExtensions Extensions { get; }
        public bool IsInputAvailable(int slot) => BattleInputLayout.Available(slot, Extensions);
        public PhraseLane LaneAt(int slot) => slot >= 0 && slot < inputs.Length ? inputs[slot] : null;
        public bool IsInputHeld(int slot) => slot >= 0 && slot < heldInputs.Length && heldInputs[slot];
        private readonly List<BeatAttack> schedule;
        private readonly List<IncomingBeatAttack> incoming = new List<IncomingBeatAttack>();
        private readonly Dictionary<string, int> attackSequences = new Dictionary<string, int>();
        private readonly List<ScheduledPhraseStart> scheduledStarts = new List<ScheduledPhraseStart>();
        private readonly double loopBeats;
        public MonsterAttackWindow AttackWindow { get; }
        private int nextAttack, cycle;
        public IReadOnlyList<PhraseLane> Lanes { get; }
        public IReadOnlyList<IncomingBeatAttack> Incoming { get; }
        public IReadOnlyList<ScheduledPhraseStart> ScheduledStarts { get; }
        public double Bpm { get; }
        public double Beat { get; private set; }
        public double PerfectWindow { get; }
        public double HalfMissWindow { get; }
        public double GroggyUntilBeat { get; private set; }
        public bool IsGroggy => Formation == null ? Beat < GroggyUntilBeat : Formation.Front != null && Beat < Formation.Front.GroggyUntilBeat;
        public bool IsPaused { get; private set; }
        public bool Aborted { get; private set; }
        public decimal PlayerHealth { get; private set; }
        public decimal PlayerMaximum { get; }
        public StageHealth EnemyHealth { get; }
        public CombatBonuses Bonuses { get; }
        public bool Victory => EnemyHealth.Defeated && PlayerHealth > 0;
        public bool Finished => Aborted || Victory || PlayerHealth == 0;
        public int PerfectCount { get; private set; }
        public int HalfMissCount { get; private set; }
        public int MissCount { get; private set; }
        public int Combo { get; private set; }
        public decimal TotalDamage { get; private set; }
        public decimal TotalBlocked { get; private set; }
        public decimal TotalReduced { get; private set; }
        public decimal TotalHealed { get; private set; }
        public double LastHitBeat { get; private set; } = double.NegativeInfinity;
        public event Action<decimal> PlayerHealthChanged;

        public FiveLaneBattle(IReadOnlyList<WeaponState> weapons, double bpm, double loopBeats,
            IEnumerable<BeatAttack> attacks, StageHealth enemyHealth, decimal playerHealth, decimal playerMaximum,
            IReadOnlyList<WeaponPhrase> phrases = null, RhythmRules rules = null,
            IReadOnlyList<WeaponPlacement> placements = null, IReadOnlyList<WeaponPhraseSet> phraseSets = null,
            InputExtensions extensions = InputExtensions.None, int rhythmSeed = 1, CombatBonuses bonuses = null,
            EncounterFormation formation = null, MonsterAttackWindow attackWindow = null)
        {
            if (weapons == null || weapons.Count < 1 || weapons.Count > RunRules.WeaponSlots)
                throw new ArgumentException("Equip between one and five weapons.");
            if (!WeaponPhraseNote.Finite(bpm) || bpm <= 0 || !WeaponPhraseNote.Finite(loopBeats) || loopBeats <= 0 ||
                playerMaximum <= 0 || playerHealth <= 0 || playerHealth > playerMaximum) throw new ArgumentOutOfRangeException(nameof(bpm));
            if (phrases != null && phrases.Count != weapons.Count) throw new ArgumentException("One phrase per weapon is required.");
            BattleInputLayout.Validate(extensions); Extensions = extensions;
            if (placements != null && placements.Count != weapons.Count || phraseSets != null && phraseSets.Count != weapons.Count)
                throw new ArgumentException("One placement and pattern set per weapon is required.");
            Bpm = bpm; this.loopBeats = loopBeats; EnemyHealth = enemyHealth ?? throw new ArgumentNullException(nameof(enemyHealth));
            Bonuses = bonuses ?? new CombatBonuses();
            Formation = formation;
            AttackWindow = attackWindow;
            if (Formation != null) Formation.FutureCut += CutFutureAttacks;
            PlayerHealth = playerHealth; PlayerMaximum = playerMaximum; rules ??= new RhythmRules(.09, .18);
            PerfectWindow = rules.PerfectSeconds * bpm / 60;
            // Adjacent half-beat starts remain unambiguous even in fast songs.
            HalfMissWindow = Math.Min(.24, rules.HalfMissSeconds * bpm / 60);
            PerfectWindow = Math.Min(PerfectWindow, HalfMissWindow * .75);
            int nextSlot = 0;
            var uniqueWeapons = new HashSet<WeaponState>();
            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] == null || !uniqueWeapons.Add(weapons[i])) throw new ArgumentException("Equip distinct item instances.");
                for (int j = 0; j < i; j++)
                    if (weapons[i].SameVariant(weapons[j])) throw new ArgumentException("The same weapon and attribute cannot be equipped twice.");
                var phrase = phrases == null ? WeaponPhraseCatalog.Find(weapons[i].DefinitionId) : phrases[i];
                if (phrase == null || phrase.WeaponId != weapons[i].DefinitionId) throw new ArgumentException("Phrase does not match its weapon.");
                WeaponPlacement placement;
                if (placements == null)
                {
                    var slots = new int[weapons[i].RequiredLanes];
                    for (int j = 0; j < slots.Length; j++) slots[j] = nextSlot++;
                    placement = new WeaponPlacement(weapons[i], slots);
                }
                else placement = placements[i];
                if (placement == null || !ReferenceEquals(placement.Weapon, weapons[i])) throw new ArgumentException("Placement does not match its item.");
                var patterns = phraseSets == null ? WeaponPhraseSet.Uniform(weapons[i], phrases == null ? null : phrase) : phraseSets[i];
                // Revalidate externally supplied sets against the actual equipment footprint.
                patterns = new WeaponPhraseSet(weapons[i], patterns?.LightStarts, patterns?.DarkStarts,
                    patterns?.LightTransitions, patterns?.DarkTransitions, patterns?.Chaos);
                patterns = WeaponNoteAssembly.Apply(weapons[i], patterns);
                var lane = new PhraseLane(weapons[i], placement, patterns, unchecked(rhythmSeed + i * 486187739)); lanes.Add(lane);
                if (IsSplitShield(lane)) foreach (int slot in placement.Slots) shieldContacts.Add(new ShieldContact(lane, slot));
                foreach (int slot in placement.Slots)
                {
                    if (!IsInputAvailable(slot)) throw new ArgumentException("This input extension is locked.");
                    if (inputs[slot] != null) throw new ArgumentException("Two weapons cannot occupy the same input line.");
                    inputs[slot] = lane;
                }
            }
            InitializeShieldRouting();
            schedule = new List<BeatAttack>(attacks ?? throw new ArgumentNullException(nameof(attacks)));
            foreach (var attack in schedule) if (attack == null || attack.Beat >= loopBeats ||
                attack.Beat + attack.HoldBeats > loopBeats + Epsilon) throw new ArgumentException("Attack outside music loop.");
            schedule.Sort((a, b) => a.Beat != b.Beat ? a.Beat.CompareTo(b.Beat) : string.CompareOrdinal(a.MonsterId, b.MonsterId));
            Lanes = lanes.AsReadOnly(); Incoming = incoming.AsReadOnly();
            ScheduledStarts = scheduledStarts.AsReadOnly(); EnsureIncoming(8);
        }

        public static FiveLaneBattle FromPlan(BattlePlan plan, IReadOnlyList<WeaponState> weapons,
            StageHealth enemyHealth, decimal health, decimal maximum, IReadOnlyList<WeaponPhrase> phrases = null,
            IReadOnlyList<WeaponPlacement> placements = null, IReadOnlyList<WeaponPhraseSet> phraseSets = null,
            InputExtensions extensions = InputExtensions.None, int rhythmSeed = 1, CombatBonuses bonuses = null, bool useFormation = true)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var formation = useFormation && plan.Monsters.Count > 0 ?
                new EncounterFormation(plan, enemyHealth, startBeat: MonsterAttackWindow.OpeningBeats) : null;
            var attacks = new List<BeatAttack>();
            double length = (double)plan.Stage.Music.TotalTicks / RhythmTime.TicksPerBeat;
            if (formation == null) foreach (var attack in plan.Attacks)
            foreach (var step in attack.Placement.Pattern.Steps)
            {
                // Hold keeps its complete interval. Other legacy gestures retain their impact timing.
                double duration = step.Kind == GestureKind.Hold ? (double)step.DurationTicks / RhythmTime.TicksPerBeat : 0;
                double at = (double)(attack.ResponseStartTick + step.OffsetTick + (duration > 0 ? 0 : step.DurationTicks)) / RhythmTime.TicksPerBeat;
                if (at < length) attacks.Add(new BeatAttack(attack.MonsterId, at, attack.Monster.DamagePerNote * attack.JudgmentWeight, duration));
            }
            return new FiveLaneBattle(weapons, plan.Stage.Music.Bpm, length, attacks, enemyHealth, health, maximum, phrases,
                placements: placements, phraseSets: phraseSets, extensions: extensions, rhythmSeed: rhythmSeed, bonuses: bonuses, formation: formation,
                attackWindow: new MonsterAttackWindow(length, plan.Stage.Music.Bpm));
        }

        public void Pause() { if (!Finished) IsPaused = true; }
        public void Resume() { if (!Finished) IsPaused = false; }
        public void Stop() { Aborted = true; IsPaused = true; }

        public void Press(int slot, double atBeat)
        {
            ValidateInput(slot, atBeat);
            if (IsPaused || Finished) return;
            Advance(atBeat);
            if (Finished) return;
            var lane = inputs[slot];
            if (lane == null || heldInputs[slot]) return;
            heldInputs[slot] = true; lane.InputHeld = true;
            if (IsSplitShield(lane))
            {
                foreach (var start in scheduledStarts)
                    if (start.State == ScheduledStartState.Pending && start.Slot == slot && start.Beat - Beat <= HalfMissWindow + Epsilon)
                    { StartScheduled(start); break; }
                PressSplitShield(lane, slot); return;
            }
            // The forecasted first note has the same early window as every other note,
            // even when the target's previous cooldown extends beyond the reservation.
            if (lane.Phase != PhraseLanePhase.Playing)
                foreach (var start in scheduledStarts)
                    if (start.State == ScheduledStartState.Pending && start.Slot == slot &&
                        start.Beat - Beat <= HalfMissWindow + Epsilon)
                    { StartScheduled(start); break; }
            if (lane.Phase == PhraseLanePhase.Cooldown || lane.Holding) return;
            bool starting = lane.Phase == PhraseLanePhase.Ready;
            if (starting)
            {
                // A fresh input chooses the phrase's phase; it is not a timing test.
                // Repeats stay Playing and never receive this opening grace again.
                BeginActivation(lane, slot, Beat);
                if (lane.Phrase.FirstNoteDelayBeats > 0)
                { lane.Feedback = "CALL"; return; }
            }
            // A different occupied line is a distinct input. It cannot hit this line's note
            // or switch the starting variant midway through an activation.
            if (slot != lane.SlotForNote(lane.NextNote)) return;
            double error = Math.Abs(Beat - lane.NextBeat);
            var note = lane.Phrase.Notes[lane.NextNote];
            if (note.IsChargedRelease) return; // This response belongs to its draw's key-up.
            if (lane.Cycle.Index == 0 && lane.NextNote == 0 && lane.Phrase.FirstNoteDelayBeats > 0 &&
                Beat < lane.NextBeat - HalfMissWindow - Epsilon) return;
            bool pressParry = note.IsParry && lane.Phrase.ParriesOnKeyDown;
            if (!starting && error > HalfMissWindow + Epsilon) { Miss(lane); return; }
            var grade = starting || error <= PerfectWindow + Epsilon ? RhythmGrade.Perfect : RhythmGrade.HalfMiss;
            if (pressParry)
            {
                // Entry grace accepts the note, but only an actual timed parry blocks
                // damage and unlocks counters. It must not retime the chosen phase.
                bool parried = TryParry(lane, out var parryGrade);
                // A Both hold may begin without a target; its release still requires
                // the authored timed parry, just like a release-only hold.
                if (!starting && !parried && lane.Phrase.ParryRequired && !lane.Phrase.ParriesOnKeyUp)
                { Miss(lane); lane.Feedback = "NO PARRY"; return; }
                lane.parried[lane.NextNote] = parried;
                if ((!starting || lane.GuardUntilBeat > Beat) && parried && parryGrade == RhythmGrade.HalfMiss) grade = RhythmGrade.HalfMiss;
                lane.LastGrade = grade; lane.LastJudgedBeat = Beat; lane.Feedback = parried ? "PARRY" : "GUARD";
            }
            if (!note.IsCall) AttachActiveHolds(lane, slot, grade);
            if (note.IsHold || lane.GuardUntilBeat > Beat)
            {
                RecordPerformance(lane, note);
                lane.HoldStartedAtBeat = Beat;
                lane.Holding = true; lane.HoldingSlot = slot; lane.states[lane.NextNote] = PhraseNoteState.Holding; lane.HoldGrade = grade;
                lane.Feedback = note.IsCall ? "CALL / HOLD" : lane.parried[lane.NextNote] ? "PARRY / HOLD" : lane.Phrase.HoldDamageReduction > 0 ? "GUARD" : "HOLD";
            }
            else Succeed(lane, grade);
        }

        public void Release(int slot, double atBeat)
        {
            ValidateInput(slot, atBeat);
            if (IsPaused || Finished) return;
            Advance(atBeat);
            var lane = inputs[slot];
            if (lane == null) return;
            heldInputs[slot] = false; lane.InputHeld = false;
            holdDefenses.RemoveAll(x => ReferenceEquals(x.Lane, lane) && x.Slot == slot);
            foreach (int input in lane.InputSlots) lane.InputHeld |= heldInputs[input];
            if (IsSplitShield(lane)) { EndSplitShield(ShieldAt(slot), false); return; }
            if (Finished || !lane.Holding || slot != lane.HoldingSlot) return;
            if (lane.Charging)
            {
                double fraction = lane.ChargeFraction(Beat);
                if (fraction <= Epsilon) { Miss(lane); return; }
                int response = lane.Phrase.ChargedResponseFor(lane.NextNote);
                var grade = lane.HoldGrade;
                // Holding past full charge delays subsequent calls by whole beats;
                // the weapon keeps its chosen downbeat/offbeat grid.
                lane.TimingShiftBeats += Math.Max(0, Math.Ceiling(Beat - lane.HoldEndBeat - Epsilon));
                Succeed(lane, grade);
                if (!Finished && lane.Phase == PhraseLanePhase.Playing && lane.NextNote == response)
                    Succeed(lane, grade, (decimal)fraction);
                return;
            }
            if (lane.WaitingForParryRelease)
            {
                double error = Math.Abs(Beat - lane.NextBeat - lane.Phrase.Notes[lane.NextNote].HoldBeats);
                if (error > HalfMissWindow + Epsilon || !TryParry(lane, out var parryGrade))
                { Miss(lane); lane.Feedback = "NO PARRY"; return; }
                var grade = error <= PerfectWindow + Epsilon ? RhythmGrade.Perfect : RhythmGrade.HalfMiss;
                if (parryGrade == RhythmGrade.HalfMiss || lane.HoldGrade == RhythmGrade.HalfMiss) grade = RhythmGrade.HalfMiss;
                lane.parried[lane.NextNote] = true;
                Succeed(lane, grade);
            }
            else if (lane.SustainingGuard && Beat + Epsilon >= lane.NextBeat + lane.Phrase.Notes[lane.NextNote].HoldBeats)
                Succeed(lane, lane.HoldGrade);
            else if (lane.Phrase.ReleaseEndsPhrase)
            {
                CancelPerformance(lane);
                lane.Holding = false; lane.states[lane.NextNote] = PhraseNoteState.Skipped;
                lane.Feedback = "GUARD END"; lane.LastJudgedBeat = Beat;
                Cooldown(lane, Beat + lane.Phrase.CompletionCooldownBeats);
            }
            else Miss(lane);
        }

        public void Advance(double atBeat)
        {
            if (!WeaponPhraseNote.Finite(atBeat) || atBeat < Beat - Epsilon) throw new ArgumentOutOfRangeException(nameof(atBeat));
            if (IsPaused || Finished) return;
            atBeat = Math.Max(Beat, atBeat);
            EnsureIncoming(Formation == null ? atBeat + 8 : Beat + Formation.Rules.PreviewBeats);
            // Resolve deadlines chronologically, including across a long frame or a song loop.
            while (!Finished)
            {
                double next = double.PositiveInfinity;
                if (Formation != null) next = Math.Min(Formation.NextDeadline, (Math.Floor(Beat * 2 + Epsilon) + 1) / 2);
                foreach (var lane in lanes) next = Math.Min(next, Deadline(lane));
                foreach (var contact in shieldContacts) next = Math.Min(next, ShieldDeadline(contact));
                foreach (var start in scheduledStarts)
                    if (start.State == ScheduledStartState.Pending) next = Math.Min(next, start.Beat);
                foreach (var attack in incoming) if (attack.State == IncomingAttackState.Pending)
                    next = Math.Min(next, AttackDeadline(attack));
                if (next > atBeat) break;
                Beat = Math.Max(Beat, next);
                if (Formation != null) { Formation.Advance(Beat); EnsureIncoming(Beat + Formation.Rules.PreviewBeats); }
                // Bind an already held guard before the weapon's authored hold deadline.
                foreach (var attack in incoming)
                    if (attack.State == IncomingAttackState.Pending && attack.Definition.IsHold && !attack.Started && attack.Beat <= Beat)
                    {
                        attack.Started = true;
                        foreach (var lane in lanes) if (lane.Holding) AttachHoldGuard(lane, lane.HoldingSlot, lane.HoldGrade, attack);
                        foreach (var contact in shieldContacts) if (contact.Phase == PhraseLanePhase.Playing)
                            AttachHoldGuard(contact.Lane, contact.Slot, contact.Grade, attack);
                    }
                AdvanceShields();
                foreach (var lane in lanes)
                {
                    if (Deadline(lane) > Beat) continue;
                    if (lane.Phase == PhraseLanePhase.Cooldown) { lane.Phase = PhraseLanePhase.Ready; lane.Feedback = "READY"; }
                    else if (lane.Holding && !lane.WaitingForParryRelease) Succeed(lane, lane.HoldGrade);
                    else Miss(lane);
                    if (Finished) break;
                }
                if (Finished) break;
                foreach (var start in scheduledStarts)
                    if (start.State == ScheduledStartState.Pending && start.Beat <= Beat) StartScheduled(start);
                foreach (var attack in incoming)
                {
                    if (attack.State != IncomingAttackState.Pending) continue;
                    if (!attack.ImpactSampled && attack.NextImpactBeat <= Beat)
                    {
                        // Sample protection at impact, not at the end of the late-parry grace window.
                        attack.ImpactSampled = true;
                        if (attack.Definition.IsHold) attack.Reduction = HoldReduction(attack);
                        else SampleTapGuard(attack);
                    }
                    if (attack.NextImpactBeat + HalfMissWindow + Epsilon > Beat) continue;
                    ResolveImpact(attack);
                    if (Finished) break;
                }
            }
            if (!Finished) Beat = atBeat;
            foreach (var lane in lanes) AdvanceOpportunities(lane);
            if (Formation != null && !Finished) EnsureIncoming(Beat + Formation.Rules.PreviewBeats);
            incoming.RemoveAll(x => x.State != IncomingAttackState.Pending && x.EndBeat < Beat - 4);
            holdDefenses.RemoveAll(x => x.Attack.State != IncomingAttackState.Pending || !heldInputs[x.Slot]);
            scheduledStarts.RemoveAll(x => x.State != ScheduledStartState.Pending && x.Beat < Beat - 4);
        }

        private void ValidateInput(int slot, double atBeat)
        {
            if (slot < 0 || slot >= BattleInputLayout.LaneCount) throw new ArgumentOutOfRangeException(nameof(slot));
            if (!WeaponPhraseNote.Finite(atBeat) || atBeat < Beat - Epsilon) throw new ArgumentOutOfRangeException(nameof(atBeat));
        }
        private bool TryParry(PhraseLane lane, out RhythmGrade grade, int inputSlot = -1)
        {
            if (inputSlot < 0) inputSlot = lane.SlotForNote(lane.NextNote);
            IncomingBeatAttack target = null; double nearest = double.PositiveInfinity;
            foreach (var attack in incoming)
                if (attack.State == IncomingAttackState.Pending && CoversAttack(lane, inputSlot, attack) && Math.Abs(ParryBeat(attack) - Beat) <= HalfMissWindow + Epsilon &&
                    Math.Abs(ParryBeat(attack) - Beat) < nearest)
                { target = attack; nearest = Math.Abs(ParryBeat(attack) - Beat); }
            grade = nearest <= PerfectWindow + Epsilon ? RhythmGrade.Perfect : RhythmGrade.HalfMiss;
            if (target == null) return false;
            double targetBeat = ParryBeat(target);
            foreach (var attack in incoming)
                if (attack.State == IncomingAttackState.Pending && CoversAttack(lane, inputSlot, attack) && Math.Abs(ParryBeat(attack) - targetBeat) < Epsilon)
                {
                    if (attack.Definition.IsHold)
                    {
                        decimal reduction = grade == RhythmGrade.Perfect ? 1m : .5m;
                        if (heldInputs[inputSlot]) BindHoldDefense(lane, inputSlot, attack, reduction, true);
                        if (Math.Abs(attack.NextImpactBeat - Beat) <= HalfMissWindow + Epsilon)
                            SetReduction(attack, reduction, lane, true);
                        attack.LastParryBeat = Beat;
                    }
                    else { attack.State = IncomingAttackState.Blocked; attack.ResolvedBeat = Beat; TotalBlocked += attack.Definition.Damage; }
                }
            if (WeaponCatalog.Find(lane.Weapon.DefinitionId).Kind == WeaponKind.Shield && !IsSplitShield(lane))
            {
                // Keep the active hold/counters playable. Their completion must not
                // reapply the cooldown that this real parry has already recovered.
                lane.ParryRecoveredCooldown = true; lane.ReadyAtBeat = Beat;
            }
            return true;
        }
        private double Deadline(PhraseLane lane)
        {
            if (lane.Phase == PhraseLanePhase.Ready) return double.PositiveInfinity;
            if (lane.Phase == PhraseLanePhase.Cooldown) return lane.ReadyAtBeat;
            if (lane.Charging || lane.Loaded) return double.PositiveInfinity;
            if (lane.WaitingForParryRelease) return lane.HoldEndBeat + HalfMissWindow + Epsilon;
            return lane.Holding ? Math.Max(Beat, lane.HoldEndBeat) :
                lane.NextBeat + HalfMissWindow + Epsilon;
        }
        private void EnsureIncoming(double until)
        {
            if (Formation != null)
            {
                until = Math.Min(until, Beat + Formation.Rules.PreviewBeats);
                var definitions = new List<BeatAttack>(Formation.Events(formationGeneratedThrough, until));
                definitions.Sort((a, b) => a.Beat != b.Beat ? a.Beat.CompareTo(b.Beat) : string.CompareOrdinal(a.MonsterId, b.MonsterId));
                foreach (var definition in definitions)
                {
                    var attack = PlaceIncoming(definition, definition.Beat);
                    if (attack == null) continue;
                    if (definition.Beat < (Formation.Find(definition.MonsterId)?.GroggyUntilBeat ?? 0))
                    { attack.State = IncomingAttackState.Interrupted; attack.ResolvedBeat = Beat; }
                    incoming.Add(attack);
                }
                formationGeneratedThrough = Math.Max(formationGeneratedThrough, until);
                incoming.Sort((a, b) => a.Beat.CompareTo(b.Beat));
                return;
            }
            if (schedule.Count == 0) return;
            while (schedule[nextAttack].Beat + cycle * loopBeats <= until)
            {
                double at = schedule[nextAttack].Beat + cycle * loopBeats;
                var attack = PlaceIncoming(schedule[nextAttack], at);
                if (attack != null)
                {
                    if (at < GroggyUntilBeat) { attack.State = IncomingAttackState.Interrupted; attack.ResolvedBeat = Beat; }
                    incoming.Add(attack);
                }
                if (++nextAttack == schedule.Count) { nextAttack = 0; cycle++; }
            }
        }
        private IncomingBeatAttack PlaceIncoming(BeatAttack definition, double at)
        {
            var attack = AttackWindow == null ? new IncomingBeatAttack(definition, at) : AttackWindow.Place(definition, at);
            if (attack != null)
            {
                string id = definition.MonsterId ?? "";
                attackSequences.TryGetValue(id, out int sequence);
                attack.SequenceIndex = sequence; attackSequences[id] = sequence + 1;
                if (definition.IsHold)
                    for (int i = incoming.Count - 1; i >= 0; i--)
                        if (incoming[i].Definition.MonsterId == definition.MonsterId && incoming[i].Beat < attack.Beat)
                        {
                            var previous = incoming[i];
                            if (previous.Definition.IsHold && previous.State != IncomingAttackState.Interrupted &&
                                Math.Abs(previous.EndBeat - attack.Beat) < Epsilon)
                                attack.SustainStartBeat = previous.SustainStartBeat;
                            break;
                        }
                RouteShieldAttack(attack);
            }
            return attack;
        }
        private void Miss(PhraseLane lane)
        {
            CancelPerformance(lane);
            if (lane.Phrase.Notes[lane.NextNote].IsOptional) ClearOtherOpportunities(lane);
            holdDefenses.RemoveAll(x => ReferenceEquals(x.Lane, lane));
            lane.GuardUntilBeat = double.NegativeInfinity;
            lane.LastGrade = RhythmGrade.Miss; lane.LastJudgedBeat = Beat; lane.Feedback = "MISS";
            lane.Holding = false; lane.CompletedPhrases = 0; lane.FailedCycle = true;
            lane.states[lane.NextNote] = PhraseNoteState.Missed; Combo = 0; MissCount++;
            ResolveDependencies(lane);
            if (!SelectNext(lane)) Cooldown(lane, Beat + lane.Phrase.MissCooldownBeats);
        }
        private void Succeed(PhraseLane lane, RhythmGrade grade, decimal responseScale = 1m)
        {
            var note = lane.Phrase.Notes[lane.NextNote];
            bool wasHolding = lane.Holding;
            if (note.IsChargedRelease) lane.PlaceReleaseFollowups(Beat);
            if (!wasHolding) RecordPerformance(lane, note);
            if (lane.LastPerformedNote != null && ReferenceEquals(lane.LastPerformedNote.Definition, note) &&
                (note.IsChargedRelease || Math.Abs(lane.LastPerformedNote.Beat - lane.NextBeat) < Epsilon))
                lane.LastPerformedNote.CompletedAtBeat = Beat;
            int heldSlot = lane.HoldingSlot;
            double heldUntil = wasHolding ? lane.HoldEndBeat : double.NegativeInfinity;
            var resolvedSide = lane.ActiveSide;
            if (note.IsOptional)
            {
                ClearOtherOpportunities(lane);
                // A later chosen shot postpones any authored next Chaos section.
                lane.TimingShiftBeats += Math.Ceiling(lane.opportunitySteps[lane.NextNote] * note.OpportunityIntervalBeats - Epsilon);
            }
            if (lane.SustainingGuard) lane.TimingShiftBeats += Math.Max(0, Beat - lane.NextBeat - note.HoldBeats);
            lane.GuardUntilBeat = double.NegativeInfinity;
            lane.states[lane.NextNote] = PhraseNoteState.Hit;
            lane.Holding = false; lane.LastGrade = grade; lane.LastJudgedBeat = Beat; lane.Activations++;
            lane.Feedback = note.IsCall ? "CALL OK" : note.IsParry ?
                (note.IsHold && !lane.Phrase.ParriesOnKeyUp ? "HOLD OK" :
                    lane.parried[lane.NextNote] ? "PARRY" : "OK") :
                grade == RhythmGrade.Perfect ? "PERFECT" : "HALF";
            if (grade == RhythmGrade.Perfect) PerfectCount++; else HalfMissCount++;
            Combo++;
            decimal effectScale = (1m + .25m * lane.Weapon.Level) * (grade == RhythmGrade.Perfect ? 1m : .5m) *
                lane.EffectMultiplier;
            decimal damage = note.Effect == PhraseEffect.Strike || note.IsParry ? note.Damage : 0;
            if (note.Effect == PhraseEffect.Heal || note.BonusHealing > 0)
            {
                decimal healed = Math.Min(PlayerMaximum - PlayerHealth, ((note.Effect == PhraseEffect.Heal ? note.Damage : 0) + note.BonusHealing) * effectScale);
                PlayerHealth += healed; TotalHealed += healed;
                if (healed > 0) PlayerHealthChanged?.Invoke(PlayerHealth);
                lane.Feedback = "HEAL";
            }
            if (note.Effect == PhraseEffect.StartAdjacent)
            {
                ScheduleAdjacent(lane, lane.StartBeat + lane.TimingShiftBeats + note.Beat + note.HoldBeats + 1);
                lane.Feedback = "CHIME +1";
            }
            if (note.Effect == PhraseEffect.ReduceAdjacentCooldown || note.Effect == PhraseEffect.EmpowerAdjacent)
            {
                ApplyAdjacentSupport(lane, note, effectScale);
                lane.Feedback = note.Effect == PhraseEffect.ReduceAdjacentCooldown ? "RECOVER" : "EMPOWER";
            }
            ResolveDependencies(lane);
            if (!SelectNext(lane))
            {
                if (!lane.FailedCycle) lane.CompletedPhrases++;
                if (!note.IsCall && !lane.FailedCycle && lane.Phrase.FinisherEvery > 0 && lane.CompletedPhrases % lane.Phrase.FinisherEvery == 0)
                {
                    damage += lane.Phrase.FinisherDamage;
                    var victim = Formation?.TargetFor(note.Target);
                    double until = Beat + lane.Phrase.GroggyBeats;
                    if (Formation == null) GroggyUntilBeat = Math.Max(GroggyUntilBeat, until);
                    else if (victim != null) victim.GroggyUntilBeat = Math.Max(victim.GroggyUntilBeat, until);
                    foreach (var attack in incoming)
                        if (attack.State == IncomingAttackState.Pending && attack.EndBeat >= Beat - HalfMissWindow && attack.Beat < until &&
                            (Formation == null || attack.Definition.MonsterId == victim?.InstanceId))
                        { attack.State = IncomingAttackState.Interrupted; attack.ResolvedBeat = Beat; }
                    lane.Feedback = "GROGGY!";
                }
                if (lane.FailedCycle) Cooldown(lane, Beat + lane.Phrase.MissCooldownBeats);
                else if (lane.CanRepeat)
                { lane.SelectCycle(lane.NextCycle(lane.Cycle)); BeginCycle(lane); }
                else
                {
                    double end = note.IsChargedRelease || note.IsOptional ? Beat :
                        Math.Max(Beat, lane.StartBeat + lane.TimingShiftBeats + lane.Phrase.LengthBeats);
                    Cooldown(lane, end + lane.Phrase.CompletionCooldownBeats, end);
                }
            }
            damage *= effectScale * Bonuses.DamageMultiplier * responseScale;
            if (damage > 0)
            {
                damage *= ConsumeResonance(resolvedSide);
                if (Beat < lane.DamageBoostUntilBeat) damage *= lane.PendingDamageMultiplier;
                lane.PendingDamageMultiplier = 1m; lane.DamageBoostUntilBeat = double.NegativeInfinity;
            }
            if (damage > 0)
            {
                lane.LastDamageBeat = Beat;
                if (Formation == null) EnemyHealth.Damage(damage);
                else lane.LastDamageMonsterId = Formation.Damage(damage, note.Target, Beat);
            }
            lane.DamageDealt += damage; TotalDamage += damage;
            if (lane.Loaded) lane.Feedback = "LOADED";
            if (!Finished && wasHolding && !note.IsOptional && lane.Phase == PhraseLanePhase.Playing &&
                heldInputs[heldSlot] && lane.SlotForNote(lane.NextNote) == heldSlot &&
                Math.Abs(lane.NextBeat - heldUntil) < Epsilon &&
                (lane.Phrase.Notes[lane.NextNote].ConnectFromPrevious || lane.Phrase.Notes[lane.NextNote].IsHold))
            {
                // Touching segments share contact and entry grade, even at a repeat boundary.
                // Releasing within the next segment still misses; pauses still require this key.
                lane.Holding = true; lane.HoldingSlot = heldSlot; lane.HoldGrade = grade;
                lane.HoldStartedAtBeat = Beat;
                lane.states[lane.NextNote] = PhraseNoteState.Holding;
                RecordPerformance(lane, lane.Phrase.Notes[lane.NextNote]);
                if (!lane.Phrase.Notes[lane.NextNote].IsHold) Succeed(lane, grade);
            }
        }
        private void RecordPerformance(PhraseLane lane, WeaponPhraseNote note)
        {
            if (note.IsCall || note.IsParry || note.IsHold && note.Damage == 0 && lane.Phrase.HoldDamageReduction > 0)
            { CancelPerformance(lane); return; }
            double next = note.IsOptional ? double.PositiveInfinity : lane.NextNote + 1 < lane.Phrase.Notes.Count ? lane.NoteBeat(lane.NextNote + 1) :
                lane.CanRepeat ? lane.NextCycle(lane.Cycle).StartBeat : double.PositiveInfinity;
            lane.LastPerformedNote = new PerformedWeaponNote(note, note.IsChargedRelease ? Beat : lane.NextBeat, Beat, lane.InvokedAtBeat,
                lane.performedNotes++, lane.LastPerformedNote, next);
        }
        private void CancelPerformance(PhraseLane lane)
        {
            if (lane.LastPerformedNote != null && !lane.LastPerformedNote.Canceled)
                lane.LastPerformedNote.CanceledAtBeat = Beat;
        }
        private void ApplyAdjacentSupport(PhraseLane source, WeaponPhraseNote note, decimal effectScale)
        {
            var affected = new HashSet<PhraseLane>();
            foreach (int position in new[] { source.Placement.Offset - 1, source.Placement.Offset + source.InputSlots.Count })
            {
                if (position < -1 || position > BattleInputLayout.MainLaneCount) continue;
                var target = LaneAt(BattleInputLayout.SlotAtPosition(position));
                if (target == null || ReferenceEquals(target, source) || !affected.Add(target)) continue;
                if (note.Effect == PhraseEffect.ReduceAdjacentCooldown)
                {
                    // Never restart or retime an active pattern or invent player contact.
                    if (IsSplitShield(target))
                    {
                        foreach (var contact in shieldContacts)
                        {
                            if (!ReferenceEquals(contact.Lane, target) || contact.Phase != PhraseLanePhase.Cooldown) continue;
                            contact.ReadyAtBeat = Math.Max(Beat, contact.ReadyAtBeat - (double)(note.Damage * effectScale));
                            if (contact.ReadyAtBeat <= Beat) { contact.Phase = PhraseLanePhase.Ready; contact.Feedback = "READY"; }
                        }
                        continue;
                    }
                    if (target.Phase != PhraseLanePhase.Cooldown) continue;
                    target.ReadyAtBeat = Math.Max(Beat, target.ReadyAtBeat - (double)(note.Damage * effectScale));
                    if (target.ReadyAtBeat <= Beat) { target.Phase = PhraseLanePhase.Ready; target.Feedback = "READY"; }
                }
                else
                {
                    // One charge per weapon, even for multi-line neighbors. Reapplication
                    // refreshes duration and keeps the stronger value; it never multiplies stacks.
                    decimal old = Beat < target.DamageBoostUntilBeat ? target.PendingDamageMultiplier : 1m;
                    target.PendingDamageMultiplier = Math.Max(old, 1m + note.Damage * effectScale);
                    target.DamageBoostUntilBeat = Math.Max(target.DamageBoostUntilBeat, Beat + note.EffectDurationBeats);
                }
            }
        }
        private void ScheduleAdjacent(PhraseLane source, double at)
        {
            foreach (int position in new[] { source.Placement.Offset - 1, source.Placement.Offset + source.InputSlots.Count })
            {
                if (position < -1 || position > BattleInputLayout.MainLaneCount) continue;
                int slot = BattleInputLayout.SlotAtPosition(position);
                var target = LaneAt(slot);
                if (target == null || ReferenceEquals(target, source)) continue;
                // Two bells may contact opposite ends of one item. One item still
                // starts once; the first reservation owns its starting Offset.
                var reservation = new ScheduledPhraseStart(target, slot, at);
                bool duplicate = scheduledStarts.Exists(x => ReferenceEquals(x.Target, target) && Math.Abs(x.Beat - reservation.Beat) < Epsilon);
                if (!duplicate) scheduledStarts.Add(reservation);
            }
        }
        private static void BeginActivation(PhraseLane lane, int slot, double at, ScheduledPhraseStart origin = null)
        {
            int offset = lane.Placement.OffsetOf(slot);
            // Manual one-shot activations still contribute to authored finishers.
            // Changing variants starts a new streak; a miss already clears it.
            if (offset != lane.StartOffset) lane.CompletedPhrases = 0;
            lane.SelectStart(offset, at, origin != null); lane.Holding = false; lane.HoldingSlot = -1; lane.ScheduledOrigin = origin;
            lane.InvokedAtBeat = at;
            BeginCycle(lane);
        }
        private void StartScheduled(ScheduledPhraseStart start)
        {
            if (IsSplitShield(start.Target))
            {
                var contact = ShieldAt(start.Slot);
                if (contact.Phase == PhraseLanePhase.Playing) { start.State = ScheduledStartState.Skipped; return; }
                // A bell can recover this half's cooldown, but the player must make
                // the contact before either rank receives a guard or parry.
                contact.ReadyAtBeat = Beat; contact.Phase = PhraseLanePhase.Ready;
                contact.Feedback = "CHIME READY"; start.State = ScheduledStartState.Started; return;
            }
            if (start.Target.Phase == PhraseLanePhase.Playing)
            { start.State = ScheduledStartState.Skipped; return; }
            start.State = ScheduledStartState.Started;
            BeginActivation(start.Target, start.Slot, start.Beat, start);
            start.Target.Feedback = "CHIME START";
        }
        private static void BeginCycle(PhraseLane lane)
        {
            lane.Phase = PhraseLanePhase.Playing; lane.NextNote = 0; lane.FailedCycle = false;
            lane.ParryRecoveredCooldown = false;
            for (int i = 0; i < lane.states.Length; i++)
            {
                lane.states[i] = lane.Phrase.Notes[i].Condition == PhraseNoteCondition.Always ? PhraseNoteState.Pending : PhraseNoteState.Locked;
                lane.parried[i] = false;
            }
        }
        private static void ResolveDependencies(PhraseLane lane)
        {
            for (int i = 0; i < lane.states.Length; i++)
            {
                if (lane.states[i] != PhraseNoteState.Locked) continue;
                var note = lane.Phrase.Notes[i];
                if (note.Condition == PhraseNoteCondition.AllCalls)
                {
                    bool waiting = false, failed = false;
                    for (int j = 0; j < i; j++)
                    {
                        if (!lane.Phrase.Notes[j].IsCall) continue;
                        waiting |= lane.states[j] == PhraseNoteState.Locked || lane.states[j] == PhraseNoteState.Pending || lane.states[j] == PhraseNoteState.Holding;
                        failed |= lane.states[j] == PhraseNoteState.Missed || lane.states[j] == PhraseNoteState.Skipped;
                    }
                    if (failed) lane.states[i] = PhraseNoteState.Skipped;
                    else if (!waiting) lane.states[i] = PhraseNoteState.Pending;
                    continue;
                }
                var state = lane.states[note.Prerequisite];
                if (state == PhraseNoteState.Locked || state == PhraseNoteState.Pending || state == PhraseNoteState.Holding) continue;
                bool met = state == PhraseNoteState.Hit && (note.Condition != PhraseNoteCondition.Parry || lane.parried[note.Prerequisite]);
                lane.states[i] = met ? PhraseNoteState.Pending : PhraseNoteState.Skipped;
            }
        }
        private static bool SelectNext(PhraseLane lane)
        {
            int next = -1;
            for (int i = 0; i < lane.states.Length; i++)
                if (lane.states[i] == PhraseNoteState.Pending && (next < 0 || lane.NoteBeat(i) < lane.NoteBeat(next))) next = i;
            if (next < 0) return false;
            lane.NextNote = next; return true;
        }
        private static void ClearOtherOpportunities(PhraseLane lane)
        {
            for (int i = 0; i < lane.states.Length; i++)
                if (i != lane.NextNote && lane.Phrase.Notes[i].IsOptional &&
                    (lane.states[i] == PhraseNoteState.Pending || lane.states[i] == PhraseNoteState.Locked))
                    lane.states[i] = PhraseNoteState.Skipped;
        }
        private void AdvanceOpportunities(PhraseLane lane)
        {
            if (!lane.Loaded) return;
            for (int i = 0; i < lane.states.Length; i++)
            {
                var note = lane.Phrase.Notes[i];
                if (!note.IsOptional || lane.states[i] != PhraseNoteState.Pending) continue;
                double missed = Beat - HalfMissWindow - Epsilon - lane.NoteBeat(i);
                if (missed > 0) lane.opportunitySteps[i] += (long)Math.Ceiling(missed / note.OpportunityIntervalBeats);
            }
            SelectNext(lane);
        }
        private void Cooldown(PhraseLane lane, double until, double? cooldownStart = null)
        {
            lane.Holding = false;
            double start = cooldownStart ?? Beat, duration = until - start;
            if (duration > 0 && Bonuses.CooldownReduction > 0)
                until = start + Math.Max(Math.Min(1, duration), duration - Bonuses.CooldownReduction);
            lane.ReadyAtBeat = lane.ParryRecoveredCooldown ? Beat : until;
            lane.Phase = lane.ParryRecoveredCooldown ? PhraseLanePhase.Ready : PhraseLanePhase.Cooldown;
        }
    }
}
