using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public sealed partial class RunSession
    {
        private readonly List<NotePartState> noteParts = new List<NotePartState>();
        private int nextNotePartId;
        public IReadOnlyList<NotePartState> NoteParts => noteParts.AsReadOnly();
        public CombatBonuses BattleBonuses => new CombatBonuses(CountAugment("force-rhythm"), CountAugment("steady-guard"), CountAugment("quick-rest"));

        public WeaponState NotePartOwner(int instanceId)
        {
            foreach (var weapon in OwnedWeapons) foreach (var binding in weapon.NoteBindings)
                if (binding.PartInstanceId == instanceId) return weapon;
            return null;
        }

        public bool TryAttachNotePart(int partIndex, int weaponIndex, int baseNoteIndex, out string reason,
            WeaponPhraseSet source = null, bool previewOnly = false)
        {
            reason = "지금은 노트를 편집할 수 없어.";
            if (!CanEditEquipment || !ValidOwned(weaponIndex) || partIndex < 0 || partIndex >= noteParts.Count) return false;
            var weapon = OwnedWeapons[weaponIndex]; var part = noteParts[partIndex];
            var candidate = new List<WeaponNoteBinding>(weapon.NoteBindings);
            candidate.RemoveAll(x => x.PartInstanceId == part.InstanceId ||
                (x.BaseNoteIndex == baseNoteIndex && x.Part.Kind == part.Definition.Kind));
            candidate.Add(new WeaponNoteBinding(part, baseNoteIndex));
            try { WeaponNoteAssembly.Apply(weapon, source ?? WeaponPhraseSet.Uniform(weapon), candidate); }
            catch (ArgumentException error) { reason = error.Message; return false; }
            if (!previewOnly)
            {
                // A moved part can never remain installed in two weapons. Replaced
                // overlays stay in the inventory; the immutable base is never removed.
                var previous = NotePartOwner(part.InstanceId);
                if (previous != null && !ReferenceEquals(previous, weapon))
                {
                    var remaining = new List<WeaponNoteBinding>(previous.NoteBindings);
                    remaining.RemoveAll(x => x.PartInstanceId == part.InstanceId); previous.SetNoteBindings(remaining);
                }
                weapon.SetNoteBindings(candidate);
            }
            reason = ""; return true;
        }

        public bool RemoveNotePart(int partIndex)
        {
            if (!CanEditEquipment || partIndex < 0 || partIndex >= noteParts.Count) return false;
            var part = noteParts[partIndex]; var owner = NotePartOwner(part.InstanceId);
            if (owner == null) return false;
            var remaining = new List<WeaponNoteBinding>(owner.NoteBindings);
            remaining.RemoveAll(x => x.PartInstanceId == part.InstanceId); owner.SetNoteBindings(remaining); return true;
        }
        private void ResetNoteWorkshop() { noteParts.Clear(); nextNotePartId = 0; }
        private void AcquireNotePart(string id) => noteParts.Add(new NotePartState(++nextNotePartId, id));
    }
}
