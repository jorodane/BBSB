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
            double heroHeight, double aspect, double support = 0, double orbitOffset = 0)
        {
            if (slot < 0 || slot >= RunRules.WeaponSlots) throw new ArgumentOutOfRangeException(nameof(slot));
            if (!(aspect > 0) || double.IsInfinity(aspect)) throw new ArgumentOutOfRangeException(nameof(aspect));
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) seconds = 0;
            double phase = slot * Math.PI * 2 / RunRules.WeaponSlots;
            // Blend radii, never two points on opposing orbits: every slot keeps its angular gap.
            support = Math.Max(0, Math.Min(1, support));
            double angle = phase + 135 * Math.PI / 180 + seconds * Math.PI * 2 / IdleOrbitPeriod
                + orbitOffset + Math.Sin(seconds * .65 + phase) * .06 * (1 - support);
            double radiusX = .42 + .016 * Math.Sin(seconds * .83 + phase);
            radiusX += (.36 - radiusX) * support;
            double radiusY = .32 + (.29 - .32) * support;
            double x = Math.Cos(angle) * radiusX - .02 * support;
            double y = Math.Sin(angle) * radiusY + .014 * Math.Sin(seconds * 1.15 + phase) * (1 - support);
            double rotation = -20 + slot * 10 + Math.Sin(seconds * .9 + phase) * 8;
            double scale = 1 + .025 * Math.Sin(seconds * .7 + phase);
            rotation += (Math.Sin(angle) * 32 - rotation) * support;
            scale += (.88 + .045 * Math.Sin(angle) - scale) * support;
            return new WeaponMotionFrame(new BattlePathPoint(hero.X + x * heroHeight / aspect,
                hero.Y + y * heroHeight), rotation, scale);
        }

        // A shared two-turn burst cannot restart midway when another weapon fires.
        // Completed turns are multiples of 2 PI, so dropping them preserves the moving idle phase.
        public static double OrbitOffset(IReadOnlyList<WeaponActivation> activations, double seconds)
        {
            if (activations == null || double.IsNaN(seconds) || double.IsInfinity(seconds)) return 0;
            double start = double.NegativeInfinity, duration = OrbitPeriod * 2;
            for (int i = 0; i < activations.Count; i++)
            {
                double at = activations[i].AtSeconds;
                if (at > seconds) break;
                if (at >= start + duration) start = at;
            }
            double age = seconds - start;
            return age >= 0 && age < duration ? Math.PI * 4 * Smooth(age / duration) : 0;
        }

        // Symmetric visible-art bounds about the authored pivot, including sprite aspect and banking.
        public static BattlePathPoint HalfExtents(PreviewRect bounds, double sourceAspect, double size, double rotation)
        {
            double x = Math.Max(Math.Abs(bounds.X - .5), Math.Abs(bounds.X + bounds.Width - .5))
                * size * Math.Min(1, sourceAspect);
            double y = Math.Max(Math.Abs(bounds.Y - .5), Math.Abs(bounds.Y + bounds.Height - .5))
                * size * Math.Min(1, 1 / sourceAspect);
            double angle = rotation * Math.PI / 180;
            double c = Math.Abs(Math.Cos(angle)), s = Math.Abs(Math.Sin(angle));
            return new BattlePathPoint(c * x + s * y, s * x + c * y);
        }

        // Expand the shared ellipse only as far as needed to separate every visible rectangle.
        // One common scale preserves slot ordering and avoids pairwise solver jitter.
        // Frames, extents, center and viewport use the same units (runtime uses screen-height units).
        public static void Separate(WeaponMotionFrame[] frames, BattlePathPoint[] extents, int count,
            BattlePathPoint center, PreviewRect viewport, double gap)
        {
            double expansion = 1;
            for (int i = 0; i < count; i++)
                for (int j = i + 1; j < count; j++)
                {
                    double dx = Math.Abs(frames[i].Position.X - frames[j].Position.X);
                    double dy = Math.Abs(frames[i].Position.Y - frames[j].Position.Y);
                    double x = dx > 1e-12 ? (extents[i].X + extents[j].X + gap) / dx : double.PositiveInfinity;
                    double y = dy > 1e-12 ? (extents[i].Y + extents[j].Y + gap) / dy : double.PositiveInfinity;
                    expansion = Math.Max(expansion, Math.Min(x, y));
                }
            double left = double.PositiveInfinity, bottom = double.PositiveInfinity;
            double right = double.NegativeInfinity, top = double.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                var f = frames[i];
                var p = new BattlePathPoint(center.X + (f.Position.X - center.X) * expansion,
                    center.Y + (f.Position.Y - center.Y) * expansion);
                frames[i] = new WeaponMotionFrame(p, f.Rotation, f.Scale);
                left = Math.Min(left, p.X - extents[i].X); right = Math.Max(right, p.X + extents[i].X);
                bottom = Math.Min(bottom, p.Y - extents[i].Y); top = Math.Max(top, p.Y + extents[i].Y);
            }
            if (count == 0) return;
            double moveX = FitOffset(left, right, viewport.X, viewport.Width);
            double moveY = FitOffset(bottom, top, viewport.Y, viewport.Height);
            for (int i = 0; i < count; i++)
            {
                var f = frames[i];
                frames[i] = new WeaponMotionFrame(new BattlePathPoint(f.Position.X + moveX,
                    f.Position.Y + moveY), f.Rotation, f.Scale);
            }
        }

        private static double FitOffset(double low, double high, double start, double length)
        {
            if (high - low > length) return start + length * .5 - (low + high) * .5;
            return Math.Max(start - low, Math.Min(0, start + length - high));
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
