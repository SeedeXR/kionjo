#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SeedeXR.Kionjo
{
    /// <summary>What a multi-angle capture produced.</summary>
    [Serializable]
    public class KionjoMultiCaptureResult
    {
        public bool ok;
        public string error;
        public List<string> videos = new();
        public List<string> stills = new();
        public string audioWav;
        public int frames;
        public List<string> warnings = new();
        /// <summary>Closest any non-main rig came to the subject's centre, in subject sizes (must stay above 1).</summary>
        public float minDistanceOverSize = float.MaxValue;
    }

    /// <summary>
    /// Films one Play mode run from several angles at once: one camera per rig, all rendering the same
    /// simulated frame (the first capture locks the clock to 1 / fps), each streamed into its own ffmpeg.
    /// Rigs follow the subject (centre smoothed over about 0.5 s, size = the largest seen so far, so
    /// cameras never close in). The scene's sound is rendered in step and muxed into every video.
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(-100)]
    public class KionjoMultiCapture : MonoBehaviour
    {
        public Transform subject;
        public Camera main;
        public string[] rigs;
        public float seconds = 10f, fps = 30f, stillsEvery = 0.5f;
        public string outFolder, baseName, ffmpeg;
        public string[] encoderArgs;
        public event Action<KionjoMultiCaptureResult> Done;

        readonly List<(string rig, Camera cam, KionjoFrameCapture capture)> _rigs = new();
        readonly KionjoMultiCaptureResult _result = new();
        KionjoAudioCapture _audio;
        Vector3 _centre;
        float _size;
        int _frame, _finished, _stillEvery;
        bool _running, _first;
        readonly Dictionary<string, Pose> _fixed = new();

        public void Begin()
        {
            if (_running) return;
            if (main == null) throw new InvalidOperationException("Kionjo: no main camera to capture from.");
            if (subject == null) throw new InvalidOperationException("Kionjo: no subject to capture.");
            Directory.CreateDirectory(outFolder);
            int frames = Mathf.CeilToInt(seconds * fps);
            _stillEvery = stillsEvery > 0f ? Mathf.Max(1, Mathf.RoundToInt(stillsEvery * fps)) : 0;
            _first = true;
            Track(0f);
            foreach (var rig in rigs.Distinct())
            {
                var (w, h, vfov) = KionjoRigs.Format(rig, main.fieldOfView);
                var go = new GameObject($"Kionjo rig {rig}");
                go.transform.SetParent(transform, false);
                var cam = go.AddComponent<Camera>();
                cam.CopyFrom(main);
                cam.fieldOfView = vfov;
                cam.enabled = false;
                var capture = go.AddComponent<KionjoFrameCapture>();
                capture.camera = cam;
                capture.width = w;
                capture.height = h;
                capture.fps = fps;
                capture.targetFrames = frames;
                capture.lockFrameRate = _rigs.Count == 0;
                var video = Path.Combine(outFolder, $"{baseName}-{rig}.mp4");
                capture.sink = new FfmpegPipeSink(ffmpeg, video, encoderArgs);
                capture.Finished += () => OnRigFinished(rig, capture, video);
                _rigs.Add((rig, cam, capture));
            }
            Place(0f);
            foreach (var r in _rigs) r.capture.Begin();
            _audio = new KionjoAudioCapture();
            if (!_audio.Start(out var why)) _result.warnings.Add(why);
            _running = true;
        }

        void LateUpdate()
        {
            if (!_running) return;
            float dt = fps > 0f ? 1f / fps : Time.deltaTime;
            Track(dt);
            Place(Mathf.Clamp01(_frame / Mathf.Max(1f, seconds * fps - 1f)));
            if (_stillEvery > 0 && _frame % _stillEvery == 0)
            {
                var t = (_frame / fps).ToString("000.0", CultureInfo.InvariantCulture);
                foreach (var (rig, cam, capture) in _rigs)
                {
                    var png = Path.Combine(outFolder, "stills", $"{baseName}-{rig}-t{t}s.png");
                    KionjoStill.CapturePng(cam, png, capture.width, capture.height);
                    _result.stills.Add(png);
                }
            }
            _audio?.Frame(fps);
            _frame++;
        }

        /// <summary>Smoothed centre and the largest extent seen, from every renderer under the subject.</summary>
        void Track(float dt)
        {
            var renderers = subject.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            Vector3 target = subject.position;
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                target = b.center;
                _size = Mathf.Max(_size, b.extents.magnitude);
            }
            _size = Mathf.Max(_size, 0.1f);
            if (_first) _centre = target;
            else _centre += (target - _centre) * (1f - Mathf.Exp(-dt / 0.5f));
        }

        void Place(float u)
        {
            foreach (var (rig, cam, _) in _rigs)
            {
                Pose p;
                if ((rig == "wide" || rig == "side") && _fixed.TryGetValue(rig, out var held))
                    p = new Pose(held.position, Quaternion.LookRotation(_centre - held.position, Vector3.up));
                else
                {
                    p = KionjoRigs.Place(rig, _centre, _size, main.transform.position, main.transform.rotation, u);
                    if (rig == "wide" || rig == "side") _fixed[rig] = p;
                }
                cam.transform.SetPositionAndRotation(p.position, p.rotation);
                if (!rig.StartsWith("main"))
                    _result.minDistanceOverSize = Mathf.Min(_result.minDistanceOverSize, Vector3.Distance(p.position, _centre) / _size);
            }
            _first = false;
        }

        void OnRigFinished(string rig, KionjoFrameCapture capture, string video)
        {
            if (capture.Error != null) _result.error = (_result.error == null ? "" : _result.error + "\n") + $"{rig}: {capture.Error}";
            else _result.videos.Add(video);
            _result.frames = _result.frames == 0 ? capture.FramesCaptured : Mathf.Min(_result.frames, capture.FramesCaptured);
            if (++_finished < _rigs.Count) return;
            _running = false;
            string why = null;
            if (_audio != null) _result.audioWav = _audio.Stop(Path.Combine(outFolder, baseName + ".wav"), out why);
            if (_result.audioWav == null && why != null) _result.warnings.Add(why);
            if (_result.audioWav != null)
            {
                foreach (var v in _result.videos)
                    if (!Mux(v, _result.audioWav, out var err)) _result.warnings.Add($"{Path.GetFileName(v)} kept silent: {err}");
                File.Delete(_result.audioWav);
                _result.audioWav = null;
            }
            _result.ok = _result.error == null && _result.videos.Count == _rigs.Count;
            foreach (var r in _rigs)
                if (r.cam != null) Destroy(r.cam.gameObject);
            Done?.Invoke(_result);
        }

        /// <summary>Puts the WAV beside the picture (video copied, sound AAC) and replaces the silent file.</summary>
        bool Mux(string video, string wav, out string error)
        {
            error = null;
            var tmp = Path.ChangeExtension(video, ".sound.mp4");
            var psi = new ProcessStartInfo(ffmpeg, $"-v error -y -i \"{video}\" -i \"{wav}\" -map 0:v -map 1:a -c:v copy -c:a aac -b:a 160k -shortest -movflags +faststart \"{tmp}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            var err = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0 || !File.Exists(tmp))
            {
                error = err.Trim();
                if (File.Exists(tmp)) File.Delete(tmp);
                return false;
            }
            File.Delete(video);
            File.Move(tmp, video);
            return true;
        }

        void OnDestroy()
        {
            if (!_running) return;
            _running = false;
            _audio?.Stop(null, out _);
            foreach (var r in _rigs)
                if (r.capture != null) r.capture.Abort();
        }
    }
}
#endif
