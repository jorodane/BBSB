namespace BBSB.Core
{
    // Only built-in fallbacks change. Authored overrides, Chaos phrases and note
    // attachments retain their own rhythms; no legacy save or pattern is rewritten.
    internal static class HandAttackPatterns
    {
        internal static WeaponPhrase Default(string id)
        {
            if (id != "dagger" && id != "dual-swords" && id != "war-drum") return null;
            var original = WeaponPhraseCatalog.Find(id);
            bool drum = id == "war-drum";
            double cooldown = drum ? original.CompletionCooldownBeats : original.LengthBeats;
            var note = drum ? new WeaponPhraseNote(0, 0, effect: PhraseEffect.AdvanceRear) : original.Notes[0];
            return new WeaponPhrase(id, original.Name,
                (drum ? "Call 1회 → 후열을 선봉으로 호출" : "Call 1회 → 타격") + " · 쿨타임 " + cooldown + "박",
                original.LengthBeats, new[] { note }, original.MissCooldownBeats,
                completionCooldownBeats: cooldown, attackOnCall: true, cooldownFromLastInput: true);
        }
    }
}
