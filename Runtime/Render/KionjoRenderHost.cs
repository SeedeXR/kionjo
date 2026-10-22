#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SeedeXR.Kionjo
{
    /// <summary>Everything a render needs, with no editor types, so the host runs in any Play mode scene.</summary>
    [Serializable]
    public class KionjoRenderRequest
    {
        public string shotId, shotCode;
        [Tooltip("Take to replay. Empty renders the camera as it moves on its own for liveSeconds.")]
        public string takeFolder;
        public float from;
        [Tooltip("Negative = the end of the take.")]
        public float to = -1f;
        public float liveSeconds = 4f;
        public int width = 1280, height = 720;
        public float fps = 30f;
        public string[] encoderArgs;
        public string ffmpegExe;
        public string outputMp4;
        public float horizontalFov;
        public List<string> hidePaths = new(), showPaths = new();
        public string stageCommand;
        public string headTrackName = "Head";
        public float smoothingSeconds = 0.1f;
        public bool rollLock = true;
    }

    /// <summary>
    /// Renders one shot in Play mode: stages the scene, replays the take onto the recorded
    /// transforms (bound through the scene's Kionjo Recorder), and captures the camera frame by
    /// frame into ffmpeg. Created by the editor's render job; destroys nothing it did not create.
    /// </summary>
    [AddComponentMenu("")]
    public class KionjoRenderHost : MonoBehaviour
    {
        public KionjoRenderRequest Request { get; private set; }
        public bool Done { get; private set; }
        public string Error { get; private set; }
        public bool Failed => Error != null;
        public int Frames => _capture != null ? _capture.FramesCaptured : 0;
        public int TargetFrames { get; private set; }
        public float Progress => TargetFrames > 0 ? Mathf.Clamp01((float)Frames / TargetFrames) : 0f;
        public List<string> Warnings { get; } = new();

        KionjoFrameCapture _capture;
        KionjoReplayer _replayer;
        bool _ownsReplayer, _aborted;

        public static KionjoRenderHost Create(KionjoRenderRequest request)
        {
            var go = new GameObject("Kionjo Render Host") { hideFlags = HideFlags.DontSave };
            var host = go.AddComponent<KionjoRenderHost>();
            host.Request = request;
            host.StartCoroutine(host.Run());
            return host;
        }

        public void Abort()
        {
            if (Done) return;
            _aborted = true;
            if (_capture != null) _capture.Abort();
            Finish("Cancelled");
        }

        IEnumerator Run()
        {
            var r = Request;
            var missing = KionjoStaging.Apply(r.hidePaths, r.showPaths);
            foreach (var m in missing) Warnings.Add($"Not found in the scene: {m}");
            yield return KionjoStaging.RunHandlers(r.shotId, r.stageCommand);
            yield return null;
            yield return null;
            if (_aborted) yield break;

            var camera = KionjoCameraSource.Resolve();
            if (camera == null)
            {
                Finish("No camera: add a Kionjo Camera Source or tag a camera MainCamera.");
                yield break;
            }

            float seconds;
            if (!string.IsNullOrEmpty(r.takeFolder))
            {
                KionjoTakeData take;
                try
                {
                    take = KionjoTakeData.Load(r.takeFolder);
                }
                catch (Exception e)
                {
                    Finish($"Could not read the take: {e.Message}");
                    yield break;
                }
                _replayer = BindReplayer(camera, take);
                _replayer.fps = r.fps;
                _replayer.lockFrameRate = true;
                _replayer.smoothingSeconds = r.smoothingSeconds;
                _replayer.rollLock = r.rollLock;
                _replayer.headTrackName = r.headTrackName;
                _replayer.Load(take);
                float to = r.to < 0f ? take.Duration : Mathf.Min(r.to, take.Duration);
                seconds = Mathf.Max(0f, to - r.from);
            }
            else seconds = r.liveSeconds;

            TargetFrames = Mathf.Max(1, Mathf.CeilToInt(seconds * r.fps - 1e-3f));
            _capture = gameObject.AddComponent<KionjoFrameCapture>();
            _capture.camera = camera;
            _capture.width = r.width;
            _capture.height = r.height;
            _capture.fps = r.fps;
            _capture.targetFrames = TargetFrames;
            _capture.lockFrameRate = _replayer == null; // the replayer owns the clock when it drives the shot
            _capture.horizontalFovOverride = r.horizontalFov;
            _capture.sink = new FfmpegPipeSink(r.ffmpegExe, r.outputMp4, r.encoderArgs);

            var source = KionjoCameraSource.Find();
            if (source != null) source.onRenderStarted.Invoke();
            try
            {
                if (_replayer != null) _replayer.Play(r.from, r.to);
                _capture.Begin();
            }
            catch (Exception e)
            {
                Finish(e.Message);
                yield break;
            }
            while (_capture != null && _capture.IsCapturing && !_aborted) yield return null;
            if (source != null) source.onRenderStopped.Invoke();
            if (!_aborted) Finish(_capture != null ? _capture.Error : "The capture was removed.");
        }

        /// <summary>The scene's replayer, or one made here and bound by name to the recorder's tracks (the head to the camera when there is no recorder).</summary>
        KionjoReplayer BindReplayer(Camera camera, KionjoTakeData take)
        {
            var replayer = FindFirstObjectByType<KionjoReplayer>();
            if (replayer != null && replayer.tracks.Count > 0) return replayer;
            if (replayer == null)
            {
                replayer = gameObject.AddComponent<KionjoReplayer>();
                _ownsReplayer = true;
            }
            var recorder = FindFirstObjectByType<KionjoRecorder>();
            if (recorder != null)
                foreach (var t in recorder.tracks)
                    replayer.tracks.Add(new KionjoTrack { name = t.name, target = t.target, recordScale = t.recordScale });
            bool hasHead = replayer.tracks.Exists(t => t.name == Request.headTrackName && t.target != null);
            if (!hasHead) replayer.headOverride = camera.transform;
            if (take.TrackIndex(Request.headTrackName) < 0)
                Warnings.Add($"The take has no \"{Request.headTrackName}\" track: the camera will not move.");
            return replayer;
        }

        void Finish(string error)
        {
            if (Done) return;
            if (_replayer != null)
            {
                _replayer.Stop();
                if (_ownsReplayer) Destroy(_replayer);
            }
            Error = string.IsNullOrEmpty(error) ? null : error;
            Done = true;
        }

        void OnDestroy()
        {
            if (!Done) Abort();
        }
    }
}
#endif
