using System.Linq;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;

namespace SeedeXR.Kionjo.Tests
{
    public class CutCommandBuilderTests
    {
        static CutPlan Plan()
        {
            var p = new CutPlan
            {
                Width = 1280, Height = 720, Fps = 30, BaseName = "Trailer",
                EncoderArgs = new[] { "-c:v", "libx264", "-crf", "23", "-preset", "veryfast" },
            };
            return p;
        }

        [Test]
        public void Timestamp_Formats_HoursMinutesSecondsComma() => Assert.AreEqual("00:01:09,600", SrtWriter.Timestamp(69.6f));

        [Test]
        public void Write_TwoCues_ExactText()
        {
            var srt = SrtWriter.Write(new[]
            {
                new SubtitleCue { Start = 0.5f, End = 7.1f, Text = "This is a time when East Africa is a federation.", OverrideTags = "{\\an5}{\\fad(500,1100)}" },
                new SubtitleCue { Start = 21.5f, End = 26.01f, Text = "Why here?" },
            });
            Assert.AreEqual(
                "1\n00:00:00,500 --> 00:00:07,100\n{\\an5}{\\fad(500,1100)}This is a time when East Africa is a federation.\n\n" +
                "2\n00:00:21,500 --> 00:00:26,010\nWhy here?\n\n", srt);
        }

        [Test]
        public void Segment_Video_ArgsExact()
        {
            var p = Plan();
            var s = new CutSegment { Label = "01_S03", Kind = SegmentKind.Video, SourcePath = "D:/T/S03_take06.mp4", SourceIn = 0.5f, Duration = 6f, FadeIn = 3f };
            CollectionAssert.AreEqual(new[]
            {
                "-v", "error", "-y", "-ss", "0.5", "-t", "6", "-i", "D:/T/S03_take06.mp4", "-an",
                "-vf", "scale=1280:720,fps=30,format=yuv420p,fade=t=in:st=0:d=3",
                "-c:v", "libx264", "-crf", "23", "-preset", "veryfast", "seg/01_S03.mp4",
            }, CutCommandBuilder.Segment(s, p).Args);
        }

        [Test]
        public void Segment_FadeOut_StartsBeforeEnd()
        {
            var s = new CutSegment { Label = "13_E03", Kind = SegmentKind.Video, SourcePath = "x.mp4", Duration = 3f, FadeOut = 1f };
            StringAssert.EndsWith("fade=t=out:st=2:d=1", CutCommandBuilder.VideoFilter(s, Plan()));
        }

        [Test]
        public void Segment_Crop_IsNormalised()
        {
            var s = new CutSegment { Kind = SegmentKind.Video, Duration = 1f, Crop = new UnityEngine.Rect(0f, 0.1f, 0.75f, 0.75f) };
            StringAssert.StartsWith("crop=iw*0.75:ih*0.75:iw*0:ih*0.1,scale=1280:720", CutCommandBuilder.VideoFilter(s, Plan()));
        }

        [Test]
        public void Segment_Black_UsesLavfi()
        {
            var args = CutCommandBuilder.Segment(new CutSegment { Label = "00_C01", Kind = SegmentKind.Black, Duration = 7.5f }, Plan()).Args;
            CollectionAssert.IsSubsetOf(new[] { "-f", "lavfi", "color=c=black:s=1280x720:r=30:d=7.5" }, args);
        }

        [Test]
        public void Segment_Text_ReadsLinesFromFiles()
        {
            var p = Plan();
            var s = new CutSegment { Label = "14_C02", Kind = SegmentKind.Text, Duration = 5f, Lines = { "You will be judged by why.", "Nothing's: 100%" } };
            p.Segments.Add(s);
            var vf = CutCommandBuilder.TextCardFilter(s, p);
            StringAssert.Contains("textfile=seg/14_C02_line1.txt:expansion=none", vf);
            StringAssert.Contains("y=(h/2)-44:enable='between(t,0.4,4.6)'", vf); // trailer 1 first line
            StringAssert.Contains("y=(h/2)+14:enable='between(t,1.9,4.6)'", vf); // trailer 1 second line
            StringAssert.Contains("alpha='if(lt(t,1),(t-0.4)/0.6,if(gt(t,4),(4.6-t)/0.6,1))'", vf);
            Assert.IsFalse(vf.Contains("Nothing"), "text must not be inside the filtergraph");
            Assert.AreEqual("Nothing's: 100%", CutCommandBuilder.TextFiles(p)["seg/14_C02_line2.txt"]);
        }

