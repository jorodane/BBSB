using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    // Its own layer/texture: changing the body clip, staff visibility or attack
    // animation cannot hide or restart any of the four held contact ornaments.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DefenseHoldGraphic : MaskableGraphic
    {
        public const string ResourcePath = "BBSB/DefenseMotion/hold-ornament";
        private FiveLaneBattle battle;
        private RectTransform player;
        private Texture2D ornament;
        public override Texture mainTexture => ornament != null ? ornament : Texture2D.whiteTexture;
        public bool HasArtwork => ornament != null;
        public int ActiveDirections => battle == null ? 0 : DefenseHoldMotion.Mask(battle);
        internal void Bind(FiveLaneBattle value, RectTransform body)
        {
            battle = value; player = body; raycastTarget = false;
            ornament = Resources.Load<Texture2D>(ResourcePath); SetAllDirty();
        }
        internal void Refresh() => SetVerticesDirty();
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (battle == null || player == null) return;
            Vector2 center = At(DefenseArtifactMotion.GripX, DefenseArtifactMotion.GripY);
            float height = Vector2.Distance(At(.5f, 0), At(.5f, 1));
            for (int d = 0; d < 4; d++)
            {
                var hold = DefenseHoldMotion.Sample(battle, d);
                if (!hold.Active) continue;
                var point = center + new Vector2((float)hold.Position.X, (float)hold.Position.Y) * height;
                for (int trail = 3; trail >= 0; trail--)
                {
                    var tint = new Color(1, 1, 1, trail == 0 ? .95f : .24f / trail);
                    float rotation = (float)hold.TrailRotation(trail);
                    // Every copy has the same center. Only the ornament rotates;
                    // no shaft, orbit or disconnected physical staff is drawn.
                    if (ornament == null) DefenseArtifactGraphic.DrawOrnament(vh, point, height * .88f, rotation, tint);
                    else DrawEcho(vh, point, height * .28f, rotation, tint);
                }
            }
        }
        private Vector2 At(float x, float y) => rectTransform.InverseTransformPoint(player.TransformPoint(
            new Vector2(Mathf.Lerp(player.rect.xMin, player.rect.xMax, x), Mathf.Lerp(player.rect.yMin, player.rect.yMax, y))));
        private static void DrawEcho(VertexHelper vh, Vector2 center, float size, float angle, Color tint)
        {
            var axis = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * size * .5f;
            var side = new Vector2(-axis.y, axis.x); int start = vh.currentVertCount;
            vh.AddVert(center - axis - side, tint, new Vector2(0, 0));
            vh.AddVert(center - axis + side, tint, new Vector2(0, 1));
            vh.AddVert(center + axis + side, tint, new Vector2(1, 1));
            vh.AddVert(center + axis - side, tint, new Vector2(1, 0));
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
