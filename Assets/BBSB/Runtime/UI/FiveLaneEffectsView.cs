using System;
using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    // All artwork comes from serialized references. The pool is presentation only;
    // no effects, timing curves or previews can alter a battle judgment.
    internal sealed class FiveLaneEffectsView
    {
        private readonly ShoulderViewPresentation art;
        private readonly FiveLaneBattle battle;
        private readonly RectTransform root, player;
        private readonly FiveLaneTrackGraphic tracks;
        private readonly FiveLaneWeaponsView weapons;
        private readonly List<RectTransform> enemies;
        private readonly List<string> instances;
        private readonly List<Sprite> projectiles;
        private readonly List<Image> pool = new List<Image>();
        private int used;

        public FiveLaneEffectsView(ShoulderViewPresentation art, FiveLaneBattle battle, RunUI ui, FiveLaneHudBindings hud,
            RectTransform playerBody, FiveLaneTrackGraphic tracks, List<RectTransform> enemies, List<string> instances, List<string> species, FiveLaneWeaponsView weapons)
        {
            this.art = art; this.battle = battle; this.tracks = tracks; this.enemies = enemies; this.instances = instances; this.weapons = weapons;
            player = playerBody;
            root = ui.Rect("Battle sprite effects", hud.transform); RunUI.Stretch(root);
            // Above bodies and note meshes, below health, labels and input surfaces.
            root.SetSiblingIndex(hud.tracks.GetSiblingIndex() + 1);
            projectiles = new List<Sprite>(); var mapped = new HashSet<string>();
            for (int i = 0; i < species.Count; i++)
            {
                var sprite = art != null ? art.FindProjectile(species[i]) : null;
                projectiles.Add(sprite); if (sprite != null) mapped.Add(instances[i]);
            }
            tracks.SpriteProjectiles = mapped;
            tracks.SpriteNoteShatter = art != null && art.noteShatter != null;
        }

        public void Refresh()
        {
            if (art == null) return;
            used = 0;
            Vector2 target = At(player, .5f, .53f);
            Vector2 guard = target + Vector2.right * (Vector2.Distance(At(player, .5f, 0), At(player, .5f, 1)) * .30f);
            foreach (var lane in battle.Lanes)
                if (FiveLaneArtTimeline.Guarding(lane))
                { Draw(art.guard, guard, .23f, .58f); break; }
            foreach (var contact in battle.ShieldContacts)
                if (contact.Phase == PhraseLanePhase.Playing) { Draw(art.guard, guard, .25f, .58f); break; }
            foreach (var attack in battle.Incoming)
            {
                int index = instances.IndexOf(attack.Definition.MonsterId);
                if (index < 0) continue;
                double remaining = attack.Beat - battle.Beat;
                if (attack.Definition.IsHold && attack.State == IncomingAttackState.Pending &&
                    battle.Beat >= attack.Beat && battle.Beat <= attack.EndBeat)
                {
                    Flurry(projectiles[index], target, battle.Beat - attack.SustainStartBeat, .20f);
                }
                else if (attack.State == IncomingAttackState.Pending && remaining <= 1 && remaining >= -battle.HalfMissWindow)
                {
                    bool chained = FiveLaneAttackMotion.ContinuesEnemy(battle, attack);
                    if (chained && remaining > 0) continue;
                    Vector2 source = At(enemies[index], .5f, .46f);
                    float t = chained ? 1 : FiveLaneArtTimeline.ProjectileProgress(attack.Beat, battle.Beat);
                    float swing = (attack.SequenceIndex % 2 == 0 ? 1 : -1) * Mathf.Clamp01((float)(-remaining / .22)) * 100;
                    Draw(projectiles[index], Vector2.Lerp(source, target, t), Mathf.Lerp(.075f, .18f, t), 1,
                        Direction(target - source) - 225 + swing);
                }
                else if (attack.State == IncomingAttackState.Blocked && FiveLaneArtTimeline.Recent(battle.Beat, attack.ResolvedBeat, .45))
                    Burst(art.parry, guard, attack.ResolvedBeat, .27f, .45);
                if (attack.Definition.IsHold && FiveLaneArtTimeline.Recent(battle.Beat, attack.LastParryBeat, .45))
                    Burst(art.parry, guard, attack.LastParryBeat, .27f, .45);
            }
            if (FiveLaneArtTimeline.Recent(battle.Beat, battle.LastHitBeat, .4))
                Burst(art.impact, target, battle.LastHitBeat, .2f, .4);
            for (int i = 0; i < battle.Lanes.Count && enemies.Count > 0; i++)
            {
                var lane = battle.Lanes[i]; var played = lane.LastPerformedNote;
                if (played == null || played.Canceled) continue;
                double age = battle.Beat - played.StartedAtBeat;
                double impact = FiveLaneAttackMotion.PlayerContactBeats(battle, lane);
                int victim = instances.IndexOf(FiveLaneWeaponMotion.Target(battle, lane));
                Vector2 enemy = At(enemies[victim >= 0 ? victim : i % enemies.Count], .5f, .48f);
                bool ranged = WeaponCatalog.Find(lane.Weapon.DefinitionId).IsRanged;
                Vector2 origin = root.InverseTransformPoint(weapons.Origin(lane));
                bool support = played.Definition.Effect != PhraseEffect.Strike ||
                    played.Definition.Damage == 0 && lane.LastDamageBeat != played.CompletedAtBeat;
                float direction = played.Ordinal % 2 == 0 ? 1 : -1;
                if (played.Definition.IsHold)
                {
                    if (FiveLaneAttackMotion.SustainingPlayer(battle, lane))
                        Flurry(support ? art.guard : ranged ? art.arrow : art.slash,
                            support ? origin : enemy, battle.Beat - played.SustainStartedAtBeat, support ? .16f : .23f);
                    if (!support && FiveLaneArtTimeline.Recent(battle.Beat, played.CompletedAtBeat, .35))
                        Burst(art.impact, enemy, played.CompletedAtBeat, .16f, .35);
                    continue;
                }
                if (age < 0 || age >= impact + .35) continue;
                if (support)
                {
                    if (age >= impact) Burst(art.guard != null ? art.guard : art.impact, origin,
                        played.StartedAtBeat + impact, .18f, .35);
                    continue;
                }
                if (ranged && age < impact)
                    Draw(art.arrow, Vector2.Lerp(origin, enemy, (float)(age / impact)), .12f, 1, Direction(enemy - origin) - 45);
                if (age >= impact)
                {
                    if (!ranged) Burst(art.slash, enemy, played.StartedAtBeat + impact, .26f, .35, direction * 55);
                    Burst(art.impact, enemy, played.StartedAtBeat + impact, .14f, .35);
                }
            }
            foreach (var note in tracks.NoteTimeline.Confirmed)
            {
                var position = (Vector2)root.InverseTransformPoint(tracks.NoteWorldPosition(note.Note.Slot, note.Note.Beat));
                Burst(art.noteConfirm, position, note.ConfirmedAt, .1f, .4);
            }
            foreach (var note in tracks.NoteTimeline.Broken)
            {
                if (!note.Note.IsPreview) continue;
                var position = (Vector2)root.InverseTransformPoint(tracks.BrokenWorldPosition(note));
                Burst(art.noteShatter, position, note.BrokenAt, .13f, BrokenTrackNote.LifetimeBeats);
            }
            for (int i = used; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
        }
        private Vector2 At(RectTransform rect, float x, float y) => root.InverseTransformPoint(rect.TransformPoint(
            new Vector2(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, x), Mathf.Lerp(rect.rect.yMin, rect.rect.yMax, y))));
        private static float Direction(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        private void Flurry(Sprite sprite, Vector2 point, double age, float height)
        {
            // Reuse the authored transparent sprite for a continuous stroke and two fading trails.
            for (int i = 2; i >= 0; i--)
            {
                float turn = (float)(age * 720 - i * 45);
                float angle = turn * Mathf.Deg2Rad;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (root.rect.height * .015f);
                Draw(sprite, point + offset, height * (1 - i * .08f), 1 - i * .3f, turn);
            }
        }
        private void Burst(Sprite sprite, Vector2 point, double at, float height, double duration, float rotation = 0)
        {
            float t = Mathf.Clamp01((float)((battle.Beat - at) / duration));
            Draw(sprite, point, height * Mathf.Lerp(.7f, 1.25f, t), 1 - t, rotation);
        }
        private void Draw(Sprite sprite, Vector2 point, float height, float alpha, float rotation = 0)
        {
            if (sprite == null || used >= 256) return;
            if (used == pool.Count)
            {
                var go = new GameObject("Sprite effect", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(root, false); var image = go.GetComponent<Image>();
                image.raycastTarget = false; image.preserveAspect = true;
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
                pool.Add(image);
            }
            var item = pool[used++]; item.gameObject.SetActive(true); item.name = sprite.name + " effect";
            item.sprite = sprite; item.color = new Color(1, 1, 1, alpha);
            item.rectTransform.anchoredPosition = point - root.rect.center;
            item.rectTransform.sizeDelta = Vector2.one * (root.rect.height * height);
            item.rectTransform.localRotation = Quaternion.Euler(0, 0, rotation);
        }
    }
}
