using BBSB.Core;

namespace BBSB.Runtime.UI
{
    // Presentation only. Calls never set LastDamageBeat, so they cannot launch an attack.
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

        public static WeaponMotionFrame Sample(FiveLaneBattle battle, PhraseLane lane, int index,
            BattlePathPoint body, double height, double aspect, BattlePathPoint target)
        {
            double seconds = battle.Beat * 60 / battle.Bpm;
            var home = WeaponFormation.Sample(index, seconds, body, height, aspect);
            if (Blocking(battle, lane)) return new WeaponMotionFrame(ShieldPoint(body, height, aspect), -8, 1.25);
            double age = (battle.Beat - lane.LastDamageBeat) * 60 / battle.Bpm;
            var style = Style(lane);
            if (age < 0 || age >= WeaponMotion.Duration(style)) return home;
            var launch = WeaponFormation.Sample(index, lane.LastDamageBeat * 60 / battle.Bpm, body, height, aspect);
            return WeaponFormation.Attack(style, launch, target, home, age / WeaponMotion.Duration(style));
        }
    }
}
