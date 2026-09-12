using BBSB.Core;
using BBSB.Runtime.UI;
using NUnit.Framework;
using UnityEngine;

namespace BBSB.Tests
{
    public sealed class MonsterArtIntegrationTests
    {
        [Test]
        public void UploadedPackConnectsEveryIconBodyPoseAndAttackPhaseByItsResourcePath()
        {
            var sprites = new MonsterAttackSprites();
            int icons = 0, bodies = 0, attacks = 0;
            foreach (var monster in MonsterCatalog.All)
            {
                SameImage(sprites, MonsterCodexView.IconRoot + "Monsters", monster.Id); icons++;
                string root = MonsterAttackDefinition.ResourceRoot + monster.Id;
                SameImage(sprites, root, "idle"); bodies++;
                foreach (var pattern in monster.Patterns)
                {
                    SameImage(sprites, MonsterCodexView.IconRoot + "Patterns", pattern.Id); icons++;
                    string body = root + "/" + pattern.Id + "/body";
                    for (int i = 0; i < pattern.Call.Count; i++) { SameImage(sprites, body, "call-" + i); bodies++; }
                    SameImage(sprites, body, "attack"); SameImage(sprites, body, "recover"); bodies += 2;
                }
            }
            foreach (var definition in MonsterAttackCatalog.All)
            {
                string folder = definition.ResourceFolder;
                foreach (var phase in new[] { "contact", "perfect", "half-miss", "miss" })
                { SameImage(sprites, folder, phase); attacks++; }
                if (definition.Motion == MonsterAttackMotion.Materialize) continue;
                SameImage(sprites, folder, "spawn"); attacks++;
                if (definition.Motion == MonsterAttackMotion.Walk)
                {
                    for (int i = 0; i < 4; i++) { SameImage(sprites, folder, "travel", i, "travel-" + i); attacks++; }
                    SameImage(sprites, folder, "travel", 4, "travel-0");
                }
                else { SameImage(sprites, folder, "travel"); attacks++; }
                if (definition.Motion == MonsterAttackMotion.WaitRush) { SameImage(sprites, folder, "wait"); attacks++; }
            }
            Assert.AreEqual(36, icons); Assert.AreEqual(93, bodies); Assert.AreEqual(223, attacks);
        }

        [Test]
        public void EveryVisiblePreviewPhaseUsesItsOwnUploadedImageAtTheRealResponseTime()
        {
            var sprites = new MonsterAttackSprites();
            foreach (var monster in MonsterCatalog.All)
            foreach (var pattern in monster.Patterns)
            {
                var preview = new MonsterPreview(monster, pattern);
                foreach (var note in preview.Round.Notes)
                {
                    var definition = MonsterAttackCatalog.For(note);
                    for (double time = 0; time < preview.DurationSeconds; time += preview.BeatSeconds / 64)
                    {
                        var frame = MonsterAttackTimeline.Evaluate(note, definition, time, preview.BeatSeconds, preview.Round.HalfMissWindow);
                        if (!frame.Visible) continue;
                        Assert.IsNotNull(sprites.Attack(definition, frame, 2, preview.BeatSeconds, out bool exact), definition.ResourceFolder);
                        Assert.IsTrue(exact, definition.ResourceFolder + "/" + frame.ImageName + " must not use another phase as a fallback.");
                    }
                    var arrival = MonsterAttackTimeline.Evaluate(note, definition, note.StartSeconds, preview.BeatSeconds, preview.Round.HalfMissWindow);
                    Assert.AreEqual(MonsterAttackPhase.Contact, arrival.Phase);
                    Assert.AreSame(Resources.Load<Sprite>(definition.ResourceFolder + "/contact"),
                        sprites.Attack(definition, arrival, 2, preview.BeatSeconds, out _));
                }
            }
        }

        private static void SameImage(MonsterAttackSprites sprites, string folder, string clip, double frame = 0, string file = null)
        {
            string path = folder + "/" + (file ?? clip);
            var imported = Resources.LoadAll<Sprite>(path);
            Assert.AreEqual(1, imported.Length, path + " is a full single-pose PNG, not an automatically sliced atlas.");
            var expected = imported[0];
            Assert.AreEqual(expected.texture.width, expected.rect.width, path + " must retain the complete canvas.");
            Assert.AreEqual(expected.texture.height, expected.rect.height, path + " must retain the complete canvas.");
            Assert.AreSame(expected, sprites.Get(folder, clip, frame), path);
        }
    }
}
