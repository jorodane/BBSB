using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public enum PhraseEffect { Strike, Parry, StartAdjacent, Heal }
    public enum ParryInputEdge { KeyDown, KeyUp }
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
        public int Prerequisite { get; }
        public PhraseNoteCondition Condition { get; }
        // Relative to the line that started this activation, within its weapon's footprint.
        public int LaneOffset { get; }
        public bool IsHold => HoldBeats > 0;
        public bool IsParry => Effect == PhraseEffect.Parry;
        public WeaponPhraseNote(double beat, decimal damage, double holdBeats = 0, PhraseEffect effect = PhraseEffect.Strike,
            int prerequisite = -1, PhraseNoteCondition condition = PhraseNoteCondition.Always, int laneOffset = 0)
        {
            if (!Finite(beat) || beat < 0 || !Finite(holdBeats) || holdBeats < 0 || damage < 0 ||
                !Enum.IsDefined(typeof(PhraseEffect), effect) || !Enum.IsDefined(typeof(PhraseNoteCondition), condition) ||
                (condition == PhraseNoteCondition.Always ? prerequisite != -1 : prerequisite < 0) ||
                laneOffset < 0 || laneOffset >= BattleInputLayout.LaneCount)
                throw new ArgumentOutOfRangeException(nameof(beat));
            Beat = beat; HoldBeats = holdBeats; Damage = damage; Effect = effect;
            Prerequisite = prerequisite; Condition = condition; LaneOffset = laneOffset;
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
        public ParryInputEdge ParryInput { get; }
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
            double completionCooldownBeats = 0)
        {
            WeaponCatalog.Find(weaponId);
            if (!WeaponPhraseNote.Finite(lengthBeats) || lengthBeats <= 0 || !WeaponPhraseNote.Finite(missCooldownBeats) ||
                missCooldownBeats <= 0 || finisherEvery < 0 || finisherDamage < 0 ||
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
                    (i > 0 && note.Beat <= copy[i - 1].Beat + copy[i - 1].HoldBeats))
                    throw new ArgumentException("Notes must be ordered, separated, and contained within the phrase.");
                if (note.IsParry && parryInput == ParryInputEdge.KeyUp && !note.IsHold)
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
            CompletionCooldownBeats = completionCooldownBeats;
        }
    }

    public static class WeaponPhraseCatalog
    {
        public static IReadOnlyList<string> ShieldIds { get; } = Array.AsReadOnly(new[] { "shield", "round-shield", "tower-shield", "heater-shield" });
        private static WeaponPhraseNote Tap(double at, decimal damage) => new WeaponPhraseNote(at, damage);
        private static WeaponPhraseNote Counter(double at, decimal damage) =>
            new WeaponPhraseNote(at, damage, prerequisite: 0, condition: PhraseNoteCondition.Parry);
        private static WeaponPhraseNote Hold(double at, double duration, decimal damage) => new WeaponPhraseNote(at, damage, duration);
        private static readonly WeaponPhrase[] phrases = {
            new WeaponPhrase("sword", "검", "Tap 0 / 1 / 2 · 4박 패턴", 4,
                new[] { Tap(0, 8), Tap(1, 8), Tap(2, 12) }),
            new WeaponPhrase("hammer", "트레실로 해머", "Tap 0 / 1.5 / 3 · 세 번째 완주에 그로기", 4,
                new[] { Tap(0, 6), Tap(1.5, 8), Tap(3, 12) }, 3, finisherEvery: 3, finisherDamage: 38, groggyBeats: 4),
            new WeaponPhrase("shield", "버클러", "첫 Tap 방어 성공 → 1 / 1.5 / 2 반격", 2.5,
                new[] { new WeaponPhraseNote(0, 0, effect: PhraseEffect.Parry), Counter(1, 5), Counter(1.5, 5), Counter(2, 9) }, 2, false),
            new WeaponPhrase("round-shield", "원형 방패", "첫 Tap 방어 성공 → 0.5 / 1.5 빠른 반격", 2,
                new[] { new WeaponPhraseNote(0, 0, effect: PhraseEffect.Parry), Counter(.5, 6), Counter(1.5, 10) }, 1.5, false),
            new WeaponPhrase("tower-shield", "대형 방패", "Hold 1.5박 후 떼기 방어 → 2.5박 강타", 3,
                new[] { new WeaponPhraseNote(0, 0, 1.5, PhraseEffect.Parry), Counter(2.5, 30) }, 3, false,
                parryInput: ParryInputEdge.KeyUp),
            new WeaponPhrase("heater-shield", "Heater Shield", "Hold 2 beats · press to parry · guard 50% · cooldown 2 beats", 2,
                new[] { new WeaponPhraseNote(0, 0, 2, PhraseEffect.Parry) }, repeat: false,
                holdDamageReduction: .5m, releaseEndsPhrase: true, parryRequired: false,
                completionCooldownBeats: 2),
            new WeaponPhrase("bow", "활", "Hold 1박으로 당기기 → 2박 Tap 발사", 4,
                new[] { Hold(0, 1, 0), new WeaponPhraseNote(2, 28, prerequisite: 0, condition: PhraseNoteCondition.Hit) }),
            new WeaponPhrase("dagger", "단검", "속성의 시작 박자에 맞춰 시작 → 성공하면 2박 뒤 다음 노트 · 미스 대기 2박", 2,
                new[] { Tap(0, 6) }, repeat: true),
            new WeaponPhrase("dual-swords", "쌍검", "속성의 시작 박자에 맞춰 시작 → 성공하면 1박 뒤 다음 노트 · 미스 대기 2박", 1,
                new[] { Tap(0, 6) }, repeat: true),
            new WeaponPhrase("staff", "봉", "시작한 쪽 Tap 0 / 0.5 → 반대쪽 1박에서 Hold 1박 · 2라인", 2,
                new[] { Tap(0, 8), Tap(.5, 8), new WeaponPhraseNote(1, 18, 1, laneOffset: 1) }),
            new WeaponPhrase("spirit-bell", "신령 방울", "Tap → 바로 양옆 무기 패턴을 1박 뒤 시작 · 쿨타임 무시 · 진행 중인 패턴 유지", 1,
                new[] { new WeaponPhraseNote(0, 0, effect: PhraseEffect.StartAdjacent) }),
            new WeaponPhrase("spear", "창", "Tap 0 / 2 · 4박 패턴", 4, new[] { Tap(0, 12), Tap(2, 18) }),
            new WeaponPhrase("greatsword", "대검", "Hold 1박 → 2박 Tap", 4, new[] { Hold(0, 1, 18), Tap(2, 20) }),
            new WeaponPhrase("bell", "종", "Tap → 1박에서 Hold 1박", 4, new[] { Tap(0, 8), Hold(1, 1, 16) }),
            new WeaponPhrase("blade", "쌍날검", "Tap 0 / 0.5 / 2 / 2.5", 4, new[] { Tap(0, 6), Tap(.5, 6), Tap(2, 6), Tap(2.5, 10) }),
            new WeaponPhrase("crossbow", "석궁", "Tap 0 / 1 · 3박 패턴", 3, new[] { Tap(0, 10), Tap(1, 14) }),
            new WeaponPhrase("wand", "마도봉", "Hold 1.5박 → 2.5박 Tap", 4, new[] { Hold(0, 1.5, 10), Tap(2.5, 22) })
        };
        public static IReadOnlyList<WeaponPhrase> All { get; } = Array.AsReadOnly(phrases);
        public static WeaponPhrase Find(string weaponId)
        {
            foreach (var phrase in phrases) if (phrase.WeaponId == weaponId) return phrase;
            throw new ArgumentException("Unknown phrase weapon: " + weaponId, nameof(weaponId));
        }
    }
}
