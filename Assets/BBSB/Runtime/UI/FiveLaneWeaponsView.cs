using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;

namespace BBSB.Runtime.UI
{
    // One physical weapon per equipped item, independent of how many input lines it owns.
    internal sealed class FiveLaneWeaponsView
    {
        private readonly FiveLaneBattle battle;
        private readonly RectTransform root, player;
        private readonly List<RectTransform> enemies;
        private readonly List<string> instances;
        private readonly List<WeaponIconGraphic> weapons = new List<WeaponIconGraphic>();

        internal FiveLaneWeaponsView(FiveLaneBattle battle, RunUI ui, FiveLaneHudBindings hud,
            RectTransform playerBody, List<RectTransform> enemies, List<string> instances)
        {
            this.battle = battle; this.enemies = enemies; this.instances = instances; player = playerBody;
            root = ui.Rect("Orbiting weapons", hud.actors); RunUI.Stretch(root);
            foreach (var lane in battle.Lanes)
            {
                var rect = ui.Rect("Physical weapon " + lane.Weapon.DefinitionId, root);
                var icon = rect.gameObject.AddComponent<WeaponIconGraphic>();
                icon.FitVisibleArtwork = true; icon.Bind(lane.Weapon); icon.raycastTarget = false;
                foreach (var socket in icon.GetComponentsInChildren<WeaponSocketGraphic>()) socket.gameObject.SetActive(false);
                weapons.Add(icon);
            }
        }

        internal Vector3 Origin(PhraseLane lane)
        {
            for (int i = 0; i < battle.Lanes.Count; i++)
                if (ReferenceEquals(battle.Lanes[i], lane)) return weapons[i].rectTransform.position;
            return player.TransformPoint(player.rect.center);
        }

        internal void Refresh()
        {
            if (root.rect.width <= 0 || root.rect.height <= 0) return;
            var body = Point(player, .5f, .53f);
            double height = root.InverseTransformVector(player.TransformVector(Vector3.up * player.rect.height)).magnitude / root.rect.height;
            double aspect = root.rect.width / root.rect.height;
            for (int i = 0; i < weapons.Count; i++)
            {
                var lane = battle.Lanes[i];
                int victim = instances.IndexOf(FiveLaneWeaponMotion.Target(battle, lane));
                var target = enemies.Count > 0 ? Point(enemies[victim >= 0 ? victim : 0], .5f, .48f) : body;
                var frame = FiveLaneWeaponMotion.Sample(battle, lane, i, body, height, aspect, target);
                var icon = weapons[i]; var rect = icon.rectTransform;
                RunUI.Pin(rect, Vector2.zero, new Vector2(.5f, .5f),
                    new Vector2((float)frame.Position.X * root.rect.width, (float)frame.Position.Y * root.rect.height),
                    Vector2.one * (float)(height * root.rect.height * .34));
                rect.localRotation = Quaternion.Euler(0, 0, (float)frame.Rotation);
                rect.localScale = Vector3.one * (float)frame.Scale;
                icon.color = lane.Phase == PhraseLanePhase.Cooldown ? new Color(.65f, .65f, .72f, .8f) : Color.white;
                icon.SetPose(FiveLaneArtTimeline.Weapon(lane, battle.Beat));
            }
        }

        private BattlePathPoint Point(RectTransform rect, float x, float y)
        {
            var p = root.InverseTransformPoint(rect.TransformPoint(new Vector2(
                Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, x), Mathf.Lerp(rect.rect.yMin, rect.rect.yMax, y))));
            return new BattlePathPoint((p.x - root.rect.xMin) / root.rect.width, (p.y - root.rect.yMin) / root.rect.height);
        }
    }
}
