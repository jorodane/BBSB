using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;

namespace BBSB.Runtime.UI
{
    // A bounded reusable set of visual actors, sampled from original results on every refresh.
    // There are no coroutines, damage callbacks or accumulated emission timers.
    public sealed class BattleVfxView : MonoBehaviour
    {
        private const int MaximumSprites = 96;
        private readonly List<BattleVfxGraphic> pool = new List<BattleVfxGraphic>();
        private RectTransform area;
        private int used;
        public int ActiveCount => used;
        public int PoolCount => pool.Count;
        internal void Refresh(RhythmRound round, Vector2 hero, float heroHeight, WeaponBattleGraphic weapons,
            IReadOnlyDictionary<string, Vector2> targets)
        {
            if (area == null) area = (RectTransform)transform;
            used = 0; double seconds = round.ElapsedSeconds;
            if (area.rect.width <= 0 || area.rect.height <= 0) { HideUnused(); return; }
            // Reaction effects are first so dense volleys can never hide the player's result.
            for (int i = round.Results.Count - 1; i >= 0; i--)
            {
                var result = round.Results[i]; double age = seconds - result.JudgedAtSeconds;
                if (age >= .30) break;
                if (age < 0) continue;
                float p = (float)(age / .30), fade = 1 - p;
                var definition = MonsterAttackCatalog.For(result.Note);
                Color tint = result.Grade == RhythmGrade.Perfect ? RunUI.Gold : RunUI.Teal;
                if (result.BlockedDamage > 0)
                    Emit("ring", hero, Vector2.one * heroHeight * (.42f + p * .25f), 0, RunUI.Teal, fade);
                if (result.DamageTaken > 0)
                {
                    Emit("impact", hero, Vector2.one * heroHeight * (.38f + p * .24f), 12, RunUI.Red, fade);
                    Emit("parry", hero, Vector2.one * heroHeight * (.18f + p * .20f), -20, Color.white, fade * .8f);
                }
                if (result.Grade == RhythmGrade.Miss) continue;
                string key = BattleVfxCatalog.Reaction(definition.Shape, result.Note.Step.Kind);
                Emit(key, hero, Vector2.one * heroHeight * (.32f + p * .26f), p * 35, tint, fade);
                if (definition.Reaction == MonsterAttackReaction.Burst || definition.Reaction == MonsterAttackReaction.Scatter)
                    for (int chip = 0; chip < 3; chip++)
                    {
                        float angle = (chip * 100 + 30) * Mathf.Deg2Rad;
                        var point = hero + new Vector2(Mathf.Cos(angle) * .055f, Mathf.Sin(angle) * .09f) * p;
                        Emit("parry", point, Vector2.one * heroHeight * .09f * (1 - p * .6f), chip * 70 + p * 120, tint, fade);
                    }
            }
            var combat = round.Combat;
            if (combat != null && weapons != null)
            {
                for (int i = combat.Activations.Count - 1; i >= 0; i--)
                {
                    var hit = combat.Activations[i]; double age = seconds - hit.AtSeconds;
                    if (age > 1.1) break;
                    if (age < 0 || !targets.TryGetValue(hit.Target.MonsterId, out var target)) continue;
                    var tint = PatternOverviewGraphic.ActionColor(hit.Action.Kind);
                    double impactAt = WeaponMotion.ImpactSeconds(hit.Action.Motion);
                    float power = Mathf.Clamp(.75f + (float)hit.Damage / 55, .75f, 1.6f);
                    if (hit.Weapon.IsRanged)
                    {
                        var muzzle = weapons.ProjectileOrigin(hit.Slot, hit.Weapon.Kind, target, hit.AtSeconds);
                        float angle = Angle(muzzle, target);
                        if (age < .10) Emit("muzzle", muzzle, Vector2.one * heroHeight * .27f * power,
                            angle, tint, 1 - (float)age / .10f);
                        if (age < impactAt)
                        {
                            double p = age / impactAt; int count = BattleVfxCatalog.ProjectileCount(hit.Action.Motion);
                            for (int shot = 0; shot < count; shot++)
                            {
                                int lane = count == 1 ? 0 : shot - 1;
                                var point = RangedWeaponTimeline.Projectile(new BattlePathPoint(muzzle.x, muzzle.y),
                                    new BattlePathPoint(target.x, target.y), p, lane);
                                var previous = RangedWeaponTimeline.Projectile(new BattlePathPoint(muzzle.x, muzzle.y),
                                    new BattlePathPoint(target.x, target.y), System.Math.Max(0, p - .04), lane);
                                var at = new Vector2((float)point.X, (float)point.Y);
                                float flightAngle = Angle(new Vector2((float)previous.X, (float)previous.Y), at);
                                Emit(BattleVfxCatalog.Projectile(hit.Weapon.Kind), at,
                                    new Vector2(hit.Weapon.Kind == WeaponKind.Wand ? .23f : .38f, .23f) * heroHeight * power,
                                    flightAngle, tint, 1);
                            }
                        }
                    }
                    else if (age < impactAt && hit.Damage > 0)
                    {
                        var frame = weapons.AttackFrame(hit, target, seconds);
                        Emit(BattleVfxCatalog.Strike(hit.Action.Motion), new Vector2((float)frame.Position.X, (float)frame.Position.Y),
                            Vector2.one * heroHeight * .42f * power, (float)frame.Rotation, tint, .7f);
                    }
                    float impact = (float)BattleVfxCatalog.Progress(age - impactAt, BattleVfxCatalog.ImpactDuration);
                    if (impact >= 0 && hit.Damage > 0)
                    {
                        Emit("impact", target, Vector2.one * heroHeight * (.28f + impact * .34f) * power, hit.Slot * 27, tint, 1 - impact);
                        string key = hit.Weapon.IsRanged ? (hit.Weapon.Kind == WeaponKind.Wand ? "ring" : "pierce") : BattleVfxCatalog.Strike(hit.Action.Motion);
                        Emit(key, target, Vector2.one * heroHeight * (.40f + impact * .30f) * power, hit.Slot * 43 - 25, Color.white, (1 - impact) * .9f);
                    }
                    if (age < .30 && hit.Guard > 0)
                        Emit("ring", hero, new Vector2(.55f, .70f) * heroHeight * (1 + (float)age), 0, RunUI.Teal, 1 - (float)age / .30f);
                }
                for (int slot = 0; slot < combat.Loadout.Equipment.Count; slot++)
                {
                    var frame = RangedWeaponTimeline.Evaluate(combat, slot, seconds, round.BeatSeconds);
                    if (frame.Pose != RangedWeaponPose.Prepare) continue;
                    var origin = weapons.MuzzlePosition(slot);
                    Emit("ring", origin, Vector2.one * heroHeight * (.14f + (float)frame.Tension * .09f),
                        (float)(seconds * 140), RunUI.Gold, .3f + (float)frame.Tension * .4f);
                }
            }
            HideUnused();
        }
        private float Angle(Vector2 from, Vector2 to)
        { var delta = Vector2.Scale(to - from, area.rect.size); return Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg; }
        private void Emit(string key, Vector2 point, Vector2 size, float angle, Color tint, float alpha)
        {
            if (used >= MaximumSprites || alpha <= 0) return;
            if (used == pool.Count)
            {
                var node = new GameObject("Battle VFX " + used, typeof(RectTransform), typeof(CanvasRenderer));
                node.transform.SetParent(transform, false);
                var image = node.AddComponent<BattleVfxGraphic>();
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.zero;
                pool.Add(image);
            }
            var graphic = pool[used++]; graphic.gameObject.SetActive(true);
            tint.a = Mathf.Clamp01(alpha); graphic.Bind(key, tint);
            var rect = graphic.rectTransform; rect.anchoredPosition = Vector2.Scale(point, area.rect.size);
            rect.sizeDelta = size; rect.localRotation = Quaternion.Euler(0, 0, angle);
        }
        private void HideUnused()
        { for (int i = used; i < pool.Count; i++) pool[i].gameObject.SetActive(false); }
    }
}
