using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public enum PhraseEffect { Strike, Parry, StartAdjacent, Heal, ReduceAdjacentCooldown, EmpowerAdjacent }
    // Keep existing serialized values; Both adds a parry at each end of a Hold.
    public enum ParryInputEdge { KeyDown = 0, KeyUp = 1, Both = 2 }
    public enum PhraseLanePhase { Ready, Playing, Cooldown }
    public enum PhraseNoteCondition { Always, Hit, Parry }
    public enum PhraseNoteState { Locked, Pending, Holding, Hit, Missed, Skipped }

    // Offsets/durations are musical beats, independent of the song's BPM and monster gestures.
    public sealed class WeaponPhraseNote
    {
        public double Beat { get; }
        public double HoldBeats { get; }
        public decimal Damage { get; }
        public PhraseEffect Effect { get; }
        public double EffectDurationBeats { get; }
        public int Prerequisite { get; }
        public PhraseNoteCondition Condition { get; }
        // Relative to the line that started this activation, within its weapon's footprint.
        public int LaneOffset { get; }
        public WeaponAttackTarget Target { get; }
        public decimal BonusHealing { get; }
        public int BaseNoteIndex { get; }
        public bool IsInjected { get; }
        // A crown can cover an existing note without deleting its damage/effect/socket.
        public bool ConnectFromPrevious { get; }
        public bool IsHold => HoldBeats > 0;
        public bool IsParry => Effect == PhraseEffect.Parry;
        public WeaponPhraseNote(double beat, decimal damage, double holdBeats = 0, PhraseEffect effect = PhraseEffect.Strike,
            int prerequisite = -1, PhraseNoteCondition condition = PhraseNoteCondition.Always, int laneOffset = 0,
            double effectDurationBeats = 2, WeaponAttackTarget target = WeaponAttackTarget.Front,
            decimal bonusHealing = 0, int baseNoteIndex = -1, bool injected = false, bool connectFromPrevious = false)
        {
            if (!Finite(beat) || beat < 0 || !Finite(holdBeats) || holdBeats < 0 || damage < 0 ||
                !Enum.IsDefined(typeof(PhraseEffect), effect) || !Enum.IsDefined(typeof(PhraseNoteCondition), condition) ||
                (condition == PhraseNoteCondition.Always ? prerequisite != -1 : prerequisite < 0) ||
                laneOffset < 0 || laneOffset >= BattleInputLayout.LaneCount || !Finite(effectDurationBeats) || effectDurationBeats <= 0 ||
                !Enum.IsDefined(typeof(WeaponAttackTarget), target) || bonusHealing < 0 || baseNoteIndex < -1)
                throw new ArgumentOutOfRangeException(nameof(beat));
            Beat = beat; HoldBeats = holdBeats; Damage = damage; Effect = effect; EffectDurationBeats = effectDurationBeats;
            Prerequisite = prerequisite; Condition = condition; LaneOffset = laneOffset;
            Target = target; BonusHealing = bonusHealing; BaseNoteIndex = baseNoteIndex; IsInjected = injected;
            ConnectFromPrevious = connectFromPrevious;
        }
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public sealed class WeaponPhrase
    {
        public string WeaponId { get; }
        public string Name { get; }
        public string Hint { get; }
        public double LengthBeats { get; }
        public double MissCooldownBeats { get; }
        public bool Repeat { get; }
        // Zero preserves unlimited repetition for authored patterns. Bridges do not consume a base cycle.
        public int MaximumCycles { get; }
        public ParryInputEdge ParryInput { get; }
        public bool ParriesOnKeyDown => ParryInput != ParryInputEdge.KeyUp;
        public bool ParriesOnKeyUp => ParryInput != ParryInputEdge.KeyDown;
        public int FinisherEvery { get; }
        public decimal FinisherDamage { get; }
        public double GroggyBeats { get; }
        public decimal HoldDamageReduction { get; }
        public bool ReleaseEndsPhrase { get; }
        public bool ParryRequired { get; }
        public double CompletionCooldownBeats { get; }
        public IReadOnlyList<WeaponPhraseNote> Notes { get; }
        public WeaponPhrase(string weaponId, string name, string hint, double lengthBeats,
            IEnumerable<WeaponPhraseNote> notes, double missCooldownBeats = 2, bool repeat = false,
            int finisherEvery = 0, decimal finisherDamage = 0, double groggyBeats = 0,
            ParryInputEdge parryInput = ParryInputEdge.KeyDown,
            decimal holdDamageReduction = 0, bool releaseEndsPhrase = false, bool parryRequired = true,
            double completionCooldownBeats = 0, int maximumCycles = 0)
        {
            WeaponCatalog.Find(weaponId);
            if (!WeaponPhraseNote.Finite(lengthBeats) || lengthBeats <= 0 || !WeaponPhraseNote.Finite(missCooldownBeats) ||
                missCooldownBeats <= 0 || maximumCycles < 0 || (maximumCycles > 0 && !repeat) || finisherEvery < 0 || finisherDamage < 0 ||
                !WeaponPhraseNote.Finite(groggyBeats) || groggyBeats < 0 ||
                holdDamageReduction < 0 || holdDamageReduction > 1 ||
                !WeaponPhraseNote.Finite(completionCooldownBeats) || completionCooldownBeats < 0)
                throw new ArgumentOutOfRangeException(nameof(lengthBeats));
            var copy = new List<WeaponPhraseNote>(notes ?? throw new ArgumentNullException(nameof(notes)));
            if (copy.Count == 0 || copy[0] == null || copy[0].Beat != 0 || copy[0].LaneOffset != 0)
                throw new ArgumentException("A phrase starts at beat zero on the input line that activates it.");
            if (!Enum.IsDefined(typeof(ParryInputEdge), parryInput))
                throw new ArgumentOutOfRangeException(nameof(parryInput));
            for (int i = 0; i < copy.Count; i++)
            {
                var note = copy[i];
                if (note == null || note.Beat >= lengthBeats || note.Beat + note.HoldBeats > lengthBeats ||
                    (i > 0 && (note.Beat <= copy[i - 1].Beat || note.Beat + .000001 < copy[i - 1].Beat + copy[i - 1].HoldBeats)))
                    throw new ArgumentException("Notes must be ordered, separated, and contained within the phrase.");
                if (note.ConnectFromPrevious && (i == 0 || !copy[i - 1].IsHold ||
                    copy[i - 1].LaneOffset != note.LaneOffset || Math.Abs(copy[i - 1].Beat + copy[i - 1].HoldBeats - note.Beat) > .000001))
                    throw new ArgumentException("A connected note needs a touching Hold on the same line.");
                if (note.IsParry && parryInput != ParryInputEdge.KeyDown && !note.IsHold)
                    throw new ArgumentException("A release parry needs a Hold note.", nameof(notes));
                if (note.Condition != PhraseNoteCondition.Always && (note.Prerequisite >= i ||
                    (note.Condition == PhraseNoteCondition.Parry && !copy[note.Prerequisite].IsParry)))
                    throw new ArgumentException("A conditional note needs a matching earlier prerequisite.", nameof(notes));
            }
            WeaponId = weaponId; Name = name; Hint = hint; LengthBeats = lengthBeats;
            MissCooldownBeats = missCooldownBeats; Repeat = repeat; FinisherEvery = finisherEvery;
            FinisherDamage = finisherDamage; GroggyBeats = groggyBeats; Notes = copy.AsReadOnly();
            // Parry placement and frequency belong to the authored notes, including repeated phrases.
            ParryInput = parryInput;
            HoldDamageReduction = holdDamageReduction;
            ReleaseEndsPhrase = releaseEndsPhrase; ParryRequired = parryRequired;
            CompletionCooldownBeats = completionCooldownBeats; MaximumCycles = maximumCycles;
        }
    }

    public static class WeaponPhraseCatalog
    {
        public static IReadOnlyList<string> ShieldIds { get; } = Array.AsReadOnly(new[] { "shield", "round-shield", "tower-shield", "heater-shield", "wide-shield", "resonance-shield" });
        private static WeaponPhraseNote Counter(double at, decimal damage) =>
            new WeaponPhraseNote(at, damage, prerequisite: 0, condition: PhraseNoteCondition.Parry);
        private static readonly WeaponPhrase[] phrases = Build();
        private static WeaponPhrase[] Build()
        {
            var result = new List<WeaponPhrase> {
            new WeaponPhrase("shield", "버클러", "첫 Tap 방어 성공 → 1 / 1.5 / 2 반격 · 패링 시 쿨타임 회복", 2.5,
                new[] { new WeaponPhraseNote(0, 0, effect: PhraseEffect.Parry), Counter(1, 5), Counter(1.5, 5), Counter(2, 9) }, 2, false),
            new WeaponPhrase("round-shield", "원형 방패", "첫 Tap 방어 성공 → 0.5 / 1.5 빠른 반격 · 패링 시 쿨타임 회복", 2,
                new[] { new WeaponPhraseNote(0, 0, effect: PhraseEffect.Parry), Counter(.5, 6), Counter(1.5, 10) }, 1.5, false),
            new WeaponPhrase("tower-shield", "대형 방패", "시작·1.5박 떼기 패링 → 2.5박 강타 · 패링 시 쿨타임 회복", 3,
                new[] { new WeaponPhraseNote(0, 0, 1.5, PhraseEffect.Parry), Counter(2.5, 30) }, 3, false,
                parryInput: ParryInputEdge.Both),
            new WeaponPhrase("heater-shield", "Heater Shield", "Hold 2 beats · press to parry · guard 50% · cooldown 2 beats, reset on parry", 2,
                new[] { new WeaponPhraseNote(0, 0, 2, PhraseEffect.Parry) }, repeat: false,
                holdDamageReduction: .5m, releaseEndsPhrase: true, parryRequired: false,
                completionCooldownBeats: 2),
            new WeaponPhrase("wide-shield", "넓은 방패", "2라인 각각 2박/50% 방어와 패링 · 전체 방패의 맨 오른쪽은 후열, 나머지는 전열 순환", 2,
                new[] { new WeaponPhraseNote(0, 0, 2, PhraseEffect.Parry) }, holdDamageReduction: .5m,
                releaseEndsPhrase: true, parryRequired: false, completionCooldownBeats: 2),
            new WeaponPhrase("resonance-shield", "축적 방패", "2박 Hold · 50% 방어 · 막은 피해 12로 같은 속성의 다음 공격 +50%", 2,
                new[] { new WeaponPhraseNote(0, 0, 2) }, holdDamageReduction: .5m,
                releaseEndsPhrase: true, parryRequired: false, completionCooldownBeats: 2),
            };
            foreach (var weapon in WeaponCatalog.All)
                if (weapon.Kind != WeaponKind.Shield)
                    result.Add(LongWeaponPatterns.Pattern(weapon.Id, 0, WeaponBeatSide.Light));
            return result.ToArray();
        }
        public static IReadOnlyList<WeaponPhrase> All { get; } = Array.AsReadOnly(phrases);
        public static WeaponPhrase Default(string weaponId, int offset, WeaponBeatSide side, bool transition = false, WeaponAttribute? attribute = null)
        {
            var expanded = LongWeaponPatterns.Pattern(weaponId, offset, side, transition, attribute);
            return expanded ?? (transition ? null : Find(weaponId));
        }
        public static WeaponPhrase Find(string weaponId)
        {
            foreach (var phrase in phrases) if (phrase.WeaponId == weaponId) return phrase;
            throw new ArgumentException("Unknown phrase weapon: " + weaponId, nameof(weaponId));
        }
    }
}
