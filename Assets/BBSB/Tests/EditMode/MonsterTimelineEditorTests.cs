#if UNITY_EDITOR && !BBSB_STANDALONE
using System.Reflection;
using BBSB.Core;
using BBSB.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BBSB.Tests
{
    // The custom inspector lives in Unity's predefined editor assembly. Resolve it
    // through CreateEditor so the runtime/test asmdefs do not depend on that assembly.
    public sealed class MonsterTimelineEditorTests
    {
        private MonsterAuthoring asset;
        private UnityEditor.Editor editor;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            asset = ScriptableObject.CreateInstance<MonsterAuthoring>();
            editor = UnityEditor.Editor.CreateEditor(asset);
            Assert.AreEqual("BBSB.Editor.MonsterAuthoringEditor", editor.GetType().FullName);
        }
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(editor);
            Undo.ClearUndo(asset);
            Object.DestroyImmediate(asset);
        }
        private SerializedProperty Pattern()
        {
            editor.serializedObject.Update();
            return editor.serializedObject.FindProperty("patterns").GetArrayElementAtIndex(0);
        }
        private void Select(bool call, int index)
        {
            editor.GetType().GetField("selectedCall", PrivateInstance).SetValue(editor, call);
            editor.GetType().GetField("selectedMarker", PrivateInstance).SetValue(editor, index);
        }
        private void Invoke(string name, params object[] args) => editor.GetType().GetMethod(name, PrivateInstance).Invoke(editor, args);
        private void Apply() => editor.serializedObject.ApplyModifiedProperties();

        [Test]
        public void MovingResponseBeforeOriginPreservesOtherAbsoluteTimesAndAttack()
        {
            var heldAttack = new MonsterAuthoring.Attack { enabled = true, resourceFolder = "custom/arrow", spawnOffsetTicks = 1 };
            asset.patterns[0].steps = new[] {
                new MonsterAuthoring.Step { offsetTick = 0 },
                new MonsterAuthoring.Step { offsetTick = 4, attack = heldAttack }
            };
            asset.patterns[0].responseTicks = 8;
            Select(false, 0);
            Invoke("SetMarkerTick", Pattern(), 2); Apply();
            var pattern = asset.patterns[0];
            Assert.AreEqual(2, pattern.cueLeadTicks);
            Assert.AreEqual(2, pattern.cueLeadTicks + pattern.steps[0].offsetTick);
            Assert.AreEqual(8, pattern.cueLeadTicks + pattern.steps[1].offsetTick);
            Assert.AreEqual(12, pattern.cueLeadTicks + pattern.responseTicks);
            Assert.AreEqual("custom/arrow", pattern.steps[1].attack.resourceFolder);
            Assert.AreEqual(1, pattern.steps[1].attack.spawnOffsetTicks);
        }
        [Test]
        public void DeletingCallRemapsReferencesByIdentityAndCanBeUndone()
        {
            var pattern = asset.patterns[0];
            // Authored array order deliberately differs from chronological order.
            pattern.calls = new[] {
                new MonsterAuthoring.Call { offsetTick = 0 },
                new MonsterAuthoring.Call { offsetTick = 6 },
                new MonsterAuthoring.Call { offsetTick = 2 },
                new MonsterAuthoring.Call { offsetTick = 4 }
            };
            pattern.steps = new[] {
                new MonsterAuthoring.Step { attack = new MonsterAuthoring.Attack { enabled = true, callIndex = 1 } },
                new MonsterAuthoring.Step { offsetTick = 1, attack = new MonsterAuthoring.Attack { enabled = true, callIndex = 3 } },
                new MonsterAuthoring.Step { offsetTick = 2, attack = new MonsterAuthoring.Attack { enabled = true, callIndex = 2 } }
            };
            Select(true, 1);
            Undo.IncrementCurrentGroup();
            Invoke("DeleteTimelineMarker", Pattern()); Undo.FlushUndoRecordObjects();
            pattern = asset.patterns[0];
            Assert.AreEqual(3, pattern.calls.Length);
            Assert.AreEqual(4, pattern.calls[pattern.steps[0].attack.callIndex].offsetTick);
            Assert.AreEqual(4, pattern.calls[pattern.steps[1].attack.callIndex].offsetTick);
            Assert.AreEqual(2, pattern.calls[pattern.steps[2].attack.callIndex].offsetTick);
            Undo.PerformUndo();
            pattern = asset.patterns[0];
            Assert.AreEqual(4, pattern.calls.Length);
            Assert.AreEqual(1, pattern.steps[0].attack.callIndex);
            Assert.AreEqual(3, pattern.steps[1].attack.callIndex);
            Assert.AreEqual(2, pattern.steps[2].attack.callIndex);
        }
        [Test]
        public void NewResponseDoesNotClonePreviousAttackAndExtendsItsWindow()
        {
            asset.patterns[0].steps[0].attack = new MonsterAuthoring.Attack {
                enabled = true, resourceFolder = "custom/previous", callIndex = 0
            };
            Invoke("AddTimelineMarker", Pattern(), false, 12);
            var pattern = asset.patterns[0];
            Assert.AreEqual(2, pattern.steps.Length);
            Assert.AreEqual(12, pattern.cueLeadTicks + pattern.steps[1].offsetTick);
            Assert.IsFalse(pattern.steps[1].attack.enabled);
            Assert.IsTrue(string.IsNullOrEmpty(pattern.steps[1].attack.resourceFolder));
            Assert.Greater(pattern.cueLeadTicks + pattern.responseTicks, 12);
            Assert.AreEqual("custom/previous", pattern.steps[0].attack.resourceFolder);
        }
        [Test]
        public void AnchorAndOccupiedTicksCannotBeMovedOrDuplicated()
        {
            asset.patterns[0].calls = new[] { new MonsterAuthoring.Call(), new MonsterAuthoring.Call { offsetTick = 2 } };
            Select(true, 0); Invoke("SetMarkerTick", Pattern(), 1); Apply();
            Invoke("DeleteTimelineMarker", Pattern());
            Assert.AreEqual(2, asset.patterns[0].calls.Length);
            Assert.AreEqual(0, asset.patterns[0].calls[0].offsetTick);
            Select(true, 1); Invoke("SetMarkerTick", Pattern(), 0); Apply();
            Assert.AreEqual(2, asset.patterns[0].calls[1].offsetTick);
            Invoke("AddTimelineMarker", Pattern(), true, 2);
            Assert.AreEqual(2, asset.patterns[0].calls.Length);
        }
        [Test]
        public void DurationEndExpandsWindowAndKeepsExistingSilentWaitAligned()
        {
            var pattern = asset.patterns[0];
            pattern.steps[0].kind = GestureKind.Hold; pattern.steps[0].durationTicks = 4;
            pattern.silentWaitTicks = 4;
            Select(false, 0); Invoke("SetMarkerTick", Pattern(), 6); Apply();
            Invoke("SetDurationEnd", Pattern(), 14); Apply();
            pattern = asset.patterns[0];
            Assert.AreEqual(6, pattern.silentWaitTicks);
            Assert.AreEqual(8, pattern.steps[0].durationTicks);
            Assert.AreEqual(14, pattern.cueLeadTicks + pattern.responseTicks);
            Invoke("SetDurationEnd", Pattern(), 1); Apply();
            Assert.AreEqual(1, asset.patterns[0].steps[0].durationTicks);
        }
    }
}
#endif
