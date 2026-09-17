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
    }
}
