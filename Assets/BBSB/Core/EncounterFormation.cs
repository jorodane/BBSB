using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public enum AttackRank { Front, Rear }
    public enum MonsterFormationRole { Standard, Trickster }
    public enum FormationChangeReason { Rotation, Intrusion, Return, Defeat }

    public sealed class FormationRules
    {
        public double PreviewBeats { get; }
        public double RotationBeats { get; }
        public double SupportInterval { get; }
        public FormationRules(double previewBeats = 3, double rotationBeats = 12, double supportInterval = 8)
        {
            if (!WeaponPhraseNote.Finite(previewBeats) || previewBeats < 3 || !WeaponPhraseNote.Finite(rotationBeats) ||
                rotationBeats < 4 || !WeaponPhraseNote.Finite(supportInterval) || supportInterval < 2)
                throw new ArgumentOutOfRangeException(nameof(previewBeats));
            PreviewBeats = previewBeats; RotationBeats = rotationBeats; SupportInterval = supportInterval;
        }
        public double BoundaryAfter(double beat) => Math.Floor(beat + PreviewBeats) + 1;
    }

    public sealed class EncounterMonster
    {
        public string InstanceId { get; }
        public MonsterDefinition Definition { get; }
        public StageHealth Health { get; }
        public bool IsTrickster => Definition.FormationRole == MonsterFormationRole.Trickster;
        public double FrontCursor { get; internal set; }
        public double LastHitBeat { get; internal set; } = double.NegativeInfinity;
        public double GroggyUntilBeat { get; internal set; }
        public double DefeatedBeat { get; internal set; } = double.PositiveInfinity;
        internal readonly List<BeatAttack> Pattern = new List<BeatAttack>();
        private readonly List<double> phraseEnds = new List<double>();
        internal double PatternLength, FirstPhraseLength;
        internal EncounterMonster(MonsterPlan plan, decimal maximum)
        {
            InstanceId = plan.InstanceId; Definition = plan.Monster; Health = new StageHealth(maximum);
            foreach (var phrase in Definition.Patterns)
            {
                double length = Math.Max(4, (double)phrase.ResponseTicks / RhythmTime.TicksPerBeat);
                foreach (var step in phrase.Pattern.Steps)
                {
                    double hold = step.Kind == GestureKind.Hold ? (double)step.DurationTicks / RhythmTime.TicksPerBeat : 0;
                    double beat = (double)(step.OffsetTick + (hold > 0 ? 0 : step.DurationTicks)) / RhythmTime.TicksPerBeat;
                    Pattern.Add(new BeatAttack(InstanceId, PatternLength + beat, Definition.DamagePerNote * phrase.JudgmentWeight, hold));
                    length = Math.Max(length, beat + hold + .5);
                }
                if (FirstPhraseLength == 0) FirstPhraseLength = Math.Max(4, length);
                PatternLength += length;
                phraseEnds.Add(PatternLength);
            }
            Pattern.Sort((a, b) => a.Beat.CompareTo(b.Beat));
        }
        internal double RemainingPhraseBeats(double cursor)
        {
            double local = cursor % PatternLength;
            foreach (double end in phraseEnds) if (end > local + .000001) return end - local;
            return FirstPhraseLength;
        }
    }

    // Owns mutable encounter state. BattlePlan remains the reusable authored source.
    // Only attacks entering the preview horizon are committed to the battle clock.
    public sealed class EncounterFormation
    {
        private const double Epsilon = .000001;
        private readonly List<EncounterMonster> monsters = new List<EncounterMonster>();
        private readonly StageHealth totalHealth;
        private double segmentStart, sourceStart, decisionAt;
        private readonly double openingBeat;
        private EncounterMonster returningTo;
        private bool returnPending;
        private int rotation;
        public IReadOnlyList<EncounterMonster> Monsters { get; }
        public FormationRules Rules { get; }
        public EncounterMonster Front { get; private set; }
        public EncounterMonster PendingFront { get; private set; }
        public double SwitchAtBeat { get; private set; } = double.PositiveInfinity;
        public FormationChangeReason ChangeReason { get; private set; }
        public double NextDeadline => Math.Min(SwitchAtBeat, decisionAt);
        public event Action<string, double> FutureCut;

        public EncounterFormation(BattlePlan plan, StageHealth totalHealth, FormationRules rules = null, double startBeat = 0)
        {
            if (plan == null || plan.Monsters.Count == 0 || totalHealth == null || totalHealth.Defeated)
                throw new ArgumentException("A formation needs living monsters and health.");
            if (!WeaponPhraseNote.Finite(startBeat) || startBeat < 0) throw new ArgumentOutOfRangeException(nameof(startBeat));
            this.totalHealth = totalHealth; Rules = rules ?? new FormationRules();
            segmentStart = openingBeat = startBeat;
            decimal used = 0;
            for (int i = 0; i < plan.Monsters.Count; i++)
            {
                decimal maximum = i == plan.Monsters.Count - 1 ? totalHealth.Maximum - used : totalHealth.Maximum / plan.Monsters.Count;
                used += maximum;
                var monster = new EncounterMonster(plan.Monsters[i], maximum); monsters.Add(monster);
                // Reopening preparation keeps damage already dealt to this encounter.
                decimal priorDamage = totalHealth.Maximum - totalHealth.Current;
                decimal previousShares = used - maximum;
                if (priorDamage > previousShares) monster.Health.Damage(Math.Min(maximum, priorDamage - previousShares));
            }
            Monsters = monsters.AsReadOnly(); Front = FirstLiving(); decisionAt = startBeat + Rules.RotationBeats;
        }
        public EncounterMonster Find(string id) => monsters.Find(x => x.InstanceId == id);
        private EncounterMonster FirstLiving() => monsters.Find(x => !x.Health.Defeated);
        public bool RequestSwitch(string id, double beat, FormationChangeReason reason = FormationChangeReason.Rotation)
        {
            var target = Find(id);
            if (!WeaponPhraseNote.Finite(beat) || beat < 0 || target == null || target.Health.Defeated ||
                ReferenceEquals(target, Front) || PendingFront != null) return false;
            PendingFront = target; SwitchAtBeat = Rules.BoundaryAfter(beat); ChangeReason = reason;
            decisionAt = double.PositiveInfinity;
            FutureCut?.Invoke(Front.InstanceId, SwitchAtBeat);
            return true;
        }
        public void Advance(double beat)
        {
            if (PendingFront != null && SwitchAtBeat <= beat + Epsilon)
            {
                var previous = Front;
                previous.FrontCursor = sourceStart + SwitchAtBeat - segmentStart;
                Front = PendingFront; segmentStart = SwitchAtBeat; sourceStart = Front.FrontCursor;
                PendingFront = null; SwitchAtBeat = double.PositiveInfinity;
                if (ChangeReason == FormationChangeReason.Intrusion && !previous.Health.Defeated)
                {
                    returningTo = previous; returnPending = true;
                    double end = Math.Ceiling(beat + Front.RemainingPhraseBeats(sourceStart));
                    decisionAt = Math.Max(beat, end - Rules.PreviewBeats - 1);
                }
                else { returningTo = null; returnPending = false; decisionAt = beat + Rules.RotationBeats; }
                if (Front.Health.Defeated) decisionAt = beat;
            }
            if (PendingFront != null || decisionAt > beat + Epsilon) return;
            EncounterMonster next = null; var reason = FormationChangeReason.Rotation;
            if (returnPending && returningTo != null && !returningTo.Health.Defeated)
            { next = returningTo; reason = FormationChangeReason.Return; }
            else
            {
                int index = monsters.IndexOf(Front);
                for (int offset = 1; offset <= monsters.Count; offset++)
                {
                    var candidate = monsters[(index + offset) % monsters.Count];
                    if (candidate.Health.Defeated || ReferenceEquals(candidate, Front)) continue;
                    next = candidate;
                    if (candidate.IsTrickster && rotation % 2 == 0) break;
                    if (offset == 1 && !candidate.IsTrickster) break;
                }
                if (Front.Health.Defeated) reason = FormationChangeReason.Defeat;
                else if (next != null && next.IsTrickster) reason = FormationChangeReason.Intrusion;
            }
            rotation++;
            if (next == null || !RequestSwitch(next.InstanceId, beat, reason)) decisionAt = beat + Rules.RotationBeats;
        }
        public EncounterMonster TargetFor(WeaponAttackTarget target)
        {
            EncounterMonster victim = null;
            if (target == WeaponAttackTarget.Rear)
                foreach (var monster in monsters)
                    if (!monster.Health.Defeated && !ReferenceEquals(monster, Front) &&
                        (victim == null || monster.Health.Current < victim.Health.Current)) victim = monster;
            victim ??= Front != null && !Front.Health.Defeated ? Front : FirstLiving();
            return victim;
        }
        public string Damage(decimal amount, WeaponAttackTarget target, double beat)
        {
            if (amount <= 0) return null;
            var victim = TargetFor(target);
            if (victim == null) return null;
            decimal dealt = Math.Min(amount, victim.Health.Current);
            victim.Health.Damage(dealt); totalHealth.Damage(dealt); victim.LastHitBeat = beat;
            if (victim.Health.Defeated)
            {
                victim.DefeatedBeat = beat; FutureCut?.Invoke(victim.InstanceId, Rules.BoundaryAfter(beat));
                if (ReferenceEquals(victim, Front) && PendingFront == null) { decisionAt = beat; Advance(beat); }
            }
            return victim.InstanceId;
        }
        public IEnumerable<BeatAttack> Events(double after, double through)
        {
            if (through <= after) yield break;
            foreach (var attack in FrontEvents(Front, segmentStart, sourceStart, after, Math.Min(through, SwitchAtBeat - Epsilon))) yield return attack;
            if (PendingFront != null && through >= SwitchAtBeat)
                foreach (var attack in FrontEvents(PendingFront, SwitchAtBeat, PendingFront.FrontCursor, Math.Max(after, SwitchAtBeat - Epsilon), through)) yield return attack;
            for (int i = 0; i < monsters.Count; i++)
            {
                var monster = monsters[i]; if (monster.Health.Defeated) continue;
                double offset = openingBeat + 4 + i * 2;
                int first = Math.Max(0, (int)Math.Floor((after - offset) / Rules.SupportInterval) + 1);
                for (int turn = first; ; turn++)
                {
                    double at = offset + turn * Rules.SupportInterval;
                    if (at > through + Epsilon) break;
                    var lead = at < SwitchAtBeat ? Front : PendingFront;
                    if (ReferenceEquals(monster, lead)) continue;
                    // Rear actions remain short and readable, with distinct start times.
                    yield return new BeatAttack(monster.InstanceId, at, Math.Max(1, monster.Definition.DamagePerNote * .75m), rank: AttackRank.Rear);
                }
            }
        }
        private IEnumerable<BeatAttack> FrontEvents(EncounterMonster monster, double start, double cursor, double after, double through)
        {
            if (monster == null || monster.Health.Defeated || through < start || through <= after) yield break;
            double localFrom = cursor + Math.Max(0, after - start);
            int firstCycle = Math.Max(0, (int)Math.Floor(localFrom / monster.PatternLength));
            int lastCycle = Math.Max(firstCycle, (int)Math.Floor((cursor + through - start) / monster.PatternLength));
            for (int cycle = firstCycle; cycle <= lastCycle; cycle++) foreach (var attack in monster.Pattern)
            {
                double local = attack.Beat + cycle * monster.PatternLength;
                double at = start + local - cursor, hold = attack.HoldBeats;
                decimal damage = attack.Damage;
                if (at < start && hold > 0 && at + hold > start)
                { double remaining = at + hold - start; damage *= (decimal)(remaining / hold); hold = remaining; at = start; }
                if (at <= after || at > through + Epsilon || at < start) continue;
                if (hold > 0 && at + hold > SwitchAtBeat && ReferenceEquals(monster, Front))
                { double remaining = SwitchAtBeat - at; damage *= (decimal)(remaining / hold); hold = remaining; }
                yield return new BeatAttack(monster.InstanceId, at, damage, hold);
            }
        }
    }
}
