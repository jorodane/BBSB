using System;
using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    /// <summary>One monster, authored body poses and the battle's actual attack timeline, facing a response marker.</summary>
    public sealed class MonsterCodexStage : MonoBehaviour
    {
        private RunUI ui;
        private MonsterDefinition monster;
        private MonsterPreview preview;
        private MonsterAttackSprites sprites;
        private MonsterAttackDisplay display;
        private Image body, marker;
        private Text missing, markerLabel;
        private readonly List<MonsterAttackGraphic> attacks = new List<MonsterAttackGraphic>();
        private Sprite fallback;

        internal void Bind(RunUI ui, MonsterDefinition monster, MonsterAttackSprites sprites)
        {
            this.ui = ui; this.monster = monster; this.sprites = sprites;
            display = Resources.Load<MonsterAttackDisplay>(MonsterAttackDisplay.ResourcePath);
            fallback = Resources.Load<Sprite>("BBSB/BattleArt/" + monster.ArtId);
            var ground = ui.Rect("Ground", transform); ui.Background(ground, RunUI.Hex("39475B"));
            RunUI.Overlay(ground, new Vector2(.02f, .06f), new Vector2(.98f, .06f), Vector2.zero, new Vector2(0, 2));
            var portrait = ui.Rect("Monster body", transform);
            body = ui.Background(portrait, Color.white); body.preserveAspect = false;
            missing = ui.Label(transform, "모습 준비 중", 24, RunUI.Muted, 60, TextAnchor.MiddleCenter);
            RunUI.Stretch(missing.rectTransform);
            var target = ui.Rect("Response marker", transform);
            RunUI.Overlay(target, new Vector2(.14f, .08f), new Vector2(.14f, .64f), new Vector2(-2, 0), new Vector2(2, 0));
            marker = ui.Background(target, RunUI.Muted);
            markerLabel = ui.Label(transform, "반응 위치", 17, RunUI.Muted, 28, TextAnchor.MiddleCenter);
            RunUI.Overlay(markerLabel.rectTransform, new Vector2(0, .64f), new Vector2(.30f, .78f), Vector2.zero, Vector2.zero);
        }

        internal void Select(MonsterPreview value)
        {
            preview = value;
            foreach (var attack in attacks) { attack.gameObject.SetActive(false); Destroy(attack.gameObject); }
            attacks.Clear();
            if (preview != null) foreach (var note in preview.Round.Notes)
            {
                var root = ui.Rect("Preview step " + note.StepIndex, transform);
                var graphic = root.gameObject.AddComponent<MonsterAttackGraphic>(); graphic.raycastTarget = false;
                attacks.Add(graphic);
            }
            marker.gameObject.SetActive(value != null); markerLabel.gameObject.SetActive(value != null);
        }

        internal void Refresh(double seconds, bool response)
        {
            var size = ((RectTransform)transform).rect.size;
            if (size.x <= 0 || size.y <= 0) return;
            double beat = preview?.BeatSeconds ?? .5;
            bool authored = false;
            var portrait = preview == null ? sprites.Get(MonsterAttackDefinition.ResourceRoot + monster.Id, "idle", seconds / beat * 2) ?? fallback :
                sprites.Body(preview.Round.Plan.Monsters[0], seconds, beat, fallback, false, out authored);
            body.sprite = portrait; body.enabled = portrait != null; missing.enabled = portrait == null;
            float height = Mathf.Min(size.y * .91f, size.x * .52f);
            float aspect = portrait != null ? portrait.rect.width / portrait.rect.height : 1;
            var foot = new Vector2(size.x * .69f, size.y * .065f);
            var bodyPivot = portrait != null ? new Vector2(portrait.pivot.x / portrait.rect.width,
                portrait.pivot.y / portrait.rect.height) : new Vector2(.5f, 0);
            // Wide wings and spider legs must fit to either side of the authored foot pivot.
            height = Mathf.Min(height, (size.x * .98f - foot.x) / Mathf.Max(.001f, (1 - bodyPivot.x) * aspect));
            height = Mathf.Min(height, (foot.x - size.x * .02f) / Mathf.Max(.001f, bodyPivot.x * aspect));
            RunUI.Pin(body.rectTransform, Vector2.zero, bodyPivot, foot, new Vector2(height * aspect, height));
            // The fallback still gives a small grounded Call cue when new pose PNGs have not been installed.
            float squash = 0;
            if (!authored && preview != null) foreach (var call in preview.Attack.Call)
            {
                double age = seconds - call.Tick * beat / RhythmTime.TicksPerBeat;
                if (age >= 0 && age < beat * .4) squash = (float)Math.Sin(age / (beat * .4) * Math.PI) * .045f;
            }
            body.rectTransform.localScale = new Vector3(1 + squash, 1 - squash, 1);
            marker.color = response ? RunUI.Teal : RunUI.Hex("566578"); markerLabel.color = marker.color;
            markerLabel.text = response ? "지금 반응!" : "반응 위치";
            if (preview == null) return;
            for (int i = 0; i < attacks.Count; i++)
            {
                var note = preview.Round.Notes[i]; var definition = MonsterAttackCatalog.For(note);
                var frame = MonsterAttackTimeline.Evaluate(note, definition, seconds, beat, preview.Round.HalfMissWindow);
                var graphic = attacks[i]; graphic.gameObject.SetActive(frame.Visible);
                if (!frame.Visible) continue;
                var calibration = display != null ? display.Find(definition) : null;
                var pose = calibration?.FindPose(frame.Phase);
                var from = foot + new Vector2(-height * .17f, height * .48f);
                var to = new Vector2(size.x * .14f, foot.y + height * .36f);
                if (definition.Grounded) { from.y = foot.y; to.y = foot.y; }
                if (note.Step.Kind == GestureKind.Dive) { from.y = foot.y + height * .74f; to.y = foot.y + height * .62f; }
                if (note.Step.Kind == GestureKind.Flick) { from.y = foot.y + height * .15f; to.y = foot.y + height * .15f; }
                from += (calibration?.sourceOffset ?? Vector2.zero) * height;
                to += (calibration?.targetOffset ?? Vector2.zero) * height;
                var point = Vector2.Lerp(from, to, (float)frame.Progress) + Vector2.up * (float)frame.Lift * height;
                var sprite = sprites.Attack(definition, frame, MonsterAttackDisplay.Positive(calibration?.framesPerBeat ?? 2), beat, out bool exact);
                float h = height * (float)definition.Height * MonsterAttackDisplay.Positive(calibration?.scale ?? 1) *
                    MonsterAttackDisplay.Positive(pose?.scale ?? 1);
                float w = h * (sprite != null ? sprite.rect.width / sprite.rect.height : 1);
                var pivot = definition.Grounded ? new Vector2(.5f, 0) : new Vector2(.5f, .5f);
                if (pose != null && pose.overridePivot) pivot = pose.pivot;
                float angle = pose?.rotation ?? 0;
                if (definition.Stretch)
                {
                    var span = from - point; w = Mathf.Max(h * .2f, span.magnitude);
                    pivot = new Vector2(1, .5f); point = from; angle += Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg;
                }
                point += ((calibration?.imageOffset ?? Vector2.zero) + (pose?.offset ?? Vector2.zero)) * height;
                RunUI.Pin(graphic.rectTransform, Vector2.zero, pivot, point, new Vector2(w, h));
                graphic.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
                graphic.SetFrame(definition, frame, sprite, exact, 1);
            }
        }
    }
}
