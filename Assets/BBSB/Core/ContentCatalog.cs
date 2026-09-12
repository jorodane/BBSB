using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    // Item/economy data; functional weapon patterns and effects live in WeaponCatalog.
    public static class ContentCatalog
    {
        private static readonly ContentDefinition[] definitions = {
            Weapon("sword", "한손검", 45),
            Weapon("shield", "방패", 45),
            Weapon("spear", "창", 50),
            Weapon("hammer", "해머", 55),
            Weapon("dagger", "단검", 40),
            Weapon("greatsword", "대검", 65),
            Weapon("bell", "진동 방울", 60),
            Weapon("blade", "비검", 60),
            new ContentDefinition("potion", "회복 물약", "가방에서 사용하면 체력 25 회복. 최대 체력을 넘지 않음.", RewardKind.Item, 25),
            new ContentDefinition("vitality", "생명의 박동", "최대 체력 +20, 현재 체력도 20 회복. 중첩 가능.", RewardKind.Augment, 65),
            new ContentDefinition("recovery", "깊은 호흡", "휴식할 때마다 추가로 체력 10 회복. 중첩 가능.", RewardKind.Augment, 55),
            new ContentDefinition("bargain", "흥정의 리듬", "상점 가격 20% 할인. 추가 획득마다 20%, 최대 60%.", RewardKind.Augment, 55)
        };

        private static ContentDefinition Weapon(string id, string name, int price)
        {
            var weapon = WeaponCatalog.Find(id);
            return new ContentDefinition(id, name, weapon.PatternLabel + "\n" + weapon.EffectLabel, RewardKind.Weapon, price);
        }

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
