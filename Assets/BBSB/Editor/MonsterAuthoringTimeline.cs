using System;
using System.Collections.Generic;
using BBSB.Core;
using BBSB.Runtime;
using UnityEditor;
using UnityEngine;

namespace BBSB.Editor
{
    // This partial owns only authoring interaction. All times on the strip are
    // absolute ticks from the first Call; serialized Response offsets stay relative.
    public sealed partial class MonsterAuthoringEditor
    {
        private int selectedMarker = -1;
        private bool selectedCall = true;
        private GestureKind newResponseKind = GestureKind.Tap;
        private Vector2 timelineScroll;
        private float pixelsPerBeat = 80;
        private int snapChoice;
        private int dragControl, dragUndoGroup = -1, dragExtent;
        private bool dragEnd;
        private float dragGrabOffset;
        private string timelineNotice;
        private static readonly Color CallColor = new Color(1f, .74f, .25f);
        private static readonly Color ResponseColor = new Color(.27f, .86f, .78f);

        private void OnEnable() => Undo.undoRedoPerformed += OnTimelineUndo;
        private void OnDisable()
        {
            FinishTimelineDrag();
            Undo.undoRedoPerformed -= OnTimelineUndo;
        }
        private void OnTimelineUndo()
        {
            if (dragControl != 0 && GUIUtility.hotControl == dragControl) GUIUtility.hotControl = 0;
            dragControl = 0; dragUndoGroup = -1; selectedMarker = -1;
            validation = null; timelineNotice = null; Repaint();
        }
        private void SelectTimelinePattern(int index)
        {
            if (selectedPattern == index) return;
            FinishTimelineDrag(); selectedPattern = index; selectedMarker = -1;
            timelineScroll = Vector2.zero; timelineNotice = null;
        }
        private void FinishTimelineDrag()
        {
            if (dragControl != 0 && GUIUtility.hotControl == dragControl) GUIUtility.hotControl = 0;
            if (dragUndoGroup >= 0) Undo.CollapseUndoOperations(dragUndoGroup);
            dragControl = 0; dragUndoGroup = -1;
        }
        private static int Tick(SerializedProperty owner, string name) => owner.FindPropertyRelative(name).intValue;
        private static SerializedProperty TimelineRows(SerializedProperty pattern, bool call) => pattern.FindPropertyRelative(call ? "calls" : "steps");
        private static bool Sustained(SerializedProperty row)
        {
            var kind = (GestureKind)row.FindPropertyRelative("kind").enumValueIndex;
            return kind == GestureKind.Hold || kind == GestureKind.Dive;
        }
        private static int AbsoluteTick(SerializedProperty pattern, SerializedProperty row, bool call) =>
            Tick(row, "offsetTick") + (call ? 0 : Tick(pattern, "cueLeadTicks"));
        private static int TimelineExtent(SerializedProperty pattern)
        {
            int end = Tick(pattern, "cueLeadTicks") + Tick(pattern, "responseTicks") + Tick(pattern, "restTicks");
            foreach (bool call in new[] { true, false })
            {
                var rows = TimelineRows(pattern, call);
                for (int i = 0; i < rows.arraySize; i++)
                {
                    var row = rows.GetArrayElementAtIndex(i);
                    end = Mathf.Max(end, AbsoluteTick(pattern, row, call) + (call ? 0 : Tick(row, "durationTicks")));
                }
            }
            return Mathf.Max(16, Mathf.CeilToInt((end + 4) / 4f) * 4);
        }
        private static bool IsAnchor(SerializedProperty pattern, int index)
        {
            var calls = TimelineRows(pattern, true);
            return index >= 0 && index < calls.arraySize && Tick(calls.GetArrayElementAtIndex(index), "offsetTick") == 0;
        }
        private int SnappedTick(float x, float left, float width, int extent)
        {
            int snap = snapChoice == 0 ? 1 : snapChoice == 1 ? 2 : 4;
            return Mathf.Clamp(Mathf.RoundToInt((x - left) / width * extent / snap) * snap, 0, 1024);
        }
        private void DrawPatternTimeline(SerializedProperty pattern)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Call / Response 타임라인", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("막대 위 빈 곳 클릭: Call 추가 · 아래: Response 추가\n마커 선택: 상세 설정 · 좌우 드래그: 박자 이동 · 유지 구간 오른쪽 손잡이: 길이 조절\n모든 눈금은 첫 Call부터의 박자야. 0박 Call은 시작점으로 고정돼.", MessageType.None);
            EditorGUILayout.BeginHorizontal();
            newResponseKind = (GestureKind)EditorGUILayout.EnumPopup("새 Response", newResponseKind);
            snapChoice = EditorGUILayout.Popup(snapChoice, new[] { "0.25박 스냅", "0.5박 스냅", "1박 스냅" }, GUILayout.Width(106));
            EditorGUILayout.EndHorizontal();
            pixelsPerBeat = EditorGUILayout.Slider("확대 (박당 너비)", pixelsPerBeat, 48, 160);

