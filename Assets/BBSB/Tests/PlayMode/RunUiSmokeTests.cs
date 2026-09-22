using TMPro;
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
        private RunStartPreferenceScope preferences;
        [SetUp] public void IsolatePreferences() => preferences = new RunStartPreferenceScope();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            preferences.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShopPurchasesKeepTheSameButtonsScrollAndExitUntilExplicitReturn()
        {
            int seed = Enumerable.Range(0, 100).First(s => MapGenerator.Generate(1, new SeededRandom(s)).Nodes.Any(n => n.Row == 1 && n.Kind == StageKind.Shop));
            root = new GameObject("Shop repeated purchase test");
            var presenter = root.AddComponent<RunPresenter>();
            presenter.Initialize(new RunRules(startingGold: 1000), PresentationFonts.Load(), seed, false, true);
            Click("탐험 시작"); Click("편성하고 시작");
            var run = presenter.Session;
            var shop = run.Map.Nodes.First(n => n.Row == 1 && n.Kind == StageKind.Shop);
            Assert.IsTrue(run.Enter(run.Map.Nodes.First(n => n.Row == 0 && n.Next.Contains(shop.Id)).Id));
            Assert.IsTrue(run.ResolveBattle(run.StageTicket, true, run.Health)); Assert.IsTrue(run.SkipReward());
            Assert.IsTrue(run.Enter(shop.Id)); presenter.SendMessage("Render");
            yield return null; Canvas.ForceUpdateCanvases();
            var buttons = root.GetComponentsInChildren<Button>().Where(b => b.GetComponentInChildren<TMP_Text>()?.text.Contains("G  ·  구매") == true).ToArray();
            Assert.AreEqual(4, buttons.Length);
            var exit = root.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<TMP_Text>()?.text == "지도에 돌아가기");
            var scroll = buttons[0].GetComponentInParent<ScrollRect>();
            scroll.verticalNormalizedPosition = .3f; Canvas.ForceUpdateCanvases();
            var position = scroll.content.anchoredPosition;
            int cleared = run.ClearedStages;
            for (int i = 0; i < buttons.Length; i++)
            {
                Assert.AreEqual(Navigation.Mode.None, buttons[i].navigation.mode);
                int gold = run.Gold - run.Offers[i].Price;
                buttons[i].onClick.Invoke(); buttons[i].onClick.Invoke();
                yield return null; Canvas.ForceUpdateCanvases();
                Assert.AreEqual(RunPhase.Stage, run.Phase); Assert.AreSame(shop, run.CurrentNode);
                Assert.AreEqual(cleared, run.ClearedStages); Assert.AreEqual(gold, run.Gold);
                Assert.AreEqual("구매 완료", buttons[i].GetComponentInChildren<TMP_Text>().text);
                Assert.IsFalse(buttons[i].interactable); Assert.IsTrue(exit.gameObject.activeInHierarchy);
                Assert.AreSame(exit, root.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<TMP_Text>()?.text == "지도에 돌아가기"));
                Assert.AreEqual(position.y, scroll.content.anchoredPosition.y, .1f);
                Assert.IsTrue(root.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("·  " + gold + " G")));
            }
            exit.onClick.Invoke(); yield return null;
            Assert.AreEqual(RunPhase.Map, run.Phase); Assert.AreEqual(cleared + 1, run.ClearedStages);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NoteWorkshopEquipsAndRemovesARewardWithoutDeletingTheBasePattern()
        {
            root = new GameObject("Note workshop UI smoke test");
            var presenter = root.AddComponent<RunPresenter>();
            presenter.Initialize(new RunRules(), PresentationFonts.Load(), 31, false, true);
            Click("탐험 시작"); yield return null;
            Click("편성하고 시작"); yield return null;
            var run = presenter.Session;
            Assert.IsTrue(run.Enter(run.Map.Nodes.First(n => run.CanEnter(n.Id)).Id));
            Assert.IsTrue(run.ResolveBattle(run.StageTicket, true, run.Health));
            Assert.AreEqual(RewardKind.Frame, run.Offers[2].Content.Kind);
            Assert.IsTrue(run.ChooseReward(2));
            presenter.SendMessage("Render"); yield return null;
            Click("메뉴"); Click("노트 조립"); yield return null;
            var weapon = run.OwnedWeapons[0];
            int originalCount = WeaponPhraseSet.Uniform(weapon).For(0, WeaponBeatSide.Light).Notes.Count;
            Click("1번 노트에 장착"); yield return null;
            Assert.AreEqual(1, weapon.NoteBindings.Count);
            Assert.AreSame(weapon, run.NotePartOwner(run.NoteParts[0].InstanceId));
            Assert.AreEqual(originalCount, WeaponNoteAssembly.Apply(weapon,
                WeaponPhraseSet.Uniform(weapon)).For(0, WeaponBeatSide.Light).Notes.Count);
            Click("부품 해제"); yield return null;
            Assert.AreEqual(0, weapon.NoteBindings.Count); Assert.AreEqual(1, run.NoteParts.Count);
            Assert.IsNull(run.NotePartOwner(run.NoteParts[0].InstanceId));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BossWeaponChoiceHasThreeCardsThenOffersEquipmentAndTheNextField()
        {
            root = new GameObject("Boss progression UI smoke test");
            var presenter = root.AddComponent<RunPresenter>();
            presenter.Initialize(new RunRules(), PresentationFonts.Load(), 31, false, true);
            Click("탐험 시작"); yield return null;
            Assert.AreEqual(7, root.GetComponentInChildren<RunSetupView>().Draft.Equipment.AvailableLaneCount);
            Click("편성하고 시작"); yield return null;
            var run = presenter.Session;
            for (int step = 0; step < 24 && !run.IsBossWeaponReward; step++)
            {
                if (run.Phase == RunPhase.Map) Assert.IsTrue(run.Enter(run.Map.Nodes.First(n => run.CanEnter(n.Id)).Id));
                else if (run.Phase == RunPhase.Reward) Assert.IsTrue(run.SkipReward());
                else if (run.CurrentNode.IsBattle) Assert.IsTrue(run.ResolveBattle(run.StageTicket, true, run.Health));
                else Assert.IsTrue(run.LeaveService());
            }
            Assert.IsTrue(run.IsBossWeaponReward);
            presenter.SendMessage("Render"); yield return null; Canvas.ForceUpdateCanvases();
            var choices = root.GetComponentsInChildren<Button>().Where(b =>
                b.GetComponentInChildren<TMP_Text>()?.text == "선택 · 가방에 보관").ToArray();
            Assert.AreEqual(3, choices.Length);
            Assert.IsFalse(root.GetComponentsInChildren<Button>().Any(b => b.GetComponentInChildren<TMP_Text>()?.text == "보상 건너뛰기"));
            var offer = run.Offers[1]; choices[1].onClick.Invoke(); choices[1].onClick.Invoke();
            yield return null;
            Assert.AreEqual(RunPhase.FieldCleared, run.Phase); Assert.AreEqual(3, run.OwnedWeapons.Count);
            Assert.AreEqual(offer.Content.Id, run.OwnedWeapons[2].DefinitionId);
            Assert.IsTrue(root.GetComponentsInChildren<Button>().Any(b => b.GetComponentInChildren<TMP_Text>()?.text == "무기 편성"));
            Click("다음 필드로"); yield return null;
            Assert.AreEqual(2, run.Map.Number); Assert.AreEqual(RunPhase.Map, run.Phase);
            Assert.AreEqual(3, run.OwnedWeapons.Count); Assert.AreEqual(7, run.Equipment.AvailableLaneCount);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CodexOpensBeforeARunAndReturnsToTheRunMenu()
        {
            root = new GameObject("Codex UI smoke test");
            var presenter = root.AddComponent<RunPresenter>();
            presenter.Initialize(new RunRules(), PresentationFonts.Load(), 73, false);
            Click("몬스터 도감"); yield return null; Canvas.ForceUpdateCanvases();
            Assert.IsNull(presenter.Session);
            var codex = root.GetComponentInChildren<MonsterCodexView>();
            var grid = codex.GetComponentInChildren<GridLayoutGroup>();
            Assert.AreEqual(MonsterCatalog.All.Count, grid.transform.childCount);
            Assert.Greater(grid.cellSize.x, 100);
            codex.GetComponentsInChildren<Button>().Single(x => x.name == "Codex tap-slime").onClick.Invoke();
            yield return null; Canvas.ForceUpdateCanvases();
            Assert.IsNotNull(codex.GetComponentInChildren<MonsterCodexStage>());
            Assert.AreEqual(MonsterCatalog.All.First(x => x.Id == "tap-slime").Patterns.Count + 1,
                codex.GetComponentsInChildren<Button>().Count(x => x.name == "Codex idle" || x.name.StartsWith("Codex pattern ")));
            Click("소리 켜짐");
            Click(MonsterCatalog.All[0].Patterns[0].Name); yield return null;
            Assert.IsNotNull(codex.GetComponentInChildren<MonsterPatternGraphic>());
            Click("평상시"); Assert.IsNull(codex.GetComponentInChildren<MonsterPatternGraphic>());
            codex.Back(); yield return null; Assert.IsTrue(grid.gameObject.activeInHierarchy);
            codex.Close(); yield return null; Click("탐험 시작"); yield return null;
            var map = presenter.Session.Map;
            var screen = root.GetComponentsInChildren<CanvasGroup>().Single(x => x.name == "Run screen");
            Click("메뉴");
            var menu = root.GetComponentsInChildren<RectTransform>().Single(x => x.name == "Run menu");
            var menuInput = menu.GetComponent<CanvasGroup>(); Assert.IsNotNull(menuInput);
            for (int cycle = 0; cycle < 2; cycle++)
            {
                Assert.IsTrue(menuInput.interactable); Assert.IsTrue(menuInput.blocksRaycasts);
                Click("몬스터 도감"); yield return null;
                codex = root.GetComponentInChildren<MonsterCodexView>(); Assert.IsNotNull(codex);
                Assert.IsFalse(menuInput.interactable); Assert.IsFalse(menuInput.blocksRaycasts);
                Assert.IsFalse(screen.interactable); Assert.IsFalse(screen.blocksRaycasts);
                Assert.IsTrue(codex.GetComponentsInChildren<Button>().Single(x => x.name == "Codex tap-slime").IsInteractable());
                codex.Close(); yield return null;
                Assert.IsTrue(menuInput.interactable); Assert.IsTrue(menuInput.blocksRaycasts);
                Assert.IsFalse(screen.interactable); Assert.AreSame(map, presenter.Session.Map);
                Assert.AreEqual(1, menu.GetComponents<CanvasGroup>().Length);
            }
            Click("돌아가기"); Assert.IsTrue(screen.interactable); Assert.IsTrue(screen.blocksRaycasts);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BootstrapCreatesKoreanUIAndConfirmedSetupOpensMap()
        {
            root = new GameObject("UI smoke test");
            root.AddComponent<RunBootstrap>();
            yield return null;
            var presenter = root.GetComponent<RunPresenter>();
            Assert.IsNotNull(presenter);
            var font = PresentationFonts.Load();
            Assert.IsNotNull(font);
            Assert.IsTrue(font.HasCharacter('탐'));
            var start = root.GetComponentsInChildren<Button>().Single(x => x.GetComponentInChildren<TextMeshProUGUI>().text == "탐험 시작");
            start.onClick.Invoke();
            Assert.IsNull(presenter.Session);
            Assert.IsNotNull(root.GetComponentInChildren<RunSetupView>());
            Click("편성하고 시작");
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(RunPhase.Map, presenter.Session.Phase);
            Assert.AreEqual(2, presenter.Session.Weapons.Count);
            var connections = root.GetComponentsInChildren<MapConnectionsGraphic>().Single();
            Assert.IsNull(connections.GetComponentInParent<ScrollRect>(), "The map must fit the viewport without scrolling.");
            var renderer = connections.GetComponent<CanvasRenderer>();
            Assert.IsNotNull(renderer, "Runtime-created map connections need a CanvasRenderer.");
            var mesh = renderer.GetMesh();
            Assert.IsNotNull(mesh, "Map connections should submit a mesh to the canvas.");
            Assert.Greater(mesh.vertexCount, 0, "Map connections should contain line geometry.");
            var nodes = root.GetComponentsInChildren<Button>().Where(x => x.GetComponentInChildren<TextMeshProUGUI>().text.Contains("진입")).ToArray();
            Assert.AreEqual(4, nodes.Length);
            foreach (var node in nodes)
            {
                Assert.IsTrue(node.interactable);
                Assert.IsTrue(node.GetComponentInChildren<TextMeshProUGUI>().text.Contains("몬스터"));
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
            presenter.Initialize(new RunRules(), PresentationFonts.Load(), 73, true);
            MusicStage announced = null;
            BattlePlan announcedPlan = null;
            presenter.BattleRequested += (ticket, kind, field) =>
            { announced = presenter.Session.BattleMusic; announcedPlan = presenter.Session.BattlePlan; };
            Click("탐험 시작");
            yield return null;
            for (int row = 0; row < FieldMap.StageCount; row++)
            {
                root.GetComponentsInChildren<Button>().First(x => x.interactable && x.GetComponentInChildren<TextMeshProUGUI>().text.Contains("진입")).onClick.Invoke();
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
            Click("메뉴"); Click("몬스터 패턴"); yield return null;
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
            var weaponCard = root.GetComponentsInChildren<TextMeshProUGUI>().Single(x => x.text == "무기  /  " + weaponOffer.Content.Name).transform.parent;
            weaponCard.GetComponentInChildren<Button>().onClick.Invoke(); yield return null;
            var replacement = root.GetComponentsInChildren<Button>().First(x => x.GetComponentInChildren<TextMeshProUGUI>().text.EndsWith("  교체"));
            Click("메뉴"); Click("장비 · 가방 · 증강"); Click("닫기"); yield return null;
            Assert.AreEqual(RunPhase.Reward, presenter.Session.Phase);
            Assert.IsTrue(replacement.IsInteractable(), "Checking equipment must retain the pending weapon offer.");
            replacement.onClick.Invoke(); yield return null;
            Assert.AreEqual(weaponOffer.Content.Id, presenter.Session.Weapons[0].DefinitionId);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SingleMonsterPreparationFillsTheListAndRendersHeaderButtonLabels()
        {
            root = new GameObject("Single monster preparation test");
            var presenter = root.AddComponent<RunPresenter>();
            presenter.Initialize(new RunRules(), PresentationFonts.Load(), 73, false);
            Click("탐험 시작"); yield return null;
            var run = presenter.Session;
            Assert.IsTrue(run.Enter(run.Map.Nodes.First(n => run.CanEnter(n.Id)).Id));
            presenter.OpenPatternPractice(); yield return null;
            var arena = root.GetComponentInChildren<BattlePreparationView>();
            Assert.IsNotNull(arena); Assert.AreEqual(1, arena.MonsterRows.Count);
            var safe = root.GetComponentInChildren<SafeAreaPanel>(); safe.enabled = false;
            foreach (var size in new[] { new Vector2(1280, 720), new Vector2(1480, 720), new Vector2(1280, 800) })
            {
                SetViewport(safe, size); yield return null; Canvas.ForceUpdateCanvases();
                yield return null; Canvas.ForceUpdateCanvases();
                AssertPreparationReadable(arena);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PreparationShowsThreeMonstersAndConfirmsReadOnlyPatternPractice()
        {
            root = new GameObject("Preparation UI smoke test");
            var presenter = root.AddComponent<RunPresenter>();
            presenter.Initialize(new RunRules(), PresentationFonts.Load(), 73, false);
            Click("탐험 시작"); yield return null;
            var run = presenter.Session;
            for (int step = 0; step < 200 && (run.BattlePlan == null || run.BattlePlan.Monsters.Count < 3); step++)
            {
                if (run.Phase == RunPhase.Map) Assert.IsTrue(run.Enter(run.Map.Nodes.First(n => run.CanEnter(n.Id)).Id));
                else if (run.Phase == RunPhase.Reward) Assert.IsTrue(run.IsBossWeaponReward ? run.ChooseReward(0) : run.SkipReward());
                else if (run.Phase == RunPhase.FieldCleared) Assert.IsTrue(run.AdvanceField());
                else if (run.CurrentNode.IsBattle) Assert.IsTrue(run.ResolveBattle(run.StageTicket, true, run.Health));
                else Assert.IsTrue(run.LeaveService());
            }
            Assert.IsNotNull(run.BattlePlan); Assert.AreEqual(3, run.BattlePlan.Monsters.Count);
            presenter.OpenPatternPractice(); yield return null; Canvas.ForceUpdateCanvases();
            var plan = run.BattlePlan; var loadout = run.BattleLoadout;
            var arena = root.GetComponentInChildren<BattlePreparationView>();
            Assert.IsNotNull(arena); Assert.IsNull(presenter.ActiveRound);
            Assert.IsNull(root.GetComponentInChildren<BattleArenaView>());
            Assert.IsNull(root.GetComponentInChildren<RhythmPlayback>());
            Assert.IsNotNull(arena.HeroPortrait.sprite);
            Assert.AreEqual(plan.Monsters.Count, arena.MonsterPortraits.Count);
            Assert.IsTrue(arena.MonsterPortraits.All(x => x.sprite != null));
            Assert.AreEqual((float)(run.Health / run.MaxHealth), arena.PlayerHealthBar.Value, .0001f);
            Assert.AreEqual((float)(run.EnemyHealth.Current / run.EnemyHealth.Maximum), arena.EnemyHealthBar.Value, .0001f);
            Assert.AreEqual(loadout.Patterns.Count, arena.PatternButtons.Count);
            Assert.AreEqual(5, arena.EquippedIcons.Count);
            Assert.IsTrue(arena.EquippedIcons.All(x => !x.raycastTarget && x.GetComponentInParent<Button>() == null));
            for (int i = 0; i < arena.PatternButtons.Count; i++)
            {
                var pattern = loadout.Patterns[i];
                var card = arena.PatternButtons.Single(x => x.name == "Practice pattern " + i);
                var expected = loadout.RespondingSlots(pattern).Select(slot => WeaponCatalog.Find(loadout.Equipment[slot].DefinitionId).Kind);
                CollectionAssert.AreEqual(expected, card.GetComponentsInChildren<WeaponIconGraphic>().Select(x => x.Kind));
                Assert.IsNotNull(card.GetComponentInChildren<PatternOverviewGraphic>());
                Assert.IsTrue(card.GetComponentsInChildren<Image>().Any(x => x.sprite != null));
            }
            var deck = root.GetComponentsInChildren<RectTransform>().Single(x => x.name == "Five equipped weapons");
            Assert.IsFalse(deck.GetComponentsInChildren<MonoBehaviour>().Any(x =>
                x is UnityEngine.EventSystems.IBeginDragHandler || x is UnityEngine.EventSystems.IDropHandler));
            var safe = root.GetComponentInChildren<SafeAreaPanel>(); safe.enabled = false;
            foreach (var size in new[] { new Vector2(1280, 720), new Vector2(1280, 800), new Vector2(1220, 680) })
            {
                SetViewport(safe, size); yield return null; Canvas.ForceUpdateCanvases();
                var rect = (RectTransform)arena.transform;
                Assert.AreEqual(size.y, rect.rect.height, .1f); Assert.AreEqual(size.x, rect.rect.width, .1f);
                AssertContained(rect, (RectTransform)safe.transform);
                var viewport = arena.GetComponentInChildren<ScrollRect>().viewport;
                foreach (var row in arena.MonsterRows) AssertContained(row, viewport);
                AssertPreparationReadable(arena);
                var buttons = arena.GetComponentsInChildren<Button>();
                foreach (var button in buttons)
                {
                    var bounds = (RectTransform)button.transform;
                    AssertContained(bounds, rect); Assert.GreaterOrEqual(bounds.rect.height, 28);
                    foreach (var other in buttons.Where(x => x != button)) AssertNoOverlap(bounds, (RectTransform)other.transform);
                }
                foreach (var icon in arena.GetComponentsInChildren<WeaponIconGraphic>())
                {
                    AssertContained(icon.rectTransform, rect);
                    icon.canvasRenderer.cull = false; icon.SetVerticesDirty(); icon.Rebuild(CanvasUpdate.PreRender);
                    Assert.Greater(icon.canvasRenderer.GetMesh().vertexCount, 0);
                }
            }
            decimal health = run.Health, enemyHealth = run.EnemyHealth.Current;
            foreach (var monster in plan.Monsters)
            {
                arena.GetComponentsInChildren<Button>().Single(x => x.name == "Preparation codex " + monster.InstanceId).onClick.Invoke();
                yield return null;
                var codex = root.GetComponentInChildren<MonsterCodexView>();
                Assert.AreSame(monster.Monster, codex.SelectedMonster); Assert.IsFalse(arena.StartButton.IsInteractable());
                codex.Close(); yield return null; Assert.IsTrue(arena.StartButton.IsInteractable());
            }
            arena.GetComponentsInChildren<Button>().Single(x => x.name == "Preparation codex all").onClick.Invoke(); yield return null;
            var overview = root.GetComponentInChildren<MonsterCodexView>(); Assert.IsNull(overview.SelectedMonster);
            overview.Close(); yield return null;
            int selected = loadout.Patterns.Count - 1;
            arena.RequestPractice(selected); yield return null;
            Assert.IsTrue(arena.HasPracticeConfirmation); Assert.IsNull(presenter.ActiveRound);
            Assert.AreEqual(selected, arena.PendingPracticeIndex);
            Assert.IsFalse(arena.StartButton.IsInteractable()); Assert.IsFalse(presenter.StartRhythmRound());
            Assert.IsTrue(arena.PatternButtons.All(x => !x.IsInteractable()));
            arena.RequestPractice(0); Assert.AreEqual(selected, arena.PendingPracticeIndex);
            Click("돌아가기"); yield return null;
            Assert.IsFalse(arena.HasPracticeConfirmation); Assert.IsTrue(arena.StartButton.IsInteractable());
            Assert.IsTrue(arena.PatternButtons.All(x => x.IsInteractable()));
            Assert.AreSame(plan, run.BattlePlan); Assert.AreSame(loadout, run.BattleLoadout);
            Assert.AreEqual(health, run.Health); Assert.AreEqual(enemyHealth, run.EnemyHealth.Current);
            Click("메뉴");
            Assert.IsFalse(root.GetComponentsInChildren<Button>().Any(x => x.GetComponentInChildren<TextMeshProUGUI>().text == "개발 도구"));
            Click("패턴 연습"); yield return null;
            arena = root.GetComponentInChildren<BattlePreparationView>();
            arena.PatternButtons.Single(x => x.name == "Practice pattern " + selected).onClick.Invoke(); yield return null;
            arena.ConfirmPractice(); var active = presenter.ActiveRound; arena.ConfirmPractice();
            Assert.AreSame(active, presenter.ActiveRound);
            yield return null;
            var practice = root.GetComponentInChildren<RhythmPlayback>(); practice.SetBeatSound(false);
            Assert.IsTrue(practice.Round.Combat.IsPractice);
            Assert.AreEqual(loadout.Patterns[selected].Pattern.Id, practice.Round.Plan.Attacks.Single().Pattern.Id);
            Assert.AreSame(loadout, run.BattleLoadout); Assert.AreSame(plan, run.BattlePlan);
            Assert.AreEqual(health, run.Health); Assert.AreEqual(enemyHealth, run.EnemyHealth.Current);
            Click("메뉴"); Click("패턴 목록으로"); yield return null;
            Assert.IsNotNull(root.GetComponentInChildren<BattlePreparationView>());
            Click("연주 시작"); yield return null;
            var battle = root.GetComponentInChildren<RhythmPlayback>(); battle.SetBeatSound(false);
            Assert.IsFalse(battle.Round.Combat.IsPractice); Assert.AreSame(plan, battle.Round.Plan);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MapUsesViewportAndMenuBlocksNodesWithoutRebuildingTheMap()
        {
            root = new GameObject("Fullscreen map test");
            var presenter = root.AddComponent<RunPresenter>();
            presenter.Initialize(new RunRules(), PresentationFonts.Load(), 73, false);
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
            presenter.Initialize(new RunRules(), PresentationFonts.Load(), 73, false);
            Click("탐험 시작"); yield return null;
            var run = presenter.Session;
            foreach (var hidden in run.Map.Nodes.Where(x => x.IsMystery))
            {
                var label = MapButton(hidden.Id).GetComponentInChildren<TextMeshProUGUI>().text;
                Assert.AreEqual((hidden.Row + 1).ToString("00") + "  ?", label);
            }
            var target = run.Map.Nodes.First(x => x.IsMystery);
            var opening = run.Map.Nodes.First(x => x.Row == 0 && x.Next.Contains(target.Id));
            MapButton(opening.Id).onClick.Invoke(); yield return null;
            Assert.IsTrue(presenter.SubmitBattleResult(run.StageTicket, true, run.Health)); yield return null;
            Click("보상 건너뛰기"); yield return null;
            Assert.AreEqual("02  ?\n진입", MapButton(target.Id).GetComponentInChildren<TextMeshProUGUI>().text);
            Click("메뉴"); Click("조작 방법"); Click("닫기"); yield return null;
            Assert.IsFalse(target.IsRevealed);
            StageKind? announced = null;
            presenter.BattleRequested += (ticket, kind, field) => announced = kind;
            MapButton(target.Id).onClick.Invoke(); yield return null;
            Assert.AreEqual(target.Kind, target.MapKind);
            if (target.IsBattle)
            {
                Assert.AreEqual(target.Kind, announced);
                Assert.IsNotNull(root.GetComponentInChildren<BattlePreparationView>());
                Assert.IsTrue(presenter.SubmitBattleResult(run.StageTicket, true, run.Health)); yield return null;
                Click("보상 건너뛰기");
            }
            else
            {
                Assert.IsNull(announced);
                Assert.IsTrue(root.GetComponentsInChildren<TextMeshProUGUI>().Any(x => x.text == ContentCatalog.StageName(target.Kind)));
                Click("지도에 돌아가기");
            }
            yield return null;
            Assert.AreEqual("02  " + ContentCatalog.StageName(target.Kind) + "\n완료", MapButton(target.Id).GetComponentInChildren<TextMeshProUGUI>().text);
            foreach (var hidden in run.Map.Nodes.Where(x => x.IsMystery && x != target))
                Assert.IsTrue(MapButton(hidden.Id).GetComponentInChildren<TextMeshProUGUI>().text.Contains("?"));
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

        private static void AssertPreparationReadable(BattlePreparationView arena)
        {
            var viewport = arena.GetComponentInChildren<ScrollRect>().viewport;
            float occupied = arena.MonsterRows.Sum(x => x.rect.height) + 8 * (arena.MonsterRows.Count - 1);
            Assert.AreEqual(viewport.rect.height, occupied, 1, "The monster list must use the middle of the screen.");
            var header = arena.GetComponentsInChildren<RectTransform>().Single(x => x.name == "Preparation header");
            foreach (var button in header.GetComponentsInChildren<Button>())
            {
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.canvasRenderer.cull = false; label.ForceMeshUpdate();
                int visibleCharacters = label.textInfo.characterInfo.Take(label.textInfo.characterCount).Count(x => x.isVisible);
                Assert.GreaterOrEqual(visibleCharacters, 2, "Header label disappeared: " + label.text);
            }
            foreach (var icon in arena.GetComponentsInChildren<WeaponIconGraphic>())
            {
                Assert.IsTrue(icon.FitVisibleArtwork);
                Assert.GreaterOrEqual(icon.rectTransform.rect.height, 22);
                icon.canvasRenderer.cull = false; icon.SetVerticesDirty(); icon.Rebuild(CanvasUpdate.PreRender);
                foreach (var vertex in icon.canvasRenderer.GetMesh().vertices)
                {
                    var rect = icon.rectTransform.rect;
                    Assert.That(vertex.x, Is.InRange(rect.xMin - 1, rect.xMax + 1));
                    Assert.That(vertex.y, Is.InRange(rect.yMin - 1, rect.yMax + 1));
                }
            }
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

        private static void AssertNoOverlap(RectTransform left, RectTransform right)
        {
            var canvas = (RectTransform)left.GetComponentInParent<Canvas>().transform;
            Assert.IsFalse(LocalRect(left, canvas).Overlaps(LocalRect(right, canvas)), left.name + " overlaps " + right.name);
        }

        private void VerifyMonsterCards(BattlePlan plan)
        {
            Assert.IsNotNull(plan);
            Assert.IsNotNull(root.GetComponentInChildren<BattlePreparationView>());
            Canvas.ForceUpdateCanvases();
            var cards = root.GetComponentsInChildren<MonsterPatternView>();
            Assert.AreEqual(plan.Monsters.Count, cards.Length);
            foreach (var monster in plan.Monsters)
            {
                var card = cards.Single(x => x.Plan.InstanceId == monster.InstanceId);
                Assert.AreSame(monster, card.Plan);
                Assert.IsTrue(card.GetComponentsInChildren<TextMeshProUGUI>().Any(x => x.text.Contains(monster.Monster.Name)));
                var graphics = card.GetComponentsInChildren<MonsterPatternGraphic>();
                Assert.AreEqual(monster.Monster.Patterns.Count, graphics.Length);
                foreach (var pattern in monster.Monster.Patterns)
                    Assert.IsTrue(card.GetComponentsInChildren<TextMeshProUGUI>().Any(x => x.text.Contains(pattern.Name)));
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
            var button = root.GetComponentsInChildren<Button>().Single(x => x.IsInteractable() && x.GetComponentInChildren<TextMeshProUGUI>().text == label);
            Assert.IsTrue(button.interactable);
            button.onClick.Invoke();
        }
    }
}
