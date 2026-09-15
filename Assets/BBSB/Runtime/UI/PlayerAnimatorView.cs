using UnityEngine;
namespace BBSB.Runtime.UI
{
    public sealed class PlayerAnimatorView : MonoBehaviour
    {
        private PlayerAuthoring asset;
        private ActorPrefabView visual;
        private bool legacyFrame;
        public void Initialize(PlayerAuthoring source)
        {
            asset = source; legacyFrame = asset.useLegacyFrames; visual = gameObject.AddComponent<ActorPrefabView>();
            visual.Initialize(asset.visualPrefab, asset.controller, asset.portrait, asset.spriteReferenceHeight);
        }
        public void Layout(float height, PlayerMotionFrame frame, double beatSeconds, PlayerMotionDisplay layout)
        {
            float calibration = 1; Vector2 offset = Vector2.zero; Vector3 deformation = Vector3.one;
            if (legacyFrame)
            {
                if (layout != null) layout.GetCalibration(frame.SourceSheet, frame.SourceIndex, out calibration, out offset);
                var reference = layout != null && layout.referencePose != null ? layout.referencePose : asset.portrait;
                if (reference != null && reference.bounds.size.y > 0) calibration *= asset.spriteReferenceHeight / reference.bounds.size.y;
                var squash = PlayerSquashStretch.Calculate(frame, beatSeconds, layout != null ? layout.squashStretchStrength : PlayerSquashStretch.DefaultStrength);
                deformation = new Vector3((float)squash.X, (float)squash.Y, 1);
            }
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0);
            rect.sizeDelta = new Vector2(512, 512); rect.anchoredPosition = (asset.displayOffset + offset) * height;
            rect.localScale = deformation * (height * asset.displayScale * calibration / 512);
        }
        public void Sample(PlayerMotionFrame frame, double seconds, double beatSeconds, Sprite legacy)
        {
            var motion = asset.Find(frame); legacyFrame = false;
            double age = (frame.Phase == PlayerMotionPhase.Idle ? seconds : frame.PhaseAge) / beatSeconds;
            if (motion == null || !visual.Sample(motion.state, age, motion.durationBeats, motion.loop))
            {
                if (motion != null && motion.frames != null && motion.frames.Length > 0)
                {
                    float t = (float)(age / Mathf.Max(.001f, motion.durationBeats));
                    t = motion.loop ? Mathf.Repeat(t, 1) : Mathf.Clamp01(t);
                    visual.SetSprite(motion.frames[Mathf.Min(motion.frames.Length - 1, Mathf.FloorToInt(t * motion.frames.Length))]);
                }
                else if (asset.useLegacyFrames) { legacyFrame = true; visual.SetSprite(legacy); }
                else if (asset.visualPrefab == null) visual.SetSprite(asset.portrait);
                visual.RefreshSprites();
            }
        }
    }
}