            int extent = dragControl == 0 ? TimelineExtent(pattern) : dragExtent;
            var viewport = GUILayoutUtility.GetRect(100, 166, GUILayout.ExpandWidth(true));
            int control = GUIUtility.GetControlID("MonsterCallResponseTimeline".GetHashCode(), FocusType.Keyboard, viewport);
            var e = Event.current;
            bool insideViewport = viewport.Contains(e.mousePosition);
            float contentWidth = Mathf.Max(viewport.width - 1, extent / 4f * pixelsPerBeat + 48);
            const float left = 24;
            float width = contentWidth - 48;
            bool add = false, delete = false, addCall = false;
            int addTick = 0;
            timelineScroll = GUI.BeginScrollView(viewport, timelineScroll, new Rect(0, 0, contentWidth, 144));
            try
            {
                var bar = new Rect(left, 52, width, 34);
                EditorGUI.DrawRect(new Rect(0, 0, contentWidth, 144), new Color(.105f, .12f, .16f));
                int cue = Tick(pattern, "cueLeadTicks");
                int responseEnd = cue + Tick(pattern, "responseTicks");
                // A shared strip, with stop handles on its upper and lower edges.
                for (int i = 0; i < 64; i++)
                {
                    float t = (i + .5f) / 64;
                    Color tint = t * extent < cue ? CallColor : t * extent < responseEnd ? ResponseColor : Color.gray;
                    EditorGUI.DrawRect(new Rect(bar.x + i * width / 64, bar.y, width / 64 + 1, bar.height),
                        Color.Lerp(new Color(.16f, .18f, .23f), tint, .24f));
                }
                for (int tick = 0; tick <= extent; tick++)
                {
                    float x = left + tick / (float)extent * width;
                    bool beat = tick % 4 == 0;
                    EditorGUI.DrawRect(new Rect(x, bar.y + (beat ? 0 : 23), 1, beat ? 34 : 11),
                        beat ? new Color(1, 1, 1, .28f) : new Color(1, 1, 1, .11f));
                    if (beat) GUI.Label(new Rect(x + 3, bar.y + 3, 46, 20), (tick / 4f).ToString("0.##"), EditorStyles.whiteMiniLabel);
                }
                float originX = left + cue / (float)extent * width;
                EditorGUI.DrawRect(new Rect(originX, bar.y, 2, bar.height), ResponseColor);
                GUI.Label(new Rect(originX + 3, 126, 130, 18), "Response 구간 시작", EditorStyles.whiteMiniLabel);
                DrawTimelineMarkers(pattern, true, left, width, extent);
                DrawTimelineMarkers(pattern, false, left, width, extent);

                // Hit the nearest handle, preferring start handles over duration ends.
                int hit = -1; bool hitCall = e.mousePosition.y < bar.center.y, hitEnd = false;
                float nearest = float.MaxValue;
                var rows = TimelineRows(pattern, hitCall);
                // A selected Hold/Dive end can coincide with the next input's start.
                // Its narrow end grip remains reachable; the next marker's tip still selects that input.
                if (!hitCall && !selectedCall && selectedMarker >= 0 && selectedMarker < rows.arraySize)
                {
                    var selected = rows.GetArrayElementAtIndex(selectedMarker);
                    if (Sustained(selected))
                    {
                        float endX = left + (AbsoluteTick(pattern, selected, false) + Tick(selected, "durationTicks")) / (float)extent * width;
                        if (new Rect(endX - 4, 95, 8, 20).Contains(e.mousePosition))
                        { hit = selectedMarker; hitEnd = true; }
                    }
                }
                for (int pass = 0; pass < 2 && hit < 0; pass++)
                {
                    for (int i = 0; i < rows.arraySize; i++)
                    {
                        var row = rows.GetArrayElementAtIndex(i);
                        if (pass == 1 && (hitCall || !Sustained(row))) continue;
                        int tick = AbsoluteTick(pattern, row, hitCall) + (pass == 1 ? Tick(row, "durationTicks") : 0);
                        float x = left + tick / (float)extent * width;
                        var handle = new Rect(x - 7, hitCall ? 20 : 88, 14, 29);
                        float distance = Mathf.Abs(x - e.mousePosition.x);
                        if (handle.Contains(e.mousePosition) && distance < nearest)
                        { nearest = distance; hit = i; hitEnd = pass == 1; }
                    }
                }
                if (hit >= 0) EditorGUIUtility.AddCursorRect(new Rect(e.mousePosition.x - 7, e.mousePosition.y - 7, 14, 14), MouseCursor.SlideArrow);

                switch (e.GetTypeForControl(control))
                {
                    case EventType.MouseDown:
                        if (e.button != 0 || !insideViewport) break;
                        if (hit < 0 && !(new Rect(left - 7, 16, width + 14, 35).Contains(e.mousePosition) ||
                            new Rect(left - 7, 88, width + 14, 35).Contains(e.mousePosition))) break;
                        GUI.FocusControl(null); EditorGUIUtility.editingTextField = false;
                        GUIUtility.keyboardControl = control;
                        if (hit >= 0)
                        {
                            selectedMarker = hit; selectedCall = hitCall; timelineNotice = null;
                            if (!hitCall || !IsAnchor(pattern, hit))
                            {
                                serializedObject.ApplyModifiedProperties();
                                Undo.IncrementCurrentGroup(); dragUndoGroup = Undo.GetCurrentGroup();
                                Undo.SetCurrentGroupName(hitEnd ? "Resize Response duration" : "Move rhythm marker");
                                dragControl = control; dragEnd = hitEnd; dragExtent = extent;
                                var row = rows.GetArrayElementAtIndex(hit);
                                int tick = AbsoluteTick(pattern, row, hitCall) + (hitEnd ? Tick(row, "durationTicks") : 0);
                                dragGrabOffset = e.mousePosition.x - (left + tick / (float)extent * width);
                                GUIUtility.hotControl = control;
                            }
                        }
                        else
                        {
                            add = true; addCall = hitCall;
                            addTick = SnappedTick(e.mousePosition.x, left, width, extent);
                        }
                        e.Use(); Repaint(); break;
                    case EventType.MouseDrag:
                        if (dragControl != control || GUIUtility.hotControl != control) break;
                        int movedTick = SnappedTick(e.mousePosition.x - dragGrabOffset, left, width, extent);
                        if (dragEnd) SetDurationEnd(pattern, movedTick);
                        else SetMarkerTick(pattern, movedTick);
                        serializedObject.ApplyModifiedProperties(); validation = null;
                        e.Use(); Repaint(); break;
                    case EventType.MouseUp:
                        if (dragControl != control || e.button != 0) break;
                        FinishTimelineDrag(); e.Use(); Repaint(); break;
                    case EventType.KeyDown:
                        if (GUIUtility.keyboardControl != control || EditorGUIUtility.editingTextField) break;
                        if (e.keyCode == KeyCode.Escape && dragControl == control)
                        {
                            int group = dragUndoGroup;
                            GUIUtility.hotControl = 0; dragControl = 0; dragUndoGroup = -1;
                            if (group >= 0) Undo.RevertAllDownToGroup(group);
                            serializedObject.Update(); e.Use(); Repaint();
                        }
                        else if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
                        { delete = true; e.Use(); }
                        break;
                }
            }
            finally { GUI.EndScrollView(); }
            if (add) { AddTimelineMarker(pattern, addCall, addTick); GUIUtility.ExitGUI(); }
            if (delete) { DeleteTimelineMarker(pattern); GUIUtility.ExitGUI(); }
            if (!string.IsNullOrEmpty(timelineNotice)) EditorGUILayout.HelpBox(timelineNotice, MessageType.Info);
        }

