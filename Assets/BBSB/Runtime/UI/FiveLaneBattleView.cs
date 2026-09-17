using System;
using System.Collections.Generic;
using BBSB.Core;
using TMPro;
using UnityEngine;

namespace BBSB.Runtime.UI
{
    internal sealed class FiveLaneBattleView
    {
        private readonly FiveLaneBattle battle;
        private readonly RunUI ui;
        private readonly RectTransform parent;
        private readonly FiveLaneHudBindings hud;
        private readonly FiveLaneTrackGraphic tracks;
        private readonly WeaponIconGraphic[] icons = new WeaponIconGraphic[BattleInputLayout.LaneCount];
        private readonly List<ActorPrefabView> monsters = new List<ActorPrefabView>();
        private readonly List<string> monsterIds = new List<string>();
        private readonly ActorPrefabView player;
        private readonly FiveLaneEffectsView effects;
        private readonly List<StageActor> stageActors = new List<StageActor>();
        private readonly List<TMP_Text> attackLabels = new List<TMP_Text>();
        private RectTransform modal;
        private static readonly string[] Keys = { "D", "F", "SPACE", "J", "K", "S", "L" };

        private sealed class StageActor
        {
            public RectTransform Slot, Visual;
            public float Scale, Aspect;
            public Vector2 Offset;
            public void Layout()
            {
                float height = Mathf.Min(Slot.rect.height, Slot.rect.width / Mathf.Max(.1f, Aspect));
                Visual.localScale = Vector3.one * (height * Scale / 512);
                Visual.anchoredPosition = Offset * height;
            }
        }

