using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;
using UnityEditor;
using UnityEngine;

namespace SeedeXR.Kionjo.Tests
{
    /// <summary>
    /// The cut half of the Tier 1 flow, end to end, on real files: a storyboard with a black card,
    /// a rendered shot (an ffmpeg clip stands in for a Kionjo render, see RenderHostTests for the
    /// real one) with a voice line and subtitle, and a text card, assembled into a draft.
    /// </summary>
    public class Tier1EndToEndTests
    {
        string _dir;
        KionjoStoryboard _sb;

        [SetUp]
        public void SetUp()
        {
            if (FfmpegLocator.FindFfmpeg() == null) Assert.Ignore("ffmpeg is not installed on this machine");
            _dir = Path.Combine(Path.GetTempPath(), "KionjoTests", "Tier1 (end to end) [x]");
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(_dir);
            _sb = ScriptableObject.CreateInstance<KionjoStoryboard>();
            _sb.outputFolder = _dir;
            _sb.title = "Baraza Test";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sb);
            Undo.ClearAll();
            if (_dir != null && Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        static void Ffmpeg(params string[] args)
        {
            var r = KionjoProcess.Run(new FfmpegCommand { Executable = FfmpegLocator.FindFfmpeg() }.Add(args), 60000);
            Assert.AreEqual(0, r.ExitCode, r.StdErrTail);
        }

        [Test]
        public void Storyboard_To_Draft()
        {
            var opening = _sb.AddShot(KionjoShotType.Card);
            opening.targetSeconds = 1f;

            var shot = _sb.AddShot();
            shot.inPoint = 0f;
            shot.outPoint = 1f;
            shot.fadeIn = 0.3f;
            // the shot's take and its render, where Kionjo would have written them
            var take = Path.Combine(KionjoPaths.ShotTakes(_sb, shot), "S01_take01");
            new KionjoTakeMeta { shotCode = "S01", takeNumber = 1, durationSeconds = 1.2f }.Save(take);
            var render = KionjoPaths.RenderPath(_sb, shot, "S01_take01", KionjoRenderPreset.Preview720p30);
            Directory.CreateDirectory(Path.GetDirectoryName(render));
            Ffmpeg("-v", "error", "-y", "-f", "lavfi", "-i", "testsrc2=s=320x180:r=30:d=1.2", "-c:v", "libx264", "-pix_fmt", "yuv420p", render);
            var voice = Path.Combine(_dir, "line 1.wav");
            Ffmpeg("-v", "error", "-y", "-f", "lavfi", "-i", "sine=frequency=440:duration=1", voice);
            shot.voice.Add(new KionjoVoiceLine { audioPath = voice, at = 0.2f, subtitle = "Hello", trimMode = KionjoTrimMode.Manual, trimOut = 0.7f });

            var closing = _sb.AddShot(KionjoShotType.Card);
            closing.cardKind = KionjoCardKind.Text;
            closing.cardLines.Add("The end");
            closing.targetSeconds = 1.5f;

            var plan = CutPlanBuilder.Build(_sb, KionjoCutPreset.Preview720p30, new FfprobeMediaProbe());
            Assert.IsEmpty(plan.Warnings, string.Join("\n", plan.Warnings));
            Assert.AreEqual(3, plan.Segments.Count);
            Assert.AreEqual(render, plan.Segments[1].SourcePath);

            var folder = KionjoPaths.NextNumbered(KionjoPaths.Cuts(_sb), "draft");
            var job = new CutJob(plan, folder, FfmpegLocator.FindFfmpeg());
            Assert.IsTrue(job.RunAsync(CancellationToken.None).Wait(180000), "cut did not finish in 3 minutes");
            Assert.IsFalse(job.Failed, job.Error);
            StringAssert.EndsWith("Baraza_Test_draft01.mp4", job.OutputMp4);
            Assert.AreEqual(3.5f, new FfprobeMediaProbe().DurationSeconds(job.OutputMp4), 0.2f);

            var probe = KionjoProcess.Run(new FfmpegCommand { Executable = FfmpegLocator.FindFfprobe() }
                .Add("-v", "error", "-show_entries", "stream=codec_type", "-of", "csv=p=0", job.OutputMp4));
            var streams = probe.StdOut.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            CollectionAssert.AreEquivalent(new[] { "video", "audio" }, streams);
            StringAssert.Contains("Hello", File.ReadAllText(job.OutputSrt));
            Assert.GreaterOrEqual(File.ReadAllLines(Path.Combine(folder, "commands.txt")).Length, 7); // header, cd, 3 segments, concat, subs, audio, mux
        }
    }
}
