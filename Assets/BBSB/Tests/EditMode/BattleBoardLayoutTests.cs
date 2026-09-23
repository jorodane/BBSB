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
        [Test] public void NotesRiseAtConstantScreenSpeedAndReachTheUpperJudgmentLine()
        {
            foreach (int slot in BattleInputLayout.DisplayOrder)
            foreach (double due in new[] { 3.0, 3.5, 3.137 })
            {
                double previousX = 0, previousY = 0, stepX = 0, stepY = 0;
                for (int frame = 0; frame <= 60; frame++)
                {
                    double now = due - 3 + frame * .05;
                    double depth = SteppedNoteTrack.Depth(SteppedNoteTrack.Distance(due, now));
                    double x = BattleBoardLayout.X(slot, depth, InputExtensions.All), y = BattleBoardLayout.Y(depth);
                    if (frame == 0) Check.Equal(BattleBoardLayout.NearY, y);
                    else
                    {
                        Check.True(y > previousY);
                        if (frame == 1) { stepX = x - previousX; stepY = y - previousY; }
                        Check.True(Math.Abs(x - previousX - stepX) < .000000001);
                        Check.True(Math.Abs(y - previousY - stepY) < .000000001);
                    }
                    previousX = x; previousY = y;
                }
                Check.Equal(BattleBoardLayout.FarY, previousY);
                Check.Equal(BattleBoardLayout.X(slot, 1, InputExtensions.All), previousX);
            }
        }
        [Test] public void HoldHeadStaysAtTheTopWhileItsLaterTailApproachesFromBelow()
        {
            double head = SteppedNoteTrack.Depth(SteppedNoteTrack.Distance(2, 2.5));
            double tail = SteppedNoteTrack.Depth(SteppedNoteTrack.Distance(4, 2.5));
            Check.Equal(1.0, head);
            Check.True(BattleBoardLayout.Y(tail) < BattleBoardLayout.Y(head));
            Check.Equal(0.0, SteppedNoteTrack.Depth(SteppedNoteTrack.Distance(8, 2.5)));
            Check.False(SteppedNoteTrack.InHorizon(8 - 2.5));
        }
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
