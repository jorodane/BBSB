using System;
using BBSB.Core;
using UnityEngine;

namespace BBSB.Runtime
{
    /// <summary>The recording uses the same DSP origin and frozen offset as judgement and Calls.</summary>
    internal sealed class StageMusicPlayer
    {
        private readonly AudioSource source;
        public bool HasRecording => source.clip != null;
        public StageMusicPlayer(Transform parent, MusicDefinition music)
        {
            var child = new GameObject("Stage music"); child.transform.SetParent(parent, false);
            source = child.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0; source.volume = .78f;
            var definition = StageCatalog.Find(music.Id);
            if (definition != null) source.clip = Resources.Load<AudioClip>(definition.AudioPath);
        }
        public void Start(double origin, double elapsed, bool repeat)
        {
            source.Stop(); if (!HasRecording) return;
            source.loop = repeat;
            if (!repeat && elapsed >= source.clip.length) return;
            double position = elapsed % source.clip.length;
            source.timeSamples = Math.Min(source.clip.samples - 1, Math.Max(0, (int)Math.Round(position * source.clip.frequency)));
            source.PlayScheduled(origin);
        }
        public void SetMuted(bool muted) { source.mute = muted; }
        public void Stop() { if (source != null) source.Stop(); }
    }
}
