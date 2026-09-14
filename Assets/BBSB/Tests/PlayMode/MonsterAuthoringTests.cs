using System;
using System.Collections;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime;
using BBSB.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif
using Object = UnityEngine.Object;

namespace BBSB.Tests
{
    public sealed class MonsterAuthoringTests
    {
        private MonsterAuthoring asset;
        private Texture2D texture;
        private Sprite sprite;
        private GameObject canvas;
        private string controllerPath;
        [SetUp]
        public void SetUp()
        {
            texture = new Texture2D(16, 32);
            sprite = Sprite.Create(texture, new Rect(0, 0, 16, 32), new Vector2(.5f, 0));
            asset = ScriptableObject.CreateInstance<MonsterAuthoring>();
            asset.monsterId = "authored-test-" + Guid.NewGuid().ToString("N"); asset.portrait = sprite;
        }
        [TearDown]
        public void TearDown()
        {
            if (canvas != null) Object.DestroyImmediate(canvas);
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(controllerPath)) AssetDatabase.DeleteAsset(controllerPath);
#endif
            Object.DestroyImmediate(asset); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture);
            MonsterAuthoringRegistry.Reload(Resources.LoadAll<MonsterAuthoring>(MonsterAuthoring.ResourceFolder));
        }
        [Test]
        public void AuthoringConvertsPatternsAndRejectsInvalidTiming()
        {
            var monster = asset.BuildDefinition();
            Assert.AreEqual(asset.monsterId, monster.Id); Assert.AreEqual(4, monster.Patterns[0].Pattern.CueLeadTicks);
            asset.patterns[0].steps[0].kind = GestureKind.Hold;
            Assert.Throws<ArgumentException>(() => asset.BuildDefinition());
            asset.patterns[0].steps[0].durationTicks = 4;
            Assert.AreEqual(4, asset.BuildDefinition().Patterns[0].Pattern.Steps[0].DurationTicks);
            asset.patterns[0].calls[0].offsetTick = 4;
            Assert.Throws<ArgumentException>(() => asset.BuildDefinition());
        }
        [Test]
        public void RegistryLoadsEnabledAssetsAndRemovesDisabledEntries()
        {
            MonsterAuthoringRegistry.Reload(new[] { asset });
            Assert.AreSame(asset, MonsterAuthoringRegistry.Find(asset.monsterId));
            Assert.IsTrue(MonsterCatalog.All.Any(x => x.Id == asset.monsterId));
            asset.includeInEncounters = false; MonsterAuthoringRegistry.Reload(new[] { asset });
            Assert.IsNull(MonsterAuthoringRegistry.Find(asset.monsterId));
            Assert.IsFalse(MonsterCatalog.All.Any(x => x.Id == asset.monsterId));
        }
        [Test]
        public void ExistingSpeciesCanBeEditedDisabledAndReplacedWithoutDefaultCopies()
        {
            asset.monsterId = MonsterCatalog.BuiltIn[0].Id;
            asset.displayName = "편집한 슬라임"; asset.damagePerNote = 17;
            MonsterAuthoringRegistry.Reload(new[] { asset });
            Assert.AreEqual(1, MonsterCatalog.All.Count);
            Assert.AreEqual(asset.displayName, MonsterCatalog.All[0].Name);
            Assert.AreEqual(17, MonsterCatalog.All[0].DamagePerNote);
            Assert.AreSame(asset, MonsterAuthoringRegistry.Find(asset.monsterId));
            asset.includeInEncounters = false; MonsterAuthoringRegistry.Reload(new[] { asset });
            Assert.AreEqual(0, MonsterCatalog.All.Count);
            Assert.IsNull(MonsterAuthoringRegistry.Find(asset.monsterId));
        }
        [Test]
        public void MigratedAssetsPreserveEveryPatternPlannerAndPortrait()
        {
            var entries = Resources.LoadAll<MonsterAuthoring>(MonsterAuthoring.ResourceFolder);
            foreach (var original in MonsterCatalog.BuiltIn)
            {
                var matches = entries.Where(x => x.monsterId == original.Id).ToArray();
                Assert.AreEqual(1, matches.Length, original.Id);
                var authored = matches[0]; var actual = authored.BuildDefinition();
                Assert.IsTrue(authored.includeInEncounters);
                Assert.IsNotNull(authored.portrait);
                Assert.IsTrue(authored.UsesLegacyBodyAnimation);
                Assert.AreEqual(original.Name, actual.Name); Assert.AreEqual(original.Description, actual.Description);
                Assert.AreEqual(original.ArtId, actual.ArtId); Assert.AreEqual(original.MainGesture, actual.MainGesture);
                Assert.AreEqual(original.EncounterWeight, actual.EncounterWeight); Assert.AreEqual(original.DamagePerNote, actual.DamagePerNote);
                Assert.AreEqual(original.PatternPlanner.GetType(), actual.PatternPlanner.GetType());
                if (original.PatternPlanner is BeatShiftPlanner expectedPlanner)
                    Assert.AreEqual(expectedPlanner.SteadyCallsPerPhase, ((BeatShiftPlanner)actual.PatternPlanner).SteadyCallsPerPhase);
                Assert.AreEqual(original.Patterns.Count, actual.Patterns.Count);
                for (int i = 0; i < original.Patterns.Count; i++)
                {
                    var expected = original.Patterns[i]; var pattern = actual.Patterns[i];
                    Assert.AreEqual(expected.Id, pattern.Id); Assert.AreEqual(expected.Name, pattern.Name);
                    Assert.AreEqual(expected.Description, pattern.Description); Assert.AreEqual(expected.Pattern.CueLeadTicks, pattern.Pattern.CueLeadTicks);
                    Assert.AreEqual(expected.ResponseTicks, pattern.ResponseTicks); Assert.AreEqual(expected.RestTicks, pattern.RestTicks);
                    Assert.AreEqual(expected.CueAlignmentTicks, pattern.CueAlignmentTicks); Assert.AreEqual(expected.SilentWaitTicks, pattern.SilentWaitTicks);
                    Assert.AreEqual(expected.ParticipationChance, pattern.ParticipationChance);
                    Assert.AreEqual(expected.Call.Count, pattern.Call.Count); Assert.AreEqual(expected.Pattern.Steps.Count, pattern.Pattern.Steps.Count);
                    for (int c = 0; c < expected.Call.Count; c++)
                    {
                        Assert.AreEqual(expected.Call[c].OffsetTick, pattern.Call[c].OffsetTick);
                        Assert.AreEqual(expected.Call[c].Label, pattern.Call[c].Label);
                        Assert.AreEqual(expected.Call[c].Sound, pattern.Call[c].Sound); Assert.AreEqual(expected.Call[c].Motion, pattern.Call[c].Motion);
                    }
                    for (int n = 0; n < expected.Pattern.Steps.Count; n++)
                    {
                        Assert.AreEqual(expected.Pattern.Steps[n].Kind, pattern.Pattern.Steps[n].Kind);
                        Assert.AreEqual(expected.Pattern.Steps[n].OffsetTick, pattern.Pattern.Steps[n].OffsetTick);
                        Assert.AreEqual(expected.Pattern.Steps[n].DurationTicks, pattern.Pattern.Steps[n].DurationTicks);
                    }
                }
            }
        }
        [Test]
        public void AuthoredControllerOverridesLegacyBodyMode()
        {
            asset.useLegacyBodyAnimation = true; Assert.IsTrue(asset.UsesLegacyBodyAnimation);
            var controller = new AnimatorOverrideController();
            try
            {
                asset.controller = controller; Assert.IsFalse(asset.UsesLegacyBodyAnimation);
                asset.controller = null; Assert.IsTrue(asset.UsesLegacyBodyAnimation);
                asset.useLegacyBodyAnimation = false; Assert.IsFalse(asset.UsesLegacyBodyAnimation);
            }
            finally { Object.DestroyImmediate(controller); }
        }
