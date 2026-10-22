using System.IO;
using System.Threading;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;

namespace SeedeXR.Kionjo.Tests
{
    public class CutJobTests
    {
        string _dir;
        string _ffmpeg;

        [SetUp]
        public void SetUp()
        {
            _ffmpeg = FfmpegLocator.FindFfmpeg();
            if (_ffmpeg == null) Assert.Ignore("ffmpeg is not installed on this machine");
            _dir = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name, "cut (draft) [01]");
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [TearDown]
        public void TearDown()
        {
            var root = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        static CutPlan SmallPlan()
        {
            var p = new CutPlan
            {
                Width = 320, Height = 180, Fps = 30, BaseName = "Test",
                EncoderArgs = new[] { "-c:v", "libx264", "-crf", "28", "-preset", "ultrafast" },
            };
            p.Segments.Add(new CutSegment { Label = "01_C01", Kind = SegmentKind.Black, Duration = 1f });
            p.Segments.Add(new CutSegment { Label = "02_C02", Kind = SegmentKind.Text, Duration = 1.5f, FilmStart = 1f, Lines = { "Nothing's: black or white 100%" } });
            p.Subtitles.Add(new SubtitleCue { Start = 0.2f, End = 1f, Text = "Hello" });
            return p;
        }

        [Test]
        public void CutJob_OnlyCards_ProducesMp4AndSrt()
        {
            var plan = SmallPlan();
            var job = new CutJob(plan, _dir, _ffmpeg);
            Assert.IsTrue(job.RunAsync(CancellationToken.None).Wait(120000), "cut did not finish in 2 minutes");
            Assert.IsFalse(job.Failed, job.Error);
            Assert.IsTrue(File.Exists(job.OutputMp4), job.OutputMp4);
            StringAssert.EndsWith("Test_cut (draft) [01].mp4", job.OutputMp4);
            Assert.AreEqual(2.5f, new FfprobeMediaProbe().DurationSeconds(job.OutputMp4), 0.15f);
            Assert.AreEqual(SrtWriter.Write(plan.Subtitles), File.ReadAllText(job.OutputSrt));
            Assert.IsTrue(File.Exists(Path.Combine(_dir, "commands.txt")));
            Assert.AreEqual(1f, job.Progress, 1e-4);
        }

        [Test]
        public void CutJob_MissingSource_FailsWithStderrTail()
        {
            var plan = SmallPlan();
            plan.Segments.Add(new CutSegment { Label = "03_S01", Kind = SegmentKind.Video, SourcePath = Path.Combine(_dir, "nope.mp4"), Duration = 1f });
            var job = new CutJob(plan, _dir, _ffmpeg);
            Assert.IsTrue(job.RunAsync(CancellationToken.None).Wait(120000));
            Assert.IsTrue(job.Failed);
            StringAssert.Contains("Segment 03_S01 failed", job.Error);
            Assert.IsTrue(job.Error.Contains("No such file") || job.Error.Contains("Error opening"), job.Error);
            Assert.IsTrue(File.Exists(Path.Combine(_dir, "commands.txt")));
        }

        [Test]
        public void CutJob_Cancel_StopsEarly()
        {
            var plan = new CutPlan
            {
                Width = 320, Height = 180, Fps = 30, BaseName = "Test",
                EncoderArgs = new[] { "-c:v", "libx264", "-preset", "veryslow" },
            };
            plan.Segments.Add(new CutSegment { Label = "01_C01", Kind = SegmentKind.Black, Duration = 600f });
            var job = new CutJob(plan, _dir, _ffmpeg);
            var cts = new CancellationTokenSource();
            var task = job.RunAsync(cts.Token);
            Thread.Sleep(500);
            cts.Cancel();
            bool cancelled = false;
            try
            {
                task.Wait(5000);
            }
            catch (System.AggregateException e) when (e.InnerException is System.OperationCanceledException)
            {
                cancelled = true;
            }
            Assert.IsTrue(cancelled || task.IsCanceled, "the job should end with a cancellation");
        }
    }
}
