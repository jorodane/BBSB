using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public enum WeaponAttackTarget { Front, Rear }
    public enum NotePartKind { Frame, Injection }
    public enum NotePartEffect { Power, Mend, Pierce, ExtraTap, Hold, CrossTap }

    public sealed class NotePartDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public NotePartKind Kind { get; }
        public NotePartEffect Effect { get; }
        internal NotePartDefinition(string id, string name, string description, NotePartKind kind, NotePartEffect effect)
        { Id = id; Name = name; Description = description; Kind = kind; Effect = effect; }
    }

    public static class NotePartCatalog
    {
        public static IReadOnlyList<NotePartDefinition> All { get; } = Array.AsReadOnly(new[] {
            new NotePartDefinition("frame-power", "강타 프레임", "선택한 공격 노트의 피해 +50%. 원래 입력과 발동 조건 유지.", NotePartKind.Frame, NotePartEffect.Power),
            new NotePartDefinition("frame-mend", "회복 프레임", "선택한 노트 성공 시 추가 체력 2 회복. 반미스는 절반.", NotePartKind.Frame, NotePartEffect.Mend),
            new NotePartDefinition("frame-pierce", "후열 관통 프레임", "선택한 공격 노트가 살아 있는 후열 중 체력이 가장 적은 적을 공격. 후열이 없으면 선봉.", NotePartKind.Frame, NotePartEffect.Pierce),
            new NotePartDefinition("inject-tap", "반박 주입", "선택한 노트 뒤 반박에 추가 Tap. 원래 노트 성공이 필요하며 기본 피해의 60%.", NotePartKind.Injection, NotePartEffect.ExtraTap),
            new NotePartDefinition("inject-hold", "홀드 주입", "선택한 Tap 위에 반박 Hold를 씌우고 피해 +40%. 같은 라인의 기존 노트도 유지 입력으로 연주하고, 맞닿은 Hold는 연결돼.", NotePartKind.Injection, NotePartEffect.Hold),
            new NotePartDefinition("inject-cross", "교차 박자 주입", "선택한 노트 뒤 반박에 다음 점유 라인의 Tap 추가. 복수 라인 무기 전용, 기본 피해의 80%.", NotePartKind.Injection, NotePartEffect.CrossTap)
        });
        public static NotePartDefinition Find(string id)
        {
            foreach (var part in All) if (part.Id == id) return part;
            throw new ArgumentException("Unknown note part: " + id, nameof(id));
        }
    }

    public sealed class NotePartState
    {
        public int InstanceId { get; }
        public NotePartDefinition Definition { get; }
        internal NotePartState(int instanceId, string definitionId)
        { InstanceId = instanceId; Definition = NotePartCatalog.Find(definitionId); }
    }

    // Immutable overlays belong to an item instance, never its current input offset.
    public sealed class WeaponNoteBinding
    {
        public int PartInstanceId { get; }
        public string PartId { get; }
        public int BaseNoteIndex { get; }
        public NotePartDefinition Part => NotePartCatalog.Find(PartId);
        internal WeaponNoteBinding(NotePartState part, int baseNoteIndex)
        { PartInstanceId = part.InstanceId; PartId = part.Definition.Id; BaseNoteIndex = baseNoteIndex; }
    }

    public static class WeaponNoteAssembly
    {
        // Every starting side/offset is checked before ownership or bindings change.
        // Chaos bridges keep their authored transition rhythm and are not base sockets.
        public static WeaponPhraseSet Apply(WeaponState weapon, WeaponPhraseSet source,
            IReadOnlyList<WeaponNoteBinding> bindings = null)
        {
            if (weapon == null || source == null) throw new ArgumentNullException(nameof(weapon));
            bindings ??= weapon.NoteBindings;
            if (bindings.Count == 0) return source;
            var light = new WeaponPhrase[weapon.RequiredLanes]; var dark = new WeaponPhrase[light.Length];
            for (int i = 0; i < light.Length; i++)
            {
                light[i] = Apply(source.LightStarts[i], weapon.RequiredLanes, bindings);
                dark[i] = Apply(source.DarkStarts[i], weapon.RequiredLanes, bindings);
            }
            return new WeaponPhraseSet(weapon, light, dark, source.LightTransitions, source.DarkTransitions, source.Chaos);
        }

        private static WeaponPhrase Apply(WeaponPhrase source, int width, IReadOnlyList<WeaponNoteBinding> bindings)
        {
            var frames = new Dictionary<int, NotePartDefinition>(); var injections = new Dictionary<int, NotePartDefinition>();
            var instances = new HashSet<int>();
            foreach (var binding in bindings)
            {
                if (binding == null || binding.BaseNoteIndex < 0 || binding.BaseNoteIndex >= source.Notes.Count || !instances.Add(binding.PartInstanceId))
                    throw new ArgumentException("이 시작 패턴에는 해당 원본 노트가 없어.");
                var table = binding.Part.Kind == NotePartKind.Frame ? frames : injections;
                if (table.ContainsKey(binding.BaseNoteIndex)) throw new ArgumentException("한 원본 노트에는 프레임과 주입을 각각 하나씩 장착해.");
                table.Add(binding.BaseNoteIndex, binding.Part);
            }
            var result = new List<WeaponPhraseNote>(); var remap = new int[source.Notes.Count];
            for (int i = 0; i < source.Notes.Count; i++)
            {
                var note = source.Notes[i]; decimal damage = note.Damage, healing = note.BonusHealing;
                double hold = note.HoldBeats; var target = note.Target;
                frames.TryGetValue(i, out var frame); injections.TryGetValue(i, out var injection);
                if (frame != null)
                {
                    if (note.IsCall) throw new ArgumentException("준비용 콜 노트에는 박자 주입만 장착할 수 있어.");
                    if (frame.Effect == NotePartEffect.Power || frame.Effect == NotePartEffect.Pierce)
                    {
                        if (note.Effect != PhraseEffect.Strike || damage <= 0) throw new ArgumentException("피해가 있는 공격 노트를 선택해줘.");
                        if (frame.Effect == NotePartEffect.Power) damage *= 1.5m; else target = WeaponAttackTarget.Rear;
                    }
                    else if (frame.Effect == NotePartEffect.Mend) healing += 2;
                }
                double next = i + 1 < source.Notes.Count ? source.Notes[i + 1].Beat : source.LengthBeats;
                if (injection != null)
                {
                    if (note.Effect != PhraseEffect.Strike || note.Damage <= 0 && !note.IsCall)
                        throw new ArgumentException("박자 주입에는 피해가 있는 공격 노트가 필요해.");
                    if (injection.Effect == NotePartEffect.Hold)
                    {
                        if (note.IsHold) throw new ArgumentException("홀드 주입은 Tap에 장착해.");
                        hold = Math.Min(.5, source.LengthBeats - note.Beat);
                        damage *= 1.4m;
                    }
                    else
                    {
                        if (note.Beat + hold + .5 >= next) throw new ArgumentException("추가 Tap을 넣을 반박 공간이 없어.");
                        if (injection.Effect == NotePartEffect.CrossTap && width < 2)
                            throw new ArgumentException("교차 주입에는 복수 라인 무기가 필요해.");
                    }
                }
                int prerequisite = note.Prerequisite < 0 ? -1 : remap[note.Prerequisite];
                remap[i] = result.Count;
                result.Add(new WeaponPhraseNote(note.Beat, damage, hold, note.Effect, prerequisite, note.Condition,
                    note.LaneOffset, note.EffectDurationBeats, target, healing, i, role: note.Role));
                if (injection != null && injection.Effect != NotePartEffect.Hold)
                    result.Add(new WeaponPhraseNote(note.Beat + hold + .5,
                        note.Damage * (injection.Effect == NotePartEffect.CrossTap ? .8m : .6m),
                        prerequisite: remap[i], condition: PhraseNoteCondition.Hit,
                        laneOffset: injection.Effect == NotePartEffect.CrossTap ? (note.LaneOffset + 1) % width : note.LaneOffset,
                        target: target, baseNoteIndex: i, injected: true, role: note.Role));
            }
            // Split a continuous crown at original note boundaries. Every original effect
            // and prerequisite still resolves once; only the required input is connected.
            double crownEnd = -1;
            int crownLane = -1;
            for (int i = 0; i < result.Count; i++)
            {
                var note = result[i];
                bool connected = crownLane == note.LaneOffset && crownEnd >= note.Beat;
                double end = Math.Max(note.Beat + note.HoldBeats, connected ? crownEnd : note.Beat);
                crownEnd = end; crownLane = note.LaneOffset;
                double hold = Math.Max(0, Math.Min(end, i + 1 < result.Count ? result[i + 1].Beat : source.LengthBeats) - note.Beat);
                result[i] = new WeaponPhraseNote(note.Beat, note.Damage, hold, note.Effect, note.Prerequisite, note.Condition,
                    note.LaneOffset, note.EffectDurationBeats, note.Target, note.BonusHealing, note.BaseNoteIndex,
                    note.IsInjected, connected, note.Role);
            }
            return new WeaponPhrase(source.WeaponId, source.Name, source.Hint, source.LengthBeats, result,
                source.MissCooldownBeats, source.Repeat, source.FinisherEvery, source.FinisherDamage, source.GroggyBeats,
                source.ParryInput, source.HoldDamageReduction, source.ReleaseEndsPhrase, source.ParryRequired,
                source.CompletionCooldownBeats, source.MaximumCycles, source.FirstNoteDelayBeats);
        }
    }

    public sealed class CombatBonuses
    {
        public decimal DamageMultiplier { get; }
        public decimal GuardBonus { get; }
        public double CooldownReduction { get; }
        public CombatBonuses(int force = 0, int guard = 0, int tempo = 0)
        {
            if (force < 0 || guard < 0 || tempo < 0) throw new ArgumentOutOfRangeException(nameof(force));
            DamageMultiplier = 1 + force * .1m; GuardBonus = Math.Min(.25m, guard * .05m);
            CooldownReduction = Math.Min(3, tempo);
        }
    }
}
