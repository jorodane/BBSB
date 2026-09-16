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
        private readonly WeaponIconGraphic[] icons = new WeaponIconGraphic[5];
        private readonly List<ActorPrefabView> monsters = new List<ActorPrefabView>();
        private readonly List<string> monsterIds = new List<string>();
        private readonly ActorPrefabView player;
        private RectTransform modal;
        private static readonly string[] Keys = { "D", "F", "SPACE", "J", "K" };

        public FiveLaneBattleView(RectTransform parent, RunUI ui, RunSession session, FiveLanePlayback playback)
        {
            this.parent = parent; this.ui = ui; battle = playback.Battle;
            var screen = parent.GetComponentInParent<CanvasScreen>();
            hud = screen != null && screen.fiveLane != null ? screen.fiveLane : FiveLaneHudBindings.CreateDefault(parent, ui.Font);
            ValidateBindings();
            StageScenery.Add(ui, hud.scenery, session.BattleMusic.Music, "Stage art");
            var shade = ui.Rect("Scene shade", hud.scenery); RunUI.Stretch(shade); ui.Background(shade, new Color(.025f, .035f, .08f, .52f));
            tracks = hud.tracks.gameObject.AddComponent<FiveLaneTrackGraphic>(); tracks.Bind(battle, hud.weaponRoots);
            hud.pause.onClick.AddListener(playback.Pause);
            hud.song.text = session.BattleMusic.Music.Name + " · " + battle.Bpm + " BPM";
            for (int i = 0; i < 5; i++)
            {
                icons[i] = hud.weaponRoots[i].gameObject.AddComponent<WeaponIconGraphic>();
                icons[i].FitVisibleArtwork = true; icons[i].Bind(battle.Lanes[i].Weapon);
                // The old gesture sockets described automatic responses. This screen shows the weapon's phrase instead.
                foreach (var sockets in icons[i].GetComponentsInChildren<WeaponSocketGraphic>()) sockets.gameObject.SetActive(false);
                hud.laneLabels[i].text = Keys[i] + "\n" + battle.Lanes[i].Phrase.Name;
                hud.inputAreas[i].gameObject.AddComponent<FiveLaneInputSurface>().Bind(playback, i);
            }
            var hero = Resources.Load<PlayerAuthoring>(PlayerAuthoring.ResourcePath);
            player = Actor("Player", .5f, .24f, .22f, hero != null ? hero.visualPrefab : null,
                hero != null ? hero.controller : null, hero != null ? hero.portrait : Resources.Load<Sprite>("BBSB/BattleArt/weapon-master"),
                hero != null ? hero.spriteReferenceHeight : 4, hero != null ? hero.displayScale : 1);
            int count = session.BattlePlan.Monsters.Count;
            for (int i = 0; i < count; i++)
            {
                var plan = session.BattlePlan.Monsters[i];
                var authored = MonsterAuthoringRegistry.Find(plan.Monster.Id);
                var prefab = authored != null ? authored.actorPrefab != null ? authored.actorPrefab :
                    authored.visualPrefab != null ? authored.visualPrefab.gameObject : null : null;
                monsters.Add(Actor(plan.Monster.Name, .5f + (i - (count - 1) * .5f) * .18f, .66f, .18f,
                    prefab, authored != null ? authored.controller : null,
                    authored != null && authored.portrait != null ? authored.portrait : Resources.Load<Sprite>("BBSB/BattleArt/" + plan.Monster.ArtId),
                    authored != null ? authored.spriteReferenceHeight : 4, authored != null ? authored.displayScale : 1));
                monsterIds.Add(plan.InstanceId);
            }
        }
        private ActorPrefabView Actor(string name, float x, float y, float height, GameObject prefab,
            RuntimeAnimatorController controller, Sprite sprite, float reference, float scale)
        {
            var root = ui.Rect(name, hud.actors);
            FiveLaneHudBindings.Place(root, x - .08f, y, x + .08f, y + height);
            var visual = ui.Rect("Visual", root); visual.anchorMin = visual.anchorMax = new Vector2(.5f, 0);
            visual.anchoredPosition = Vector2.zero; visual.sizeDelta = new Vector2(512, 512);
            visual.localScale = Vector3.one * (height * 720 / 512 * scale);
            var actor = visual.gameObject.AddComponent<ActorPrefabView>(); actor.Initialize(prefab, controller, sprite, reference);
            return actor;
        }
        private void ValidateBindings()
        {
            if (hud.scenery == null || hud.actors == null || hud.tracks == null || hud.song == null || hud.health == null ||
                hud.enemyHealth == null || hud.beat == null || hud.feedback == null || hud.help == null ||
                hud.playerFill == null || hud.enemyFill == null || hud.pause == null ||
                hud.weaponRoots.Length != 5 || hud.inputAreas.Length != 5 || hud.laneLabels.Length != 5 || hud.laneStatus.Length != 5 || hud.laneResults.Length != 5)
                throw new InvalidOperationException("FiveLaneHudBindings requires all HUD references and five lane entries.");
            for (int i = 0; i < 5; i++)
                if (hud.weaponRoots[i] == null || hud.inputAreas[i] == null || hud.laneLabels[i] == null || hud.laneStatus[i] == null || hud.laneResults[i] == null)
                    throw new InvalidOperationException("FiveLaneHudBindings lane " + i + " is incomplete.");
        }
        public void Refresh(int countdown, bool waitingForHold)
        {
            hud.health.text = "HP " + battle.PlayerHealth.ToString("0.#") + " / " + battle.PlayerMaximum;
            hud.enemyHealth.text = "ENEMY " + battle.EnemyHealth.Current.ToString("0.#") + " / " + battle.EnemyHealth.Maximum;
            hud.playerFill.anchorMax = new Vector2((float)(battle.PlayerHealth / battle.PlayerMaximum), 1);
            hud.enemyFill.anchorMax = new Vector2((float)(battle.EnemyHealth.Current / battle.EnemyHealth.Maximum), 1);
            hud.beat.text = countdown > 0 ? "COUNT IN  " + countdown : "BEAT " + ((int)battle.Beat % 4 + 1) + "   ·   COMBO " + battle.Combo;
            hud.beat.color = battle.Beat % 1 < .18 ? RunUI.Gold : RunUI.TextColor;
            hud.feedback.text = waitingForHold ? "Hold 중이던 버튼을 다시 눌러줘" : battle.IsGroggy ? "GROGGY" :
                battle.Beat - battle.LastHitBeat < .5 ? "HIT" : "";
            hud.feedback.color = battle.IsGroggy ? RunUI.Gold : RunUI.Red;
            int latestSlot = 0;
            for (int i = 0; i < 5; i++)
            {
                var lane = battle.Lanes[i];
                if (lane.LastJudgedBeat > battle.Lanes[latestSlot].LastJudgedBeat) latestSlot = i;
                hud.laneStatus[i].text = lane.Phase == PhraseLanePhase.Cooldown ? "CD " + Math.Max(0, lane.ReadyAtBeat - battle.Beat).ToString("0.0") :
                    lane.Phase == PhraseLanePhase.Ready ? "READY" : lane.WaitingForParryRelease ? "HOLD → UP" : lane.Holding ? "HOLD" :
                    "NOTE " + (lane.NextNote + 1) + "/" + lane.Phrase.Notes.Count +
                    (lane.Phrase.FinisherEvery > 0 ? " · " + (lane.CompletedPhrases % lane.Phrase.FinisherEvery + 1) + "/" + lane.Phrase.FinisherEvery : "");
                hud.laneResults[i].text = battle.Beat - lane.LastJudgedBeat < 1 ? lane.Feedback : "";
                hud.laneResults[i].color = lane.LastGrade == RhythmGrade.Miss ? RunUI.Red : lane.LastGrade == RhythmGrade.HalfMiss ? RunUI.Gold : RunUI.Teal;
                icons[i].color = lane.Phase == PhraseLanePhase.Cooldown ? new Color(.45f, .45f, .5f, .65f) : Color.white;
                // Beat-driven recoil gives each successful input a readable weapon response.
                double age = battle.Beat - lane.LastJudgedBeat;
                float pulse = age >= 0 && age < .4 && lane.LastGrade != RhythmGrade.Miss ? 1 - (float)(age / .4) : 0;
                hud.weaponRoots[i].localScale = Vector3.one * (1 + pulse * .18f);
                hud.weaponRoots[i].localRotation = Quaternion.Euler(0, 0, pulse * (i % 2 == 0 ? -24 : 24));
            }
            var recent = battle.Lanes[latestSlot]; double recentAge = battle.Beat - recent.LastJudgedBeat;
            if (recentAge < .5 && recent.LastGrade != RhythmGrade.Miss)
                player.Sample("Base Layer.TapImpact", recentAge, .5f, false);
            else player.Sample("Base Layer.Idle", battle.Beat, 4, true);
            for (int i = 0; i < monsters.Count; i++)
            {
                string state = battle.IsGroggy ? "Base Layer.Hit" : "Base Layer.Idle";
                double age = battle.Beat;
                foreach (var attack in battle.Incoming)
                    if (attack.Definition.MonsterId == monsterIds[i] && attack.State == IncomingAttackState.Pending &&
                        attack.Beat > battle.Beat && attack.Beat - battle.Beat <= 1)
                    { state = "Base Layer.Attack"; age = 1 - (attack.Beat - battle.Beat); break; }
                monsters[i].Sample(state, age, 1, state == "Base Layer.Idle");
            }
            tracks.Refresh();
        }
        public void ShowPause(Action resume, Action leave)
        {
            HideModal();
            modal = ui.Modal(parent, "Five lane pause", "일시정지", resume, out var content);
            ui.Label(content, "D · F · Space · J · K 또는 다섯 입력 영역을 사용해.\n파란 긴 박자 표시는 끝까지 유지해. 금빛 Hold 끝에서는 버튼을 떼어 방어해.", 22, null, 100);
            ui.Button(content, "이어하기", resume, primary: true);
            ui.Button(content, "준비 화면으로", leave);
        }
        public void ShowResult(Action finish)
        {
            HideModal();
            modal = ui.Modal(parent, "Five lane result", battle.Victory ? "STAGE CLEAR" : "GAME OVER", finish, out var content);
            ui.Label(content, "PERFECT " + battle.PerfectCount + "  ·  HALF " + battle.HalfMissCount + "  ·  MISS " + battle.MissCount, 24, null, 70);
            ui.Label(content, "피해 " + battle.TotalDamage.ToString("0.#") + "  ·  방어 " + battle.TotalBlocked.ToString("0.#"), 22, null, 60);
            ui.Button(content, battle.Victory ? "보상 받기" : "결과 보기", finish, primary: true);
        }
        public void HideModal()
        { if (modal == null) return; modal.gameObject.SetActive(false); UnityEngine.Object.Destroy(modal.gameObject); modal = null; }
    }
}
