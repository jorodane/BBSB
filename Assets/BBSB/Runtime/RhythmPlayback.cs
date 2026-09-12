using System;
using BBSB.Core;
using BBSB.Runtime.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BBSB.Runtime
{
    /// <summary>Owns one live round, its DSP clock, pointer capture, beat audio and pause lifecycle.</summary>
    public sealed class RhythmPlayback : MonoBehaviour
    {
        public RhythmRound Round { get; private set; }
        public bool IsPaused { get; private set; }
        public bool WaitingForContact { get; private set; }
        public bool CanReceiveInput => Round != null && !Round.Finished && !completed && (!IsPaused || WaitingForContact);
        private RhythmInputSurface surface;
        private RhythmPlaybackView view;
        private BeatMetronome metronome;
        private Action<RhythmRound> onFinished;
        private Action onLeave;
        private double origin, offset;
        private bool heldAtPause, completed;

        internal void Bind(RhythmRound round, RunSession session, RunUI ui, Action<RhythmRound> finished, Action leave)
        {
            Round = round; onFinished = finished; onLeave = leave;
            surface = gameObject.AddComponent<RhythmInputSurface>(); surface.Bind(this);
            view = new RhythmPlaybackView((RectTransform)transform, ui, round, session, Pause, Continue, ToggleSound, Leave);
            metronome = new BeatMetronome(transform, Round.Plan);
            RestartClock(true); view.Refresh(0, false);
        }

        private double Now => Math.Max(Round.ElapsedSeconds, offset + Math.Max(0, AudioSettings.dspTime - origin));

        private void Update()
        {
            if (Round == null || completed || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (IsPaused && !WaitingForContact) Continue(); else Pause();
        }

        private void LateUpdate()
        {
            if (Round == null || completed) return;
            if (!IsPaused)
            {
                double now = Now;
                // Sample after UI events, once per frame. Sampling the stale position in Update
                // would incorrectly count movement as stationary time depending on script order.
                if (surface.Captured) Round.Move(now, surface.Position.x, surface.Position.y);
                else Round.Advance(now);
                if (Round.Finished) { Finish(); return; }
                if (Round.Combat != null && Round.Combat.Victory) metronome.SuppressCalls();
                metronome.Schedule(AudioSettings.dspTime, origin, offset,
                    Round.Combat != null && Round.Combat.Victory ? Round.Combat.OverkillEndSeconds : Round.Plan.Stage.Music.DurationSeconds);
            }
            view.Refresh(Round.ElapsedSeconds, WaitingForContact);
            if (Round.Finished) Finish();
        }

        internal void PointerDown(Vector2 position)
        {
            if (!CanReceiveInput) return;
            if (WaitingForContact)
            {
                Round.Resume(true, position.x, position.y);
                WaitingForContact = IsPaused = false; RestartClock();
            }
            else Round.Press(Now, position.x, position.y);
            if (Round.Finished) { Finish(); return; }
            view.Refresh(Round.ElapsedSeconds, false);
        }

        internal void PointerUp(Vector2 position)
        {
            if (!CanReceiveInput || IsPaused) return;
            Round.Release(Now, position.x, position.y);
            if (Round.Finished) { Finish(); return; }
            view.Refresh(Round.ElapsedSeconds, false);
        }

        public void Pause()
        {
            if (Round == null || completed) return;
            if (IsPaused)
            {
                // Reopening the menu during recontact must disable rhythm input again,
                // while retaining the original held-at-pause state and frozen clock.
                if (WaitingForContact)
                { WaitingForContact = false; surface.Cancel(); view.ShowPause(true); }
                return;
            }
            if (surface.Captured) Round.Move(Now, surface.Position.x, surface.Position.y);
            else Round.Advance(Now);
            if (Round.Finished) { Finish(); return; }
            view.Refresh(Round.ElapsedSeconds, false);
            heldAtPause = Round.Suspend(); IsPaused = true; WaitingForContact = false;
            surface.Cancel(); metronome.Stop(); view.ShowPause(true);
        }

        private void Continue()
        {
            if (!IsPaused || completed) return;
            view.ShowPause(false);
            if (heldAtPause) WaitingForContact = true;
            else { Round.Resume(false); IsPaused = false; RestartClock(); }
        }

        public void SetBeatSound(bool enabled)
        { if (metronome != null) { metronome.SetMuted(!enabled); view.SetSound(enabled); } }
        private void ToggleSound() { SetBeatSound(metronome.Muted); }
        private void RestartClock(bool firstStart = false)
        {
            offset = Round.ElapsedSeconds; origin = AudioSettings.dspTime + .12;
            metronome.Restart(offset, firstStart);
        }

        private void Finish()
        { if (completed) return; completed = true; metronome.Stop(); surface.Cancel(); onFinished?.Invoke(Round); }
        private void Leave()
        { if (completed) return; completed = true; metronome.Stop(); surface.Cancel(); onLeave?.Invoke(); }
        private void OnApplicationPause(bool paused) { if (paused) Pause(); }
        private void OnApplicationFocus(bool focused) { if (!focused) Pause(); }
        private void OnDisable() { metronome?.Stop(); if (surface != null) surface.Cancel(); }
        private void OnDestroy() { metronome?.Dispose(); }
    }
}
