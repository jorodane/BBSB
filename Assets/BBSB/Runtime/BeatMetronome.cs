using System;
using UnityEngine;

namespace BBSB.Runtime
{
    /// <summary>Sample scores have no recording yet. Schedule a quiet, synthetic half-beat guide on the DSP clock.</summary>
    internal sealed class BeatMetronome : IDisposable
    {
        private readonly AudioSource[] voices = new AudioSource[4];
        private readonly AudioClip accent, beat, offbeat;
        private readonly double halfBeat;
        private readonly int beatsPerBar;
        private int nextPulse, voice;
        public bool Muted { get; private set; }

        public BeatMetronome(Transform parent, double bpm, int beatsPerBar)
        {
            halfBeat = 30.0 / bpm; this.beatsPerBar = beatsPerBar;
            accent = Click("Downbeat", 1200); beat = Click("Beat", 850); offbeat = Click("Offbeat", 550);
            for (int i = 0; i < voices.Length; i++)
            {
                var child = new GameObject("Beat audio " + i); child.transform.SetParent(parent, false);
                voices[i] = child.AddComponent<AudioSource>();
                voices[i].playOnAwake = false; voices[i].spatialBlend = 0; voices[i].loop = false;
            }
        }

        public void Restart(double elapsed, bool firstStart = false)
        { Stop(); nextPulse = firstStart ? 0 : (int)Math.Floor(elapsed / halfBeat) + 1; }

        public void Schedule(double dspNow, double origin, double offset, double duration)
        {
            while (nextPulse * halfBeat < duration)
            {
                double at = origin + nextPulse * halfBeat - offset;
                if (at > dspNow + .15) break;
                int pulse = nextPulse++;
                // Don't burst through clicks missed by a long frame or while muted.
                if (Muted || at < dspNow + .005) continue;
                var source = voices[voice++ % voices.Length];
                bool strong = pulse % (beatsPerBar * 2) == 0, half = pulse % 2 != 0;
                source.clip = strong ? accent : half ? offbeat : beat;
                source.volume = strong ? .36f : half ? .12f : .24f;
                source.PlayScheduled(at);
            }
        }

        public void SetMuted(bool value) { Muted = value; if (value) Stop(); }
        public void Stop() { foreach (var source in voices) if (source != null) source.Stop(); }
        public void Dispose()
        {
            Stop(); UnityEngine.Object.Destroy(accent); UnityEngine.Object.Destroy(beat); UnityEngine.Object.Destroy(offbeat);
        }

        private static AudioClip Click(string name, double frequency)
        {
            const int rate = 44100, length = 1544;
            var samples = new float[length];
            for (int i = 0; i < samples.Length; i++)
            {
                double t = i / (double)rate;
                samples[i] = (float)(Math.Sin(2 * Math.PI * frequency * t) * Math.Exp(-t * 150) * Math.Min(1, t / .001));
            }
            var clip = AudioClip.Create("BBSB " + name, length, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }
    }
}
