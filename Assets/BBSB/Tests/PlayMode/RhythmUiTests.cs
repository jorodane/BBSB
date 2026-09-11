using System.Collections;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime;
using BBSB.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BBSB.Tests
{
    public sealed class RhythmUiTests
    {
        private GameObject root;
        private RunPresenter presenter;

        [UnityTearDown]
        public IEnumerator TearDown()
        { if (root != null) Object.Destroy(root); yield return null; }

        [UnityTest]
        public IEnumerator LiveRoundShowsBeatsAndFinishesWithoutClearingOrRerollingStage()
        {
            yield return Prepare(new RunRules(startingHealth: 10000));
            var plan = presenter.Session.BattlePlan; string ticket = presenter.Session.StageTicket;
            decimal health = presenter.Session.Health;
            var player = Begin(); yield return null;
            Assert.IsNotNull(player.Round); Assert.AreSame(plan, player.Round.Plan);
            Assert.IsFalse(presenter.StartRhythmRound(), "A second start cannot replace an active performance.");
            var pulses = root.GetComponentsInChildren<Image>().Where(x => x.name.StartsWith("Beat pulse ")).ToArray();
            Assert.AreEqual(plan.Stage.Music.BeatsPerBar * 2, pulses.Length);
            Assert.IsTrue(pulses.All(x => !x.raycastTarget));
            Assert.AreEqual(1, root.GetComponentsInChildren<Button>().Length, "Only pause is a live action button.");
            Canvas.ForceUpdateCanvases();
            var arena = root.GetComponentInChildren<BattleArenaView>(); Assert.IsNotNull(arena);
            arena.Refresh(); Assert.IsNotNull(arena.HeroPortrait.sprite);
            Assert.AreEqual(plan.Monsters.Count, arena.MonsterPortraits.Count);
            Assert.Greater(((RectTransform)arena.transform).rect.height, 300);
            Assert.IsTrue(arena.GetComponentsInChildren<Graphic>().All(x => !x.raycastTarget));
            Assert.AreEqual(0, root.GetComponentsInChildren<MonsterPatternGraphic>().Length,
                "Live play reserves the space for characters. Detailed patterns are in the menu.");
            var safe = root.GetComponentInChildren<SafeAreaPanel>(); safe.enabled = false;
            foreach (var size in new[] { new Vector2(1280, 720), new Vector2(1280, 800), new Vector2(1220, 680) })
            {
                var viewport = (RectTransform)safe.transform;
                viewport.anchorMin = viewport.anchorMax = new Vector2(.5f, .5f); viewport.sizeDelta = size;
                yield return null; Canvas.ForceUpdateCanvases();
                var scene = (RectTransform)arena.transform;
                Assert.AreEqual(size.y, scene.rect.height, .1f, "Battle fills the viewport behind all HUD elements.");
                Assert.AreEqual(size.x, scene.rect.width, .1f);
                Assert.IsNull(scene.parent.GetComponent<LayoutGroup>());
                var menu = root.GetComponentInChildren<RoundMenuGraphic>().rectTransform;
                Assert.AreSame(scene.parent, menu.parent);
                Assert.AreEqual(80, menu.rect.height, .1f); Assert.AreEqual(80, menu.rect.width, .1f);
                var beats = root.GetComponentsInChildren<RectTransform>().Single(x => x.name == "Beat signals");
                Assert.AreSame(scene.parent, beats.parent);
                Assert.LessOrEqual(beats.rect.height, 60.1f, "Beat indicators are a small overlay.");
                Assert.Less(scene.InverseTransformPoint(arena.HeroPortrait.transform.position).x, 0);
                Assert.IsTrue(arena.MonsterPortraits.All(x => scene.InverseTransformPoint(x.transform.position).x > 0));
                foreach (var target in new[] { (RectTransform)arena.transform,
                    root.GetComponentsInChildren<Text>().Single(x => x.name == "Input status").rectTransform, menu, beats })
                {
                    var corners = new Vector3[4]; target.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var local = viewport.InverseTransformPoint(corner);
                        Assert.That(local.y, Is.InRange(viewport.rect.yMin - 1, viewport.rect.yMax + 1));
                    }
                }
            }
            Click("메뉴"); Click("연주 정보 · 패턴"); yield return null; Canvas.ForceUpdateCanvases();
            var graphics = root.GetComponentsInChildren<MonsterPatternGraphic>();
            Assert.AreEqual(plan.Monsters.Count, graphics.Length);
            foreach (var graphic in graphics)
            {
                var renderer = graphic.GetComponent<CanvasRenderer>(); Assert.IsNotNull(renderer);
                renderer.cull = false; graphic.SetVerticesDirty(); graphic.Rebuild(CanvasUpdate.PreRender);
                Assert.Greater(renderer.GetMesh().vertexCount, 0);
                Assert.IsFalse(graphic.raycastTarget);
            }
            Click("메뉴로 돌아가기"); Click("이어하기");
            Assert.IsFalse(player.IsPaused); Assert.AreSame(arena, root.GetComponentInChildren<BattleArenaView>());
            var performance = player.Round;
            performance.Advance(plan.Stage.Music.DurationSeconds + 1);
            yield return null; yield return null;
            Assert.IsTrue(performance.Finished); Assert.AreEqual(performance.Notes.Count, performance.MissCount);
            Assert.IsNull(presenter.ActiveRound);
            Assert.IsTrue(root.GetComponentsInChildren<Text>().Any(x => x.text == "연주 결과"));
            Assert.AreEqual(RunPhase.Stage, presenter.Session.Phase);
            Assert.AreEqual(ticket, presenter.Session.StageTicket);
            Assert.AreEqual(health - performance.TotalDamageTaken, presenter.Session.Health);
            Click("다시 준비"); yield return null;
            Assert.AreSame(plan, presenter.Session.BattlePlan);
            Assert.IsNotNull(root.GetComponentInChildren<BattleArenaView>());
            Assert.AreEqual(0, root.GetComponentsInChildren<MonsterPatternView>().Length);
            Click("몬스터 패턴"); yield return null;
            Assert.AreEqual(plan.Monsters.Count, root.GetComponentsInChildren<MonsterPatternView>().Length);
            Click("닫기");
            player = Begin(); Assert.AreSame(plan, player.Round.Plan); Assert.AreEqual(0, player.Round.Results.Count);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PauseFreezesClockAndRegrabsOnePointerWithoutAnotherPress()
        {
            yield return Prepare(); var player = Begin(); yield return null;
            var surface = root.GetComponentInChildren<RhythmInputSurface>();
            var first = Pointer(11, new Vector2(100, 200));
            surface.OnPointerDown(first); Assert.IsTrue(player.Round.IsDown);
            surface.OnPointerDown(Pointer(12, new Vector2(200, 300)));
            surface.OnPointerUp(Pointer(12, new Vector2(200, 300)));
            Assert.IsTrue(player.Round.IsDown, "A second finger cannot release the captured contact.");
            Click("메뉴"); double frozen = player.Round.ElapsedSeconds;
            int results = player.Round.Results.Count;
            yield return null; yield return null;
            Assert.AreEqual(frozen, player.Round.ElapsedSeconds);
            Assert.IsTrue(player.IsPaused); Assert.IsFalse(surface.Captured);
            Click("이어하기");
            Assert.IsTrue(player.WaitingForContact); Assert.IsTrue(player.IsPaused);
            Click("메뉴");
            Assert.IsFalse(player.WaitingForContact);
            Assert.IsFalse(player.CanReceiveInput, "An open menu must not regrab a held gesture.");
            Click("조작 방법");
            surface.OnPointerDown(first);
            Assert.IsFalse(surface.Captured);
            yield return null;
            Assert.AreEqual(frozen, player.Round.ElapsedSeconds);
            Assert.AreEqual(results, player.Round.Results.Count);
            Click("메뉴로 돌아가기"); Click("이어하기");
            Assert.IsTrue(player.WaitingForContact);
            surface.OnPointerDown(first);
            Assert.IsFalse(player.IsPaused); Assert.IsTrue(player.Round.IsDown);
            Assert.AreEqual(results, player.Round.Results.Count);
            surface.OnPointerUp(first); Assert.IsFalse(player.Round.IsDown);
            Click("메뉴"); Click("준비로 돌아가기"); yield return null;
            Assert.IsNull(presenter.ActiveRound); Assert.AreEqual(RunPhase.Stage, presenter.Session.Phase);
            Assert.IsTrue(root.GetComponentsInChildren<Text>().Any(x => x.text == "준비하기"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator HiddenPauseMenuReopensAfterManualFocusAndApplicationPause()
        {
            yield return Prepare(new RunRules(startingHealth: 10000));
            var player = Begin(); yield return null;
            var overlay = root.GetComponentsInChildren<RectTransform>(true).Single(x => x.name == "Pause overlay");
            var scroll = overlay.GetComponentInChildren<ScrollRect>(true);
            Assert.IsNotNull(scroll);
            for (int source = 0; source < 4; source++)
            {
                Assert.IsFalse(overlay.gameObject.activeSelf);
                if (source == 0) Click("메뉴");
                else if (source == 1) player.Pause(); // Also used by the Escape key.
                else if (source == 2) player.SendMessage("OnApplicationFocus", false);
                else player.SendMessage("OnApplicationPause", true);
                Assert.IsTrue(player.IsPaused); Assert.IsTrue(overlay.gameObject.activeSelf);
                Assert.IsTrue(root.GetComponentsInChildren<Button>().Any(x => x.name == "Button 이어하기"));
                yield return null; Canvas.ForceUpdateCanvases();
                Assert.AreEqual(1, scroll.verticalNormalizedPosition, .001f);
                Click("조작 방법"); yield return null; Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = 0;
                scroll.velocity = new Vector2(0, 200);
                double frozen = player.Round.ElapsedSeconds;
                yield return null;
                Assert.AreEqual(frozen, player.Round.ElapsedSeconds);
                Click("닫기");
                Assert.IsFalse(player.IsPaused); Assert.IsFalse(overlay.gameObject.activeSelf);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest]
        public IEnumerator ExternalBattleResultStopsTheLiveRoundAndRejectsStaleResults()
        {
            yield return Prepare(); var player = Begin(); yield return null;
            string ticket = presenter.Session.StageTicket;
            Assert.IsTrue(presenter.SubmitBattleResult(ticket, true, presenter.Session.Health));
            Assert.IsTrue(player.Round.Aborted);
            Assert.IsFalse(player.gameObject.activeInHierarchy);
            yield return null;
            Assert.IsNull(presenter.ActiveRound); Assert.AreEqual(RunPhase.Reward, presenter.Session.Phase);
            Assert.IsNull(presenter.Session.BattlePlan);
            Assert.IsFalse(presenter.SubmitBattleResult(ticket, true, presenter.Session.Health));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator HealthHudAndPausePreserveDamageAcrossPreparation()
        {
            yield return Prepare(new RunRules(startingHealth: 10000));
            var player = Begin(); var round = player.Round;
            round.Advance(round.Notes[0].EndSeconds + round.HalfMissWindow + .001);
            Click("메뉴"); decimal remaining = presenter.Session.Health;
            Assert.Less(remaining, 10000m);
            var label = root.GetComponentsInChildren<Text>(true).Single(x => x.name == "Player health");
            Assert.AreEqual("HP  " + remaining.ToString("0.##") + " / 10000", label.text);
            var bar = root.GetComponentsInChildren<RectTransform>(true).Single(x => x.name == "Player health bar");
            var fill = (RectTransform)bar.GetChild(0);
            Assert.AreEqual((float)(remaining / 10000), fill.anchorMax.x, .00001f);
            Assert.IsTrue(bar.GetComponentsInChildren<Graphic>().All(x => !x.raycastTarget));
            Assert.IsFalse(label.raycastTarget);
            yield return null; yield return null;
            Assert.AreEqual(remaining, presenter.Session.Health);
            Click("준비로 돌아가기"); yield return null;
            Assert.AreEqual(remaining, presenter.Session.Health); Assert.IsTrue(round.Aborted);
            round.Advance(10000); Assert.AreEqual(remaining, presenter.Session.Health);
            player = Begin(); Assert.AreEqual(remaining, presenter.Session.Health);
            Assert.AreSame(round.Plan, player.Round.Plan); Assert.AreEqual(0, player.Round.Results.Count);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator LethalDamageEndsPlaybackAndShowsGameOver()
        {
            yield return Prepare(new RunRules(startingHealth: 1));
            var player = Begin(); var round = player.Round;
            round.Advance(round.Plan.Stage.Music.DurationSeconds + 1);
            Assert.IsTrue(round.Aborted); Assert.IsFalse(player.CanReceiveInput);
            Assert.AreEqual(0m, presenter.Session.Health);
            yield return null; yield return null;
            Assert.IsNull(presenter.ActiveRound); Assert.IsNull(presenter.Session.ActiveRhythmRound);
            Assert.AreEqual(RunPhase.GameOver, presenter.Session.Phase);
            Assert.IsTrue(root.GetComponentsInChildren<Text>().Any(x => x.text == "GAME OVER"));
            Assert.IsNull(root.GetComponentInChildren<RhythmPlayback>());
            Assert.IsFalse(presenter.StartRhythmRound());
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator Prepare(RunRules rules = null)
        {
            root = new GameObject("Rhythm UI test");
            if (EventSystem.current == null)
            {
                var events = new GameObject("Test events", typeof(EventSystem)); events.transform.SetParent(root.transform);
            }
            presenter = root.AddComponent<RunPresenter>();
            presenter.Initialize(rules ?? new RunRules(), Resources.Load<Font>("BBSB/Fonts/BBSBUI"), 73, false);
            Click("탐험 시작"); yield return null;
            for (int row = 0; row < FieldMap.StageCount; row++)
            {
                root.GetComponentsInChildren<Button>().First(x => x.interactable && x.GetComponentInChildren<Text>().text.Contains("진입")).onClick.Invoke();
                yield return null;
                if (presenter.Session.CurrentNode.IsBattle) break;
                Click("지도에 돌아가기"); yield return null;
            }
            Assert.IsNotNull(presenter.Session.BattlePlan);
        }

        private RhythmPlayback Begin()
        {
            Click("연주 시작"); var player = root.GetComponentInChildren<RhythmPlayback>();
            player.SetBeatSound(false); return player; // Tests need no audio device/AudioListener.
        }
        private static PointerEventData Pointer(int id, Vector2 position) => new PointerEventData(EventSystem.current)
        { pointerId = id, position = position, button = PointerEventData.InputButton.Left };
        private void Click(string label)
        { root.GetComponentsInChildren<Button>().Single(x => x.IsInteractable() && x.GetComponentInChildren<Text>().text == label).onClick.Invoke(); }
    }
}
