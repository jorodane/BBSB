using System;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class GestureIconTests
    {
        [Test]
        public void ActionColorsAndBothAssetSizesUseTheAgreedIdentity()
        {
            var kinds = new[] { GestureKind.Tap, GestureKind.Hold, GestureKind.Flick, GestureKind.Dive, GestureKind.Shake };
            var colors = new[] { "DE424B", "287DD1", "27985A", "E28222", "9256C9" };
            var names = new[] { "TAP", "HOLD", "FLICK", "DIVE", "SHAKE" };
            for (int i = 0; i < kinds.Length; i++)
            {
                Check.Equal(colors[i], GestureIconCatalog.ColorHex(kinds[i]));
                Check.Equal(names[i], GestureIconCatalog.Name(kinds[i]));
                Check.Equal("BBSB/GestureIcons/Small/" + names[i].ToLowerInvariant(), GestureIconCatalog.ResourcePath(kinds[i], false));
                Check.Equal("BBSB/GestureIcons/Large/" + names[i].ToLowerInvariant(), GestureIconCatalog.ResourcePath(kinds[i], true));
            }
        }

        [Test]
        public void PromptStartsWithTheCallAndFollowsActualUnresolvedStepsWithoutChangingTheRound()
        {
            var preview = Preview(new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Flick, 4));
            var round = preview.Round; var output = new GestureKind[5]; string id = preview.Attack.MonsterId;
            Check.Equal(0, ResponsePromptTimeline.Fill(round, id, preview.CallSeconds - .001, output));
            Check.Equal(1, ResponsePromptTimeline.Fill(round, id, preview.CallSeconds, output)); Check.Equal(GestureKind.Tap, output[0]);
            Check.Equal(0, ResponsePromptTimeline.Fill(round, "other-monster", preview.CallSeconds, output));
            for (int i = 0; i < 100; i++) ResponsePromptTimeline.Fill(round, id, preview.CallSeconds, output);
            Check.Equal(0.0, round.ElapsedSeconds); Check.Equal(0, round.Results.Count); Check.Equal(0, round.Calls.Count);
            double at = round.Notes[0].StartSeconds; round.Press(at, 0, 0);
            Check.Equal(1, ResponsePromptTimeline.Fill(round, id, at, output)); Check.Equal(GestureKind.Flick, output[0]);
            round.Advance(round.Notes[1].EndSeconds + .3);
            Check.Equal(0, ResponsePromptTimeline.Fill(round, id, round.ElapsedSeconds, output));
            preview.Restart();
            Check.Equal(0, ResponsePromptTimeline.Fill(preview.Round, id, 0, output));
            Check.Equal(1, ResponsePromptTimeline.Fill(preview.Round, id, preview.CallSeconds, output)); Check.Equal(GestureKind.Tap, output[0]);
        }

        [Test]
        public void HoldAndDivePromptsRemainVisibleUntilTheirRealEndAndClearOnResolution()
        {
            foreach (var kind in new[] { GestureKind.Hold, GestureKind.Dive })
            {
                var preview = Preview(new PatternStep(kind, 0, 8)); var round = preview.Round; var note = round.Notes.Single();
                var output = new GestureKind[5]; string id = preview.Attack.MonsterId;
                round.Press(note.StartSeconds,0,0); double mid = (note.StartSeconds+note.EndSeconds)/2; round.Advance(mid);
                Check.Equal(1,ResponsePromptTimeline.Fill(round,id,mid,output)); Check.Equal(kind,output[0]);
                round.Release(note.EndSeconds,0,0);
                Check.Equal(0,ResponsePromptTimeline.Fill(round,id,round.ElapsedSeconds,output));
            }
        }

        [Test]
        public void SharedSimultaneousActionsHaveSeparateShapesAndAbortedRoundsHidePrompts()
        {
            var preview = Preview(new PatternStep(GestureKind.Hold,0,8),new PatternStep(GestureKind.Shake,0));
            var output = new GestureKind[5]; var round = preview.Round; string id=preview.Attack.MonsterId;
            Check.Equal(2,ResponsePromptTimeline.Fill(round,id,preview.CallSeconds,output));
            Check.True(output.Take(2).Contains(GestureKind.Hold)); Check.True(output.Take(2).Contains(GestureKind.Shake));
            Check.Equal(0,ResponsePromptTimeline.Fill(round,id,double.NaN,output));
            round.Stop(); Check.Equal(0,ResponsePromptTimeline.Fill(round,id,preview.CallSeconds,output));
        }

        private static MonsterPreview Preview(params PatternStep[] steps)
        {
            var pattern = new RhythmPattern("icon-test",4,steps);
            var monster = new MonsterDefinition("icon-target","Icon target","",pattern,
                new[] { new CallSignal(0,"CALL") },Math.Max(12,pattern.EndOffsetTick),4,1);
            return new MonsterPreview(monster,monster.Patterns[0]);
        }
    }
}
