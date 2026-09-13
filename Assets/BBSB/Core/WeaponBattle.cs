using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public sealed class StageHealth
    {
        public decimal Maximum { get; }
        public decimal Current { get; private set; }
        public bool Defeated => Current == 0;
        public StageHealth(decimal maximum)
        {
            if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
            Maximum = Current = maximum;
        }
        internal void Damage(decimal amount) { Current = Math.Max(0, Current - Math.Max(0, amount)); }
    }

    /// <summary>A weapon action subscribes to a monster's existing note; it owns no input state.</summary>
    public sealed class WeaponBinding
    {
        public int Slot { get; }
        public int StepIndex => Note.StepIndex;
        public WeaponActionDefinition Action { get; }
        public ResponseNote Note { get; }
        internal WeaponBinding(int slot, WeaponActionDefinition action, ResponseNote note)
        { Slot = slot; Action = action; Note = note; }
    }

    public sealed class WeaponJudgment
    {
        public WeaponBinding Binding { get; }
        public RhythmResult Source { get; }
        public RhythmGrade Grade => Source.Grade;
        internal WeaponJudgment(WeaponBinding binding, RhythmResult source)
        { Binding = binding; Source = source; }
    }

    public sealed class WeaponActivation
    {
        public WeaponDefinition Weapon { get; }
        public int Slot { get; }
        public PlannedAttack Target { get; }
        public int StepIndex { get; }
        public double AtSeconds { get; }
        public decimal Damage { get; }
        public decimal Guard { get; }
        public WeaponActionDefinition Action { get; }
        public RhythmGrade Grade { get; }
        internal WeaponActivation(WeaponDefinition weapon, WeaponJudgment result, decimal damage, decimal guard)
        {
            Weapon = weapon; Slot = result.Binding.Slot; Target = result.Source.Note.Attack; StepIndex = result.Binding.StepIndex;
            AtSeconds = result.Source.JudgedAtSeconds; Damage = damage; Guard = guard; Action = result.Binding.Action; Grade = result.Grade;
        }
    }

    /// <summary>Combat is driven only by judgments. Song time also owns buffs, Overkill and practice.</summary>
    public sealed class WeaponBattle
    {
        private sealed class GuardCharge { public double Start, End; public decimal Amount; }
        private readonly List<GuardCharge> guards = new List<GuardCharge>();
        private readonly List<WeaponBinding> bindings = new List<WeaponBinding>();
        private readonly Dictionary<ResponseNote, List<WeaponBinding>> subscribers = new Dictionary<ResponseNote, List<WeaponBinding>>();
        private readonly List<WeaponJudgment> judgments = new List<WeaponJudgment>();
        private readonly List<WeaponActivation> activations = new List<WeaponActivation>();
        private readonly Dictionary<int, RhythmGrade> edgeGrades = new Dictionary<int, RhythmGrade>();
        private readonly HashSet<string> overkillAttacks = new HashSet<string>();
        private readonly int[] daggerChains = new int[RunRules.WeaponSlots];
        private double resonanceEnd = double.NegativeInfinity;
        private decimal resonance;
        private BattlePlan plan;
        private double beat;
        private int finaleTick = -1;
        private RhythmGrade? finaleGrade;
        public WeaponArrangement Loadout { get; private set; }
        public StageHealth EnemyHealth { get; }
        public bool IsPractice { get; }
        public decimal PlayerHealth { get; private set; }
        public decimal PlayerMaximum { get; }
        public decimal TotalDamage { get; private set; }
        public decimal TotalBlocked { get; private set; }
        public IReadOnlyList<WeaponActivation> Activations { get; }
        public IReadOnlyList<WeaponBinding> Bindings { get; }
        public IReadOnlyList<WeaponJudgment> Judgments { get; }
        public bool Victory { get; private set; }
        public double DefeatedAtSeconds { get; private set; } = double.PositiveInfinity;
        public double OverkillEndSeconds { get; private set; } = double.PositiveInfinity;
        public double FinaleAtSeconds { get; private set; } = double.PositiveInfinity;
        public bool FinaleSuccess => finaleGrade.HasValue && finaleGrade.Value != RhythmGrade.Miss;

        public WeaponBattle(WeaponArrangement loadout, StageHealth enemyHealth, decimal playerHealth, decimal playerMaximum, bool practice = false)
        {
            Loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            EnemyHealth = enemyHealth ?? throw new ArgumentNullException(nameof(enemyHealth));
            if (playerHealth < 0 || playerMaximum <= 0 || playerHealth > playerMaximum) throw new ArgumentOutOfRangeException(nameof(playerHealth));
            PlayerHealth = playerHealth; PlayerMaximum = playerMaximum; IsPractice = practice;
            Activations = activations.AsReadOnly(); Bindings = bindings.AsReadOnly(); Judgments = judgments.AsReadOnly();
        }

        internal void Bind(BattlePlan battlePlan, IReadOnlyList<ResponseNote> notes)
        {
            if (plan != null) throw new InvalidOperationException("Combat belongs to one performance.");
            plan = battlePlan; beat = 60 / plan.Stage.Music.Bpm; Loadout = Loadout.Snapshot(plan);
            foreach (var placement in Loadout.Placements)
            {
                var weapon = WeaponCatalog.Find(Loadout.Equipment[placement.Slot].DefinitionId);
                foreach (var attack in plan.Attacks)
                {
                    if (!placement.Matches(attack)) continue;
                    int sourceIndex = WeaponArrangement.MatchingStep(attack.Placement.Pattern, weapon, placement.OffsetTick, placement.Kind);
                    foreach (var note in notes)
                    {
                        if (note.Attack != attack || note.StepIndex != sourceIndex) continue;
                        var binding = new WeaponBinding(placement.Slot, weapon.ActionFor(note.Step.Kind), note); bindings.Add(binding);
                        if (!subscribers.TryGetValue(note, out var list)) subscribers[note] = list = new List<WeaponBinding>();
                        list.Add(binding); break;
                    }
                }
            }
            foreach (var list in subscribers.Values)
                list.Sort((a, b) => EffectOrder(a) != EffectOrder(b) ? EffectOrder(a).CompareTo(EffectOrder(b)) : a.Slot.CompareTo(b.Slot));
            if (EnemyHealth.Defeated && !IsPractice) ConfirmVictory(0);
        }

        public bool Allows(PlannedAttack attack) => !Victory || overkillAttacks.Contains(attack.Id);
        public bool AllowsEnemyEffect(double seconds) => !Victory || seconds < DefeatedAtSeconds;

        private static int EffectOrder(WeaponBinding binding) => binding.Action.Guard > 0 ? 0 : binding.Action.GrantsResonance ? 1 : 2;

        internal int JudgmentOrder(ResponseNote note) => subscribers.TryGetValue(note, out var list) ? EffectOrder(list[0]) : 3;

        internal void JudgeWeapons(RhythmResult source)
        {
            if (!Allows(source.Note.Attack) || !subscribers.TryGetValue(source.Note, out var list)) return;
            // Every subscriber receives the very same judgment, even if an earlier weapon
            // defeats the shared target. No input is consumed or judged a second time.
            foreach (var binding in list)
            {
                var result = new WeaponJudgment(binding, source); judgments.Add(result); JudgeWeapon(result);
            }
        }

        private void JudgeWeapon(WeaponJudgment result)
        {
            var source = result.Source; var binding = result.Binding;
            var state = Loadout.Equipment[binding.Slot]; var weapon = WeaponCatalog.Find(state.DefinitionId);
            var action = binding.Action;
            decimal efficiency = (decimal)source.Efficiency;
            if (action.PerfectOnly && result.Grade != RhythmGrade.Perfect) efficiency = 0;
            if (efficiency == 0) { daggerChains[binding.Slot] = 0; return; }
            decimal damage = action.Damage * efficiency;
            if (action.BuildsCombo)
            { damage += 2 * Math.Min(3, daggerChains[binding.Slot]) * efficiency; daggerChains[binding.Slot]++; }
            if (result.Grade == RhythmGrade.Perfect) damage += action.PerfectBonus;
            damage *= weapon.LevelMultiplier(state.Level);
            decimal guard = action.Guard * efficiency * weapon.LevelMultiplier(state.Level);
            double at = source.JudgedAtSeconds;
            if (damage > 0 && at <= resonanceEnd)
            { damage *= 1 + resonance; resonance = 0; resonanceEnd = double.NegativeInfinity; }
            if (action.GrantsResonance) { resonance = .5m * efficiency; resonanceEnd = at + 2 * beat; }
            if (guard > 0) guards.Add(new GuardCharge { Start = at, End = at + 4 * beat, Amount = guard });
            TotalDamage += damage; EnemyHealth.Damage(damage);
            activations.Add(new WeaponActivation(weapon, result, damage, guard));
            if (!Victory && EnemyHealth.Defeated && !IsPractice) ConfirmVictory(at);
        }

        internal void JudgeIncoming(RhythmResult result)
        {
            decimal remaining = result.RawDamageTaken;
            if (Victory && result.JudgedAtSeconds >= DefeatedAtSeconds) remaining = 0;
            else foreach (var guard in guards)
            {
                if (result.JudgedAtSeconds < guard.Start || result.JudgedAtSeconds > guard.End) continue;
                decimal absorbed = Math.Min(remaining, guard.Amount);
                remaining -= absorbed; guard.Amount -= absorbed;
                TotalBlocked += absorbed;
            }
            result.BlockedDamage = result.RawDamageTaken - remaining;
            PlayerHealth = Math.Max(0, PlayerHealth - remaining);
            ObserveFinale(result);
        }

        public decimal GuardAt(double seconds)
        {
            decimal total = 0;
            foreach (var guard in guards) if (seconds >= guard.Start && seconds <= guard.End) total += guard.Amount;
            return total;
        }

        private void ConfirmVictory(double at)
        {
            Victory = true; DefeatedAtSeconds = at;
            double phraseEnd = at;
            foreach (var attack in plan.Attacks)
            {
                if (RhythmTime.Seconds(attack.CallStartTick, plan.Stage.Music.Bpm) > at + 1e-9 ||
                    RhythmTime.Seconds(attack.PhraseEndTick, plan.Stage.Music.Bpm) < at - beat * .24) continue;
                overkillAttacks.Add(attack.Id);
                phraseEnd = Math.Max(phraseEnd, RhythmTime.Seconds(attack.PhraseEndTick, plan.Stage.Music.Bpm));
                foreach (var step in attack.Placement.Pattern.Steps)
                    finaleTick = Math.Max(finaleTick, attack.ResponseStartTick + step.OffsetTick + step.DurationTicks);
            }
            if (edgeGrades.TryGetValue(finaleTick, out var previousGrade)) finaleGrade = previousGrade;
            FinaleAtSeconds = Math.Max(at, RhythmTime.Seconds(Math.Max(0, finaleTick), plan.Stage.Music.Bpm) + beat * .24);
            OverkillEndSeconds = Math.Max(phraseEnd, FinaleAtSeconds) + .8;
        }

        private void ObserveFinale(RhythmResult result)
        {
            // A failed Dive start is an earlier miss, not a failed final release. A real final
            // input may also be judged slightly early, before another weapon confirms victory.
            if (result.JudgedAtSeconds < result.Note.EndSeconds - beat * .24 - 1e-9) return;
            int tick = result.Note.EndTick;
            edgeGrades[tick] = edgeGrades.TryGetValue(tick, out var grade) ?
                (RhythmGrade)Math.Min((int)grade, (int)result.Grade) : result.Grade;
            if (Victory && Allows(result.Note.Attack) && tick == finaleTick) finaleGrade = edgeGrades[tick];
        }
    }

    public static class WeaponPractice
    {
        // Keep the authored Call (including silent wait), but rehearse only the selected pattern.
        public static RhythmRound Create(BattlePlan source, WeaponArrangement loadout, PlannedAttack selected, decimal enemyMaximum, decimal playerMaximum)
        {
            if (source == null || loadout == null || selected == null) throw new ArgumentNullException(nameof(source));
            int start = selected.Placement.Pattern.CueLeadTicks + 4;
            int ticks = start + selected.Pattern.ResponseTicks + 8;
            int bars = (ticks + source.Stage.Music.TicksPerBar - 1) / source.Stage.Music.TicksPerBar;
            var music = new MusicDefinition("practice", selected.Pattern.Name + " · 연습", source.Stage.Music.Bpm,
                source.Stage.Music.BeatsPerBar, new[] { new MusicSection("PRACTICE", 0, bars, 1) },
                new[] { new[] { new SlotTemplate(GestureKind.Tap, 0) } });
            var attack = new PlannedAttack(selected.MonsterId, selected.Monster,
                WeaponArrangement.PlacePattern(selected.Placement.Pattern, start));
            var plan = new BattlePlan(MusicStage.Generate(music), new List<MonsterPlan> {
                new MonsterPlan(selected.MonsterId, selected.Monster, 1, new List<PlannedAttack> { attack }, 0)
            }, new List<PlanWithdrawal>());
            var combat = new WeaponBattle(loadout, new StageHealth(enemyMaximum), playerMaximum, playerMaximum, true);
            return new RhythmRound(plan, combat: combat);
        }
    }
}
