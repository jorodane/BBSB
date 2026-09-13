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
            Assert.AreEqual(1, rect.localScale.x * rect.localScale.y, .0001f);
            Assert.Greater(rect.localScale.x, .84f); Assert.Less(rect.localScale.x, 1.2f);
            Assert.AreEqual(1, rect.localScale.z);
            Assert.AreEqual(Quaternion.identity, rect.localRotation);
            Assert.AreEqual(sprite.rect.width / sprite.rect.height, rect.rect.width / rect.rect.height, .0001f);
        }

        [UnityTest]
        public IEnumerator FreeInputMovesTheSmallerHeroWithoutCounterEffects()
        {
            var round = Round(1, new PatternStep(GestureKind.Tap, 0));
            var arena = Arena(round); yield return null; Canvas.ForceUpdateCanvases();
            Assert.Less(arena.HeroGroundPosition.x, .2f);
            Assert.Less(arena.HeroPortrait.rectTransform.rect.height / ((RectTransform)arena.transform).rect.height, .4f);
            Assert.Less(arena.MonsterPortraits[0].rectTransform.rect.height / ((RectTransform)arena.transform).rect.height, .25f);
            round.Press(.4, 0, 0); arena.Refresh();
            Assert.IsTrue(arena.CurrentHeroMotion.IsFreeInput);
            Assert.AreEqual(GestureKind.Tap, arena.CurrentHeroMotion.Kind);
            Assert.AreEqual(PlayerMotionPhase.Prepare, arena.CurrentHeroMotion.Phase);
            Assert.AreEqual(0, arena.CurrentHeroMotion.Index % 4);
            Assert.AreEqual(0, arena.ActiveResponseEffects); Assert.AreEqual(Color.white, arena.HeroPortrait.color);
            round.Advance(.44); arena.Refresh(); Assert.AreEqual(1, arena.CurrentHeroMotion.Index % 4);
            round.Release(.45, .1, 0); arena.Refresh();
            Assert.IsTrue(arena.CurrentHeroMotion.IsFreeInput);
            Assert.AreEqual(GestureKind.Flick, arena.CurrentHeroMotion.Kind);
            Assert.AreEqual(0, arena.ActiveResponseEffects); Assert.AreEqual(0, round.Results.Count);
            round.Press(.6, 0, 0); round.Advance(.8); arena.Refresh();
            Assert.AreEqual(GestureKind.Hold, arena.CurrentHeroMotion.Kind);
            Assert.AreEqual(PlayerMotionPhase.Sustain, arena.CurrentHeroMotion.Phase);
            round.Release(.9, 0, 0); arena.Refresh();
            Assert.IsTrue(arena.CurrentHeroMotion.IsFreeInput);
            Assert.AreEqual("hold", arena.CurrentHeroMotion.Sheet);
            Assert.AreEqual(PlayerMotionPhase.Recover, arena.CurrentHeroMotion.Phase);
            Assert.AreEqual(5, arena.CurrentHeroMotion.Index);
            using (var sprites = new PlayerMotionSprites())
                Assert.AreSame(sprites.Get("hold", 5), arena.HeroPortrait.sprite);
            Assert.AreEqual(0, arena.ActiveResponseEffects); Assert.AreEqual(0, round.Results.Count);
            Assert.AreEqual(0m, round.TotalDamageTaken);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TapPreparationAndSquashKeepFeetFixedThroughRapidInputAndPause()
        {
            var display = ScriptableObject.CreateInstance<PlayerMotionDisplay>();
            try
            {
                var round = Round(1, new PatternStep(GestureKind.Tap, 0));
                var arena = Arena(round, display); yield return null; Canvas.ForceUpdateCanvases();
                round.Press(.1, 0, 0); arena.Refresh();
                Assert.AreEqual(PlayerMotionPhase.Prepare, arena.CurrentHeroMotion.Phase);
                round.Advance(.117); arena.Refresh(); AssertGrounded(arena, 0);
                var rect = arena.HeroPortrait.rectTransform; var compressed = rect.localScale;
                Assert.Less(compressed.y, 1);
                round.Suspend(); arena.SetPaused(true); round.Advance(100);
                ((RectTransform)arena.transform).sizeDelta = new Vector2(1000, 700);
                Canvas.ForceUpdateCanvases(); arena.Refresh();
                Assert.AreEqual(compressed, rect.localScale); AssertGrounded(arena, 0);
                round.Resume(true, 0, 0); arena.SetPaused(false); round.Move(.14, 0, 0); arena.Refresh();
                Assert.AreEqual(PlayerMotionPhase.Impact, arena.CurrentHeroMotion.Phase);
                Assert.AreEqual(1, arena.CurrentHeroMotion.Index % 4); AssertGrounded(arena, 0);
                round.Release(.141, 0, 0); round.Press(.18, 0, 0); arena.Refresh();
                Assert.AreEqual(PlayerMotionPhase.Prepare, arena.CurrentHeroMotion.Phase);
                Assert.AreEqual(0, arena.CurrentHeroMotion.Index % 4);
                display.squashStretchStrength = 0; round.Advance(.197); arena.Refresh();
                Assert.AreEqual(Vector3.one, rect.localScale); AssertGrounded(arena, 0);
                Assert.AreEqual(0, round.Results.Count); Assert.AreEqual(0, arena.ActiveResponseEffects);
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.DestroyImmediate(display); }
        }

        [UnityTest]
        public IEnumerator CounterRouteStaysAboveTheIncomingImageSlot()
        {
            var round = Round(1, new PatternStep(GestureKind.Tap, 0));
            var arena = Arena(round); yield return null; Canvas.ForceUpdateCanvases();
            var graphic = arena.transform.Find("Battle effects and five weapons").GetComponent<BattleArenaGraphic>();
            var renderer = graphic.GetComponent<CanvasRenderer>(); renderer.cull = false;
            round.Advance(1.9); arena.Refresh(); graphic.Rebuild(CanvasUpdate.PreRender);
            float height = graphic.rectTransform.rect.height, bottom = graphic.rectTransform.rect.yMin;
            var incoming = arena.GetComponentsInChildren<MonsterAttackGraphic>().Single();
            incoming.Rebuild(CanvasUpdate.PreRender);
            Assert.Greater(incoming.GetComponent<CanvasRenderer>().GetMesh().vertexCount, 0);
            Assert.Less(incoming.rectTransform.anchoredPosition.y / height, .55f);
            round.Press(2, 0, 0); round.Advance(2.1); arena.Refresh(); graphic.Rebuild(CanvasUpdate.PreRender);
            Assert.AreEqual(1, arena.ActiveResponseEffects);
            Assert.Greater((renderer.GetMesh().vertices.Max(v => v.y) - bottom) / height, .55f);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MonsterStageVisitKeepsItsGroundLabelsAndShadowsTogether()
        {
            var round = Round(3, new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 4),
                new PatternStep(GestureKind.Tap, 8));
            var arena = Arena(round); yield return null; Canvas.ForceUpdateCanvases();
            var roots = arena.MonsterPortraits.Select(p => (RectTransform)p.transform.parent).ToArray();
            var homes = roots.Select(r => r.anchorMin).ToArray();
            for (int i = 0; i < roots.Length; i++)
            {
                Assert.Less(homes[i].y, .31f);
                var sprite = arena.MonsterPortraits[i].sprite;
                Assert.AreEqual(sprite.pivot.y / sprite.rect.height, arena.MonsterPortraits[i].rectTransform.pivot.y, .00001f);
                if (i > 0) Assert.Greater(roots[i].GetSiblingIndex(), roots[i - 1].GetSiblingIndex());
            }
            round.Advance(1.75); arena.Refresh();
            var forward = roots.Select(r => r.anchorMin).ToArray();
            for (int i = 0; i < roots.Length; i++) Assert.Less(forward[i].x, homes[i].x - .05f);
            round.Advance(2.25); arena.Refresh();
            for (int i = 0; i < roots.Length; i++)
            {
                Assert.AreEqual(forward[i], roots[i].anchorMin, "Stay out between the phrase's beats.");
                var labelRoot = (RectTransform)arena.transform.Find("Actor labels/Labels slime-" + i);
                Assert.AreEqual(forward[i], labelRoot.anchorMin);
            }
            var backdrop = arena.transform.Find("Arena backdrop").GetComponent<BattleArenaGraphic>();
            var renderer = backdrop.GetComponent<CanvasRenderer>(); renderer.cull = false;
            backdrop.SetVerticesDirty(); backdrop.Rebuild(CanvasUpdate.PreRender);
            var bounds = backdrop.rectTransform.rect;
            foreach (var foot in forward)
            {
                var center = new Vector3(bounds.xMin + bounds.width * foot.x, bounds.yMin + bounds.height * foot.y, 0);
                Assert.IsTrue(renderer.GetMesh().vertices.Any(v => (v - center).sqrMagnitude < .01f), "Shadow stays under the moving actor.");
            }
            round.Suspend(); arena.SetPaused(true); round.Advance(10);
            ((RectTransform)arena.transform).sizeDelta = new Vector2(1280, 800); arena.Refresh();
            for (int i = 0; i < roots.Length; i++) Assert.AreEqual(forward[i], roots[i].anchorMin);
            round.Resume(false); arena.SetPaused(false); round.Advance(4); arena.Refresh();
            for (int i = 0; i < roots.Length; i++) Assert.AreEqual(homes[i], roots[i].anchorMin);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator UnscheduledMonstersKeepTheirWaitingPositions()
        {
            var stage = MusicStage.Generate(MusicCatalog.All.Single(x => x.Id == "steady-pulse"));
            var monster = MonsterCatalog.All.Single(x => x.Id == "tap-slime");
            var pattern = monster.Patterns[0];
            var plan = BattlePlanner.Resolve(stage, new[] { 32, 64, 96 }.Select((tick, i) =>
                new MonsterProposal("actor-" + i, monster, stage.FindPlacements(pattern.Pattern).Where(x => x.StartTick == tick))), 1);
            var round = new RhythmRound(plan); var arena = Arena(round); yield return null;
            var roots = arena.MonsterPortraits.Select(p => (RectTransform)p.transform.parent).ToArray();
            var homes = roots.Select(r => r.anchorMin).ToArray();
            var first = plan.Attacks.OrderBy(x => x.CallStartTick).First();
            round.Advance(RhythmTime.Seconds(first.CallStartTick, stage.Music.Bpm) + .3); arena.Refresh();
            for (int i = 0; i < roots.Length; i++)
                if (plan.Monsters[i].InstanceId == first.MonsterId) Assert.Less(roots[i].anchorMin.x, homes[i].x);
                else Assert.AreEqual(homes[i], roots[i].anchorMin);
            LogAssert.NoUnexpectedReceived();
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
        public IEnumerator TurtleTailCueUsesAuthoredPosesWhileBothResponsesAreStillPending()
        {
            var turtle = MonsterCatalog.All.Single(x => x.Id == "iron-turtle");
            var stage = MusicStage.Generate(MusicCatalog.All.Single(x => x.Id == "steady-pulse"));
            var proposals = turtle.Patterns.Select((pattern, i) => new MonsterProposal("turtle-" + i, turtle,
                stage.FindPlacements(pattern.Pattern).Where(x => x.StartTick == 32)));
            var plan = BattlePlanner.Resolve(stage, proposals, 1); Assert.AreEqual(2, plan.Monsters.Count);
            var round = new RhythmRound(plan); var arena = Arena(round); yield return null;
            round.Advance(RhythmTime.Seconds(24, stage.Music.Bpm) + round.BeatSeconds * .21); arena.Refresh();
            for (int i = 0; i < plan.Monsters.Count; i++)
            {
                string pattern = plan.Monsters[i].Attacks.Single().Pattern.Id;
                Assert.AreSame(Resources.Load<Sprite>(MonsterAttackDefinition.ResourceRoot + "iron-turtle/" + pattern + "/body/call-0"),
                    arena.MonsterPortraits[i].sprite);
                Assert.AreEqual(Vector3.one, arena.MonsterPortraits[i].rectTransform.localScale,
                    "An authored Call pose must not also receive the old procedural squash.");
            }
            Assert.AreEqual(2, arena.GetComponentsInChildren<Text>().Count(x => x.text == "CALL · 쿵"));
            round.Advance(RhythmTime.Seconds(28, stage.Music.Bpm) + round.BeatSeconds * .21); arena.Refresh();
            for (int i = 0; i < plan.Monsters.Count; i++)
            {
                string pattern = plan.Monsters[i].Attacks.Single().Pattern.Id;
                string image = pattern == "turtle-hold-tap" ? pattern + "/body/call-1" : "idle";
                Assert.AreSame(Resources.Load<Sprite>(MonsterAttackDefinition.ResourceRoot + "iron-turtle/" + image),
                    arena.MonsterPortraits[i].sprite);
                Assert.AreEqual(Quaternion.identity, arena.MonsterPortraits[i].rectTransform.localRotation);
            }
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
        public IEnumerator WaitingPatternsKeepTheirSpeciesSpecificCountingBehavior()
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
                bool walking = monster.Id == "clock-spirit";
                Assert.IsTrue(arena.GetComponentsInChildren<Text>().Any(x => x.text ==
                    (walking ? "쉼 · 인형의 걸음 따라가기" : "쉼 · 박자 기억하기")));
                Assert.AreEqual(walking ? 1 : 0, arena.MonsterAttacks.ActiveCount);
                if (walking)
                {
                    var doll = arena.GetComponentsInChildren<MonsterAttackGraphic>().Single();
                    Assert.Less(doll.Frame.Progress, 1); Assert.Greater(doll.Frame.Progress, .8);
                }
                Assert.AreEqual(0f, arena.MonsterPortraits.Single().rectTransform.anchoredPosition.x, .001f);
                Assert.AreEqual(1, round.Calls.Count); Assert.AreEqual(0, round.Results.Count);
                round.Advance(target); arena.Refresh();
                Assert.Less(arena.MonsterPortraits.Single().rectTransform.anchoredPosition.x, 0);
                Assert.IsTrue(arena.GetComponentsInChildren<Text>().Any(x => x.text == "RESPONSE"));
                Assert.AreEqual(1, arena.MonsterAttacks.ActiveCount);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AttackArtLoadsIncrementallyAndSortsNumberedFramesNumerically()
        {
            var texture = new Texture2D(4, 4); var sprites = new List<Sprite>();
            try
            {
                foreach (var name in new[] { "travel-10", "travel-2", "travel-0", "travel-1", "travel-3", "travel-4", "travel-5",
                    "travel-6", "travel-7", "travel-8", "travel-9", "perfect" })
                {
                    var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f); sprite.name = name; sprites.Add(sprite);
                }
                int loads = 0;
                var art = new MonsterAttackSprites(path =>
                { if (path != "test") return new Sprite[0]; loads++; return sprites.ToArray(); });
                Assert.AreEqual("travel-2", art.Get("test", "travel", 2).name);
                Assert.AreEqual("travel-10", art.Get("test", "travel", 10).name);
                Assert.AreEqual("travel-0", art.Get("test", "travel", 11).name);
                Assert.AreEqual("perfect", art.Get("test", "perfect").name);
                Assert.IsNull(art.Get("test", "miss")); Assert.AreEqual(1, loads);
                var empty = new MonsterAttackSprites(path => new Sprite[0]);
                Assert.IsNull(empty.Get("test", "travel"));
            }
            finally
            { foreach (var sprite in sprites) Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        [UnityTest]
        public IEnumerator HarpyFeathersWaitAtOneOriginThenReachThePunchAtTheirOwnBeat()
        {
            var monster = MonsterCatalog.All.Single(x => x.Id == "tresillo-bat");
            var round = new MonsterPreview(monster, monster.Patterns.Single(x => x.Id == "tresillo-taps")).Round;
            var arena = Arena(round); yield return null; Canvas.ForceUpdateCanvases();
            round.Advance(round.Plan.Calls.Last().Tick * round.BeatSeconds / 4 + round.BeatSeconds * .1); arena.Refresh();
            var slots = arena.GetComponentsInChildren<MonsterAttackGraphic>().OrderBy(x => x.name).ToArray();
            Assert.AreEqual(3, slots.Length);
            var origin = slots[0].rectTransform.anchoredPosition;
            Assert.IsTrue(slots.All(x => x.Frame.Progress == 0));
            Assert.IsTrue(slots.All(x => Vector2.Distance(origin, x.rectTransform.anchoredPosition) < .01f));
            Assert.AreEqual(3, slots.Select(x => x.rectTransform.localRotation).Distinct().Count());
            for (int i = 0; i < round.Notes.Count; i++)
            {
                var note = round.Notes[i];
                round.Advance(note.StartSeconds - round.BeatSeconds * .25); arena.Refresh();
                var size = ((RectTransform)arena.transform).rect.size;
                var target = Vector2.Scale(arena.HeroImpactPosition, size);
                Assert.AreEqual(.5, slots[i].Frame.Progress, 1e-6);
                Assert.Less(Vector2.Distance(Vector2.Lerp(origin, target, .5f), slots[i].rectTransform.anchoredPosition), .01f);
                round.Advance(note.StartSeconds); arena.Refresh();
                Assert.AreEqual(MonsterAttackPhase.Contact, slots[i].Frame.Phase);
                Assert.Less(Vector2.Distance(target, slots[i].rectTransform.anchoredPosition), .01f);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NekomataUsesOppositeLanesInBothPhaseChangesWithoutOldTailsRemaining()
        {
            var stage = MusicStage.Generate(MusicCatalog.All.Single(x => x.Id == "rapid-drive"));
            var monster = MonsterCatalog.All.Single(x => x.Id == "seesaw-goblin");
            var chain = monster.PatternPlanner.Candidates(stage, monster).Single(x => x.CallStartTick == 16);
            var round = new RhythmRound(BattlePlanner.Resolve(stage,
                new[] { new MonsterProposal(monster.Id, monster, new List<PatternChain> { chain }) }, 1));
            var arena = Arena(round); yield return null; Canvas.ForceUpdateCanvases();
            foreach (var attack in round.Plan.Attacks.Where(x => x.Pattern.Id == "seesaw-early-finish").Take(2))
            {
                var notes = round.Notes.Where(x => ReferenceEquals(x.Attack, attack)).ToArray();
                var slots = arena.GetComponentsInChildren<MonsterAttackGraphic>(true)
                    .Where(x => x.name.StartsWith("Attack " + attack.Id + "/step-")).OrderBy(x => x.name).ToArray();
                round.Advance(notes[0].StartSeconds); arena.Refresh();
                float firstY = TailTip(slots[0]).y, tailHeight = slots[0].rectTransform.rect.height;
                round.Advance(notes[1].StartSeconds); arena.Refresh();
                Assert.IsFalse(slots[0].gameObject.activeSelf, "The previous missed tail must already be gone.");
                float direction = notes[0].StartTick % 4 == 0 ? -1 : 1;
                Assert.Greater((TailTip(slots[1]).y - firstY) * direction, tailHeight * 1.5f);
                Assert.AreEqual(MonsterAttackPhase.Contact, slots[1].Frame.Phase);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static Vector3 TailTip(MonsterAttackGraphic slot) =>
            slot.rectTransform.TransformPoint(new Vector3(slot.rectTransform.rect.xMin, 0, 0));

        [UnityTest]
        public IEnumerator SustainedArtChangesThroughoutContactAndShowsAnEndCueAfterAnEarlyMiss()
        {
            foreach (string id in new[] { "turtle-long-hold", "bat-hold", "ray-short-dive", "ray-deep-dive" })
            {
                var monster = MonsterCatalog.All.Single(x => x.Patterns.Any(p => p.Id == id));
                var round = new MonsterPreview(monster, monster.Patterns.Single(x => x.Id == id)).Round;
                var note = round.Notes.Single(); var arena = Arena(round); yield return null; Canvas.ForceUpdateCanvases();
                round.Press(note.StartSeconds, 0, 0); arena.Refresh();
                var slot = arena.GetComponentsInChildren<MonsterAttackGraphic>().Single();
                Assert.IsNotNull(slot.CurrentSprite, id + " must use the supplied art.");
                var initialSize = slot.rectTransform.sizeDelta;
                round.Release(note.StartSeconds + .01, 0, 0);
                round.Advance(note.StartSeconds + (note.EndSeconds - note.StartSeconds) * .75); arena.Refresh();
                bool dive = note.Step.Kind == GestureKind.Dive;
                Assert.Less(dive ? slot.rectTransform.rect.width : slot.rectTransform.rect.height,
                    dive ? initialSize.x : initialSize.y);
                Assert.AreEqual(1, round.MissCount);
                var foreground = arena.GetComponentsInChildren<BattleArenaGraphic>().Single(x => x.name == "Battle effects and five weapons");
                round.Advance(note.EndSeconds); arena.Refresh();
                var finish = RenderMesh(foreground); int finishVertices = finish.vertexCount;
                var frozen = finish.vertices;
                if (dive) Assert.IsTrue(RenderMesh(slot).colors32.All(x => x.a == 0), "The trailing edge clears at the release beat.");
                round.Suspend(); arena.SetPaused(true); round.Advance(100); arena.Refresh();
                CollectionAssert.AreEqual(frozen, RenderMesh(foreground).vertices);
                round.Resume(false); arena.SetPaused(false); round.Advance(note.EndSeconds + .25); arena.Refresh();
                Assert.Greater(finishVertices, RenderMesh(foreground).vertexCount, "The end cue must expire independently of the early miss.");
                Assert.IsTrue(RenderMesh(slot).colors32.All(x => x.a == 0), "Expired contact art must not reappear during its result tail.");
                Assert.AreEqual(1, round.Results.Count);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static Mesh RenderMesh(Graphic graphic)
        {
            graphic.canvasRenderer.cull = false; graphic.SetVerticesDirty(); graphic.Rebuild(CanvasUpdate.PreRender);
            return graphic.canvasRenderer.GetMesh();
        }

        [UnityTest]
        public IEnumerator ThrownDollMeetsThePunchBeforeItsMissLandingAcrossLayouts()
        {
            var display = ScriptableObject.CreateInstance<PlayerMotionDisplay>();
            display.groundPosition = new Vector2(.18f, .19f);
            try
            {
                var monster = MonsterCatalog.All.Single(m => m.Id == "clock-spirit");
                var pattern = monster.Patterns.Single(p => p.Id == "clock-quick-tap");
                foreach (float viewportHeight in new[] { 720f, 800f })
                foreach (float scale in new[] { .6f, .9f })
                {
                    display.characterScale = scale;
                    var round = new MonsterPreview(monster, pattern).Round;
                    var note = round.Notes.Single(); var arena = Arena(round, display);
                    ((RectTransform)arena.transform).sizeDelta = new Vector2(1280, viewportHeight);
                    yield return null; Canvas.ForceUpdateCanvases();
                    round.Advance(note.StartSeconds); arena.Refresh();
                    var slot = arena.GetComponentsInChildren<MonsterAttackGraphic>().Single();
                    var size = ((RectTransform)arena.transform).rect.size;
                    Assert.AreEqual(MonsterAttackPhase.Contact, slot.Frame.Phase);
                    Assert.AreEqual(arena.HeroImpactPosition.x * size.x, slot.rectTransform.anchoredPosition.x, .01f);
                    Assert.AreEqual(arena.HeroImpactPosition.y * size.y, slot.rectTransform.anchoredPosition.y, .01f);
                    Assert.AreEqual(0, round.Results.Count);
                    double deadline = note.StartSeconds + round.HalfMissWindow;
                    double duration = MonsterAttackTimeline.MissApproachSeconds(round.BeatSeconds);
                    round.Advance(deadline + duration * .5); arena.Refresh();
                    Assert.AreEqual(1, round.MissCount); Assert.AreEqual(MonsterAttackPhase.Travel, slot.Frame.Phase);
                    var position = slot.rectTransform.anchoredPosition;
                    round.Suspend(); arena.SetPaused(true); round.Advance(100); arena.Refresh();
                    Assert.AreEqual(position, slot.rectTransform.anchoredPosition);
                    round.Resume(false); arena.SetPaused(false); round.Advance(deadline + duration); arena.Refresh();
                    Assert.AreEqual(MonsterAttackPhase.Miss, slot.Frame.Phase);
                    float bottom = slot.rectTransform.anchoredPosition.y - slot.rectTransform.rect.height * slot.rectTransform.pivot.y;
                    Assert.AreEqual(arena.HeroGroundPosition.y * size.y, bottom, .01f);
                    Assert.AreEqual(1, round.Results.Count);
                }
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.DestroyImmediate(display); }
        }

        [UnityTest]
        public IEnumerator AttackSlotsFollowPlayerCalibrationAndKeepTheirPhaseWhilePaused()
        {
            var display = ScriptableObject.CreateInstance<PlayerMotionDisplay>();
            display.groundPosition = new Vector2(.18f, .19f); display.characterScale = .6f;
            try
            {
                var round = Round(3, new PatternStep(GestureKind.Tap, 0)); var arena = Arena(round, display);
                yield return null; Canvas.ForceUpdateCanvases();
                round.Advance(1.75); arena.Refresh();
                var slots = arena.GetComponentsInChildren<MonsterAttackGraphic>();
                Assert.AreEqual(3, slots.Length); Assert.AreEqual(3, arena.MonsterAttacks.ActiveCount);
                var progress = slots.Select(x => x.Frame.Progress).ToArray();
                round.Suspend(); arena.SetPaused(true); round.Advance(999);
                ((RectTransform)arena.transform).sizeDelta = new Vector2(1280, 800);
                display.characterScale = .8f; arena.Refresh();
                CollectionAssert.AreEqual(progress, slots.Select(x => x.Frame.Progress).ToArray());
                round.Resume(false); arena.SetPaused(false); round.Advance(2); arena.Refresh();
                var size = ((RectTransform)arena.transform).rect.size;
                foreach (var slot in slots)
                {
                    Assert.AreEqual(MonsterAttackPhase.Contact, slot.Frame.Phase);
                    Assert.AreEqual(arena.HeroImpactPosition.x * size.x, slot.rectTransform.anchoredPosition.x, .01f);
                    Assert.AreEqual(arena.HeroImpactPosition.y * size.y, slot.rectTransform.anchoredPosition.y, .01f);
                    Assert.IsFalse(slot.raycastTarget);
                }
                round.Press(2, 0, 0); arena.Refresh();
                Assert.AreEqual(3, round.Results.Count);
                Assert.IsTrue(slots.All(x => x.Frame.Phase == MonsterAttackPhase.Contact));
                round.Advance(2 + PlayerMotionTimeline.TapPreparationDuration(round.BeatSeconds)); arena.Refresh();
                Assert.IsTrue(slots.All(x => x.Frame.Phase == MonsterAttackPhase.Perfect));
                round.Advance(3); arena.Refresh(); Assert.AreEqual(0, arena.MonsterAttacks.ActiveCount);
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.DestroyImmediate(display); }
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
            Assert.AreEqual(PlayerMotionPhase.Prepare, arena.CurrentHeroMotion.Phase);
            Assert.AreEqual(0, arena.CurrentHeroMotion.Index % 4);
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
                new PatternStep(GestureKind.Shake, 0), new PatternStep(GestureKind.Flick, 8));
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
            Assert.AreEqual(4, round.PerfectCount); Assert.AreEqual(3, arena.ActiveResponseEffects,
                "The early Shake response has expired; only the coincident Hold, Dive and Flick endings react now.");
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
            Assert.AreEqual(0, a.CurrentHeroMotion.Index % 4); Assert.AreEqual(0, b.CurrentHeroMotion.Index % 4);
            perfect.Advance(2.04); a.Refresh(); half.Advance(2.14); b.Refresh();
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
                slots.Add(new SlotTemplate(GestureKind.Shake, tick));
                foreach (var kind in new[] { GestureKind.Hold, GestureKind.Dive })
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
