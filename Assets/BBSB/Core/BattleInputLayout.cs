using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    [Flags] public enum InputExtensions { None = 0, Left = 1, Right = 2 }

    public static class BattleInputLayout
    {
        // Keep the original five IDs stable. Extensions have their own IDs and availability.
        public const int MainLaneCount = 5, LaneCount = 7, Left = 5, Right = 6;
        public static readonly IReadOnlyList<int> DisplayOrder = Array.AsReadOnly(new[] { Left, 0, 1, 2, 3, 4, Right });
        public static string Key(int slot)
        {
            switch (slot)
            {
                case 0: return "D"; case 1: return "F"; case 2: return "Space";
                case 3: return "J"; case 4: return "K"; case Left: return "S"; case Right: return "L";
                default: throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }
        public static int Position(int slot) => slot == Left ? -1 : slot == Right ? MainLaneCount : slot;
        public static int SlotAtPosition(int position) => position == -1 ? Left : position == MainLaneCount ? Right :
            position >= 0 && position < MainLaneCount ? position : -1;
        public static bool Available(int slot, InputExtensions extensions) => slot >= 0 && slot < MainLaneCount ||
            slot == Left && (extensions & InputExtensions.Left) != 0 || slot == Right && (extensions & InputExtensions.Right) != 0;
        public static void Validate(InputExtensions extensions)
        {
            if ((extensions & ~(InputExtensions.Left | InputExtensions.Right)) != 0) throw new ArgumentOutOfRangeException(nameof(extensions));
        }
    }
}
