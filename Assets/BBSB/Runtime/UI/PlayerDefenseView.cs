using BBSB.Core;
using UnityEngine;

namespace BBSB.Runtime.UI
{
    // This optional alpha atlas includes the hands AND staff in every pose. Rendering
    // them together prevents the weapon sliding away from the fingers during a smear.
    public sealed class PlayerDefenseView : MonoBehaviour
    {
        public const string ResourcePath = "BBSB/DefenseMotion/weapon-master";
        private ActorPrefabView actor;
        private FiveLaneBattle battle;
        private bool nativeHit;
        private readonly Sprite[] poses = new Sprite[16];
        private readonly PlayerDefenseClipLibrary clips = new PlayerDefenseClipLibrary();
        private Sprite currentPose;
        public bool HasArtwork => poses[0] != null || clips.Count > 0;
        public bool HasDirectionalArtwork => clips.Count == 15;
        public Sprite CurrentPose => currentPose;
        public PlayerDefenseFrame Frame { get; private set; }
        public bool StaffInPose => currentPose != null && battle != null && battle.LaneAt(4) != null &&
            (Frame.Phase != DefenseMotionPhase.Hit || !nativeHit);

        internal void Bind(ActorPrefabView view, FiveLaneBattle value, string characterId)
        {
            ClearArtwork();
            actor = view; battle = value;
            nativeHit = actor.Animator != null && actor.Animator.runtimeAnimatorController != null &&
                actor.Animator.HasState(0, Animator.StringToHash("Base Layer.Hit"));
            // Do not silently replace another playable character with Weapon Master.
            if (characterId != RunCharacterDefinition.DefaultId) return;
            clips.Load();
            var atlas = Resources.Load<Texture2D>(ResourcePath);
            if (atlas == null || atlas.width != atlas.height) return;
            float scale = atlas.width / 1254f;
            for (int i = 0; i < poses.Length; i++)
            {
                var box = Bounds[i]; var feet = Feet[i];
                var rect = new Rect(box.x * scale, atlas.height - box.w * scale, (box.z - box.x) * scale, (box.w - box.y) * scale);
                var pivot = new Vector2((feet.x - box.x) / (box.z - box.x), (box.w - feet.y) / (box.w - box.y));
                poses[i] = Sprite.Create(atlas, rect, pivot, 220 * scale, 0, SpriteMeshType.FullRect);
                poses[i].name = "Defense pose " + i;
            }
        }

        internal void Refresh()
        {
            Frame = PlayerDefenseMotion.Sample(battle);
            var directional = clips.Sample(Frame);
            currentPose = directional != null ? directional : poses[Frame.Pose];
            float recoil = Frame.Phase == DefenseMotionPhase.Hit ? Mathf.Sin(Mathf.PI * Mathf.Clamp01((float)Frame.Age / .4f)) : 0;
            // A shared body scale leaves headroom for the largest overhead smear;
            // do not resize individual poses to fit, which would make the feet bob.
            actor.SetPoseOverride(StaffInPose ? currentPose : null, 1.35f, directional == null && Frame.Flip,
                new Vector2(-.035f * recoil, 0), 6 * recoil);
        }
        private void OnDestroy() => ClearArtwork();
        private void ClearArtwork()
        {
            currentPose = null; clips.Dispose();
            for (int i = 0; i < poses.Length; i++) { if (poses[i] != null) Destroy(poses[i]); poses[i] = null; }
        }

        // Authored source rectangles in the delivered 1254px atlas, top-left origin.
        // Deliberately not a grid slice: the horizontal smear extends beyond a cell.
        // Common 220px body reference and per-pose planted foot pivots prevent bobbing.
        private static readonly Vector4[] Bounds = {
            new Vector4(18,60,342,312), new Vector4(354,10,642,312), new Vector4(680,6,942,312), new Vector4(942,62,1248,312),
            new Vector4(8,329,321,636), new Vector4(325,396,698,638), new Vector4(698,325,931,636), new Vector4(976,344,1254,636),
            new Vector4(16,742,347,938), new Vector4(344,638,622,936), new Vector4(622,731,972,936), new Vector4(988,631,1208,936),
            new Vector4(12,940,320,1224), new Vector4(327,947,642,1224), new Vector4(644,961,944,1224), new Vector4(950,980,1254,1224)
        };
        private static readonly Vector2[] Feet = {
            new Vector2(180,305), new Vector2(464,305), new Vector2(783,306), new Vector2(1084,305),
            new Vector2(165,626), new Vector2(464,626), new Vector2(799,626), new Vector2(1104,626),
            new Vector2(181,925), new Vector2(492,925), new Vector2(794,926), new Vector2(1100,925),
            new Vector2(160,1212), new Vector2(464,1211), new Vector2(792,1211), new Vector2(1121,1210)
        };
    }
}
