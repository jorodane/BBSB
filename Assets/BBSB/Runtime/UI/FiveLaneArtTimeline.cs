using System;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    public readonly struct FiveLaneActorFrame
    {
        public string State { get; }
        public double Age { get; }
        public float Duration { get; }
        public bool Loop { get; }
        public FiveLaneActorFrame(string state, double age, float duration = 1, bool loop = false)
        { State = "Base Layer." + state; Age = Math.Max(0, age); Duration = duration; Loop = loop; }
    }

    // Native Animator clips are sampled from the battle clock, so pausing and resuming
    // never desynchronizes a telegraph or an effect from the actual parry window.
    public static class FiveLaneArtTimeline
    {
        public const double RushBeats = .35;
        public static bool Recent(double now, double at, double duration) => now >= at && now - at < duration;
        public static bool Guarding(PhraseLane lane) => lane.Holding &&
            (lane.Phrase.HoldDamageReduction > 0 || lane.Phrase.Notes[lane.NextNote].IsParry);

        private static bool BowDrawn(PhraseLane lane)
        {
            if (lane.Weapon.DefinitionId != "bow" || lane.Phase != PhraseLanePhase.Playing) return false;
            if (lane.Holding) return true;
            var next = lane.Phrase.Notes[lane.NextNote];
            return next.Condition == PhraseNoteCondition.Hit && next.Prerequisite >= 0 &&
                lane.Phrase.Notes[next.Prerequisite].IsHold && lane.NoteStates[next.Prerequisite] == PhraseNoteState.Hit;
        }
        public static RangedWeaponPose Weapon(PhraseLane lane, double beat)
        {
            if (lane == null || !WeaponCatalog.Find(lane.Weapon.DefinitionId).IsRanged) return RangedWeaponPose.Idle;
            if (Recent(beat, lane.LastDamageBeat, .18)) return RangedWeaponPose.Release;
            if (lane.Phase != PhraseLanePhase.Playing) return RangedWeaponPose.Idle;
            if (lane.Holding || BowDrawn(lane) ||
                lane.NextBeat >= beat && lane.NextBeat - beat <= .3) return RangedWeaponPose.Prepare;
            return RangedWeaponPose.Idle;
        }

        public static FiveLaneActorFrame Player(FiveLaneBattle battle)
        {
            if (Recent(battle.Beat, battle.LastHitBeat, .4)) return new FiveLaneActorFrame("Hit", battle.Beat - battle.LastHitBeat, .4f);
            double strike = double.NegativeInfinity;
            foreach (var lane in battle.Lanes) strike = Math.Max(strike, lane.LastDamageBeat);
            if (Recent(battle.Beat, strike, .4)) return new FiveLaneActorFrame("TapImpact", battle.Beat - strike, .4f);
            foreach (var lane in battle.Lanes)
                if (Guarding(lane)) return new FiveLaneActorFrame("Guard", battle.Beat - lane.NextBeat, 2, true);
            foreach (var incoming in battle.Incoming)
                if (incoming.State == IncomingAttackState.Blocked && Recent(battle.Beat, incoming.ResolvedBeat, .4))
                    return new FiveLaneActorFrame("Guard", battle.Beat - incoming.ResolvedBeat, .4f);
            foreach (var lane in battle.Lanes)
                if (BowDrawn(lane))
                    return new FiveLaneActorFrame("Bow", battle.Beat - lane.StartBeat, 2, true);
            return new FiveLaneActorFrame("Idle", battle.Beat, 4, true);
        }

        public static FiveLaneActorFrame Monster(FiveLaneBattle battle, string instanceId)
        {
            if (battle.IsGroggy) return new FiveLaneActorFrame("Hit", battle.Beat, 1, true);
            IncomingBeatAttack nearest = null;
            foreach (var attack in battle.Incoming)
            {
                if (attack.Definition.MonsterId != instanceId || attack.State == IncomingAttackState.Interrupted ||
                    attack.EndBeat < battle.Beat - .35 || attack.Beat > battle.Beat + 1) continue;
                if (attack.Definition.IsHold && attack.Beat <= battle.Beat && attack.EndBeat >= battle.Beat &&
                    attack.State == IncomingAttackState.Pending)
                    return new FiveLaneActorFrame("Attack", battle.Beat - attack.Beat, 1, true);
                if (nearest == null || attack.Beat < nearest.Beat) nearest = attack;
            }
            if (nearest == null) return new FiveLaneActorFrame("Idle", battle.Beat, 4, true);
            double remaining = nearest.Beat - battle.Beat;
            return remaining > RushBeats ? new FiveLaneActorFrame("Call", 1 - remaining, (float)(1 - RushBeats)) :
                new FiveLaneActorFrame("Attack", RushBeats - remaining, .7f);
        }

        public static float ProjectileProgress(double impactBeat, double beat)
        {
            double t = Math.Max(0, Math.Min(1, (beat - impactBeat + RushBeats) / RushBeats));
            return (float)(t * t * (3 - 2 * t));
        }
    }
}
