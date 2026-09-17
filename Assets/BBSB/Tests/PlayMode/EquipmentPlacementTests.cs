using System.Collections;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime;
using BBSB.Runtime.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BBSB.Tests
{
    public sealed class EquipmentPlacementTests
    {
        private GameObject root;
        [UnityTearDown] public IEnumerator Cleanup() { if (root != null) Object.Destroy(root); yield return null; }
        private void Click(string text) => root.GetComponentsInChildren<Button>().First(b =>
            b.GetComponentInChildren<TMP_Text>()?.text == text).onClick.Invoke();
        private IEnumerator OpenInventory()
        {
            root = new GameObject("Equipment bootstrap"); root.AddComponent<RunBootstrap>(); yield return null;
            Click("탐험 시작");
            var presenter = root.GetComponent<RunPresenter>();
            Assert.IsTrue(presenter.Session.Enter(presenter.Session.Map.Nodes.First(n => presenter.Session.CanEnter(n.Id)).Id));
            presenter.SendMessage("Render"); Click("무기 편성"); yield return null; Canvas.ForceUpdateCanvases();
        }
        private static Vector2 At(EquipmentPlacementView board, double center)
        {
            var r = board.LaneArea.rect;
            var p = new Vector2(r.xMin + (float)(center + 1.5) * r.width / 7, r.center.y);
            return RectTransformUtility.WorldToScreenPoint(null, board.LaneArea.TransformPoint(p));
        }
        private static Vector2 Middle(RectTransform rect) =>
            RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        private static PointerEventData Pointer(Vector2 position) => new PointerEventData(EventSystem.current)
        { pointerId = 12, button = PointerEventData.InputButton.Left, pressPosition = position, position = position };
        [UnityTest] public IEnumerator DragPreviewIsCenteredAndOverlapChangesOnlyBindings()
        {
            yield return OpenInventory();
            var presenter = root.GetComponent<RunPresenter>(); var run = presenter.Session;
            var wide = new WeaponState("greatsword", WeaponRarity.Epic, 2, 2); run.Equipment.Acquire(wide);
            // Rebuild the inventory after injecting a future two-line item fixture.
            presenter.SendMessage("Render"); yield return null; Canvas.ForceUpdateCanvases();
            var board = root.GetComponentInChildren<EquipmentPlacementView>();
            Assert.IsNotNull(board); Assert.IsNull(board.GetComponentInParent<ScrollRect>(), "The board must remain visible while browsing inventory.");
            var first = run.OwnedWeapons[0]; var second = run.OwnedWeapons[1];
            var data = new PointerEventData(EventSystem.current) { pointerId = 3, button = PointerEventData.InputButton.Left, position = At(board, .5) };
            board.BeginWeaponDrag(2, data);
            Assert.IsTrue(board.PreviewValid); CollectionAssert.AreEqual(new[] { 0, 1 }, board.Preview.Slots);
            var center = RectTransformUtility.WorldToScreenPoint(null, board.DragPreview.position);
            Assert.Less(Vector2.Distance(center, data.position), .1f, "Even-width footprints must be centred under the pointer.");
            Assert.AreEqual(2, run.Weapons.Count, "Previewing must not mutate equipment.");
            board.EndWeaponDrag(data); yield return null;
            Assert.AreEqual(1, run.Weapons.Count); Assert.AreEqual(3, run.OwnedWeapons.Count);
            Assert.AreSame(wide, run.Equipment.At(0).Weapon); Assert.AreSame(first, run.OwnedWeapons[0]); Assert.AreSame(second, run.OwnedWeapons[1]);
            Assert.IsNull(run.Equipment.PlacementOf(first)); Assert.IsNull(run.Equipment.PlacementOf(second));
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator LockedKlDropIsCanceledThenUnlockedKlUsesTheSameCenteredPlacement()
        {
            yield return OpenInventory();
            var presenter = root.GetComponent<RunPresenter>(); var run = presenter.Session;
            var wide = new WeaponState("greatsword", WeaponRarity.Common, requiredLanes: 2); run.Equipment.Acquire(wide);
            Assert.IsTrue(run.UnequipWeapon(0)); Assert.IsTrue(run.EquipWeaponAtCenter(2, 3.5));
            presenter.SendMessage("Render"); yield return null; Canvas.ForceUpdateCanvases();
            var board = root.GetComponentInChildren<EquipmentPlacementView>(); var before = run.Equipment.PlacementOf(wide);
            var data = new PointerEventData(EventSystem.current) { pointerId = 8, button = PointerEventData.InputButton.Left, position = At(board, 4.5) };
            board.BeginWeaponDrag(2, data); Assert.IsFalse(board.PreviewValid); board.EndWeaponDrag(data);
            Assert.AreSame(before, run.Equipment.PlacementOf(wide));
            Assert.IsTrue(run.UnlockInputExtensions(InputExtensions.Right));
            presenter.SendMessage("Render"); yield return null; Canvas.ForceUpdateCanvases();
            board = root.GetComponentInChildren<EquipmentPlacementView>(); data.position = At(board, 4.5);
            board.BeginWeaponDrag(2, data); Assert.IsTrue(board.PreviewValid); CollectionAssert.AreEqual(new[] { 4, 6 }, board.Preview.Slots);
            board.EndWeaponDrag(data); yield return null;
            Assert.AreSame(wide, run.Equipment.At(4).Weapon); Assert.AreSame(wide, run.Equipment.At(BattleInputLayout.Right).Weapon);
            Assert.IsNull(run.Equipment.At(3));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator DownwardBoardDropRemovesTheWholeWeaponOnlyOnRelease()
        {
            yield return OpenInventory();
            var presenter = root.GetComponent<RunPresenter>(); var run = presenter.Session;
            var staff = new WeaponState("staff"); run.Equipment.Acquire(staff);
            Assert.IsTrue(run.EquipWeaponAtCenter(2, .5));
            presenter.SendMessage("Render"); yield return null; Canvas.ForceUpdateCanvases();
            var board = root.GetComponentInChildren<EquipmentPlacementView>();
            var binding = run.Equipment.PlacementOf(staff);
            var data = Pointer(At(board, 1)); // Grab either occupied line, not just the left one.
            board.OnBeginDrag(data);
            data.position = Middle(board.UnequipArea); board.OnDrag(data);
            Assert.IsTrue(board.PreviewUnequip); Assert.IsFalse(board.PreviewValid);
            Assert.AreSame(binding, run.Equipment.PlacementOf(staff), "Hovering over removal must not change a binding.");
            board.OnEndDrag(data); yield return null;
            Assert.IsNull(run.Equipment.At(0)); Assert.IsNull(run.Equipment.At(1));
            Assert.IsNull(run.Equipment.PlacementOf(staff));
            Assert.AreEqual(3, run.OwnedWeapons.Count); Assert.AreSame(staff, run.OwnedWeapons[2]);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator InventoryReturnAndEmptyOrSideDragsDoNotUnequip()
        {
            yield return OpenInventory();
            var run = root.GetComponent<RunPresenter>().Session;
            var board = root.GetComponentInChildren<EquipmentPlacementView>();
            var binding = run.Equipment.PlacementOf(run.OwnedWeapons[0]);
            var data = Pointer(At(board, binding.Center));
            board.BeginWeaponDrag(0, data);
            data.position = Middle(board.UnequipArea); board.MoveWeaponDrag(data);
            Assert.IsFalse(board.PreviewUnequip, "Returning an inventory thumbnail cancels its placement drag.");
            board.EndWeaponDrag(data); Assert.AreSame(binding, run.Equipment.PlacementOf(run.OwnedWeapons[0]));

            // A previous selection must not turn dragging an empty board cell into moving that weapon.
            board.SelectWeapon(0); data = Pointer(At(board, 4)); board.OnBeginDrag(data);
            data.position = Middle(board.UnequipArea); board.OnDrag(data); board.OnEndDrag(data);
            Assert.IsFalse(board.DragPreview.gameObject.activeSelf);
            Assert.AreSame(binding, run.Equipment.PlacementOf(run.OwnedWeapons[0]));

            data = Pointer(At(board, binding.Center)); board.OnBeginDrag(data);
            var rect = (RectTransform)board.transform;
            data.position = RectTransformUtility.WorldToScreenPoint(null,
                rect.TransformPoint(new Vector2(rect.rect.xMax + 20, rect.rect.yMin - 20)));
            board.OnDrag(data); Assert.IsFalse(board.PreviewUnequip); board.OnEndDrag(data);
            Assert.AreSame(binding, run.Equipment.PlacementOf(run.OwnedWeapons[0]));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator CompactCardsKeepTheBoardVisibleAndScrollPositionAcrossEdits()
        {
            yield return OpenInventory();
            var presenter = root.GetComponent<RunPresenter>(); var run = presenter.Session;
            for (int i = 0; i < 18; i++) run.Equipment.Acquire(new WeaponState("dagger"));
            presenter.SendMessage("Render"); yield return null; yield return null; Canvas.ForceUpdateCanvases();
            var inventory = root.GetComponentInChildren<EquipmentInventoryView>();
            var grid = inventory.GetComponent<GridLayoutGroup>();
            var cards = inventory.transform.Cast<RectTransform>().ToArray();
            Assert.AreEqual(run.OwnedWeapons.Count, cards.Length);
            if (((RectTransform)inventory.transform).rect.width >= 580) Assert.Greater(grid.constraintCount, 1);
            foreach (var card in cards)
            {
                Assert.LessOrEqual(card.rect.height, 142, "Descriptions must not expand a card into a full page.");
                Assert.Greater(card.rect.height, 0);
                Assert.AreEqual(1, card.GetComponentsInChildren<Button>().Length);
                Assert.IsNotNull(card.GetComponentInChildren<WeaponIconGraphic>());
                var handle = card.GetComponentInChildren<EquipmentDragHandle>(); Assert.IsNotNull(handle);
                Assert.AreNotEqual(card.gameObject, handle.gameObject, "Text must remain available for scrolling.");
                var hint = card.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "Weapon short description");
                Assert.LessOrEqual(hint.rectTransform.rect.height, 56); Assert.AreEqual(2, hint.maxVisibleLines);
                Assert.AreEqual(TextOverflowModes.Ellipsis, hint.overflowMode);
            }
            Assert.AreEqual("2박 간격 · 성공 시 반복",
                cards[0].GetComponentsInChildren<TMP_Text>().Single(t => t.name == "Weapon short description").text);
            var scroll = inventory.GetComponentInParent<ScrollRect>();
            Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height);
            var scaler = inventory.GetComponentInParent<CanvasScaler>();
            var resolution = scaler.referenceResolution;
            scaler.referenceResolution = resolution * 1.5f;
            yield return null; yield return null; Canvas.ForceUpdateCanvases();
            scaler.referenceResolution = resolution;
            yield return null; yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(((RectTransform)inventory.transform).rect.width,
                Is.EqualTo(scroll.viewport.rect.width).Within(.1f), "Cards must shrink with the viewport after resizing.");
            scroll.StopMovement(); scroll.verticalNormalizedPosition = .42f;
            var board = root.GetComponentInChildren<EquipmentPlacementView>();
            Assert.IsNull(board.GetComponentInParent<ScrollRect>());
            var data = Pointer(At(board, 0));
            board.BeginWeaponDrag(2, data); board.EndWeaponDrag(data); yield return null; Canvas.ForceUpdateCanvases();
            scroll = root.GetComponentInChildren<EquipmentInventoryView>().GetComponentInParent<ScrollRect>();
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(.42f).Within(.01f));
            Assert.AreSame(run.OwnedWeapons[2], run.Equipment.At(0).Weapon);

            board = root.GetComponentInChildren<EquipmentPlacementView>(); data = Pointer(At(board, 0)); board.OnBeginDrag(data);
            // The list below the removal strip also accepts a downward drop.
            data.position = Middle(scroll.viewport); board.OnDrag(data);
            Assert.IsTrue(board.PreviewUnequip); board.OnEndDrag(data); yield return null; Canvas.ForceUpdateCanvases();
            scroll = root.GetComponentInChildren<EquipmentInventoryView>().GetComponentInParent<ScrollRect>();
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(.42f).Within(.01f));
            Assert.IsNull(run.Equipment.PlacementOf(run.OwnedWeapons[2]));
            Assert.AreEqual(20, run.OwnedWeapons.Count);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
