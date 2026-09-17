using System;

namespace BBSB.Core
{
    // Stable serialized values. Existing items and v1 starter saves default to Light.
    public enum WeaponAttribute { Light, Dark, Dual, Chaos }
    public enum WeaponBeatSide { Light, Dark }

    public static class WeaponAttributes
    {
        public static void Validate(WeaponAttribute attribute)
        { if (!Enum.IsDefined(typeof(WeaponAttribute), attribute)) throw new ArgumentOutOfRangeException(nameof(attribute)); }
        public static string Name(WeaponAttribute attribute)
        {
            Validate(attribute);
            switch (attribute)
            { case WeaponAttribute.Light: return "빛"; case WeaponAttribute.Dark: return "어둠"; case WeaponAttribute.Dual: return "이면"; default: return "혼돈"; }
        }
        public static string Hint(WeaponAttribute attribute)
        {
            Validate(attribute);
            switch (attribute)
            {
                case WeaponAttribute.Light: return "정박 시작";
                case WeaponAttribute.Dark: return "엇박 시작";
                case WeaponAttribute.Dual: return "시작 박자에 따라 빛 / 어둠 효과";
                default: return "6박 유지 후 박자 전환 · 효과 증가";
            }
        }
        public static double SnapStart(WeaponAttribute attribute, double beat)
        {
            Validate(attribute);
            if (!WeaponPhraseNote.Finite(beat) || beat < 0) throw new ArgumentOutOfRangeException(nameof(beat));
            if (attribute == WeaponAttribute.Light) return Math.Floor(beat + .5);
            if (attribute == WeaponAttribute.Dark) return Math.Floor(beat) + .5;
            return Math.Floor(beat * 2 + .5) * .5;
        }
        public static WeaponBeatSide SideAt(double beat) =>
            ((long)Math.Floor(beat * 2 + .5) & 1) == 0 ? WeaponBeatSide.Light : WeaponBeatSide.Dark;
        public static WeaponBeatSide Opposite(WeaponBeatSide side) => side == WeaponBeatSide.Light ? WeaponBeatSide.Dark : WeaponBeatSide.Light;
        public static WeaponAttribute Roll(string weaponId, SeededRandom random) =>
            (WeaponAttribute)random.Next(WeaponPhraseCatalog.Find(weaponId).Repeat ? 4 : 3);
    }

    public sealed class ChaosRules
    {
        public double MinimumBeats { get; }
        public decimal TransitionChance { get; }
        public decimal EffectMultiplier { get; }
        public ChaosRules(double minimumBeats = 6, decimal transitionChance = .25m, decimal effectMultiplier = 1.5m)
        {
            if (!WeaponPhraseNote.Finite(minimumBeats) || minimumBeats < 6 || transitionChance < 0 || transitionChance > 1 || effectMultiplier <= 1)
                throw new ArgumentOutOfRangeException(nameof(minimumBeats));
            MinimumBeats = minimumBeats; TransitionChance = transitionChance; EffectMultiplier = effectMultiplier;
        }
    }
}
