using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    /// <summary>The wrapper owns stage placement; the Animator owns the authored visual hierarchy inside it.</summary>
    public sealed class MonsterAnimatorView : MonoBehaviour
    {
        private MonsterAuthoring asset;
        private Animator animator;
        private ActorPrefabView visualHost;
        private float poseX, poseY, poseRotation, poseScaleX = 1, poseScaleY = 1;
        private readonly HashSet<string> missing = new HashSet<string>();
        public bool HasAnimator => animator != null && animator.runtimeAnimatorController != null;
        public float DisplayScale => asset != null ? asset.displayScale : 1;
        public RectTransform Rect => (RectTransform)transform;
        public MonsterAnimationSituation Situation { get; private set; }
        public string State { get; private set; }
        public void Initialize(MonsterAuthoring source)
        {
            asset = source;
            visualHost = gameObject.AddComponent<ActorPrefabView>();
            visualHost.Initialize(asset.actorPrefab != null ? asset.actorPrefab : asset.visualPrefab != null ? asset.visualPrefab.gameObject : null,
                asset.controller, asset.portrait, asset.spriteReferenceHeight);
            animator = visualHost.Animator;
        }
        public void Layout(float height)
        {
            Rect.anchorMin = Rect.anchorMax = Vector2.zero; Rect.pivot = new Vector2(.5f, 0);
            Rect.sizeDelta = new Vector2(512, 512); ApplyPose(height);
        }
        public void SetPose(float x, float y, float rotation, float sx, float sy, float height)
        {
            poseX = x; poseY = y; poseRotation = rotation; poseScaleX = sx; poseScaleY = sy; ApplyPose(height);
        }
        private void ApplyPose(float height)
        {
            Rect.anchoredPosition = new Vector2(poseX, poseY) + asset.displayOffset * height;
            Rect.localRotation = Quaternion.Euler(0, 0, poseRotation);
            Rect.localScale = new Vector3(poseScaleX, poseScaleY, 1) * (height / 512);
        }
        public void Sample(MonsterAnimationFrame frame)
        {
            Situation = frame.Situation;
            if (!HasAnimator) { visualHost.RefreshSprites(); return; }
            string state = asset.StateFor(frame);
            var motion = asset.FindMotion(frame.Situation);
            if (string.IsNullOrWhiteSpace(state) || !animator.HasState(0, Animator.StringToHash(state)))
            {
                if (!string.IsNullOrWhiteSpace(state) && missing.Add(state)) Debug.LogWarning(asset.displayName + ": Animator 상태를 찾을 수 없어: " + state, asset);
                state = asset.FindMotion(MonsterAnimationSituation.Idle)?.state;
                motion = asset.FindMotion(MonsterAnimationSituation.Idle);
            }
            if (string.IsNullOrWhiteSpace(state) || !animator.HasState(0, Animator.StringToHash(state))) return;
            float normalized = (float)(frame.AgeBeats / (motion != null ? Mathf.Max(.001f, motion.durationBeats) : .5f));
            normalized = motion != null && motion.loop ? Mathf.Repeat(normalized, 1) : Mathf.Min(normalized, .99999f);
            State = state; animator.Play(state, 0, normalized); animator.Update(0); visualHost.RefreshSprites();
        }
    }
}
