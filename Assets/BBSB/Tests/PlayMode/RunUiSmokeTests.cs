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
        public IEnumerator CodexOpensBeforeARunAndReturnsToTheRunMenu()
        {
            root = new GameObject("Codex UI smoke test");
            var presenter = root.AddComponent<RunPresenter>();
            presenter.Initialize(new RunRules(), Resources.Load<Font>("BBSB/Fonts/BBSBUI"), 73, false);
            Click("몬스터 도감"); yield return null; Canvas.ForceUpdateCanvases();
            Assert.IsNull(presenter.Session);
            var codex = root.GetComponentInChildren<MonsterCodexView>();
            var grid = codex.GetComponentInChildren<GridLayoutGroup>();
            Assert.AreEqual(MonsterCatalog.All.Count, grid.transform.childCount);
            Assert.Greater(grid.cellSize.x, 100);
            codex.GetComponentsInChildren<Button>().Single(x => x.name == "Codex tap-slime").onClick.Invoke();
            yield return null; Canvas.ForceUpdateCanvases();
            Assert.IsNotNull(codex.GetComponentInChildren<MonsterCodexStage>());
            Assert.AreEqual(3, codex.GetComponentsInChildren<Button>().Count(x => x.name == "Codex idle" || x.name.StartsWith("Codex pattern ")));
            Click("소리 켜짐");
            Click(MonsterCatalog.All[0].Patterns[0].Name); yield return null;
            Assert.IsNotNull(codex.GetComponentInChildren<MonsterPatternGraphic>());
            Click("평상시"); Assert.IsNull(codex.GetComponentInChildren<MonsterPatternGraphic>());
            codex.Back(); yield return null; Assert.IsTrue(grid.gameObject.activeInHierarchy);
            codex.Close(); yield return null; Click("탐험 시작"); yield return null;
            var map = presenter.Session.Map;
            Click("메뉴"); Click("몬스터 도감"); yield return null;
            root.GetComponentInChildren<MonsterCodexView>().Close(); yield return null;
            Assert.AreSame(map, presenter.Session.Map); Click("돌아가기");
            LogAssert.NoUnexpectedReceived();
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
            Assert.IsNull(connections.GetComponentInParent<ScrollRect>(), "The map must fit the viewport without scrolling.");
            var renderer = connections.GetComponent<CanvasRenderer>();
            Assert.IsNotNull(renderer, "Runtime-created map connections need a CanvasRenderer.");
            var mesh = renderer.GetMesh();
            Assert.IsNotNull(mesh, "Map connections should submit a mesh to the canvas.");
            Assert.Greater(mesh.vertexCount, 0, "Map connections should contain line geometry.");
            var nodes = root.GetComponentsInChildren<Button>().Where(x => x.GetComponentInChildren<Text>().text.Contains("진입")).ToArray();
            Assert.AreEqual(4, nodes.Length);
            foreach (var node in nodes)
            {
                Assert.IsTrue(node.interactable);
                Assert.IsTrue(node.GetComponentInChildren<Text>().text.Contains("몬스터"));
                Assert.Greater(((RectTransform)node.transform).rect.height, 40);
                Assert.Greater(((RectTransform)node.transform).rect.width, 40);
            }
            nodes[0].onClick.Invoke();
            yield return null;
            Assert.AreEqual(RunPhase.Stage, presenter.Session.Phase);
            Assert.AreEqual(StageKind.Monster, presenter.Session.CurrentNode.Kind);
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
            Click("몬스터 패턴"); yield return null;
            VerifyMonsterCards(plan);
            Click("닫기"); Click("메뉴"); Click("개발 도구");
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
            Assert.AreEqual(0, root.GetComponentsInChildren<MonsterPatternView>().Length,
                "Pattern details should not occupy the preparation screen.");
            Click("클리어 처리");
            yield return null;
            Assert.AreEqual(RunPhase.Reward, presenter.Session.Phase);
            Assert.IsNull(presenter.Session.BattleMusic);
            Assert.IsNull(presenter.Session.BattlePlan);
            Assert.IsFalse(root.GetComponentsInChildren<RectTransform>().Any(x => x.name == "Run menu"));
            var weaponOffer = presenter.Session.Offers.Single(x => x.Content.Kind == RewardKind.Weapon);
            var weaponCard = root.GetComponentsInChildren<Text>().Single(x => x.text == "무기  /  " + weaponOffer.Content.Name).transform.parent;
            weaponCard.GetComponentInChildren<Button>().onClick.Invoke(); yield return null;
            var replacement = root.GetComponentsInChildren<Button>().First(x => x.GetComponentInChildren<Text>().text.EndsWith("  교체"));
            Click("메뉴"); Click("장비 · 가방 · 증강"); Click("닫기"); yield return null;
            Assert.AreEqual(RunPhase.Reward, presenter.Session.Phase);
            Assert.IsTrue(replacement.IsInteractable(), "Checking equipment must retain the pending weapon offer.");
            replacement.onClick.Invoke(); yield return null;
            Assert.AreEqual(weaponOffer.Content.Id, presenter.Session.Weapons[0].DefinitionId);
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
            var plan = presenter.Session.BattlePlan;
            var arena = root.GetComponentInChildren<BattleArenaView>();
            Assert.IsNotNull(arena); Assert.IsNull(presenter.ActiveRound);
            Assert.AreEqual(0, root.GetComponentsInChildren<MonsterPatternView>().Length);
            var safe = root.GetComponentInChildren<SafeAreaPanel>(); safe.enabled = false;
            foreach (var size in new[] { new Vector2(1280, 720), new Vector2(1280, 800), new Vector2(1220, 680) })
            {
                SetViewport(safe, size); yield return null; Canvas.ForceUpdateCanvases();
                var rect = (RectTransform)arena.transform;
                Assert.AreEqual(size.y, rect.rect.height, .1f, "The entire preparation scene must remain behind the HUD.");
                Assert.AreEqual(size.x, rect.rect.width, .1f);
                AssertFloatingMenu(rect);
                AssertContained(rect, (RectTransform)safe.transform);
                var start = root.GetComponentsInChildren<Button>().Single(x => x.GetComponentInChildren<Text>().text == "연주 시작");
                AssertContained((RectTransform)start.transform, (RectTransform)safe.transform);
            }
            Click("몬스터 패턴"); yield return null;
            VerifyMonsterCards(plan);
            Click("닫기");
            Assert.AreSame(arena, root.GetComponentInChildren<BattleArenaView>(), "Closing details must preserve the preparation view.");
            Assert.AreSame(plan, presenter.Session.BattlePlan); Assert.IsNull(presenter.ActiveRound);
            Click("메뉴");
            Assert.IsFalse(root.GetComponentsInChildren<Button>().Any(x => x.GetComponentInChildren<Text>().text == "개발 도구"));
            Assert.IsFalse(root.GetComponentsInChildren<Button>().Any(x => x.GetComponentInChildren<Text>().text == "슬롯 펼치기"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MapUsesViewportAndMenuBlocksNodesWithoutRebuildingTheMap()
        {
            root = new GameObject("Fullscreen map test");
            var presenter = root.AddComponent<RunPresenter>();
            presenter.Initialize(new RunRules(), Resources.Load<Font>("BBSB/Fonts/BBSBUI"), 73, false);
            Click("탐험 시작"); yield return null;
            var safe = root.GetComponentInChildren<SafeAreaPanel>(); safe.enabled = false;
            var map = (RectTransform)root.GetComponentInChildren<MapConnectionsGraphic>().transform.parent;
            var model = presenter.Session.Map;
            foreach (var size in new[] { new Vector2(1280, 720), new Vector2(1280, 800), new Vector2(1220, 680) })
            {
                SetViewport(safe, size); yield return null; Canvas.ForceUpdateCanvases();
                Assert.AreEqual(size.y, map.rect.height, .1f, "The map must fill the viewport behind its HUD.");
                Assert.AreEqual(size.x, map.rect.width, .1f);
                AssertFloatingMenu(map);
                var buttons = map.GetComponentsInChildren<Button>();
                Assert.AreEqual(21, buttons.Length);
                var hud = (RectTransform)map.parent.Find("Status HUD");
                var menu = root.GetComponentInChildren<RoundMenuGraphic>().rectTransform;
                foreach (var button in buttons)
                {
                    AssertContained((RectTransform)button.transform, map);
                    Assert.Greater(((RectTransform)button.transform).rect.height, 70);
                    var bounds = LocalRect((RectTransform)button.transform, map);
                    Assert.IsFalse(bounds.Overlaps(LocalRect(hud, map)), "Nodes must not overlap the status HUD.");
                    Assert.IsFalse(bounds.Overlaps(LocalRect(menu, map)), "Nodes must not overlap the menu.");
                    foreach (var other in buttons.Where(x => x != button))
                        Assert.IsFalse(bounds.Overlaps(LocalRect((RectTransform)other.transform, map)), "Map nodes must not overlap.");
                }
            }
            foreach (var from in model.Nodes)
                foreach (var next in from.Next)
                    Assert.Greater(MapConnectionsGraphic.Position(model.Find(next)).x, MapConnectionsGraphic.Position(from).x,
                        "Every map connection must progress toward the right.");
            var node = map.GetComponentsInChildren<Button>().First(x => x.interactable);
            Click("메뉴"); yield return null;
            Assert.IsFalse(node.IsInteractable(), "The covered map must not accept stage selection.");
            Click("장비 · 가방 · 증강"); yield return null;
            Assert.AreEqual(5, root.GetComponentsInChildren<RectTransform>().Count(x => x.name.StartsWith("Weapon ")));
            Click("닫기"); yield return null;
            Assert.IsTrue(node.IsInteractable());
            Assert.AreSame(model, presenter.Session.Map);
            Assert.AreSame(map, root.GetComponentInChildren<MapConnectionsGraphic>().transform.parent);
            Assert.AreEqual(0, root.GetComponentsInChildren<RectTransform>().Count(x => x.name.StartsWith("Weapon ")));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MysteryMapLabelsRevealOnlyAfterEntryAndRemainKnownOnReturn()
        {
            root = new GameObject("Mystery map test");
            var presenter = root.AddComponent<RunPresenter>();
            presenter.Initialize(new RunRules(), Resources.Load<Font>("BBSB/Fonts/BBSBUI"), 73, false);
            Click("탐험 시작"); yield return null;
            var run = presenter.Session;
            foreach (var hidden in run.Map.Nodes.Where(x => x.IsMystery))
            {
                var label = MapButton(hidden.Id).GetComponentInChildren<Text>().text;
                Assert.AreEqual((hidden.Row + 1).ToString("00") + "  ?", label);
            }
            var target = run.Map.Nodes.First(x => x.IsMystery);
            var opening = run.Map.Nodes.First(x => x.Row == 0 && x.Next.Contains(target.Id));
            MapButton(opening.Id).onClick.Invoke(); yield return null;
            Assert.IsTrue(presenter.SubmitBattleResult(run.StageTicket, true, run.Health)); yield return null;
            Click("보상 건너뛰기"); yield return null;
            Assert.AreEqual("02  ?\n진입", MapButton(target.Id).GetComponentInChildren<Text>().text);
            Click("메뉴"); Click("조작 방법"); Click("닫기"); yield return null;
            Assert.IsFalse(target.IsRevealed);
            StageKind? announced = null;
            presenter.BattleRequested += (ticket, kind, field) => announced = kind;
            MapButton(target.Id).onClick.Invoke(); yield return null;
            Assert.AreEqual(target.Kind, target.MapKind);
            if (target.IsBattle)
            {
                Assert.AreEqual(target.Kind, announced);
                Assert.IsNotNull(root.GetComponentInChildren<BattleArenaView>());
                Assert.IsTrue(presenter.SubmitBattleResult(run.StageTicket, true, run.Health)); yield return null;
                Click("보상 건너뛰기");
            }
            else
            {
                Assert.IsNull(announced);
                Assert.IsTrue(root.GetComponentsInChildren<Text>().Any(x => x.text == ContentCatalog.StageName(target.Kind)));
                Click("지도에 돌아가기");
            }
            yield return null;
            Assert.AreEqual("02  " + ContentCatalog.StageName(target.Kind) + "\n완료", MapButton(target.Id).GetComponentInChildren<Text>().text);
            foreach (var hidden in run.Map.Nodes.Where(x => x.IsMystery && x != target))
                Assert.IsTrue(MapButton(hidden.Id).GetComponentInChildren<Text>().text.Contains("?"));
            LogAssert.NoUnexpectedReceived();
        }

        private Button MapButton(string id)
        { return root.GetComponentsInChildren<Button>().Single(x => x.name == "Stage " + id); }

        private static Rect LocalRect(RectTransform child, RectTransform parent)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            var min = parent.InverseTransformPoint(corners[0]);
            var max = parent.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void SetViewport(SafeAreaPanel safe, Vector2 size)
        {
            var rect = (RectTransform)safe.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition = Vector2.zero;
        }

        private void AssertFloatingMenu(RectTransform scene)
        {
            var graphic = root.GetComponentInChildren<RoundMenuGraphic>(); Assert.IsNotNull(graphic);
            var menu = graphic.rectTransform;
            Assert.AreSame(scene.parent, menu.parent, "HUD and scene must be independent siblings.");
            Assert.IsNull(scene.parent.GetComponent<LayoutGroup>(), "A header row must never allocate scene space.");
            Assert.AreEqual(80, menu.rect.width, .1f); Assert.AreEqual(80, menu.rect.height, .1f);
            AssertContained(menu, (RectTransform)scene.parent);
            var center = RectTransformUtility.WorldToScreenPoint(null, menu.TransformPoint(menu.rect.center));
            var corner = RectTransformUtility.WorldToScreenPoint(null, menu.TransformPoint(menu.rect.max - Vector2.one));
            Assert.IsTrue(graphic.Raycast(center, null));
            Assert.IsFalse(graphic.Raycast(corner, null), "The circular button's empty corners must not block the scene.");
        }

        private static void AssertContained(RectTransform child, RectTransform parent)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var local = parent.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(parent.rect.xMin - 1, parent.rect.xMax + 1));
                Assert.That(local.y, Is.InRange(parent.rect.yMin - 1, parent.rect.yMax + 1));
            }
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
                var graphics = card.GetComponentsInChildren<MonsterPatternGraphic>();
                Assert.AreEqual(monster.Monster.Patterns.Count, graphics.Length);
                foreach (var pattern in monster.Monster.Patterns)
                    Assert.IsTrue(card.GetComponentsInChildren<Text>().Any(x => x.text.Contains(pattern.Name)));
                foreach (var graphic in graphics)
                {
                    var renderer = graphic.GetComponent<CanvasRenderer>();
                    Assert.IsNotNull(renderer);
                    Assert.Greater(graphic.rectTransform.rect.width, 100);
                    renderer.cull = false;
                    graphic.SetVerticesDirty(); graphic.Rebuild(CanvasUpdate.PreRender);
                    Assert.IsNotNull(renderer.GetMesh()); Assert.Greater(renderer.GetMesh().vertexCount, 0);
                }
            }
        }

        private void Click(string label)
        {
            var button = root.GetComponentsInChildren<Button>().Single(x => x.IsInteractable() && x.GetComponentInChildren<Text>().text == label);
            Assert.IsTrue(button.interactable);
            button.onClick.Invoke();
        }
    }
}
