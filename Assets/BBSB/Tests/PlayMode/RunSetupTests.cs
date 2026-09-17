using System;
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
using Object = UnityEngine.Object;

namespace BBSB.Tests
{
    // PlayMode tests must not overwrite the developer's real preferred starting layout.
    internal sealed class RunStartPreferenceScope : IDisposable
    {
        private readonly bool existed = PlayerPrefs.HasKey(RunStartPreferences.StorageKey);
        private readonly string previous = PlayerPrefs.GetString(RunStartPreferences.StorageKey, "");
        public RunStartPreferenceScope() { PlayerPrefs.DeleteKey(RunStartPreferences.StorageKey); }
        public void Dispose()
        {
            if (existed) PlayerPrefs.SetString(RunStartPreferences.StorageKey, previous);
            else PlayerPrefs.DeleteKey(RunStartPreferences.StorageKey);
            PlayerPrefs.Save();
        }
    }

    public sealed class RunSetupTests
    {
        private GameObject root;
        private RunStartPreferenceScope preferences;
        [SetUp] public void IsolatePreferences() => preferences = new RunStartPreferenceScope();
        [UnityTearDown] public IEnumerator Cleanup()
        { if (root != null) Object.Destroy(root); preferences.Dispose(); yield return null; }
        private void Click(string text) => root.GetComponentsInChildren<Button>().Single(b =>
            b.GetComponentInChildren<TMP_Text>()?.text == text).onClick.Invoke();
        private IEnumerator OpenSetup()
        {
            root = new GameObject("Run setup test"); root.AddComponent<RunBootstrap>(); yield return null;
            Click("탐험 시작"); yield return null; Canvas.ForceUpdateCanvases();
        }
        private void Place(int item, float center)
        {
            var board = root.GetComponentInChildren<EquipmentPlacementView>();
            var r = board.LaneArea.rect;
            var screen = RectTransformUtility.WorldToScreenPoint(null, board.LaneArea.TransformPoint(
                new Vector2(r.xMin + (center + 1.5f) * r.width / 7, r.center.y)));
            var data = new PointerEventData(EventSystem.current) { pointerId = 2, position = screen, button = PointerEventData.InputButton.Left };
            board.BeginWeaponDrag(item, data); Assert.IsTrue(board.PreviewValid); board.EndWeaponDrag(data);
        }
        private void Remove(float center)
        {
            var board = root.GetComponentInChildren<EquipmentPlacementView>(); var r = board.LaneArea.rect;
            var start = RectTransformUtility.WorldToScreenPoint(null, board.LaneArea.TransformPoint(
                new Vector2(r.xMin + (center + 1.5f) * r.width / 7, r.center.y)));
            var data = new PointerEventData(EventSystem.current) { pointerId = 2, position = start, pressPosition = start, button = PointerEventData.InputButton.Left };
            board.OnBeginDrag(data);
            data.position = RectTransformUtility.WorldToScreenPoint(null, board.UnequipArea.TransformPoint(board.UnequipArea.rect.center));
            board.OnDrag(data); Assert.IsTrue(board.PreviewUnequip); board.OnEndDrag(data);
        }

