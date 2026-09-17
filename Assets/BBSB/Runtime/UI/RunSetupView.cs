using System;
using System.Collections.Generic;
using BBSB.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    public sealed class RunSetupView : MonoBehaviour
    {
        private RunUI ui;
        private RunSetupBindings bindings;
        private RunStartPreferences preferences;
        private IReadOnlyList<PlayerCharacter> characters;
        private readonly Dictionary<string, StartingLoadout> drafts = new Dictionary<string, StartingLoadout>();
        private readonly Dictionary<string, Graphic> choices = new Dictionary<string, Graphic>();
        private PlayerCharacter selected;
        public StartingLoadout Draft { get; private set; }
        public string SelectedCharacterId => selected?.Definition.Id;
        public Button StartButton => bindings.start;

        internal void Bind(RunUI ui, IReadOnlyList<PlayerCharacter> characters, RunStartPreferences preferences,
            Action<RunCharacterDefinition, StartingLoadoutPreset> start, Action back)
        {
            this.ui = ui; this.characters = characters; this.preferences = preferences;
            bindings = GetComponentInParent<CanvasScreen>()?.runSetup;
            if (bindings == null) bindings = RunSetupBindings.CreateDefault((RectTransform)transform, ui.Font);
            if (!bindings.IsValid) throw new InvalidOperationException("시작 편성 화면의 UI 참조를 모두 연결해줘.");
            bindings.back.onClick.AddListener(() => back());
            bindings.start.onClick.AddListener(() => { if (Draft.CanStart) start(Draft.Character, Draft.Capture()); });
            foreach (var character in characters)
            {
                string id = character.Definition.Id;
                var button = ui.Button(bindings.characterList, character.Name, () => SelectCharacter(id), height: 60);
                button.name = "Character " + id;
                choices.Add(id, button.targetGraphic);
                var label = button.GetComponentInChildren<TMP_Text>();
                label.fontSize = 20; label.alignment = TextAlignmentOptions.Left;
                RunUI.Stretch(label.rectTransform, 6); label.rectTransform.offsetMin = new Vector2(64, 6);
                var icon = ui.Rect("Character thumbnail", button.transform);
                RunUI.Pin(icon, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(6, 0), new Vector2(50, 50));
                var image = ui.Background(icon, Color.white); image.sprite = Portrait(character); image.preserveAspect = true;
                if (image.sprite == null) image.color = Color.clear;
            }
            SelectCharacter(preferences.selectedCharacterId);
        }
        public void SelectCharacter(string id)
        {
            selected = characters[0];
            foreach (var character in characters) if (character.Definition.Id == id) { selected = character; break; }
            if (!drafts.TryGetValue(selected.Definition.Id, out var draft))
            {
                draft = new StartingLoadout(selected.Definition, preferences.Find(selected.Definition.Id));
                drafts.Add(selected.Definition.Id, draft);
            }
            Draft = draft;
            bindings.characterName.text = selected.Name; bindings.description.text = selected.Description;
            bindings.portrait.sprite = Portrait(selected); bindings.portrait.color = bindings.portrait.sprite != null ? Color.white : Color.clear;
            foreach (var choice in choices) choice.Value.color = choice.Key == selected.Definition.Id ? RunUI.Teal : RunUI.Hex("2B3850");
            RefreshEquipment(false);
        }
        private static Sprite Portrait(PlayerCharacter character) => character.Appearance != null && character.Appearance.portrait != null ?
            character.Appearance.portrait : Resources.Load<Sprite>("BBSB/BattleArt/weapon-master");

        private void RefreshEquipment(bool preserveScroll)
        {
            var scroll = bindings.weaponList.GetComponentInParent<ScrollRect>();
            float position = preserveScroll ? scroll.verticalNormalizedPosition : 1;
            Clear(bindings.boardRoot); Clear(bindings.weaponList);
            var boardRoot = ui.Rect("Starting equipment placement", bindings.boardRoot); RunUI.Stretch(boardRoot);
            var board = boardRoot.gameObject.AddComponent<EquipmentPlacementView>();
            board.Bind(Draft, ui, () => RefreshEquipment(true));
            var cards = ui.Rect("Starting weapon grid", bindings.weaponList);
            var inventory = cards.gameObject.AddComponent<EquipmentInventoryView>(); inventory.Bind(Draft, ui, board);
            bindings.capacity.text = "시작 무기  ·  장착 " + Draft.Equipment.Equipped.Count + " / " + Draft.Equipment.Capacity + "개" +
                "  ·  " + Draft.Equipment.OccupiedLaneCount + " / " + Draft.Equipment.AvailableLaneCount + "라인";
            bindings.start.interactable = Draft.CanStart;
            bindings.status.text = Draft.CanStart ? "선택한 캐릭터와 시작 편성은\n다음 탐험에도 이어져." : "시작하려면 무기를 하나 이상 배치해줘.";
            bindings.status.color = Draft.CanStart ? RunUI.Muted : RunUI.Gold;
            Canvas.ForceUpdateCanvases(); inventory.RefreshLayout();
            LayoutRebuilder.ForceRebuildLayoutImmediate(bindings.weaponList); Canvas.ForceUpdateCanvases();
            scroll.StopMovement(); scroll.verticalNormalizedPosition = Mathf.Clamp01(position);
        }
        private static void Clear(Transform parent)
        {
            foreach (Transform child in parent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        }
    }
}
