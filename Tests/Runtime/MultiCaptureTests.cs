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
    }
}
