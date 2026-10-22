using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SeedeXR.Kionjo
{
    /// <summary>
    /// Records any set of Transforms (head camera, hands, props) plus Kionjo events into a take
    /// folder. Works with any rig: assign the transforms your XR rig (or your flat camera) moves.
    /// Samples in LateUpdate at a late execution order so rigs have already written their poses.
    /// </summary>
    [AddComponentMenu("SeedeXR/Kionjo Recorder")]
    [DefaultExecutionOrder(32000)]
    public class KionjoRecorder : MonoBehaviour
    {
        [Tooltip("Transforms recorded every frame. Name the head track \"Head\" so renders follow it.")]
        public List<KionjoTrack> tracks = new();
        [Tooltip("A take stops by itself after this many seconds and marks the event \"cap\".")]
        public float maxTakeSeconds = 120f;

        [NonSerialized] public string PendingTakeFolder;
        [NonSerialized] public string PendingShotId, PendingShotCode;
        [NonSerialized] public int PendingTakeNumber;

        public bool IsRecording => _writer != null;
        public float Elapsed => IsRecording ? Time.time - _start : _lastElapsed;
        public int FrameCount => _writer?.FrameCount ?? _lastFrames;
        public string CurrentTakeFolder { get; private set; }

        public event Action<string> TakeStarted, TakeFinished;

        public static KionjoRecorder Active { get; private set; }

        KionjoTakeWriter _writer;
        KionjoTakeMeta _meta;
        float _start, _lastElapsed;
        int _lastFrames;
        KionjoPoseSample[] _samples;

        void OnEnable() => Active = this;

        void OnDisable()
        {
            StopTake();
            if (Active == this) Active = null;
        }

        void OnDestroy() => StopTake();

        public void ToggleTake()
        {
            if (IsRecording)
            {
                StopTake();
                return;
            }
            if (string.IsNullOrEmpty(PendingTakeFolder))
            {
                Debug.LogWarning("Kionjo: no take is pending. Select a shot in the Kionjo window (Shoot tab) before recording.");
                return;
            }
            var folder = PendingTakeFolder;
            PendingTakeFolder = null;
            StartTake(folder, PendingShotId, PendingShotCode, PendingTakeNumber);
        }

        public void StartTake(string takeFolder, string shotId, string shotCode, int takeNumber)
        {
            if (IsRecording) StopTake();
            _meta = new KionjoTakeMeta
            {
                shotId = shotId,
                shotCode = shotCode,
                takeNumber = takeNumber,
                startedAt = DateTime.UtcNow.ToString("o"),
            };
            foreach (var t in tracks)
                _meta.tracks.Add(new KionjoTrackInfo { name = t.name, path = PathOf(t.target), recordScale = t.recordScale });
            _samples = new KionjoPoseSample[tracks.Count];
            _writer = new KionjoTakeWriter(takeFolder, _meta);
            CurrentTakeFolder = takeFolder;
            _start = Time.time;
            KionjoEvents.Marked += OnMarked;

            foreach (var hook in Hooks()) hook.OnKionjoRecordingStarted(takeFolder);
            KionjoCameraSource.Find()?.onRecordingStarted.Invoke();
            TakeStarted?.Invoke(takeFolder);
        }

        public void StopTake()
        {
            if (!IsRecording) return;
            KionjoEvents.Marked -= OnMarked;
            _lastElapsed = Time.time - _start;
            _lastFrames = _writer.FrameCount;
            _writer.Close(_lastElapsed);
            _writer = null;
            var folder = CurrentTakeFolder;

            foreach (var hook in Hooks()) hook.OnKionjoRecordingStopped(folder);
            var source = KionjoCameraSource.Find();
            if (source != null) source.onRecordingStopped.Invoke();
            TakeFinished?.Invoke(folder);
        }

        void LateUpdate()
        {
            if (!IsRecording) return;
            float t = Time.time - _start;
            for (int i = 0; i < tracks.Count; i++)
            {
                var target = tracks[i].target;
                _samples[i] = target == null || !target.gameObject.activeInHierarchy
                    ? new KionjoPoseSample { active = false, rotation = Quaternion.identity, scale = Vector3.one }
                    : new KionjoPoseSample { active = true, position = target.position, rotation = target.rotation, scale = target.localScale };
            }
            _writer.WriteFrame(t, _samples);
            if (t >= maxTakeSeconds)
            {
                _writer.AddEvent(t, "cap", null);
                StopTake();
            }
        }

        void OnMarked(string name, string payload)
        {
            if (IsRecording) _writer.AddEvent(Time.time - _start, name, payload);
        }

        static IEnumerable<IKionjoRecordingHook> Hooks()
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (mb is IKionjoRecordingHook hook) yield return hook;
        }

        /// <summary>"/Root/Child" for a scene transform, empty for null.</summary>
        public static string PathOf(Transform t)
        {
            if (t == null) return "";
            var path = "/" + t.name;
            for (var p = t.parent; p != null; p = p.parent) path = "/" + p.name + path;
            return path;
        }
    }
}
