using System;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class BattleBoardLayoutTests
    {
        [Test] public void EveryProjectedLaneHitsItsOwnKeyAcrossTheWholePerspectiveBoard()
        {
            foreach (InputExtensions extensions in new[] { InputExtensions.None, InputExtensions.Left, InputExtensions.Right, InputExtensions.All })
            foreach (int slot in BattleInputLayout.DisplayOrder)
            {
                if (!BattleInputLayout.Available(slot, extensions)) continue;
                for (int step = 0; step <= 20; step++)
                {
                    double d = step / 20.0, x = BattleBoardLayout.X(slot, d, extensions), y = BattleBoardLayout.Y(d);
                    double radius = BattleBoardLayout.CellWidth(d, extensions) * .499;
                    foreach (double point in new[] { x - radius, x, x + radius })
                        Check.Equal(slot, BattleBoardLayout.HitSlot(point, y, extensions));
                }
                Check.Equal(slot, BattleBoardLayout.HitSlot(BattleBoardLayout.X(slot, 1, extensions), .44, extensions));
            }
        }
        [Test] public void NeighboursDoNotOverlapAndSevenInputsKeepTheirPhysicalOrder()
        {
            foreach (double distance in new[] { 0.0, .5, 1 })
            {
                double previous = -1, width = BattleBoardLayout.CellWidth(distance, InputExtensions.All);
                foreach (int slot in BattleInputLayout.DisplayOrder)
                {
                    double x = BattleBoardLayout.X(slot, distance, InputExtensions.All);
                    Check.True(x - width * .5 > 0 && x + width * .5 < 1);
                    if (previous >= 0) Check.True(Math.Abs(x - previous - width) < .000001);
                    previous = x;
                }
            }
            Check.True(BattleBoardLayout.Width(1) < BattleBoardLayout.Width(0));
            Check.Equal(-1, BattleBoardLayout.HitSlot(.1, .44, InputExtensions.All));
            Check.Equal(-1, BattleBoardLayout.HitSlot(.5, .6, InputExtensions.All));
            Check.Equal(-1, BattleBoardLayout.HitSlot(.5, .01, InputExtensions.All));
        }
        [Test] public void BeatColourFollowsTheNoteTimeIncludingFutureChaosTransitions()
        {
            foreach (double beat in new[] { 0.0, 1, 4, 12 }) Check.False(BattleBoardLayout.IsOffbeat(beat));
            foreach (double beat in new[] { .5, 1.5, 4.5, 12.5 }) Check.True(BattleBoardLayout.IsOffbeat(beat));
        }
    }
}
