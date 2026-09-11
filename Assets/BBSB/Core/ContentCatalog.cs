using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    // Starter data. Rhythm definitions and combat modifiers will be supplied by the combat layer.
    public static class ContentCatalog
    {
        private static readonly ContentDefinition[] definitions = {
            new ContentDefinition("sword", "한손검", "두 번의 Tap으로 공격하는 기본 무기.", RewardKind.Weapon, 45),
            new ContentDefinition("shield", "방패", "누름을 유지하며 적의 효과를 추가 상쇄하는 무기.", RewardKind.Weapon, 45),
            new ContentDefinition("spear", "창", "한 번의 Tap에 힘을 집중하는 무기.", RewardKind.Weapon, 50),
            new ContentDefinition("hammer", "해머", "묵직한 Tap 공격을 사용하는 무기.", RewardKind.Weapon, 55),
            new ContentDefinition("dagger", "단검", "짧은 리듬에 배치하는 가벼운 무기.", RewardKind.Weapon, 40),
            new ContentDefinition("greatsword", "대검", "Dive의 마지막 떼기에 힘을 싣는 무기.", RewardKind.Weapon, 65),
            new ContentDefinition("bell", "진동 방울", "누른 채 흔드는 Shake에 반응하는 무기.", RewardKind.Weapon, 60),
            new ContentDefinition("blade", "비검", "튕기는 Flick에 반응하는 무기.", RewardKind.Weapon, 60),
            new ContentDefinition("potion", "회복 물약", "가방에서 사용하면 체력 25 회복. 최대 체력을 넘지 않음.", RewardKind.Item, 25),
            new ContentDefinition("vitality", "생명의 박동", "최대 체력 +20, 현재 체력도 20 회복. 중첩 가능.", RewardKind.Augment, 65),
            new ContentDefinition("recovery", "깊은 호흡", "휴식할 때마다 추가로 체력 10 회복. 중첩 가능.", RewardKind.Augment, 55),
            new ContentDefinition("bargain", "흥정의 리듬", "상점 가격 20% 할인. 추가 획득마다 20%, 최대 60%.", RewardKind.Augment, 55)
        };

        public static ContentDefinition Find(string id)
        {
            foreach (var definition in definitions) if (definition.Id == id) return definition;
            throw new ArgumentException("Unknown content: " + id, nameof(id));
        }

        public static ContentDefinition Pick(RewardKind kind, SeededRandom random)
        {
            var pool = new List<ContentDefinition>();
            foreach (var definition in definitions) if (definition.Kind == kind) pool.Add(definition);
            return pool[random.Next(pool.Count)];
        }

        public static string StageName(StageKind kind)
        {
            switch (kind)
            {
                case StageKind.Monster: return "몬스터";
                case StageKind.Elite: return "엘리트";
                case StageKind.Upgrade: return "강화";
                case StageKind.Rest: return "휴식";
                case StageKind.Shop: return "상점";
                case StageKind.Boss: return "보스";
                case StageKind.Mystery: return "?";
                default: throw new System.ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
