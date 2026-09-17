using BBSB.Runtime;
using BBSB.Runtime.UI;
using NUnit.Framework;
using UnityEngine;

namespace BBSB.Tests
{
    public sealed class ShoulderViewPresentationTests
    {
        [Test]
        public void InstalledActorReferencesCoverEveryMonsterAndEffectWithoutRequiringBackdrops()
        {
            var art = Resources.Load<ShoulderViewPresentation>(ShoulderViewPresentation.ResourcePath);
            if (art == null) Assert.Ignore("Install BBSB_ShoulderView_Art.zip to run the native art integration test.");
            foreach (var monster in Resources.LoadAll<MonsterAuthoring>(MonsterAuthoring.ResourceFolder))
            {
                if (!monster.includeInEncounters) continue;
                Assert.IsNotNull(art.FindProjectile(monster.monsterId), monster.monsterId + " projectile");
                CheckActor(monster.actorPrefab, monster.controller, monster.portrait, monster.spriteReferenceHeight,
                    new[] { "Idle", "Call", "Attack" });
            }
            foreach (var sprite in new[] { art.parry, art.guard, art.slash, art.arrow, art.impact, art.noteConfirm, art.noteShatter })
                Assert.IsNotNull(sprite, "An effect reference is missing.");
            var hero = Resources.Load<PlayerAuthoring>(PlayerAuthoring.ResourcePath);
            Assert.IsFalse(hero.useLegacyFrames);
            CheckActor(hero.visualPrefab, hero.controller, hero.portrait, hero.spriteReferenceHeight,
                new[] { "Idle", "TapImpact", "Guard", "Bow", "Hit" });
        }
        [Test]
        public void LateBackdropsImportIndependentlyAndRetainArtistChanges()
        {
            var art = ScriptableObject.CreateInstance<ShoulderViewPresentation>();
            var first = new Texture2D(2, 2); var second = new Texture2D(2, 2);
            try
            {
                art.installedVersion = 1;
                Assert.IsFalse(art.ImportStageOnce("POP", null, Color.blue));
                Assert.IsFalse(art.HasImportedStage("POP"));
                Assert.IsTrue(art.ImportStageOnce("BARD", first, Color.yellow));
                Assert.IsNull(art.FindStage("POP-01", "POP"));
                Assert.AreSame(first, art.FindStage("BARD-06", "BARD").backdrop);
                var authored = art.stages[0]; authored.backdrop = second; authored.sky = Color.green;
                Assert.IsFalse(art.ImportStageOnce("BARD", first, Color.red));
                Assert.AreSame(second, authored.backdrop); Assert.AreEqual(Color.green, authored.sky);
                Assert.IsTrue(art.ImportStageOnce("POP", first, Color.blue));
                Assert.AreEqual(2, art.stages.Length); Assert.AreSame(authored, art.stages[0]);
                art.stages = System.Array.Empty<ShoulderViewPresentation.Stage>();
                Assert.IsFalse(art.ImportStageOnce("BARD", first, Color.red));
                Assert.IsEmpty(art.stages, "Reimport must not recreate an intentionally removed stage entry.");
                Assert.AreEqual(1, art.installedVersion, "Backdrop import must not invalidate actor installation.");
            }
            finally { Object.DestroyImmediate(art); Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
        }
        [Test]
        public void ExistingStageOverridesSurviveTheFirstImportOfTrackingMetadata()
        {
            var art = ScriptableObject.CreateInstance<ShoulderViewPresentation>();
            var texture = new Texture2D(2, 2);
            try
            {
                var authored = new ShoulderViewPresentation.Stage { id = "BARD", backdrop = null, sky = Color.green };
                art.stages = new[] { authored }; art.importedStageIds = null;
                Assert.IsTrue(art.ImportStageOnce("BARD", texture, Color.red));
                Assert.AreSame(authored, art.stages[0]); Assert.IsNull(authored.backdrop);
                Assert.AreEqual(Color.green, authored.sky); Assert.IsTrue(art.HasImportedStage("BARD"));
                Assert.IsFalse(art.ImportStageOnce("BARD", texture, Color.red)); Assert.AreEqual(1, art.stages.Length);
            }
            finally { Object.DestroyImmediate(art); Object.DestroyImmediate(texture); }
        }
        private static void CheckActor(GameObject prefab, RuntimeAnimatorController controller, Sprite portrait, float reference, string[] states)
        {
            Assert.IsNotNull(prefab); Assert.IsNotNull(controller); Assert.IsNotNull(portrait);
            Assert.IsNotNull(prefab.GetComponentInChildren<SpriteRenderer>());
            var host = new GameObject("Native shoulder actor test", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = host.AddComponent<ActorPrefabView>(); view.Initialize(prefab, controller, portrait, reference);
                var renderer = view.Instance.GetComponentInChildren<SpriteRenderer>();
                var mesh = host.GetComponentInChildren<SpriteCanvasGraphic>();
                Sprite previous = null;
                foreach (string state in states)
                {
                    Assert.IsTrue(view.Sample("Base Layer." + state, .2, 1, false), prefab.name + " / " + state);
                    Assert.IsNotNull(renderer.sprite); Assert.AreNotSame(previous, renderer.sprite, state + " must change the Sprite reference.");
                    Assert.AreSame(renderer.sprite.texture, mesh.mainTexture, "Canvas must follow the animated SpriteRenderer.");
                    previous = renderer.sprite;
                }
            }
            finally { Object.DestroyImmediate(host); }
        }
        [Test]
        public void StageOverridesTakePriorityAndIncompleteOverridesKeepTheMapFallback()
        {
            var art = ScriptableObject.CreateInstance<ShoulderViewPresentation>();
            var texture = new Texture2D(2, 2);
            try
            {
                var map = new ShoulderViewPresentation.Stage { id = "BARD", backdrop = texture };
                var song = new ShoulderViewPresentation.Stage { id = "BARD-06", backdrop = texture };
                art.stages = new[] { map, song };
                Assert.AreSame(song, art.FindStage("BARD-06", "BARD"));
                song.backdrop = null; Assert.AreSame(map, art.FindStage("BARD-06", "BARD"));
                art.stages = null; Assert.IsNull(art.FindStage("BARD-06", "BARD"));
                art.projectiles = null; Assert.IsNull(art.FindProjectile("tap-slime"));
            }
            finally { Object.DestroyImmediate(art); Object.DestroyImmediate(texture); }
        }
    }
}
