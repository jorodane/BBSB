using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    /// <summary>Displays supplied art, or a species-specific untextured placeholder in exactly the same slot.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MonsterAttackGraphic : MaskableGraphic
    {
        private MonsterAttackDefinition definition;
        private MonsterAttackFrame frame;
        private Sprite sprite;
        private float opacity;
        private bool exactPhase;
        public MonsterAttackFrame Frame => frame;
        public Sprite CurrentSprite => sprite;
        public override Texture mainTexture => sprite != null ? sprite.texture : Texture2D.whiteTexture;

        internal void SetFrame(MonsterAttackDefinition art, MonsterAttackFrame value, Sprite image, bool exact, float alpha)
        {
            if (sprite != image) { sprite = image; SetMaterialDirty(); }
            definition = art; frame = value; opacity = alpha; exactPhase = exact;
            raycastTarget = false; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (definition == null || !frame.Visible) return;
            Color tint = Tint(definition.Shape);
            var grade = frame.Phase == MonsterAttackPhase.Perfect ? RunUI.Teal :
                frame.Phase == MonsterAttackPhase.HalfMiss ? RunUI.Gold : RunUI.Red;
            if (sprite != null)
            {
                Color paint = !exactPhase && frame.IsReaction ? Color.Lerp(Color.white, grade, .35f) : Color.white;
                paint.a = opacity;
                var uv = DataUtility.GetOuterUV(sprite); var r = rectTransform.rect;
                vh.AddVert(new Vector3(r.xMin, r.yMin), paint, new Vector2(uv.x, uv.y));
                vh.AddVert(new Vector3(r.xMax, r.yMin), paint, new Vector2(uv.z, uv.y));
                vh.AddVert(new Vector3(r.xMax, r.yMax), paint, new Vector2(uv.z, uv.w));
                vh.AddVert(new Vector3(r.xMin, r.yMax), paint, new Vector2(uv.x, uv.w));
                vh.AddTriangle(0, 1, 2); vh.AddTriangle(0, 2, 3);
                return;
            }
            if (frame.IsReaction) tint = Color.Lerp(tint, grade, .55f);
            tint.a = opacity;
            Color light = Color.Lerp(tint, Color.white, .55f); light.a = opacity;
            switch (definition.Shape)
            {
                case MonsterAttackShape.Jelly:
                    Disc(vh, .50f, .38f, .45f, .34f, tint);
                    Triangle(vh, P(.12f, .3f), P(.42f, 1), P(.75f, .25f), tint);
                    Disc(vh, .65f, .52f, .10f, .07f, light); break;
                case MonsterAttackShape.Gauntlet:
                    Box(vh, .36f, .20f, .90f, .80f, tint);
                    for (int i = 0; i < 3; i++) Box(vh, .05f, .22f + i * .2f, .44f, .39f + i * .2f, light);
                    break;
                case MonsterAttackShape.Feather:
                    Triangle(vh, P(.05f, .55f), P(.86f, .95f), P(.69f, .10f), tint);
                    Stroke(vh, .13f, .53f, .96f, .45f, .045f, light);
                    for (int i = 0; i < 4; i++) Stroke(vh, .25f + i * .14f, .51f, .4f + i * .13f, .74f, .025f, light);
                    break;
                case MonsterAttackShape.Foxfire:
                    Triangle(vh, P(.04f, .44f), P(.87f, .95f), P(.70f, .08f), tint);
                    Disc(vh, .48f, .4f, .25f, .27f, light); break;
                case MonsterAttackShape.Dream:
                    Disc(vh, .5f, .5f, .43f, .43f, tint);
                    Disc(vh, .37f, .65f, .13f, .13f, light);
                    Stroke(vh, .18f, .29f, .72f, .29f, .045f, light); break;
                case MonsterAttackShape.Doll:
                    float step = Mathf.Sin((float)frame.AnimationBeat * Mathf.PI * 2) * .09f;
                    Disc(vh, .55f, .83f, .21f, .15f, light);
                    Box(vh, .34f, .33f, .73f, .68f, tint);
                    Stroke(vh, .4f, .36f, .30f + step, .07f, .13f, light);
                    Stroke(vh, .65f, .36f, .76f - step, .07f, .13f, light);
                    Stroke(vh, .37f, .59f, frame.Phase >= MonsterAttackPhase.Contact ? .02f : .20f,
                        frame.Phase >= MonsterAttackPhase.Contact ? .86f : .43f - step, .12f, light);
                    Stroke(vh, .7f, .59f, .91f, .40f + step, .12f, light);
                    Disc(vh, .48f, .86f, .03f, .025f, tint); break;
                case MonsterAttackShape.Tail:
                    for (int i = 0; i < 12; i++) Disc(vh, .06f + i * .078f, .5f + Mathf.Sin(i * .5f) * .11f, .09f, .21f, i < 2 ? light : tint);
                    break;
                case MonsterAttackShape.Electric:
                    for (int i = 0; i < 8; i++) Stroke(vh, i / 8f, i % 2 == 0 ? .25f : .75f,
                        (i + 1) / 8f, i % 2 == 0 ? .75f : .25f, .065f, tint);
                    break;
                case MonsterAttackShape.Scale:
                    Triangle(vh, P(.08f, .70f), P(.55f, .98f), P(.57f, .02f), tint);
                    Triangle(vh, P(.55f, .98f), P(.94f, .65f), P(.57f, .02f), light); break;
                case MonsterAttackShape.Veil:
                    for (int i = 0; i < 10; i++)
                    {
                        float x = i / 10f, top = .74f + Mathf.Sin(i * .7f) * .15f;
                        Box(vh, x, .12f, x + .10f, top, i % 2 == 0 ? tint : light);
                    }
                    break;
                case MonsterAttackShape.Membrane:
                    Disc(vh, .55f, .5f, .38f, .47f, tint);
                    Disc(vh, .38f, .58f, .13f, .33f, light);
                    break;
                case MonsterAttackShape.Thread:
                    Stroke(vh, .03f, .48f, .97f, .53f, .08f, tint);
                    Box(vh, 0, .20f, .04f, .83f, light); Box(vh, .96f, .20f, 1, .83f, light); break;
            }
            if (frame.Shielded)
            { Stroke(vh, .05f, .08f, .05f, .93f, .05f, RunUI.Teal); }
        }

        private Vector2 P(float x, float y)
        { var r = rectTransform.rect; return new Vector2(r.xMin + r.width * x, r.yMin + r.height * y); }
        private void Box(VertexHelper vh, float x0, float y0, float x1, float y1, Color tint)
        { Quad(vh, P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1), tint); }
        private void Disc(VertexHelper vh, float x, float y, float rx, float ry, Color tint)
        {
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8, b = (i + 1) * Mathf.PI / 8;
                Triangle(vh, P(x, y), P(x + Mathf.Cos(a) * rx, y + Mathf.Sin(a) * ry), P(x + Mathf.Cos(b) * rx, y + Mathf.Sin(b) * ry), tint);
            }
        }
        private void Stroke(VertexHelper vh, float x0, float y0, float x1, float y1, float width, Color tint)
        {
            var a = P(x0, y0); var b = P(x1, y1); var d = (b - a).normalized;
            var n = new Vector2(-d.y, d.x) * Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * width * .5f;
            Quad(vh, a - n, b - n, b + n, a + n, tint);
        }
        private static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        { int i = vh.currentVertCount; vh.AddVert(a, tint, Vector2.zero); vh.AddVert(b, tint, Vector2.zero); vh.AddVert(c, tint, Vector2.zero); vh.AddTriangle(i, i + 1, i + 2); }
        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        { int i = vh.currentVertCount; vh.AddVert(a, tint, Vector2.zero); vh.AddVert(b, tint, Vector2.zero); vh.AddVert(c, tint, Vector2.zero); vh.AddVert(d, tint, Vector2.zero); vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3); }
        private static Color Tint(MonsterAttackShape shape)
        {
            switch (shape)
            {
                case MonsterAttackShape.Jelly: return RunUI.Hex("85DF61");
                case MonsterAttackShape.Gauntlet: return RunUI.Hex("DBC4A4");
                case MonsterAttackShape.Feather: return RunUI.Hex("B59AED");
                case MonsterAttackShape.Foxfire: return RunUI.Hex("72D3F7");
                case MonsterAttackShape.Dream: return RunUI.Hex("D8A4E6");
                case MonsterAttackShape.Doll: return RunUI.Hex("E8B468");
                case MonsterAttackShape.Tail: return RunUI.Hex("EC92B4");
                case MonsterAttackShape.Electric: return RunUI.Hex("F6E869");
                case MonsterAttackShape.Scale: return RunUI.Hex("97B7CA");
                case MonsterAttackShape.Veil: return RunUI.Hex("8CD5C1");
                case MonsterAttackShape.Membrane: return RunUI.Hex("B4E5F0");
                default: return RunUI.Hex("E9DEE9");
            }
        }
    }
}
