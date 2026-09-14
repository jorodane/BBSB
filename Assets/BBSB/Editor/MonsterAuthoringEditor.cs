using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime;
using BBSB.Runtime.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Editor
{
    public sealed class MonsterEditorWindow : EditorWindow
    {
        private MonsterAuthoring selected;
        private UnityEditor.Editor inspector;
        private Vector2 scroll;
        private MonsterAuthoring[] available = Array.Empty<MonsterAuthoring>();
        private void OnEnable() => RefreshMonsters();
        private void OnProjectChange() { RefreshMonsters(); Repaint(); }
        private void RefreshMonsters()
        {
            available = AssetDatabase.FindAssets("t:MonsterAuthoring")
                .Select(guid => AssetDatabase.LoadAssetAtPath<MonsterAuthoring>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null).OrderBy(asset => asset.displayName, StringComparer.Ordinal).ToArray();
        }
        [MenuItem("BBSB/Monster Editor")]
        public static void Open() => GetWindow<MonsterEditorWindow>("몬스터 에디터");
        private void OnDisable() { if (inspector != null) DestroyImmediate(inspector); }
        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("새 몬스터", EditorStyles.toolbarButton)) Create();
            if (GUILayout.Button("선택한 에셋 열기", EditorStyles.toolbarButton)) selected = Selection.activeObject as MonsterAuthoring;
            EditorGUILayout.EndHorizontal();
            var labels = new[] { "몬스터 선택…" }.Concat(available.Select(asset => asset.displayName + " (" + asset.monsterId + ")")).ToArray();
            int index = Array.IndexOf(available, selected) + 1;
            int choice = EditorGUILayout.Popup("등록된 몬스터", index, labels);
            if (choice != index) selected = choice == 0 ? null : available[choice - 1];
            selected = (MonsterAuthoring)EditorGUILayout.ObjectField("몬스터", selected, typeof(MonsterAuthoring), false);
            if (selected == null)
            { EditorGUILayout.HelpBox("새 몬스터를 만들거나 Monster 에셋을 선택해줘. 기본 모습 → 패턴 → Animator 순서로 설정하면 돼.", MessageType.Info); return; }
            UnityEditor.Editor.CreateCachedEditor(selected, typeof(MonsterAuthoringEditor), ref inspector);
            scroll = EditorGUILayout.BeginScrollView(scroll); inspector.OnInspectorGUI(); EditorGUILayout.EndScrollView();
        }
        private void Create()
        {
            const string folder = "Assets/BBSB/Resources/BBSB/Monsters";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var asset = CreateInstance<MonsterAuthoring>();
            asset.monsterId = "monster-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/NewMonster.asset");
            AssetDatabase.CreateAsset(asset, path); AssetDatabase.SaveAssets(); selected = asset; Selection.activeObject = asset;
        }
    }
    [CustomEditor(typeof(MonsterAuthoring))]
    public sealed class MonsterAuthoringEditor : UnityEditor.Editor
    {
        private int tab, selectedPattern;
        private string validation;
        private MessageType validationType;
        private float previewBpm = 120;
        private readonly string[] tabs = { "기본 모습", "패턴", "Animator 모션" };
        private MonsterAuthoring Asset => (MonsterAuthoring)target;
        public override void OnInspectorGUI()
        {
            serializedObject.Update(); tab = GUILayout.Toolbar(tab, tabs); EditorGUILayout.Space();
            if (tab == 0) Appearance(); else if (tab == 1) Patterns(); else Motions();
            if (serializedObject.ApplyModifiedProperties()) validation = null;
            EditorGUILayout.Space();
            if (GUILayout.Button("설정 검증 및 저장")) ValidateAndSave();
            if (!string.IsNullOrEmpty(validation)) EditorGUILayout.HelpBox(validation, validationType);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                previewBpm = EditorGUILayout.Slider("테스트 BPM", previewBpm, 40, 240);
                if (GUILayout.Button("선택한 패턴의 테스트 장면 열기")) CreatePreviewScene();
            }
        }
        private void Field(string name, string label) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label), true);
        private static void Field(SerializedProperty owner, string name, string label) => EditorGUILayout.PropertyField(owner.FindPropertyRelative(name), new GUIContent(label), true);
        private void Appearance()
        {
            Field("includeInEncounters", "출현 목록에 등록"); Field("monsterId", "고유 ID"); Field("displayName", "이름"); Field("description", "설명");
            Field("artId", "기존 외형 리소스 ID (선택)");
            Field("mainGesture", "주요 입력"); Field("encounterWeight", "출현 가중치"); Field("damagePerNote", "판정 기본 피해");
            Field("portrait", "기본 모습 / 도감 Sprite"); Field("displayScale", "전투 표시 크기"); Field("displayOffset", "위치 보정 (몸 높이 기준)");
            var sprite = serializedObject.FindProperty("portrait").objectReferenceValue as Sprite;
            if (sprite != null)
            {
                var preview = AssetPreview.GetAssetPreview(sprite);
                var rect = GUILayoutUtility.GetRect(100, 180, GUILayout.ExpandWidth(true));
                if (preview != null) GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit, true);
            }
            EditorGUILayout.HelpBox("Resources/BBSB/Monsters 폴더에 저장한 유효한 에셋은 다음 Play부터 출현 목록과 도감에 등록돼. ID가 겹치면 등록하지 않아. 패턴은 해당 곡의 리듬 소켓에 배치할 수 있을 때 출현해.", MessageType.Info);
        }
        private static void Beats(SerializedProperty parent, string name, string label, int minimum = 0)
        {
            var value = parent.FindPropertyRelative(name);
            EditorGUI.BeginChangeCheck(); float beats = EditorGUILayout.FloatField(label + " (박)", value.intValue / 4f);
            if (EditorGUI.EndChangeCheck() && !float.IsNaN(beats) && !float.IsInfinity(beats))
                value.intValue = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(beats, 0, 256) * 4), minimum, 1024);
        }
        private void Patterns()
        {
            Field("patternStrategy", "패턴 배치 방식");
            if (serializedObject.FindProperty("patternStrategy").enumValueIndex == (int)MonsterPatternStrategy.BeatShift)
            {
                Field("steadyCallsPerPhase", "전환 전 기본 Call 횟수");
                EditorGUILayout.HelpBox("Beat Shift는 네코마타의 정박 ↔ 엇박 반복 방식이야. 1박 간격, 0.5박 정렬, 휴식 0인 Tap 패턴 두 개(한 번 / 반 박 뒤 추가 입력)가 필요해.", MessageType.Info);
            }
            var patterns = serializedObject.FindProperty("patterns");
            EditorGUILayout.HelpBox("시간은 시작점을 0박으로 세고 0.25박 단위로 설정해. Call 시각은 Call 시작 기준, Response 시각은 Response 시작 기준이야. Hold/Dive에만 유지 길이를 지정해.", MessageType.Info);
            if (patterns.arraySize > 0)
            {
                var names = Enumerable.Range(0, patterns.arraySize).Select(i => patterns.GetArrayElementAtIndex(i).FindPropertyRelative("displayName").stringValue).ToArray();
                selectedPattern = EditorGUILayout.Popup("편집할 패턴", Mathf.Clamp(selectedPattern, 0, patterns.arraySize - 1), names);
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("패턴 추가"))
            {
                serializedObject.ApplyModifiedProperties(); Undo.RecordObject(Asset, "Add monster pattern");
                var list = new List<MonsterAuthoring.Pattern>(Asset.patterns ?? Array.Empty<MonsterAuthoring.Pattern>());
                list.Add(new MonsterAuthoring.Pattern { id = "pattern-" + Guid.NewGuid().ToString("N").Substring(0, 6) });
                Asset.patterns = list.ToArray(); EditorUtility.SetDirty(Asset); serializedObject.Update(); selectedPattern = list.Count - 1;
            }
            if (patterns.arraySize > 0 && GUILayout.Button("패턴 삭제"))
            { patterns.DeleteArrayElementAtIndex(selectedPattern); selectedPattern = Mathf.Max(0, selectedPattern - 1); }
            EditorGUILayout.EndHorizontal();
            if (patterns.arraySize == 0) return;
            var pattern = patterns.GetArrayElementAtIndex(Mathf.Clamp(selectedPattern, 0, patterns.arraySize - 1));
            Field(pattern, "id", "패턴 ID"); Field(pattern, "displayName", "패턴 이름"); Field(pattern, "description", "설명");
            Beats(pattern, "cueLeadTicks", "Call → Response 간격", 1);
            Beats(pattern, "responseTicks", "Response 구간 길이", 1); Beats(pattern, "restTicks", "이후 휴식");
            Beats(pattern, "cueAlignmentTicks", "Call 시작 정렬 단위", 1); Beats(pattern, "silentWaitTicks", "긴 무음 대기 (없으면 0)");
            Field(pattern, "participationChance", "패턴 참여 확률");
            Rows(pattern.FindPropertyRelative("calls"), true); Rows(pattern.FindPropertyRelative("steps"), false);
            Field(pattern, "attackState", "이 패턴의 Attack 상태 (선택)"); Field(pattern, "recoverState", "이 패턴의 Recover 상태 (선택)");
            DrawTimeline(pattern);
        }
        private static void Rows(SerializedProperty rows, bool calls)
        {
            EditorGUILayout.LabelField(calls ? "Call 신호" : "Response 입력", EditorStyles.boldLabel);
            for (int i = 0; i < rows.arraySize; i++)
            {
                var row = rows.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox); EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField((calls ? "Call " : "Response ") + (i + 1));
                bool delete = GUILayout.Button("삭제", GUILayout.Width(48)); EditorGUILayout.EndHorizontal();
                Beats(row, "offsetTick", "시작 시각");
                if (calls)
                {
                    Field(row, "label", "신호 이름"); Field(row, "sound", "Call 소리"); Field(row, "motion", "기본 Call 동작");
                    Field(row, "animatorState", "Animator 상태 (선택)");
                }
                else
                {
                    var kind = row.FindPropertyRelative("kind");
                    EditorGUI.BeginChangeCheck(); EditorGUILayout.PropertyField(kind, new GUIContent("입력"));
                    bool sustained = kind.enumValueIndex == (int)GestureKind.Hold || kind.enumValueIndex == (int)GestureKind.Dive;
                    if (EditorGUI.EndChangeCheck()) row.FindPropertyRelative("durationTicks").intValue = sustained ? 4 : 0;
                    if (sustained) Beats(row, "durationTicks", "유지 길이", 1);
                }
                EditorGUILayout.EndVertical();
                if (delete) { rows.DeleteArrayElementAtIndex(i); break; }
            }
            if (GUILayout.Button(calls ? "+ Call" : "+ Response"))
            {
                int index = rows.arraySize; rows.InsertArrayElementAtIndex(index);
                var row = rows.GetArrayElementAtIndex(index);
                row.FindPropertyRelative("offsetTick").intValue = index * 4;
                if (calls)
                {
                    row.FindPropertyRelative("label").stringValue = "통!"; row.FindPropertyRelative("sound").enumValueIndex = 0;
                    row.FindPropertyRelative("motion").enumValueIndex = 0; row.FindPropertyRelative("animatorState").stringValue = "";
                }
                else { row.FindPropertyRelative("kind").enumValueIndex = 0; row.FindPropertyRelative("durationTicks").intValue = 0; }
            }
        }
        private static void DrawTimeline(SerializedProperty pattern)
        {
            float cue = pattern.FindPropertyRelative("cueLeadTicks").intValue;
            float total = Mathf.Max(4, cue + pattern.FindPropertyRelative("responseTicks").intValue + pattern.FindPropertyRelative("restTicks").intValue);
            var rect = GUILayoutUtility.GetRect(100, 104, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(.12f, .14f, .19f));
            for (int tick = 0; tick <= total; tick += 4)
            {
                float x = rect.x + tick / total * rect.width;
                EditorGUI.DrawRect(new Rect(x, rect.y, 1, rect.height), Color.gray);
                if (total <= 128 || tick % 16 == 0) GUI.Label(new Rect(x + 2, rect.y, 40, 18), (tick / 4f).ToString("0.##"));
            }
            foreach (bool call in new[] { true, false })
            {
                var rows = pattern.FindPropertyRelative(call ? "calls" : "steps");
                for (int i = 0; i < rows.arraySize; i++)
                {
                    var row = rows.GetArrayElementAtIndex(i);
                    float start = (call ? 0 : cue) + row.FindPropertyRelative("offsetTick").intValue;
                    float duration = call ? .4f : Mathf.Max(.4f, row.FindPropertyRelative("durationTicks").intValue);
                    var mark = new Rect(rect.x + start / total * rect.width, rect.y + (call ? 26 : 66), Mathf.Max(4, duration / total * rect.width), 12);
                    EditorGUI.DrawRect(mark, call ? new Color(1, .75f, .25f) : new Color(.25f, .9f, .8f));
                    GUI.Label(new Rect(mark.x, mark.y + 12, 85, 20), call ? "C" + (i + 1) : ((GestureKind)row.FindPropertyRelative("kind").enumValueIndex).ToString());
                }
            }
        }
        private void Motions()
        {
            Field("useLegacyBodyAnimation", "기존 리소스 몸체 모션 사용");
            EditorGUILayout.HelpBox("기존 몬스터는 리소스에 등록된 전용 몸체 모션을 유지해. Controller 또는 외형 Prefab을 연결하면 새 외형 설정이 우선 적용돼. 기존 모션으로 돌아가려면 연결을 비우고 이 옵션을 켜줘.", MessageType.Info);
            Field("controller", "Animator Controller"); Field("visualPrefab", "UI 외형 Prefab (선택)");
            EditorGUILayout.HelpBox("Base Layer의 전체 상태 경로를 지정해. 예: Base Layer.Call. 재생 길이는 박자 기준이며 곡 시계에 맞춰 샘플링해. 자동 Transition과 Animation Event는 사용하지 않아. Prefab은 512 높이, 발 위치는 아래 중앙이고 Animator는 루트에 둬. 없으면 기본 Image 외형을 만들어.", MessageType.Info);
            Field("motions", "상황별 모션");
            if (GUILayout.Button("Animator · Clip · UI Prefab 기본 틀 생성"))
            { serializedObject.ApplyModifiedProperties(); CreateAnimationTemplate(); serializedObject.Update(); }
            if (Asset.controller != null && GUILayout.Button("Animator 열기")) AssetDatabase.OpenAsset(Asset.controller);
            if (Asset.visualPrefab != null && GUILayout.Button("외형 Prefab 열기")) AssetDatabase.OpenAsset(Asset.visualPrefab.gameObject);
        }
        private bool ValidateAndSave()
        {
            try
            {
                Asset.BuildDefinition();
                string path = AssetDatabase.GetAssetPath(Asset).Replace('\\', '/');
                if (!path.Contains("/Resources/" + MonsterAuthoring.ResourceFolder + "/"))
                    throw new ArgumentException("에셋을 Resources/BBSB/Monsters 폴더로 옮겨줘.");
                foreach (var guid in AssetDatabase.FindAssets("t:MonsterAuthoring"))
                {
                    var other = AssetDatabase.LoadAssetAtPath<MonsterAuthoring>(AssetDatabase.GUIDToAssetPath(guid));
                    if (other != Asset && other.monsterId == Asset.monsterId) throw new ArgumentException("다른 에셋과 ID가 같아: " + other.name);
                }
                ValidateAnimator();
                AssetDatabase.SaveAssets(); validation = "저장했어. 다음 Play부터 적용돼. 테스트 장면에서는 선택한 패턴을 바로 연습할 수 있어."; validationType = MessageType.Info; return true;
            }
            catch (ArgumentException exception) { validation = exception.Message; validationType = MessageType.Error; return false; }
        }
        private void ValidateAnimator()
        {
            var controller = Asset.controller;
            if (Asset.visualPrefab != null)
            {
                if (!Asset.visualPrefab.gameObject.activeSelf) throw new ArgumentException("UI Prefab의 루트를 활성화해줘.");
                if (Asset.visualPrefab.GetComponentsInChildren<Image>(true).Length == 0) throw new ArgumentException("UI Prefab에 Image가 필요해. SpriteRenderer 대신 UI Image를 사용해줘.");
                var animator = Asset.visualPrefab.GetComponent<Animator>();
                if (Asset.visualPrefab.GetComponentsInChildren<Animator>(true).Any(x => x != animator))
                    throw new ArgumentException("Animator는 UI Prefab 루트에 하나만 두고 자식의 Image를 움직여줘.");
                if (controller == null && animator != null) controller = animator.runtimeAnimatorController;
            }
            if (controller == null) return;
            while (controller is AnimatorOverrideController overrides) controller = overrides.runtimeAnimatorController;
            if (!(controller is AnimatorController graph) || graph.layers.Length != 1) throw new ArgumentException("Animator Controller는 Base Layer 하나로 구성해줘.");
            var names = new HashSet<string>();
            void Collect(AnimatorStateMachine machine, string prefix)
            {
                if (machine.anyStateTransitions.Length > 0 || machine.entryTransitions.Length > 0)
                    throw new ArgumentException("자동 Transition은 제거해줘. 상황과 박자에 맞춰 코드가 상태를 선택해.");
                foreach (var state in machine.states)
                {
                    if (state.state.transitions.Length > 0) throw new ArgumentException("상태의 Transition은 제거해줘: " + state.state.name);
                    if (state.state.motion == null) throw new ArgumentException("상태에 Animation Clip을 등록해줘: " + state.state.name);
                    names.Add(prefix + state.state.name);
                }
                foreach (var child in machine.stateMachines) Collect(child.stateMachine, prefix + child.stateMachine.name + ".");
            }
            Collect(graph.layers[0].stateMachine, graph.layers[0].name + ".");
            void Check(string state) { if (!string.IsNullOrWhiteSpace(state) && !names.Contains(state)) throw new ArgumentException("Animator 상태가 없어: " + state); }
            if (Asset.FindMotion(MonsterAnimationSituation.Idle) == null || string.IsNullOrWhiteSpace(Asset.FindMotion(MonsterAnimationSituation.Idle).state))
                throw new ArgumentException("Idle 상태를 연결해줘.");
            if (Asset.motions != null) foreach (var motion in Asset.motions) Check(motion.state);
            foreach (var pattern in Asset.patterns)
            { Check(pattern.attackState); Check(pattern.recoverState); foreach (var call in pattern.calls) Check(call.animatorState); }
        }
        private void CreateAnimationTemplate()
        {
            if (Asset.portrait == null) { validation = "먼저 기본 Sprite를 등록해줘."; validationType = MessageType.Error; return; }
            string parent = Path.GetDirectoryName(AssetDatabase.GetAssetPath(Asset)).Replace('\\', '/');
            string folder = AssetDatabase.GenerateUniqueAssetPath(parent + "/" + Asset.monsterId + "-animation");
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            var controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/Monster.controller");
            var motions = MonsterAuthoring.DefaultMotions();
            foreach (var motion in motions)
            {
                var clip = new AnimationClip { name = motion.situation.ToString(), frameRate = 30 };
                AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("Portrait", typeof(Image), "m_Sprite"),
                    new[] { new ObjectReferenceKeyframe { time = 0, value = Asset.portrait }, new ObjectReferenceKeyframe { time = 1, value = Asset.portrait } });
                AssetDatabase.CreateAsset(clip, folder + "/" + clip.name + ".anim");
                var state = controller.layers[0].stateMachine.AddState(clip.name); state.motion = clip;
                if (motion.situation == MonsterAnimationSituation.Idle) controller.layers[0].stateMachine.defaultState = state;
            }
            var root = new GameObject("Visual", typeof(RectTransform), typeof(Animator));
            try
            {
                var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(512, 512); rect.pivot = new Vector2(.5f, 0);
                root.GetComponent<Animator>().runtimeAnimatorController = controller;
                var image = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(root.transform, false); image.sprite = Asset.portrait; image.raycastTarget = false;
                var child = image.rectTransform; child.anchorMin = child.anchorMax = new Vector2(.5f, 0);
                child.pivot = Asset.portrait.pivot / Asset.portrait.rect.size;
                child.sizeDelta = new Vector2(512 * Asset.portrait.rect.width / Asset.portrait.rect.height, 512); child.anchoredPosition = Vector2.zero;
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/Visual.prefab");
                Undo.RecordObject(Asset, "Assign monster animation template"); Asset.controller = controller;
                Asset.visualPrefab = prefab.GetComponent<RectTransform>(); Asset.motions = motions; EditorUtility.SetDirty(Asset); AssetDatabase.SaveAssets();
            }
            finally { DestroyImmediate(root); }
        }
        private void CreatePreviewScene()
        {
            if (!ValidateAndSave() || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var host = new GameObject("Monster rehearsal").AddComponent<MonsterAuthoringPreview>();
            host.monster = Asset; host.patternIndex = selectedPattern; host.bpm = previewBpm;
            Selection.activeObject = host; EditorSceneManager.MarkSceneDirty(scene);
            validation = "테스트 장면을 열었어. Play를 누르면 선택한 패턴을 반복 연습해. 원래 장면은 Project 창에서 다시 열 수 있어."; validationType = MessageType.Info;
        }
    }
}
