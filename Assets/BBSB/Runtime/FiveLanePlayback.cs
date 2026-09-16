using System;
using BBSB.Core;
using BBSB.Runtime.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BBSB.Runtime
{
    public sealed class FiveLanePlayback : MonoBehaviour
    {
        public FiveLaneBattle Battle { get; private set; }
        public bool IsInitialized { get; private set; }
        public bool WaitingForHold { get; private set; }
        public bool CanReceiveInput => IsInitialized && isActiveAndEnabled && !Battle.Finished &&
            (WaitingForHold || (!Battle.IsPaused && AudioSettings.dspTime >= origin));
        private readonly bool[] pointerHeld = new bool[5], sentHeld = new bool[5];
        private readonly AudioSource[] pulses = new AudioSource[8];
        private AudioClip tick, accent;
        private int nextPulse, pulseSource;
        private FiveLaneBattleView view;
        private StageMusicPlayer music;
        private Action onFinish, onLeave;
        private double origin, offsetBeat;
        private bool resultShown;
        internal void Bind(FiveLaneBattle battle, RunSession session, RunUI ui, Action finished, Action leave)
        {
            if (Battle != null) throw new InvalidOperationException("Five-lane playback is already bound.");
            Battle = battle ?? throw new ArgumentNullException(nameof(battle));
            Battle.Pause(); onFinish = finished; onLeave = leave;
            string stage = "validating the encounter";
            try
            {
                if (session?.BattleMusic == null || session.BattlePlan == null || ui == null)
                    throw new InvalidOperationException("Five-lane playback requires a battle plan, music and UI.");
                stage = "building the battle HUD and actors";
                view = new FiveLaneBattleView((RectTransform)transform, ui, session, this);
                view.Refresh(0, false);
                stage = "preparing the audio clock";
                music = new StageMusicPlayer(transform, session.BattleMusic.Music);
                tick = MakeTick(740); accent = MakeTick(1100);
                for (int i = 0; i < pulses.Length; i++)
                {
                    var child = new GameObject("Beat " + i); child.transform.SetParent(transform, false);
                    pulses[i] = child.AddComponent<AudioSource>(); pulses[i].playOnAwake = false;
                    pulses[i].volume = music.HasRecording ? .12f : .28f;
                }
                // Publish readiness only after the entire view and audio setup succeeds.
                // A constructor exception must not leave LateUpdate running a partial battle.
                bool holds = false; foreach (var lane in Battle.Lanes) holds |= lane.Holding;
                if (holds) WaitingForHold = true;
                else { Battle.Resume(); ReleaseStaleContacts(); RestartClock(Battle.Beat == 0); }
                IsInitialized = true;
                ClearSelection();
            }
            catch (Exception error)
            {
                Battle.Pause(); WaitingForHold = false; StopAudio();
                view = null; IsInitialized = false; enabled = false;
                throw new InvalidOperationException("Five-lane playback failed while " + stage + ".", error);
            }
        }
        private double Now => Math.Max(Battle.Beat, offsetBeat + Math.Max(0, AudioSettings.dspTime - origin) * Battle.Bpm / 60);
        private bool KeyHeld(int slot)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            switch (slot)
            {
                case 0: return keyboard.dKey.isPressed;
                case 1: return keyboard.fKey.isPressed;
                case 2: return keyboard.spaceKey.isPressed;
                case 3: return keyboard.jKey.isPressed;
                default: return keyboard.kKey.isPressed;
            }
        }
        private void Update()
        {
            if (!IsInitialized || resultShown) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            { if (Battle.IsPaused && !WaitingForHold) Continue(); else Pause(); return; }
            if (WaitingForHold) { TryResumeHeld(); return; }
            if (!CanReceiveInput) return;
            for (int i = 0; i < 5; i++) SyncInput(i);
        }
        private void LateUpdate()
        {
            if (!IsInitialized || resultShown) return;
            if (!Battle.IsPaused)
            {
                SchedulePulses();
                if (AudioSettings.dspTime >= origin) Battle.Advance(Now);
            }
            int count = Battle.IsPaused ? 0 : (int)Math.Ceiling(Math.Max(0, origin - AudioSettings.dspTime) * Battle.Bpm / 60);
            view.Refresh(count, WaitingForHold);
            if (Battle.Finished)
            { resultShown = true; StopAudio(); view.ShowResult(onFinish); ClearSelection(); }
        }
        public void SetPointer(int slot, bool down)
        {
            if (slot < 0 || slot >= 5 || !IsInitialized || !isActiveAndEnabled) return;
            pointerHeld[slot] = down;
            if (WaitingForHold) TryResumeHeld(); else if (CanReceiveInput) SyncInput(slot);
        }
        private void SyncInput(int slot)
        {
            bool held = KeyHeld(slot) || pointerHeld[slot];
            if (held == sentHeld[slot]) return;
            sentHeld[slot] = held;
            if (held) Battle.Press(slot, Now); else Battle.Release(slot, Now);
        }
        public void Pause()
        {
            if (!IsInitialized || Battle.Finished || resultShown) return;
            if (!Battle.IsPaused && AudioSettings.dspTime >= origin) Battle.Advance(Now);
            if (Battle.Finished) return;
            Battle.Pause(); WaitingForHold = false; StopAudio(); CancelPointers();
            view.ShowPause(Continue, Leave); ClearSelection();
        }
        public void Continue()
        {
            if (!IsInitialized || Battle.Finished || !Battle.IsPaused) return;
            view.HideModal(); ClearSelection();
            foreach (var lane in Battle.Lanes)
                if (lane.Holding) { WaitingForHold = true; return; }
            Battle.Resume(); ReleaseStaleContacts(); RestartClock(false);
        }
        private void TryResumeHeld()
        {
            for (int i = 0; i < 5; i++) if (Battle.Lanes[i].Holding && !KeyHeld(i) && !pointerHeld[i]) return;
            WaitingForHold = false; Battle.Resume(); ReleaseStaleContacts(); RestartClock(false); ClearSelection();
        }
        private void ReleaseStaleContacts()
        {
            for (int i = 0; i < 5; i++)
            {
                sentHeld[i] = Battle.Lanes[i].Holding;
                if (!sentHeld[i]) Battle.Release(i, Battle.Beat);
            }
        }
        private void Leave()
        {
            Battle.Pause(); StopAudio(); CancelPointers(); onLeave?.Invoke();
        }
        private void CancelPointers()
        {
            Array.Clear(pointerHeld, 0, pointerHeld.Length);
            foreach (var surface in GetComponentsInChildren<FiveLaneInputSurface>()) surface.Cancel();
        }
        private void RestartClock(bool countIn)
        {
            offsetBeat = Battle.Beat; origin = AudioSettings.dspTime + (countIn ? 4 * 60 / Battle.Bpm : .12);
            nextPulse = countIn ? -4 : (int)Math.Ceiling(offsetBeat);
            music.Start(origin, offsetBeat * 60 / Battle.Bpm, true);
        }
        private void SchedulePulses()
        {
            double now = AudioSettings.dspTime;
            while (origin + (nextPulse - offsetBeat) * 60 / Battle.Bpm <= now + .12)
            {
                double at = origin + (nextPulse - offsetBeat) * 60 / Battle.Bpm;
                if (at >= now)
                {
                    var source = pulses[pulseSource++ % pulses.Length]; source.clip = nextPulse % 4 == 0 ? accent : tick;
                    source.PlayScheduled(at);
                }
                nextPulse++;
            }
        }
        private static AudioClip MakeTick(float frequency)
        {
            const int rate = 22050; var data = new float[1323];
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / rate) * Mathf.Exp(-i / 200f);
            var clip = AudioClip.Create("Phrase beat", data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        private void StopAudio() { music?.Stop(); foreach (var source in pulses) if (source != null) source.Stop(); }
        private static void ClearSelection() { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); }
        private void OnApplicationFocus(bool focus) { if (!focus) Pause(); }
        private void OnApplicationPause(bool paused) { if (paused) Pause(); }
        private void OnDisable() { if (Battle != null && !Battle.Finished) Battle.Pause(); StopAudio(); }
        private void OnDestroy() { if (tick != null) Destroy(tick); if (accent != null) Destroy(accent); }
    }
}
