using BBSB.Runtime;
using BBSB.Runtime.UI;
using NUnit.Framework;
using UnityEngine;

namespace BBSB.Tests
{
    public sealed class ShoulderViewPresentationTests
    {
        [Test]
        public void InstalledArtReferencesCoverEveryMapMonsterAndEffect()
        {
            var art = Resources.Load<ShoulderViewPresentation>(ShoulderViewPresentation.ResourcePath);
            if (art == null) Assert.Ignore("Install BBSB_ShoulderView_Art.zip to run the native art integration test.");
            foreach (string map in new[] { "POP", "RNB", "JAZZ", "RAP", "BARD", "CELT", "NEW", "METAL" })
                Assert.IsNotNull(art.FindStage(map + "-01", map)?.backdrop, map);
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
