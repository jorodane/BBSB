using System;
using System.Collections.Generic;
using BBSB.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    // Optional artwork sits on top of the same geometry used by the fallback mesh.
    // Images are pooled, never instantiated once per note or once per frame.
    internal sealed class BattleThemeView
    {
        private readonly BattleVisualTheme theme;
        private readonly FiveLaneBattle battle;
        private readonly FiveLaneTrackGraphic tracks;
        private readonly RectTransform notes;
        private readonly List<Image> pool = new List<Image>();
        private int used;

        public static bool AddArena(RunUI ui, RectTransform parent, BattleVisualTheme theme)
        {
            if (theme == null || !theme.useArena || theme.arena == null) return false;
            var sky = ui.Rect("Cathedral sky", parent); RunUI.Stretch(sky); ui.Background(sky, theme.sky);
            if (theme.skyClouds != null)
            {
                var clouds = ui.Rect("Distant clouds", parent); RunUI.Stretch(clouds);
                var cloudImage = clouds.gameObject.AddComponent<RawImage>(); cloudImage.texture = theme.skyClouds; cloudImage.raycastTarget = false;
                var cloudFit = clouds.gameObject.AddComponent<AspectRatioFitter>();
                cloudFit.aspectRatio = (float)theme.skyClouds.width / theme.skyClouds.height; cloudFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            }
            var arena = ui.Rect("Cathedral arena", parent); RunUI.Stretch(arena);
            var image = arena.gameObject.AddComponent<RawImage>(); image.texture = theme.arena; image.raycastTarget = false;
            var fit = arena.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectRatio = (float)theme.arena.width / theme.arena.height; fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            return true;
        }

        public BattleThemeView(RunUI ui, FiveLaneHudBindings hud, FiveLaneBattle battle, FiveLaneTrackGraphic tracks, BattleVisualTheme theme)
        {
            this.theme = theme; this.battle = battle; this.tracks = tracks;
            var chrome = ui.Rect("Battle frame", hud.transform); RunUI.Stretch(chrome);
            chrome.SetSiblingIndex(hud.actors.GetSiblingIndex());
            chrome.gameObject.AddComponent<BattleFrameGraphic>().raycastTarget = false;
            var title = ui.Label(chrome, "BBSB", 48, RunUI.TextColor);
            title.fontSize = 48; title.alignment = TextAlignmentOptions.Left;
            title.fontStyle = FontStyles.Bold | FontStyles.Italic;
            FiveLaneHudBindings.Place(title.rectTransform, .025f, .87f, .19f, .98f);
            var subtitle = ui.Label(chrome, "R H Y T H M   R O G U E L I K E", 11, RunUI.Muted);
            subtitle.fontSize = 11; subtitle.alignment = TextAlignmentOptions.Left;
            FiveLaneHudBindings.Place(subtitle.rectTransform, .027f, .85f, .22f, .88f);
            if (theme != null)
            {
                Picture(ui, chrome, "Combo ornament", theme.comboCrest, .015f, .332f, .19f, .375f);
                Picture(ui, chrome, "Beat ring", theme.beatRing, .85f, .305f, 1, .535f);
                foreach (var lane in battle.Lanes)
                    foreach (int slot in lane.InputSlots)
                    {
                        var weapon = hud.weaponRoots[slot];
                        var halo = Picture(ui, chrome, "Weapon halo " + slot, theme.weaponHalo,
                            weapon.anchorMin.x - .015f, weapon.anchorMin.y - .02f, weapon.anchorMax.x + .015f, weapon.anchorMax.y + .02f);
                        if (halo != null) halo.color = new Color(1, 1, 1, .38f);
                    }
                Frame(ui, (RectTransform)hud.playerFill.parent, theme.healthFrame);
            }
            notes = ui.Rect("Crystal note sprites", tracks.transform); RunUI.Stretch(notes);
        }

        public static void Frame(RunUI ui, RectTransform bar, Sprite sprite)
        {
            if (sprite == null) return;
            // The generated bezel includes transparent padding. Map its central
            // opening onto the existing dynamic fill, rather than covering the HP.
            var image = Picture(ui, bar, "Health bezel", sprite, -.19f, -4.98f, 1.19f, 5.70f);
            image.preserveAspect = false;
        }
        private static Image Picture(RunUI ui, Transform parent, string name, Sprite sprite, float x0, float y0, float x1, float y1)
        {
            if (sprite == null) return null;
            var rect = ui.Rect(name, parent); FiveLaneHudBindings.Place(rect, x0, y0, x1, y1);
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
            return image;
        }

        public void Refresh()
        {
            if (theme == null) return;
            used = 0;
            foreach (var note in tracks.NoteTimeline.Notes)
            {
                var sprite = theme.NoteSprite(note.Beat);
                if (!note.IsConnected) Head(sprite, note.Slot, note.Beat, note.IsPreview);
                if (note.IsHold && !note.ConnectsNext && SteppedNoteTrack.InHorizon(note.EndBeat - battle.Beat))
                    Head(sprite, note.Slot, note.EndBeat, note.IsPreview);
            }
            foreach (int slot in BattleInputLayout.DisplayOrder)
            {
                var lane = battle.LaneAt(slot); if (lane == null) continue;
                foreach (var attack in battle.Incoming)
                {
                    if (!EnemyAttackNotes.Visible(battle, attack, slot)) continue;
                    var sprite = theme.enemyAttackNote;
                    bool blocked = attack.State == IncomingAttackState.Blocked;
                    Head(sprite, slot, attack.Beat, blocked);
                    if (EnemyAttackNotes.TailVisible(battle, attack)) Head(sprite, slot, attack.EndBeat, blocked);
                }
                var contact = battle.ShieldAt(slot);
                double judged = contact != null ? contact.LastJudgedBeat : lane.LastJudgedBeat;
                var grade = contact != null ? contact.Grade : lane.LastGrade;
                double age = battle.Beat - judged;
                if (grade == RhythmGrade.Miss || age < 0 || age >= .45) continue;
                float fade = 1 - (float)(age / .45);
                Draw(theme.judgmentFlash, tracks.NoteWorldPosition(slot, battle.Beat),
                    tracks.NoteWidth(slot, battle.Beat) * (1.4f - fade * .35f), fade * .85f);
            }
            for (int i = used; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
        }
        private void Head(Sprite sprite, int slot, double beat, bool preview) =>
            Draw(sprite, tracks.NoteWorldPosition(slot, beat), tracks.NoteWidth(slot, beat), preview ? .35f : 1);
        private void Draw(Sprite sprite, Vector3 world, float width, float alpha)
        {
            if (sprite == null || used >= FiveLaneNoteTimeline.MaxNotesPerLane * BattleInputLayout.LaneCount * 4 + 7) return;
            if (used == pool.Count)
            {
                var go = new GameObject("Pooled note art", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(notes, false); var added = go.GetComponent<Image>(); added.raycastTarget = false;
                added.rectTransform.anchorMin = added.rectTransform.anchorMax = new Vector2(.5f, .5f); pool.Add(added);
            }
            var image = pool[used++]; image.gameObject.SetActive(true); image.sprite = sprite;
            image.color = new Color(1, 1, 1, alpha);
            image.rectTransform.anchoredPosition = (Vector2)notes.InverseTransformPoint(world) - notes.rect.center;
            image.rectTransform.sizeDelta = new Vector2(width, width * sprite.rect.height / sprite.rect.width);
        }
    }

}
