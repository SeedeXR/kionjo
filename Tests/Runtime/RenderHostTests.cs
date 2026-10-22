using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SeedeXR.Kionjo.Tests
{
    public class RenderHostTests
    {
        string _dir;
        GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            foreach (var host in Object.FindObjectsByType<KionjoRenderHost>(FindObjectsSortMode.None)) Object.DestroyImmediate(host.gameObject);
            Time.captureDeltaTime = 0f;
            try
            {
                if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            }
            catch (IOException)
            {
            }
        }

        [Test]
        public void Staging_Resolve_FindsInactiveChild()
        {
            _root = new GameObject("StageRoot");
            var child = new GameObject("Panel");
            child.transform.SetParent(_root.transform);
            var leaf = new GameObject("Leaf");
            leaf.transform.SetParent(child.transform);
            child.SetActive(false);
            Assert.AreSame(leaf, KionjoStaging.Resolve("/StageRoot/Panel/Leaf"));
            Assert.AreEqual("/StageRoot/Panel/Leaf", KionjoStaging.PathOf(leaf.transform));
            Assert.IsNull(KionjoStaging.Resolve("/StageRoot/Nope"));
        }

        [Test]
        public void Staging_Apply_HidesShowsAndReportsMissing()
        {
            _root = new GameObject("StageRoot");
            var a = new GameObject("A");
            a.transform.SetParent(_root.transform);
            var b = new GameObject("B");
            b.transform.SetParent(_root.transform);
            b.SetActive(false);
            var missing = KionjoStaging.Apply(new[] { "/StageRoot/A", "/StageRoot/Ghost" }, new[] { "/StageRoot/B" });
            Assert.IsFalse(a.activeSelf);
            Assert.IsTrue(b.activeSelf);
            CollectionAssert.AreEqual(new[] { "/StageRoot/Ghost" }, missing);
        }

        [UnityTest]
        public IEnumerator RenderHost_RendersRecordedTake()
        {
            var ffmpeg = FrameCaptureTests.FindExe("ffmpeg");
            if (ffmpeg == null) Assert.Ignore("ffmpeg is not installed on this machine");
            _root = new GameObject("Rig");
            var cam = new GameObject("Head").AddComponent<Camera>();
            cam.transform.SetParent(_root.transform);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.gray;
            cam.tag = "MainCamera";
            var recorder = _root.AddComponent<KionjoRecorder>();
            recorder.tracks.Add(new KionjoTrack { name = "Head", target = cam.transform });

            var take = Path.Combine(_dir, "takes", "S01", "S01_take01");
            recorder.StartTake(take, "id", "S01", 1);
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 1.2f)
            {
                float t = Time.realtimeSinceStartup - start;
                cam.transform.position = new Vector3(Mathf.Sin(t) * 0.3f, 1.6f, 0f);
                yield return null;
            }
            recorder.StopTake();

            var output = Path.Combine(_dir, "renders", "S01", "S01_take01_Preview720p30.mp4");
            var host = KionjoRenderHost.Create(new KionjoRenderRequest
            {
                shotId = "id", shotCode = "S01", takeFolder = take, from = 0f, to = 1f,
                width = 320, height = 180, fps = 30f, ffmpegExe = ffmpeg, outputMp4 = output,
                encoderArgs = new[] { "-c:v", "libx264", "-crf", "28", "-preset", "ultrafast" },
            });
            float until = Time.realtimeSinceStartup + 60f;
            while (!host.Done && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(host.Done, "render did not finish in 60 s");
            Assert.IsNull(host.Error, host.Error);
            Assert.AreEqual(30, host.TargetFrames);
            var count = FrameCaptureTests.RunTool(FrameCaptureTests.FindExe("ffprobe"),
                $"-v error -count_frames -select_streams v:0 -show_entries stream=nb_read_frames -of csv=p=0 \"{output}\"", out _).Trim();
            Assert.AreEqual("30", count);
            Assert.AreEqual(0f, Time.captureDeltaTime, "the clock lock must be released after a render");
        }
    }
}
