using System;
using System.Collections.Generic;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    public enum MonsterAttackMotion { Linear, Lob, Relay, WaitRush, Walk, Extend, Materialize }
    public enum MonsterAttackShape { Jelly, Gauntlet, Feather, Foxfire, Dream, Doll, Tail, Electric, Scale, Veil, Membrane, Thread }
    public enum MonsterAttackReaction { Burst, Recoil, Scatter, Fade, Withdraw, Push }

    /// <summary>One visual actor per ORIGINAL monster step, regardless of the number of bound weapons.</summary>
    public sealed class MonsterAttackDefinition
    {
        public const string ResourceRoot = "BBSB/BattleArt/MonsterAttacks/";
        public string MonsterId { get; }
        public string PatternId { get; }
        public int StepIndex { get; }
        public MonsterAttackMotion Motion { get; }
        public MonsterAttackShape Shape { get; }
        public MonsterAttackReaction Reaction { get; }
        public int CallIndex { get; }
        public int SpawnOffsetTicks { get; }
        public int RushTicks { get; }
        public double Height { get; }
        public double Arc { get; }
        public bool Stretch { get; }
        public bool Grounded { get; }
        public string PatternFolder => ResourceRoot + MonsterId + "/" + PatternId;
        public string ResourceFolder => PatternFolder + "/step-" + StepIndex;

        internal MonsterAttackDefinition(string monster, string pattern, int step, MonsterAttackMotion motion,
            MonsterAttackShape shape, MonsterAttackReaction reaction, int call = 0, int offset = 0,
            double height = .24, double arc = .18, int rush = 1, bool stretch = false, bool grounded = false)
        {
            MonsterId = monster; PatternId = pattern; StepIndex = step; Motion = motion; Shape = shape;
            Reaction = reaction; CallIndex = call; SpawnOffsetTicks = offset; Height = height; Arc = arc;
            RushTicks = rush; Stretch = stretch; Grounded = grounded;
        }

        public double SpawnSeconds(ResponseNote note, double beatSeconds) =>
            (note.Attack.Call[Math.Min(CallIndex, note.Attack.Call.Count - 1)].Tick + SpawnOffsetTicks) * beatSeconds / RhythmTime.TicksPerBeat;
    }

    public static class MonsterAttackCatalog
    {
        public static IReadOnlyList<MonsterAttackDefinition> All { get; } = Build();
        private static readonly Dictionary<string, MonsterAttackDefinition> lookup = Index();

        public static MonsterAttackDefinition Find(string monster, string pattern, int step) =>
            lookup.TryGetValue(Key(monster, pattern, step), out var value) ? value : null;

        public static MonsterAttackDefinition For(ResponseNote note) =>
            Find(note.Attack.Monster.Id, note.Attack.Pattern.Id, note.StepIndex) ??
            new MonsterAttackDefinition(note.Attack.Monster.Id, note.Attack.Pattern.Id, note.StepIndex,
                MonsterAttackMotion.Linear, MonsterAttackShape.Jelly, MonsterAttackReaction.Burst);

        private static string Key(string monster, string pattern, int step) => monster + "/" + pattern + "/" + step;
        private static Dictionary<string, MonsterAttackDefinition> Index()
        {
            var values = new Dictionary<string, MonsterAttackDefinition>();
            foreach (var value in All) values.Add(Key(value.MonsterId, value.PatternId, value.StepIndex), value);
            return values;
        }

        private static IReadOnlyList<MonsterAttackDefinition> Build()
        {
            var values = new List<MonsterAttackDefinition>();
            foreach (var monster in MonsterCatalog.All)
            foreach (var pattern in monster.Patterns)
            for (int i = 0; i < pattern.Pattern.Steps.Count; i++)
            {
                int offset = pattern.Pattern.Steps[i].OffsetTick;
                MonsterAttackDefinition D(MonsterAttackMotion motion, MonsterAttackShape shape, MonsterAttackReaction reaction,
                    int call = 0, int delay = 0, double height = .24, double arc = .18, int rush = 1,
                    bool stretch = false, bool grounded = false) =>
                    new MonsterAttackDefinition(monster.Id, pattern.Id, i, motion, shape, reaction, call, delay, height, arc, rush, stretch, grounded);

                // The motion, source Call and subsequent emissions are authored per pattern, not per input kind.
                switch (pattern.Id)
                {
                    case "count-four-tap": values.Add(D(MonsterAttackMotion.Relay, MonsterAttackShape.Jelly, MonsterAttackReaction.Burst, height: .48, grounded: true)); break;
                    case "tresillo-call-tap": values.Add(D(MonsterAttackMotion.Relay, MonsterAttackShape.Jelly, MonsterAttackReaction.Burst, height: .34, arc: .30)); break;
                    case "march-three": values.Add(D(MonsterAttackMotion.Linear, MonsterAttackShape.Gauntlet, MonsterAttackReaction.Recoil, delay: offset, height: .30)); break;
                    case "march-spaced": values.Add(D(MonsterAttackMotion.Lob, MonsterAttackShape.Gauntlet, MonsterAttackReaction.Recoil, delay: offset, height: .34, arc: .36)); break;
                    case "tresillo-taps": values.Add(D(MonsterAttackMotion.Linear, MonsterAttackShape.Feather, MonsterAttackReaction.Scatter, call: i, height: .30)); break;
                    case "rotated-tresillo": values.Add(D(MonsterAttackMotion.Linear, MonsterAttackShape.Feather, MonsterAttackReaction.Scatter, height: .30)); break;
                    case "offbeat-single-tap": values.Add(D(MonsterAttackMotion.WaitRush, MonsterAttackShape.Foxfire, MonsterAttackReaction.Fade)); break;
                    case "offbeat-pair": values.Add(D(MonsterAttackMotion.WaitRush, MonsterAttackShape.Foxfire, MonsterAttackReaction.Fade, call: i)); break;
                    case "drowsy-quick-tap": values.Add(D(MonsterAttackMotion.Lob, MonsterAttackShape.Dream, MonsterAttackReaction.Fade, height: .32)); break;
                    case "drowsy-four-beat-wait": values.Add(D(MonsterAttackMotion.Materialize, MonsterAttackShape.Dream, MonsterAttackReaction.Fade, height: .32)); break;
                    case "clock-quick-tap": values.Add(D(MonsterAttackMotion.Lob, MonsterAttackShape.Doll, MonsterAttackReaction.Recoil, height: .52, arc: .75, grounded: true)); break;
                    case "clock-seven-beat-wait": values.Add(D(MonsterAttackMotion.Walk, MonsterAttackShape.Doll, MonsterAttackReaction.Recoil, height: .52, grounded: true)); break;
                    case "seesaw-steady-tap": values.Add(D(MonsterAttackMotion.Extend, MonsterAttackShape.Tail, MonsterAttackReaction.Withdraw, height: .17, stretch: true)); break;
                    case "seesaw-early-finish": values.Add(D(MonsterAttackMotion.Extend, MonsterAttackShape.Tail, MonsterAttackReaction.Withdraw, delay: offset, height: .17, stretch: true)); break;
                    case "bat-quick-taps": values.Add(D(MonsterAttackMotion.Linear, MonsterAttackShape.Electric, MonsterAttackReaction.Scatter, call: i, height: .25)); break;
                    case "bat-hold": values.Add(D(MonsterAttackMotion.Extend, MonsterAttackShape.Electric, MonsterAttackReaction.Fade, height: .30, stretch: true)); break;
                    case "turtle-long-hold": values.Add(D(MonsterAttackMotion.Lob, MonsterAttackShape.Scale, MonsterAttackReaction.Recoil, height: .60, arc: .08)); break;
                    case "turtle-hold-tap": values.Add(i == 0 ?
                        D(MonsterAttackMotion.Lob, MonsterAttackShape.Scale, MonsterAttackReaction.Recoil, height: .60, arc: .08) :
                        D(MonsterAttackMotion.WaitRush, MonsterAttackShape.Scale, MonsterAttackReaction.Recoil, call: 1, height: .26, rush: 2)); break;
                    case "ray-deep-dive": values.Add(D(MonsterAttackMotion.Extend, MonsterAttackShape.Veil, MonsterAttackReaction.Withdraw, call: 1, height: .25, stretch: true)); break;
                    case "ray-short-dive": values.Add(D(MonsterAttackMotion.Extend, MonsterAttackShape.Veil, MonsterAttackReaction.Withdraw, height: .22, stretch: true)); break;
                    case "one-beat-shake": values.Add(D(MonsterAttackMotion.Lob, MonsterAttackShape.Membrane, MonsterAttackReaction.Push, height: .60, arc: .15)); break;
                    case "two-bubble-shakes": values.Add(D(MonsterAttackMotion.Lob, MonsterAttackShape.Membrane, MonsterAttackReaction.Push, delay: offset, height: .52, arc: .15)); break;
                    case "counted-flick": values.Add(D(MonsterAttackMotion.WaitRush, MonsterAttackShape.Thread, MonsterAttackReaction.Withdraw, height: .07, rush: 2, stretch: true)); break;
                    case "offbeat-flick": values.Add(D(MonsterAttackMotion.WaitRush, MonsterAttackShape.Thread, MonsterAttackReaction.Withdraw, height: .07, rush: 1, stretch: true)); break;
                    default: throw new InvalidOperationException("Author the new monster attack's image slots: " + pattern.Id);
                }
            }
            return values.AsReadOnly();
        }
    }
}
