using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class NoteWorkshopTests
    {
        private static RunSession Parts(params string[] ids)
        {
            var run = new RunSession(31, useFiveLaneCombat: true);
            // Deterministic bag fixture without coupling these UI rules to random rewards.
            var acquire = typeof(RunSession).GetMethod("AcquireNotePart", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (string id in ids) acquire.Invoke(run, new object[] { id });
            return run;
        }

        [Test] public void BagGroupsFreeCopiesAndHidesEveryInstalledCopy()
        {
            var run = Parts("frame-mend", "frame-mend", "inject-hold");
            var stacks = NoteWorkshopModel.FreeParts(run);
            Check.Equal(2, stacks.Count); Check.Equal(2, stacks[0].Count);
            var source = WeaponPhraseSet.Uniform(run.OwnedWeapons[0]);
            int first = run.NoteParts[0].InstanceId, second = run.NoteParts[1].InstanceId;
            Check.True(NoteWorkshopModel.TryAttach(run, first, 0, 0, source, out _, true));
            Check.Equal(2, NoteWorkshopModel.FreeParts(run)[0].Count);
            Check.Equal(0, run.OwnedWeapons[0].NoteBindings.Count);
            Check.True(NoteWorkshopModel.TryAttach(run, first, 0, 0, source, out _));
            Check.Equal(second, NoteWorkshopModel.FreeParts(run)[0].Part.InstanceId);
            Check.Equal(1, NoteWorkshopModel.FreeParts(run)[0].Count);
            Check.True(run.TryAttachNotePart(1, 1, 0, out _));
            Check.Equal(1, NoteWorkshopModel.FreeParts(run).Count);
            Check.Equal("inject-hold", NoteWorkshopModel.FreeParts(run)[0].Part.Definition.Id);
            Check.False(NoteWorkshopModel.TryAttach(run, second, 0, 0, source, out _));
            Check.True(ReferenceEquals(run.OwnedWeapons[1], run.NotePartOwner(second)));
            Check.False(NoteWorkshopModel.Remove(run, second, run.OwnedWeapons[0]));
            Check.True(NoteWorkshopModel.Remove(run, first, run.OwnedWeapons[0]));
            Check.Equal(2, NoteWorkshopModel.FreeParts(run).Count);
            Check.Equal(3, run.NoteParts.Count);
        }

        [Test] public void ReplacingAndInvalidDropsPreserveInstancesAndBaseNotes()
        {
            var run = Parts("frame-mend", "frame-mend", "inject-cross");
            var weapon = run.OwnedWeapons[0]; var source = WeaponPhraseSet.Uniform(weapon);
            int first = run.NoteParts[0].InstanceId, second = run.NoteParts[1].InstanceId;
            Check.True(NoteWorkshopModel.TryAttach(run, first, 0, 0, source, out _));
            Check.False(NoteWorkshopModel.TryAttach(run, second, 0, 99, source, out _));
            Check.False(NoteWorkshopModel.TryAttach(run, 999, 0, 0, source, out _));
            Check.False(NoteWorkshopModel.TryAttach(run, run.NoteParts[2].InstanceId, 0, 0, source, out _));
            Check.Equal(first, weapon.NoteBindings.Single().PartInstanceId);
            Check.True(NoteWorkshopModel.TryAttach(run, second, 0, 0, source, out _));
            Check.Equal(second, weapon.NoteBindings.Single().PartInstanceId);
            Check.Equal(first, NoteWorkshopModel.FreeParts(run)[0].Part.InstanceId);
            Check.Equal(source.LightStarts[0].Notes.Count, WeaponNoteAssembly.Apply(weapon, source).LightStarts[0].Notes.Count);
        }

        [Test] public void InstalledPartMovesWithinItsWeaponButBattleLockStopsCommit()
        {
            var run = Parts("frame-mend"); var weapon = new WeaponState("bow"); run.Equipment.Acquire(weapon);
            var source = WeaponPhraseSet.Uniform(weapon); int id = run.NoteParts[0].InstanceId;
            Check.True(NoteWorkshopModel.TryAttach(run, id, 2, 0, source, out _));
            Check.True(NoteWorkshopModel.TryAttach(run, id, 2, 1, source, out _));
            Check.Equal(1, weapon.NoteBindings.Single().BaseNoteIndex); Check.Equal(0, NoteWorkshopModel.FreeParts(run).Count);
            run.Enter(run.Map.Nodes.First(x => run.CanEnter(x.Id)).Id); Check.True(run.StartFiveLaneBattle() != null);
            Check.False(NoteWorkshopModel.TryAttach(run, id, 2, 0, source, out _));
            Check.False(NoteWorkshopModel.Remove(run, id, weapon));
            Check.Equal(1, weapon.NoteBindings.Single().BaseNoteIndex);
        }

        [Test] public void AutoExampleKeepsDaggerAndDualSwordNativeInfiniteIntervals()
        {
            foreach (var pair in new[] { ("dagger", 2d), ("dual-swords", 1d) })
            {
                var demo = new NoteWorkshopPlayback(WeaponPhraseSet.Uniform(new WeaponState(pair.Item1)).LightStarts[0], 1, 0);
                Check.Equal(pair.Item2, demo.LoopBeats);
                Check.Equal(NoteDemoInput.Idle, demo.InputAt(-.01, 0));
                Check.Equal(NoteDemoInput.Press, demo.InputAt(0, 0));
                Check.Equal(NoteDemoInput.Idle, demo.InputAt(.4, 0));
                Check.Equal(NoteDemoInput.Press, demo.InputAt(pair.Item2, 0));
                Check.Equal(NoteDemoInput.Press, demo.InputAt(pair.Item2 * 10000, 0));
                Check.Equal(0d, demo.PatternBeat(pair.Item2 * 10000));
            }
        }

        [Test] public void CrownChainsShowOnePressOneContinuousHoldAndOneRelease()
        {
            var weapon = new WeaponState("dagger");
            var original = new WeaponPhrase("dagger", "crown fixture", "", 2, new[] {
                new WeaponPhraseNote(0, 10), new WeaponPhraseNote(.25, 10), new WeaponPhraseNote(.5, 10), new WeaponPhraseNote(1.5, 10) });
            var bindings = new[] { new WeaponNoteBinding(new NotePartState(1, "inject-hold"), 0),
                new WeaponNoteBinding(new NotePartState(2, "inject-hold"), 2) };
            var composed = WeaponNoteAssembly.Apply(weapon, WeaponPhraseSet.Uniform(weapon, original), bindings).LightStarts[0];
            var demo = new NoteWorkshopPlayback(composed, 1, 0);
            Check.Equal(4, composed.Notes.Count); Check.Equal(NoteDemoInput.Press, demo.InputAt(0, 0));
            Check.Equal(NoteDemoInput.Hold, demo.InputAt(.25, 0)); Check.Equal(NoteDemoInput.Hold, demo.InputAt(.5, 0));
            Check.Equal(NoteDemoInput.Hold, demo.InputAt(.99, 0)); Check.Equal(NoteDemoInput.Release, demo.InputAt(1, 0));
            Check.Equal(NoteDemoInput.Idle, demo.InputAt(1.3, 0)); Check.Equal(NoteDemoInput.Press, demo.InputAt(1.5, 0));
        }

        [Test] public void ExampleRoutesCrossNotesFromTheSelectedStartingLane()
        {
            var weapon = new WeaponState("staff");
            var phrase = new WeaponPhrase("staff", "cross fixture", "", 2, new[] {
                new WeaponPhraseNote(0, 10), new WeaponPhraseNote(.5, 5, prerequisite: 0,
                    condition: PhraseNoteCondition.Hit, laneOffset: 1, injected: true) }, repeat: true);
            var demo = new NoteWorkshopPlayback(phrase, 2, 1);
            Check.Equal(NoteDemoInput.Press, demo.InputAt(0, 1)); Check.Equal(NoteDemoInput.Idle, demo.InputAt(0, 0));
            Check.Equal(NoteDemoInput.Press, demo.InputAt(.5, 0)); Check.Equal(NoteDemoInput.Idle, demo.InputAt(.5, 1));
            var starts = new List<double>(); var lanes = new List<int>();
            demo.VisitNotes(-3, 3, (n, start, end, lane) => { starts.Add(start); lanes.Add(lane); });
            Check.True(starts.SequenceEqual(new[] { 0d, .5, 2, 2.5 })); Check.True(lanes.SequenceEqual(new[] { 1, 0, 1, 0 }));
        }

        [Test] public void TouchingHoldsStayPressedAcrossNativeRepeatBoundaries()
        {
            var phrase = new WeaponPhrase("dagger", "joined", "", 1, new[] {
                new WeaponPhraseNote(0, 3, .5), new WeaponPhraseNote(.5, 4, .5) }, repeat: true);
            var demo = new NoteWorkshopPlayback(phrase, 1, 0);
            Check.Equal(NoteDemoInput.Press, demo.InputAt(0, 0));
            foreach (double beat in new[] { .5, 1, 1.5, 2, 10000 })
                Check.Equal(NoteDemoInput.Hold, demo.InputAt(beat, 0));
            Check.False(demo.ConnectsFromPrevious(phrase.Notes[0], 0));
            Check.True(demo.ConnectsFromPrevious(phrase.Notes[1], .5));
            Check.True(demo.ConnectsFromPrevious(phrase.Notes[0], 1));
        }

        [Test] public void HoldTailsStayVisibleAndFinitePatternsRestBeforeRestart()
        {
            var phrase = new WeaponPhrase("bow", "finite fixture", "", 2, new[] { new WeaponPhraseNote(0, 10, 1.5) },
                repeat: true, maximumCycles: 2, completionCooldownBeats: 2);
            var demo = new NoteWorkshopPlayback(phrase, 1, 0);
            Check.Equal(6d, demo.LoopBeats); Check.Equal(2, demo.CyclesPerLoop);
            int visible = 0; demo.VisitNotes(1, 1.25, (n, s, e, l) => { visible++; Check.Equal(0d, s); Check.Equal(1.5, e); });
            Check.Equal(1, visible); Check.Equal(NoteDemoInput.Release, demo.InputAt(1.5, 0));
            Check.Equal(NoteDemoInput.Press, demo.InputAt(2, 0));
            Check.Equal(-1d, demo.PatternBeat(4)); Check.Equal(NoteDemoInput.Idle, demo.InputAt(4, 0));
            Check.Equal(NoteDemoInput.Press, demo.InputAt(6, 0)); Check.Equal(0d, demo.PatternBeat(6));
        }
    }
}