        public FiveLaneBattleView(RectTransform parent, RunUI ui, RunSession session, FiveLanePlayback playback)
        {
            this.parent = parent; this.ui = ui; battle = playback.Battle;
            var screen = parent.GetComponentInParent<CanvasScreen>();
            var authored = screen != null ? screen.fiveLane : parent.GetComponentInChildren<FiveLaneHudBindings>();
            if (authored != null && !authored.TryValidate(out var problem))
            {
                Debug.LogWarning(problem + " Using the built-in battle layout for this instance.", authored);
                if (authored.transform != parent && authored.transform.IsChildOf(parent)) authored.gameObject.SetActive(false);
                authored = null;
            }
            hud = authored != null ? authored : FiveLaneHudBindings.CreateDefault(parent, ui.Font);
            hud.EnsureStageLayout();
            hud.ConfigureLanes(battle);
            var inputNames = new List<string>();
            foreach (int slot in BattleInputLayout.DisplayOrder) if (battle.IsInputAvailable(slot)) inputNames.Add(Keys[slot]);
            hud.help.text = string.Join(" / ", inputNames) + "  ·  Tap / Hold  ·  Esc";
            var presentation = hud.presentation != null ? hud.presentation : Resources.Load<ShoulderViewPresentation>(ShoulderViewPresentation.ResourcePath);
            StageScenery.Add(ui, hud.scenery, session.BattleMusic.Music, "Stage art", presentation);
            var shade = ui.Rect("Scene shade", hud.scenery); RunUI.Stretch(shade); ui.Background(shade, new Color(.025f, .035f, .08f, .16f));
            // A prefab slot may already have an Image. Unity permits only one Graphic per
            // object, so runtime meshes get their own children instead of AddComponent failing.
            var trackMesh = ui.Rect("Live note tracks", hud.tracks); RunUI.Stretch(trackMesh);
            tracks = trackMesh.gameObject.AddComponent<FiveLaneTrackGraphic>();
            tracks.Bind(battle, hud.judgmentPoints, hud.playerSlot, hud.monsterArea);
            hud.pause.onClick.AddListener(playback.Pause);
            hud.song.text = session.BattleMusic.Music.Name + "\n" + battle.Bpm + " BPM";
            for (int i = 0; i < BattleInputLayout.LaneCount; i++)
            {
                var lane = battle.LaneAt(i);
                hud.laneLabels[i].text = Keys[i] + "\n" + (lane == null ? "비어 있음" : lane.Patterns.Starts[lane.Placement.OffsetOf(i)].Name);
                hud.laneLabels[i].color = lane == null ? RunUI.Muted : RunUI.TextColor;
                if (lane == null) continue;
                var weaponMesh = ui.Rect("Live weapon " + Keys[i], hud.weaponRoots[i]); RunUI.Stretch(weaponMesh);
                icons[i] = weaponMesh.gameObject.AddComponent<WeaponIconGraphic>();
                icons[i].FitVisibleArtwork = true; icons[i].Bind(lane.Weapon);
                // The old gesture sockets described automatic responses. This screen shows the weapon's phrase instead.
                foreach (var sockets in icons[i].GetComponentsInChildren<WeaponSocketGraphic>()) sockets.gameObject.SetActive(false);
                var input = hud.inputAreas[i].GetComponent<FiveLaneInputSurface>();
                if (input == null) input = hud.inputAreas[i].gameObject.AddComponent<FiveLaneInputSurface>();
                input.Bind(playback, i);
            }
            var hero = PlayerCharacterRegistry.Find(session.CharacterId);
            Sprite heroPortrait = hero != null ? hero.portrait : null;
            if (heroPortrait == null)
                foreach (var sprite in Resources.LoadAll<Sprite>(PlayerMotionSprites.ResourcePath + "idle"))
                    if (sprite.name == "idle_0") { heroPortrait = sprite; break; }
            if (heroPortrait == null) heroPortrait = Resources.Load<Sprite>("BBSB/BattleArt/weapon-master");
            player = Actor("Player", hud.playerSlot, 0, 1, hero != null ? hero.visualPrefab : null,
                hero != null ? hero.controller : null, heroPortrait,
                hero != null ? hero.spriteReferenceHeight : 4, hero != null ? hero.displayScale : 1,
                hero != null ? hero.displayOffset : Vector2.zero);
            int count = session.BattlePlan.Monsters.Count;
            var species = new List<string>();
            var enemySlots = new List<RectTransform>();
            for (int i = 0; i < count; i++)
            {
                var plan = session.BattlePlan.Monsters[i];
                var appearance = MonsterAuthoringRegistry.Find(plan.Monster.Id);
                var prefab = appearance != null ? appearance.actorPrefab != null ? appearance.actorPrefab :
                    appearance.visualPrefab != null ? appearance.visualPrefab.gameObject : null : null;
                monsters.Add(Actor(plan.Monster.Name, hud.monsterArea, i, count,
                    prefab, appearance != null ? appearance.controller : null,
                    appearance != null && appearance.portrait != null ? appearance.portrait : Resources.Load<Sprite>("BBSB/BattleArt/" + plan.Monster.ArtId),
                    appearance != null ? appearance.spriteReferenceHeight : 4, appearance != null ? appearance.displayScale : 1,
                    appearance != null ? appearance.displayOffset : Vector2.zero));
                monsterIds.Add(plan.InstanceId);
                species.Add(plan.Monster.Id); enemySlots.Add(stageActors[i + 1].Slot);
                var cue = ui.Label(hud.actors, "", 20, RunUI.Red);
                cue.alignment = TextAlignmentOptions.Center;
                float x0 = Mathf.Lerp(hud.monsterArea.anchorMin.x, hud.monsterArea.anchorMax.x, (float)i / count);
                float x1 = Mathf.Lerp(hud.monsterArea.anchorMin.x, hud.monsterArea.anchorMax.x, (float)(i + 1) / count);
                FiveLaneHudBindings.Place(cue.rectTransform, x0, .85f, x1, .89f);
                attackLabels.Add(cue);
            }
            effects = new FiveLaneEffectsView(presentation, battle, ui, hud, tracks, enemySlots, monsterIds, species);
        }
        private ActorPrefabView Actor(string name, RectTransform stage, int index, int count, GameObject prefab,
            RuntimeAnimatorController controller, Sprite sprite, float reference, float scale, Vector2 offset)
        {
            var root = ui.Rect(name, stage);
            float gap = count > 1 ? .02f : 0;
            FiveLaneHudBindings.Place(root, (float)index / count + gap, 0, (float)(index + 1) / count - gap, 1);
            var visual = ui.Rect("Visual", root); visual.anchorMin = visual.anchorMax = visual.pivot = new Vector2(.5f, 0);
            visual.anchoredPosition = Vector2.zero; visual.sizeDelta = new Vector2(512, 512);
            var actor = visual.gameObject.AddComponent<ActorPrefabView>(); actor.Initialize(prefab, controller, sprite, reference);
            var placement = new StageActor { Slot = root, Visual = visual, Scale = Mathf.Max(.01f, scale), Offset = offset,
                Aspect = sprite != null ? sprite.rect.width / sprite.rect.height : 1 };
            placement.Layout(); stageActors.Add(placement);
            return actor;
        }
        public void Refresh(int countdown, bool waitingForHold)
        {
            foreach (var actor in stageActors) actor.Layout();
            hud.health.text = "HP " + battle.PlayerHealth.ToString("0.#") + " / " + battle.PlayerMaximum;
            hud.enemyHealth.text = "ENEMY " + battle.EnemyHealth.Current.ToString("0.#") + " / " + battle.EnemyHealth.Maximum;
            hud.playerFill.anchorMax = new Vector2((float)(battle.PlayerHealth / battle.PlayerMaximum), 1);
            hud.enemyFill.anchorMax = new Vector2((float)(battle.EnemyHealth.Current / battle.EnemyHealth.Maximum), 1);
            hud.beat.text = countdown > 0 ? "COUNT IN\n" + countdown : battle.Combo + "\nCOMBO";
            hud.beat.color = battle.Beat % 1 < .18 ? RunUI.Gold : RunUI.TextColor;
            hud.feedback.text = waitingForHold ? "Hold 중이던 버튼을 다시 눌러줘" : battle.IsGroggy ? "GROGGY" :
                battle.Beat - battle.LastHitBeat < .5 ? "HIT" : "";
            hud.feedback.color = battle.IsGroggy ? RunUI.Gold : RunUI.Red;
            for (int i = 0; i < BattleInputLayout.LaneCount; i++)
            {
                var lane = battle.LaneAt(i);
                if (lane == null) continue;
                hud.laneStatus[i].text = lane.Phase == PhraseLanePhase.Cooldown ? "CD " + Math.Max(0, lane.ReadyAtBeat - battle.Beat).ToString("0.0") :
                    lane.Phase == PhraseLanePhase.Ready ? "READY" :
                    lane.WaitingForParryRelease ? "HOLD → UP" : lane.Holding ?
                    "HOLD " + Math.Max(0, lane.NextBeat + lane.Phrase.Notes[lane.NextNote].HoldBeats - battle.Beat).ToString("0.0") :
                    "NOTE " + (lane.NextNote + 1) + "/" + lane.Phrase.Notes.Count +
                    (lane.Phrase.FinisherEvery > 0 ? " · " + (lane.CompletedPhrases % lane.Phrase.FinisherEvery + 1) + "/" + lane.Phrase.FinisherEvery : "");
                hud.laneResults[i].text = battle.Beat - lane.LastJudgedBeat < 1 ? lane.Feedback : "";
                foreach (var start in battle.ScheduledStarts)
                    if (start.Slot == i && start.State == ScheduledStartState.Pending)
                    {
                        hud.laneStatus[i].text += "\nCHIME " + Math.Max(0, start.Beat - battle.Beat).ToString("0.0");
                        break;
                    }
                hud.laneResults[i].color = lane.LastGrade == RhythmGrade.Miss ? RunUI.Red : lane.LastGrade == RhythmGrade.HalfMiss ? RunUI.Gold : RunUI.Teal;
                icons[i].color = lane.Phase == PhraseLanePhase.Cooldown ? new Color(.45f, .45f, .5f, .65f) : Color.white;
                // Beat-driven recoil gives each successful input a readable weapon response.
                double age = battle.Beat - lane.LastJudgedBeat;
                float pulse = age >= 0 && age < .4 && lane.LastGrade != RhythmGrade.Miss ? 1 - (float)(age / .4) : 0;
                hud.weaponRoots[i].localScale = Vector3.one * (1 + pulse * .18f);
                hud.weaponRoots[i].localRotation = Quaternion.Euler(0, 0, pulse * (i % 2 == 0 ? -24 : 24));
            }
            var heroFrame = FiveLaneArtTimeline.Player(battle);
            player.Sample(heroFrame.State, heroFrame.Age, heroFrame.Duration, heroFrame.Loop);
            double nearestAttack = double.PositiveInfinity;
            for (int i = 0; i < monsters.Count; i++)
            {
                double remaining = double.PositiveInfinity;
                foreach (var attack in battle.Incoming)
                    if (attack.Definition.MonsterId == monsterIds[i] && attack.State == IncomingAttackState.Pending &&
                        attack.Beat - battle.Beat >= -battle.HalfMissWindow && attack.Beat - battle.Beat <= 3)
                    { remaining = Math.Max(0, attack.Beat - battle.Beat); break; }
                nearestAttack = Math.Min(nearestAttack, remaining);
                var frame = FiveLaneArtTimeline.Monster(battle, monsterIds[i]);
                bool animated = monsters[i].Sample(frame.State, frame.Age, frame.Duration, frame.Loop);
                attackLabels[i].text = double.IsPositiveInfinity(remaining) ? "" : remaining <= battle.PerfectWindow ? "ATTACK!" : "ATTACK " + remaining.ToString("0.0");
                if (!animated)
                {
                    // Portrait-only enemies still telegraph the impact on the shared beat clock.
                    float windup = remaining <= 1 ? 1 - (float)remaining : 0;
                    stageActors[i + 1].Visual.localScale *= 1 + .06f * windup;
                    stageActors[i + 1].Visual.anchoredPosition += Vector2.up * (12 * windup);
                    monsters[i].RefreshSprites();
                }
            }
            hud.attackCue.text = countdown > 0 ? "" : double.IsPositiveInfinity(nearestAttack) ? "" :
                nearestAttack <= battle.PerfectWindow ? "PARRY NOW" : "PARRY IN " + nearestAttack.ToString("0.0") + " BEATS";
            hud.attackCue.color = nearestAttack <= .5 ? RunUI.Gold : RunUI.Red;
            player.RefreshSprites();
            tracks.Refresh();
            effects.Refresh();
        }
        public void ShowPause(Action resume, Action leave)
        {
            HideModal();
            modal = ui.Modal(parent, "Five lane pause", "일시정지", resume, out var content);
            ui.Label(content, hud.help.text + "\n단검은 2박마다, 쌍검은 1박마다. 긴 표시는 끝까지 유지.\n빨간 표시가 아래에 닿는 순간 방어해.", 22, null, 100);
            ui.Button(content, "이어하기", resume, primary: true);
            ui.Button(content, "준비 화면으로", leave);
        }
        public void ShowResult(Action finish)
        {
            HideModal();
            modal = ui.Modal(parent, "Five lane result", battle.Victory ? "STAGE CLEAR" : "GAME OVER", finish, out var content);
            ui.Label(content, "PERFECT " + battle.PerfectCount + "  ·  HALF " + battle.HalfMissCount + "  ·  MISS " + battle.MissCount, 24, null, 70);
            ui.Label(content, "피해 " + battle.TotalDamage.ToString("0.#") + "  ·  방어 " + (battle.TotalBlocked + battle.TotalReduced).ToString("0.#"), 22, null, 60);
            ui.Button(content, battle.Victory ? "보상 받기" : "결과 보기", finish, primary: true);
        }
        public void HideModal()
        { if (modal == null) return; modal.gameObject.SetActive(false); UnityEngine.Object.Destroy(modal.gameObject); modal = null; }
    }
}