#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator AnimatorSamplesRegisteredStatesWithoutOwningInputOrTheClock()
        {
            controllerPath = "Assets/BBSB-MonsterAnimator-Test-" + Guid.NewGuid().ToString("N") + ".controller";
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            foreach (var motion in asset.motions)
            {
                var clip = new AnimationClip { name = motion.situation.ToString() };
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Portrait", typeof(Transform), "m_LocalPosition.x"), AnimationCurve.Linear(0, 0, 1, 100));
                AssetDatabase.AddObjectToAsset(clip, controller);
                var state = controller.layers[0].stateMachine.AddState(clip.name); state.motion = clip;
                if (motion.situation == MonsterAnimationSituation.Idle) controller.layers[0].stateMachine.defaultState = state;
            }
            asset.controller = controller;
            canvas = new GameObject("Test canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var root = new GameObject("Visual wrapper", typeof(RectTransform), typeof(MonsterAnimatorView));
            root.transform.SetParent(canvas.transform, false);
            var view = root.GetComponent<MonsterAnimatorView>(); view.Initialize(asset); view.Layout(256);
            yield return null;
            var frame = new MonsterAnimationFrame(MonsterAnimationSituation.Call, .25);
            view.Sample(frame);
            Assert.AreEqual("Base Layer.Call", view.State);
            var image = root.GetComponentInChildren<Image>(); Assert.AreSame(sprite, image.sprite);
            Assert.AreEqual(50, image.transform.localPosition.x, 1);
            yield return null; yield return null;
            Assert.AreEqual(50, image.transform.localPosition.x, 1, "A frozen song must not advance the Animator.");
            view.Sample(new MonsterAnimationFrame(MonsterAnimationSituation.Hit, 0));
            Assert.AreEqual("Base Layer.Hit", view.State); Assert.AreEqual(0, image.transform.localPosition.x, 1);
            view.Sample(frame); Assert.AreEqual(50, image.transform.localPosition.x, 1, "Scrubbing back must reproduce the pose.");
            Assert.IsTrue(root.GetComponentsInChildren<Graphic>().All(x => !x.raycastTarget));
            Assert.IsFalse(root.GetComponent<CanvasGroup>().blocksRaycasts);
            LogAssert.NoUnexpectedReceived();
        }
#endif
    }
}
