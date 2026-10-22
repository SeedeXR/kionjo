using System.IO;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;

namespace SeedeXR.Kionjo.Tests
{
    public class SilenceDetectTests
    {
        const string Canned =
            "[silencedetect @ 0000016c] silence_start: 0\n" +
            "[silencedetect @ 0000016c] silence_end: 0.82 | silence_duration: 0.82\n" +
            "[silencedetect @ 0000016c] silence_start: 5.1\n" +
            "[silencedetect @ 0000016c] silence_end: 5.4 | silence_duration: 0.3\n" +
            "[silencedetect @ 0000016c] silence_start: 12.88\n" +
            "[silencedetect @ 0000016c] silence_end: 13.7 | silence_duration: 0.82\n";

        [Test]
        public void Parse_Silences_FromCannedStderr()
        {
            var s = SilenceDetect.ParseSilences(Canned);
            Assert.AreEqual(3, s.Count);
            Assert.AreEqual(0.82f, s[0].End, 1e-5);
            Assert.AreEqual(12.88f, s[2].Start, 1e-5);
        }

        [Test]
        public void SpeechBounds_LeadingAndTrailing()
        {
            var (start, end) = SilenceDetect.SpeechBounds(SilenceDetect.ParseSilences(Canned), 13.7f);
            Assert.AreEqual(0.82f, start, 1e-5); // judge-charge-03 in trailer 1
            Assert.AreEqual(12.88f, end, 1e-5);
        }

        [Test]
        public void SpeechBounds_NoSilence_FullRange() =>
            Assert.AreEqual((0f, 4f), SilenceDetect.SpeechBounds(SilenceDetect.ParseSilences(""), 4f));

        [Test]
        public void SpeechBounds_OpenSilenceAtEnd()
        {
            var s = SilenceDetect.ParseSilences("silence_start: 3.5\n");
            Assert.AreEqual((0f, 3.5f), SilenceDetect.SpeechBounds(s, 4f));
        }

        [Test]
        public void Resolver_RealTone_PaddedWithSilence()
        {
            var ffmpeg = FfmpegLocator.FindFfmpeg();
            if (ffmpeg == null) Assert.Ignore("ffmpeg is not installed on this machine");
            var dir = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name);
            Directory.CreateDirectory(dir);
            try
            {
                var wav = Path.Combine(dir, "tone (padded).wav");
                var r = KionjoProcess.Run(new FfmpegCommand { Executable = ffmpeg }
                    .Add("-v", "error", "-y", "-f", "lavfi", "-i", "sine=frequency=440:duration=1", "-af", "adelay=500|500,apad=pad_dur=0.5", wav));
                Assert.AreEqual(0, r.ExitCode, r.StdErrTail);
                var bounds = SilenceDetect.Resolver(ffmpeg)(new KionjoVoiceLine { audioPath = wav });
                Assert.IsTrue(bounds.HasValue);
                Assert.AreEqual(0.5f, bounds.Value.start, 0.1f);
                Assert.AreEqual(1.5f, bounds.Value.end, 0.1f);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
