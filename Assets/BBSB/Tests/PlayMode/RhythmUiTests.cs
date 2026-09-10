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
            yield return Prepare();
            var plan = presenter.Session.BattlePlan; string ticket = presenter.Session.StageTicket;
            int health = presenter.Session.Health;
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
            foreach (var size in new[] { new Vector2(720, 1280), new Vector2(1280, 720), new Vector2(720, 720) })
            {
                var viewport = (RectTransform)safe.transform;
                viewport.anchorMin = viewport.anchorMax = new Vector2(.5f, .5f); viewport.sizeDelta = size;
                yield return null; Canvas.ForceUpdateCanvases();
                Assert.Greater(((RectTransform)arena.transform).rect.height, size.y * .55f, "Live arena: " + size);
                Assert.Greater(((RectTransform)arena.transform).rect.width, size.x * .9f);
                foreach (var target in new[] { (RectTransform)arena.transform,
                    root.GetComponentsInChildren<Text>().Single(x => x.name == "Input status").rectTransform })
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
            Assert.AreEqual(ticket, presenter.Session.StageTicket); Assert.AreEqual(health, presenter.Session.Health);
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
        public IEnumerator ExternalBattleResultStopsTheLiveRoundAndRejectsStaleResults()
        {
            yield return Prepare(); var player = Begin(); yield return null;
            string ticket = presenter.Session.StageTicket;
            Assert.IsTrue(presenter.SubmitBattleResult(ticket, true, presenter.Session.Health));
            Assert.IsFalse(player.gameObject.activeInHierarchy);
            yield return null;
            Assert.IsNull(presenter.ActiveRound); Assert.AreEqual(RunPhase.Reward, presenter.Session.Phase);
            Assert.IsNull(presenter.Session.BattlePlan);
            Assert.IsFalse(presenter.SubmitBattleResult(ticket, true, presenter.Session.Health));
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator Prepare()
        {
            root = new GameObject("Rhythm UI test");
            if (EventSystem.current == null)
            {
                var events = new GameObject("Test events", typeof(EventSystem)); events.transform.SetParent(root.transform);
            }
            presenter = root.AddComponent<RunPresenter>();
            presenter.Initialize(new RunRules(), Resources.Load<Font>("BBSB/Fonts/BBSBUI"), 73, false);
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