        private void DrawTimelineMarkers(SerializedProperty pattern, bool call, float left, float width, int extent)
        {
            var rows = TimelineRows(pattern, call);
            for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < rows.arraySize; i++)
            {
                bool selected = selectedCall == call && selectedMarker == i;
                if (selected != (pass == 1)) continue;
                var row = rows.GetArrayElementAtIndex(i);
                int tick = AbsoluteTick(pattern, row, call);
                float x = left + tick / (float)extent * width;
                Color color = call ? CallColor : ResponseColor;
                if (!call && Sustained(row))
                {
                    float endX = x + Tick(row, "durationTicks") / (float)extent * width;
                    EditorGUI.DrawRect(new Rect(x, 101, Mathf.Max(1, endX - x), 5), new Color(color.r, color.g, color.b, .65f));
                    EditorGUI.DrawRect(new Rect(endX - 3, 95, 6, 19), selected ? Color.white : color);
                }
                var body = new Rect(x - 6, call ? 25 : 98, 12, 13);
                if (selected) EditorGUI.DrawRect(new Rect(body.x - 2, body.y - 2, body.width + 4, body.height + 4), Color.white);
                EditorGUI.DrawRect(body, color);
                if (Event.current.type == EventType.Repaint)
                {
                    var saved = Handles.color; Handles.color = selected ? Color.white : color;
                    Handles.DrawAAConvexPolygon(new Vector3(x - 6, call ? 39 : 97), new Vector3(x + 6, call ? 39 : 97),
                        new Vector3(x, call ? 50 : 88));
                    Handles.color = saved;
                }
                string name = call ? "C" + (i + 1) : "R" + (i + 1) + " " + (GestureKind)row.FindPropertyRelative("kind").enumValueIndex;
                string tooltip = name + " · " + (tick / 4f).ToString("0.##") + "박" + (call && tick == 0 ? " (시작점 고정)" : "");
                GUI.Label(new Rect(x - 7, call ? 7 : 111, Mathf.Max(18, width / extent - 1), 18),
                    new GUIContent((call ? "C" : "R") + (i + 1), tooltip), EditorStyles.whiteMiniLabel);
                GUI.Label(new Rect(x - 7, call ? 20 : 88, 14, 29), new GUIContent("", tooltip));
            }
        }

        private void DrawSelectedMarker(SerializedProperty pattern)
        {
            var rows = TimelineRows(pattern, selectedCall);
            if (selectedMarker < 0 || selectedMarker >= rows.arraySize)
            {
                EditorGUILayout.HelpBox("위쪽 Call 또는 아래쪽 Response 마커를 선택하면 여기에 설정이 열려.", MessageType.Info);
                return;
            }
            var row = rows.GetArrayElementAtIndex(selectedMarker);
            bool anchor = selectedCall && IsAnchor(pattern, selectedMarker);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField((selectedCall ? "Call " : "Response ") + (selectedMarker + 1) + " 설정", EditorStyles.boldLabel);
            bool delete;
            using (new EditorGUI.DisabledScope(anchor)) delete = GUILayout.Button("선택 삭제", GUILayout.Width(78));
            EditorGUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(anchor))
            {
                EditorGUI.BeginChangeCheck();
                float beats = EditorGUILayout.FloatField("첫 Call부터의 시각 (박)", AbsoluteTick(pattern, row, selectedCall) / 4f);
                if (EditorGUI.EndChangeCheck() && !float.IsNaN(beats) && !float.IsInfinity(beats))
                    SetMarkerTick(pattern, Mathf.RoundToInt(Mathf.Clamp(beats, 0, 256) * 4));
            }
            if (selectedCall)
            {
                if (anchor) EditorGUILayout.LabelField("0박 Call은 패턴의 시작점이야.", EditorStyles.miniLabel);
                Field(row, "label", "신호 이름"); Field(row, "sound", "Call 소리"); Field(row, "motion", "기본 Call 동작");
                Field(row, "animatorState", "Animator 상태 (선택)");
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                var kind = row.FindPropertyRelative("kind");
                EditorGUILayout.PropertyField(kind, new GUIContent("입력"));
                bool kindChanged = EditorGUI.EndChangeCheck();
                if (kindChanged) row.FindPropertyRelative("durationTicks").intValue = Sustained(row) ? 4 : 0;
                EditorGUI.BeginChangeCheck();
                if (Sustained(row)) Beats(row, "durationTicks", "유지 길이", 1);
                bool durationChanged = EditorGUI.EndChangeCheck();
                if (kindChanged || durationChanged) EnsureResponseFits(pattern);
                EditorGUILayout.LabelField("Response 구간 안의 상대 시각: " + (Tick(row, "offsetTick") / 4f).ToString("0.##") + "박", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
            if (delete) { DeleteTimelineMarker(pattern); GUIUtility.ExitGUI(); }
            if (!selectedCall) DrawAttack(pattern, row, selectedMarker);
        }

        private void SetMarkerTick(SerializedProperty pattern, int tick)
        {
            var rows = TimelineRows(pattern, selectedCall);
            if (selectedMarker < 0 || selectedMarker >= rows.arraySize || (selectedCall && IsAnchor(pattern, selectedMarker))) return;
            tick = Mathf.Clamp(tick, selectedCall ? 0 : 1, 1024);
            for (int i = 0; i < rows.arraySize; i++)
                if (i != selectedMarker && AbsoluteTick(pattern, rows.GetArrayElementAtIndex(i), selectedCall) == tick)
                { timelineNotice = "같은 줄의 같은 박자에는 이미 마커가 있어."; return; }
            if (!selectedCall) RebaseResponseEarlier(pattern, tick);
            rows.GetArrayElementAtIndex(selectedMarker).FindPropertyRelative("offsetTick").intValue =
                tick - (selectedCall ? 0 : Tick(pattern, "cueLeadTicks"));
            EnsureResponseFits(pattern); UpdateSilentWait(pattern);
            timelineNotice = null;
        }
        private void SetDurationEnd(SerializedProperty pattern, int tick)
        {
            var rows = TimelineRows(pattern, false);
            if (selectedCall || selectedMarker < 0 || selectedMarker >= rows.arraySize) return;
            var row = rows.GetArrayElementAtIndex(selectedMarker);
            if (!Sustained(row)) return;
            row.FindPropertyRelative("durationTicks").intValue = Mathf.Clamp(tick - AbsoluteTick(pattern, row, false), 1, 1024);
            EnsureResponseFits(pattern);
        }
        private static void RebaseResponseEarlier(SerializedProperty pattern, int tick)
        {
            int delta = Tick(pattern, "cueLeadTicks") - tick;
            if (delta <= 0) return;
            pattern.FindPropertyRelative("cueLeadTicks").intValue = tick;
            pattern.FindPropertyRelative("responseTicks").intValue += delta;
            var rows = TimelineRows(pattern, false);
            for (int i = 0; i < rows.arraySize; i++) rows.GetArrayElementAtIndex(i).FindPropertyRelative("offsetTick").intValue += delta;
        }
        private static void EnsureResponseFits(SerializedProperty pattern)
        {
            int end = Mathf.Max(1, Tick(pattern, "responseTicks"));
            var rows = TimelineRows(pattern, false);
            for (int i = 0; i < rows.arraySize; i++)
            {
                var row = rows.GetArrayElementAtIndex(i);
                end = Mathf.Max(end, Tick(row, "offsetTick") + Mathf.Max(1, Tick(row, "durationTicks")));
            }
            pattern.FindPropertyRelative("responseTicks").intValue = end;
        }
        private static void UpdateSilentWait(SerializedProperty pattern)
        {
            if (Tick(pattern, "silentWaitTicks") == 0) return;
            var calls = TimelineRows(pattern, true); var steps = TimelineRows(pattern, false);
            if (calls.arraySize == 0 || steps.arraySize == 0) return;
            int lastCall = 0, firstResponse = int.MaxValue;
            for (int i = 0; i < calls.arraySize; i++) lastCall = Mathf.Max(lastCall, Tick(calls.GetArrayElementAtIndex(i), "offsetTick"));
            for (int i = 0; i < steps.arraySize; i++) firstResponse = Mathf.Min(firstResponse, AbsoluteTick(pattern, steps.GetArrayElementAtIndex(i), false));
            if (firstResponse > lastCall) pattern.FindPropertyRelative("silentWaitTicks").intValue = firstResponse - lastCall;
        }

        private void AddTimelineMarker(SerializedProperty pattern, bool call, int tick)
        {
            FinishTimelineDrag(); selectedCall = call;
            tick = Mathf.Clamp(tick, call ? 0 : 1, 1024);
            var rows = TimelineRows(pattern, call);
            for (int i = 0; i < rows.arraySize; i++)
                if (AbsoluteTick(pattern, rows.GetArrayElementAtIndex(i), call) == tick)
                { selectedMarker = i; Repaint(); return; }
            serializedObject.ApplyModifiedProperties();
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(call ? "Add Call marker" : "Add Response marker");
            Undo.RecordObject(Asset, call ? "Add Call marker" : "Add Response marker");
            var data = Asset.patterns[selectedPattern];
            if (call)
            {
                var list = new List<MonsterAuthoring.Call>(data.calls ?? Array.Empty<MonsterAuthoring.Call>());
                if (list.Count == 0 && tick != 0) list.Add(new MonsterAuthoring.Call());
                list.Add(new MonsterAuthoring.Call { offsetTick = tick });
                data.calls = list.ToArray(); selectedMarker = list.Count - 1;
            }
            else
            {
                int delta = Mathf.Max(0, data.cueLeadTicks - tick);
                data.cueLeadTicks -= delta; data.responseTicks += delta;
                var list = new List<MonsterAuthoring.Step>(data.steps ?? Array.Empty<MonsterAuthoring.Step>());
                foreach (var step in list) step.offsetTick += delta;
                bool sustained = newResponseKind == GestureKind.Hold || newResponseKind == GestureKind.Dive;
                // Construct a fresh entry: never duplicate the previous entry's Sprite/attack settings.
                list.Add(new MonsterAuthoring.Step { kind = newResponseKind, offsetTick = tick - data.cueLeadTicks,
                    durationTicks = sustained ? 4 : 0 });
                data.steps = list.ToArray(); selectedMarker = list.Count - 1;
            }
            EditorUtility.SetDirty(Asset); Undo.FlushUndoRecordObjects(); serializedObject.Update();
            pattern = serializedObject.FindProperty("patterns").GetArrayElementAtIndex(selectedPattern);
            EnsureResponseFits(pattern); UpdateSilentWait(pattern);
            serializedObject.ApplyModifiedProperties(); Undo.CollapseUndoOperations(group);
            validation = null; timelineNotice = null; Repaint();
        }
        private void DeleteTimelineMarker(SerializedProperty pattern)
        {
            FinishTimelineDrag();
            var rows = TimelineRows(pattern, selectedCall);
            if (selectedMarker < 0 || selectedMarker >= rows.arraySize) return;
            if (selectedCall && IsAnchor(pattern, selectedMarker))
            { timelineNotice = "0박 Call은 시작점이므로 삭제할 수 없어."; Repaint(); return; }
            int removed = selectedMarker;
            timelineNotice = null;
            if (selectedCall)
            {
                // Keep array identity: moving a Call does not reorder attack associations.
                // When deleting a referenced Call, use the nearest earlier remaining Call.
                int fallback = -1, bestTick = -1, removedTick = Tick(rows.GetArrayElementAtIndex(removed), "offsetTick");
                for (int i = 0; i < rows.arraySize; i++)
                {
                    int at = Tick(rows.GetArrayElementAtIndex(i), "offsetTick");
                    if (i != removed && at <= removedTick && at > bestTick) { fallback = i; bestTick = at; }
                }
                if (fallback < 0) fallback = removed == 0 ? 1 : 0;
                var steps = TimelineRows(pattern, false);
                bool reassigned = false;
                for (int i = 0; i < steps.arraySize; i++)
                {
                    var attack = steps.GetArrayElementAtIndex(i).FindPropertyRelative("attack");
                    var link = attack.FindPropertyRelative("callIndex");
                    int index = link.intValue;
                    if (index == removed) { index = fallback; reassigned = true; }
                    link.intValue = Mathf.Max(0, index > removed ? index - 1 : index);
                }
                if (reassigned) timelineNotice = "삭제한 Call에 연결된 공격은 앞쪽의 가까운 Call로 옮겼어. 생성 시각을 확인해줘.";
            }
            rows.DeleteArrayElementAtIndex(removed); selectedMarker = -1;
            UpdateSilentWait(pattern); serializedObject.ApplyModifiedProperties();
            validation = null; Repaint();
        }
    }
}
