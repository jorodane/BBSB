using System;
using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;

namespace BBSB.Runtime
{
    /// <summary>Sample scores have no recording yet. Schedule the beat guide and distinct monster Call tones on the same DSP clock.</summary>
    internal sealed class BeatMetronome : IDisposable
    {
        private readonly AudioSource[] voices = new AudioSource[4];
        private readonly AudioSource[] callVoices = new AudioSource[8];
        private readonly List<AudioClip> callClips = new List<AudioClip>();
        private readonly List<(double seconds, AudioClip clip)> calls = new List<(double, AudioClip)>();
        private readonly AudioClip accent, beat, offbeat;
        private readonly double halfBeat;
        private readonly int beatsPerBar;
        private int nextPulse, voice, nextCall, callVoice;
        public bool Muted { get; private set; }
        private bool callsSuppressed;
        public void SuppressCalls()
        { if (callsSuppressed) return; callsSuppressed = true; foreach (var source in callVoices) source.Stop(); }

        public BeatMetronome(Transform parent, BattlePlan plan)
        {
            double bpm = plan.Stage.Music.Bpm;
            halfBeat = 30.0 / bpm; beatsPerBar = plan.Stage.Music.BeatsPerBar;
            accent = Click("Downbeat", 1200); beat = Click("Beat", 850); offbeat = Click("Offbeat", 550);
            for (int i = 0; i < voices.Length; i++)
            {
                var child = new GameObject("Beat audio " + i); child.transform.SetParent(parent, false);
                voices[i] = child.AddComponent<AudioSource>();
                voices[i].playOnAwake = false; voices[i].spatialBlend = 0; voices[i].loop = false;
            }
            for (int i = 0; i < callVoices.Length; i++)
            {
                var child = new GameObject("Call audio " + i); child.transform.SetParent(parent, false);
                callVoices[i] = child.AddComponent<AudioSource>();
                callVoices[i].playOnAwake = false; callVoices[i].spatialBlend = 0; callVoices[i].loop = false;
            }
            var cueClips = new Dictionary<CallSound, AudioClip>();
            foreach (var call in plan.Calls)
            {
                if (!cueClips.TryGetValue(call.Sound, out var clip))
                {
                    clip = CallAudio.CreateClip(call.Sound, halfBeat * 2);
                    cueClips.Add(call.Sound, clip); callClips.Add(clip);
                }
                calls.Add((RhythmTime.Seconds(call.Tick, bpm), clip));
            }
        }

        public void Restart(double elapsed, bool firstStart = false)
        {
            Stop(); nextPulse = firstStart ? 0 : (int)Math.Floor(elapsed / halfBeat) + 1;
            nextCall = 0;
            if (!firstStart) while (nextCall < calls.Count && calls[nextCall].seconds <= elapsed) nextCall++;
        }

        public void RepeatLoop(double duration, double loops)
        {
            // Keep the downbeat already queued in the previous loop's lookahead.
            nextPulse = (int)Math.Max(0, nextPulse - Math.Ceiling(duration / halfBeat) * loops);
            nextCall = (int)Math.Max(0, nextCall - calls.Count * loops);
        }

        public void Schedule(double dspNow, double origin, double offset, double duration, bool repeat = false)
        {
            int pulsesPerLoop = Math.Max(1, (int)Math.Ceiling(duration / halfBeat));
            while (repeat || nextPulse * halfBeat < duration)
            {
                int pulse = repeat ? nextPulse % pulsesPerLoop : nextPulse;
                double cycle = repeat ? nextPulse / pulsesPerLoop * duration : 0;
                double at = origin + cycle + pulse * halfBeat - offset;
                if (at > dspNow + .15) break;
                nextPulse++;
                // Don't burst through clicks missed by a long frame or while muted.
                if (Muted || at < dspNow + .005) continue;
                var source = voices[voice++ % voices.Length];
                bool strong = pulse % (beatsPerBar * 2) == 0, half = pulse % 2 != 0;
                source.clip = strong ? accent : half ? offbeat : beat;
                source.volume = strong ? .36f : half ? .12f : .24f;
                source.PlayScheduled(at);
            }
            while (!callsSuppressed && calls.Count > 0 && (repeat || nextCall < calls.Count))
            {
                var call = calls[nextCall % calls.Count];
                double cycle = repeat ? nextCall / calls.Count * duration : 0;
                double at = origin + cycle + call.seconds - offset;
                if (at > dspNow + .15) break;
                nextCall++;
                if (Muted || at < dspNow + .005) continue;
                var source = callVoices[callVoice++ % callVoices.Length];
                source.clip = call.clip; source.volume = .5f; source.PlayScheduled(at);
            }
        }

        public void SetMuted(bool value) { Muted = value; if (value) Stop(); }
        public void Stop()
        {
            foreach (var source in voices) if (source != null) source.Stop();
            foreach (var source in callVoices) if (source != null) source.Stop();
        }
        public void Dispose()
        {
            Stop(); UnityEngine.Object.Destroy(accent); UnityEngine.Object.Destroy(beat); UnityEngine.Object.Destroy(offbeat);
            foreach (var clip in callClips) UnityEngine.Object.Destroy(clip);
        }

        private static AudioClip Click(string name, double frequency, double duration = .035)
        {
            const int rate = 44100;
            int length = (int)(rate * duration);
            var samples = new float[length];
            for (int i = 0; i < samples.Length; i++)
            {
                double t = i / (double)rate;
                samples[i] = (float)(Math.Sin(2 * Math.PI * frequency * t) * Math.Exp(-t * 5 / duration) * Math.Min(1, t / .001));
            }
            var clip = AudioClip.Create("BBSB " + name, length, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }
    }
}
