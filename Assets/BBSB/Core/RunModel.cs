using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public enum StageKind { Monster, Elite, Upgrade, Rest, Shop, Boss }
    public enum RunPhase { Map, Stage, Reward, FieldCleared, GameOver }
    public enum RewardKind { Weapon, Item, Augment }

    public sealed class StageNode
    {
        private readonly List<string> next = new List<string>();
        public string Id { get; }
        public int Row { get; }
        public int Column { get; }
        public StageKind Kind { get; }
        public IReadOnlyList<string> Next { get; }
        public bool IsBattle => Kind == StageKind.Monster || Kind == StageKind.Elite || Kind == StageKind.Boss;

        internal StageNode(int field, int row, int column, StageKind kind)
        {
            Id = "f" + field + "r" + row + "c" + column;
            Row = row;
            Column = column;
            Kind = kind;
            Next = next.AsReadOnly();
        }

        internal void Connect(StageNode target)
        {
            if (!next.Contains(target.Id)) next.Add(target.Id);
        }

        internal void Disconnect(StageNode target) { next.Remove(target.Id); }
    }

    public sealed class FieldMap
    {
        public const int StageCount = 4;
        public const int Width = 3;
        public int Number { get; }
        public IReadOnlyList<StageNode> Nodes { get; }

        internal FieldMap(int number, List<StageNode> nodes)
        {
            Number = number;
            Nodes = nodes.AsReadOnly();
        }

        public StageNode Find(string id)
        {
            foreach (var node in Nodes) if (node.Id == id) return node;
            return null;
        }
    }

    // Stable across Unity/Mono and standalone .NET tests, without touching UnityEngine.Random.
    public sealed class SeededRandom
    {
        private uint state;
        public SeededRandom(int seed) { state = unchecked((uint)seed); if (state == 0) state = 0x6d2b79f5u; }
        public int Next(int exclusiveMax)
        {
            if (exclusiveMax <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (int)(state % (uint)exclusiveMax);
        }
        public void Shuffle<T>(IList<T> values)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int other = Next(i + 1);
                T temp = values[i]; values[i] = values[other]; values[other] = temp;
            }
        }
    }

    public sealed class RunRules
    {
        public int StartingHealth { get; }
        public int StartingGold { get; }
        public int RestPercent { get; }
        public const int WeaponSlots = 5;
        public const int MaximumUpgrade = 3;

        public RunRules(int startingHealth = 100, int startingGold = 60, int restPercent = 30)
        {
            if (startingHealth <= 0 || startingGold < 0 || restPercent < 1 || restPercent > 100)
                throw new ArgumentOutOfRangeException(nameof(startingHealth), "Invalid run configuration.");
            StartingHealth = startingHealth; StartingGold = startingGold; RestPercent = restPercent;
        }
    }

    public sealed class WeaponState
    {
        public string DefinitionId { get; }
        public int Level { get; internal set; }
        public WeaponState(string definitionId) { DefinitionId = definitionId; }
    }

    public sealed class ContentDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public RewardKind Kind { get; }
        public int Price { get; }
        public ContentDefinition(string id, string name, string description, RewardKind kind, int price)
        { Id = id; Name = name; Description = description; Kind = kind; Price = price; }
    }

    public sealed class Offer
    {
        public ContentDefinition Content { get; }
        public int Price { get; internal set; }
        public bool Purchased { get; internal set; }
        internal Offer(ContentDefinition content, int price) { Content = content; Price = price; }
    }
}
