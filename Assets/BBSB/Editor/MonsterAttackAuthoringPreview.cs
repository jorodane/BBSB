using System;
using System.Collections.Generic;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime;
using BBSB.Runtime.UI;
using UnityEditor;
using UnityEngine;

namespace BBSB.Editor
{
    public sealed partial class MonsterAuthoringEditor
    {
        internal Action repaintHost;
        private MonsterAttackPhase imagePhase = MonsterAttackPhase.Travel;
        private string imageNotice, attackPreviewKey, attackPreviewError;
        private Texture2D[] pendingTextures = Array.Empty<Texture2D>();
        private int pendingTexturePattern = -1, pendingTextureStep = -1;
        private MonsterAttackPhase pendingTexturePhase;
        private MonsterPreview attackPreview;
        private float pathScrub;
        private int playingAttack = -1;
        private double pathOrigin;
        private readonly MonsterAttackSprites previewSprites = new MonsterAttackSprites();

        private void UpdateAttackPreview()
        {
            if (playingAttack < 0 || (tab != 1 && tab != 2)) return;
            Repaint(); repaintHost?.Invoke();
        }
        private void DrawAttackImages(SerializedProperty attack, int stepIndex)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("공격 이미지", EditorStyles.boldLabel);
            imagePhase = (MonsterAttackPhase)EditorGUILayout.EnumPopup("등록할 단계", imagePhase);
            if (imagePhase == MonsterAttackPhase.Hidden) imagePhase = MonsterAttackPhase.Travel;
            var images = attack.FindPropertyRelative("images");
            var drop = GUILayoutUtility.GetRect(100, 48, GUILayout.ExpandWidth(true));
            GUI.Box(drop, imagePhase + " 이미지 드롭\nProject 창의 Sprite / 이미지 파일 여러 개를 끌어놓아줘.");
            var e = Event.current;
            if (drop.Contains(e.mousePosition) && (e.type == EventType.DragUpdated || e.type == EventType.DragPerform))
            {
                bool supported = DragAndDrop.objectReferences.Any(x => x is Sprite || x is Texture2D);
                DragAndDrop.visualMode = supported ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                if (supported && e.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    var sprites = new List<Sprite>(); var textures = new List<Texture2D>();
                    foreach (var item in DragAndDrop.objectReferences)
                    {
                        if (item is Sprite sprite) sprites.Add(sprite);
                        else if (item is Texture2D texture)
                        {
                            var found = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(texture)).OfType<Sprite>()
                                .OrderBy(x => x.name, StringComparer.Ordinal).ToArray();
                            if (found.Length == 0) textures.Add(texture); else sprites.AddRange(found);
                        }
                    }
                    AppendSprites(images, imagePhase, sprites.Distinct());
                    pendingTextures = textures.Distinct().ToArray(); pendingTexturePattern = selectedPattern;
                    pendingTextureStep = stepIndex; pendingTexturePhase = imagePhase;
                    imageNotice = sprites.Count > 0 ? "이미지를 등록했어. 아래 화살표로 프레임 순서를 바꿀 수 있어." : null;
                    serializedObject.ApplyModifiedProperties(); Repaint(); repaintHost?.Invoke();
                }
                e.Use();
            }
            if (pendingTextures.Length > 0 && pendingTexturePattern == selectedPattern && pendingTextureStep == stepIndex)
            {
                EditorGUILayout.HelpBox("Sprite로 임포트되지 않은 이미지 " + pendingTextures.Length + "개가 있어. 아래 버튼은 해당 파일의 Texture Type을 Sprite로 변경해.", MessageType.Info);
                if (GUILayout.Button("대기 이미지들을 Sprite로 임포트하고 등록"))
                {
                    var sprites = new List<Sprite>();
                    foreach (var texture in pendingTextures)
                    {
                        string path = AssetDatabase.GetAssetPath(texture);
                        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (importer == null) continue;
                        if (importer.textureType != TextureImporterType.Sprite)
                        { importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.SaveAndReimport(); }
                        sprites.AddRange(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(x => x.name, StringComparer.Ordinal));
                    }
                    AppendSprites(images, pendingTexturePhase, sprites); pendingTextures = Array.Empty<Texture2D>();
                    serializedObject.ApplyModifiedProperties(); Repaint(); repaintHost?.Invoke();
                }
            }
            if (!string.IsNullOrEmpty(imageNotice)) EditorGUILayout.HelpBox(imageNotice, MessageType.None);
            var entry = FindImagePhase(images, imagePhase);
            if (entry != null)
            {
                Field(entry, "loop", "프레임 반복");
                var frames = entry.FindPropertyRelative("frames");
                for (int n = 0; n < frames.arraySize; n++)
                {
                    EditorGUILayout.BeginHorizontal();
                    var slot = frames.GetArrayElementAtIndex(n);
                    DrawSprite(GUILayoutUtility.GetRect(68, 68, GUILayout.Width(68)), slot.objectReferenceValue as Sprite);
                    EditorGUILayout.PropertyField(slot, new GUIContent("프레임 " + (n + 1)));
                    bool previous, next;
                    using (new EditorGUI.DisabledScope(n == 0)) previous = GUILayout.Button("◀", GUILayout.Width(26));
                    using (new EditorGUI.DisabledScope(n == frames.arraySize - 1)) next = GUILayout.Button("▶", GUILayout.Width(26));
                    bool remove = GUILayout.Button("×", GUILayout.Width(26));
                    EditorGUILayout.EndHorizontal();
                    if (remove) { slot.objectReferenceValue = null; frames.DeleteArrayElementAtIndex(n); break; }
                    if (previous || next) { frames.MoveArrayElement(n, n + (previous ? -1 : 1)); break; }
                }
            }
            else EditorGUILayout.HelpBox("이 단계에 직접 등록한 이미지는 없어. 기존 리소스 이미지가 있으면 미리보기에서 사용해.", MessageType.None);
            if (GUILayout.Button("빈 Sprite 슬롯 추가")) AppendSprites(images, imagePhase, new Sprite[] { null });
        }
        private static SerializedProperty FindImagePhase(SerializedProperty images, MonsterAttackPhase phase)
        {
            for (int i = 0; i < images.arraySize; i++)
            {
                var row = images.GetArrayElementAtIndex(i);
                if (row.FindPropertyRelative("phase").enumValueIndex == (int)phase) return row;
            }
            return null;
        }
        private static void AppendSprites(SerializedProperty images, MonsterAttackPhase phase, IEnumerable<Sprite> sprites)
        {
            var values = sprites.ToArray(); if (values.Length == 0) return;
            var row = FindImagePhase(images, phase);
            if (row == null)
            {
                int index = images.arraySize; images.InsertArrayElementAtIndex(index); row = images.GetArrayElementAtIndex(index);
                row.FindPropertyRelative("phase").enumValueIndex = (int)phase;
                row.FindPropertyRelative("loop").boolValue = true; row.FindPropertyRelative("frames").ClearArray();
            }
            var frames = row.FindPropertyRelative("frames");
            foreach (var sprite in values)
            { int n = frames.arraySize; frames.InsertArrayElementAtIndex(n); frames.GetArrayElementAtIndex(n).objectReferenceValue = sprite; }
        }
        private static void DrawSprite(Rect rect, Sprite sprite)
        {
            EditorGUI.DrawRect(rect, new Color(.18f, .19f, .23f));
            if (sprite == null) { GUI.Label(rect, "이미지 없음", EditorStyles.centeredGreyMiniLabel); return; }
            // AssetPreview handles packed/tight sprites without assuming the whole texture is one frame.
            var preview = AssetPreview.GetAssetPreview(sprite);
            if (preview != null) { GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit, true); return; }
            try
            {
                var source = sprite.textureRect; var texture = sprite.texture;
                float scale = Mathf.Min(rect.width / source.width, rect.height / source.height);
                var destination = new Rect(rect.center.x - source.width * scale / 2, rect.center.y - source.height * scale / 2,
                    source.width * scale, source.height * scale);
                GUI.DrawTextureWithTexCoords(destination, texture, new Rect(source.x / texture.width, source.y / texture.height,
                    source.width / texture.width, source.height / texture.height), true);
            }
            catch (UnityException) { GUI.Label(rect, AssetPreview.GetMiniThumbnail(sprite)); }
        }
        private void DrawAttackPath(SerializedProperty pattern, SerializedProperty step, int index)
        {
            var attack = step.FindPropertyRelative("attack");
            EditorGUILayout.Space(); EditorGUILayout.LabelField("궤적 · 진행 미리보기", EditorStyles.boldLabel);
            Field(attack, "customTrajectory", "Animation Curve로 궤적 조절");
            if (attack.FindPropertyRelative("customTrajectory").boolValue)
            {
                Field(attack, "progressCurve", "진행률 커브 (0 → 1)"); Field(attack, "heightCurve", "높이 커브 (플레이어 키 기준)");
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("직선")) SetPathCurves(attack, false);
                if (GUILayout.Button("포물선")) SetPathCurves(attack, true);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox("커브를 클릭하면 Unity 키·접선 편집기가 열려. 가로축은 이동 진행 0~1, 높이 값은 플레이어 키 비율이야. 이 높이 커브가 기존 솟는 높이를 대신해. 출발·도착은 고정하고 WaitRush의 대기 시간과 판정 박자는 유지해.", MessageType.None);
            }
            // Preview an immutable battle plan only when authored data changes, not on every repaint.
            if (serializedObject.ApplyModifiedProperties()) validation = null;
            string key = selectedPattern + ":" + previewBpm + ":" + EditorJsonUtility.ToJson(Asset);
            if (key != attackPreviewKey)
            {
                attackPreviewKey = key; attackPreview = null; attackPreviewError = null;
                try
                {
                    var monster = Asset.BuildDefinition();
                    attackPreview = new MonsterPreview(monster, monster.Patterns[selectedPattern], previewBpm);
                }
                catch (ArgumentException exception) { attackPreviewError = exception.Message; playingAttack = -1; }
            }
            if (attackPreview == null)
            { EditorGUILayout.HelpBox("궤적 미리보기를 위해 설정을 먼저 확인해줘: " + attackPreviewError, MessageType.Info); return; }
            var data = Asset.patterns[selectedPattern]; var source = data.steps[index];
            var ordered = data.steps.OrderBy(x => x.offsetTick).ThenBy(x => x.kind).ToArray();
            int sortedIndex = Array.IndexOf(ordered, source);
            var note = attackPreview.Round.Notes.FirstOrDefault(x => x.StepIndex == sortedIndex);
            if (note == null) return;
            var definition = MonsterAttackCatalog.For(note); var config = MonsterAuthoring.FindAttackArt(definition);
            double beat = attackPreview.BeatSeconds, spawn = definition.SpawnSeconds(note, beat);
            double duration = note.StartSeconds - spawn;
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(playingAttack == index ? "정지" : "재생", GUILayout.Width(60)))
            {
                if (playingAttack == index) playingAttack = -1;
                else { playingAttack = index; pathOrigin = EditorApplication.timeSinceStartup - pathScrub * duration; }
            }
            EditorGUI.BeginChangeCheck(); pathScrub = EditorGUILayout.Slider("생성 → 판정", pathScrub, 0, 1);
            if (EditorGUI.EndChangeCheck()) playingAttack = -1;
            EditorGUILayout.EndHorizontal();
            if (playingAttack == index) pathScrub = (float)(((EditorApplication.timeSinceStartup - pathOrigin) / Math.Max(.01, duration)) % 1);
            var display = config != null && config.overrideDisplay ? config.display : null;
            float targetY = note.Step.Kind == GestureKind.Dive ? .8f : note.Step.Kind == GestureKind.Flick ? .12f : .48f;
            var from = new Vector2(3, definition.Grounded ? 0 : targetY) + (display?.sourceOffset ?? Vector2.zero);
            var to = (definition.Grounded ? Vector2.zero : MonsterAttackDisplay.DefaultSocket(note.Step.Kind)) + (display?.targetOffset ?? Vector2.zero);
            const int samples = 96;
            var points = new Vector2[samples + 1]; var frames = new MonsterAttackFrame[samples + 1];
            Vector2 min = Vector2.Min(from, to), max = Vector2.Max(from, to);
            for (int n = 0; n <= samples; n++)
            {
                frames[n] = MonsterAttackTimeline.Evaluate(note, definition, spawn + duration * n / samples, beat, attackPreview.Round.HalfMissWindow);
                points[n] = MonsterAttackPath.Evaluate(config, frames[n], from, to, 1);
                min = Vector2.Min(min, points[n]); max = Vector2.Max(max, points[n]);
            }
            var area = GUILayoutUtility.GetRect(120, 205, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(area, new Color(.09f, .11f, .15f));
            float scale = Mathf.Min((area.width - 86) / Mathf.Max(.5f, max.x - min.x), (area.height - 80) / Mathf.Max(.8f, max.y - min.y));
            Vector2 center = (min + max) / 2;
            Vector3 Map(Vector2 point) => new Vector3(area.center.x + (point.x - center.x) * scale, area.center.y - (point.y - center.y) * scale);
            if (Event.current.type == EventType.Repaint && definition.Motion != MonsterAttackMotion.Materialize)
            {
                var old = Handles.color;
                Handles.color = new Color(1, .4f, .7f, .3f); Handles.DrawAAPolyLine(2, points.Select(Map).ToArray());
                int count = Mathf.Clamp(Mathf.FloorToInt(pathScrub * samples), 0, samples);
                if (count > 0) { Handles.color = new Color(1, .4f, .7f); Handles.DrawAAPolyLine(4, points.Take(count + 1).Select(Map).ToArray()); }
                Handles.color = old;
            }
            var frame = MonsterAttackTimeline.Evaluate(note, definition, spawn + duration * pathScrub, beat, attackPreview.Round.HalfMissWindow);
            Vector3 marker = Map(MonsterAttackPath.Evaluate(config, frame, from, to, 1));
            var sprite = previewSprites.Attack(definition, frame, display?.framesPerBeat ?? 2, beat, out _);
            if (frame.Visible)
            {
                if (sprite != null) DrawSprite(new Rect(marker.x - 25, marker.y - 25, 50, 50), sprite);
                else EditorGUI.DrawRect(new Rect(marker.x - 5, marker.y - 5, 10, 10), Color.white);
            }
            Vector3 a = Map(from), b = Map(to);
            GUI.Label(new Rect(a.x - 25, a.y + 29, 60, 20), "출발", EditorStyles.whiteMiniLabel);
            GUI.Label(new Rect(b.x - 25, b.y + 29, 60, 20), "판정", EditorStyles.whiteMiniLabel);
            GUI.Label(new Rect(area.x + 8, area.y + 6, area.width - 16, 20), frame.Phase + " · " +
                ((spawn + duration * pathScrub - attackPreview.CallSeconds) / beat).ToString("0.##") + "박", EditorStyles.whiteMiniLabel);
            EditorGUILayout.HelpBox("실제 공격 시계와 경로 계산을 사용한 상대 미리보기야. 기준 거리는 플레이어 키의 3배이며 이미지는 확대 표시해. Materialize는 이동 없이 판정 지점에 나타나. 전투 배치·몸체 연결·판정 후 반응은 테스트 장면에서 확인해.", MessageType.None);
        }
        private static void SetPathCurves(SerializedProperty attack, bool arc)
        {
            attack.FindPropertyRelative("progressCurve").animationCurveValue = AnimationCurve.Linear(0, 0, 1, 1);
            float height = attack.FindPropertyRelative("arc").floatValue;
            attack.FindPropertyRelative("heightCurve").animationCurveValue = arc ?
                new AnimationCurve(new Keyframe(0, 0, 0, height * 4), new Keyframe(.5f, height, 0, 0), new Keyframe(1, 0, -height * 4, 0)) :
                AnimationCurve.Linear(0, 0, 1, 0);
        }
    }
}
