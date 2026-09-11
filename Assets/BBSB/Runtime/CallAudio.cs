using System;
using BBSB.Core;
using UnityEngine;

namespace BBSB.Runtime
{
    /// <summary>Stable cue timbres. Captions are mnemonic sounds, not recorded speech.</summary>
    public static class CallAudio
    {
        public static AudioClip CreateClip(CallSound sound, double beatSeconds)
        {
            if (!Enum.IsDefined(typeof(CallSound), sound)) throw new ArgumentOutOfRangeException(nameof(sound));
            if (double.IsNaN(beatSeconds) || double.IsInfinity(beatSeconds) || beatSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(beatSeconds));
            const int rate = 44100;
            // Even the shortest half-beat response/next cue starts after this sound has finished.
            double duration = beatSeconds * .35;
            int length = Math.Max(2, (int)(rate * duration));
            double start, end, overtone = 0, ratio = 2, decay = 5, noise = 0;
            switch (sound)
            {
                case CallSound.Wood: start = end = 640; overtone = .45; ratio = 1.61; decay = 9; break;
                case CallSound.Drum: start = 190; end = 75; overtone = .15; decay = 7; break;
                case CallSound.Bell: start = end = 960; overtone = .5; ratio = 2.76; decay = 3; break;
                case CallSound.RisingChime: start = 440; end = 880; overtone = .45; ratio = 3; decay = 3; break;
                case CallSound.FallingChime: start = 880; end = 330; overtone = .45; ratio = 3; decay = 3; break;
                case CallSound.RisingWhistle: start = 300; end = 1300; decay = 1.5; break;
                case CallSound.FallingWhistle: start = 1200; end = 240; decay = 1.5; break;
                case CallSound.Rattle: start = end = 1800; noise = .85; decay = 2; break;
                case CallSound.Sweep: start = 700; end = 120; noise = .65; decay = 2; break;
                default: throw new ArgumentOutOfRangeException(nameof(sound));
            }
            var samples = new float[length]; uint random = 17;
            for (int i = 0; i < length; i++)
            {
                double t = i / (double)rate, p = t / duration;
                double phase = 2 * Math.PI * (start * t + (end - start) * t * t / (2 * duration));
                random = unchecked(random * 1664525u + 1013904223u);
                double grain = (random >> 8) / 8388607.5 - 1;
                double tone = (Math.Sin(phase) + overtone * Math.Sin(phase * ratio)) / (1 + overtone);
                double envelope = Math.Exp(-p * decay) * Math.Min(1, t / .002) * Math.Min(1, (1 - p) * 20);
                if (sound == CallSound.Rattle) envelope *= .25 + .75 * Math.Pow(Math.Cos(p * Math.PI * 5), 4);
                samples[i] = (float)((tone * (1 - noise) + grain * noise) * envelope);
            }
            var clip = AudioClip.Create("BBSB Call " + sound, length, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }
    }
}
