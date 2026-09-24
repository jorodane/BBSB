using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    // Starting Offset and beat side are independent axes. A multi-line Dual weapon
    // can select a different complete pattern/effect at every (Offset, side) pair.
    public sealed class WeaponPhraseSet
    {
        public IReadOnlyList<WeaponPhrase> Starts => LightStarts;
        public IReadOnlyList<WeaponPhrase> LightStarts { get; }
        public IReadOnlyList<WeaponPhrase> DarkStarts { get; }
        public IReadOnlyList<WeaponPhrase> LightTransitions { get; }
        public IReadOnlyList<WeaponPhrase> DarkTransitions { get; }
        public ChaosRules Chaos { get; }
        public bool RandomizeChaosSections { get; }
        public WeaponPhraseSet(WeaponState weapon, IReadOnlyList<WeaponPhrase> starts,
            IReadOnlyList<WeaponPhrase> darkStarts = null, IReadOnlyList<WeaponPhrase> lightTransitions = null,
            IReadOnlyList<WeaponPhrase> darkTransitions = null, ChaosRules chaos = null)
        {
            LightStarts = Validate(weapon, starts); DarkStarts = Validate(weapon, darkStarts ?? starts);
            Chaos = chaos ?? new ChaosRules();
            if (weapon.Attribute != WeaponAttribute.Chaos) return;
            foreach (var side in new[] { LightStarts, DarkStarts })
                foreach (var phrase in side)
                    if (!phrase.Repeat || Math.Abs(phrase.LengthBeats - Math.Round(phrase.LengthBeats)) > .000001)
                        throw new ArgumentException("Chaos needs repeating base patterns that retain their beat side.");
            RandomizeChaosSections = LightStarts[0].MaximumCycles > 0;
            foreach (var side in new[] { LightStarts, DarkStarts })
                foreach (var phrase in side)
                    if (RandomizeChaosSections ? phrase.MaximumCycles != 2 : phrase.MaximumCycles != 0)
                        throw new ArgumentException("Chaos bases must all use either two random sections or unlimited transitions.");
            if (RandomizeChaosSections) return;
            LightTransitions = ValidateTransitions(weapon, FillTransitions(LightStarts, lightTransitions));
            DarkTransitions = ValidateTransitions(weapon, FillTransitions(DarkStarts, darkTransitions));
        }
        public WeaponPhrase For(int offset, WeaponBeatSide side, bool transition = false)
        {
            var list = transition ? (side == WeaponBeatSide.Light ? LightTransitions : DarkTransitions) :
                (side == WeaponBeatSide.Light ? LightStarts : DarkStarts);
            return list[offset];
        }
        private static IReadOnlyList<WeaponPhrase> Validate(WeaponState weapon, IReadOnlyList<WeaponPhrase> starts)
        {
            if (weapon == null || starts == null || starts.Count != weapon.RequiredLanes)
                throw new ArgumentException("Each occupied line needs a starting pattern.", nameof(starts));
            var copy = new WeaponPhrase[starts.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                var phrase = starts[i];
                if (phrase == null || phrase.WeaponId != weapon.DefinitionId) throw new ArgumentException("Wrong weapon pattern.");
                if (weapon.DefinitionId == "wide-shield" && (phrase.Notes.Count != 1 || !phrase.Notes[0].IsHold ||
                    phrase.Repeat || phrase.ParryInput != ParryInputEdge.KeyDown))
                    throw new ArgumentException("넓은 방패는 각 입력에 하나의 짧은 Hold와 누르기 패링을 사용해.");
                foreach (var note in phrase.Notes)
                    if (note.LaneOffset >= weapon.RequiredLanes) throw new ArgumentException("Note offset lies outside this weapon's footprint.");
                copy[i] = phrase;
            }
            return Array.AsReadOnly(copy);
        }
        private static IReadOnlyList<WeaponPhrase> ValidateTransitions(WeaponState weapon, IReadOnlyList<WeaponPhrase> starts)
        {
            var result = Validate(weapon, starts);
            foreach (var phrase in result)
            {
                bool offbeat = false;
                foreach (var note in phrase.Notes) offbeat |= Math.Abs(note.Beat % 1 - .5) < .000001;
                if (!phrase.Repeat || !offbeat || Math.Abs(phrase.LengthBeats % 1 - .5) > .000001)
                    throw new ArgumentException("A Chaos transition must cross beat sides and end on the opposite half-beat grid.");
            }
            return result;
        }
        private static IReadOnlyList<WeaponPhrase> FillTransitions(IReadOnlyList<WeaponPhrase> starts, IReadOnlyList<WeaponPhrase> authored)
        {
            if (authored != null && authored.Count != starts.Count) throw new ArgumentException("Each Offset needs a transition slot.");
            var result = new WeaponPhrase[starts.Count];
            for (int i = 0; i < result.Length; i++)
            {
                if (authored != null && authored[i] != null) { result[i] = authored[i]; continue; }
                var source = starts[i];
                if (source.Notes.Count != 1 || source.Notes[0].IsHold || source.Notes[0].Condition != PhraseNoteCondition.Always)
                    throw new ArgumentException("Author a Chaos transition for this repeating pattern.");
                var note = source.Notes[0];
                result[i] = new WeaponPhrase(source.WeaponId, source.Name, "반박 3회 입력 → 반대 박자 유지", 1.5,
                    new[] { note, new WeaponPhraseNote(.5, note.Damage, effect: note.Effect, role: note.Role),
                        new WeaponPhraseNote(1, note.Damage, effect: note.Effect, role: note.Role) },
                    source.MissCooldownBeats, repeat: true, parryRequired: source.ParryRequired);
            }
            return result;
        }
        public static WeaponPhraseSet Uniform(WeaponState weapon, WeaponPhrase phrase = null)
        {
            var starts = new WeaponPhrase[weapon.RequiredLanes];
            var dark = new WeaponPhrase[starts.Length];
            var lightBridges = new WeaponPhrase[starts.Length]; var darkBridges = new WeaponPhrase[starts.Length];
            for (int i = 0; i < starts.Length; i++)
            {
                starts[i] = phrase ?? WeaponPhraseCatalog.Default(weapon.DefinitionId, i, WeaponBeatSide.Light, attribute: weapon.Attribute);
                dark[i] = phrase ?? WeaponPhraseCatalog.Default(weapon.DefinitionId, i, WeaponBeatSide.Dark, attribute: weapon.Attribute);
                lightBridges[i] = phrase != null ? null : WeaponPhraseCatalog.Default(weapon.DefinitionId, i, WeaponBeatSide.Light, true);
                darkBridges[i] = phrase != null ? null : WeaponPhraseCatalog.Default(weapon.DefinitionId, i, WeaponBeatSide.Dark, true);
            }
            return new WeaponPhraseSet(weapon, starts, dark, lightBridges, darkBridges);
        }
    }

    // Immutable, deterministic cycle plans are shared by judgment and the three-beat
    // forecast. Looking ahead never consumes randomness or changes gameplay state.
    public readonly struct WeaponRhythmCycle
    {
        public WeaponPhrase Phrase { get; }
        public WeaponBeatSide Side { get; }
        public bool IsTransition { get; }
        public double StartBeat { get; }
        public double StableSinceBeat { get; }
        public int Index { get; }
        public int BaseIndex { get; }
        public bool CanContinue => Phrase.Repeat &&
            (Phrase.MaximumCycles == 0 || IsTransition || BaseIndex + 1 < Phrase.MaximumCycles);
        internal WeaponRhythmCycle(WeaponPhrase phrase, WeaponBeatSide side, double start, double stableSince, int index, bool transition = false, int baseIndex = 0)
        { BaseIndex = baseIndex; Phrase = phrase; Side = side; StartBeat = start; StableSinceBeat = stableSince; Index = index; IsTransition = transition; }
        internal WeaponRhythmCycle Next(WeaponPhraseSet patterns, WeaponAttribute attribute, int offset, int seed)
        {
            double start = StartBeat + Phrase.LengthBeats;
            if (attribute == WeaponAttribute.Chaos && patterns.RandomizeChaosSections)
            {
                var randomSide = RandomSide(seed, BaseIndex + 1);
                // Never overlap the preceding phrase; changing sides inserts a half-beat breath.
                if (WeaponAttributes.SideAt(start) != randomSide) start += .5;
                return new WeaponRhythmCycle(patterns.For(offset, randomSide), randomSide, start, start,
                    Index + 1, baseIndex: BaseIndex + 1);
            }
            var side = IsTransition ? WeaponAttributes.Opposite(Side) : Side;
            double stableSince = IsTransition ? start : StableSinceBeat;
            var last = Phrase.Notes[Phrase.Notes.Count - 1];
            double maintained = StartBeat + last.Beat + last.HoldBeats - stableSince;
            bool transition = attribute == WeaponAttribute.Chaos && !IsTransition && maintained + .000001 >= patterns.Chaos.MinimumBeats &&
                Roll(seed, Index + 1) < patterns.Chaos.TransitionChance;
            return new WeaponRhythmCycle(patterns.For(offset, side, transition), side, start, stableSince, Index + 1, transition, transition ? BaseIndex : BaseIndex + 1);
        }
        internal static WeaponBeatSide RandomSide(int seed, int section) =>
            Roll(seed, section) < .5m ? WeaponBeatSide.Light : WeaponBeatSide.Dark;
        private static decimal Roll(int seed, int index)
        {
            uint value = unchecked((uint)seed ^ ((uint)index * 0x9e3779b9u));
            value ^= value >> 16; value = unchecked(value * 0x7feb352du);
            value ^= value >> 15; value = unchecked(value * 0x846ca68bu); value ^= value >> 16;
            return value / 4294967296m;
        }
    }
}
