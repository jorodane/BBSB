using System;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class MonsterAuthoringCoreTests
    {
        private static MonsterDefinition Monster(string id = "test-authored") => new MonsterDefinition(id, "테스트", "",
            new RhythmPattern("custom-hold", 4, new[] { new PatternStep(GestureKind.Hold, 0, 8) }),
            new[] { new CallSignal(0, "통!") }, 12, 4, 1);
        [Test]
        public void CustomCatalogIsDeterministicRejectsCollisionsAndKeepsBuiltIns()
        {
            var previous = MonsterCatalog.All;
            try
            {
                MonsterCatalog.SetCustom(new[] { Monster("z-custom"), Monster("a-custom") });
                Check.Equal(MonsterCatalog.BuiltIn.Count + 2, MonsterCatalog.All.Count);
                Check.Equal("a-custom", MonsterCatalog.All[MonsterCatalog.BuiltIn.Count].Id);
                var before = MonsterCatalog.All;
                bool rejected = false;
                try { MonsterCatalog.SetCustom(new[] { Monster(MonsterCatalog.BuiltIn[0].Id) }); }
                catch (ArgumentException) { rejected = true; }
                Check.True(rejected); Check.True(ReferenceEquals(before, MonsterCatalog.All));
                rejected = false;
                try { MonsterCatalog.SetCustom(new[] { Monster(), Monster() }); }
                catch (ArgumentException) { rejected = true; }
                Check.True(rejected); Check.True(ReferenceEquals(before, MonsterCatalog.All));
            }
            finally { MonsterCatalog.SetRoster(previous); }
        }
        [Test]
        public void AuthoredRosterReplacesDefaultsPreservesOrderAndNeverReinsertsRemovedMonsters()
        {
            var previous = MonsterCatalog.All;
            try
            {
                var edited = Monster(MonsterCatalog.BuiltIn[0].Id);
                var second = MonsterCatalog.BuiltIn[1];
                MonsterCatalog.SetRoster(new[] { Monster("z-custom"), second, Monster("a-custom"), edited });
                Check.Equal(4, MonsterCatalog.All.Count);
                Check.True(ReferenceEquals(edited, MonsterCatalog.All[0]));
                Check.True(ReferenceEquals(second, MonsterCatalog.All[1]));
                Check.Equal("a-custom", MonsterCatalog.All[2].Id);
                var before = MonsterCatalog.All;
                bool rejected = false;
                try { MonsterCatalog.SetRoster(new[] { edited, edited }); }
                catch (ArgumentException) { rejected = true; }
                Check.True(rejected); Check.True(ReferenceEquals(before, MonsterCatalog.All));
                rejected = false;
                try { MonsterCatalog.SetRoster(new MonsterDefinition[] { null }); }
                catch (ArgumentException) { rejected = true; }
                Check.True(rejected); Check.True(ReferenceEquals(before, MonsterCatalog.All));
                MonsterCatalog.SetRoster(new[] { second });
                Check.Equal(1, MonsterCatalog.All.Count); Check.True(ReferenceEquals(second, MonsterCatalog.All[0]));
                MonsterCatalog.SetRoster(Array.Empty<MonsterDefinition>());
                Check.Equal(0, MonsterCatalog.All.Count);
            }
            finally { MonsterCatalog.SetRoster(previous); }
        }

        [Test]
        public void LeadingResponseSpacePreservesCallInputTimesPhraseAndDamageBudget()
        {
            var pattern = MonsterPatternDefinition.FromPhrase("delayed-flick", "Delayed", "", 4,
                new[] { new PatternStep(GestureKind.Flick, 2) },
                new[] { new CallSignal(0, "one"), new CallSignal(2, "two"), new CallSignal(4, "three") }, 8, 0, .6);
            Check.Equal(6, pattern.Pattern.CueLeadTicks); Check.Equal(0, pattern.Pattern.Steps[0].OffsetTick);
            Check.Equal(6, pattern.ResponseTicks); Check.Equal(12, pattern.Pattern.CueLeadTicks + pattern.ResponseTicks);
            Check.Equal(1.5m, pattern.JudgmentWeight);
            var monster = new MonsterDefinition("flick", "Flick", "", GestureKind.Flick, new[] { pattern });
            var preview = new MonsterPreview(monster, pattern);
            var attack = preview.Round.Notes[0].Attack;
            Check.Equal(6, preview.Round.Notes[0].StartTick - attack.CallStartTick);
            Check.Equal(4, attack.Call[2].Tick - attack.CallStartTick);
        }

        [Test]
        public void ThreeBeatWaitIsValidAndAmbiguousCallsCanBeReviewedWithoutDiscardingTheMonster()
        {
            var wait = MonsterPatternDefinition.FromPhrase("wait", "Wait", "", 12,
                new[] { new PatternStep(GestureKind.Tap, 0) }, new[] { new CallSignal(0, "wait") }, 4, 0, .1,
                silentWaitTicks: 12);
            Check.Equal(12, wait.SilentWaitTicks);
            var other = MonsterPatternDefinition.FromPhrase("other", "Other", "", 4,
                new[] { new PatternStep(GestureKind.Tap, 0) }, new[] { new CallSignal(0, "same sound") }, 4, 0, .8);
            Check.True(CallReadability.Warning(wait, other) != null);
            var authored = new MonsterDefinition("authored", "Authored", "", GestureKind.Tap, new[] { wait, other },
                validateCallReadability: false);
            Check.Equal(2, authored.Patterns.Count);
        }

        [Test]
        public void BeatShiftAuthoredProbabilitiesChangeTransitionsWithoutBreakingThePulse()
        {
            var source = MonsterCatalog.BuiltIn.Single(x => x.Id == "seesaw-goblin");
            MonsterDefinition WithChance(double chance)
            {
                var patterns = source.Patterns.Take(2).Select(p => new MonsterPatternDefinition(p.Name, p.Description, p.Pattern, p.Call,
                    p.ResponseTicks, p.RestTicks, p.Pattern.Steps.Count == 2 ? chance : 1, p.CueAlignmentTicks)).ToArray();
                return new MonsterDefinition(source.Id, source.Name, "", source.MainGesture, patterns,
                    patternPlanner: new BeatShiftPlanner(3, true));
            }
            var stage = MusicStage.Generate(MusicCatalog.All.Single(x => x.Id == "rapid-drive"));
            var dense = WithChance(1); var sparse = WithChance(.2);
            int denseTransitions = 0, sparseTransitions = 0; bool different = false;
            string Fingerprint(MonsterProposal p) => string.Join("|", p.Placements.Select(x => x.Pattern.Id + "@" + x.StartTick));
            for (int seed = 0; seed < 12; seed++)
            {
                var a = BattlePlanner.Propose(stage, "same", dense, seed);
                var b = BattlePlanner.Propose(stage, "same", sparse, seed);
                Check.Equal(Fingerprint(b), Fingerprint(BattlePlanner.Propose(stage, "same", sparse, seed)));
                different |= Fingerprint(a) != Fingerprint(b);
                denseTransitions += a.Placements.Count(x => x.Pattern.Steps.Count == 2);
                sparseTransitions += b.Placements.Count(x => x.Pattern.Steps.Count == 2);
                foreach (var chain in b.Chains)
                {
                    var ticks = chain.Placements.SelectMany(x => x.Pattern.Steps.Select(step => x.StartTick + step.OffsetTick)).OrderBy(x => x).ToArray();
                    for (int i = 1; i < ticks.Length; i++) Check.True(ticks[i] - ticks[i - 1] > 0 && ticks[i] - ticks[i - 1] <= 4);
                }
            }
            Check.True(different); Check.True(sparseTransitions < denseTransitions);
        }


        [Test]
        public void AttackOverridesAreIsolatedPerMonsterDefinitionAndReachTheirOwnJudgment()
        {
            var first = Monster(); var second = Monster();
            var a = new MonsterPreview(first, first.Patterns[0]); var b = new MonsterPreview(second, second.Patterns[0]);
            var visual = new MonsterAttackDefinition(first.Id, first.Patterns[0].Id, 0,
                MonsterAttackMotion.WaitRush, MonsterAttackShape.Feather, MonsterAttackReaction.Scatter,
                rush: 1, resourceFolder: "BBSB/Shared/Feathers");
            MonsterAttackCatalog.Register(first, new[] { visual });
            Check.True(ReferenceEquals(visual, MonsterAttackCatalog.For(a.Round.Notes[0])));
            Check.Equal(MonsterAttackMotion.Linear, MonsterAttackCatalog.For(b.Round.Notes[0]).Motion);
            var note = a.Round.Notes[0]; double beat = a.Round.BeatSeconds;
            Check.Equal("BBSB/Shared/Feathers", visual.ResourceFolder);
            var waiting = MonsterAttackTimeline.Evaluate(note, visual, note.StartSeconds - beat * .5, beat, a.Round.HalfMissWindow);
            Check.Equal(MonsterAttackPhase.Wait, waiting.Phase); Check.Equal(0.0, waiting.Progress);
            var flying = MonsterAttackTimeline.Evaluate(note, visual, note.StartSeconds - beat * .125, beat, a.Round.HalfMissWindow);
            Check.True(Math.Abs(flying.Progress - .5) < .000001);
            var contact = MonsterAttackTimeline.Evaluate(note, visual, note.StartSeconds, beat, a.Round.HalfMissWindow);
            Check.Equal(MonsterAttackPhase.Contact, contact.Phase); Check.Equal(1.0, contact.Progress);
        }
        [Test]
        public void NewPatternHasAnAttackVisualWithoutHardCodedImageSlots()
        {
            var monster = Monster(); var preview = new MonsterPreview(monster, monster.Patterns[0]);
            var visual = MonsterAttackCatalog.For(preview.Round.Notes[0]);
            Check.Equal(monster.Id, visual.MonsterId); Check.Equal("custom-hold", visual.PatternId);
            Check.Equal(MonsterAttackMotion.Linear, visual.Motion);
        }
        [Test]
        public void AnimatorTimelineUsesCallResponseSustainRecoveryAndRealJudgments()
        {
            var monster = Monster(); var preview = new MonsterPreview(monster, monster.Patterns[0]);
            var round = preview.Round; var plan = round.Plan.Monsters[0]; var note = round.Notes[0];
            Check.Equal(MonsterAnimationSituation.Idle, MonsterAnimationTimeline.Evaluate(round, plan, 0).Situation);
            var call = MonsterAnimationTimeline.Evaluate(round, plan, preview.CallSeconds + .1);
            Check.Equal(MonsterAnimationSituation.Call, call.Situation); Check.Equal(0, call.CallOffsetTick);
            Check.Equal(MonsterAnimationSituation.Attack, MonsterAnimationTimeline.Evaluate(round, plan, note.StartSeconds + .1).Situation);
            Check.Equal(MonsterAnimationSituation.Attack, MonsterAnimationTimeline.Evaluate(round, plan, note.EndSeconds - .1).Situation);
            round.Press(note.StartSeconds + .1, 0, 0); round.Advance(note.EndSeconds);
            Check.Equal(MonsterAnimationSituation.HalfMiss, MonsterAnimationTimeline.Evaluate(round, plan, round.ElapsedSeconds).Situation);
            Check.Equal(MonsterAnimationSituation.Idle, MonsterAnimationTimeline.Evaluate(round, plan, note.EndSeconds + .3).Situation);
            preview.Restart(); round = preview.Round; plan = round.Plan.Monsters[0];
            Check.Equal(MonsterAnimationSituation.Recover, MonsterAnimationTimeline.Evaluate(round, plan, note.EndSeconds + .05).Situation);
            round.Advance(note.StartSeconds + round.HalfMissWindow + .01);
            Check.Equal(MonsterAnimationSituation.Miss, MonsterAnimationTimeline.Evaluate(round, plan, round.ElapsedSeconds).Situation);
        }
        [Test]
        public void AnimatorTimelineResetsCallAgeForEachSignalAndRestart()
        {
            var monster = new MonsterDefinition("two-call", "테스트", "",
                new RhythmPattern("two-call-tap", 8, new[] { new PatternStep(GestureKind.Tap, 0) }),
                new[] { new CallSignal(0, "통"), new CallSignal(4, "탕") }, 4, 4, 1);
            var preview = new MonsterPreview(monster, monster.Patterns[0]);
            var round = preview.Round; var plan = round.Plan.Monsters[0];
            var first = MonsterAnimationTimeline.Evaluate(round, plan, preview.CallSeconds + .1);
            var second = MonsterAnimationTimeline.Evaluate(round, plan, preview.CallSeconds + round.BeatSeconds + .1);
            Check.Equal(0, first.CallOffsetTick); Check.Equal(4, second.CallOffsetTick);
            Check.True(Math.Abs(first.AgeBeats - second.AgeBeats) < 1e-8);
            round.Press(round.Notes[0].StartSeconds, 0, 0);
            Check.Equal(MonsterAnimationSituation.Perfect, MonsterAnimationTimeline.Evaluate(round, plan, round.ElapsedSeconds).Situation);
            preview.Restart(); Check.Equal(MonsterAnimationSituation.Idle,
                MonsterAnimationTimeline.Evaluate(preview.Round, plan, 0).Situation);
        }
    }
}
