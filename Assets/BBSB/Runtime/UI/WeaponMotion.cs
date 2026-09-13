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

    /// <summary>The selected action owns the attack motion; all playback uses song time.</summary>
    public static class WeaponMotion
    {
        public static double Duration(WeaponAttackStyle style)
        {
            switch (style)
            {
                case WeaponAttackStyle.QuickStab: return .34;
                case WeaponAttackStyle.CounterStab: return .46;
                case WeaponAttackStyle.Thrust: return .42;
                case WeaponAttackStyle.Slam: return .72;
                case WeaponAttackStyle.ChargedSlam: return .82;
                case WeaponAttackStyle.ChargedSlash: return .76;
                case WeaponAttackStyle.RisingSlash: return .64;
                case WeaponAttackStyle.Resonance: return .65;
                case WeaponAttackStyle.Returning: return .72;
                case WeaponAttackStyle.Spin: return .6;
                default: return .5;
            }
        }

        public static WeaponMotionFrame Sample(WeaponAttackStyle style, BattlePathPoint from, BattlePathPoint to, double progress)
        {
            double p = Math.Max(0, Math.Min(1, progress));
            if (style == WeaponAttackStyle.Guard)
                return new WeaponMotionFrame(new BattlePathPoint(from.X + .07 * Math.Sin(Math.PI * p), from.Y), 0, 1 + .6 * Math.Sin(Math.PI * p));
            if (style == WeaponAttackStyle.Ward)
                return new WeaponMotionFrame(new BattlePathPoint(from.X, from.Y + .025 * Math.Sin(Math.PI * p)),
                    Math.Sin(p * Math.PI * 2) * 12, 1 + .4 * Math.Sin(Math.PI * p));
            if (style == WeaponAttackStyle.Resonance)
                return new WeaponMotionFrame(from, Math.Sin(p * Math.PI * 8) * 22 * (1 - p), 1 + .2 * Math.Sin(Math.PI * p));
            double windup = style == WeaponAttackStyle.ChargedSlash || style == WeaponAttackStyle.ChargedSlam ? .2 :
                style == WeaponAttackStyle.Slam ? .12 : 0;
            double travel = Math.Max(0, Math.Min(1, (p - windup) / (.6 - windup)));
            if (p > .78) travel = (1 - p) / .22;
            double u = 1 - travel, ceiling = Math.Max(.62, Math.Max(from.Y, to.Y) + .1);
            var point = new BattlePathPoint(from.X + (to.X - from.X) * travel,
                u * u * u * from.Y + 3 * u * travel * ceiling + travel * travel * travel * to.Y);
            double rotation = -65, scale = 1;
            switch (style)
            {
                case WeaponAttackStyle.Slash:
                    rotation = p < .6 ? -35 : -35 - Math.Sin((p - .6) / .4 * Math.PI) * 170; break;
                case WeaponAttackStyle.Sweep:
                    rotation = 30 - 260 * travel; scale = 1.2; break;
                case WeaponAttackStyle.Thrust:
                    point = new BattlePathPoint(point.X, from.Y + (to.Y - from.Y) * travel + Math.Sin(Math.PI * travel) * .08);
                    rotation = -90; scale = 1.15; break;
                case WeaponAttackStyle.Slam:
                case WeaponAttackStyle.ChargedSlam:
                    point = new BattlePathPoint(point.X, point.Y + .045 * Math.Sin(Math.PI * travel));
                    rotation = 70 - 180 * travel; scale = style == WeaponAttackStyle.Slam ? 1.35 : 1.6; break;
                case WeaponAttackStyle.QuickStab:
                    point = new BattlePathPoint(point.X, point.Y + Math.Sin(travel * Math.PI * 4) * .025);
                    rotation = -90 + Math.Sin(p * Math.PI * 2) * 80; scale = .8; break;
                case WeaponAttackStyle.CounterStab:
                    point = new BattlePathPoint(point.X, from.Y + (to.Y - from.Y) * travel - Math.Sin(Math.PI * travel) * .08);
                    rotation = -120 + 55 * travel; scale = .9; break;
                case WeaponAttackStyle.ChargedSlash:
                    rotation = 40 - travel * 210; scale = 1.5; break;
                case WeaponAttackStyle.RisingSlash:
                    point = new BattlePathPoint(point.X, from.Y + (to.Y - from.Y) * travel - Math.Sin(Math.PI * travel) * .1);
                    rotation = -160 + 240 * travel; scale = 1.4; break;
                case WeaponAttackStyle.Returning:
                    rotation = p * 720; scale = 1.1; break;
                case WeaponAttackStyle.Spin:
                    rotation = p * 1440; scale = 1.25; break;
                case WeaponAttackStyle.ShieldBash:
                    point = new BattlePathPoint(point.X, from.Y + (to.Y - from.Y) * travel);
                    rotation = -20 * Math.Sin(Math.PI * p); scale = 1.2; break;
            }
            return new WeaponMotionFrame(point, rotation, scale);
        }
    }
}
