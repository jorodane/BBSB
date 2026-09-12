using System;
using System.Collections.Generic;
using BBSB.Runtime.UI;
using UnityEditor;
using UnityEngine;

namespace BBSB.Editor
{
    [CustomEditor(typeof(MonsterAttackDisplay))]
    public sealed class MonsterAttackDisplayEditor : UnityEditor.Editor
    {
        private int selected;
        private bool sockets;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            sockets = EditorGUILayout.Foldout(sockets, "Player contact sockets", true);
            if (sockets) foreach (var name in new[] { "punch", "guard", "head", "feet", "push" })
                EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
            var slots = serializedObject.FindProperty("slots");
            var labels = new string[slots.arraySize];
            for (int i = 0; i < labels.Length; i++)
            {
                var slot = slots.GetArrayElementAtIndex(i);
                labels[i] = slot.FindPropertyRelative("monsterId").stringValue + "/" +
                    slot.FindPropertyRelative("patternId").stringValue + "/step-" + slot.FindPropertyRelative("stepIndex").intValue;
            }
            if (labels.Length > 0)
            {
                selected = EditorGUILayout.Popup("Attack image slot", Mathf.Clamp(selected, 0, labels.Length - 1), labels);
                var slot = slots.GetArrayElementAtIndex(selected);
                EditorGUILayout.SelectableLabel("Assets/BBSB/Resources/" + MonsterAttackDefinition.ResourceRoot + labels[selected],
                    EditorStyles.wordWrappedLabel, GUILayout.Height(44));
                foreach (var name in new[] { "scale", "sourceOffset", "targetOffset", "imageOffset", "framesPerBeat", "poses" })
                    EditorGUILayout.PropertyField(slot.FindPropertyRelative(name), true);
            }
            serializedObject.ApplyModifiedProperties();
            if (GUILayout.Button("Add missing catalog slots")) AddMissing();
        }

        private void AddMissing()
        {
            var display = (MonsterAttackDisplay)target;
            Undo.RecordObject(display, "Add monster attack image slots");
            var slots = new List<MonsterAttackDisplay.Slot>(display.slots ?? Array.Empty<MonsterAttackDisplay.Slot>());
            foreach (var definition in MonsterAttackCatalog.All)
            {
                if (display.Find(definition) != null) continue;
                var poses = new List<MonsterAttackDisplay.Pose>();
                foreach (MonsterAttackPhase phase in Enum.GetValues(typeof(MonsterAttackPhase)))
                    if (phase != MonsterAttackPhase.Hidden) poses.Add(new MonsterAttackDisplay.Pose { phase = phase });
                slots.Add(new MonsterAttackDisplay.Slot { monsterId = definition.MonsterId, patternId = definition.PatternId,
                    stepIndex = definition.StepIndex, poses = poses.ToArray() });
            }
            display.slots = slots.ToArray(); EditorUtility.SetDirty(display); serializedObject.Update();
        }
    }
}
