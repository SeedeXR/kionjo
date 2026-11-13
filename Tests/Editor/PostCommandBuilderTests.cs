using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;

namespace SeedeXR.Kionjo.Tests
{
    public class PostCommandBuilderTests
    {
        sealed class FakeInfo : IMediaInfo
        {
            public float Seconds = 4f;
            public (int, int) Px = (1920, 1080);
            public bool Audio = true;
            public float Duration(string path) => Seconds;
            public (int w, int h) Size(string path) => Px;
            public bool HasAudio(string path) => Audio;
        }

        static PostSegment Seg(int w, int h, PostFit fit = PostFit.Crop, float d = 3f) =>
            new() { Label = "01-a", SourcePath = "D:/a.mp4", Duration = d, SrcW = w, SrcH = h, Fit = fit, HasAudio = true };

        const string Tail3 = ",setsar=1,fps=30,format=yuv420p,tpad=stop_mode=clone:stop_duration=3";

        [Test]
        public void FitFilter_Crop_16x9Source() =>
            Assert.AreEqual("crop=ih*1080/1920:ih:(iw-ih*1080/1920)*0.5:0,scale=1080:1920" + Tail3, PostCommandBuilder.FitFilter(Seg(1920, 1080), new PostPlan()));

        [Test]
        public void FitFilter_VerticalSource_NoCrop() =>
            Assert.AreEqual("scale=1080:1920" + Tail3, PostCommandBuilder.FitFilter(Seg(1080, 1920), new PostPlan()));

        [Test]
        public void FitFilter_BlurFill_Exact() =>
            Assert.AreEqual(
                "[0:v]split[bg][fg];[bg]scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920,boxblur=20:2[b];" +
                "[fg]scale=1080:1920:force_original_aspect_ratio=decrease:force_divisible_by=2[f];[b][f]overlay=(W-w)/2:(H-h)/2" + Tail3 + "[v]",
                PostCommandBuilder.FitFilter(Seg(1920, 1080, PostFit.BlurFill), new PostPlan()));

        [Test]
        public void Segment_OutPastEnd_HoldsLastFrame()
        {
            var c = PostCommandBuilder.Segment(Seg(1920, 1080, d: 7.5f), new PostPlan());
            var vf = c.Args[c.Args.IndexOf("-vf") + 1];
            StringAssert.EndsWith("tpad=stop_mode=clone:stop_duration=7.5", vf);
            Assert.AreEqual("225", c.Args[c.Args.IndexOf("-frames:v") + 1]);
            Assert.AreEqual("seg/01-a.mp4", c.Args.Last());
        }

        [Test]
        public void Build_PathWithSpacesAndBrackets_IsOneArgument()
        {
            const string src = "D:/BERNARD/UNITY PROJECTS (GAMES)/Ghost look 1 - Pattern [v2].mp4";
            var post = new KionjoPost { name = "ghost", hook = "Hook", items = { new PostItem { path = src } } };
            var plan = PostPlanBuilder.Build(post, new FakeInfo(), null);
            var cmds = PostCommandBuilder.Build(plan, "D:/out", "ffmpeg", PostCommandBuilder.Joined);
            var segment = cmds[0];
            Assert.AreEqual(1, segment.Args.Count(a => a == src));
            Assert.AreEqual("01-ghost-look-1-pattern-v2", plan.Segments[0].Label);
            foreach (var c in cmds)
            {
                Assert.AreEqual("D:/out", c.WorkingDirectory);
                foreach (var a in c.Args)
                    if (a.Contains("=") || a.Contains(";"))
                        StringAssert.DoesNotContain("UNITY PROJECTS", a);
            }
        }

        [Test]
        public void AudioGraph_ItemWithoutAudio_UsesSilence()
        {
            var plan = new PostPlan { ClipSound = true, TotalSeconds = 6f };
            plan.Segments.Add(Seg(1920, 1080));
            var silent = Seg(1920, 1080);
            silent.HasAudio = false;
            plan.Segments.Add(silent);
            var c = PostCommandBuilder.AudioGraph(plan, true);
            Assert.AreEqual(1, c.Args.Count(a => a == "anullsrc=r=48000:cl=stereo"));
            Assert.AreEqual(1, c.Args.Count(a => a == "D:/a.mp4"));
            var graph = c.Args[c.Args.IndexOf("-filter_complex") + 1];
            StringAssert.Contains("[c0][c1]concat=n=2:v=0:a=1,volume=1[clip];", graph);
            StringAssert.EndsWith("[clip]loudnorm=I=-14:TP=-1:LRA=11[a]", graph);
            Assert.AreEqual("audio.wav", c.Args.Last());
        }

