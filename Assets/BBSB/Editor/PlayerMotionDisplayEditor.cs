using BBSB.Runtime.UI;
using UnityEditor;
using UnityEngine;

namespace BBSB.Editor
{
    [CustomEditor(typeof(PlayerMotionDisplay))]
    public sealed class PlayerMotionDisplayEditor : UnityEditor.Editor
    {
        private int sheetIndex, poseIndex;
        private bool showReference = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("groundPosition"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("characterScale"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("squashStretchStrength"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("referencePose"));
            var sheets = serializedObject.FindProperty("sheets");
            if (sheets.arraySize == 0)
            {
                EditorGUILayout.PropertyField(sheets, true);
                serializedObject.ApplyModifiedProperties(); return;
            }
            var names = new string[sheets.arraySize];
            for (int i = 0; i < names.Length; i++) names[i] = sheets.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
            sheetIndex = EditorGUILayout.Popup("Motion sheet", Mathf.Clamp(sheetIndex, 0, names.Length - 1), names);
            var sheet = sheets.GetArrayElementAtIndex(sheetIndex);
            EditorGUILayout.PropertyField(sheet.FindPropertyRelative("scale"), new GUIContent("Sheet scale"));
            var poses = sheet.FindPropertyRelative("poses");
            if (poses.arraySize == 0)
            {
                EditorGUILayout.PropertyField(poses, true);
                serializedObject.ApplyModifiedProperties(); return;
            }
            var labels = new string[poses.arraySize];
            for (int i = 0; i < labels.Length; i++) labels[i] = poses.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
            poseIndex = EditorGUILayout.Popup("Pose", Mathf.Clamp(poseIndex, 0, labels.Length - 1), labels);
            var pose = poses.GetArrayElementAtIndex(poseIndex);
            EditorGUILayout.PropertyField(pose.FindPropertyRelative("scale"), new GUIContent("Pose scale"));
            EditorGUILayout.PropertyField(pose.FindPropertyRelative("offset"), new GUIContent("Ground offset (X / lift)"));
            serializedObject.ApplyModifiedProperties();

            showReference = EditorGUILayout.Toggle("Show idle reference", showReference);
            var display = (PlayerMotionDisplay)target;
            var sprite = FindSprite(names[sheetIndex], poseIndex);
            var reference = display.referencePose != null ? display.referencePose : FindSprite("idle", 0);
            if (sprite == null || reference == null)
            {
                EditorGUILayout.HelpBox("Sprite Mode를 Multiple로 설정하고 시트명_번호(예: idle_0)로 분할해줘.", MessageType.Info);
                return;
            }
            Rect preview = GUILayoutUtility.GetAspectRect(16f / 10f);
            if (Event.current.type == EventType.Repaint) Preview(preview, display, sprite, reference, names[sheetIndex]);
            EditorGUILayout.HelpBox("발 기준은 Sprite Editor의 Custom Pivot에서 조절해. 배율을 바꿔도 그 점은 지면에 고정돼. " +
                "Ground offset의 Y는 점프처럼 의도적으로 지면에서 떨어지는 높이야. 기준 대기 이미지를 겹쳐 보며 크기를 맞출 수 있어.", MessageType.None);
        }

        private static Sprite FindSprite(string sheet, int index)
        {
            foreach (var sprite in Resources.LoadAll<Sprite>(PlayerMotionSprites.ResourcePath + sheet))
                if (sprite.name == sheet + "_" + index) return sprite;
            return null;
        }

        private void Preview(Rect rect, PlayerMotionDisplay display, Sprite sprite, Sprite reference, string sheet)
        {
            if (rect.width <= 0 || rect.height <= 0) return;
            GUI.BeginClip(rect);
            try
            {
                var bounds = new Rect(0, 0, rect.width, rect.height);
                EditorGUI.DrawRect(bounds, new Color(.07f, .09f, .14f));
                var ground = new Vector2(rect.width * display.groundPosition.x, rect.height * (1 - display.groundPosition.y));
                EditorGUI.DrawRect(new Rect(0, ground.y, rect.width, 1), new Color(.5f, .85f, .77f));
                float height = PlayerMotionDisplay.ReferenceDisplayHeight(rect.size, display.characterScale);
                if (showReference) Draw(reference, reference, height, 1, Vector2.zero, ground, .18f);
                display.GetCalibration(sheet, poseIndex, out float scale, out var offset);
                Draw(sprite, reference, height, scale, offset, ground, 1);
                EditorGUI.DrawRect(new Rect(ground.x - 3, ground.y - 3, 6, 6), new Color(1, .8f, .4f));
            }
            finally { GUI.EndClip(); }
        }

        private static void Draw(Sprite sprite, Sprite reference, float height, float scale, Vector2 offset, Vector2 ground, float alpha)
        {
            var size = PlayerMotionDisplay.Measure(sprite, reference, height, scale);
            var pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            var at = ground + new Vector2(offset.x, -offset.y) * height;
            var target = new Rect(at.x - pivot.x * size.x, at.y - (1 - pivot.y) * size.y, size.x, size.y);
            var source = sprite.rect; var texture = sprite.texture;
            var uv = new Rect(source.x / texture.width, source.y / texture.height, source.width / texture.width, source.height / texture.height);
            Color previous = GUI.color;
            try { GUI.color = new Color(1, 1, 1, alpha); GUI.DrawTextureWithTexCoords(target, texture, uv, true); }
            finally { GUI.color = previous; }
        }
    }
}
