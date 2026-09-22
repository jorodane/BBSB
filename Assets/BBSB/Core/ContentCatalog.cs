using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    // Item/economy data; weapon actions and effects live in WeaponCatalog.
    public static class ContentCatalog
    {
        private static readonly ContentDefinition[] definitions = Build();
        private static ContentDefinition[] Build()
        {
            var result = new List<ContentDefinition> {
            Weapon("sword", "한손검", 45),
            Weapon("shield", "버클러", 45),
            Weapon("round-shield", "원형 방패", 50),
            Weapon("tower-shield", "대형 방패", 60),
            Weapon("heater-shield", "Heater Shield", 45),
            Weapon("wide-shield", "넓은 방패", 65),
            Weapon("resonance-shield", "축적 방패", 60),
            Weapon("spear", "창", 50),
            Weapon("hammer", "해머", 55),
            Weapon("dagger", "단검", 40),
            Weapon("dual-swords", "쌍검", 50),
            Weapon("staff", "봉", 55),
            Weapon("spirit-bell", "신령 방울", 60),
            Weapon("greatsword", "대검", 65),
            Weapon("bell", "진동 방울", 60),
            Weapon("blade", "비검", 60),
            Weapon("bow", "활", 50),
            Weapon("crossbow", "석궁", 60),
            Weapon("wand", "마도봉", 60),
            new ContentDefinition("potion", "회복 물약", "가방에서 사용하면 체력 25 회복. 최대 체력을 넘지 않음.", RewardKind.Item, 25),
            new ContentDefinition("vitality", "생명의 박동", "최대 체력 +20, 현재 체력도 20 회복. 중첩 가능.", RewardKind.Augment, 65),
            new ContentDefinition("recovery", "깊은 호흡", "휴식할 때마다 추가로 체력 10 회복. 중첩 가능.", RewardKind.Augment, 55),
            new ContentDefinition("bargain", "흥정의 리듬", "상점 가격 20% 할인. 추가 획득마다 20%, 최대 60%.", RewardKind.Augment, 55),
            new ContentDefinition("force-rhythm", "힘의 리듬", "모든 무기의 공격 피해 +10%. 중첩 가능.", RewardKind.Augment, 65),
            new ContentDefinition("steady-guard", "안정된 방어", "홀드 방어 감소율 +5%p. 최대 +25%p.", RewardKind.Augment, 65),
            new ContentDefinition("quick-rest", "짧은 쉼표", "무기 쿨타임 1박 감소. 최대 3박 감소, 최소 1박.", RewardKind.Augment, 70)
            };
            foreach (var entry in WeaponExpansion.All) result.Add(Weapon(entry.Definition.Id, entry.Name, entry.Price));
            foreach (var part in NotePartCatalog.All)
                result.Add(new ContentDefinition(part.Id, part.Name, part.Description,
                    part.Kind == NotePartKind.Frame ? RewardKind.Frame : RewardKind.BeatInjection, 40));
            return result.ToArray();
        }

        private static ContentDefinition Weapon(string id, string name, int price)
        {
            var weapon = WeaponCatalog.Find(id);
            return new ContentDefinition(id, name, weapon.ActionLabelAt(WeaponRarity.Common) + "\n" + weapon.EffectLabelAt(WeaponRarity.Common) + "\n" + weapon.ProgressionLabel, RewardKind.Weapon, price);
        }

        public static ContentDefinition Find(string id)
        {
            foreach (var definition in definitions) if (definition.Id == id) return definition;
            throw new ArgumentException("Unknown content: " + id, nameof(id));
        }

        public static ContentDefinition Pick(RewardKind kind, SeededRandom random, bool includeCombatGrowth = true)
        {
            var pool = new List<ContentDefinition>();
            foreach (var definition in definitions) if (definition.Kind == kind && (includeCombatGrowth ||
                (definition.Id != "force-rhythm" && definition.Id != "steady-guard" && definition.Id != "quick-rest"))) pool.Add(definition);
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
