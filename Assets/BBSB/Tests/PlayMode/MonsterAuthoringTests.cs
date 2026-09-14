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
