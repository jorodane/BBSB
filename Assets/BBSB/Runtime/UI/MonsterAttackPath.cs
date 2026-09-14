using System;
using UnityEngine;

namespace BBSB.Runtime.UI
{
    /// <summary>The editor and battle sample the same path. Judgment time stays authoritative.</summary>
    public static class MonsterAttackPath
    {
        public static Vector2 Evaluate(MonsterAuthoring.Attack attack, MonsterAttackFrame frame,
            Vector2 from, Vector2 to, float heroHeight)
        {
            float t = Mathf.Clamp01((float)frame.Progress);
            if (attack == null || !attack.customTrajectory)
                return Vector2.LerpUnclamped(from, to, t) + Vector2.up * (float)frame.Lift * heroHeight;
            // Keep waiting shots at their source and contact/reaction at the judgment socket,
            // even if the user moved endpoint keys in the curve editor.
            if (t <= 0) return from;
            if (t >= 1) return to;
            float a = Sample(attack.progressCurve, 0, 0), b = Sample(attack.progressCurve, 1, 1);
            float progress = Mathf.Abs(b - a) < .00001f ? t : Mathf.Clamp01((Sample(attack.progressCurve, t, t) - a) / (b - a));
            float lift = Sample(attack.heightCurve, t, 0);
            lift -= Mathf.Lerp(Sample(attack.heightCurve, 0, 0), Sample(attack.heightCurve, 1, 0), t);
            return Vector2.LerpUnclamped(from, to, progress) + Vector2.up * lift * heroHeight;
        }
        private static float Sample(AnimationCurve curve, float t, float fallback)
        {
            float value = curve == null || curve.length == 0 ? fallback : curve.Evaluate(t);
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
        public static AnimationCurve Copy(AnimationCurve curve) => curve == null ? null :
            new AnimationCurve(curve.keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };
        public static void Validate(AnimationCurve curve, string context)
        {
            if (curve == null || curve.length < 2) throw new ArgumentException(context + "커브에 키를 두 개 이상 등록해줘.");
            foreach (var key in curve.keys)
                if (float.IsNaN(key.time) || float.IsInfinity(key.time) || float.IsNaN(key.value) || float.IsInfinity(key.value) ||
                    float.IsNaN(key.inTangent) || float.IsNaN(key.outTangent) ||
                    float.IsNaN(key.inWeight) || float.IsInfinity(key.inWeight) || float.IsNaN(key.outWeight) || float.IsInfinity(key.outWeight))
                    throw new ArgumentException(context + "커브의 키 값과 접선을 확인해줘.");
            // Infinite tangents are Unity's valid constant/step interpolation mode.
            if (curve[0].time > 0 || curve[curve.length - 1].time < 1)
                throw new ArgumentException(context + "커브는 진행 구간 0~1을 포함해야 해.");
        }
    }
}
