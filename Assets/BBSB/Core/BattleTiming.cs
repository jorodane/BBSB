using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    // The last word is the downbeat shared by audio, input and battle time.
    public sealed class BattleCountIn
    {
        public const int LeadBeats = 3;
        public IReadOnlyList<string> Words { get; }
        public BattleCountIn(IReadOnlyList<string> words = null)
        {
            words ??= new[] { "Beat", "Block", "Shake", "Beat" };
            if (words.Count != 4) throw new ArgumentException("카운트인은 네 단어를 사용해.", nameof(words));
            var copy = new string[4];
            for (int i = 0; i < copy.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(words[i])) throw new ArgumentException("카운트인 단어를 채워줘.", nameof(words));
                copy[i] = words[i];
            }
            Words = Array.AsReadOnly(copy);
        }
        public int IndexAt(double songBeat) => songBeat < -LeadBeats || songBeat >= 1 || !WeaponPhraseNote.Finite(songBeat) ?
            -1 : (int)Math.Floor(songBeat) + LeadBeats;
        public string WordAt(double songBeat) { int index = IndexAt(songBeat); return index < 0 ? "" : Words[index]; }
    }

    public sealed class MonsterAttackWindow
    {
        public const double OpeningBeats = 3, EndingSeconds = 3;
        public double LoopBeats { get; }
        public double EndingBeats { get; }
        public MonsterAttackWindow(double loopBeats, double bpm)
        {
            if (!WeaponPhraseNote.Finite(loopBeats) || loopBeats <= 0 || !WeaponPhraseNote.Finite(bpm) || bpm <= 0)
                throw new ArgumentOutOfRangeException(nameof(loopBeats));
            LoopBeats = loopBeats; EndingBeats = EndingSeconds * bpm / 60;
        }
        internal IncomingBeatAttack Place(BeatAttack definition, double at)
        {
            double cycleStart = Math.Floor(at / LoopBeats) * LoopBeats;
            double closing = cycleStart + LoopBeats - EndingBeats;
            if (at < cycleStart + OpeningBeats || at >= closing) return null;
            var attack = new IncomingBeatAttack(definition, at);
            if (definition.IsHold && attack.EndBeat >= closing)
            {
                // Keep only complete half-beat pulses strictly before the protected outro.
                // CutoffBeat scales the damage budget instead of compressing it into the tail.
                double pulses = Math.Ceiling((closing - at) * 2 - .000001) - 1;
                if (pulses < 1) return null;
                attack.CutoffBeat = at + pulses * .5;
            }
            return attack;
        }
    }
}
