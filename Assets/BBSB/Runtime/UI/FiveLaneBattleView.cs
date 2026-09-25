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
        private readonly FiveLaneWeaponsView weapons;
        private readonly BattleThemeView themeView;
        private readonly List<StageActor> stageActors = new List<StageActor>();
        private readonly List<TMP_Text> attackLabels = new List<TMP_Text>();
        private readonly List<TMP_Text> monsterHealthLabels = new List<TMP_Text>();
        private readonly List<RectTransform> monsterHealthFills = new List<RectTransform>();
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
                Visual.localRotation = Quaternion.identity;
            }
            public void Attack(AttackMotionState state, double beat, int facing)
            {
                var frame = FiveLaneAttackMotion.Body(state, beat, facing);
                float height = 512 * Visual.localScale.y;
                Visual.anchoredPosition += new Vector2((float)frame.Position.X, (float)frame.Position.Y) * height;
                Visual.localRotation = Quaternion.Euler(0, 0, (float)frame.Rotation);
                Visual.localScale *= (float)frame.Scale;
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
            var theme = hud.visualTheme != null ? hud.visualTheme : Resources.Load<BattleVisualTheme>(BattleVisualTheme.ResourcePath);
            if (!BattleThemeView.AddArena(ui, hud.scenery, theme)) StageScenery.Add(ui, hud.scenery, session.BattleMusic.Music, "Stage art", presentation);
            var shade = ui.Rect("Scene shade", hud.scenery); RunUI.Stretch(shade); ui.Background(shade, new Color(.025f, .035f, .08f, .09f));
            // A prefab slot may already have an Image. Unity permits only one Graphic per
            // object, so runtime meshes get their own children instead of AddComponent failing.
            var trackMesh = ui.Rect("Live note tracks", hud.tracks); RunUI.Stretch(trackMesh);
            tracks = trackMesh.gameObject.AddComponent<FiveLaneTrackGraphic>();
            tracks.Theme = theme;
            tracks.Bind(battle, hud.judgmentPoints, hud.playerSlot, hud.monsterArea);
            themeView = new BattleThemeView(ui, hud, battle, tracks, theme);
            hud.pause.onClick.AddListener(playback.Pause);
            hud.stageBadge.text = "FLOOR  " + session.Map.Number + "–" + (session.CurrentNode.Row + 1);
            hud.song.text = session.BattleMusic.Music.Name + "\n" + battle.Bpm + " BPM";
            for (int i = 0; i < BattleInputLayout.LaneCount; i++)
            {
                var lane = battle.LaneAt(i);
                hud.laneLabels[i].text = Keys[i];
                hud.laneLabels[i].color = lane == null ? RunUI.Muted : RunUI.TextColor;
                if (lane == null) continue;
                var weaponMesh = ui.Rect("Lane weapon icon " + Keys[i], hud.weaponRoots[i]); RunUI.Stretch(weaponMesh);
                icons[i] = weaponMesh.gameObject.AddComponent<WeaponIconGraphic>();
                icons[i].FitVisibleArtwork = true; icons[i].Bind(lane.Weapon);
                // The old gesture sockets described automatic responses. This screen shows the weapon's phrase instead.
                foreach (var sockets in icons[i].GetComponentsInChildren<WeaponSocketGraphic>()) sockets.gameObject.SetActive(false);
                var input = hud.inputAreas[i].GetComponent<FiveLaneInputSurface>();
                if (input == null) input = hud.inputAreas[i].gameObject.AddComponent<FiveLaneInputSurface>();
                input.Bind(playback, i, hud.arrangeEquippedLanes ? (RectTransform)hud.transform : null);
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
                species.Add(plan.Monster.Id); enemySlots.Add(stageActors[i + 1].Visual);
                var actorSlot = stageActors[i + 1].Slot;
                var healthLabel = ui.Label(actorSlot, "", 14, RunUI.TextColor);
                healthLabel.fontSize = 14; healthLabel.color = RunUI.TextColor;
                healthLabel.enableAutoSizing = true; healthLabel.fontSizeMin = 10; healthLabel.fontSizeMax = 14;
                healthLabel.name = "Monster health " + plan.InstanceId; healthLabel.alignment = TextAlignmentOptions.Center;
                FiveLaneHudBindings.Place(healthLabel.rectTransform, -.05f, 1.01f, 1.05f, 1.17f); monsterHealthLabels.Add(healthLabel);
                var bar = ui.Rect("Monster health bar", actorSlot); FiveLaneHudBindings.Place(bar, .03f, .965f, .97f, .99f); ui.Background(bar, RunUI.Ink);
                var fill = ui.Rect("Fill", bar); RunUI.Stretch(fill); ui.Background(fill, new Color(.88f, .13f, .20f)); monsterHealthFills.Add(fill);
                BattleThemeView.Frame(ui, bar, theme != null ? theme.healthFrame : null);
                var cue = ui.Label(hud.actors, "", 20, RunUI.Red);
                cue.alignment = TextAlignmentOptions.Center;
                float x0 = Mathf.Lerp(hud.monsterArea.anchorMin.x, hud.monsterArea.anchorMax.x, (float)i / count);
                float x1 = Mathf.Lerp(hud.monsterArea.anchorMin.x, hud.monsterArea.anchorMax.x, (float)(i + 1) / count);
                FiveLaneHudBindings.Place(cue.rectTransform, x0, .85f, x1, .89f);
                attackLabels.Add(cue);
            }
            var playerBody = stageActors[0].Visual;
            weapons = new FiveLaneWeaponsView(battle, ui, hud, playerBody, enemySlots, monsterIds);
            tracks.Bind(battle, hud.judgmentPoints, playerBody, hud.monsterArea);
            tracks.BindEnemySlots(enemySlots, monsterIds);
            effects = new FiveLaneEffectsView(presentation, battle, ui, hud, playerBody, tracks, enemySlots, monsterIds, species, weapons);
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
        public void Refresh(string countInWord, bool waitingForHold)
        {
            if (battle.Formation != null)
            {
                int rear = 0;
                for (int i = 0; i < monsterIds.Count; i++)
                {
                    var monster = battle.Formation.Find(monsterIds[i]); var slot = stageActors[i + 1].Slot;
                    bool front = ReferenceEquals(monster, battle.Formation.Front);
                    // The frontline faces the player; the reserve waits behind it.
                    if (monsterIds.Count == 1 || front) FiveLaneHudBindings.Place(slot, .02f, 0, .42f, .90f);
                    else
                    {
                        float x = monsterIds.Count == 2 ? .65f : .54f + .26f * (rear % 2);
                        float y = .14f + .08f * (rear % 2); rear++;
                        FiveLaneHudBindings.Place(slot, x, y, Math.Min(1, x + (monsterIds.Count == 2 ? .28f : .22f)), .88f);
                    }
                    monsterHealthLabels[i].text = (monster.Health.Defeated ? "격파" : front ? "선봉" : monster.IsTrickster ? "후열 · 난입" : "후열") +
                        " · " + monster.Health.Current.ToString("0.#") + " / " + monster.Health.Maximum.ToString("0.#");
                    monsterHealthFills[i].anchorMax = new Vector2((float)(monster.Health.Current / monster.Health.Maximum), 1);
                    if (attackLabels[i].transform.parent != slot) attackLabels[i].transform.SetParent(slot, false);
                    FiveLaneHudBindings.Place(attackLabels[i].rectTransform, -.05f, -.13f, 1.05f, .02f);
                    attackLabels[i].fontSize = 15;
                }
            }
            foreach (var actor in stageActors) actor.Layout();
            for (int i = 0; i < monsterHealthFills.Count; i++)
            {
                var slot = stageActors[i + 1].Slot;
                // Stage slots include generous horizontal space. The HP bar should
                // follow the body size instead of stretching across that whole space.
                float width = slot.rect.width > 0 ? Mathf.Min(.9f, slot.rect.height * 1.2f / slot.rect.width) : .9f;
                FiveLaneHudBindings.Place((RectTransform)monsterHealthFills[i].parent, .5f - width * .5f, .965f, .5f + width * .5f, .99f);
                FiveLaneHudBindings.Place(monsterHealthLabels[i].rectTransform, .5f - width * .65f, 1.01f, .5f + width * .65f, 1.17f);
            }
            hud.health.text = battle.PlayerHealth.ToString("0.#") + " / " + battle.PlayerMaximum;
            hud.playerFill.anchorMax = new Vector2((float)(battle.PlayerHealth / battle.PlayerMaximum), 1);
            hud.beat.text = "<size=15>COMBO</size>\n<size=48>" + battle.Combo + "</size>";
            hud.countIn.text = countInWord ?? "";
            hud.countIn.gameObject.SetActive(!string.IsNullOrEmpty(countInWord));
            hud.rhythmBeat.text = ((int)Math.Floor(battle.Beat) % 4 + 1) + "<size=17>/4</size>\n<size=12>BEAT</size>";
            hud.rhythmBeat.color = battle.Beat % 1 < .18 ? BattleVisualTheme.Light : RunUI.TextColor;
            hud.beat.color = battle.Beat % 1 < .18 ? RunUI.Gold : RunUI.TextColor;
            hud.feedback.text = waitingForHold ? "Hold 중이던 버튼을 다시 눌러줘" : battle.IsGroggy ? "GROGGY" :
                battle.Beat - battle.LastHitBeat < .5 ? "HIT" : "";
            hud.feedback.color = battle.IsGroggy ? RunUI.Gold : RunUI.Red;
            if (!waitingForHold && battle.Formation?.PendingFront != null)
                hud.feedback.text = (battle.Formation.ChangeReason == FormationChangeReason.Intrusion ? "난입" : battle.Formation.ChangeReason == FormationChangeReason.Return ? "복귀" : "교대") +
                    " " + Math.Max(0, battle.Formation.SwitchAtBeat - battle.Beat).ToString("0.0") + "박 · " + battle.Formation.PendingFront.Definition.Name;
            bool hasStorageShield = false;
            foreach (var lane in battle.Lanes) hasStorageShield |= lane.Weapon.DefinitionId == "resonance-shield";
            if (hasStorageShield)
                hud.help.text = string.Join(" / ", Keys) + " · 축적 빛 " + battle.LightResonance.ToString("0.#") + " / 어둠 " + battle.DarkResonance.ToString("0.#") + " · 12마다 다음 공격 +50%";
            for (int i = 0; i < BattleInputLayout.LaneCount; i++)
            {
                var lane = battle.LaneAt(i);
                if (lane == null) continue;
                hud.laneResults[i].text = lane.Charging ? "충전 " + (lane.ChargeFraction(battle.Beat) * 100).ToString("0") + "% · 떼어 발사" :
                    lane.Loaded ? "장전 완료 · 발사 선택" : lane.Phase == PhraseLanePhase.Playing && lane.Cycle.Index == 0 && lane.NextNote == 0 &&
                    lane.Phrase.FirstNoteDelayBeats > 0 && battle.Beat < lane.NextBeat ? "CALL · " + (lane.NextBeat - battle.Beat).ToString("0.0") :
                    battle.Beat - lane.LastJudgedBeat < 1 ? lane.Feedback : "";
                hud.laneResults[i].color = lane.Charging || lane.Loaded ? RunUI.Teal : lane.LastGrade == RhythmGrade.Miss ? RunUI.Red : lane.LastGrade == RhythmGrade.HalfMiss ? RunUI.Gold : RunUI.Teal;
                icons[i].color = lane.Phase == PhraseLanePhase.Cooldown ? new Color(.45f, .45f, .5f, .65f) : Color.white;
                icons[i].SetPose(RangedWeaponPose.Idle);
                var contact = battle.ShieldAt(i);
                if (contact != null)
                {
                    hud.laneResults[i].text = battle.Beat - contact.LastJudgedBeat < 1 ? contact.Feedback : "";
                    icons[i].color = contact.Phase == PhraseLanePhase.Cooldown ? new Color(.45f, .45f, .5f, .65f) : Color.white;
                }
                hud.weaponRoots[i].localScale = Vector3.one;
                hud.weaponRoots[i].localRotation = Quaternion.identity;
            }
            if (string.IsNullOrEmpty(hud.feedback.text))
            {
                double latest = battle.Beat - .8;
                foreach (var lane in battle.Lanes)
                    if (lane.LastJudgedBeat > latest)
                    {
                        latest = lane.LastJudgedBeat; hud.feedback.text = lane.Feedback;
                        hud.feedback.color = lane.LastGrade == RhythmGrade.Miss ? RunUI.Red : RunUI.Gold;
                    }
                foreach (var contact in battle.ShieldContacts)
                    if (contact.LastJudgedBeat >= latest)
                    {
                        latest = contact.LastJudgedBeat; hud.feedback.text = contact.Feedback;
                        hud.feedback.color = contact.Grade == RhythmGrade.Miss ? RunUI.Red : RunUI.Gold;
                    }
            }
            var heroFrame = FiveLaneArtTimeline.Player(battle);
            player.Sample(heroFrame.State, heroFrame.Age, heroFrame.Duration, heroFrame.Loop);
            AttackMotionState heroMotion = default;
            foreach (var lane in battle.Lanes)
            {
                var motion = FiveLaneAttackMotion.Player(battle, lane);
                if (motion.Active && (!heroMotion.Active || motion.StartBeat > heroMotion.StartBeat)) heroMotion = motion;
            }
            if (heroFrame.State != "Base Layer.Hit") stageActors[0].Attack(heroMotion, battle.Beat, 1);
            double nearestAttack = double.PositiveInfinity;
            for (int i = 0; i < monsters.Count; i++)
            {
                double remaining = double.PositiveInfinity;
                foreach (var attack in battle.Incoming)
                {
                    if (attack.Definition.MonsterId != monsterIds[i] || attack.State != IncomingAttackState.Pending) continue;
                    double cue = attack.Definition.IsHold && attack.Beat <= battle.Beat ? attack.NextImpactBeat : attack.Beat;
                    if (cue - battle.Beat >= -battle.HalfMissWindow && cue - battle.Beat <= 3)
                        remaining = Math.Min(remaining, Math.Max(0, cue - battle.Beat));
                }
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
                }
                stageActors[i + 1].Attack(FiveLaneAttackMotion.Enemy(battle, monsterIds[i]), battle.Beat, -1);
                monsters[i].RefreshSprites();
            }
            hud.attackCue.text = !string.IsNullOrEmpty(countInWord) ? "" : double.IsPositiveInfinity(nearestAttack) ? "" :
                nearestAttack <= battle.PerfectWindow ? "PARRY NOW" : "PARRY IN " + nearestAttack.ToString("0.0") + " BEATS";
            hud.attackCue.color = nearestAttack <= .5 ? RunUI.Gold : RunUI.Red;
            player.RefreshSprites();
            weapons.Refresh();
            tracks.Refresh();
            themeView.Refresh();
            effects.Refresh();
        }
        public void ShowPause(Action resume, Action leave)
        {
            HideModal();
            modal = ui.Modal(parent, "Five lane pause", "일시정지", resume, out var content);
            ui.Label(content, hud.help.text + "\n긴 공격은 0.5박마다 피해. 방어 버튼을 끝까지 유지해.\n중간부터 방어해도 남은 홀드를 막을 수 있어.", 22, null, 100);
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
