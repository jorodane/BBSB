using System.Collections;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime;
using BBSB.Runtime.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BBSB.Tests
{
    public sealed class FiveLanePlaybackTests
    {
        private GameObject root;
        private RunStartPreferenceScope preferences;
        [SetUp] public void IsolatePreferences() => preferences = new RunStartPreferenceScope();
        private PresentationPrefabs catalog;
        private PresentationPrefabs.Screen[] savedScreens;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (catalog != null && savedScreens != null) catalog.screens = savedScreens;
            if (root != null) Object.Destroy(root);
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
                Assert.AreEqual(words[i], hud.beat.text); Assert.IsFalse(playback.CanReceiveInput);
                Assert.AreEqual(0, playback.Battle.Beat); Assert.AreEqual(100, playback.Battle.PlayerHealth);
            }
            playback.Pause(); playback.Continue();
            Assert.IsFalse(playback.CanReceiveInput);
            Assert.Greater((double)clock.GetValue(playback) - AudioSettings.dspTime, 2.9 * 60 / playback.Battle.Bpm);
            clock.SetValue(playback, AudioSettings.dspTime - .01 * 60 / playback.Battle.Bpm);
            playback.SendMessage("LateUpdate");
            Assert.AreEqual("Begin", hud.beat.text); Assert.IsTrue(playback.CanReceiveInput);
            Assert.Greater(playback.Battle.Beat, 0); Assert.Less(playback.Battle.Beat, .5);
            Assert.IsTrue(playback.Battle.Incoming.All(a => a.Beat >= 3));
            yield return null; LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator DefaultBootstrapOpensTwoEquippedLanesAndWaitsForHeldContactOnResume()
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
            Assert.AreEqual(2, playback.GetComponentsInChildren<FiveLaneInputSurface>().Length);
            Assert.IsNotNull(playback.GetComponentInChildren<FiveLaneTrackGraphic>());
            var hud = playback.GetComponentInChildren<FiveLaneHudBindings>();
            Assert.IsNotNull(hud.attackCue);
            for (int i = 2; i < 5; i++)
            {
                Assert.IsFalse(hud.inputAreas[i].gameObject.activeSelf);
                Assert.IsFalse(hud.weaponRoots[i].gameObject.activeSelf);
                Assert.DoesNotThrow(() => playback.SetPointer(i, true));
            }
            var battle = playback.Battle;
            battle.Press(1, 0); battle.Advance(.5);
            playback.Pause(); Assert.IsTrue(battle.IsPaused);
            playback.Continue(); Assert.IsTrue(playback.WaitingForHold); Assert.IsTrue(battle.IsPaused);
            playback.SetPointer(1, true);
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
                Assert.Less(heroRect.xMax, enemyRect.xMin, "The foreground player and enemy stage must remain separate.");
                Assert.Greater(enemyRect.yMin, heroRect.yMin + size.y * .25f);
                Assert.Greater(((RectTransform)hero.transform).rect.height * hero.transform.localScale.y, size.y * .40f);
                Assert.AreEqual(new Vector2(.5f, 0), ((RectTransform)hero.transform).pivot, "Actor feet belong at the stage slot's bottom.");
                for (int i = 0; i < playback.Battle.Lanes.Count; i++)
                {
                    var point = BoundsIn(screen, hud.judgmentPoints[i]);
                    Assert.Greater(point.center.x, heroRect.xMax);
                    Assert.Less(point.yMax, enemyRect.yMin);
                    if (i > 0) Assert.Greater(point.xMin, BoundsIn(screen, hud.judgmentPoints[i - 1]).xMax);
                }
                Assert.Less(BoundsIn(screen, hud.health.rectTransform).yMax, heroRect.yMin);
                Assert.Greater(BoundsIn(screen, hud.enemyFill).yMin, enemyRect.yMax);
            }
            Assert.AreEqual(2, playback.GetComponentsInChildren<WeaponIconGraphic>().Length);
            Assert.AreEqual(1, playback.GetComponentsInChildren<FiveLaneTrackGraphic>().Length);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator WideShieldRequiresBothHeldInputsOnResumeAndShowsIndividualMonsterHealth()
        {
            yield return OpenPreparation();
            var presenter = root.GetComponent<RunPresenter>(); var run = presenter.Session;
            run.Equipment.Acquire(new WeaponState("wide-shield", WeaponRarity.Common));
            Assert.IsTrue(run.EquipWeapon(2, 1, 2));
            Assert.IsTrue(presenter.StartFiveLaneBattle()); yield return null;
            var playback = root.GetComponentInChildren<FiveLanePlayback>(); var battle = playback.Battle;
            var healthLabels = playback.GetComponentsInChildren<TMP_Text>().Where(t => t.name.StartsWith("Monster health ")).ToArray();
            Assert.AreEqual(battle.Formation.Monsters.Count, healthLabels.Length);
            Assert.IsTrue(healthLabels.All(t => t.text.Contains(" / ")));
            battle.Press(1, 0); battle.Press(2, 0); battle.Advance(.5);
            Assert.AreEqual(PhraseLanePhase.Playing, battle.ShieldAt(1).Phase); Assert.AreEqual(PhraseLanePhase.Playing, battle.ShieldAt(2).Phase);
            playback.Pause(); playback.Continue(); Assert.IsTrue(playback.WaitingForHold);
            playback.SetPointer(1, true); Assert.IsTrue(playback.WaitingForHold);
            playback.SetPointer(2, true); Assert.IsFalse(playback.WaitingForHold);
            Assert.IsFalse(battle.IsPaused); Assert.AreEqual(.5, battle.Beat);
            Assert.AreEqual(PhraseLanePhase.Playing, battle.ShieldAt(1).Phase); Assert.AreEqual(PhraseLanePhase.Playing, battle.ShieldAt(2).Phase);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator SparseKBindingUsesKForInputHoldResumeAndHudPosition()
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
            Assert.IsTrue(hud.laneLabels[4].text.StartsWith("K"));
            Assert.Greater(hud.judgmentPoints[4].anchorMin.x, hud.judgmentPoints[2].anchorMax.x);
            var battle = playback.Battle; battle.Press(4, 0); battle.Advance(.5);
            playback.Pause(); playback.Continue(); Assert.IsTrue(playback.WaitingForHold);
            playback.SetPointer(1, true); Assert.IsTrue(playback.WaitingForHold);
            playback.SetPointer(4, true); Assert.IsFalse(playback.WaitingForHold);
            Assert.IsTrue(battle.LaneAt(4).Holding); Assert.AreEqual(4, battle.LaneAt(4).HoldingSlot);
        }

        [UnityTest] public IEnumerator UnlockedRightExtensionBindsLAndResumesItsOwnHoldContact()
        {
            yield return OpenPreparation();
            var presenter = root.GetComponent<RunPresenter>(); var run = presenter.Session;
            run.Equipment.Acquire(new WeaponState("greatsword", WeaponRarity.Common, requiredLanes: 2));
            Assert.IsTrue(run.UnequipWeapon(0)); Assert.AreEqual(InputExtensions.All, run.Equipment.Extensions);
            Assert.IsTrue(run.EquipWeaponAtCenter(2, 4.5)); Assert.IsTrue(presenter.StartFiveLaneBattle()); yield return null;
            var playback = root.GetComponentInChildren<FiveLanePlayback>();
            var hud = playback.GetComponentInChildren<FiveLaneHudBindings>();
            Assert.AreEqual(7, hud.inputAreas.Length); Assert.IsTrue(hud.inputAreas[6].gameObject.activeSelf);
            Assert.IsFalse(hud.inputAreas[5].gameObject.activeSelf); Assert.IsTrue(hud.laneLabels[6].text.StartsWith("L"));
            var battle = playback.Battle; battle.Press(6, 0); battle.Advance(.5);
            playback.Pause(); playback.Continue(); Assert.IsTrue(playback.WaitingForHold);
            playback.SetPointer(4, true); Assert.IsTrue(playback.WaitingForHold);
            playback.SetPointer(6, true); Assert.IsFalse(playback.WaitingForHold); Assert.AreEqual(1, battle.LaneAt(6).StartOffset);
            Assert.IsTrue(battle.LaneAt(6).Holding); LogAssert.NoUnexpectedReceived();
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
                LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("Five-lane playback failed while building the battle HUD and actors"));
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
            Assert.Less(text.anchorMax.y, .2f);
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