        [Test]
        public void Segment_Image_PushIn()
        {
            var s = new CutSegment { Label = "15_C03", Kind = SegmentKind.Image, SourcePath = "cover.png", Duration = 5f, PushIn = 1.04f, FadeIn = 1f, FadeOut = 0.8f };
            var vf = CutCommandBuilder.ImageFilter(s, Plan());
            StringAssert.Contains("zoompan=z='min(1.04,1+0.00026667*on)'", vf);
            StringAssert.Contains("fade=t=out:st=4.2:d=0.8", vf);
        }

        [Test]
        public void Concat_ListUsesRelativeSegPaths()
        {
            var p = Plan();
            p.Segments.Add(new CutSegment { Label = "01_C01" });
            p.Segments.Add(new CutSegment { Label = "02_S01" });
            Assert.AreEqual("file 'seg/01_C01.mp4'\nfile 'seg/02_S01.mp4'\n", CutCommandBuilder.ConcatList(p));
            Assert.AreEqual("video.mp4", CutCommandBuilder.Concat(p).Args.Last()); // no subtitles: straight to video.mp4
        }

        [Test]
        public void BurnSubtitles_ForceStyleExact()
        {
            var p = Plan();
            p.SubtitleStyle = new KionjoSubtitleStyle { fontName = "Darker Grotesque SemiBold", size = 15, colour = "&H00EDEDED", outline = 0, marginV = 12 };
            p.Subtitles.Add(new SubtitleCue { Start = 0, End = 1, Text = "x" });
            var args = CutCommandBuilder.BurnSubtitles(p).Args;
            CollectionAssert.Contains(args,
                "subtitles=subs.srt:fontsdir=.:force_style='FontName=Darker Grotesque SemiBold,FontSize=15,PrimaryColour=&H00EDEDED,BorderStyle=1,Outline=0,Shadow=0,MarginV=12'");
            Assert.AreEqual("video_nosub.mp4", CutCommandBuilder.Concat(p).Args.Last());
        }

        [Test]
        public void Mux_NoAudio_CopiesVideo() =>
            CollectionAssert.AreEqual(new[] { "-v", "error", "-y", "-i", "video.mp4", "-c", "copy", "-movflags", "+faststart", "T_draft01.mp4" },
                CutCommandBuilder.Mux(Plan(), "T_draft01.mp4").Args);

        [Test]
        public void Build_Order_IsSegmentsConcatSubsAudioMux()
        {
            var p = Plan();
            p.Segments.Add(new CutSegment { Label = "01_C01", Kind = SegmentKind.Black, Duration = 1 });
            p.Subtitles.Add(new SubtitleCue { Start = 0, End = 1, Text = "x" });
            p.Voice.Add(new AudioCue { Path = "a.wav", TrimOut = 1 });
            var labels = CutCommandBuilder.Build(p, "D:/cut", "ffmpeg", "out.mp4").Select(c => c.Label).ToArray();
            CollectionAssert.AreEqual(new[] { "Segment 01_C01", "Join segments", "Burn subtitles", "Mix audio", "Mux" }, labels);
        }

        [Test]
        public void SourceWithSpacesAndBrackets_IsOneArg()
        {
            var src = "D:/BERNARD/UNITY PROJECTS (GAMES)/x [1].mp4";
            var args = CutCommandBuilder.Segment(new CutSegment { Label = "01_S01", Kind = SegmentKind.Video, SourcePath = src, Duration = 1 }, Plan()).Args;
            Assert.AreEqual(src, args[args.IndexOf("-i") + 1]);
        }

        [Test]
        public void AudioGraph_TwoVoiceLines_FilterExact()
        {
            var p = Plan();
            p.Segments.Add(new CutSegment { Duration = 30f });
            p.Voice.Add(new AudioCue { Path = "welcome.wav", TrimIn = 0f, TrimOut = 6.6f, FilmAt = 0.5f, FadeIn = 0.03f, FadeOut = 0.25f });
            p.Voice.Add(new AudioCue { Path = "whyhere.wav", TrimIn = 0f, TrimOut = 4.51f, FilmAt = 21.5f, FadeIn = 0.03f, FadeOut = 0.25f });
            var args = CutCommandBuilder.AudioGraph(p).Args;
            var f = args[args.IndexOf("-filter_complex") + 1];
            Assert.AreEqual(
                "[0:a]aformat=sample_rates=48000:channel_layouts=stereo,afade=t=in:d=0.03,afade=t=out:st=6.35:d=0.25,adelay=500|500[v0];" +
                "[1:a]aformat=sample_rates=48000:channel_layouts=stereo,afade=t=in:d=0.03,afade=t=out:st=4.26:d=0.25,adelay=21500|21500[v1];" +
                "[v0][v1]amix=inputs=2:normalize=0,apad,atrim=0:30[vo];" +
                "[vo]loudnorm=I=-14:TP=-1.5:LRA=11[a]", f);
            CollectionAssert.IsSubsetOf(new[] { "-ss", "0", "-t", "6.6", "-i", "welcome.wav" }, args);
        }

