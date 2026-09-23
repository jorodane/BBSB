using BBSB.Core;

namespace BBSB.Runtime.UI
{
    // The sprite and mesh paths share visibility and the battle's already committed
    // physical shield destination. Presentation never reroutes an incoming attack.
    public static class EnemyAttackNotes
    {
        public static bool Visible(FiveLaneBattle battle, IncomingBeatAttack attack, int slot)
        {
            var lane = battle.LaneAt(slot);
            return lane != null && WeaponCatalog.Find(lane.Weapon.DefinitionId).Kind == WeaponKind.Shield &&
                battle.CoversAttack(lane, slot, attack) &&
                SteppedNoteTrack.InHorizon(attack.Beat - battle.Beat) &&
                attack.EndBeat >= battle.Beat - battle.HalfMissWindow &&
                attack.State != IncomingAttackState.Interrupted && attack.State != IncomingAttackState.Hit;
        }
        public static bool TailVisible(FiveLaneBattle battle, IncomingBeatAttack attack) =>
            attack.Definition.IsHold && attack.EndBeat > System.Math.Max(battle.Beat, attack.Beat) &&
            SteppedNoteTrack.InHorizon(attack.EndBeat - battle.Beat);
    }
}
