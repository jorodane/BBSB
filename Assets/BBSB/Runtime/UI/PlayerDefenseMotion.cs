using System;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    public enum DefenseMotionPhase { Ready, Windup, Smear, Contact, Recover, Hold, Hit }

    public readonly struct PlayerDefenseFrame
    {
        public DefenseMotionPhase Phase { get; }
        public int Directions { get; }
        public int Pose { get; }
        public bool Flip { get; }
        public double Age { get; }
        public double Smear => Phase == DefenseMotionPhase.Smear ? 1 : 0;
        public bool Active => Phase != DefenseMotionPhase.Ready;
        public PlayerDefenseFrame(DefenseMotionPhase phase, int directions, int pose, double age, bool flip = false)
        { Phase = phase; Directions = directions; Pose = pose; Age = age; Flip = flip; }
    }

    // Presentation only. Accepted defense inputs own the body; offensive Calls never
    // restart this motion. Sample the music clock, including during pause and seek.
    public static class PlayerDefenseMotion
    {
        public const double WindupEnd = .045, SmearEnd = .13, ContactEnd = .28, RecoverEnd = .48;
        public const double ChordBeats = .035;

        public static PlayerDefenseFrame Sample(FiveLaneBattle battle)
        {
            if (!battle.SeparateHands || battle.LaneAt(4) == null) return Pose(0, 0, false);
            if (FiveLaneArtTimeline.Recent(battle.Beat, battle.LastHitBeat, .4))
                return new PlayerDefenseFrame(DefenseMotionPhase.Hit, 0, 15, battle.Beat - battle.LastHitBeat);
            double latest = double.NegativeInfinity, released = double.NegativeInfinity;
            int held = 0;
            for (int d = 0; d < 4; d++)
            {
                var lane = battle.LaneAt(4 + d);
                if (lane == null) continue;
                latest = Math.Max(latest, Trigger(lane));
                if (FiveLaneArtTimeline.Guarding(lane)) held |= 1 << d;
                if (!lane.Holding && lane.Phrase.ReleaseEndsPhrase && (!lane.Phrase.ParriesOnKeyUp || lane.LastGrade == RhythmGrade.Miss))
                    released = Math.Max(released, lane.LastJudgedBeat);
            }
            if (held == 0 && released > latest)
                return new PlayerDefenseFrame(battle.Beat - released < .22 ? DefenseMotionPhase.Recover : DefenseMotionPhase.Ready,
                    0, battle.Beat - released < .22 ? 15 : 0, battle.Beat - released);
            int mask = held;
            for (int d = 0; d < 4; d++)
            {
                var lane = battle.LaneAt(4 + d);
                if (lane != null && !double.IsNegativeInfinity(latest) && latest - Trigger(lane) <= ChordBeats)
                    mask |= 1 << d;
            }
            double age = battle.Beat - latest;
            // Released directions participate in the stroke, but not in a sustained guard.
            if (age >= RecoverEnd) mask = held;
            return Pose(mask, age, held != 0);
        }

        private static double Trigger(PhraseLane lane)
        {
            double at = lane.InvokedAtBeat;
            // Defensive notes intentionally have no offensive PerformedWeaponNote.
            // Successful Tap / key-up parries still provide their own judgment time.
            // A simple guard's release ends the pose without starting another stroke.
            if (lane.LastGrade != RhythmGrade.Miss && (!lane.Phrase.ReleaseEndsPhrase || lane.Phrase.ParriesOnKeyUp))
                at = Math.Max(at, lane.LastJudgedBeat);
            return at;
        }

        public static PlayerDefenseFrame Pose(int mask, double age, bool held)
        {
            mask &= 15;
            if (mask == 0 || age < 0 || double.IsNaN(age) || age >= RecoverEnd && !held)
                return new PlayerDefenseFrame(DefenseMotionPhase.Ready, 0, 0, 0);
            bool left = mask == 1;
            bool side = left || mask == 8;
            var phase = age < WindupEnd ? DefenseMotionPhase.Windup : age < SmearEnd ? DefenseMotionPhase.Smear :
                age < ContactEnd ? DefenseMotionPhase.Contact : age < RecoverEnd ? DefenseMotionPhase.Recover : DefenseMotionPhase.Hold;
            int contact = left ? 2 : mask == 8 ? 6 : mask == 2 ? 8 : mask == 4 ? 9 : mask == 9 ? 10 : mask == 6 ? 11 : 14;
            int pose = phase == DefenseMotionPhase.Windup ? side ? 4 : 12 :
                phase == DefenseMotionPhase.Smear ? side || mask == 9 ? 5 : 13 :
                phase == DefenseMotionPhase.Recover ? age < .38 ? side ? left ? 3 : 7 : contact : 15 : contact;
            if (phase == DefenseMotionPhase.Hold || phase == DefenseMotionPhase.Recover && held && age >= .38)
                pose = contact == 14 ? 10 : contact;
            return new PlayerDefenseFrame(phase, mask, pose, age, left && pose != 15);
        }

        public static WeaponMotionFrame Staff(PlayerDefenseFrame pose)
        {
            int mask = pose.Directions;
            if (mask == 0) return new WeaponMotionFrame(new BattlePathPoint(.06, -.08), 52, 1);
            bool single = mask == 1 || mask == 2 || mask == 4 || mask == 8;
            bool sweep = !single && mask != 9 && mask != 6;
            double target = mask == 1 || mask == 8 || mask == 6 ? 90 : sweep ? -35 : 0;
            double t = Math.Max(0, Math.Min(1, pose.Age / SmearEnd));
            double angle = target + (1 - t) * (sweep ? -160 : mask == 1 ? 60 : -60);
            var point = single ? DefenseArtifactMotion.Contact(mask == 1 ? 0 : mask == 2 ? 1 : mask == 4 ? 2 : 3) : new BattlePathPoint(0, 0);
            return new WeaponMotionFrame(point, angle, single ? 1 : 1.12);
        }
    }
}
