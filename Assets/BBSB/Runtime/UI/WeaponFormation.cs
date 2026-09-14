using System;
using System.Collections.Generic;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    /// <summary>Read-only, song-time formation motion. No timers or gameplay callbacks.</summary>
    public static class WeaponFormation
    {
        public const double SupportDuration = 1.6;
        public const double OrbitPeriod = .6;
        public const double IdleOrbitPeriod = 12;

        public static double SupportStrength(IReadOnlyList<WeaponActivation> activations, double seconds)
        {
            if (activations == null || double.IsNaN(seconds) || double.IsInfinity(seconds)) return 0;
            double strength = 0;
            for (int i = activations.Count - 1; i >= 0; i--)
            {
                double age = seconds - activations[i].AtSeconds;
                if (age >= SupportDuration) break;
                if (age < 0) continue;
                // Overlapping attacks sustain the orbit without resetting its angular phase.
                strength = Math.Max(strength, Smooth(age / .18) * (1 - Smooth((age - 1.05) / .55)));
            }
            return strength;
        }

        public static WeaponMotionFrame Sample(int slot, double seconds, BattlePathPoint hero,
            double heroHeight, double aspect, double support = 0)
        {
            if (slot < 0 || slot >= RunRules.WeaponSlots) throw new ArgumentOutOfRangeException(nameof(slot));
            if (!(aspect > 0) || double.IsInfinity(aspect)) throw new ArgumentOutOfRangeException(nameof(aspect));
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) seconds = 0;
            double phase = slot * Math.PI * 2 / RunRules.WeaponSlots;
            double idleAngle = phase + 135 * Math.PI / 180 + seconds * Math.PI * 2 / IdleOrbitPeriod + Math.Sin(seconds * .65 + phase) * .06;
            double x = Math.Cos(idleAngle) * (.42 + .016 * Math.Sin(seconds * .83 + phase));
            double y = Math.Sin(idleAngle) * .24 + .014 * Math.Sin(seconds * 1.15 + phase);
            double rotation = -20 + slot * 10 + Math.Sin(seconds * .9 + phase) * 8;
            double scale = 1 + .025 * Math.Sin(seconds * .7 + phase);
            double orbit = seconds * Math.PI * 2 / OrbitPeriod + phase;
            support = Math.Max(0, Math.Min(1, support));
            x += (-.02 + Math.Cos(orbit) * .36 - x) * support;
            y += (Math.Sin(orbit) * .22 - y) * support;
            rotation += (Math.Sin(orbit) * 32 - rotation) * support;
            scale += (.88 + .045 * Math.Sin(orbit) - scale) * support;
            return new WeaponMotionFrame(new BattlePathPoint(hero.X + x * heroHeight / aspect,
                hero.Y + y * heroHeight), rotation, scale);
        }

        public static double ReturnWeight(double progress) => Smooth((progress - .78) / .22);
        public static double Smooth(double value)
        { double p = Math.Max(0, Math.Min(1, value)); return p * p * (3 - 2 * p); }

        public static WeaponMotionFrame Attack(WeaponAttackStyle style, WeaponMotionFrame launch,
            BattlePathPoint target, WeaponMotionFrame home, double progress)
        {
            if (progress <= 0) return launch;
            if (progress >= 1) return home;
            var frame = WeaponMotion.Sample(style, launch.Position, target, progress);
            double enter = Smooth(progress / .12), leave = ReturnWeight(progress);
            double rotation = BlendAngle(launch.Rotation, frame.Rotation, enter);
            double scale = launch.Scale + (frame.Scale - launch.Scale) * enter;
            return new WeaponMotionFrame(new BattlePathPoint(
                frame.Position.X + (home.Position.X - frame.Position.X) * leave,
                frame.Position.Y + (home.Position.Y - frame.Position.Y) * leave),
                BlendAngle(rotation, home.Rotation, leave), scale + (home.Scale - scale) * leave);
        }

        private static double BlendAngle(double from, double to, double weight)
        {
            if (weight <= 0) return from;
            if (weight >= 1) return to;
            double delta = ((to - from + 180) % 360 + 360) % 360 - 180;
            return from + delta * weight;
        }
    }
}
