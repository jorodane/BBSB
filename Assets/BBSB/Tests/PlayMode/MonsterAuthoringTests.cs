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
        public void FormationRoleCanBeOverriddenWithoutChangingPatternArrangement()
        {
            asset.monsterId = "offbeat-goblin";
            Assert.AreEqual(MonsterFormationRole.Trickster, asset.BuildDefinition().FormationRole);
            asset.overrideFormationRole = true; asset.formationRole = MonsterFormationRole.Standard;
            var standard = asset.BuildDefinition();
            Assert.AreEqual(MonsterFormationRole.Standard, standard.FormationRole);
            asset.formationRole = MonsterFormationRole.Trickster;
            var trickster = asset.BuildDefinition();
            Assert.AreEqual(MonsterFormationRole.Trickster, trickster.FormationRole);
            Assert.AreEqual(standard.PatternPlanner.GetType(), trickster.PatternPlanner.GetType());
        }
        [Test]
        public void CustomTrajectoryIsSharedWithPreviewAndSnapshotsCurveKeys()
        {
            var authored = asset.patterns[0].steps[0].attack;
            authored.enabled = true; authored.customTrajectory = true;
            authored.progressCurve = AnimationCurve.Linear(0, 0, 1, 1);
            authored.heightCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.5f, .6f), new Keyframe(1, 0));
            var monster = asset.BuildDefinition(); var preview = new MonsterPreview(monster, monster.Patterns[0]);
            var note = preview.Round.Notes[0]; var definition = MonsterAttackCatalog.For(note);
            var snapshot = MonsterAuthoring.FindAttackArt(definition);
            double spawn = definition.SpawnSeconds(note, preview.BeatSeconds);
            var from = new Vector2(300, 50); var to = new Vector2(0, 50);
            var middle = MonsterAttackTimeline.Evaluate(note, definition, (spawn + note.StartSeconds) / 2, preview.BeatSeconds, preview.Round.HalfMissWindow);
            var point = MonsterAttackPath.Evaluate(snapshot, middle, from, to, 100);
            Assert.AreEqual(150, point.x, .001f); Assert.AreEqual(110, point.y, .001f);
            authored.heightCurve = AnimationCurve.Linear(0, 0, 1, 0);
            Assert.AreEqual(.6f, snapshot.heightCurve.Evaluate(.5f), .001f);
            var contact = MonsterAttackTimeline.Evaluate(note, definition, note.StartSeconds, preview.BeatSeconds, preview.Round.HalfMissWindow);
            Assert.AreEqual(to, MonsterAttackPath.Evaluate(snapshot, contact, from, to, 100));
            snapshot.customTrajectory = false;
            Assert.AreEqual(Vector2.LerpUnclamped(from, to, (float)middle.Progress) + Vector2.up * (float)middle.Lift * 100,
                MonsterAttackPath.Evaluate(snapshot, middle, from, to, 100));
        }
        [Test]
        public void InvalidTrajectoryCannotReplaceAValidAuthoringSnapshot()
        {
            var attack = asset.patterns[0].steps[0].attack;
            attack.enabled = true; attack.customTrajectory = true;
            attack.progressCurve = new AnimationCurve(new Keyframe(.25f, 0), new Keyframe(.75f, 1));
            Assert.Throws<ArgumentException>(() => asset.BuildDefinition());
            attack.progressCurve = AnimationCurve.Linear(0, 0, 1, 1);
            Assert.DoesNotThrow(() => asset.BuildDefinition());
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
        public void AllAuthoredSpeciesBuildWithTheirEditedPatternsAndAttackSettings()
        {
            var entries = Resources.LoadAll<MonsterAuthoring>(MonsterAuthoring.ResourceFolder);
            Assert.GreaterOrEqual(entries.Length, 12);
            foreach (var entry in entries)
            {
                if (!entry.includeInEncounters) continue;
                var definition = entry.BuildDefinition();
                Assert.AreEqual(entry.displayName, definition.Name);
                Assert.AreEqual(entry.patterns.Length, definition.Patterns.Count);
                for (int i = 0; i < definition.Patterns.Count; i++)
                {
                    var pattern = definition.Patterns[i];
                    var preview = new MonsterPreview(definition, pattern);
                    foreach (var note in preview.Round.Notes)
                    {
                        var attack = MonsterAttackCatalog.For(note);
                        Assert.Less(attack.SpawnSeconds(note, preview.Round.BeatSeconds), note.StartSeconds);
                        Assert.AreEqual(definition.Id, attack.MonsterId);
                    }
                }
            }
        }
        [Test]
        public void AuthoringKeepsOffsetCallsAndUsesPerInputArtInAnIsolatedPreview()
        {
            var pattern = asset.patterns[0];
            pattern.cueLeadTicks = 4; pattern.responseTicks = 8;
            pattern.calls = new[] { new MonsterAuthoring.Call { offsetTick = 0 }, new MonsterAuthoring.Call { offsetTick = 4 } };
            pattern.steps = new[] { new MonsterAuthoring.Step { kind = GestureKind.Flick, offsetTick = 2,
                attack = new MonsterAuthoring.Attack { enabled = true, motion = MonsterAttackMotion.WaitRush,
                    callIndex = 1, rushTicks = 2, overrideDisplay = true,
                    images = new[] { new MonsterAuthoring.AttackFrames { phase = MonsterAttackPhase.Travel, frames = new[] { sprite } } } } } };
            var definition = asset.BuildDefinition();
            var preview = new MonsterPreview(definition, definition.Patterns[0]);
            var note = preview.Round.Notes[0]; var attack = MonsterAttackCatalog.For(note);
            Assert.AreEqual(6, note.StartTick - note.Attack.CallStartTick);
            Assert.AreEqual(note.StartSeconds - preview.Round.BeatSeconds * .5, attack.SpawnSeconds(note, preview.Round.BeatSeconds), .000001);
            Assert.AreSame(sprite, MonsterAuthoring.FindAttackArt(attack).SpriteFor(MonsterAttackPhase.Travel, 0));
            pattern.steps[0].attack.height = 5;
            Assert.AreEqual(.3, attack.Height, .00001, "A running plan keeps its original attack configuration.");
            pattern.steps[0].attack.spawnOffsetTicks = 2;
            Assert.Throws<ArgumentException>(() => asset.BuildDefinition(), "Spawning at contact must be rejected.");
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
