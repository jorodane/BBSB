using System;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    public readonly struct DefenseHoldFrame
    {
        public bool Active { get; }
        public BattlePathPoint Position { get; }
        public double Rotation { get; }
        public DefenseHoldFrame(bool active, BattlePathPoint position, double rotation)
        { Active = active; Position = position; Rotation = rotation; }
        public double TrailRotation(int sample) => Rotation - sample * 24;
    }

    // Separate from the body's latest stroke. Each held contact spins in place;
    // the music clock keeps pause/seek deterministic and new inputs cannot reset it.
    public static class DefenseHoldMotion
    {
        public static DefenseHoldFrame Sample(FiveLaneBattle battle, int direction)
        {
            if (direction < 0 || direction > 3) throw new ArgumentOutOfRangeException(nameof(direction));
            bool active = battle.SeparateHands && battle.HasActiveHoldDefense(4 + direction);
            return new DefenseHoldFrame(active, DefenseArtifactMotion.Contact(direction), (battle.Beat % 1) * 360);
        }
        public static int Mask(FiveLaneBattle battle)
        {
            int mask = 0;
            for (int d = 0; d < 4; d++) if (Sample(battle, d).Active) mask |= 1 << d;
            return mask;
        }
    }
}
