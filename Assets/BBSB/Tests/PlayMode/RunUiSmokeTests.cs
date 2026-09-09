using System.Collections;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime;
using BBSB.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BBSB.Tests
{
    public sealed class RunUiSmokeTests
    {
        private GameObject root;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BootstrapCreatesKoreanUIAndStartButtonOpensMap()
        {
            root = new GameObject("UI smoke test");
            root.AddComponent<RunBootstrap>();
            yield return null;
            var presenter = root.GetComponent<RunPresenter>();
            Assert.IsNotNull(presenter);
            var font = Resources.Load<Font>("BBSB/Fonts/BBSBUI");
            Assert.IsNotNull(font);
            Assert.IsTrue(font.HasCharacter('탐'));
            var start = root.GetComponentsInChildren<Button>().Single(x => x.GetComponentInChildren<Text>().text == "탐험 시작");
            start.onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(RunPhase.Map, presenter.Session.Phase);
            Assert.AreEqual(5, presenter.Session.Weapons.Count);
            var connections = root.GetComponentsInChildren<MapConnectionsGraphic>().Single();
            var renderer = connections.GetComponent<CanvasRenderer>();
            Assert.IsNotNull(renderer, "Runtime-created map connections need a CanvasRenderer.");
            var mesh = renderer.GetMesh();
            Assert.IsNotNull(mesh, "Map connections should submit a mesh to the canvas.");
            Assert.Greater(mesh.vertexCount, 0, "Map connections should contain line geometry.");
            var nodes = root.GetComponentsInChildren<Button>().Where(x => x.GetComponentInChildren<Text>().text.Contains("진입")).ToArray();
            Assert.AreEqual(3, nodes.Length);
            foreach (var node in nodes)
            {
                Assert.IsTrue(node.interactable);
                Assert.Greater(((RectTransform)node.transform).rect.height, 40);
                Assert.Greater(((RectTransform)node.transform).rect.width, 40);
            }
            nodes[0].onClick.Invoke();
            yield return null;
            Assert.AreEqual(RunPhase.Stage, presenter.Session.Phase);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MusicPreviewBuildsGeometryAndBrowsingDoesNotRerollTheEncounter()
        {
            root = new GameObject("Music UI smoke test");
            var presenter = root.AddComponent<RunPresenter>();
            presenter.Initialize(new RunRules(), Resources.Load<Font>("BBSB/Fonts/BBSBUI"), 73, true);
            MusicStage announced = null;
            BattlePlan announcedPlan = null;
            presenter.BattleRequested += (ticket, kind, field) =>
            { announced = presenter.Session.BattleMusic; announcedPlan = presenter.Session.BattlePlan; };
            Click("탐험 시작");
            yield return null;
            for (int row = 0; row < FieldMap.StageCount; row++)
            {
                root.GetComponentsInChildren<Button>().First(x => x.interactable && x.GetComponentInChildren<Text>().text.Contains("진입")).onClick.Invoke();
                yield return null;
                if (presenter.Session.CurrentNode.IsBattle) break;
                Click("지도에 돌아가기");
                yield return null;
            }
            var encounter = presenter.Session.BattleMusic;
            Assert.IsNotNull(encounter);
            Assert.AreSame(encounter, announced, "Music must exist before the battle event fires.");
            var plan = presenter.Session.BattlePlan;
            Assert.IsNotNull(plan);
            Assert.AreSame(plan, announcedPlan, "Monster plans must exist before the battle event fires.");
            VerifyMonsterCards(plan);
            Click("슬롯 펼치기");
            yield return null;
            Canvas.ForceUpdateCanvases();
            var graphic = root.GetComponentsInChildren<MusicSlotsGraphic>().Single();
            var renderer = graphic.GetComponent<CanvasRenderer>();
            Assert.IsNotNull(renderer);
            Assert.IsFalse(graphic.raycastTarget);
            Assert.Greater(graphic.rectTransform.rect.width, 100);
            // The preview can be below the viewport. Submit one mesh without scroll culling.
            renderer.cull = false;
            graphic.SetVerticesDirty(); graphic.Rebuild(CanvasUpdate.PreRender);
            Assert.IsNotNull(renderer.GetMesh());
            Assert.Greater(renderer.GetMesh().vertexCount, 0);
            Click("다음 곡");
            yield return null;
            Click("다음 마디");
            yield return null;
            Assert.AreSame(encounter, presenter.Session.BattleMusic);
            Assert.AreSame(plan, presenter.Session.BattlePlan);
            VerifyMonsterCards(plan);
            Click("클리어 처리");
            yield return null;
            Assert.AreEqual(RunPhase.Reward, presenter.Session.Phase);
            Assert.IsNull(presenter.Session.BattleMusic);
            Assert.IsNull(presenter.Session.BattlePlan);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PreparationShowsMonstersWithDeveloperControlsDisabled()
        {
            root = new GameObject("Preparation UI smoke test");
            var presenter = root.AddComponent<RunPresenter>();
            presenter.Initialize(new RunRules(), Resources.Load<Font>("BBSB/Fonts/BBSBUI"), 73, false);
            Click("탐험 시작");
            yield return null;
            for (int row = 0; row < FieldMap.StageCount; row++)
            {
                root.GetComponentsInChildren<Button>().First(x => x.interactable && x.GetComponentInChildren<Text>().text.Contains("진입")).onClick.Invoke();
                yield return null;
                if (presenter.Session.CurrentNode.IsBattle) break;
                Click("지도에 돌아가기"); yield return null;
            }
            VerifyMonsterCards(presenter.Session.BattlePlan);
            Assert.IsFalse(root.GetComponentsInChildren<Button>().Any(x => x.GetComponentInChildren<Text>().text == "슬롯 펼치기"));
            LogAssert.NoUnexpectedReceived();
        }

        private void VerifyMonsterCards(BattlePlan plan)
        {
            Assert.IsNotNull(plan);
            Assert.IsTrue(root.GetComponentsInChildren<Text>().Any(x => x.text == "준비하기"));
            Canvas.ForceUpdateCanvases();
            var cards = root.GetComponentsInChildren<MonsterPatternView>();
            Assert.AreEqual(plan.Monsters.Count, cards.Length);
            foreach (var monster in plan.Monsters)
            {
                var card = cards.Single(x => x.Plan.InstanceId == monster.InstanceId);
                Assert.AreSame(monster, card.Plan);
                Assert.IsTrue(card.GetComponentsInChildren<Text>().Any(x => x.text.Contains(monster.Monster.Name)));
                var graphic = card.GetComponentInChildren<MonsterPatternGraphic>();
                Assert.IsNotNull(graphic);
                var renderer = graphic.GetComponent<CanvasRenderer>();
                Assert.IsNotNull(renderer);
                Assert.Greater(graphic.rectTransform.rect.width, 100);
                renderer.cull = false;
                graphic.SetVerticesDirty(); graphic.Rebuild(CanvasUpdate.PreRender);
                Assert.IsNotNull(renderer.GetMesh()); Assert.Greater(renderer.GetMesh().vertexCount, 0);
            }
        }

        private void Click(string label)
        {
            var button = root.GetComponentsInChildren<Button>().Single(x => x.GetComponentInChildren<Text>().text == label);
            Assert.IsTrue(button.interactable);
            button.onClick.Invoke();
        }
    }
}
