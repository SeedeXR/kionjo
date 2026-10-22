using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SeedeXR.Kionjo.Tests
{
    public class FrameCaptureTests
    {
        string _dir;
        GameObject _root;

        public static string FindExe(string name)
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                try
                {
                    var p = Path.Combine(dir.Trim().Trim('"'), name + ".exe");
                    if (File.Exists(p)) return p;
                    p = Path.Combine(dir.Trim().Trim('"'), name);
                    if (File.Exists(p)) return p;
                }
                catch (ArgumentException)
                {
                }
            }
            return null;
        }

        public static string RunTool(string exe, string args, out int exit)
        {
            var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using var p = Process.Start(psi);
            var err = p.StandardError.ReadToEndAsync();
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            exit = p.ExitCode;
            return output + err.Result;
        }

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
            Time.captureDeltaTime = 0f;
            try
            {
                if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            }
            catch (IOException)
            {
                // ffmpeg may still hold a file for a moment after a kill
            }
        }

        /// <summary>Camera at the origin looking +Z over a blue background, a red quad filling the lower half.</summary>
        Camera TwoColourScene()
        {
            _root = new GameObject("Capture Scene");
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.transform.SetParent(_root.transform);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.blue;
            cam.fieldOfView = 60f;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.transform.SetParent(_root.transform);
            quad.transform.position = new Vector3(0f, -2.5f, 5f);
            quad.transform.localScale = new Vector3(20f, 5f, 1f);
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", Color.red);
            mat.SetColor("_Color", Color.red);
            quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return cam;
        }

        KionjoFrameCapture MakeCapture(Camera cam, string output, int frames)
        {
            var ffmpeg = FindExe("ffmpeg");
            if (ffmpeg == null) Assert.Ignore("ffmpeg is not installed on this machine");
            var cap = _root.AddComponent<KionjoFrameCapture>();
            cap.camera = cam;
            cap.width = 320;
            cap.height = 180;
            cap.fps = 30f;
            cap.targetFrames = frames;
            cap.sink = new FfmpegPipeSink(ffmpeg, output, new[] { "-c:v", "libx264", "-crf", "18", "-preset", "ultrafast" });
            return cap;
        }

        [UnityTest]
        public IEnumerator Capture_TenFrames_ProducesMp4WithTenFrames()
        {
            var cam = TwoColourScene();
            var output = Path.Combine(_dir, "out [1].mp4");
            var cap = MakeCapture(cam, output, 10);
            bool finished = false;
            cap.Finished += () => finished = true;
            cap.Begin();
            float until = Time.realtimeSinceStartup + 30f;
            while (!finished && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(finished, "capture did not finish");
            Assert.IsNull(cap.Error, cap.Error);
            Assert.IsTrue(File.Exists(output));

            var ffprobe = FindExe("ffprobe");
            var count = RunTool(ffprobe, $"-v error -count_frames -select_streams v:0 -show_entries stream=nb_read_frames -of csv=p=0 \"{output}\"", out _).Trim();
            Assert.AreEqual("10", count);

            var raw = Path.Combine(_dir, "frame.rgb");
            RunTool(FindExe("ffmpeg"), $"-v error -y -i \"{output}\" -frames:v 1 -f rawvideo -pix_fmt rgb24 \"{raw}\"", out int exit);
            Assert.AreEqual(0, exit);
            var bytes = File.ReadAllBytes(raw);
            Assert.AreEqual(320 * 180 * 3, bytes.Length);
            int TopLeft(int c) => bytes[(10 * 320 + 10) * 3 + c];
            int BottomLeft(int c) => bytes[(170 * 320 + 10) * 3 + c];
            bool black = TopLeft(0) + TopLeft(1) + TopLeft(2) + BottomLeft(0) + BottomLeft(1) + BottomLeft(2) < 30;
            Assert.IsFalse(black, "frame is black: was the editor started with -nographics?");
            Assert.IsTrue(TopLeft(2) > 150 && TopLeft(0) < 100, $"top left should be blue, was {TopLeft(0)},{TopLeft(1)},{TopLeft(2)}");
            Assert.IsTrue(BottomLeft(0) > 150 && BottomLeft(2) < 100, $"bottom left should be red, was {BottomLeft(0)},{BottomLeft(1)},{BottomLeft(2)}");
        }

        [UnityTest]
        public IEnumerator Abort_RestoresCameraAndCaptureDeltaTime()
        {
            var cam = TwoColourScene();
            Time.captureDeltaTime = 0f;
            var cap = MakeCapture(cam, Path.Combine(_dir, "a.mp4"), 0);
            cap.horizontalFovOverride = 100f;
            cap.Begin();
            Assert.AreNotEqual(60f, cam.fieldOfView);
            Assert.IsFalse(cam.enabled);
            Assert.AreEqual(1f / 30f, Time.captureDeltaTime, 1e-6);
            yield return null;
            cap.Abort();
            Assert.AreEqual(60f, cam.fieldOfView, 1e-4);
            Assert.IsTrue(cam.enabled);
            Assert.AreEqual(0f, Time.captureDeltaTime);
        }

        [UnityTest]
        public IEnumerator Destroy_MidCapture_KillsFfmpeg()
        {
            int before = Process.GetProcessesByName("ffmpeg").Length;
            var cam = TwoColourScene();
            var cap = MakeCapture(cam, Path.Combine(_dir, "d.mp4"), 0);
            cap.Begin();
            yield return null;
            yield return null;
            Assert.Greater(Process.GetProcessesByName("ffmpeg").Length, before);
            Object.Destroy(_root);
            _root = null;
            float until = Time.realtimeSinceStartup + 3f;
            while (Process.GetProcessesByName("ffmpeg").Length > before && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(before, Process.GetProcessesByName("ffmpeg").Length);
        }

        [UnityTest]
        public IEnumerator Still_WritesPng()
        {
            var cam = TwoColourScene();
            var png = Path.Combine(_dir, "thumb.png");
            KionjoStill.CapturePng(cam, png, 160, 90);
            yield return null;
            Assert.IsTrue(File.Exists(png));
            var tex = new Texture2D(2, 2);
            Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(png)));
            Assert.AreEqual(160, tex.width);
            var top = tex.GetPixel(5, 85); // Texture2D rows start at the bottom
            Assert.Greater(top.b, 0.6f, $"top should be blue, was {top}");
            Object.DestroyImmediate(tex);
        }
    }
}