        [UnityTest] public IEnumerator SetupCanBeCanceledWithoutCreatingARunOrSavingDraftChanges()
        {
            yield return OpenSetup();
            var presenter = root.GetComponent<RunPresenter>();
            Assert.IsNull(presenter.Session); Assert.IsFalse(PlayerPrefs.HasKey(RunStartPreferences.StorageKey));
            var view = root.GetComponentInChildren<RunSetupView>(); Assert.IsNotNull(view);
            Assert.AreEqual(2, view.Draft.OwnedWeapons.Count);
            var board = root.GetComponentInChildren<EquipmentPlacementView>();
            Assert.IsNull(board.GetComponentInParent<ScrollRect>());
            Assert.Greater(board.LaneArea.rect.width, 0); Assert.Greater(board.LaneArea.rect.height, 0);
            Place(0, 2); Place(1, 4); Click("돌아가기"); yield return null;
            Assert.IsNull(presenter.Session); Assert.IsFalse(PlayerPrefs.HasKey(RunStartPreferences.StorageKey));
            Click("탐험 시작"); yield return null;
            view = root.GetComponentInChildren<RunSetupView>();
            Assert.AreEqual("dagger", view.Draft.Equipment.At(0).Weapon.DefinitionId);
            Assert.AreEqual("heater-shield", view.Draft.Equipment.At(1).Weapon.DefinitionId);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator ConfirmedLayoutSurvivesANewBootstrapAndIgnoresInRunEquipmentEdits()
        {
            yield return OpenSetup(); Place(0, 2); Place(1, 4); Click("편성하고 시작"); yield return null;
            var run = root.GetComponent<RunPresenter>().Session;
            Assert.AreEqual(RunPhase.Map, run.Phase); Assert.AreEqual(RunCharacterDefinition.DefaultId, run.CharacterId);
            Assert.AreEqual("dagger", run.Equipment.At(2).Weapon.DefinitionId);
            Assert.AreEqual("heater-shield", run.Equipment.At(4).Weapon.DefinitionId);
            var saved = PlayerPrefs.GetString(RunStartPreferences.StorageKey);
            run.EquipWeaponAtCenter(0, 0); run.OwnedWeapons[0].Level = 2;
            run.Equipment.Acquire(new WeaponState("staff")); run.Abandon();
            Assert.AreEqual(saved, PlayerPrefs.GetString(RunStartPreferences.StorageKey));
            Object.Destroy(root); yield return null;
            yield return OpenSetup();
            var view = root.GetComponentInChildren<RunSetupView>();
            Assert.AreEqual("dagger", view.Draft.Equipment.At(2).Weapon.DefinitionId);
            Assert.AreEqual("heater-shield", view.Draft.Equipment.At(4).Weapon.DefinitionId);
            Assert.AreEqual(2, view.Draft.OwnedWeapons.Count); Assert.AreEqual(0, view.Draft.OwnedWeapons[0].Level);
            Remove(2); Remove(4);
            Assert.IsFalse(view.StartButton.interactable); view.StartButton.onClick.Invoke();
            Assert.IsNull(root.GetComponent<RunPresenter>().Session);
            Assert.AreEqual(saved, PlayerPrefs.GetString(RunStartPreferences.StorageKey));
            LogAssert.NoUnexpectedReceived();
        }

        [Test] public void PreferenceSerializationKeepsSeparateCharacterLayoutsAndRecoversInvalidFiles()
        {
            var first = new StartingLoadout(RunCharacterDefinition.Default); first.EquipWeaponAtCenter(0, 2); first.EquipWeaponAtCenter(1, 4);
            var other = new RunCharacterDefinition("staff-player", new[] { new StartingWeaponDefinition("main", "staff", 0) });
            var second = new StartingLoadout(other); second.EquipWeaponAtCenter(0, 3.5);
            var settings = new RunStartPreferences(); settings.Remember(first.Character, first.Capture());
            settings.Remember(other, second.Capture()); settings.Save();
            var restored = RunStartPreferences.Load(); Assert.AreEqual("staff-player", restored.selectedCharacterId);
            Assert.AreEqual(2, new StartingLoadout(first.Character, restored.Find(first.Character.Id)).Equipment.Placements[0].Offset);
            Assert.AreEqual(3, new StartingLoadout(other, restored.Find(other.Id)).Equipment.Placements[0].Offset);
            PlayerPrefs.SetString(RunStartPreferences.StorageKey, "invalid json");
            Assert.AreEqual(RunCharacterDefinition.DefaultId, RunStartPreferences.Load().selectedCharacterId);
            PlayerPrefs.SetString(RunStartPreferences.StorageKey, "{\"version\":99,\"selectedCharacterId\":\"unknown\"}");
            Assert.IsNull(RunStartPreferences.Load().Find(other.Id));
        }
    }
}
