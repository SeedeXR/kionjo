using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace SeedeXR.Kionjo
{
    /// <summary>
    /// Plays a take back onto Transforms, frame locked through Time.captureDeltaTime, and raises the
    /// recorded events. It never presses a button or opens a device: the game decides what an event
    /// means through onEvent. The head is stabilised (roll lock, short smoothing) and written again
    /// just before rendering so an XR rig that writes the camera late cannot win.
    /// </summary>
    [AddComponentMenu("SeedeXR/Kionjo Replayer")]
    [DefaultExecutionOrder(32000)]
    public class KionjoReplayer : MonoBehaviour
    {
        [Tooltip("Transforms to drive, bound to the take's tracks by name.")]
        public List<KionjoTrack> tracks = new();
        [Tooltip("Name of the head track in the take.")]
        public string headTrackName = "Head";
        [Tooltip("When set, this Transform receives the head track instead of the bound one (a render camera).")]
        public Transform headOverride;
        [Tooltip("Centred smoothing window for the head, in seconds. 0 = raw.")]
        public float smoothingSeconds = 0.1f;
        [Tooltip("Rebuild the head rotation from yaw and pitch only, removing tilt.")]
        public bool rollLock = true;
        [Tooltip("Advance time by exactly 1 / fps per frame, whatever the real frame time.")]
        public bool lockFrameRate = true;
        public float fps = 60f;
        public UnityEvent<string, string> onEvent = new();

        public event Action<string, string> EventFired;
        public event Action Finished;

        public KionjoTakeData Take { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsDone { get; private set; }
        public float Clock { get; private set; }
        public int Frame { get; private set; }

        Transform[] _targets = Array.Empty<Transform>();
        int _headIndex = -1;
        KionjoPoseSample[] _head;
        int _nextEvent;
        float _from, _to;
        float _savedCaptureDelta;
        bool _ownsCaptureDelta;

        public void Load(string takeFolder) => Load(KionjoTakeData.Load(takeFolder));

        public void Load(KionjoTakeData take)
        {
            Stop();
            Take = take;
            IsDone = false;
        }

        public void Play(float from = 0f, float to = -1f)
        {
            if (Take == null) throw new InvalidOperationException("Kionjo: load a take before playing it.");
            Stop();
            Bind();
            _savedCaptureDelta = Time.captureDeltaTime;
            _ownsCaptureDelta = lockFrameRate && fps > 0f;
            if (_ownsCaptureDelta) Time.captureDeltaTime = 1f / fps;
            Clock = _from = Mathf.Max(0f, from);
            _to = to < 0f ? Take.Duration : to;
            Frame = 0;
            IsDone = false;
            IsPlaying = true;
            _nextEvent = 0;
            // Events before the start already happened: skip them rather than fire a burst.
            var events = Take.Meta?.events;
            if (events != null)
                while (_nextEvent < events.Count && events[_nextEvent].t < Clock) _nextEvent++;
            Application.onBeforeRender += OnBeforeRender;
        }

        public void Stop()
        {
            if (!IsPlaying) return;
            IsPlaying = false;
            Application.onBeforeRender -= OnBeforeRender;
            if (_ownsCaptureDelta) Time.captureDeltaTime = _savedCaptureDelta;
            _ownsCaptureDelta = false;
        }

        void OnDisable() => Stop();
        void OnDestroy() => Stop();

        void Bind()
        {
            int n = Take.Meta?.tracks.Count ?? 0;
            _targets = new Transform[n];
            _headIndex = Take.TrackIndex(headTrackName);
            for (int i = 0; i < n; i++)
            {
                var trackName = Take.Meta.tracks[i].name;
                foreach (var t in tracks)
                    if (t.name == trackName) _targets[i] = t.target;
            }
            if (_headIndex >= 0 && headOverride != null) _targets[_headIndex] = headOverride;
            _head = _headIndex >= 0
                ? KionjoReplayMath.Stabilize(Take.Times, Take.Tracks[_headIndex], smoothingSeconds, rollLock)
                : null;
        }

        void LateUpdate()
        {
            if (!IsPlaying) return;
            Apply();
            FireEvents();
            if (Clock >= _to || Take.Times.Length == 0)
            {
                IsDone = true;
                Stop();
                Finished?.Invoke();
                return;
            }
            Frame++;
            // Locked: computed, not accumulated, so frame k is exactly from + k / fps (and the frame
            // Play was called in, which Unity still timed in real time, does not drift the clock).
            Clock = _ownsCaptureDelta ? _from + Frame / fps : Clock + Time.deltaTime;
        }

        void OnBeforeRender()
        {
            if (IsPlaying) ApplyHead();
        }

        void Apply()
        {
            for (int i = 0; i < _targets.Length; i++)
            {
                if (_targets[i] == null) continue;
                if (i == _headIndex)
                {
                    ApplyHead();
                    continue;
                }
                Write(_targets[i], KionjoReplayMath.Sample(Take.Times, Take.Tracks[i], Clock));
            }
        }

        void ApplyHead()
        {
            if (_headIndex < 0 || _targets[_headIndex] == null) return;
            Write(_targets[_headIndex], KionjoReplayMath.Sample(Take.Times, _head, Clock));
        }

        static void Write(Transform t, in KionjoPoseSample s)
        {
            if (!s.active) return;
            t.SetPositionAndRotation(s.position, s.rotation);
            if (s.hasScale) t.localScale = s.scale;
        }

        void FireEvents()
        {
            var events = Take.Meta?.events;
            if (events == null) return;
            while (_nextEvent < events.Count && events[_nextEvent].t <= Clock)
            {
                var e = events[_nextEvent++];
                onEvent?.Invoke(e.name, e.payload);
                EventFired?.Invoke(e.name, e.payload);
            }
        }
    }
}
