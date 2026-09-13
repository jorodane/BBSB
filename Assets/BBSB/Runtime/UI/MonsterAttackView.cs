using System;
using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    /// <summary>Reusable sprite slots driven by the same round in practice and battle.</summary>
    public sealed class MonsterAttackView : MonoBehaviour
    {
        private sealed class Slot
        {
            public ResponseNote Note;
            public MonsterAttackDefinition Definition;
            public MonsterAttackGraphic Image;
            public MonsterStageMotion Stage;
            public int Order;
        }
        private readonly List<Slot> slots = new List<Slot>();
        private RhythmRound round;
        private MonsterAttackDisplay display;
        private RectTransform area;
        public MonsterAttackSprites Sprites { get; } = new MonsterAttackSprites();
        public int ActiveCount { get; private set; }

        internal void Initialize(RhythmRound value, RunUI ui, MonsterAttackDisplay settings)
        {
            round = value; display = settings; area = (RectTransform)transform;
            var stages = new Dictionary<string, MonsterStageMotion>();
            foreach (var monster in round.Plan.Monsters)
                stages.Add(monster.InstanceId, new MonsterStageMotion(monster, round.Plan.Stage.Music.Bpm, round.HalfMissWindow));
            // One fixed slot per note avoids frame-triggered emissions, duplicate results and pooled identity leaks.
            foreach (var note in round.Notes)
            {
                MonsterPlan monster = null; int order = 0;
                for (int i = 0; i < round.Plan.Monsters.Count; i++)
                    if (round.Plan.Monsters[i].InstanceId == note.Attack.MonsterId) { monster = round.Plan.Monsters[i]; order = i; break; }
                if (monster == null) continue;
                var root = ui.Rect("Attack " + note.Attack.Id + "/step-" + note.StepIndex, area);
                root.anchorMin = root.anchorMax = Vector2.zero;
                var image = root.gameObject.AddComponent<MonsterAttackGraphic>();
                image.raycastTarget = false; image.gameObject.SetActive(false);
                slots.Add(new Slot { Note = note, Definition = MonsterAttackCatalog.For(note), Image = image,
                    Order = order, Stage = stages[monster.InstanceId] });
            }
        }

        internal Sprite Body(MonsterPlan monster, double seconds, Sprite fallback, out bool authoredPose)
        {
            return Sprites.Body(monster, seconds, round.BeatSeconds, fallback,
                round.Combat != null && round.Combat.Victory, out authoredPose);
        }

        internal void Repeat(RhythmRound value)
        {
            if (!ReferenceEquals(round.Plan, value.Plan) || slots.Count != value.Notes.Count)
                throw new InvalidOperationException("Attack slots must keep the same practice plan.");
            round = value;
            for (int i = 0; i < slots.Count; i++) slots[i].Note = value.Notes[i];
        }

        internal void Refresh(double seconds, Vector2 heroGround, float heroHeight)
        {
            ActiveCount = 0;
            Vector2 size = area.rect.size;
            if (size.x <= 0 || size.y <= 0) return;
            foreach (var slot in slots)
            {
                var definition = slot.Definition;
                var frame = MonsterAttackTimeline.Evaluate(slot.Note, definition, seconds, round.BeatSeconds, round.HalfMissWindow);
                bool visible = frame.Visible && (round.Combat == null || round.Combat.AllowsEnemyEffect(seconds));
                slot.Image.gameObject.SetActive(visible);
                if (!visible) continue;
                ActiveCount++;
                var calibration = display != null ? display.Find(definition) : null;
                var pose = calibration?.FindPose(frame.Phase);
                float scale = MonsterAttackDisplay.Positive(calibration?.scale ?? 1) * MonsterAttackDisplay.Positive(pose?.scale ?? 1);
                float height = (float)definition.Height * heroHeight * scale;
                var socket = display != null ? display.Socket(slot.Note.Step.Kind) : MonsterAttackDisplay.DefaultSocket(slot.Note.Step.Kind);
                var to = Vector2.Scale(heroGround, size) + (socket + (calibration?.targetOffset ?? Vector2.zero)) * heroHeight;
                double spawn = definition.SpawnSeconds(slot.Note, round.BeatSeconds);
                // A feather volley shares one origin as well as one travel duration; stepping forward
                // between Calls must not shorten later feathers' paths or change their speed.
                if (definition.Shape == MonsterAttackShape.Feather)
                    spawn = slot.Note.Attack.Call[0].Tick * round.BeatSeconds / RhythmTime.TicksPerBeat;
                // Detached shots retain the firing origin. Attached limbs continue to follow their owner.
                var stage = BattleStageLayout.Monster(slot.Order, round.Plan.Monsters.Count, heroGround.x, heroGround.y,
                    slot.Stage.Evaluate(definition.Stretch ? seconds : spawn));
                var ground = new Vector2((float)stage.X * size.x, (float)stage.Y * size.y);
                float monsterHeight = (float)BattleStageLayout.MonsterSize(round.Plan.Monsters.Count, size.x, size.y, stage.Scale);
                var from = ground + new Vector2(-.15f, slot.Note.Step.Kind == GestureKind.Dive ? .8f :
                    slot.Note.Step.Kind == GestureKind.Flick ? .12f : .48f) * monsterHeight;
                from += (calibration?.sourceOffset ?? Vector2.zero) * monsterHeight;
                if (definition.Grounded)
                {
                    from.y = ground.y + (calibration?.sourceOffset.y ?? 0) * monsterHeight;
                    // Ground art is drawn from its feet. Its raised hand / burst tip meets the punch socket.
                    to.y = heroGround.y * size.y + (calibration?.targetOffset.y ?? 0) * heroHeight;
                }
                float lane = (float)definition.LaneHeight(slot.Note) * heroHeight;
                from.y += lane; to.y += lane;
                float t = (float)frame.Progress;
                var point = Vector2.LerpUnclamped(from, to, t) + Vector2.up * (float)frame.Lift * heroHeight;
                float reaction = (float)frame.ReactionProgress;
                float alpha = frame.IsReaction ? 1 - reaction : 1;
                var pivot = definition.Grounded ? new Vector2(.5f, 0) : new Vector2(.5f, .5f);
                if (pose != null && pose.overridePivot) pivot = pose.pivot;
                float angle = pose?.rotation ?? 0;
                if (definition.Shape == MonsterAttackShape.Feather && definition.Motion == MonsterAttackMotion.WaitRush)
                    angle += (slot.Note.StepIndex - 1) * 22 * (1 - t);
                if (frame.MissApproach > 0)
                {
                    var landing = BattleTrajectory.MissLanding(new BattlePathPoint(to.x, to.y),
                        heroGround.y * size.y + (calibration?.targetOffset.y ?? 0) * heroHeight,
                        height, pivot.y, heroHeight, frame.MissApproach);
                    point = new Vector2((float)landing.X, (float)landing.Y);
                }
                if (frame.IsReaction)
                {
                    if (frame.Phase == MonsterAttackPhase.Perfect)
                    {
                        if (definition.Reaction == MonsterAttackReaction.Recoil || definition.Reaction == MonsterAttackReaction.Push)
                            point += new Vector2(.75f, .15f) * heroHeight * reaction;
                        if (definition.Reaction == MonsterAttackReaction.Withdraw) point = Vector2.Lerp(to, from, reaction);
                        if (definition.Reaction == MonsterAttackReaction.Burst || definition.Reaction == MonsterAttackReaction.Scatter) height *= 1 + reaction * .65f;
                    }
                    else if (frame.Phase == MonsterAttackPhase.HalfMiss) { point += new Vector2(-.28f, .28f) * heroHeight * reaction; angle += reaction * 55; }
                    else point += Vector2.left * heroHeight * reaction * .25f;
                }
                if (slot.Note.Step.Kind == GestureKind.Shake && frame.Phase == MonsterAttackPhase.Contact)
                    point += Vector2.right * heroHeight * (float)frame.ShakeProgress * .18f;

                var contact = MonsterAttackTimeline.ContactFeedback(slot.Note, seconds, round.BeatSeconds, round.HalfMissWindow);
                if (contact.Active && slot.Note.Step.Kind != GestureKind.Shake)
                {
                    float progress = (float)contact.Progress;
                    if (definition.Shape == MonsterAttackShape.Scale)
                    {
                        height *= 1 - progress * .16f;
                        point += Vector2.right * heroHeight * (progress * .1f + (float)contact.Pulse * .018f);
                        if (contact.IsEnding) point += Vector2.right * heroHeight * (float)contact.EndProgress * .5f;
                    }
                    else if (definition.Shape == MonsterAttackShape.Electric)
                        height *= (1 - progress * .65f) * (1 + (float)contact.Pulse * .14f);
                    else if (definition.Shape == MonsterAttackShape.Veil)
                    {
                        // The trailing edge passes toward the player's head. Its last edge
                        // clears the contact point exactly when the player should release.
                        from = Vector2.Lerp(from, to, progress); point = to;
                        if (contact.IsEnding) alpha = 0;
                    }
                    if (contact.IsEnding) alpha *= 1 - (float)contact.EndProgress;
                }
                else if ((slot.Note.Step.Kind == GestureKind.Hold || slot.Note.Step.Kind == GestureKind.Dive) && seconds >= slot.Note.EndSeconds)
                    alpha = 0;
                if (definition.Shape == MonsterAttackShape.Tail &&
                    (frame.Phase == MonsterAttackPhase.Wait || frame.Phase == MonsterAttackPhase.Spawn)) alpha *= .45f;

                float rate = MonsterAttackDisplay.Positive(calibration?.framesPerBeat ?? 2);
                var sprite = Sprites.Attack(definition, frame, rate, round.BeatSeconds, out bool exactPhase);
                float aspect = sprite != null ? sprite.rect.width / sprite.rect.height : definition.Shape == MonsterAttackShape.Doll ? .65f : 1;
                var rect = slot.Image.rectTransform;
                rect.pivot = pivot;
                rect.sizeDelta = new Vector2(height * aspect, height);
                if (definition.Stretch)
                {
                    Vector2 span = from - point;
                    rect.pivot = new Vector2(1, .5f);
                    rect.sizeDelta = new Vector2(Mathf.Max(height * .2f, span.magnitude), height);
                    point = from;
                    angle += Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg;
                }
                point += ((calibration?.imageOffset ?? Vector2.zero) + (pose?.offset ?? Vector2.zero)) * heroHeight;
                rect.anchoredPosition = point;
                rect.localRotation = Quaternion.Euler(0, 0, angle);
                slot.Image.SetFrame(definition, frame, sprite, exactPhase, alpha);
            }
        }
    }
}
