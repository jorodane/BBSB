using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SpriteCanvasGraphic : MaskableGraphic
    {
        public SpriteRenderer Source { get; private set; }
        private Transform origin;
        private float scale;
        public override Texture mainTexture => Source != null && Source.sprite != null ? Source.sprite.texture : Texture2D.whiteTexture;
        public void Bind(SpriteRenderer source, Transform root, float pixelsPerUnit)
        { Source = source; origin = root; scale = pixelsPerUnit; raycastTarget = false; Refresh(); }
        public void Refresh()
        {
            enabled = Source != null && Source.enabled && Source.gameObject.activeInHierarchy;
            SetVerticesDirty(); SetMaterialDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (Source == null || Source.sprite == null || origin == null) return;
            var sprite = Source.sprite; var vertices = sprite.vertices; var uv = sprite.uv; var triangles = sprite.triangles;
            Color tint = color * Source.color;
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = vertices[i]; if (Source.flipX) p.x = -p.x; if (Source.flipY) p.y = -p.y;
                var local = origin.InverseTransformPoint(Source.transform.TransformPoint(p)) * scale;
                vh.AddVert(new Vector3(local.x, local.y, 0), tint, uv[i]);
            }
            for (int i = 0; i < triangles.Length; i += 3) vh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
        }
    }
}
