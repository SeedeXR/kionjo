using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SeedeXR.Kionjo.Tests
{
    public class MultiCaptureTests
    {
        string _dir;
        GameObject _root;

        class Spin : MonoBehaviour
        {
            void Update() => transform.Rotate(0f, 90f * Time.deltaTime, 0f);
        }

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name, "out (x)");
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            Time.captureDeltaTime = 0f;
            try
            {
                var top = Path.GetDirectoryName(_dir);
                if (Directory.Exists(top)) Directory.Delete(top, true);
            }
            catch (IOException)
            {
            }
        }

        [UnityTest]
        public IEnumerator CapturesFrameLockedRigs()
        {
            var ffmpeg = FrameCaptureTests.FindExe("ffmpeg");
            var ffprobe = FrameCaptureTests.FindExe("ffprobe");
            if (ffmpeg == null || ffprobe == null) Assert.Ignore("ffmpeg is not on PATH");

            _root = new GameObject("Capture Scene");
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.transform.SetParent(_root.transform);
            cam.transform.position = new Vector3(0f, 1f, -5f);
            cam.transform.LookAt(new Vector3(0f, 1f, 0f));
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Hero";
            cube.transform.SetParent(_root.transform);
            cube.transform.position = new Vector3(0f, 1f, 0f);
            cube.AddComponent<Spin>();
            var clip = AudioClip.Create("sine", 48000 * 2, 1, 48000, false);
            var data = new float[48000 * 2];
            for (int i = 0; i < data.Length; i++) data[i] = 0.3f * Mathf.Sin(2f * Mathf.PI * 440f * i / 48000f);
            clip.SetData(data, 0);
            var source = cube.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.Play();

            var capture = new GameObject("Capture").AddComponent<KionjoMultiCapture>();
            capture.transform.SetParent(_root.transform);
            capture.subject = cube.transform;
            capture.main = cam;
            capture.rigs = new[] { "main", "orbit", "close" };
            capture.seconds = 1f;
            capture.fps = 30f;
            capture.stillsEvery = 0.5f;
            capture.outFolder = _dir;
            capture.baseName = "hero-v1";
            capture.ffmpeg = ffmpeg;
            capture.encoderArgs = new[] { "-c:v", "libx264", "-crf", "16", "-preset", "veryfast" };
            KionjoMultiCaptureResult result = null;
            capture.Done += r => result = r;
            capture.Begin();
            for (int i = 0; i < 600 && result == null; i++) yield return null;

            Assert.IsNotNull(result, "capture did not finish");
            Assert.IsTrue(result.ok, result.error);
            Assert.AreEqual(3, result.videos.Count);
            foreach (var rig in capture.rigs)
            {
                var video = Path.Combine(_dir, $"hero-v1-{rig}.mp4");
                Assert.IsTrue(File.Exists(video), video);
                var frames = FrameCaptureTests.RunTool(ffprobe, $"-v error -select_streams v:0 -count_frames -show_entries stream=nb_read_frames -of csv=p=0 \"{video}\"", out _).Trim();
                Assert.AreEqual("30", frames, rig);
                var streams = FrameCaptureTests.RunTool(ffprobe, $"-v error -show_entries stream=codec_type -of csv=p=0 \"{video}\"", out _);
                StringAssert.Contains("audio", streams, $"{rig}: {string.Join("; ", result.warnings)}");
            }
            Assert.AreEqual(6, Directory.GetFiles(Path.Combine(_dir, "stills"), "*.png").Length);
            Assert.AreEqual(6, result.stills.Count);
            Assert.Greater(result.minDistanceOverSize, 1f, "a rig camera stood inside the subject");
            Assert.IsFalse(Directory.GetFiles(_dir).Any(f => f.EndsWith(".partial.mp4")));
        }

        class SteppedScene : MonoBehaviour, IKionjoCaptureDriver
        {
            public int begun, steps, asked;
            public void CaptureBegin(float fps) => begun++;
            public void CaptureStep(float dt) => steps++;
            public bool TryGetFocus(out Vector3 centre, out float radius)
            {
                asked++;
                centre = new Vector3(0f, 1f, 0f);
                radius = 0f;
                return true;
            }
        }

        /// <summary>A scene that steps its own simulation is started once and stepped once per captured frame.</summary>
        [UnityTest]
        public IEnumerator DriverIsSteppedEveryFrame()
        {
            var ffmpeg = FrameCaptureTests.FindExe("ffmpeg");
            if (ffmpeg == null) Assert.Ignore("ffmpeg is not on PATH");
            _root = new GameObject("Capture Scene");
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.transform.SetParent(_root.transform);
            cam.transform.position = new Vector3(0f, 1f, -5f);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(_root.transform);
            var driver = _root.AddComponent<SteppedScene>();
            var capture = new GameObject("Capture").AddComponent<KionjoMultiCapture>();
            capture.subject = cube.transform;
            capture.main = cam;
            capture.rigs = new[] { "main" };
            capture.seconds = 0.2f;
            capture.fps = 30f;
            capture.stillsEvery = 0f;
            capture.outFolder = _dir;
            capture.baseName = "stepped";
            capture.ffmpeg = ffmpeg;
            capture.encoderArgs = new[] { "-c:v", "libx264", "-preset", "ultrafast" };
            KionjoMultiCaptureResult result = null;
            capture.Done += r => result = r;
            capture.Begin();
            for (int i = 0; i < 300 && result == null; i++) yield return null;

            Assert.IsNotNull(result, "capture did not finish");
            Assert.IsTrue(result.ok, result.error);
            Assert.AreEqual(1, driver.begun);
            Assert.GreaterOrEqual(driver.steps, result.frames, "a captured frame was not stepped");
            Assert.LessOrEqual(driver.steps, result.frames + 1);
            Assert.Greater(driver.asked, 0, "the driver's focus was never asked");
        }

        /// <summary>A capture whose object goes away (a scene load) must still report, or Play mode and the lock never end.</summary>
        [UnityTest]
        public IEnumerator DestroyedCaptureStillReportsDone()
        {
            var ffmpeg = FrameCaptureTests.FindExe("ffmpeg");
            if (ffmpeg == null) Assert.Ignore("ffmpeg is not on PATH");
            _root = new GameObject("Capture Scene");
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.transform.SetParent(_root.transform);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(_root.transform);
            var capture = new GameObject("Capture").AddComponent<KionjoMultiCapture>();
            capture.subject = cube.transform;
            capture.main = cam;
            capture.rigs = new[] { "main", "close" };
            capture.seconds = 5f;
            capture.fps = 30f;
            capture.stillsEvery = 0f;
            capture.outFolder = _dir;
            capture.baseName = "gone";
            capture.ffmpeg = ffmpeg;
            capture.encoderArgs = new[] { "-c:v", "libx264", "-preset", "ultrafast" };
            KionjoMultiCaptureResult result = null;
            capture.Done += r => result = r;
            capture.Begin();
            yield return null;
            yield return null;
            Object.Destroy(capture.gameObject);
            for (int i = 0; i < 60 && result == null; i++) yield return null;

            Assert.IsNotNull(result, "Done never fired");
            Assert.IsFalse(result.ok);
            StringAssert.Contains("stopped", result.error);
        }
    }
}
