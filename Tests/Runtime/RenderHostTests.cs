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
            Assert.IsFalse(File.Exists(Path.ChangeExtension(output, ".partial.mp4")), "the render is moved into place when it succeeds");
            Assert.AreEqual(0f, Time.captureDeltaTime, "the clock lock must be released after a render");
        }

        // Bullet time: the world is frozen (game time stands still, physics does not step) while the camera orbits.
        [UnityTest]
        public IEnumerator RenderHost_FrozenLiveShot_CameraMovesWorldStands()
        {
            var ffmpeg = FrameCaptureTests.FindExe("ffmpeg");
            if (ffmpeg == null) Assert.Ignore("ffmpeg is not installed on this machine");
            _root = new GameObject("Set");
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(_root.transform);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.transform.SetParent(_root.transform);
            cam.gameObject.AddComponent<KionjoCameraSource>();
            var move = cam.gameObject.AddComponent<KionjoCameraMove>();
            move.mode = KionjoCameraMove.MoveMode.Orbit;
            move.pivot = pivot;
            move.orbitDegrees = 180f;
            move.seconds = 1f;
            move.radius = 2f;
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.transform.SetParent(_root.transform);
            ball.transform.position = new Vector3(0f, 5f, 0f);
            ball.AddComponent<Rigidbody>();
            yield return null;
            var start = pivot.position + pivot.forward * 2f + pivot.up * move.height; // where the orbit begins
            float ballY = float.NaN, gameTime = 0f; // read once rendering runs: the ball falls during staging
            float fixedBefore = Time.fixedDeltaTime;

            var host = KionjoRenderHost.Create(new KionjoRenderRequest
            {
                shotId = "id", shotCode = "S02", liveSeconds = 1f,
                width = 160, height = 90, fps = 30f, ffmpegExe = ffmpeg, outputMp4 = Path.Combine(_dir, "frozen.mp4"),
                encoderArgs = new[] { "-c:v", "libx264", "-crf", "28", "-preset", "ultrafast" },
                speedKeys = { new KionjoSpeedKey { t = 0f, speed = 0f } },
            });
            float until = Time.realtimeSinceStartup + 60f;
            while (!host.Done && Time.realtimeSinceStartup < until)
            {
                if (host.Frames > 2)
                {
                    if (float.IsNaN(ballY)) ballY = ball.transform.position.y;
                    gameTime += Time.deltaTime;
                }
                yield return null;
            }
            Assert.IsTrue(host.Done, "render did not finish in 60 s");
            Assert.IsNull(host.Error, host.Error);
            Assert.AreEqual(30, host.TargetFrames, "a live shot's length is output time");
            Assert.AreEqual(0f, gameTime, 1e-6, "game time must stand still");
            Assert.AreEqual(ballY, ball.transform.position.y, 1e-3, "physics must not step while frozen");
            Assert.AreEqual(4f, Vector3.Distance(start, cam.transform.position), 0.05f, "the camera orbits half way round (2 m radius)");
            Assert.AreEqual(1f, Time.timeScale, "time restored");
            Assert.AreEqual(fixedBefore, Time.fixedDeltaTime, 1e-6, "physics step restored");
        }

        [UnityTest]
        public IEnumerator RenderHost_HalfSpeedTake_TakesTwiceTheFrames()
        {
            var ffmpeg = FrameCaptureTests.FindExe("ffmpeg");
            if (ffmpeg == null) Assert.Ignore("ffmpeg is not installed on this machine");
            _root = new GameObject("Rig");
            var cam = new GameObject("Head").AddComponent<Camera>();
            cam.transform.SetParent(_root.transform);
            cam.tag = "MainCamera";
            var recorder = _root.AddComponent<KionjoRecorder>();
            recorder.tracks.Add(new KionjoTrack { name = "Head", target = cam.transform });
            var take = Path.Combine(_dir, "takes", "S03", "S03_take01");
            recorder.StartTake(take, "id", "S03", 1);
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 1.2f) yield return null;
            recorder.StopTake();

            var host = KionjoRenderHost.Create(new KionjoRenderRequest
            {
                shotId = "id", shotCode = "S03", takeFolder = take, from = 0f, to = 1f,
                width = 160, height = 90, fps = 30f, ffmpegExe = ffmpeg, outputMp4 = Path.Combine(_dir, "half.mp4"),
                encoderArgs = new[] { "-c:v", "libx264", "-crf", "28", "-preset", "ultrafast" },
                speedKeys = { new KionjoSpeedKey { t = 0f, speed = 0.5f } },
            });
            float until = Time.realtimeSinceStartup + 60f;
            while (!host.Done && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsNull(host.Error, host.Error);
            Assert.AreEqual(60, host.TargetFrames, "1 s of take at half speed is 2 s of video");
            Assert.AreEqual(1f, Time.timeScale, "time restored");
        }
    }
}
