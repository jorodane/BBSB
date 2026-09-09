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

        private static readonly List<Link> CandidateLinks = BuildCandidateLinks();
        private static readonly List<int> Prunings = BuildPrunings();

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
                foreach (var link in CandidateLinks) grid[row, link.From].Connect(grid[row + 1, link.To]);

            // Place nodes first, then randomly cut candidate links. Straight links have no special
            // protection. Choose a complete valid cut plan so local deletions cannot trap a route.
            var cuts = ChoosePruning(grid, random);
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < CandidateLinks.Count; i++)
                    if ((cuts[row] & (1 << i)) == 0)
                    {
                        var link = CandidateLinks[i];
                        grid[row, link.From].Disconnect(grid[row + 1, link.To]);
                    }

            var boss = new StageNode(field, 3, 1, StageKind.Boss);
            nodes.Add(boss);
            for (int col = 0; col < FieldMap.Width; col++) grid[2, col].Connect(boss);
            return new FieldMap(field, nodes);
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
                // Retain 3–4 of the 7 candidates per layer. Every node remains on a full route,
                // but at least one straight link is removed at each non-boss layer transition.
                if (incoming == allColumns && outgoing == allColumns &&
                    count <= FieldMap.Width + 1 && straight < FieldMap.Width) result.Add(mask);
            }
            return result;
        }

        private static int[] ChoosePruning(StageNode[,] grid, SeededRandom random)
        {
            var choices = new List<int[]>();
            foreach (int first in Prunings)
                foreach (int second in Prunings)
                {
                    var plan = new[] { first, second };
                    // Keep at least one split/merge inside the field, rather than three single paths.
                    if (!HasBranch(first) && !HasBranch(second)) continue;
                    if (RespectsServiceLimits(grid, plan)) choices.Add(plan);
                }
            if (choices.Count == 0)
                throw new InvalidOperationException("Stage distribution has no connected map satisfying the service limits.");
            return choices[random.Next(choices.Count)];
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

        private static bool RespectsServiceLimits(StageNode[,] grid, int[] plan)
        {
            var histories = new int[3, FieldMap.Width];
            for (int col = 0; col < FieldMap.Width; col++) histories[0, col] = ServiceFlag(grid[0, col].Kind);
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < CandidateLinks.Count; i++)
                {
                    if ((plan[row] & (1 << i)) == 0) continue;
                    var link = CandidateLinks[i];
                    int previous = histories[row, link.From];
                    int next = ServiceFlag(grid[row + 1, link.To].Kind);
                    if ((previous & next) != 0) return false;
                    // At a merge, retain services from ALL incoming routes. If any of them already
                    // visited a shop/rest, none of its continuations may visit that type again.
                    histories[row + 1, link.To] |= previous | next;
                }
            return true;
        }

        private static int ServiceFlag(StageKind kind)
        { return kind == StageKind.Shop ? 1 : kind == StageKind.Rest ? 2 : 0; }
    }
}
