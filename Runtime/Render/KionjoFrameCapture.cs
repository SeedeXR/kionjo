using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SeedeXR.Kionjo
{
    /// <summary>
    /// Renders a camera into a RenderTexture once per frame, after the replayer has posed it
    /// (execution order 32001 against the replayer's 32000), reads the pixels back and hands them to
    /// a sink. The camera's own rendering is paused meanwhile, so each frame is drawn once. The clock
    /// can be locked to 1 / fps; leave it to the replayer when one is driving the shot.
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(32001)]
    public class KionjoFrameCapture : MonoBehaviour
    {
        public new Camera camera;
        public int width = 1280, height = 720;
        public float fps = 30f;
        [Tooltip("Stop after this many frames. 0 = until Abort or Finish.")]
        public int targetFrames;
        public bool lockFrameRate = true;
        [Tooltip("Horizontal field of view during capture. 0 keeps the camera's lens.")]
        public float horizontalFovOverride;
        public IKionjoFrameSink sink;

        public int FramesCaptured { get; private set; }
        public bool IsCapturing { get; private set; }
        public string Error { get; private set; }
        public event Action Finished;

        RenderTexture _rt;
        float _savedFov, _savedCaptureDelta;
        bool _savedEnabled, _ownsCaptureDelta;

        /// <summary>
        /// Readbacks arrive bottom row first on every graphics API Unity supports, while ffmpeg
        /// expects the top row first, so frames are flipped unless a test proves otherwise.
        /// </summary>
        public static bool FlipVertically = true;

        public void Begin()
        {
            if (IsCapturing) return;
            if (camera == null) throw new InvalidOperationException("Kionjo: no camera to capture.");
            if (sink == null) throw new InvalidOperationException("Kionjo: no frame sink.");
            _rt = KionjoRender.NewTarget(width, height);
            _rt.Create();
            _savedFov = camera.fieldOfView;
            _savedEnabled = camera.enabled;
            _savedCaptureDelta = Time.captureDeltaTime;
            if (horizontalFovOverride > 0f)
                camera.fieldOfView = KionjoReplayMath.VerticalFov(horizontalFovOverride, (float)width / height);
            _ownsCaptureDelta = lockFrameRate && fps > 0f;
            if (_ownsCaptureDelta) Time.captureDeltaTime = 1f / fps;
            camera.enabled = false;
            FramesCaptured = 0;
            Error = null;
            try
            {
                sink.Begin(width, height, fps, FlipVertically);
            }
            catch
            {
                Restore();
                throw;
            }
            IsCapturing = true;
        }

        void LateUpdate()
        {
            if (!IsCapturing) return;
            if (camera == null)
            {
                Fail("The camera was destroyed during capture.");
                return;
            }
            if (sink.Error != null)
            {
                Fail(sink.Error);
                return;
            }
            KionjoRender.RenderTo(camera, _rt);
            var request = AsyncGPUReadback.Request(_rt, 0, TextureFormat.RGBA32);
            request.WaitForCompletion(); // ponytail: synchronous readback, pipeline it if render time matters
            if (request.hasError)
            {
                Fail("GPU readback failed.");
                return;
            }
            sink.Push(request.GetData<byte>());
            FramesCaptured++;
            if (targetFrames > 0 && FramesCaptured >= targetFrames) Finish();
        }

        /// <summary>Ends the sink normally (the output is complete) and restores the camera.</summary>
        public void Finish()
        {
            if (!IsCapturing) return;
            IsCapturing = false;
            try
            {
                sink.End();
                if (sink.Error != null) Error = sink.Error;
            }
            finally
            {
                Restore();
                Finished?.Invoke();
            }
        }

        /// <summary>Stops without completing the output.</summary>
        public void Abort()
        {
            if (!IsCapturing) return;
            IsCapturing = false;
            sink?.Dispose();
            Restore();
        }

        void Fail(string error)
        {
            Error = error;
            Abort();
            Finished?.Invoke();
        }

        void Restore()
        {
            if (camera != null)
            {
                camera.fieldOfView = _savedFov;
                camera.enabled = _savedEnabled;
            }
            if (_ownsCaptureDelta) Time.captureDeltaTime = _savedCaptureDelta;
            _ownsCaptureDelta = false;
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
                _rt = null;
            }
        }

        void OnDisable() => Abort();
        void OnDestroy() => Abort();
    }
}
