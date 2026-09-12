using System;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    public readonly struct WeaponMotionFrame
    {
        public BattlePathPoint Position { get; }
        public double Rotation { get; }
        public double Scale { get; }
        public WeaponMotionFrame(BattlePathPoint position, double rotation, double scale)
        { Position = position; Rotation = rotation; Scale = scale; }
    }

    /// <summary>Eight attack styles, sampled from song time so pause/replay never drifts.</summary>
    public static class WeaponMotion
    {
        public static double Duration(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.Dagger: return .34;
                case WeaponKind.Spear: return .42;
                case WeaponKind.Hammer: return .72;
                case WeaponKind.Greatsword: return .76;
                case WeaponKind.Bell: return .65;
                case WeaponKind.Blade: return .72;
                default: return .5;
            }
        }

        public static WeaponMotionFrame Sample(WeaponKind kind, BattlePathPoint from, BattlePathPoint to, double progress)
        {
            double p = Math.Max(0, Math.Min(1, progress));
            if (kind == WeaponKind.Shield)
                return new WeaponMotionFrame(new BattlePathPoint(from.X + .07 * Math.Sin(Math.PI * p), from.Y), 0, 1 + .6 * Math.Sin(Math.PI * p));
            if (kind == WeaponKind.Bell)
                return new WeaponMotionFrame(from, Math.Sin(p * Math.PI * 8) * 22 * (1 - p), 1 + .2 * Math.Sin(Math.PI * p));
            double windup = kind == WeaponKind.Greatsword ? .2 : kind == WeaponKind.Hammer ? .12 : 0;
            double travel = Math.Max(0, Math.Min(1, (p - windup) / (.6 - windup)));
            bool returning = p > .78;
            if (returning) travel = (1 - p) / .22;
            // Keep the weapon's full silhouette below the live weapon HUD. Incoming notes
            // still use the lower lane; the common counter-arrow fallback is unchanged.
            double u = 1 - travel, ceiling = Math.Max(.62, Math.Max(from.Y, to.Y) + .1);
            var point = new BattlePathPoint(from.X + (to.X - from.X) * travel,
                u * u * u * from.Y + 3 * u * travel * ceiling + travel * travel * travel * to.Y);
            double rotation = -65, scale = 1;
            switch (kind)
            {
                case WeaponKind.Sword:
                    rotation = p < .6 ? -35 : -35 - Math.Sin((p - .6) / .4 * Math.PI) * 170;
                    break;
                case WeaponKind.Spear:
                    point = new BattlePathPoint(from.X + (to.X - from.X) * travel,
                        from.Y + (to.Y - from.Y) * travel + Math.Sin(Math.PI * travel) * .08);
                    rotation = -90; scale = 1.15; break;
                case WeaponKind.Hammer:
                    point = new BattlePathPoint(point.X, point.Y + .045 * Math.Sin(Math.PI * travel));
                    rotation = 70 - 180 * travel; scale = 1.35; break;
                case WeaponKind.Dagger:
                    point = new BattlePathPoint(point.X, point.Y + Math.Sin(travel * Math.PI * 4) * .025);
                    rotation = -90 + Math.Sin(p * Math.PI * 2) * 80; scale = .8; break;
                case WeaponKind.Greatsword:
                    rotation = 40 - travel * 210; scale = 1.5; break;
                case WeaponKind.Blade:
                    rotation = p * 720; scale = 1.1; break;
            }
            return new WeaponMotionFrame(point, rotation, scale);
        }
    }
}
