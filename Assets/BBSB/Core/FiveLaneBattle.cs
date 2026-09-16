using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public enum IncomingAttackState { Pending, Blocked, Interrupted, Hit }
    public sealed class BeatAttack
    {
        public string MonsterId { get; }
        public double Beat { get; }
        public decimal Damage { get; }
        public BeatAttack(string monsterId, double beat, decimal damage)
        {
            if (!WeaponPhraseNote.Finite(beat) || beat < 0 || damage < 0) throw new ArgumentOutOfRangeException(nameof(beat));
            MonsterId = monsterId; Beat = beat; Damage = damage;
        }
    }
    public sealed class IncomingBeatAttack
    {
        public BeatAttack Definition { get; }
        public double Beat { get; }
        public IncomingAttackState State { get; internal set; }
        internal IncomingBeatAttack(BeatAttack definition, double beat) { Definition = definition; Beat = beat; }
    }
    public sealed class PhraseLane
    {
        public WeaponState Weapon { get; }
        public WeaponPhrase Phrase { get; }
        public PhraseLanePhase Phase { get; internal set; }
        public double StartBeat { get; internal set; }
        public int NextNote { get; internal set; }
        public bool Holding { get; internal set; }
        public bool WaitingForParryRelease => Holding && Phrase.Notes[NextNote].IsParry && Phrase.ParryInput == ParryInputEdge.KeyUp;
        public bool InputHeld { get; internal set; }
        public int CompletedPhrases { get; internal set; }
        public double ReadyAtBeat { get; internal set; }
        public double LastJudgedBeat { get; internal set; } = double.NegativeInfinity;
        public RhythmGrade LastGrade { get; internal set; }
        public string Feedback { get; internal set; } = "READY";
        public decimal DamageDealt { get; internal set; }
        public int Activations { get; internal set; }
        internal RhythmGrade HoldGrade;
        public double NextBeat => StartBeat + Phrase.Notes[NextNote].Beat;
        internal PhraseLane(WeaponState weapon, WeaponPhrase phrase)
        { Weapon = new WeaponState(weapon.DefinitionId, weapon.Rarity, weapon.Level); Phrase = phrase; }
    }

    /// <summary>Continuous five-lane combat. Input, notes, impacts and cooldowns share one musical clock.</summary>
    public sealed class FiveLaneBattle
    {
        private const double Epsilon = .000001;
        private readonly List<PhraseLane> lanes = new List<PhraseLane>();
        private readonly List<BeatAttack> schedule;
        private readonly List<IncomingBeatAttack> incoming = new List<IncomingBeatAttack>();
        private readonly double loopBeats;
        private int nextAttack, cycle;
        public IReadOnlyList<PhraseLane> Lanes { get; }
        public IReadOnlyList<IncomingBeatAttack> Incoming { get; }
        public double Bpm { get; }
        public double Beat { get; private set; }
        public double PerfectWindow { get; }
        public double HalfMissWindow { get; }
        public double GroggyUntilBeat { get; private set; }
        public bool IsGroggy => Beat < GroggyUntilBeat;
        public bool IsPaused { get; private set; }
        public bool Aborted { get; private set; }
        public decimal PlayerHealth { get; private set; }
        public decimal PlayerMaximum { get; }
        public StageHealth EnemyHealth { get; }
        public bool Victory => EnemyHealth.Defeated && PlayerHealth > 0;
        public bool Finished => Aborted || Victory || PlayerHealth == 0;
        public int PerfectCount { get; private set; }
        public int HalfMissCount { get; private set; }
        public int MissCount { get; private set; }
        public int Combo { get; private set; }
        public decimal TotalDamage { get; private set; }
        public decimal TotalBlocked { get; private set; }
        public double LastHitBeat { get; private set; } = double.NegativeInfinity;
        public event Action<decimal> PlayerHealthChanged;

        public FiveLaneBattle(IReadOnlyList<WeaponState> weapons, double bpm, double loopBeats,
            IEnumerable<BeatAttack> attacks, StageHealth enemyHealth, decimal playerHealth, decimal playerMaximum,
            IReadOnlyList<WeaponPhrase> phrases = null, RhythmRules rules = null)
        {
            if (weapons == null || weapons.Count != RunRules.WeaponSlots) throw new ArgumentException("Exactly five weapons are required.");
            if (!WeaponPhraseNote.Finite(bpm) || bpm <= 0 || !WeaponPhraseNote.Finite(loopBeats) || loopBeats <= 0 ||
                playerMaximum <= 0 || playerHealth <= 0 || playerHealth > playerMaximum) throw new ArgumentOutOfRangeException(nameof(bpm));
            if (phrases != null && phrases.Count != weapons.Count) throw new ArgumentException("One phrase per weapon is required.");
            Bpm = bpm; this.loopBeats = loopBeats; EnemyHealth = enemyHealth ?? throw new ArgumentNullException(nameof(enemyHealth));
            PlayerHealth = playerHealth; PlayerMaximum = playerMaximum; rules ??= new RhythmRules(.09, .18);
            PerfectWindow = rules.PerfectSeconds * bpm / 60;
            // Adjacent half-beat starts remain unambiguous even in fast songs.
            HalfMissWindow = Math.Min(.24, rules.HalfMissSeconds * bpm / 60);
            PerfectWindow = Math.Min(PerfectWindow, HalfMissWindow * .75);
            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] == null) throw new ArgumentException("Null weapon.");
                var phrase = phrases == null ? WeaponPhraseCatalog.Find(weapons[i].DefinitionId) : phrases[i];
                if (phrase == null || phrase.WeaponId != weapons[i].DefinitionId) throw new ArgumentException("Phrase does not match its weapon.");
                lanes.Add(new PhraseLane(weapons[i], phrase));
            }
            schedule = new List<BeatAttack>(attacks ?? throw new ArgumentNullException(nameof(attacks)));
            foreach (var attack in schedule) if (attack == null || attack.Beat >= loopBeats) throw new ArgumentException("Attack outside music loop.");
            schedule.Sort((a, b) => a.Beat != b.Beat ? a.Beat.CompareTo(b.Beat) : string.CompareOrdinal(a.MonsterId, b.MonsterId));
            Lanes = lanes.AsReadOnly(); Incoming = incoming.AsReadOnly(); EnsureIncoming(8);
        }

        public static FiveLaneBattle FromPlan(BattlePlan plan, IReadOnlyList<WeaponState> weapons,
            StageHealth enemyHealth, decimal health, decimal maximum, IReadOnlyList<WeaponPhrase> phrases = null)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var attacks = new List<BeatAttack>();
            double length = (double)plan.Stage.Music.TotalTicks / RhythmTime.TicksPerBeat;
            foreach (var attack in plan.Attacks)
            foreach (var step in attack.Placement.Pattern.Steps)
            {
                // Sustained legacy attacks land at their end; gestures no longer constrain equipment.
                double at = (double)(attack.ResponseStartTick + step.OffsetTick + step.DurationTicks) / RhythmTime.TicksPerBeat;
                if (at < length) attacks.Add(new BeatAttack(attack.MonsterId, at, attack.Monster.DamagePerNote * attack.JudgmentWeight));
            }
            return new FiveLaneBattle(weapons, plan.Stage.Music.Bpm, length, attacks, enemyHealth, health, maximum, phrases);
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
            var lane = lanes[slot];
            if (lane.InputHeld) return;
            lane.InputHeld = true;
            if (lane.Phase == PhraseLanePhase.Cooldown || lane.Holding) return;
            bool starting = lane.Phase == PhraseLanePhase.Ready;
            if (starting)
            {
                lane.StartBeat = Math.Round(Beat * 2, MidpointRounding.AwayFromZero) / 2;
                lane.NextNote = lane.CompletedPhrases = 0; lane.Phase = PhraseLanePhase.Playing;
            }
            double error = Math.Abs(Beat - lane.NextBeat);
            var note = lane.Phrase.Notes[lane.NextNote];
            bool pressParry = note.IsParry && lane.Phrase.ParryInput == ParryInputEdge.KeyDown;
            bool alignOpening = starting && pressParry;
            if (!alignOpening && error > HalfMissWindow + Epsilon) { Miss(lane); return; }
            var grade = error <= PerfectWindow + Epsilon ? RhythmGrade.Perfect : RhythmGrade.HalfMiss;
            if (pressParry)
            {
                if (!TryParry(out double targetBeat, out var parryGrade)) { Miss(lane); lane.Feedback = "NO PARRY"; return; }
                // Only a fresh start chooses its phase; later parries keep the weapon's established rhythm.
                if (alignOpening) { lane.StartBeat = targetBeat; grade = parryGrade; }
                else if (parryGrade == RhythmGrade.HalfMiss) grade = RhythmGrade.HalfMiss;
                lane.LastGrade = grade; lane.LastJudgedBeat = Beat; lane.Feedback = "PARRY";
            }
            if (note.IsHold)
            { lane.Holding = true; lane.HoldGrade = grade; lane.Feedback = pressParry ? "PARRY / HOLD" : "HOLD"; }
            else Succeed(lane, grade);
        }

        public void Release(int slot, double atBeat)
        {
            ValidateInput(slot, atBeat);
            if (IsPaused || Finished) return;
            Advance(atBeat);
            var lane = lanes[slot]; lane.InputHeld = false;
            if (Finished || !lane.Holding) return;
            if (lane.WaitingForParryRelease)
            {
                double error = Math.Abs(Beat - lane.NextBeat - lane.Phrase.Notes[lane.NextNote].HoldBeats);
                if (error > HalfMissWindow + Epsilon || !TryParry(out _, out var parryGrade))
                { Miss(lane); lane.Feedback = "NO PARRY"; return; }
                var grade = error <= PerfectWindow + Epsilon ? RhythmGrade.Perfect : RhythmGrade.HalfMiss;
                if (parryGrade == RhythmGrade.HalfMiss || lane.HoldGrade == RhythmGrade.HalfMiss) grade = RhythmGrade.HalfMiss;
                Succeed(lane, grade);
            }
            else Miss(lane);
        }

        public void Advance(double atBeat)
        {
            if (!WeaponPhraseNote.Finite(atBeat) || atBeat < Beat - Epsilon) throw new ArgumentOutOfRangeException(nameof(atBeat));
            if (IsPaused || Finished) return;
            atBeat = Math.Max(Beat, atBeat);
            EnsureIncoming(atBeat + 8);
            // Resolve deadlines chronologically, including across a long frame or a song loop.
            while (!Finished)
            {
                double next = double.PositiveInfinity;
                foreach (var lane in lanes) next = Math.Min(next, Deadline(lane));
                foreach (var attack in incoming) if (attack.State == IncomingAttackState.Pending)
                    next = Math.Min(next, attack.Beat + HalfMissWindow + Epsilon);
                if (next > atBeat) break;
                Beat = Math.Max(Beat, next);
                foreach (var lane in lanes)
                {
                    if (Deadline(lane) > Beat) continue;
                    if (lane.Phase == PhraseLanePhase.Cooldown) { lane.Phase = PhraseLanePhase.Ready; lane.Feedback = "READY"; }
                    else if (lane.Holding && !lane.WaitingForParryRelease) Succeed(lane, lane.HoldGrade);
                    else Miss(lane);
                    if (Finished) break;
                }
                if (Finished) break;
                foreach (var attack in incoming)
                {
                    if (attack.State != IncomingAttackState.Pending || attack.Beat + HalfMissWindow + Epsilon > Beat) continue;
                    attack.State = IncomingAttackState.Hit; LastHitBeat = Beat;
                    PlayerHealth = Math.Max(0, PlayerHealth - attack.Definition.Damage);
                    PlayerHealthChanged?.Invoke(PlayerHealth);
                    if (Finished) break;
                }
            }
            if (!Finished) Beat = atBeat;
            incoming.RemoveAll(x => x.State != IncomingAttackState.Pending && x.Beat < Beat - 4);
        }

        private void ValidateInput(int slot, double atBeat)
        {
            if (slot < 0 || slot >= lanes.Count) throw new ArgumentOutOfRangeException(nameof(slot));
            if (!WeaponPhraseNote.Finite(atBeat) || atBeat < Beat - Epsilon) throw new ArgumentOutOfRangeException(nameof(atBeat));
        }
        private bool TryParry(out double targetBeat, out RhythmGrade grade)
        {
            IncomingBeatAttack target = null; double nearest = double.PositiveInfinity;
            foreach (var attack in incoming)
                if (attack.State == IncomingAttackState.Pending && Math.Abs(attack.Beat - Beat) <= HalfMissWindow + Epsilon &&
                    Math.Abs(attack.Beat - Beat) < nearest)
                { target = attack; nearest = Math.Abs(attack.Beat - Beat); }
            targetBeat = target == null ? Beat : target.Beat;
            grade = nearest <= PerfectWindow + Epsilon ? RhythmGrade.Perfect : RhythmGrade.HalfMiss;
            if (target == null) return false;
            // The configured key edge blocks simultaneous impacts only, never the whole Hold interval.
            foreach (var attack in incoming)
                if (attack.State == IncomingAttackState.Pending && Math.Abs(attack.Beat - target.Beat) < Epsilon)
                { attack.State = IncomingAttackState.Blocked; TotalBlocked += attack.Definition.Damage; }
            return true;
        }
        private double Deadline(PhraseLane lane)
        {
            if (lane.Phase == PhraseLanePhase.Ready) return double.PositiveInfinity;
            if (lane.Phase == PhraseLanePhase.Cooldown) return lane.ReadyAtBeat;
            if (lane.WaitingForParryRelease) return lane.NextBeat + lane.Phrase.Notes[lane.NextNote].HoldBeats + HalfMissWindow + Epsilon;
            return lane.Holding ? Math.Max(Beat, lane.NextBeat + lane.Phrase.Notes[lane.NextNote].HoldBeats) :
                lane.NextBeat + HalfMissWindow + Epsilon;
        }
        private void EnsureIncoming(double until)
        {
            if (schedule.Count == 0) return;
            while (schedule[nextAttack].Beat + cycle * loopBeats <= until)
            {
                double at = schedule[nextAttack].Beat + cycle * loopBeats;
                var attack = new IncomingBeatAttack(schedule[nextAttack], at);
                if (at < GroggyUntilBeat) attack.State = IncomingAttackState.Interrupted;
                incoming.Add(attack);
                if (++nextAttack == schedule.Count) { nextAttack = 0; cycle++; }
            }
        }
        private void Miss(PhraseLane lane)
        {
            lane.LastGrade = RhythmGrade.Miss; lane.LastJudgedBeat = Beat; lane.Feedback = "MISS";
            lane.Holding = false; lane.CompletedPhrases = 0; lane.Phase = PhraseLanePhase.Cooldown;
            lane.ReadyAtBeat = Beat + lane.Phrase.MissCooldownBeats; Combo = 0; MissCount++;
        }
        private void Succeed(PhraseLane lane, RhythmGrade grade)
        {
            var note = lane.Phrase.Notes[lane.NextNote];
            lane.Holding = false; lane.LastGrade = grade; lane.LastJudgedBeat = Beat; lane.Activations++;
            lane.Feedback = note.IsParry ?
                (note.IsHold && lane.Phrase.ParryInput == ParryInputEdge.KeyDown ? "HOLD OK" : "PARRY") :
                grade == RhythmGrade.Perfect ? "PERFECT" : "HALF";
            if (grade == RhythmGrade.Perfect) PerfectCount++; else HalfMissCount++;
            Combo++;
            decimal damage = note.Damage;
            if (++lane.NextNote == lane.Phrase.Notes.Count)
            {
                lane.CompletedPhrases++;
                if (lane.Phrase.FinisherEvery > 0 && lane.CompletedPhrases % lane.Phrase.FinisherEvery == 0)
                {
                    damage += lane.Phrase.FinisherDamage; GroggyUntilBeat = Math.Max(GroggyUntilBeat, Beat + lane.Phrase.GroggyBeats);
                    foreach (var attack in incoming)
                        if (attack.State == IncomingAttackState.Pending && attack.Beat >= Beat - HalfMissWindow && attack.Beat < GroggyUntilBeat)
                            attack.State = IncomingAttackState.Interrupted;
                    lane.Feedback = "GROGGY!";
                }
                lane.NextNote = 0;
                if (lane.Phrase.Repeat) lane.StartBeat += lane.Phrase.LengthBeats;
                else
                { lane.Phase = PhraseLanePhase.Cooldown; lane.ReadyAtBeat = Math.Max(Beat, lane.StartBeat + lane.Phrase.LengthBeats); }
            }
            damage *= (1m + .25m * lane.Weapon.Level) * (grade == RhythmGrade.Perfect ? 1m : .5m);
            EnemyHealth.Damage(damage); lane.DamageDealt += damage; TotalDamage += damage;
        }
    }
}
