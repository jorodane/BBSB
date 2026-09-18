using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WeaponIconGraphic : MaskableGraphic
    {
        private string definitionId;
        private Sprite artwork;
        private WeaponSocketGraphic sockets;
        private bool fitVisibleArtwork;
        public bool FitVisibleArtwork
        {
            get => fitVisibleArtwork;
            set { if (fitVisibleArtwork == value) return; fitVisibleArtwork = value; SetAllDirty(); }
        }
        public WeaponKind Kind { get; private set; }
        public WeaponRarity Rarity { get; private set; }
        public WeaponAttribute Attribute { get; private set; }
        public bool HasAttributeArtwork { get; private set; }
        public RangedWeaponPose Pose { get; private set; }
        public bool HasArtwork => artwork != null;
        public override Texture mainTexture => artwork != null ? artwork.texture : base.mainTexture;

        public void Bind(WeaponKind kind)
        {
            foreach (var definition in WeaponCatalog.All)
                if (definition.Kind == kind) { Bind(new WeaponState(definition.Id)); return; }
        }
        public void Bind(WeaponState state)
        {
            var definition = WeaponCatalog.Find(state.DefinitionId);
            definitionId = state.DefinitionId; Pose = RangedWeaponPose.Idle;
            Kind = definition.Kind; Rarity = state.Rarity; Attribute = state.Attribute;
            HasAttributeArtwork = WeaponSpriteCache.HasAttributeArtwork(definitionId);
            artwork = WeaponSpriteCache.Get(definitionId, Rarity, attribute: Attribute); raycastTarget = false;
            if (sockets == null)
            {
                var child = new GameObject("Action sockets", typeof(RectTransform), typeof(CanvasRenderer));
                child.transform.SetParent(transform, false);
                var rect = (RectTransform)child.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                sockets = child.AddComponent<WeaponSocketGraphic>();
            }
            sockets.Bind(this, definition, state.Rarity);
            SetAllDirty();
        }
        internal void SetPose(RangedWeaponPose pose)
        {
            if (Pose == pose) return;
            Pose = pose; artwork = WeaponSpriteCache.Get(definitionId, Rarity, pose, Attribute);
            sockets.SetPose(pose); SetAllDirty();
        }
        internal void SetActivity(WeaponBattle combat, int slot, double seconds)
        { if (sockets != null) sockets.SetActivity(combat, slot, seconds); }

        internal Rect ArtworkRect
        {
            get
            {
                var rect = GetPixelAdjustedRect();
                float aspect = artwork != null ? artwork.rect.width / artwork.rect.height : 1;
                if (FitVisibleArtwork && artwork != null)
                {
                    var fitted = WeaponPreviewBounds.Fit(ArtworkBounds, aspect, rect.width, rect.height);
                    return new Rect(rect.xMin + (float)fitted.X, rect.yMin + (float)fitted.Y, (float)fitted.Width, (float)fitted.Height);
                }
                float height = Mathf.Min(rect.height, rect.width / aspect), width = height * aspect;
                return new Rect(rect.center.x - width * .5f, rect.center.y - height * .5f, width, height);
            }
        }
        private PreviewRect ArtworkBounds => HasAttributeArtwork ? WeaponAttributeArtLayout.Bounds(definitionId, Attribute, Pose) :
            WeaponPreviewBounds.Get(definitionId, Rarity, Pose);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var rect = ArtworkRect;
            if (artwork != null)
            {
                var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(artwork);
                if (FitVisibleArtwork)
                {
                    var bounds = ArtworkBounds;
                    // Crop only the preview mesh/UVs; source pixels and combat framing stay intact.
                    rect = new Rect(rect.xMin + (float)bounds.X * rect.width, rect.yMin + (float)bounds.Y * rect.height,
                        (float)bounds.Width * rect.width, (float)bounds.Height * rect.height);
                    uv = new Vector4(Mathf.Lerp(uv.x, uv.z, (float)bounds.X), Mathf.Lerp(uv.y, uv.w, (float)bounds.Y),
                        Mathf.Lerp(uv.x, uv.z, (float)(bounds.X + bounds.Width)), Mathf.Lerp(uv.y, uv.w, (float)(bounds.Y + bounds.Height)));
                }
                vh.AddVert(new Vector3(rect.xMin, rect.yMin), color, new Vector2(uv.x, uv.y));
                vh.AddVert(new Vector3(rect.xMin, rect.yMax), color, new Vector2(uv.x, uv.w));
                vh.AddVert(new Vector3(rect.xMax, rect.yMax), color, new Vector2(uv.z, uv.w));
                vh.AddVert(new Vector3(rect.xMax, rect.yMin), color, new Vector2(uv.z, uv.y));
                vh.AddTriangle(0, 1, 2); vh.AddTriangle(2, 3, 0); return;
            }
            float length = Kind == WeaponKind.Spear ? 2.1f : Kind == WeaponKind.Greatsword ? 1.65f :
                Kind == WeaponKind.Shield || Kind == WeaponKind.Bell ? 1.1f : 1.4f;
            float size = Mathf.Min(rect.width / 2.2f, rect.height / (length + 1.3f)) * .86f;
            WeaponBattleGraphic.DrawWeapon(vh, Kind, rect.center - Vector2.up * ((length - 1) * size * .5f), size, 0);
        }
        public static Color RarityColor(WeaponRarity rarity)
        {
            switch (rarity)
            {
                case WeaponRarity.Rare: return RunUI.Hex("6BBEFF");
                case WeaponRarity.Epic: return RunUI.Hex("D497FF");
                case WeaponRarity.Legendary: return RunUI.Gold;
                default: return RunUI.TextColor;
            }
        }
    }
}
