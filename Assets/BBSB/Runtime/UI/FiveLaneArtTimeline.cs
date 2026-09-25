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
        public static bool Guarding(PhraseLane lane) => lane.Holding && !lane.Phrase.Notes[lane.NextNote].IsCall &&
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
            var played = lane.LastPerformedNote;
            if (lane.Holding && played != null && !played.Canceled && played.Definition.IsHold &&
                played.Definition.Effect == PhraseEffect.Strike && played.Definition.Damage > 0 && beat < played.EndBeat)
                return (beat - played.SustainStartedAtBeat) % .5 < .15 ? RangedWeaponPose.Release : RangedWeaponPose.Prepare;
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
            foreach (var lane in battle.Lanes)
            {
                strike = Math.Max(strike, lane.LastDamageBeat);
                var played = lane.LastPerformedNote;
                if (played == null || played.Canceled) continue;
                if (lane.Holding && played.Definition.IsHold && battle.Beat < played.EndBeat)
                    return new FiveLaneActorFrame("TapImpact", battle.Beat - played.SustainStartedAtBeat, .5f, true);
                strike = Math.Max(strike, played.StartedAtBeat);
            }
            if (Recent(battle.Beat, strike, .4)) return new FiveLaneActorFrame("TapImpact", battle.Beat - strike, .4f);
            foreach (var lane in battle.Lanes)
                if (Guarding(lane)) return new FiveLaneActorFrame("Guard", battle.Beat - lane.NextBeat, 2, true);
            foreach (var contact in battle.ShieldContacts)
                if (contact.Phase == PhraseLanePhase.Playing) return new FiveLaneActorFrame("Guard", battle.Beat - contact.StartBeat, 2, true);
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
            var monster = battle.Formation?.Find(instanceId);
            if (monster != null && monster.Health.Defeated) return new FiveLaneActorFrame("Defeated", battle.Beat - monster.DefeatedBeat, 1);
            if (monster != null && Recent(battle.Beat, monster.LastHitBeat, .35)) return new FiveLaneActorFrame("Hit", battle.Beat - monster.LastHitBeat, .35f);
            if (monster != null ? battle.Beat < monster.GroggyUntilBeat : battle.IsGroggy) return new FiveLaneActorFrame("Hit", battle.Beat, 1, true);
            var motion = FiveLaneAttackMotion.Enemy(battle, instanceId);
            if (motion.Active && !motion.Canceled)
            {
                if (motion.Mode == AttackMotionMode.Sustain && battle.Beat >= motion.SustainStartBeat && battle.Beat < motion.EndBeat)
                    return new FiveLaneActorFrame("Attack", battle.Beat - motion.SustainStartBeat, .5f, true);
                // Hold the contact pose between combo inputs, then replay on the next actual beat.
                double age = Math.Min(.45, battle.Beat - motion.StartBeat);
                if (battle.Beat > motion.RecoverAtBeat) age += battle.Beat - motion.RecoverAtBeat;
                return new FiveLaneActorFrame("Attack", age, .7f);
            }
            IncomingBeatAttack nearest = null;
            foreach (var attack in battle.Incoming)
            {
                if (attack.Definition.MonsterId != instanceId || attack.State == IncomingAttackState.Interrupted ||
                    attack.State != IncomingAttackState.Pending || attack.Beat <= battle.Beat || attack.Beat > battle.Beat + 1) continue;
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
