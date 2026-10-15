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
        public bool Releasing { get; }
        public double Smear => Phase == DefenseMotionPhase.Smear ? 1 : 0;
        public bool Active => Phase != DefenseMotionPhase.Ready;
        public PlayerDefenseFrame(DefenseMotionPhase phase, int directions, int pose, double age, bool flip = false, bool releasing = false)
        { Phase = phase; Directions = directions; Pose = pose; Age = age; Flip = flip; Releasing = releasing; }
    }

    // Presentation only. Every defense input owns the body, even on a whiff; offensive Calls never
    // restart this motion. Sample the music clock, including during pause and seek.
    public static class PlayerDefenseMotion
    {
        public const double WindupEnd = .045, SmearEnd = .13, ContactEnd = .28, RecoverEnd = .48;
        public const double ChordBeats = .035;

        public static PlayerDefenseFrame Sample(FiveLaneBattle battle)
        {
            if (!battle.SeparateHands) return Pose(0, 0, false);
            double latest = double.NegativeInfinity, latestHeld = double.NegativeInfinity, lastRelease = double.NegativeInfinity;
            int held = 0;
            for (int d = 0; d < 4; d++)
            {
                var lane = battle.LaneAt(4 + d);
                if (lane == null) continue;
                latest = Math.Max(latest, lane.LastInputBeat);
                if (FiveLaneArtTimeline.Guarding(lane))
                { held |= 1 << d; latestHeld = Math.Max(latestHeld, lane.LastInputBeat); }
                if (Released(lane)) lastRelease = Math.Max(lastRelease, lane.LastJudgedBeat);
            }
            // New input can interrupt an older hit reaction. A later impact still
            // interrupts the swing, without changing any damage or parry result.
            if (battle.LastHitBeat > latest && FiveLaneArtTimeline.Recent(battle.Beat, battle.LastHitBeat, .4))
                return new PlayerDefenseFrame(DefenseMotionPhase.Hit, 0, 15, battle.Beat - battle.LastHitBeat);
            int mask = ChordAt(battle, latest, 15);
            double age = battle.Beat - latest, released = double.NegativeInfinity;
            // Finish a quick tap's swing even when key-up occurs before rendering.
            // Only an accepted guard settles into a sustained pose afterwards.
            if (age < RecoverEnd) return Pose(mask, age, (held & mask) != 0);
            for (int d = 0; d < 4; d++)
            {
                var lane = battle.LaneAt(4 + d);
                if ((mask & 1 << d) != 0 && lane != null && Released(lane))
                    released = Math.Max(released, lane.LastJudgedBeat);
            }
            // A held left contact must not turn a new UP input into LEFT+UP. Chords
            // group new input edges only. Recovery can be interrupted at once.
            if ((held & mask) == 0 && released >= latest && !double.IsNegativeInfinity(released))
            {
                double releaseAge = battle.Beat - released;
                if (releaseAge < .22) return ReleasePose(mask, releaseAge);
            }
            if (held == 0 && battle.Beat - lastRelease < .22)
            {
                int releasedMask = 0;
                for (int d = 0; d < 4; d++)
                {
                    var lane = battle.LaneAt(4 + d);
                    if (lane != null && Released(lane) && lastRelease - lane.LastJudgedBeat <= ChordBeats) releasedMask |= 1 << d;
                }
                return ReleasePose(releasedMask, battle.Beat - lastRelease);
            }
            // Once a stroke finishes, settle into the most recently held direction
            // group. Other held contacts continue independently in DefenseHoldMotion.
            return Pose(ChordAt(battle, latestHeld, held), Math.Max(RecoverEnd, battle.Beat - latestHeld), held != 0);
        }

        private static bool Released(PhraseLane lane) => !lane.Holding && lane.Phrase.ReleaseEndsPhrase &&
            (!lane.Phrase.ParriesOnKeyUp || lane.LastGrade == RhythmGrade.Miss) &&
            // Short taps already finish their original recovery; do not rewind it.
            lane.LastJudgedBeat - lane.LastInputBeat >= RecoverEnd;
        private static PlayerDefenseFrame ReleasePose(int mask, double age)
        {
            var recovery = Pose(mask, .32, false);
            return new PlayerDefenseFrame(DefenseMotionPhase.Recover, mask, recovery.Pose, age, recovery.Flip, true);
        }

        private static int ChordAt(FiveLaneBattle battle, double latest, int allowed)
        {
            if (double.IsNegativeInfinity(latest)) return 0;
            int mask = 0;
            for (int d = 0; d < 4; d++)
            {
                var lane = battle.LaneAt(4 + d);
                if ((allowed & 1 << d) != 0 && lane != null && latest - lane.LastInputBeat <= ChordBeats)
                    mask |= 1 << d;
            }
            return mask;
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
            var clip = DefenseMotionCatalog.For(mask);
            double t = pose.Releasing ? 1 : Math.Max(0, Math.Min(1, pose.Age / SmearEnd));
            double angle = clip.StaffAngle - (1 - t) * clip.SweepDegrees;
            return new WeaponMotionFrame(clip.Contact, angle, (mask & (mask - 1)) == 0 ? 1 : 1.12);
        }
    }
}
