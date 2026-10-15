using System.Collections;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime;
using BBSB.Runtime.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using InputExtensions = BBSB.Core.InputExtensions;

namespace BBSB.Tests
{
    public sealed class FiveLanePlaybackTests
    {
        private GameObject root;
        private GameObject listener;
        private Keyboard keyboard;
        private RunStartPreferenceScope preferences;
        [SetUp] public void IsolatePreferences()
        {
            preferences = new RunStartPreferenceScope();
            listener = new GameObject("Test audio listener", typeof(AudioListener));
        }
        private PresentationPrefabs catalog;
        private PresentationPrefabs.Screen[] savedScreens;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (catalog != null && savedScreens != null) catalog.screens = savedScreens;
            if (root != null) Object.Destroy(root);
            if (listener != null) Object.Destroy(listener);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            preferences.Dispose();
            yield return null;
        }

        [UnityTest] public IEnumerator FourthConfiguredWordSharesTheSongAndInputOriginAndPauseRestartsTheLeadIn()
        {
            root = new GameObject("Word count-in test");
            var presenter = root.AddComponent<RunPresenter>();
            var words = new[] { "Bounce", "Block", "Swing", "Begin" };
            presenter.Initialize(new RunRules(), PresentationFonts.Load(), 31, false, true, words);
            Click("탐험 시작"); Click("편성하고 시작");
            Assert.IsTrue(presenter.Session.Enter(presenter.Session.Map.Nodes.First(n => presenter.Session.CanEnter(n.Id)).Id));
            Assert.IsTrue(presenter.StartFiveLaneBattle());
            var playback = root.GetComponentInChildren<FiveLanePlayback>();
            var hud = playback.GetComponentInChildren<FiveLaneHudBindings>();
            var clock = typeof(FiveLanePlayback).GetField("origin", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            for (int i = 0; i < 3; i++)
            {
                clock.SetValue(playback, AudioSettings.dspTime + (3 - i - .01) * 60 / playback.Battle.Bpm);
                playback.SendMessage("LateUpdate");
                Assert.AreEqual(words[i], hud.countIn.text); Assert.IsFalse(playback.CanReceiveInput);
                Assert.AreEqual(0, playback.Battle.Beat); Assert.AreEqual(100, playback.Battle.PlayerHealth);
            }
            playback.Pause(); playback.Continue();
            Assert.IsFalse(playback.CanReceiveInput);
            Assert.Greater((double)clock.GetValue(playback) - AudioSettings.dspTime, 2.9 * 60 / playback.Battle.Bpm);
            clock.SetValue(playback, AudioSettings.dspTime - .01 * 60 / playback.Battle.Bpm);
            playback.SendMessage("LateUpdate");
            Assert.AreEqual("Begin", hud.countIn.text); Assert.IsTrue(playback.CanReceiveInput);
            Assert.Greater(playback.Battle.Beat, 0); Assert.Less(playback.Battle.Beat, .5);
            Assert.IsTrue(playback.Battle.Incoming.All(a => a.Beat >= 3));
            yield return null; LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator DefaultBootstrapOpensAttackAndFourDefenseInputsAndWaitsForHeldContactOnResume()
        {
            root = new GameObject("Five lane bootstrap"); root.AddComponent<RunBootstrap>();
            yield return null;
            var start = root.GetComponentsInChildren<Button>().First(b =>
                b.GetComponentInChildren<TMP_Text>() != null && b.GetComponentInChildren<TMP_Text>().text == "탐험 시작");
            start.onClick.Invoke(); Click("편성하고 시작");
            var presenter = root.GetComponent<RunPresenter>();
            Assert.IsTrue(presenter.Session.UsesFiveLaneCombat);
            Assert.IsTrue(presenter.Session.Enter(presenter.Session.Map.Nodes.First(n => presenter.Session.CanEnter(n.Id)).Id));
            Assert.IsTrue(presenter.StartFiveLaneBattle());
            var playback = root.GetComponentInChildren<FiveLanePlayback>();
            Assert.IsNotNull(playback);
            Assert.IsTrue(playback.IsInitialized);
            yield return null; // Include a real Update/LateUpdate; constructor-only checks missed the reported failure.
            Assert.IsTrue(playback.Battle.SeparateHands);
            Assert.AreEqual(5, playback.GetComponentsInChildren<FiveLaneInputSurface>().Length);
            Assert.IsNotNull(playback.GetComponentInChildren<FiveLaneTrackGraphic>());
            var hud = playback.GetComponentInChildren<FiveLaneHudBindings>();
            Assert.IsNotNull(hud.attackCue);
            for (int i = 1; i < 4; i++)
            {
                Assert.IsFalse(hud.inputAreas[i].gameObject.activeSelf);
                Assert.IsFalse(hud.weaponRoots[i].gameObject.activeSelf);
                Assert.DoesNotThrow(() => playback.SetPointer(i, true));
            }
            var battle = playback.Battle;
            battle.Press(4, 0); battle.Advance(.5);
            playback.Pause(); Assert.IsTrue(battle.IsPaused);
            playback.Continue(); Assert.IsTrue(playback.WaitingForHold); Assert.IsTrue(battle.IsPaused);
            playback.SetPointer(4, true);
            Assert.IsFalse(playback.WaitingForHold); Assert.IsFalse(battle.IsPaused);
            Assert.IsTrue(battle.Lanes[1].Holding); Assert.AreEqual(.5, battle.Beat);
            Assert.AreSame(battle, presenter.Session.PhraseBattle);
        }

        [UnityTest] public IEnumerator AuthoredImagesDoNotBlockRuntimeMeshesAndActorsKeepTheBattleComposition()
        {
            yield return OpenPreparation();
            var template = UseBattleTemplate();
            template.fiveLane.tracks.gameObject.AddComponent<Image>().color = Color.clear;
            foreach (var slot in template.fiveLane.weaponRoots) slot.gameObject.AddComponent<Image>().color = Color.clear;
            var presenter = root.GetComponent<RunPresenter>();
            Assert.IsTrue(presenter.StartFiveLaneBattle());
            var playback = root.GetComponentInChildren<FiveLanePlayback>();
            var hud = playback.GetComponentInChildren<FiveLaneHudBindings>();
            var screen = (RectTransform)playback.transform;
            screen.anchorMin = screen.anchorMax = new Vector2(.5f, .5f);
            foreach (var size in new[] { new Vector2(1280, 720), new Vector2(1280, 800), new Vector2(1220, 680) })
            {
                screen.sizeDelta = size;
                Canvas.ForceUpdateCanvases();
                yield return null;
                Assert.IsTrue(playback.IsInitialized);
                var hero = hud.playerSlot.GetComponentInChildren<ActorPrefabView>();
                var enemies = hud.monsterArea.GetComponentsInChildren<ActorPrefabView>();
                Assert.AreEqual(presenter.Session.BattlePlan.Monsters.Count, enemies.Length);
                AssertActorVisible(hero);
                foreach (var enemy in enemies) AssertActorVisible(enemy);
                var heroRect = BoundsIn(screen, hud.playerSlot);
                var enemyRect = BoundsIn(screen, hud.monsterArea);
                Assert.AreEqual(size.x * .15f, heroRect.center.x - screen.rect.xMin, 1, "The player stands on the far left.");
                Assert.Greater(enemyRect.xMin, heroRect.xMax);
                var front = BoundsIn(screen, (RectTransform)enemies[0].transform.parent);
                Assert.AreEqual(size.x * .5054f, front.center.x - screen.rect.xMin, 2, "The vanguard occupies the former player position.");
                Assert.Greater(((RectTransform)hero.transform).rect.height * hero.transform.localScale.y, size.y * .22f);
                Assert.AreEqual(new Vector2(.5f, 0), ((RectTransform)hero.transform).pivot, "Actor feet belong at the stage slot's bottom.");
                for (int i = 4; i < 8; i++)
                {
                    var point = BoundsIn(screen, hud.judgmentPoints[i]);
                    Assert.AreEqual(size.y * .42f, point.center.y - screen.rect.yMin, 1);
                    var track = playback.GetComponentInChildren<FiveLaneTrackGraphic>();
                    var arrival = screen.InverseTransformPoint(track.NoteWorldPosition(i, playback.Battle.Beat));
                    var spawn = screen.InverseTransformPoint(track.NoteWorldPosition(i, playback.Battle.Beat + 3));
                    Assert.AreEqual(point.center.y, arrival.y, 1);
                    Assert.Less(spawn.y, arrival.y, "All notes rise from the bottom toward combat.");
                    Assert.AreEqual(track.NoteWidth(i, playback.Battle.Beat + 3), track.NoteWidth(i, playback.Battle.Beat));
                    Assert.AreEqual(playback.Battle.Key(i), hud.laneLabels[i].text);
                    Assert.IsTrue(hud.laneStatus[i] == null || !hud.laneStatus[i].gameObject.activeInHierarchy);
                    Assert.Less(point.yMax, heroRect.yMin);
                    Assert.Less(point.yMax, enemyRect.yMin);
                    if (i > 4) Assert.Greater(point.xMin, BoundsIn(screen, hud.judgmentPoints[i - 1]).xMax);
                }
                Assert.IsFalse(hud.judgmentPoints[0].gameObject.activeInHierarchy);
                Assert.IsEmpty(playback.GetComponentInChildren<FiveLaneTrackGraphic>().NoteTimeline.Notes);
                Assert.Less(BoundsIn(screen, hud.health.rectTransform).yMin, heroRect.yMin);
                Assert.IsTrue(hud.enemyHealth == null || !hud.enemyHealth.gameObject.activeInHierarchy);
                Assert.IsTrue(hud.enemyFill == null || !hud.enemyFill.gameObject.activeInHierarchy);
            }
            Assert.AreEqual(1, hud.actors.Find("Orbiting weapons").GetComponentsInChildren<WeaponIconGraphic>().Length);
            Assert.AreEqual(3, playback.GetComponentsInChildren<WeaponIconGraphic>().Length);
            Assert.AreEqual(1, hud.actors.GetComponentsInChildren<DefenseArtifactGraphic>().Length);
            Assert.AreEqual(1, playback.GetComponentsInChildren<FiveLaneTrackGraphic>().Length);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator KeyboardRoutesAllEightKeysAndKeepsAttackAndDefenseIndependent()
        {
            yield return OpenPreparation();
            var presenter = root.GetComponent<RunPresenter>(); var run = presenter.Session;
            foreach (string id in new[] { "dual-swords", "sword", "hammer" })
            {
                run.Equipment.Acquire(new WeaponState(id));
                Assert.IsTrue(run.EquipWeapon(run.OwnedWeapons.Count - 1, run.OwnedWeapons.Count - 2));
            }
            Assert.IsTrue(presenter.StartFiveLaneBattle());
            var playback = root.GetComponentInChildren<FiveLanePlayback>(); var battle = playback.Battle;
            var clock = typeof(FiveLanePlayback).GetField("origin", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            keyboard = InputSystem.AddDevice<Keyboard>();
            clock.SetValue(playback, AudioSettings.dspTime - .51 * 60 / battle.Bpm);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Z, Key.X, Key.C, Key.V, Key.LeftArrow, Key.DownArrow, Key.UpArrow, Key.RightArrow));
            InputSystem.Update(); playback.SendMessage("Update");
            for (int slot = 0; slot < 8; slot++) Assert.IsTrue(battle.IsInputHeld(slot), battle.Key(slot));
            for (int slot = 0; slot < 4; slot++) Assert.AreEqual("CALL", battle.LaneAt(slot).Feedback);
            for (int slot = 4; slot < 8; slot++) Assert.IsTrue(battle.LaneAt(slot).Holding);
            Assert.AreEqual(12, battle.TotalDamage, "Only the dagger and dual-swords action Calls strike immediately.");
            Assert.IsTrue(battle.LaneAt(0).Phase == PhraseLanePhase.Cooldown);
            Assert.AreEqual(0, battle.LaneAt(2).Activations, "The sword still needs its rhythm response.");
            var artifact = playback.GetComponentInChildren<DefenseArtifactGraphic>();
            Assert.AreEqual(15, artifact.ActiveDirections);
            var orbit = playback.GetComponentInChildren<FiveLaneHudBindings>().actors.Find("Orbiting weapons");
            Assert.AreEqual(4, orbit.GetComponentsInChildren<WeaponIconGraphic>().Length);
            Assert.IsTrue(orbit.GetComponentsInChildren<WeaponIconGraphic>().All(w => w.Kind != WeaponKind.Shield));
            Canvas.ForceUpdateCanvases();
            var staffMesh = artifact.canvasRenderer.GetMesh();
            Assert.Greater(staffMesh.vertexCount, 0, "The held staff must render without an imported sprite.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow));
            InputSystem.Update(); playback.SendMessage("Update");
            double first = battle.LaneAt(2).NextBeat;
            clock.SetValue(playback, AudioSettings.dspTime - first * 60 / battle.Bpm);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Z, Key.X, Key.C, Key.V, Key.UpArrow));
            InputSystem.Update(); playback.SendMessage("Update"); playback.SendMessage("LateUpdate");
            Assert.Greater(battle.TotalDamage, 12);
            Assert.IsTrue(battle.LaneAt(6).Holding);
            Assert.IsFalse(battle.LaneAt(4).Holding);
            Assert.IsEmpty(playback.GetComponentInChildren<FiveLaneTrackGraphic>().NoteTimeline.Notes);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            yield return null;
            string capture = System.Environment.GetEnvironmentVariable("BBSB_CAPTURE_PATH");
            if (!string.IsNullOrEmpty(capture))
            {
                double available = System.Math.Max(battle.Beat, Enumerable.Range(4, 4).Max(s => battle.LaneAt(s).ReadyAtBeat)) + .4;
                clock.SetValue(playback, AudioSettings.dspTime - available * 60 / battle.Bpm);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftArrow, Key.DownArrow, Key.RightArrow));
                InputSystem.Update(); playback.SendMessage("Update"); playback.SendMessage("LateUpdate");
                Assert.AreEqual(11, artifact.ActiveDirections);
                battle.Pause();
                CaptureLayout(capture);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator MultiLineAttackWorkshopUsesOneKeyAndMotionInsteadOfScrollingNotes()
        {
            yield return OpenPreparation();
            var presenter = root.GetComponent<RunPresenter>(); var run = presenter.Session;
            run.Equipment.Acquire(new WeaponState("staff")); Assert.IsTrue(run.EquipWeapon(2, 2));
            presenter.SendMessage("Render"); Click("노트 조립"); yield return null;
            Click("›"); Click("›"); yield return null;
            var demo = root.GetComponentInChildren<NoteWorkshopDemoView>();
            Assert.AreEqual(1, demo.Playback.Width);
            Assert.AreEqual("staff", demo.Playback.Phrase.WeaponId);
            typeof(NoteWorkshopDemoView).GetField("beat", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(demo, 2d);
            demo.SendMessage("Update");
            Assert.IsNotNull(demo.transform.Find("Upward note example/Weapon rhythm example"));
            Assert.IsFalse(demo.GetComponentsInChildren<RectTransform>().Any(t => t.name == "Demo note" || t.name == "Judgment line"));
            Assert.IsTrue(demo.GetComponentsInChildren<TMP_Text>().Any(t => t.text.StartsWith("C\n")));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator WideShieldRequiresBothHeldInputsOnResumeAndShowsIndividualMonsterHealth()
        {
            yield return OpenPreparation();
            var presenter = root.GetComponent<RunPresenter>(); var run = presenter.Session;
            run.Equipment.Acquire(new WeaponState("wide-shield", WeaponRarity.Common));
            Assert.IsTrue(run.EquipWeapon(2, 4));
            Assert.IsTrue(presenter.StartFiveLaneBattle()); yield return null;
            var playback = root.GetComponentInChildren<FiveLanePlayback>(); var battle = playback.Battle;
            var hud = playback.GetComponentInChildren<FiveLaneHudBindings>();
            Assert.AreEqual(battle.Lanes.Count - 1, hud.actors.Find("Orbiting weapons").GetComponentsInChildren<WeaponIconGraphic>().Length,
                "The one defense artifact stays out of the offensive orbit.");
            Assert.AreEqual(1, hud.actors.GetComponentsInChildren<DefenseArtifactGraphic>().Length);
            var healthLabels = playback.GetComponentsInChildren<TMP_Text>().Where(t => t.name.StartsWith("Monster health ")).ToArray();
            Assert.AreEqual(battle.Formation.Monsters.Count, healthLabels.Length);
            Assert.IsTrue(healthLabels.All(t => t.text.Contains(" / ")));
            battle.Press(4, 0); battle.Press(7, 0); battle.Advance(.5);
            Assert.IsTrue(battle.LaneAt(4).Holding); Assert.IsTrue(battle.LaneAt(7).Holding);
            playback.Pause(); playback.Continue(); Assert.IsTrue(playback.WaitingForHold);
            playback.SetPointer(4, true); Assert.IsTrue(playback.WaitingForHold);
            playback.SetPointer(7, true); Assert.IsFalse(playback.WaitingForHold);
            Assert.IsFalse(battle.IsPaused); Assert.AreEqual(.5, battle.Beat);
            Assert.IsTrue(battle.LaneAt(4).Holding); Assert.IsTrue(battle.LaneAt(7).Holding);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator SparseAttackBindingKeepsDefenseInputAndHoldResumeSeparate()
        {
            yield return OpenPreparation();
            var presenter = root.GetComponent<RunPresenter>();
            Assert.IsTrue(presenter.Session.EquipWeapon(0, 2));
            Assert.IsTrue(presenter.Session.EquipWeapon(1, 4));
            Assert.IsTrue(presenter.StartFiveLaneBattle()); yield return null;
            var playback = root.GetComponentInChildren<FiveLanePlayback>();
            var hud = playback.GetComponentInChildren<FiveLaneHudBindings>();
            Assert.IsFalse(hud.inputAreas[0].gameObject.activeSelf);
            Assert.IsFalse(hud.inputAreas[1].gameObject.activeSelf);
            Assert.IsTrue(hud.inputAreas[2].gameObject.activeSelf); Assert.IsTrue(hud.inputAreas[4].gameObject.activeSelf);
            Assert.AreEqual("C", hud.laneLabels[2].text); Assert.AreEqual("←", hud.laneLabels[4].text);
            Assert.Greater(hud.judgmentPoints[4].anchorMin.x, hud.judgmentPoints[2].anchorMax.x);
            var battle = playback.Battle; battle.Press(4, 0); battle.Advance(.5);
            playback.Pause(); playback.Continue(); Assert.IsTrue(playback.WaitingForHold);
            playback.SetPointer(1, true); Assert.IsTrue(playback.WaitingForHold);
            playback.SetPointer(4, true); Assert.IsFalse(playback.WaitingForHold);
            Assert.IsTrue(battle.LaneAt(4).Holding); Assert.AreEqual(4, battle.LaneAt(4).HoldingSlot);
        }

        [UnityTest] public IEnumerator MultiLineWeaponUsesOneAttackKeyAndResumesAlongsideRightArrow()
        {
            yield return OpenPreparation();
            var presenter = root.GetComponent<RunPresenter>(); var run = presenter.Session;
            run.Equipment.Acquire(new WeaponState("greatsword", WeaponRarity.Common, requiredLanes: 2));
            Assert.IsTrue(run.UnequipWeapon(0)); Assert.AreEqual(InputExtensions.All, run.Equipment.Extensions);
            Assert.IsTrue(run.EquipWeaponAtCenter(2, 3)); Assert.IsTrue(presenter.StartFiveLaneBattle()); yield return null;
            var playback = root.GetComponentInChildren<FiveLanePlayback>();
            var hud = playback.GetComponentInChildren<FiveLaneHudBindings>();
            Assert.AreEqual(8, hud.inputAreas.Length); Assert.IsTrue(hud.inputAreas[7].gameObject.activeSelf);
            Assert.IsTrue(hud.inputAreas[3].gameObject.activeSelf); Assert.AreEqual("V", hud.laneLabels[3].text);
            var battle = playback.Battle; battle.Press(3, 0); battle.Release(3, 0);
            battle.Press(3, battle.LaneAt(3).NextBeat); battle.Press(7, battle.Beat);
            Assert.IsTrue(battle.LaneAt(3).Holding);
            playback.Pause(); playback.Continue(); Assert.IsTrue(playback.WaitingForHold);
            playback.SetPointer(3, true); Assert.IsTrue(playback.WaitingForHold);
            playback.SetPointer(7, true); Assert.IsFalse(playback.WaitingForHold); Assert.AreEqual(0, battle.LaneAt(3).StartOffset);
            Assert.IsTrue(battle.LaneAt(3).Holding); Assert.IsTrue(battle.LaneAt(7).Holding); LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator IncompleteHudRecoversWithTheBuiltInStageInsteadOfLosingActors()
        {
            yield return OpenPreparation();
            var template = UseBattleTemplate();
            template.fiveLane.weaponRoots = null;
            Assert.IsFalse(template.fiveLane.TryValidate(out var problem));
            LogAssert.Expect(LogType.Warning, problem + " Using the built-in battle layout for this instance.");
            var presenter = root.GetComponent<RunPresenter>();
            Assert.IsTrue(presenter.StartFiveLaneBattle());
            yield return null;
            var playback = root.GetComponentInChildren<FiveLanePlayback>();
            Assert.IsTrue(playback.IsInitialized);
            var hud = playback.GetComponentInChildren<FiveLaneHudBindings>();
            Assert.IsTrue(hud.TryValidate(out _));
            AssertActorVisible(hud.playerSlot.GetComponentInChildren<ActorPrefabView>());
            Assert.AreEqual(presenter.Session.BattlePlan.Monsters.Count, hud.monsterArea.GetComponentsInChildren<ActorPrefabView>().Length);
            Assert.IsNull(template.fiveLane.weaponRoots, "Runtime recovery must not change the source prefab.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator FailedActorSetupPausesOnceAndCanReturnToPreparationAndRetry()
        {
            yield return OpenPreparation();
            var hero = Resources.Load<PlayerAuthoring>(PlayerAuthoring.ResourcePath);
            Assert.IsNotNull(hero);
            var original = hero.visualPrefab;
            var invalid = new GameObject("Invalid actor without a renderer"); invalid.transform.SetParent(root.transform, false);
            try
            {
                hero.visualPrefab = invalid;
                var presenter = root.GetComponent<RunPresenter>();
                LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("ArgumentException: 외형 프리팹"));
                Assert.IsFalse(presenter.StartFiveLaneBattle());
                var playback = root.GetComponentInChildren<FiveLanePlayback>();
                var battle = playback.Battle;
                Assert.IsFalse(playback.IsInitialized); Assert.IsFalse(playback.enabled);
                Assert.IsTrue(battle.IsPaused); Assert.IsFalse(playback.CanReceiveInput);
                double beat = battle.Beat;
                decimal health = battle.PlayerHealth;
                playback.SetPointer(0, true); playback.Pause(); playback.Continue();
                yield return null; yield return null;
                Assert.AreEqual(beat, battle.Beat); Assert.AreEqual(health, battle.PlayerHealth);
                hero.visualPrefab = original;
                Click("준비 화면으로");
                Assert.IsNull(presenter.ActiveFiveLaneBattle);
                Assert.AreSame(battle, presenter.Session.PhraseBattle);
                Assert.IsTrue(presenter.StartFiveLaneBattle(), "A failed Render must release its re-entry guard.");
                yield return null;
                var retry = root.GetComponentInChildren<FiveLanePlayback>();
                Assert.IsTrue(retry.IsInitialized); Assert.AreSame(battle, retry.Battle);
                Assert.IsNotNull(retry.GetComponentInChildren<ActorPrefabView>());
                LogAssert.NoUnexpectedReceived();
            }
            finally { hero.visualPrefab = original; }
        }

        [Test] public void LayoutMigrationMovesUntouchedDefaultsAndKeepsAuthoredPositions()
        {
            root = new GameObject("Legacy HUD", typeof(RectTransform));
            var hud = FiveLaneHudBindings.CreateDefault((RectTransform)root.transform, PresentationFonts.Load());
            typeof(FiveLaneHudBindings).GetField("stageLayoutVersion", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(hud, 0);
            var text = hud.health.rectTransform;
            text.anchorMin = new Vector2(.02f, .93f); text.anchorMax = new Vector2(.28f, .99f);
            text.offsetMin = text.offsetMax = Vector2.zero;
            var custom = hud.weaponRoots[0]; custom.anchorMin = new Vector2(.11f, .21f); custom.offsetMin = new Vector2(13, 17);
            var min = custom.anchorMin; var offset = custom.offsetMin;
            Assert.IsTrue(hud.EnsureStageLayout());
            Assert.Less(text.anchorMax.y, .55f); Assert.Greater(text.anchorMin.y, .45f);
            Assert.Less(hud.playerSlot.anchorMax.x, hud.monsterArea.anchorMin.x);
            Assert.AreEqual(new Vector2(.38f, .54f), hud.monsterArea.anchorMin);
            Assert.AreEqual(min, custom.anchorMin); Assert.AreEqual(offset, custom.offsetMin);
            Assert.IsFalse(hud.EnsureStageLayout(), "Migration must be idempotent.");
        }

        private IEnumerator OpenPreparation()
        {
            root = new GameObject("Five lane bootstrap"); root.AddComponent<RunBootstrap>();
            yield return null;
            Click("탐험 시작"); Click("편성하고 시작");
            var session = root.GetComponent<RunPresenter>().Session;
            Assert.IsTrue(session.Enter(session.Map.Nodes.First(n => session.CanEnter(n.Id)).Id));
        }
        private void Click(string label) => root.GetComponentsInChildren<Button>().First(b =>
            b.GetComponentInChildren<TMP_Text>() != null && b.GetComponentInChildren<TMP_Text>().text == label).onClick.Invoke();
        private void CaptureLayout(string path)
        {
            // Optional local render for visual review. Normal test runs create no files.
            var canvas = root.GetComponentInChildren<Canvas>();
            var cameraObject = new GameObject("Layout capture camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var texture = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previousTarget = RenderTexture.active; var previousMode = canvas.renderMode; var previousCamera = canvas.worldCamera;
            try
            {
                camera.targetTexture = texture;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                // Recompute pixel-positioned actors and weapons at the capture size.
                // Otherwise a resized Canvas keeps the previous Game view's offsets.
                root.GetComponentInChildren<FiveLanePlayback>().SendMessage("LateUpdate");
                Canvas.ForceUpdateCanvases();
                if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null) camera.Render();
                else UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                    new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = texture });
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                System.IO.File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = previousMode; canvas.worldCamera = previousCamera; RenderTexture.active = previousTarget;
                texture.Release(); Object.Destroy(texture); Object.Destroy(pixels); Object.Destroy(cameraObject);
            }
        }
        private CanvasScreen UseBattleTemplate()
        {
            catalog = Resources.Load<PresentationPrefabs>(PresentationPrefabs.ResourcePath); savedScreens = catalog.screens;
            var source = new GameObject("Authored battle template", typeof(RectTransform), typeof(CanvasScreen));
            source.transform.SetParent(root.transform, false);
            var template = source.GetComponent<CanvasScreen>();
            template.content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            template.content.SetParent(source.transform, false);
            template.content.anchorMin = Vector2.zero; template.content.anchorMax = Vector2.one;
            template.content.offsetMin = template.content.offsetMax = Vector2.zero;
            template.fiveLane = FiveLaneHudBindings.CreateDefault(template.content, PresentationFonts.Load());
            catalog.screens = savedScreens.Where(s => s.kind != RunScreenKind.FiveLaneBattle).Concat(new[] {
                new PresentationPrefabs.Screen { kind = RunScreenKind.FiveLaneBattle, prefab = template } }).ToArray();
            return template;
        }
        private static Rect BoundsIn(RectTransform parent, RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var min = parent.InverseTransformPoint(corners[0]); var max = parent.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static void AssertActorVisible(ActorPrefabView actor)
        {
            Assert.IsNotNull(actor); Assert.IsTrue(actor.gameObject.activeInHierarchy);
            Assert.Greater(actor.transform.localScale.x, 0);
            bool sprite = actor.GetComponentsInChildren<SpriteCanvasGraphic>().Any(g => g.enabled && g.Source != null && g.Source.sprite != null);
            bool image = actor.GetComponentsInChildren<Image>().Any(g => g.enabled && g.sprite != null);
            Assert.IsTrue(sprite || image, actor.name + " has no visible artwork.");
            foreach (var graphic in actor.GetComponentsInChildren<SpriteCanvasGraphic>())
            {
                Assert.IsNotNull(graphic.canvasRenderer, "Canvas meshes need a live CanvasRenderer.");
                graphic.Rebuild(CanvasUpdate.PreRender);
                var mesh = graphic.canvasRenderer.GetMesh();
                Assert.IsNotNull(mesh);
                Assert.Greater(mesh.vertexCount, 0, "The sprite must submit geometry to the Canvas.");
            }
        }
    }
}
