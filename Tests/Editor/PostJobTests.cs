using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;
using UnityEngine;

namespace SeedeXR.Kionjo.Tests
{
    public class PostJobTests
    {
        string _dir, _ffmpeg, _a, _b, _music;

        [SetUp]
        public void SetUp()
        {
            _ffmpeg = FfmpegLocator.FindFfmpeg();
            if (_ffmpeg == null) Assert.Ignore("ffmpeg is not installed on this machine");
            _dir = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(_dir);
            _a = Path.Combine(_dir, "a.mp4");
            _b = Path.Combine(_dir, "b clip [x].mp4");
            _music = Path.Combine(_dir, "music.wav");
        }

        [TearDown]
        public void TearDown()
        {
            if (_dir != null && Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        void Make(params string[] args)
        {
            var c = new FfmpegCommand { Executable = _ffmpeg, WorkingDirectory = _dir }.Add("-v", "error", "-y").Add(args);
            var r = KionjoProcess.Run(c, 60000);
            Assert.AreEqual(0, r.ExitCode, r.StdErrTail);
        }

        void MakeClipWithSound() => Make("-f", "lavfi", "-i", "testsrc=size=1920x1080:rate=30:duration=3", "-f", "lavfi", "-i", "sine=frequency=440:duration=3", "-shortest", _a);
        void MakeClipSilent(string path) => Make("-f", "lavfi", "-i", "testsrc=size=1920x1080:rate=30:duration=3", "-an", path);

        JournalEntry Entry()
        {
            var e = KionjoJournal.Create(Path.Combine(_dir, "journal"), "Test post", "Game", new System.DateTime(2026, 11, 13));
            e.achieved = "It works.";
            return e;
        }

        KionjoPost Post(params string[] paths)
        {
            var p = new KionjoPost { name = "test", hook = "Can it work?", overlays = { "It does." }, coverTitle = "IT WORKS" };
            foreach (var path in paths) p.items.Add(new PostItem { path = path });
            return p;
        }

        PostJob Run(JournalEntry e, KionjoPost p)
        {
            var job = new PostJob(e, p, null, Path.Combine(_dir, "posts"), _ffmpeg, new FfprobeMediaInfo(), null);
            Assert.IsTrue(job.RunAsync(CancellationToken.None).Wait(300000), "post did not finish in 5 minutes");
            return job;
        }

        static string Probe(string file)
        {
            var r = KionjoProcess.Run(new FfmpegCommand { Executable = FfmpegLocator.FindFfprobe() }
                .Add("-v", "error", "-show_entries", "stream=codec_name,profile,width,height,r_frame_rate,pix_fmt,sample_rate,channels:format=duration", "-of", "default=nw=1", file), 20000);
            return r.StdOut;
        }

        [Test]
        public void Run_TwoClips_BundleComplete()
        {
            MakeClipWithSound();
            MakeClipSilent(_b);
            var e = Entry();
            e.media.Add(new JournalMedia { path = _a });
            KionjoJournal.Save(e);
            var p = Post(_a, _b);
            var job = Run(e, p);
            Assert.IsFalse(job.Failed, job.Error);
            foreach (var f in new[] { "post/test_9x16.mp4", "capcut/test_clean_9x16.mp4", "post/cover.jpg", "post/caption.txt", "recipe/post.json", "recipe/commands.txt", "recipe/post.ass", "BRIEF.md", "materials/a.mp4" })
                Assert.IsTrue(File.Exists(Path.Combine(job.Folder, f)), f);
            Assert.IsFalse(File.Exists(Path.Combine(job.Folder, "post", "test_nomusic_9x16.mp4")));
            foreach (var gone in new[] { "seg", "joined.mp4", "audio.wav" })
                Assert.IsFalse(File.Exists(Path.Combine(job.Folder, gone)) || Directory.Exists(Path.Combine(job.Folder, gone)), gone);
            Assert.AreEqual(Path.Combine(job.Folder, "post", "test_9x16.mp4"), job.OutputMp4);
            var probe = Probe(job.OutputMp4);
            StringAssert.Contains("width=1080", probe);
            StringAssert.Contains("height=1920", probe);
            StringAssert.Contains("r_frame_rate=30/1", probe);
            StringAssert.Contains("profile=High", probe);
            StringAssert.Contains("pix_fmt=yuv420p", probe);
            StringAssert.Contains("sample_rate=48000", probe);
            StringAssert.Contains("channels=2", probe);
            Assert.AreEqual(6f, new FfprobeMediaProbe().DurationSeconds(job.OutputMp4), 0.2f);
            StringAssert.Contains("Can it work?", File.ReadAllText(Path.Combine(job.Folder, "post", "caption.txt")));
            // the entry now records the post
            var back = KionjoJournal.Load(e.Folder, out _);
            Assert.AreEqual(1, back.posts.Count);
            Assert.AreEqual(p.id, back.posts[0].id);
            Assert.AreEqual(Path.Combine(_dir, "posts", "2026-11-13_test-post", "v01"), job.Folder);

            // A second build writes v02; v01 keeps its recipe and loses its videos.
            var second = Run(KionjoJournal.Load(e.Folder, out _), p);
            Assert.IsFalse(second.Failed, second.Error);
            Assert.AreEqual(Path.Combine(_dir, "posts", "2026-11-13_test-post", "v02"), second.Folder);
            Assert.IsFalse(File.Exists(Path.Combine(job.Folder, "post", "test_9x16.mp4")));
            Assert.IsTrue(File.Exists(Path.Combine(job.Folder, "recipe", "post.json")));
            Assert.IsTrue(File.Exists(Path.Combine(second.Folder, "post", "test_9x16.mp4")));
            Assert.IsTrue(File.Exists(_a), "the footage stays");
        }

        [Test]
        public void Run_WithMusic_AlsoWritesNoMusic()
        {
            MakeClipWithSound();
            Make("-f", "lavfi", "-i", "sine=frequency=220:duration=10", _music);
            var p = Post(_a);
            p.musicPath = _music;
            var job = Run(Entry(), p);
            Assert.IsFalse(job.Failed, job.Error);
            Assert.IsTrue(File.Exists(Path.Combine(job.Folder, "post", "test_nomusic_9x16.mp4")));
            Assert.IsFalse(File.Exists(Path.Combine(job.Folder, "audio_nomusic.wav")), "intermediate sound is deleted");
        }

        [Test]
        public void Run_MissingMedia_FailsWithMessage()
        {
            var job = Run(Entry(), Post("D:/nope/x.mp4"));
            Assert.IsTrue(job.Failed);
            Assert.AreEqual("Missing media: D:/nope/x.mp4", job.Error);
        }

        [Test]
        public void Run_Over180s_Refused()
        {
            MakeClipSilent(_b);
            var p = Post(_b);
            p.items[0].outPoint = 200f;
            var job = Run(Entry(), p);
            Assert.IsTrue(job.Failed);
            Assert.AreEqual("Longer than 3 minutes: Instagram will not recommend it.", job.Error);
            Assert.IsFalse(Directory.Exists(job.Folder));
        }

        [Test]
        public void Run_SilentClips_WarnsSilence()
        {
            MakeClipSilent(_b);
            var c = Path.Combine(_dir, "c.mp4");
            MakeClipSilent(c);
            var job = Run(Entry(), Post(_b, c));
            Assert.IsFalse(job.Failed, job.Error);
            Assert.IsTrue(job.Warnings.Any(w => w.StartsWith("Silence of ") && w.EndsWith(" s: keep a sound bed under the picture.")), string.Join("\n", job.Warnings));
        }
    }
}
