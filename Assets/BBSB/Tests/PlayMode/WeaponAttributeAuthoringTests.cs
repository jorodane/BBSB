using System;
using BBSB.Core;
using BBSB.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BBSB.Tests
{
    public sealed class WeaponAttributeAuthoringTests
    {
        private static WeaponPhraseAuthoring Asset(string id, WeaponBeatSide side, int offset, PhraseEffect effect, float amount)
        {
            var asset = ScriptableObject.CreateInstance<WeaponPhraseAuthoring>();
            asset.weaponId = id; asset.anyAttribute = false; asset.attribute = WeaponAttribute.Dual;
            asset.bothSides = false; asset.beatSide = side; asset.startLaneOffset = offset;
            asset.lengthBeats = 1; asset.notes = new[] { new WeaponPhraseAuthoring.Note { damage = amount, effect = effect } };
            return asset;
        }
        [Test] public void AuthoredAttributeSideAndStartingOffsetChooseIndependentEffects()
        {
            var leftLight = Asset("staff", WeaponBeatSide.Light, 0, PhraseEffect.Heal, 4);
            var rightDark = Asset("staff", WeaponBeatSide.Dark, 1, PhraseEffect.Strike, 30);
            try
            {
                var weapon = new WeaponState("staff", WeaponAttribute.Dual);
                var set = WeaponPhraseAuthoring.BuildSetsFor(new[] { weapon }, new[] { leftLight, rightDark })[0];
                Assert.AreEqual(PhraseEffect.Heal, set.For(0, WeaponBeatSide.Light).Notes[0].Effect);
                Assert.AreEqual(30m, set.For(1, WeaponBeatSide.Dark).Notes[0].Damage);
                Assert.AreEqual(8, set.For(0, WeaponBeatSide.Dark).LengthBeats);
                Assert.AreEqual(18m, set.For(0, WeaponBeatSide.Dark).Notes[2].Damage);
                Assert.AreEqual(1, set.For(1, WeaponBeatSide.Light).Notes[2].LaneOffset);
                var ordinary = WeaponPhraseAuthoring.BuildSetsFor(new[] { new WeaponState("staff") }, new[] { leftLight, rightDark })[0];
                Assert.AreEqual(8, ordinary.Starts[0].LengthBeats);
                Assert.AreEqual(PhraseEffect.Strike, ordinary.Starts[0].Notes[0].Effect);
            }
            finally { Object.DestroyImmediate(leftLight); Object.DestroyImmediate(rightDark); }
        }
        [Test] public void CommonAuthoringRemainsFallbackAndExactAttributeWins()
        {
            var common = Asset("dagger", WeaponBeatSide.Light, 0, PhraseEffect.Strike, 2);
            var exact = Asset("dagger", WeaponBeatSide.Dark, 0, PhraseEffect.Heal, 8);
            common.anyAttribute = common.bothSides = true;
            try
            {
                var set = WeaponPhraseAuthoring.BuildSetsFor(new[] { new WeaponState("dagger", WeaponAttribute.Dual) }, new[] { common, exact })[0];
                Assert.AreEqual(2m, set.For(0, WeaponBeatSide.Light).Notes[0].Damage);
                Assert.AreEqual(PhraseEffect.Heal, set.For(0, WeaponBeatSide.Dark).Notes[0].Effect);
            }
            finally { Object.DestroyImmediate(common); Object.DestroyImmediate(exact); }
        }
        [Test] public void ChaosAuthoringPreservesTwoSectionsAndDefaultInfiniteTransitions()
        {
            var authored = Asset("sword", WeaponBeatSide.Light, 0, PhraseEffect.Heal, 4);
            authored.attribute = WeaponAttribute.Chaos; authored.bothSides = true;
            authored.repeat = true; authored.maximumCycles = 2; authored.lengthBeats = 8;
            authored.completionCooldownBeats = 6;
            try
            {
                var set = WeaponPhraseAuthoring.BuildSetsFor(new[] { new WeaponState("sword", WeaponAttribute.Chaos) }, new[] { authored })[0];
                Assert.IsTrue(set.RandomizeChaosSections);
                Assert.AreEqual(2, set.LightStarts[0].MaximumCycles);
                Assert.AreEqual(6, set.DarkStarts[0].CompletionCooldownBeats);
                Assert.AreEqual(PhraseEffect.Heal, set.DarkStarts[0].Notes[0].Effect);
                var defaults = WeaponPhraseAuthoring.BuildSetsFor(new[] { new WeaponState("dagger", WeaponAttribute.Chaos) }, Array.Empty<WeaponPhraseAuthoring>())[0];
                Assert.IsFalse(defaults.RandomizeChaosSections);
                Assert.AreEqual(0, defaults.LightStarts[0].MaximumCycles);
                Assert.AreEqual(4.5, defaults.LightTransitions[0].LengthBeats);
            }
            finally { Object.DestroyImmediate(authored); }
        }
        [Test] public void PlayerAssetAndJsonPreferencesPreserveWeaponAttributes()
        {
            var player = ScriptableObject.CreateInstance<PlayerAuthoring>();
            try
            {
                player.characterId = "attribute-save-test";
                player.startingWeapons = new[] {
                    new PlayerAuthoring.Starter { key = "light", weaponId = "dagger", defaultOffset = 0 },
                    new PlayerAuthoring.Starter { key = "dark", weaponId = "dagger", defaultOffset = 4, attribute = WeaponAttribute.Dark }
                };
                var character = player.BuildCharacter(); var draft = new StartingLoadout(character);
                Assert.IsTrue(draft.EquipWeaponAtCenter(1, 2));
                var preferences = new RunStartPreferences(); preferences.Remember(character, draft.Capture());
                using (new RunStartPreferenceScope())
                {
                    preferences.Save(); var restored = RunStartPreferences.Load();
                    var next = new StartingLoadout(character, restored.Find(character.Id));
                    Assert.AreEqual(WeaponAttribute.Dark, next.Equipment.At(2).Weapon.Attribute);
                    Assert.AreEqual(WeaponAttribute.Light, next.Equipment.At(0).Weapon.Attribute);
                }
                var legacy = JsonUtility.FromJson<StartingWeaponBinding>("{\"key\":\"light\",\"weaponId\":\"dagger\",\"lanes\":1,\"equipped\":true,\"offset\":0}");
                Assert.AreEqual(WeaponAttribute.Light, legacy.attribute);
            }
            finally { Object.DestroyImmediate(player); }
        }
    }
}
