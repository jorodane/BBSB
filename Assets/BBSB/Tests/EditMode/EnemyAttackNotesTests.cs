using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class EnemyAttackNotesTests
    {
        [Test] public void NotesFollowCommittedShieldLanesAndStayInsideThePreviewHorizon()
        {
            var battle = new FiveLaneBattle(new[] { new WeaponState("heater-shield"), new WeaponState("round-shield"),
                new WeaponState("tower-shield") }, 120, 32,
                new[] { new BeatAttack("a", 4, 10), new BeatAttack("b", 4, 10, rank: AttackRank.Rear), new BeatAttack("c", 5, 10) },
                new StageHealth(1000), 100, 100);
            battle.Advance(1);
            for (int index = 0; index < battle.Incoming.Count; index++)
            {
                var attack = battle.Incoming[index];
                for (int slot = 0; slot < BattleInputLayout.LaneCount; slot++)
                    Check.Equal(index < 2 && slot == attack.ShieldSlot, EnemyAttackNotes.Visible(battle, attack, slot));
            }
            battle.Advance(2);
            var next = battle.Incoming[2];
            Check.True(EnemyAttackNotes.Visible(battle, next, next.ShieldSlot));
            Check.Equal(1, next.ShieldSlot);
            battle.Advance(5.3);
            foreach (var attack in battle.Incoming)
                Check.False(EnemyAttackNotes.Visible(battle, attack, attack.ShieldSlot));
        }
        [Test] public void HoldBodyCanEnterBeforeItsTailAndDisappearsAfterResolution()
        {
            var battle = new FiveLaneBattle(new[] { new WeaponState("heater-shield") }, 120, 32,
                new[] { new BeatAttack("a", 3, 10, 4) }, new StageHealth(1000), 100, 100);
            var attack = battle.Incoming[0];
            Check.True(EnemyAttackNotes.Visible(battle, attack, 0));
            Check.False(EnemyAttackNotes.TailVisible(battle, attack));
            battle.Advance(4);
            Check.True(EnemyAttackNotes.Visible(battle, attack, 0));
            Check.True(EnemyAttackNotes.TailVisible(battle, attack));
            battle.Advance(7.3);
            Check.False(EnemyAttackNotes.Visible(battle, attack, 0));
            Check.False(EnemyAttackNotes.TailVisible(battle, attack));
        }
    }
}
