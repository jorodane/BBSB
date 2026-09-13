using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    // Separate white-texture mesh keeps socket colors independent of the weapon sprite's UVs.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WeaponSocketGraphic : MaskableGraphic
    {
        private WeaponIconGraphic owner;
        private WeaponDefinition definition;
        private WeaponRarity rarity;
        private IReadOnlyList<WeaponArtSocket> layout;
        private readonly float[] pulses = new float[3];
        public int SocketCount => definition == null ? 0 : definition.ActionCountAt(rarity);

        internal void Bind(WeaponIconGraphic icon, WeaponDefinition weapon, WeaponRarity grade)
        {
            owner = icon; definition = weapon; rarity = grade;
            layout = WeaponArtLayout.Sockets(weapon.Id, rarity);
            for (int i = 0; i < pulses.Length; i++) pulses[i] = 0;
            raycastTarget = false; SetVerticesDirty();
        }
        internal void SetActivity(WeaponBattle combat, int slot, double seconds)
        {
            for (int i = 0; i < pulses.Length; i++) pulses[i] = 0;
            for (int a = combat.Activations.Count - 1; a >= 0; a--)
            {
                var activation = combat.Activations[a]; double age = seconds - activation.AtSeconds;
                if (age > WeaponSocketPulse.Duration) break;
                if (activation.Slot != slot || age < 0) continue;
                for (int i = 0; i < SocketCount; i++)
                    if (definition.Actions[i].Kind == activation.Action.Kind)
                        pulses[i] = Mathf.Max(pulses[i], (float)WeaponSocketPulse.Sample(age));
            }
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (owner == null || definition == null) return;
            var rect = owner.ArtworkRect;
            for (int i = 0; i < SocketCount; i++)
            {
                var socket = layout[i];
                var center = owner.HasArtwork ? new Vector2(rect.xMin + (float)socket.X * rect.width,
                    rect.yMin + (float)socket.Y * rect.height) : rect.center + Vector2.right * ((i - (SocketCount - 1) * .5f) * rect.width * .15f);
                var radius = owner.HasArtwork ? new Vector2((float)socket.RadiusX * rect.width, (float)socket.RadiusY * rect.height) : Vector2.one * (rect.width * .055f);
                Color tint = PatternOverviewGraphic.ActionColor(definition.Actions[i].Kind);
                float pulse = pulses[i];
                var halo = tint; halo.a = .06f + .2f * pulse;
                Disc(vh, center, radius * (1.15f + .65f * pulse), halo);
                Color baseColor = Color.Lerp(tint * .72f, tint, .3f + .7f * pulse); baseColor.a = 1;
                Disc(vh, center, radius * .96f, baseColor);
                Color core = Color.Lerp(tint, Color.white, .25f + .65f * pulse); core.a = 1;
                Disc(vh, center, radius * (.43f + .18f * pulse), core);
                var shine = new Color(1, 1, 1, .65f + .3f * pulse);
                Disc(vh, center + new Vector2(-radius.x * .25f, radius.y * .3f), radius * .18f, shine);
            }
        }
        private static void Disc(VertexHelper vh, Vector2 center, Vector2 radius, Color tint)
        {
            const int segments = 28; int start = vh.currentVertCount;
            vh.AddVert(center, tint, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y), tint, Vector2.zero);
                if (i > 0) vh.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }
}