        [Test]
        public void AudioGraph_NoCues_ReturnsNull() => Assert.IsNull(CutCommandBuilder.AudioGraph(Plan()));

        [Test]
        public void Music_TwoCues_CrossfadeDuckedUnderVoice()
        {
            var p = Plan();
            p.Segments.Add(new CutSegment { Duration = 86f });
            p.MusicCrossfade = 2f;
            p.Voice.Add(new AudioCue { Path = "v.wav", TrimOut = 1f, FilmAt = 10f, FadeIn = 0.03f, FadeOut = 0.25f });
            p.Music.Add(new AudioCue { Path = "m.mp3", TrimIn = 0f, TrimOut = 60.5f, FilmAt = 7.5f, FadeIn = 1.5f, FadeOut = 2f, Volume = 0.5f });
            p.Music.Add(new AudioCue { Path = "m.mp3", TrimIn = 98.5f, TrimOut = 118.5f });
            var args = CutCommandBuilder.AudioGraph(p).Args;
            var f = args[args.IndexOf("-filter_complex") + 1];
            StringAssert.Contains("[1:a]aformat=sample_rates=48000:channel_layouts=stereo,atrim=0:60.5,asetpts=PTS-STARTPTS[m0];", f);
            StringAssert.Contains("[2:a]aformat=sample_rates=48000:channel_layouts=stereo,atrim=98.5:118.5,asetpts=PTS-STARTPTS[m1];", f);
            // joined music is 60.5 + 20 - 2 = 78.5 s from film 7.5 s: it ends at 86 s, so the fade out starts at 76.5 s into it
            StringAssert.Contains("[m0][m1]acrossfade=d=2:c1=tri:c2=tri,afade=t=in:d=1.5,afade=t=out:st=76.5:d=2,volume=0.5,adelay=7500|7500,apad,atrim=0:86[mus];", f);
            StringAssert.Contains("asplit[vo][key];", f);
            StringAssert.Contains("[mus][key]sidechaincompress=threshold=0.05:ratio=3:attack=40:release=700[duck];", f);
            StringAssert.EndsWith("[duck][vo]amix=inputs=2:normalize=0,loudnorm=I=-14:TP=-1.5:LRA=11[a]", f);
        }

        [Test]
        public void Sfx_TwoCues_AdelayMilliseconds()
        {
            var p = Plan();
            p.Segments.Add(new CutSegment { Duration = 86f });
            p.Sfx.Add(new AudioCue { Path = "open.wav", FilmAt = 52.07f, Volume = 0.9f });
            p.Sfx.Add(new AudioCue { Path = "open.wav", FilmAt = 55.01f, Volume = 0.9f });
            var args = CutCommandBuilder.AudioGraph(p).Args;
            var f = args[args.IndexOf("-filter_complex") + 1];
            StringAssert.Contains("volume=0.9,adelay=52070|52070[x0]", f);
            StringAssert.Contains("volume=0.9,adelay=55010|55010[x1]", f);
            StringAssert.EndsWith("[sfx]loudnorm=I=-14:TP=-1.5:LRA=11[a]", f);
        }

        [Test]
        public void MusicOnly_NoSidechain()
        {
            var p = Plan();
            p.Segments.Add(new CutSegment { Duration = 10f });
            p.Music.Add(new AudioCue { Path = "m.mp3", TrimOut = 30f, FadeIn = 1f, FadeOut = 2f, Volume = 0.5f });
            var f = CutCommandBuilder.AudioGraph(p).Args.First(a => a.Contains("loudnorm"));
            Assert.IsFalse(f.Contains("sidechaincompress"));
            StringAssert.Contains("[m0]afade=t=in:d=1,afade=t=out:st=8:d=2", f);
        }
    }
}
