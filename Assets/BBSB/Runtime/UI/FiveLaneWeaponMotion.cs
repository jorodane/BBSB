using System;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    // Presentation only. Preparation and actual response inputs have separate poses.
    public static class FiveLaneWeaponMotion
    {
        public static WeaponAttackStyle Style(PhraseLane lane)
        {
            foreach (var action in WeaponCatalog.Find(lane.Weapon.DefinitionId).Actions)
                if (action.Damage > 0) return action.Motion;
            return WeaponAttackStyle.Slash;
        }

        public static bool Blocking(FiveLaneBattle battle, PhraseLane lane)
        {
            if (WeaponCatalog.Find(lane.Weapon.DefinitionId).Kind != WeaponKind.Shield) return false;
            if (FiveLaneArtTimeline.Guarding(lane)) return true;
            if (lane.LastDamageBeat < lane.LastJudgedBeat && lane.LastGrade != RhythmGrade.Miss &&
                FiveLaneArtTimeline.Recent(battle.Beat, lane.LastJudgedBeat, .4)) return true;
            foreach (var contact in battle.ShieldContacts)
                if (ReferenceEquals(contact.Lane, lane) && (contact.Phase == PhraseLanePhase.Playing ||
                    contact.Grade != RhythmGrade.Miss && FiveLaneArtTimeline.Recent(battle.Beat, contact.LastJudgedBeat, .4))) return true;
            return false;
        }

        public static BattlePathPoint ShieldPoint(BattlePathPoint body, double height, double aspect) =>
            new BattlePathPoint(body.X + height * .30 / aspect, body.Y);

        public static bool Local(PhraseLane lane)
        {
            var weapon = WeaponCatalog.Find(lane.Weapon.DefinitionId);
            return weapon.IsRanged || weapon.Kind == WeaponKind.Bell || weapon.Kind == WeaponKind.SpiritBell ||
                lane.LastPerformedNote != null && lane.LastPerformedNote.Definition.Effect != PhraseEffect.Strike;
        }

        public static string Target(FiveLaneBattle battle, PhraseLane lane)
        {
            var played = lane.LastPerformedNote;
            if (played != null && double.IsPositiveInfinity(played.CompletedAtBeat))
                return battle.Formation?.TargetFor(played.Definition.Target)?.InstanceId;
            return lane.LastDamageMonsterId ?? battle.Formation?.Front?.InstanceId;
        }

        public static WeaponMotionFrame Sample(FiveLaneBattle battle, PhraseLane lane, int index,
            BattlePathPoint body, double height, double aspect, BattlePathPoint target)
        {
            double seconds = battle.Beat * 60 / battle.Bpm;
            var home = WeaponFormation.Sample(index, seconds, body, height, aspect);
            if (Blocking(battle, lane)) return new WeaponMotionFrame(ShieldPoint(body, height, aspect), -8, 1.25);
            bool local = Local(lane);
            var state = FiveLaneAttackMotion.Player(battle, lane);
            if (!state.Active)
            {
                if (local && FiveLaneArtTimeline.Weapon(lane, battle.Beat) == RangedWeaponPose.Prepare)
                    return new WeaponMotionFrame(ShieldPoint(body, height, aspect), -10, 1.22);
                return home;
            }
            double age = (battle.Beat - state.StartBeat) * 60 / battle.Bpm;
            var style = Style(lane);
            var launch = WeaponFormation.Sample(index, state.StartBeat * 60 / battle.Bpm, body, height, aspect);
            if (local)
            {
                target = ShieldPoint(body, height, aspect);
                if (WeaponCatalog.Find(lane.Weapon.DefinitionId).IsRanged)
                    launch = new WeaponMotionFrame(target, -10, 1.22);
            }
            if (local || state.Mode != AttackMotionMode.Single || state.Canceled)
                return FiveLaneAttackMotion.Sample(state, battle.Beat, launch, home, target, height, aspect, local);
            if (age < 0 || age >= WeaponMotion.Duration(style)) return home;
            var frame = WeaponFormation.Attack(style, launch, target, home, age / WeaponMotion.Duration(style));
            // Repeated isolated taps still alternate the direction of the physical stroke.
            double phase = Math.Min(1, age * battle.Bpm / 60 / state.ContactBeats);
            var position = new BattlePathPoint(frame.Position.X,
                frame.Position.Y + state.Direction * Math.Sin(phase * Math.PI) * height * .065);
            return new WeaponMotionFrame(position, home.Rotation + state.Direction * (frame.Rotation - home.Rotation), frame.Scale);
        }
    }
}
