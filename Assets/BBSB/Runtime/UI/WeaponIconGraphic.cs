using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WeaponIconGraphic : MaskableGraphic
    {
        public WeaponKind Kind { get; private set; }
        public void Bind(WeaponKind kind) { Kind = kind; raycastTarget = false; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var rect = rectTransform.rect;
            float length = Kind == WeaponKind.Spear ? 2.1f : Kind == WeaponKind.Greatsword ? 1.65f :
                Kind == WeaponKind.Shield || Kind == WeaponKind.Bell ? 1.1f : 1.4f;
            float size = Mathf.Min(rect.width / 2.2f, rect.height / (length + 1.3f)) * .86f;
            WeaponBattleGraphic.DrawWeapon(vh, Kind, rect.center - Vector2.up * ((length - 1) * size * .5f), size, 0);
        }
    }
}
