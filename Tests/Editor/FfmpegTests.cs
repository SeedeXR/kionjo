using System.Diagnostics;
using System.IO;
using System.Threading;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;

namespace SeedeXR.Kionjo.Tests
{
    public class FfmpegTests
    {
        string _dir;
        string _savedPref;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(_dir);
            _savedPref = FfmpegLocator.PreferredPath;
        }

        [TearDown]
        public void TearDown()
        {
            FfmpegLocator.PreferredPath = _savedPref;
            FfmpegLocator.ClearCache();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        static string Ffmpeg()
        {
            var exe = FfmpegLocator.FindFfmpeg();
            if (exe == null) Assert.Ignore("ffmpeg is not installed on this machine");
            return exe;
        }

        [Test]
        public void ToShellString_QuotesSpacesAndBrackets()
        {
            var cmd = new FfmpegCommand { Args = { "-i", "D:/BERNARD/UNITY PROJECTS (GAMES)/x [1].mp4" } };
            Assert.AreEqual("ffmpeg -i \"D:/BERNARD/UNITY PROJECTS (GAMES)/x [1].mp4\"", cmd.ToShellString());
        }

        [Test]
        public void Args_KeepPathsAsSingleEntries()
        {
            var cmd = new FfmpegCommand().Add("-i", "D:/a b/c (d).mp4", "-y");
            Assert.AreEqual(3, cmd.Args.Count);
            Assert.AreEqual("D:/a b/c (d).mp4", cmd.Args[1]);
        }

        [Test]
        public void Quote_EscapesInnerQuotesAndTrailingBackslash()
        {
            Assert.AreEqual("plain", FfmpegCommand.Quote("plain"));
            Assert.AreEqual("\"a \\\"b\\\"\"", FfmpegCommand.Quote("a \"b\""));
            Assert.AreEqual("\"C:\\a b\\\\\"", FfmpegCommand.Quote("C:\\a b\\"));
        }

        [Test]
        public void Locator_PrefersPreferencePath()
        {
            var fake = Path.Combine(_dir, "ffmpeg.exe");
            File.WriteAllText(fake, "");
            FfmpegLocator.PreferredPath = fake;
            Assert.AreEqual(fake, FfmpegLocator.FindFfmpeg());
        }

        [Test]
        public void Locator_ParsesVersionLine() =>
            Assert.AreEqual("7.1", FfmpegLocator.ParseVersion("ffmpeg version 7.1-full_build-www.gyan.dev Copyright (c) 2000-2024"));

        [Test]
        public void Locator_FindsVersionOfInstalledFfmpeg()
        {
            Assert.IsTrue(FfmpegLocator.TryGetVersion(Ffmpeg(), out var v));
            Assert.IsTrue(char.IsDigit(v[0]), v);
        }

        [Test]
        public void Process_RunsFfmpegVersion_ExitZero()
        {
            var r = KionjoProcess.Run(new FfmpegCommand { Executable = Ffmpeg(), Args = { "-version" } });
            Assert.AreEqual(0, r.ExitCode, r.StdErrTail);
            StringAssert.StartsWith("ffmpeg version", r.StdOut);
        }

        [Test]
        public void Process_Cancel_KillsLongRun()
        {
            var cts = new CancellationTokenSource();
            var cmd = new FfmpegCommand { Executable = Ffmpeg() }.Add("-v", "error", "-re", "-f", "lavfi", "-i", "color=black:d=60", "-f", "null", "-");
            var sw = Stopwatch.StartNew();
            var task = KionjoProcess.RunAsync(cmd, cts.Token);
            Thread.Sleep(300);
            cts.Cancel();
            Assert.IsTrue(task.Wait(5000), "process did not stop within 5 s of cancel");
            Assert.IsTrue(task.Result.Cancelled);
            Assert.Less(sw.Elapsed.TotalSeconds, 6);
        }

        [Test]
        public void Process_Progress_ParsesOutTime()
        {
            Assert.IsTrue(KionjoProcess.TryParseOutTime("out_time_us=1500000", out var s));
            Assert.AreEqual(1.5, s, 1e-9);
            Assert.IsFalse(KionjoProcess.TryParseOutTime("frame=12", out _));
        }

        [Test]
        public void Probe_Duration_OfLavfiGeneratedFile()
        {
            var ffmpeg = Ffmpeg();
            var mp4 = Path.Combine(_dir, "black clip (1).mp4");
            var r = KionjoProcess.Run(new FfmpegCommand { Executable = ffmpeg }
                .Add("-v", "error", "-y", "-f", "lavfi", "-i", "color=c=black:s=160x90:r=30:d=1.5", "-c:v", "libx264", "-pix_fmt", "yuv420p", mp4));
            Assert.AreEqual(0, r.ExitCode, r.StdErrTail);
            Assert.AreEqual(1.5f, new FfprobeMediaProbe().DurationSeconds(mp4), 0.1f);
        }
    }
}
