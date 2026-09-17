using System;
using BBSB.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
namespace BBSB.Editor
{
    [CustomEditor(typeof(PlayerAuthoring))]
    public sealed class PlayerAuthoringEditor : UnityEditor.Editor
    {
        [MenuItem("BBSB/Player Editor")]
        public static void Open()
        {
            PresentationPrefabBuilder.Ensure();
            Selection.activeObject = Resources.Load<PlayerAuthoring>(PlayerAuthoring.ResourcePath);
        }
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var player = (PlayerAuthoring)target;
            EditorGUILayout.HelpBox("Resources/BBSB/Characters 안에 Player 에셋을 추가하면 시작 선택창에 표시돼. Character ID는 캐릭터마다 다르게, Starting Weapons의 Key는 같은 무기를 여러 개 줘도 서로 다르게 지정해. 저장된 배치는 이 ID와 Key를 기준으로 복원돼.", MessageType.Info);
            EditorGUILayout.HelpBox("Visual Prefab에 SpriteRenderer 또는 UI Image 외형을 연결해. 동작별 Frames 또는 Animator의 전체 상태 경로를 등록하면 판정 시계에 맞춰 재생해. 조건이 더 구체적인 동작이 우선하며 Punch는 왼손 0 / 오른손 1 / 어퍼 2야. UI Image 기준 높이는 512, SpriteRenderer는 Reference Height 단위야.", MessageType.Info);
            if (player.visualPrefab != null && GUILayout.Button("외형 프리팹 열기")) AssetDatabase.OpenAsset(player.visualPrefab);
            if (player.layout != null && GUILayout.Button("크기 · 위치 · 판정 소켓 표시 설정 열기")) Selection.activeObject = player.layout;
            if (GUILayout.Button("Animator · 동작 Clip 틀 생성")) CreateAnimator(player);
            if (GUILayout.Button("검증 및 저장"))
            {
                try
                {
                    player.BuildCharacter();
                    if (player.visualPrefab != null) ActorPrefabValidation.Validate(player.visualPrefab);
                    foreach (var motion in player.motions)
                        if (motion == null || float.IsNaN(motion.durationBeats) || float.IsInfinity(motion.durationBeats) || motion.durationBeats <= 0)
                            throw new ArgumentException("동작 길이는 0보다 큰 유한한 값이어야 해.");
                    AssetDatabase.SaveAssets(); Debug.Log("플레이어 설정을 저장했어.", player);
                }
                catch (ArgumentException exception) { Debug.LogError(exception.Message, player); }
            }
        }
        private static void CreateAnimator(PlayerAuthoring player)
        {
            if (player.visualPrefab == null) { Debug.LogError("먼저 외형 프리팹을 연결해줘.", player); return; }
            string parent = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(player)).Replace('\\', '/');
            string folder = AssetDatabase.GenerateUniqueAssetPath(parent + "/PlayerAnimation");
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
            var controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/Player.controller");
            var renderer = player.visualPrefab.GetComponentInChildren<SpriteRenderer>(true);
            var image = player.visualPrefab.GetComponentInChildren<Image>(true);
            Transform animated = renderer != null ? renderer.transform : image != null ? image.transform : null;
            var animator = player.visualPrefab.GetComponentInChildren<Animator>(true);
            Transform animatorRoot = animator != null ? animator.transform : player.visualPrefab.transform;
            if (animated == null || (animated != animatorRoot && !animated.IsChildOf(animatorRoot)))
            { Debug.LogError("Animator 아래에 SpriteRenderer 또는 Image를 배치해줘.", player); return; }
            Undo.RecordObject(player, "Create player animation template");
            foreach (var motion in player.motions)
            {
                if (motion == null) continue;
                string name = string.IsNullOrWhiteSpace(motion.name) ? "Motion" : motion.name.Replace(' ', '_');
                var clip = new AnimationClip { name = name, frameRate = 30 };
                var frames = motion.frames != null && motion.frames.Length > 0 ? motion.frames : new[] { player.portrait };
                var keys = new ObjectReferenceKeyframe[frames.Length + 1];
                for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / (float)frames.Length, value = frames[i] };
                keys[frames.Length] = new ObjectReferenceKeyframe { time = 1, value = frames[frames.Length - 1] };
                AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(AnimationUtility.CalculateTransformPath(animated, animatorRoot),
                    renderer != null ? typeof(SpriteRenderer) : typeof(Image), "m_Sprite"), keys);
                AssetDatabase.CreateAsset(clip, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".anim"));
                var state = controller.layers[0].stateMachine.AddState(name); state.motion = clip;
                motion.state = "Base Layer." + state.name;
                if (motion.phase == BBSB.Runtime.UI.PlayerMotionPhase.Idle) controller.layers[0].stateMachine.defaultState = state;
            }
            player.controller = controller; EditorUtility.SetDirty(player); AssetDatabase.SaveAssets(); AssetDatabase.OpenAsset(controller);
        }
    }
}
