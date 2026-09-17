using System;
using System.Collections.Generic;
using BBSB.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    // Compact catalogue cards. Only the artwork captures a placement drag, so
    // dragging the text or gaps can still scroll the inventory on a touch screen.
    public sealed class EquipmentInventoryView : MonoBehaviour
    {
        public const float CardHeight = 138;
        private GridLayoutGroup grid;
        private LayoutElement size;
        private float lastWidth = -1;

        internal void Bind(IEquipmentEditor session, RunUI ui, EquipmentPlacementView board)
        {
            grid = gameObject.AddComponent<GridLayoutGroup>();
            grid.spacing = new Vector2(10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            size = RunUI.Size((RectTransform)transform, CardHeight, 1);
            // The viewport controls the width, including when a wider window shrinks.
            // GridLayoutGroup's previous cell widths must not become the new minimum.
            size.minWidth = size.preferredWidth = 0;
            var sets = WeaponPhraseAuthoring.LoadSetsFor(session.OwnedWeapons);
            for (int i = 0; i < session.OwnedWeapons.Count; i++)
            {
                int index = i; var weapon = session.OwnedWeapons[i];
                var placement = session.Equipment.PlacementOf(weapon);
                var card = ui.Rect("Owned weapon " + i, transform);
                var background = ui.Background(card, placement == null ? RunUI.Panel : RunUI.Hex("293D49"), true);
                var button = card.gameObject.AddComponent<Button>(); button.targetGraphic = background;
                button.interactable = session.CanEditEquipment;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() => board.SelectWeapon(index));

                var thumbnail = ui.Rect("Weapon thumbnail", card);
                RunUI.Pin(thumbnail, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -8), new Vector2(58, 68));
                ui.Background(thumbnail, Color.clear, true);
                thumbnail.gameObject.AddComponent<EquipmentDragHandle>().Bind(board, index);
                var visual = ui.Rect("Weapon artwork " + weapon.DefinitionId, thumbnail); RunUI.Stretch(visual, 3);
                var artwork = visual.gameObject.AddComponent<WeaponIconGraphic>();
                artwork.FitVisibleArtwork = true; artwork.Bind(weapon);
                foreach (var sockets in artwork.GetComponentsInChildren<WeaponSocketGraphic>()) sockets.gameObject.SetActive(false);

                var name = Caption(ui, card, weapon.DisplayName + " +" + weapon.Level, 21, RunUI.TextColor);
                RunUI.Overlay(name.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(78, -34), new Vector2(-10, -8));
                var rarity = Caption(ui, card, WeaponRarities.Name(weapon.Rarity) + " · " + weapon.RequiredLanes + "라인", 17,
                    WeaponIconGraphic.RarityColor(weapon.Rarity));
                RunUI.Overlay(rarity.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(78, -55), new Vector2(-10, -35));
                var keys = new List<string>();
                if (placement != null) foreach (int slot in placement.Slots) keys.Add(BattleInputLayout.Key(slot));
                var binding = Caption(ui, card, placement == null ? "미배치" : string.Join(" + ", keys), 17,
                    placement == null ? RunUI.Muted : RunUI.Teal);
                RunUI.Overlay(binding.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(78, -76), new Vector2(-10, -56));
                var hint = Caption(ui, card, AttributeHint(weapon, sets[i]), 17, RunUI.Muted);
                hint.name = "Weapon short description"; hint.textWrappingMode = TextWrappingModes.Normal;
                hint.maxVisibleLines = 2;
                RunUI.Overlay(hint.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(10, 6), new Vector2(-10, 58));
            }
            RefreshLayout();
        }

        private static TMP_Text Caption(RunUI ui, RectTransform parent, string value, int fontSize, Color color)
        {
            var text = ui.Label(parent, value, fontSize, color);
            // Prefab typography must not expand these fixed-size inventory cards.
            text.fontSize = fontSize; text.color = color; text.alignment = TextAlignmentOptions.Left;
            text.enableAutoSizing = false; text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis; text.raycastTarget = false;
            return text;
        }

        private static string AttributeHint(WeaponState weapon, WeaponPhraseSet set)
        {
            if ((weapon.Attribute == WeaponAttribute.Dual || weapon.Attribute == WeaponAttribute.Chaos) &&
                !ReferenceEquals(set.LightStarts[0], set.DarkStarts[0]))
                return "빛: " + ShortHint(set.LightStarts[0]) + "\n어둠: " + ShortHint(set.DarkStarts[0]);
            string hint = weapon.Attribute == WeaponAttribute.Chaos ?
                set.Chaos.MinimumBeats + "박 유지 후 전환 · 효과 ×" + set.Chaos.EffectMultiplier : WeaponAttributes.Hint(weapon.Attribute);
            return hint + "\n" + ShortHint(set.For(0, weapon.Attribute == WeaponAttribute.Dark ? WeaponBeatSide.Dark : WeaponBeatSide.Light));
        }
        private static string ShortHint(WeaponPhrase phrase)
        {
            // Authored patterns retain their own descriptions instead of inheriting
            // a summary for a different version of that weapon.
            if (!ReferenceEquals(phrase, WeaponPhraseCatalog.Find(phrase.WeaponId))) return phrase.Hint;
            if (phrase.Repeat && phrase.Notes.Count == 1)
                return phrase.LengthBeats.ToString("0.##") + "박 간격 · 성공 시 반복";
            switch (phrase.WeaponId)
            {
                case "staff": return "시작 쪽 반박 2회 입력 → 반대쪽 1박 홀드";
                case "spirit-bell": return "양옆 무기 1박 뒤 시작 · 쿨타임 무시";
                case "heater-shield": return "시작 패링 · 2박/50% 방어 · 패링 시 쿨타임 회복";
                case "bow": return "1박 당기기 → 2박에 발사";
                default: return phrase.Hint;
            }
        }

        public void RefreshLayout()
        {
            if (grid == null) return;
            float width = ((RectTransform)transform).rect.width;
            if (width <= 0 || Mathf.Abs(width - lastWidth) < .1f) return;
            lastWidth = width;
            int columns = Mathf.Clamp(Mathf.FloorToInt((width + 10) / 290), 1, 4);
            grid.constraintCount = columns;
            grid.cellSize = new Vector2(Mathf.Max(1, (width - (columns - 1) * 10) / columns), CardHeight);
            int rows = (transform.childCount + columns - 1) / columns;
            size.minHeight = size.preferredHeight = Mathf.Max(0, rows * (CardHeight + 10) - 10);
        }
        private void LateUpdate() => RefreshLayout();
    }
}
