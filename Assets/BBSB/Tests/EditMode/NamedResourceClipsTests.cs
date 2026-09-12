using System;
using System.Collections.Generic;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif
using BBSB.Runtime.UI;

namespace BBSB.Tests
{
    public sealed class NamedResourceClipsTests
    {
        [Test]
        public void SingleImagesUseTheirFilePathWithoutLoadingOtherPatterns()
        {
            var paths = new List<string>();
            var clips = new NamedResourceClips<string>(path =>
            { paths.Add(path); return path == "monster/idle" ? new[] { "edited-sprite-name_0" } : null; }, value => value);
            Check.Equal("edited-sprite-name_0", clips.Get("monster", "idle"));
            Check.True(!paths.Contains("monster"), "An idle lookup must not load the monster's entire attack tree.");
            int reads = paths.Count;
            Check.Equal("edited-sprite-name_0", clips.Get("monster", "idle", 123));
            Check.Equal(reads, paths.Count);
        }

        [Test]
        public void SeparateWalkingFramesOverrideTheStaticFallbackAndLoopInNumericOrder()
        {
            var files = new Dictionary<string, string[]> { ["doll/travel"] = new[] { "fallback" } };
            for (int i = 0; i < 12; i++) files.Add("doll/travel-" + i, new[] { "pose_" + i });
            var paths = new List<string>();
            var clips = new NamedResourceClips<string>(path =>
            { paths.Add(path); return files.TryGetValue(path, out var file) ? file : null; }, value => value);
            Check.Equal("pose_10", clips.Get("doll", "travel", 10.5));
            Check.Equal("pose_0", clips.Get("doll", "travel", 12));
            Check.Equal("pose_0", clips.Get("doll", "travel", -1));
            Check.Equal("pose_0", clips.Get("doll", "travel", double.NaN));
            Check.False(paths.Contains("doll")); Check.False(paths.Contains("doll/travel"));
            Check.True(paths.Count == 13, "Each numbered path and the end of the clip are read once.");
        }

        [Test]
        public void MissingFrameNumbersDoNotSpliceUnrelatedPosesIntoTheAnimation()
        {
            var clips = new NamedResourceClips<string>(path => path == "body/call-0-0" ? new[] { "ready" } :
                path == "body/call-0-2" ? new[] { "unreachable" } : null, value => value);
            Check.Equal("ready", clips.Get("body", "call-0", 1));
            var calls = new NamedResourceClips<string>(path => path == "body/call-0" ? new[] { "first-call" } :
                path == "body/call-1" ? new[] { "second-call" } : null, value => value);
            Check.Equal("first-call", calls.Get("body", "call-0", 1));
            Check.Equal("second-call", calls.Get("body", "call-1"));
        }

        [Test]
        public void AuthoredAtlasNamesRemainSupportedWithoutAnimatingAutomaticSliceNames()
        {
            int folders = 0;
            var clips = new NamedResourceClips<string>(path =>
            {
                if (path == "step/travel") return new[] { "travel_0", "travel_1" };
                if (path != "step") return null;
                folders++; return new[] { "travel", "travel-2", "travel-0", "travel-1", "perfect", "spawn_0", "spawn_1" };
            }, value => value);
            Check.Equal("travel-2", clips.Get("step", "travel", 2));
            Check.Equal("travel-0", clips.Get("step", "travel", 3));
            Check.Equal("perfect", clips.Get("step", "perfect"));
            Check.True(clips.Get("step", "spawn") == null);
            Check.True(clips.Get("step", "missing") == null);
            Check.Equal(1, folders);
        }

        [Test]
        public void IdenticalPhaseNamesStayWithinTheirMonsterAndStep()
        {
            var clips = new NamedResourceClips<string>(path => path.EndsWith("/contact", StringComparison.Ordinal) ?
                new[] { path } : null, value => "contact");
            foreach (var folder in new[] { "fox/pair/step-0", "fox/pair/step-1", "jelly/tap/step-0" })
                Check.Equal(folder + "/contact", clips.Get(folder, "contact"));
        }
    }
}