        [Test]
        public void AudioGraph_DuckWhenSpeech_HasSidechain()
        {
            var plan = new PostPlan { ClipSound = true, TotalSeconds = 3f, MusicPath = "D:/m.wav", MusicStart = 2f, DuckMusicUnderSpeech = true };
            plan.Segments.Add(Seg(1920, 1080));
            var graph = PostCommandBuilder.AudioGraph(plan, true).Args.SkipWhile(a => a != "-filter_complex").ElementAt(1);
            StringAssert.Contains("asplit[clip][key];", graph);
            StringAssert.Contains("atrim=2:5,", graph);
            StringAssert.Contains("afade=t=out:st=2:d=1", graph);
            StringAssert.Contains("[mus][key]sidechaincompress=threshold=0.05:ratio=8:attack=20:release=400[duck];", graph);
            StringAssert.EndsWith("[clip][duck]amix=inputs=2:normalize=0,loudnorm=I=-14:TP=-1:LRA=11[a]", graph);
            Assert.AreEqual("audio_nomusic.wav", PostCommandBuilder.AudioGraph(plan, false).Args.Last());
        }

        [Test]
        public void AudioGraph_NoSoundNoMusic_ReturnsNull()
        {
            var plan = new PostPlan { ClipSound = false, TotalSeconds = 3f };
            plan.Segments.Add(Seg(1920, 1080));
            Assert.IsNull(PostCommandBuilder.AudioGraph(plan, true));
        }

        [Test]
        public void Build_NoMusic_SkipsNoMusicVariant()
        {
            var post = new KionjoPost { name = "p", items = { new PostItem { path = "D:/a.mp4" } } };
            var outputs = PostCommandBuilder.Build(PostPlanBuilder.Build(post, new FakeInfo(), null), "D:/out", "ffmpeg", PostCommandBuilder.Joined)
                .Select(c => c.Args.Last()).ToList();
            CollectionAssert.AreEqual(new[] { "seg/01-a.mp4", "joined.mp4", "audio.wav", "p_9x16.mp4", "p_clean_9x16.mp4", "cover.jpg" }, outputs);

            post.musicPath = "D:/m.wav";
            outputs = PostCommandBuilder.Build(PostPlanBuilder.Build(post, new FakeInfo(), null), "D:/out", "ffmpeg", PostCommandBuilder.Joined)
                .Select(c => c.Args.Last()).ToList();
            CollectionAssert.AreEqual(new[] { "seg/01-a.mp4", "joined.mp4", "audio.wav", "audio_nomusic.wav", "p_9x16.mp4", "p_nomusic_9x16.mp4", "p_clean_9x16.mp4", "cover.jpg" }, outputs);
        }

        [Test]
        public void PlanBuilder_LoopEnd_AppendsFirstHalfSecond()
        {
            var post = new KionjoPost { end = PostEnd.Loop, items = { new PostItem { path = "D:/a.mp4", inPoint = 1f }, new PostItem { path = "D:/b.mp4" } } };
            var plan = PostPlanBuilder.Build(post, new FakeInfo(), null);
            Assert.AreEqual(3, plan.Segments.Count);
            var loop = plan.Segments[2];
            Assert.AreEqual("D:/a.mp4", loop.SourcePath);
            Assert.AreEqual(1f, loop.SourceIn);
            Assert.AreEqual(0.5f, loop.Duration);
            Assert.AreEqual(3f + 4f + 0.5f, plan.TotalSeconds, 1e-4);
        }

        [Test]
        public void PlanBuilder_CaptionWords_ShiftedToFilmTime()
        {
            var post = new KionjoPost
            {
                items =
                {
                    new PostItem { path = "D:/a.mp4" },
                    new PostItem { path = "D:/b.mp4", inPoint = 2f, outPoint = 4f, captionSpeech = true },
                },
            };
            var words = new List<WordTimer.Word> { new() { w = "early", s = 1f, e = 1.5f }, new() { w = "hello", s = 3f, e = 3.4f }, new() { w = "late", s = 3.9f, e = 4.6f } };
            var plan = PostPlanBuilder.Build(post, new FakeInfo(), p => p == "D:/b.mp4" ? words : null);
            Assert.AreEqual(2, plan.CaptionWords.Count);
            Assert.AreEqual(5f, plan.CaptionWords[0].s, 1e-4);
            Assert.AreEqual(5.4f, plan.CaptionWords[0].e, 1e-4);
            Assert.AreEqual(6f, plan.CaptionWords[1].e, 1e-4); // clipped to the segment end
            Assert.IsFalse(plan.DuckMusicUnderSpeech);
        }

        [Test]
        public void PlanBuilder_EndText_LastOverlayBeforeEnd()
        {
            var post = new KionjoPost { hook = "Hook", endText = "Send this to a friend.", overlays = { "One.", "Two." }, items = { new PostItem { path = "D:/a.mp4" }, new PostItem { path = "D:/b.mp4" } } };
            var plan = PostPlanBuilder.Build(post, new FakeInfo(), null);
            var end = plan.Texts.Last();
            Assert.AreEqual("Send this to a friend.", end.Text);
            Assert.AreEqual(7.9f, end.End, 1e-4);
            Assert.AreEqual(5.9f, end.Start, 1e-4);
            // hook 0.5-2.5, One 2.7-4.7; Two would end 6.9, after the end text starts: left out
            Assert.AreEqual(3, plan.Texts.Count);
            CollectionAssert.Contains(plan.Warnings, "1 overlay(s) did not fit and were left out.");
        }
    }
}
