using System;
using System.Linq;
using BBSB.Core;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class StageCatalogTests
    {
        [Test]
        public void EightMapsEachHaveTenUniqueEightySecondSongsWithThreeHooks()
        {
            Check.Equal(8, StageCatalog.Maps.Count); Check.Equal(80, StageCatalog.All.Count);
            Check.Equal(80, StageCatalog.All.Select(x => x.Id).Distinct().Count());
            foreach (var map in StageCatalog.Maps) Check.Equal(10, StageCatalog.All.Count(x => x.MapId == map.Id));
            foreach (var song in StageCatalog.All)
            {
                Check.True(Math.Abs(song.Music.DurationSeconds - 80) < .000001, song.Id);
                Check.Equal(3, song.Music.Sections.Count(x => x.IsHook));
                Check.True(song.Music.Sections.Where(x => x.IsHook).All(x => x.AllowsResponse));
                Check.True(song.BackgroundPath.Contains(song.Id) && song.AudioPath.Contains(song.Id));
                if (song.Meter == "6/8") Check.Equal(2, song.Music.BeatsPerBar);
                if (song.Meter == "3/4") Check.Equal(3, song.Music.BeatsPerBar);
            }
        }

        [Test]
        public void TheChosenMapOwnsEveryBattleAndIsStableAcrossEntryAndRestart()
        {
            foreach (var map in StageCatalog.Maps) foreach (int seed in new[] { -7, 42, int.MaxValue })
            {
                var run = new RunSession(seed, mapId: map.Id);
                Check.Equal(map.Id, run.Map.Theme.Id);
                var assignment = string.Join("|", run.Map.Nodes.Select(x => x.SongId));
                foreach (var node in run.Map.Nodes)
                    if (node.IsBattle) Check.Equal(map.Id, StageCatalog.Find(node.SongId).MapId);
                    else Check.True(node.SongId == null);
                var first = run.Map.Nodes.First(); string id = first.SongId;
                Check.True(run.Enter(first.Id)); Check.Equal(id, run.BattleMusic.Music.Id);
                run.Restart(seed, map.Id);
                Check.Equal(assignment, string.Join("|", run.Map.Nodes.Select(x => x.SongId)));
                for (int field = 1; field <= 8; field++)
                    Check.Equal(StageCatalog.ThemeFor(seed, field, map.Id).Id,
                        StageCatalog.ForEncounter(seed, field, 2, 1, map.Id).MapId);
                Check.Equal(8, Enumerable.Range(1, 8).Select(x => StageCatalog.ThemeFor(seed, x, map.Id).Id).Distinct().Count());
            }
        }

        [Test]
        public void AllEightySongsGuaranteeHooksAfterArbitrationWithoutConflictingInputs()
        {
            int seed = 1701;
            foreach (var song in StageCatalog.All)
            {
                var stage = MusicStage.Generate(song.Music);
                foreach (int field in new[] { 1, 3 })
                {
                    var plan = BattlePlanner.Generate(stage, field == 1 ? StageKind.Monster : StageKind.Boss, seed++, field);
                    Check.True(plan.Monsters.Count > 0, song.Id);
                    foreach (var hook in song.Music.Sections.Where(x => x.IsHook))
                        Check.True(HookPatternGuarantee.Covers(plan, hook), song.Id + " " + hook.Name);
                    for (int i = 0; i < plan.Attacks.Count; i++) for (int j = i + 1; j < plan.Attacks.Count; j++)
                        Check.False(BattlePlanner.Conflicts(plan.Attacks[i], plan.Attacks[j], out _));
                    foreach (var attack in plan.Attacks)
                        Check.True(attack.CallStartTick >= 0 && attack.CallStartTick < attack.ResponseStartTick && attack.PhraseEndTick <= song.Music.TotalTicks);
                }
            }
        }

        [Test]
        public void MissingHooksAreRepairedAndLinkedMonsterPhrasesStayWhole()
        {
            foreach (string id in new[] { "POP-01", "JAZZ-02", "CELT-01" })
            {
                var stage = MusicStage.Generate(StageCatalog.Find(id).Music);
                foreach (var monster in MonsterCatalog.All)
                {
                    var candidates = monster.PatternPlanner.Candidates(stage, monster);
                    Check.True(HookPatternGuarantee.Eligible(stage, candidates), monster.Id + " " + id);
                    var chain = candidates[0];
                    var proposal = new MonsterProposal(monster.Id, monster, new System.Collections.Generic.List<PatternChain> { chain });
                    var plan = HookPatternGuarantee.Ensure(BattlePlanner.Resolve(stage, new[] { proposal }, 4), 5);
                    foreach (var hook in stage.Music.Sections.Where(x => x.IsHook)) Check.True(HookPatternGuarantee.Covers(plan, hook));
                    foreach (var attack in plan.Attacks.Where(x => x.Chain != null))
                        Check.Equal(attack.Chain.Placements.Count, plan.Attacks.Count(x => ReferenceEquals(x.Chain, attack.Chain)));
                }
            }
        }
    }
}
