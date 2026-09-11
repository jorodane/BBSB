using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime;
using BBSB.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BBSB.Tests
{
    public sealed class BattleArenaTests
    {
        private GameObject root;

        [UnityTearDown]
        public IEnumerator TearDown()
        { if (root != null) Object.Destroy(root); yield return null; }

        [Test]
        public void EveryBattlePortraitImportsAsASprite()
        {
            foreach (var id in MonsterCatalog.All.Select(x => x.ArtId).Append("weapon-master").Distinct())
            {
                var sprite = Resources.Load<Sprite>("BBSB/BattleArt/" + id);
                Assert.IsNotNull(sprite, id); Assert.Greater(sprite.rect.width, 0, id);
                Assert.LessOrEqual(sprite.texture.width, 512, "Mobile import size: " + id);
            }
        }

        [Test]
        public void PlayerSpritesUseEveryAtlasPoseOrTheExistingPortrait()
        {
            using (var sprites = new PlayerMotionSprites())
            {
                var fallback = Resources.Load<Sprite>("BBSB/BattleArt/weapon-master");
                foreach (string sheet in new[] { "idle", "tap", "hold", "dive", "flick", "shake" })
                {
                    int count = sheet == "idle" ? 4 : sheet == "tap" ? 12 : 6;
                    var distinct = new HashSet<Sprite>();
                    for (int i = 0; i < count; i++)
                    {
                        var sprite = sprites.Get(sheet, i); Assert.IsNotNull(sprite);
                        if (sprites.UsesFallbackPortrait)
                        {
                            Assert.AreSame(fallback, sprite, "Missing atlases must retain the existing portrait.");
                            continue;
                        }
                        Assert.IsTrue(distinct.Add(sprite), "Every pose must have its own region: " + sheet);
                        Assert.LessOrEqual(sprite.rect.xMax, sprite.texture.width);
                        Assert.LessOrEqual(sprite.rect.yMax, sprite.texture.height);
                        Assert.Greater(sprite.rect.width, 100);
                    }
                }
                if (sprites.UsesFallbackPortrait) return;
                var source = sprites.Get("idle", 0).texture;
                var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                var previous = RenderTexture.active;
                var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                try
                {
                    Graphics.Blit(source, target); RenderTexture.active = target;
                    readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); readable.Apply();
                    Assert.Less(readable.GetPixel(0, 0).a, .01f, "Generated background must not appear in battle.");
                    Assert.IsTrue(readable.GetPixels32().Any(p => p.a > 240), "The character must survive compositing.");
                }
                finally
                { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(readable); }
            }
        }

        [Test]
        public void PlayerMotionUsesAuthoredSlicesAndKeepsSharedSpritesAlive()
        {
            Sprite retained;
            using (var sprites = new PlayerMotionSprites())
            {
                Assert.IsFalse(sprites.UsesFallbackPortrait, "All eight sheets must import in Multiple mode.");
                foreach (var sheet in new[] { "idle", "tap-left", "tap-right", "tap-upper", "hold", "dive", "flick", "shake" })
                {
                    var imported = Resources.LoadAll<Sprite>(PlayerMotionSprites.ResourcePath + sheet);
                    int count = sheet == "idle" || sheet.StartsWith("tap-") ? 4 : 6;
                    for (int i = 0; i < count; i++)
                    {
                        var authored = imported.Single(s => s.name == sheet + "_" + i);
                        Assert.AreSame(authored, sprites.Get(sheet, i));
                        Assert.GreaterOrEqual(authored.pivot.y, 0);
                        Assert.Less(authored.pivot.y / authored.rect.height, .25f, "Pivot must be on the contact edge, not the center.");
                    }
                }
                retained = sprites.Get("idle", 0);
            }
            Assert.IsTrue(retained != null, "Disposing an arena must not destroy imported shared sprites.");
            using (var next = new PlayerMotionSprites()) Assert.AreSame(retained, next.Get("idle", 0));
        }

        [UnityTest]
        public IEnumerator PlayerFeetStayOnConfiguredGroundWhilePosesScalesAndViewportChange()
        {
            var display = ScriptableObject.CreateInstance<PlayerMotionDisplay>();
            display.groundPosition = new Vector2(.31f, .21f);
            display.sheets = new[]
            {
                new PlayerMotionDisplay.Sheet { name = "idle", scale = 1.2f, poses = new[]
                { new PlayerMotionDisplay.Pose(), new PlayerMotionDisplay.Pose { scale = .9f } } },
                new PlayerMotionDisplay.Sheet { name = "flick", poses = new[]
                { new PlayerMotionDisplay.Pose(), new PlayerMotionDisplay.Pose { offset = new Vector2(0, .12f) } } }
            };
            try
            {
                var round = Round(1, new PatternStep(GestureKind.Tap, 0));
                var arena = Arena(round, display); yield return null;
                foreach (var size in new[] { new Vector2(1280, 720), new Vector2(1280, 800) })
                {
                    ((RectTransform)arena.transform).sizeDelta = size;
                    foreach (var scale in new[] { .75f, 1.4f })
                    {
                        display.characterScale = scale;
                        foreach (var time in new[] { .1, .3, .6, .9 })
                        {
                            round.Advance(Math.Max(round.ElapsedSeconds, time)); arena.Refresh();
                            AssertGrounded(arena, 0);
                        }
                    }
                }
                round.Press(2, 0, 0); arena.Refresh(); AssertGrounded(arena, 0);
                round.Release(2.01, 0, 0); round.Advance(2.08); arena.Refresh(); AssertGrounded(arena, 0);
                round.Advance(2.3); arena.Refresh(); AssertGrounded(arena, 0);
                round.Suspend(); arena.SetPaused(true);
                var frozenSprite = arena.HeroPortrait.sprite;
                display.groundPosition = new Vector2(.28f, .18f);
                ((RectTransform)arena.transform).sizeDelta = new Vector2(1000, 700);
                arena.Refresh(); AssertGrounded(arena, 0); Assert.AreSame(frozenSprite, arena.HeroPortrait.sprite);

                var jump = Round(1, new PatternStep(GestureKind.Flick, 0));
                var jumping = Arena(jump, display);
                jump.Press(1.95, 0, 0); jump.Release(2, .1, 0); jumping.Refresh();
                Assert.AreEqual(1, jumping.CurrentHeroMotion.SourceIndex);
                float height = PlayerMotionDisplay.ReferenceDisplayHeight(((RectTransform)jumping.transform).rect.size, display.characterScale);
                AssertGrounded(jumping, height * .12f);
                jump.Advance(2.3); jumping.Refresh(); AssertGrounded(jumping, 0);
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.DestroyImmediate(display); }
        }

        private static void AssertGrounded(BattleArenaView arena, float lift)
        {
            var parent = (RectTransform)arena.transform;
            var image = arena.HeroPortrait; var rect = image.rectTransform; var sprite = image.sprite;
            // Convert the source's actual pivot back through the displayed rectangle to arena space.
            var footLocal = new Vector3(rect.rect.xMin + rect.rect.width * sprite.pivot.x / sprite.rect.width,
                rect.rect.yMin + rect.rect.height * sprite.pivot.y / sprite.rect.height, 0);
            var foot = parent.InverseTransformPoint(rect.TransformPoint(footLocal));
            Assert.AreEqual(parent.rect.xMin + parent.rect.width * arena.HeroGroundPosition.x, foot.x, .02f);
            Assert.AreEqual(parent.rect.yMin + parent.rect.height * arena.HeroGroundPosition.y + lift, foot.y, .02f);
            Assert.AreEqual(Vector3.one, rect.localScale);
            Assert.AreEqual(Quaternion.identity, rect.localRotation);
            Assert.AreEqual(sprite.rect.width / sprite.rect.height, rect.rect.width / rect.rect.height, .0001f);
        }

        [Test]
        public void EveryCallSoundHasADistinctStableOnsetAndEndsBeforeHalfABeat()
        {
            var heard = new List<float[]>();
            foreach (CallSound sound in Enum.GetValues(typeof(CallSound)))
            {
                var clip = CallAudio.CreateClip(sound, 60.0 / 168);
                var repeat = CallAudio.CreateClip(sound, 60.0 / 168);
                try
                {
                    var samples = new float[clip.samples]; var duplicate = new float[repeat.samples];
                    Assert.IsTrue(clip.GetData(samples, 0)); Assert.IsTrue(repeat.GetData(duplicate, 0));
                    Assert.IsTrue(samples.SequenceEqual(duplicate), "Cue identity must survive a new encounter: " + sound);
                    Assert.IsTrue(samples.All(x => !float.IsNaN(x) && !float.IsInfinity(x) && Math.Abs(x) <= 1));
                    Assert.IsTrue(samples.Any(x => Math.Abs(x) > .05f));
                    Assert.Less(clip.length, (float)(30.0 / 168));
                    var onset = samples.Take(1323).ToArray();
                    foreach (var previous in heard)
                        Assert.Greater(onset.Zip(previous, (a, b) => Math.Abs(a - b)).Sum(), 1, "Indistinguishable onset: " + sound);
                    heard.Add(onset);
                }
                finally { Object.DestroyImmediate(clip); Object.DestroyImmediate(repeat); }
            }
        }

        [UnityTest]
        public IEnumerator TurtleTailCueLooksDifferentWhileBothResponsesAreStillPending()
        {
            var turtle = MonsterCatalog.All.Single(x => x.Id == "iron-turtle");
            var stage = MusicStage.Generate(MusicCatalog.All.Single(x => x.Id == "steady-pulse"));
            var proposals = turtle.Patterns.Select((pattern, i) => new MonsterProposal("turtle-" + i, turtle,
                stage.FindPlacements(pattern.Pattern).Where(x => x.StartTick == 32)));
            var plan = BattlePlanner.Resolve(stage, proposals, 1); Assert.AreEqual(2, plan.Monsters.Count);
            var round = new RhythmRound(plan); var arena = Arena(round); yield return null;
            round.Advance(RhythmTime.Seconds(24, stage.Music.Bpm)); arena.Refresh();
            Assert.IsTrue(arena.MonsterPortraits.All(x => x.rectTransform.localScale.y < .9f));
            Assert.AreEqual(2, arena.GetComponentsInChildren<Text>().Count(x => x.text == "CALL · 쿵"));
            round.Advance(RhythmTime.Seconds(28, stage.Music.Bpm)); arena.Refresh();
            Assert.Greater(Quaternion.Angle(arena.MonsterPortraits[0].rectTransform.localRotation,
                arena.MonsterPortraits[1].rectTransform.localRotation), 25);
            Assert.AreEqual(1, arena.GetComponentsInChildren<Text>().Count(x => x.text == "CALL · 휙!"));
            Assert.AreEqual(0, round.Results.Count);
            Assert.IsTrue(round.Notes.All(x => x.StartSeconds > round.ElapsedSeconds));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator LinkedCadenceShowsTheNextCallAlongsideTheCurrentTap()
        {
            var stage = MusicStage.Generate(MusicCatalog.All.Single(x => x.Id == "rapid-drive"));
            var monster = MonsterCatalog.All.Single(x => x.Id == "seesaw-goblin");
            var chain = monster.PatternPlanner.Candidates(stage, monster).Single(x => x.CallStartTick == 16);
            var plan = BattlePlanner.Resolve(stage, new[] { new MonsterProposal(monster.Id, monster, new List<PatternChain> { chain }) }, 1);
            var round = new RhythmRound(plan); var arena = Arena(round); yield return null;
            round.Advance(RhythmTime.Seconds(28, stage.Music.Bpm)); arena.Refresh();
            Assert.IsTrue(arena.GetComponentsInChildren<Text>().Any(x => x.text == "CALL · 당겨! · TAP"));
            round.Advance(RhythmTime.Seconds(34, stage.Music.Bpm)); arena.Refresh();
            Assert.IsTrue(arena.GetComponentsInChildren<Text>().Any(x => x.text == "CALL · 또각! · TAP"));
            Assert.AreEqual(34, round.Calls.Last().Tick);
            round.Press(RhythmTime.Seconds(34, stage.Music.Bpm), 0, 0); arena.Refresh();
            Assert.AreEqual(RhythmGrade.Perfect, round.Results.Last().Grade);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator WaitingPatternsStayQuietWithoutALastMomentAttackWindup()
        {
            var stage = MusicStage.Generate(MusicCatalog.All.Single(x => x.Id == "steady-pulse"));
            foreach (var monster in MonsterCatalog.All.Where(x => x.Patterns.Any(p => p.SilentWaitTicks > 0)))
            {
                var pattern = monster.Patterns.Single(x => x.SilentWaitTicks > 0);
                var proposal = new MonsterProposal(monster.Id, monster,
                    stage.FindPlacements(pattern.Pattern).Where(x => x.StartTick == 64));
                var plan = BattlePlanner.Resolve(stage, new[] { proposal }, 1);
                var round = new RhythmRound(plan); var arena = Arena(round); yield return null;
                Canvas.ForceUpdateCanvases();
                round.Advance(RhythmTime.Seconds(plan.Attacks.Single().CallStartTick, stage.Music.Bpm)); arena.Refresh();
                Assert.IsTrue(arena.GetComponentsInChildren<Text>().Any(x => x.text == "CALL · " + pattern.Call[0].Label));
                double target = RhythmTime.Seconds(64, stage.Music.Bpm);
                round.Advance(target - .1); arena.Refresh();
                Assert.IsTrue(arena.GetComponentsInChildren<Text>().Any(x => x.text == "쉼 · 박자 기억하기"));
                Assert.AreEqual(0f, arena.MonsterPortraits.Single().rectTransform.anchoredPosition.x, .001f);
                Assert.AreEqual(1, round.Calls.Count); Assert.AreEqual(0, round.Results.Count);
                round.Advance(target); arena.Refresh();
                Assert.Less(arena.MonsterPortraits.Single().rectTransform.anchoredPosition.x, 0);
                Assert.IsTrue(arena.GetComponentsInChildren<Text>().Any(x => x.text == "RESPONSE"));
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CallsAttacksAndSharedCountersUseThePlanAndFreezeTogether()
        {
            var round = Round(3, new PatternStep(GestureKind.Tap, 0));
            var arena = Arena(round); yield return null; Canvas.ForceUpdateCanvases();
            arena.Refresh(); var idle = arena.MonsterPortraits[0].rectTransform.anchoredPosition;
            round.Advance(1.5); arena.Refresh();
            Assert.Greater(arena.MonsterPortraits[0].rectTransform.anchoredPosition.y, idle.y + 10);
            Assert.AreEqual(3, arena.GetComponentsInChildren<Text>().Count(x => x.text == "CALL · 통!"));
            Assert.AreEqual(0, arena.ActiveResponseEffects);
            var call = arena.MonsterPortraits[0].rectTransform.anchoredPosition;
            round.Advance(2); arena.Refresh();
            Assert.Less(arena.MonsterPortraits[0].rectTransform.anchoredPosition.y, call.y);
            round.Press(2, 0, 0); arena.Refresh();
            Assert.AreEqual(3, round.PerfectCount); Assert.AreEqual(3, arena.ActiveResponseEffects);
            Assert.AreEqual(GestureKind.Tap, arena.CurrentHeroMotion.Kind);
            Assert.AreEqual(PlayerMotionPhase.Impact, arena.CurrentHeroMotion.Phase);
            Assert.AreEqual(1, arena.CurrentHeroMotion.Index % 4);
            Assert.IsTrue(arena.GetComponentsInChildren<Graphic>().All(x => !x.raycastTarget));
            foreach (var graphic in arena.GetComponentsInChildren<BattleArenaGraphic>())
            {
                var renderer = graphic.GetComponent<CanvasRenderer>(); Assert.IsNotNull(renderer);
                renderer.cull = false; graphic.SetVerticesDirty(); graphic.Rebuild(CanvasUpdate.PreRender);
                Assert.Greater(renderer.GetMesh().vertexCount, 0);
            }
            var heroPosition = arena.HeroPortrait.rectTransform.anchoredPosition;
            var monsterPosition = arena.MonsterPortraits[0].rectTransform.anchoredPosition;
            var rotation = arena.HeroPortrait.rectTransform.localRotation;
            var heroSprite = arena.HeroPortrait.sprite;
            round.Suspend(); arena.SetPaused(true); round.Advance(100); arena.Refresh();
            yield return null; arena.Refresh();
            Assert.AreEqual(2, round.ElapsedSeconds);
            Assert.AreEqual(heroPosition, arena.HeroPortrait.rectTransform.anchoredPosition);
            Assert.AreEqual(monsterPosition, arena.MonsterPortraits[0].rectTransform.anchoredPosition);
            Assert.AreEqual(rotation, arena.HeroPortrait.rectTransform.localRotation);
            Assert.AreSame(heroSprite, arena.HeroPortrait.sprite);
            round.Resume(true); arena.SetPaused(false); arena.Refresh();
            Assert.AreEqual(heroPosition, arena.HeroPortrait.rectTransform.anchoredPosition);
            round.Release(2.01, 0, 0); round.Advance(4); arena.Refresh();
            Assert.AreEqual(0, arena.ActiveResponseEffects, "A clock jump must not replay expired counters.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SustainedPosesRequireInputAndAllCombinedEndingsRespond()
        {
            var round = Round(1, new PatternStep(GestureKind.Hold, 0, 8), new PatternStep(GestureKind.Dive, 0, 8),
                new PatternStep(GestureKind.Shake, 0, 8), new PatternStep(GestureKind.Flick, 8));
            var arena = Arena(round); yield return null;
            round.Advance(2); arena.Refresh();
            Assert.AreEqual(1, arena.HeroPortrait.rectTransform.localScale.y, "An automatic Shake window isn't a player action.");
            Assert.AreEqual(PlayerMotionPhase.Idle, arena.CurrentHeroMotion.Phase);
            round.Press(2, 0, 0); arena.Refresh();
            Assert.AreEqual("dive", arena.CurrentHeroMotion.Sheet);
            Assert.IsTrue(arena.GetComponentsInChildren<Text>().Any(x => x.text.Contains("회피 준비")));
            for (int i = 1; i <= 15; i++)
            {
                round.Move(2 + i * .05, i % 2 == 1 ? .1 : 0, 0); arena.Refresh();
                if (i == 1)
                {
                    Assert.IsTrue(arena.GetComponentsInChildren<Text>().Any(x => x.text == "양손 밀쳐내기"));
                    Assert.AreEqual("shake", arena.CurrentHeroMotion.Sheet);
                }
            }
            // After a credited round trip Shake is complete; the remaining held Dive resumes.
            Assert.AreEqual("dive", arena.CurrentHeroMotion.Sheet);
            var scale = arena.HeroPortrait.rectTransform.localScale;
            arena.SetPaused(true); round.Suspend(); arena.Refresh();
            Assert.AreEqual(scale, arena.HeroPortrait.rectTransform.localScale, "Pause cannot drop a held pose.");
            round.Resume(true, .1, 0); arena.SetPaused(false); arena.Refresh();
            round.Move(2.94, .1, 0); round.Release(3, .2, 0); arena.Refresh();
            Assert.AreEqual(4, round.PerfectCount); Assert.AreEqual(4, arena.ActiveResponseEffects);
            round.Advance(4); arena.Refresh();
            Assert.AreEqual(1, arena.HeroPortrait.rectTransform.localScale.y);
            Assert.AreEqual(0, arena.ActiveResponseEffects);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PartialAndMissGradesProduceDistinctPlayerReactions()
        {
            var perfect = Round(1, new PatternStep(GestureKind.Tap, 0)); var a = Arena(perfect);
            var half = Round(1, new PatternStep(GestureKind.Tap, 0)); var b = Arena(half);
            var miss = Round(1, new PatternStep(GestureKind.Tap, 0)); var c = Arena(miss);
            var early = Round(1, new PatternStep(GestureKind.Tap, 0)); var d = Arena(early);
            yield return null;
            perfect.Press(2, 0, 0); a.Refresh();
            half.Press(2.1, 0, 0); b.Refresh();
            Assert.AreEqual(1, half.HalfMissCount);
            Assert.AreEqual(1, a.CurrentHeroMotion.Index % 4);
            Assert.AreEqual(2, b.CurrentHeroMotion.Index % 4);
            miss.Advance(2.121); c.Refresh();
            Assert.AreEqual(1, miss.MissCount); Assert.Less(c.HeroPortrait.color.g, c.HeroPortrait.color.r);
            Assert.AreEqual(3, c.CurrentHeroMotion.Index % 4);
            early.Press(1.8, 0, 0); d.Refresh();
            Assert.IsTrue(d.GetComponentsInChildren<Text>().Any(x => x.text == "너무 이른 동작"));
            Assert.AreEqual(MissReason.TooEarly, early.Results.Single().Reason);
            LogAssert.NoUnexpectedReceived();
        }

        private BattleArenaView Arena(RhythmRound round, PlayerMotionDisplay display = null)
        {
            if (root == null)
            {
                root = new GameObject("Battle arena tests", typeof(RectTransform), typeof(Canvas));
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            }
            var rect = new GameObject("Battle arena", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(root.transform, false); rect.sizeDelta = new Vector2(1280, 720);
            var view = rect.gameObject.AddComponent<BattleArenaView>();
            view.Initialize(round, Resources.Load<Font>("BBSB/Fonts/BBSBUI"), display); return view;
        }

        private static RhythmRound Round(int count, params PatternStep[] steps)
        {
            var slots = new List<SlotTemplate>();
            for (int tick = 0; tick < 16; tick += 2)
            {
                slots.Add(new SlotTemplate(GestureKind.Tap, tick)); slots.Add(new SlotTemplate(GestureKind.Flick, tick));
                foreach (var kind in new[] { GestureKind.Hold, GestureKind.Dive, GestureKind.Shake })
                    foreach (int duration in new[] { 4, 8, 16 }) slots.Add(new SlotTemplate(kind, tick, duration));
            }
            var stage = MusicStage.Generate(new MusicDefinition("arena", "Arena", 120, 4, new[]
            { new MusicSection("INTRO", 0, 1, 1, false), new MusicSection("BODY", 1, 3, 1) }, new[] { slots }));
            var pattern = new RhythmPattern("arena", 4, steps);
            var monster = new MonsterDefinition("tap-slime", "통통 슬라임", "", pattern, new[] { new CallSignal(0, "통!") },
                Math.Max(4, pattern.EndOffsetTick), 4, 1);
            var proposals = Enumerable.Range(0, count).Select(i => new MonsterProposal("slime-" + i, monster,
                stage.FindPlacements(pattern).Where(x => x.StartTick == 16)));
            return new RhythmRound(BattlePlanner.Resolve(stage, proposals, 1));
        }
    }
}
