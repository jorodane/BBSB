using System.Collections.Generic;

namespace BBSB.Core
{
    public static class MapGenerator
    {
        public static FieldMap Generate(int field, SeededRandom random)
        {
            var nodes = new List<StageNode>();
            var grid = new StageNode[3, FieldMap.Width];
            // The opening offers combat, recovery, or shopping; later rows introduce elites/upgrades.
            // Sample distribution: all five normal stage types appear in every field.
            var opening = new List<StageKind> { StageKind.Monster, StageKind.Rest, StageKind.Shop };
            var later = new List<StageKind> { StageKind.Monster, StageKind.Elite, StageKind.Upgrade,
                StageKind.Rest, StageKind.Shop, StageKind.Monster };
            random.Shuffle(opening); random.Shuffle(later);
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < FieldMap.Width; col++)
                {
                    var kind = row == 0 ? opening[col] : later[(row - 1) * FieldMap.Width + col];
                    grid[row, col] = new StageNode(field, row, col, kind);
                    nodes.Add(grid[row, col]);
                }

            for (int row = 0; row < 2; row++)
            {
                // Straight paths guarantee reachability. One or two diagonals add splits/merges
                // without crossing each other or allowing skipped rows.
                for (int col = 0; col < FieldMap.Width; col++) grid[row, col].Connect(grid[row + 1, col]);
                int mandatoryGap = random.Next(FieldMap.Width - 1);
                for (int gap = 0; gap < FieldMap.Width - 1; gap++)
                {
                    if (gap != mandatoryGap && random.Next(2) == 0) continue;
                    int direction = random.Next(2);
                    grid[row, gap + direction].Connect(grid[row + 1, gap + 1 - direction]);
                }
            }
            var boss = new StageNode(field, 3, 1, StageKind.Boss);
            nodes.Add(boss);
            for (int col = 0; col < FieldMap.Width; col++) grid[2, col].Connect(boss);
            return new FieldMap(field, nodes);
        }
    }
}
