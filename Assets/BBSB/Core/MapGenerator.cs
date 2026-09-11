using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public static class MapGenerator
    {
        private struct Link
        {
            public int From;
            public int To;
            public Link(int from, int to) { From = from; To = to; }
        }

        private const int NormalRows = FieldMap.StageCount - 1;
        private const int LayoutAttempts = 32;
        private static readonly List<Link> CandidateLinks = BuildCandidateLinks();
        private static readonly List<int> Prunings = BuildPrunings();

        public static FieldMap Generate(int field, SeededRandom random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            for (int attempt = 0; attempt < LayoutAttempts; attempt++)
            {
                var later = LaterStages(random, attempt == LayoutAttempts - 1);
                var nodes = new List<StageNode>();
                var grid = new StageNode[NormalRows, FieldMap.Width];
                for (int row = 0; row < NormalRows; row++)
                {
                    // All four entrances are visible battles. Each middle row has one unknown area.
                    int mysteryColumn = row == 0 ? -1 : random.Next(FieldMap.Width);
                    for (int col = 0; col < FieldMap.Width; col++)
                    {
                        var kind = row == 0 ? StageKind.Monster : later[(row - 1) * FieldMap.Width + col];
                        grid[row, col] = new StageNode(field, row, col, kind, col == mysteryColumn);
                        nodes.Add(grid[row, col]);
                    }
                }

                // Place all outcomes before pruning, including hidden shops/rests. Backtracking
                // considers the whole route so a local cut cannot trap a later branch or merge.
                var cuts = ChoosePruning(grid, random);
                if (cuts == null) continue;
                for (int row = 0; row < NormalRows - 1; row++)
                {
                    foreach (var link in CandidateLinks) grid[row, link.From].Connect(grid[row + 1, link.To]);
                    for (int i = 0; i < CandidateLinks.Count; i++)
                        if ((cuts[row] & (1 << i)) == 0)
                        {
                            var link = CandidateLinks[i];
                            grid[row, link.From].Disconnect(grid[row + 1, link.To]);
                        }
                }

                var boss = new StageNode(field, NormalRows, FieldMap.Width / 2, StageKind.Boss);
                nodes.Add(boss);
                for (int col = 0; col < FieldMap.Width; col++) grid[NormalRows - 1, col].Connect(boss);
                return new FieldMap(field, nodes);
            }
            throw new InvalidOperationException("No connected map satisfies the service limits.");
        }

        private static List<StageKind> LaterStages(SeededRandom random, bool separateServices)
        {
            var stages = new List<StageKind> { StageKind.Shop, StageKind.Shop, StageKind.Rest, StageKind.Rest,
                StageKind.Elite, StageKind.Elite, StageKind.Upgrade, StageKind.Upgrade };
            while (stages.Count < (NormalRows - 1) * FieldMap.Width) stages.Add(StageKind.Monster);
            random.Shuffle(stages);
            if (!separateServices) return stages;

            // Bounded fallback: putting each service type in a single row guarantees a feasible
            // topology without dropping nodes or weakening the per-route limit. Rows still vary.
            stages.RemoveAll(x => x == StageKind.Shop || x == StageKind.Rest);
            var rows = new List<List<StageKind>>();
            int next = 0;
            for (int row = 0; row < NormalRows - 1; row++)
            {
                var layer = new List<StageKind>();
                if (row < 2)
                {
                    var service = row == 0 ? StageKind.Shop : StageKind.Rest;
                    layer.Add(service); layer.Add(service);
                }
                while (layer.Count < FieldMap.Width) layer.Add(stages[next++]);
                random.Shuffle(layer); rows.Add(layer);
            }
            random.Shuffle(rows); stages.Clear();
            foreach (var row in rows) stages.AddRange(row);
            return stages;
        }

        private static List<Link> BuildCandidateLinks()
        {
            var links = new List<Link>();
            for (int from = 0; from < FieldMap.Width; from++)
                for (int to = 0; to < FieldMap.Width; to++)
                    if (Math.Abs(from - to) <= 1) links.Add(new Link(from, to));
            return links;
        }

        private static List<int> BuildPrunings()
        {
            var result = new List<int>();
            int allColumns = (1 << FieldMap.Width) - 1;
            for (int mask = 0; mask < (1 << CandidateLinks.Count); mask++)
            {
                int incoming = 0, outgoing = 0, count = 0, straight = 0;
                for (int i = 0; i < CandidateLinks.Count; i++)
                {
                    if ((mask & (1 << i)) == 0) continue;
                    var link = CandidateLinks[i];
                    outgoing |= 1 << link.From; incoming |= 1 << link.To; count++;
                    if (link.From == link.To) straight++;
                }
                // Retain 4–5 of the 10 candidates per layer. Every node stays on a full route;
                // at least one straight link is removed at every non-boss transition.
                if (incoming == allColumns && outgoing == allColumns &&
                    count <= FieldMap.Width + 1 && straight < FieldMap.Width) result.Add(mask);
            }
            return result;
        }

        private static int[] ChoosePruning(StageNode[,] grid, SeededRandom random)
        {
            var choices = new List<int>[NormalRows - 1];
            for (int row = 0; row < choices.Length; row++)
            {
                choices[row] = new List<int>(Prunings);
                random.Shuffle(choices[row]);
            }
            var plan = new int[choices.Length];
            return TryPrune(grid, choices, plan, new HashSet<int>(), 0, 0, false) ? plan : null;
        }

        private static bool TryPrune(StageNode[,] grid, List<int>[] choices, int[] plan,
            HashSet<int> failed, int row, int histories, bool hasBranch)
        {
            if (row == plan.Length) return hasBranch;
            // Two bits per column summarize all incoming routes: shop and rest visited.
            int state = (row << (FieldMap.Width * 2 + 1)) | (histories << 1) | (hasBranch ? 1 : 0);
            if (failed.Contains(state)) return false;
            foreach (int mask in choices[row])
            {
                if (!TryAdvance(grid, row, mask, histories, out int next)) continue;
                plan[row] = mask;
                if (TryPrune(grid, choices, plan, failed, row + 1, next, hasBranch || HasBranch(mask))) return true;
            }
            failed.Add(state);
            return false;
        }

        private static bool HasBranch(int mask)
        {
            int sources = 0;
            for (int i = 0; i < CandidateLinks.Count; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                int source = 1 << CandidateLinks[i].From;
                if ((sources & source) != 0) return true;
                sources |= source;
            }
            return false;
        }

        private static bool TryAdvance(StageNode[,] grid, int row, int mask, int histories, out int nextHistories)
        {
            nextHistories = 0;
            for (int i = 0; i < CandidateLinks.Count; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                var link = CandidateLinks[i];
                int previous = (histories >> (link.From * 2)) & 3;
                int next = ServiceFlag(grid[row + 1, link.To].Kind);
                if ((previous & next) != 0) return false;
                // Merges carry services from ALL incoming routes, including unrevealed outcomes.
                nextHistories |= (previous | next) << (link.To * 2);
            }
            return true;
        }

        private static int ServiceFlag(StageKind kind)
        { return kind == StageKind.Shop ? 1 : kind == StageKind.Rest ? 2 : 0; }
    }
}
