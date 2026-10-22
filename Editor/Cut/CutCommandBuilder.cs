using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Turns a CutPlan into the ffmpeg steps of Baraza's trailer 1 cut: one encode per segment,
    /// concat, burned subtitles, one audio graph (voice, music with crossfade and ducking, SFX,
    /// loudness), mux. Every command runs in the cut folder; anything inside a filtergraph is a
    /// relative name, so paths with spaces, brackets or drive colons never need escaping.
    /// </summary>
    public static class CutCommandBuilder
    {
        public const string SegFolder = "seg", ListFile = "list.txt", NoSubsVideo = "video_nosub.mp4", Video = "video.mp4",
            Audio = "audio.wav", Subs = "subs.srt", TitleFont = "title.ttf", SubsFont = "subs.ttf";

        const string Stereo48 = "aformat=sample_rates=48000:channel_layouts=stereo";

        public static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        static string Ms(float seconds) => Mathf.RoundToInt(seconds * 1000f).ToString(CultureInfo.InvariantCulture);

        public static string SegmentFile(CutSegment s) => $"{SegFolder}/{s.Label}.mp4";

        /// <summary>Card line text files (relative name to content) that the text card filters read.</summary>
        public static Dictionary<string, string> TextFiles(CutPlan plan)
        {
            var files = new Dictionary<string, string>();
            foreach (var s in plan.Segments.Where(s => s.Kind == SegmentKind.Text))
                for (int i = 0; i < s.Lines.Count; i++)
                    files[LineFile(s, i)] = s.Lines[i];
            return files;
        }

        static string LineFile(CutSegment s, int i) => $"{SegFolder}/{s.Label}_line{i + 1}.txt";

        /// <summary>All steps in order: segments, concat, subtitles, audio, mux.</summary>
        public static List<FfmpegCommand> Build(CutPlan plan, string cutFolder, string ffmpegExe, string outputFile)
        {
            var list = new List<FfmpegCommand>();
            foreach (var s in plan.Segments) list.Add(Segment(s, plan));
            list.Add(Concat(plan));
            var subs = BurnSubtitles(plan);
            if (subs != null) list.Add(subs);
            var audio = AudioGraph(plan);
            if (audio != null) list.Add(audio);
            list.Add(Mux(plan, outputFile));
            foreach (var c in list)
            {
                c.Executable = ffmpegExe;
                c.WorkingDirectory = cutFolder;
            }
            return list;
        }

        static FfmpegCommand Cmd(string label, float seconds) =>
            new FfmpegCommand { Label = label, ExpectedSeconds = seconds }.Add("-v", "error", "-y");

        public static FfmpegCommand Segment(CutSegment s, CutPlan p)
        {
            var c = Cmd($"Segment {s.Label}", s.Duration);
            switch (s.Kind)
            {
                case SegmentKind.Video:
                    c.Add("-ss", F(s.SourceIn), "-t", F(s.Duration), "-i", s.SourcePath, "-an", "-vf", VideoFilter(s, p));
                    break;
                case SegmentKind.Image:
                    c.Add("-loop", "1", "-framerate", p.Fps.ToString(CultureInfo.InvariantCulture), "-t", F(s.Duration), "-i", s.SourcePath, "-vf", ImageFilter(s, p));
                    break;
                case SegmentKind.Text:
                    c.Add("-f", "lavfi", "-i", BlackSource(s, p), "-vf", TextCardFilter(s, p));
                    break;
                default:
                    c.Add("-f", "lavfi", "-i", BlackSource(s, p), "-vf", "format=yuv420p");
                    break;
            }
            c.Add(p.EncoderArgs);
            c.Add(SegmentFile(s));
            return c;
        }

        static string BlackSource(CutSegment s, CutPlan p) => $"color=c=black:s={p.Width}x{p.Height}:r={p.Fps}:d={F(s.Duration)}";

        static string Fades(CutSegment s)
        {
            var sb = new StringBuilder();
            if (s.FadeIn > 0f) sb.Append($",fade=t=in:st=0:d={F(s.FadeIn)}");
            if (s.FadeOut > 0f) sb.Append($",fade=t=out:st={F(Mathf.Max(0f, s.Duration - s.FadeOut))}:d={F(s.FadeOut)}");
            return sb.ToString();
        }

        public static string VideoFilter(CutSegment s, CutPlan p)
        {
            var crop = s.Crop.width > 0f && s.Crop.height > 0f
                ? $"crop=iw*{F(s.Crop.width)}:ih*{F(s.Crop.height)}:iw*{F(s.Crop.x)}:ih*{F(s.Crop.y)},"
                : "";
            return $"{crop}scale={p.Width}:{p.Height},fps={p.Fps},format=yuv420p{Fades(s)}";
        }

        /// <summary>Fills the frame without stretching (scale up then crop), optional slow push in, fades.</summary>
        public static string ImageFilter(CutSegment s, CutPlan p)
        {
            if (s.PushIn > 1.0001f)
            {
                int w2 = p.Width * 2, h2 = p.Height * 2;
                float step = (s.PushIn - 1f) / Mathf.Max(1f, s.Duration * p.Fps);
                return $"scale={w2}:{h2}:force_original_aspect_ratio=increase,crop={w2}:{h2}," +
                       $"zoompan=z='min({F(s.PushIn)},1+{step.ToString("0.########", CultureInfo.InvariantCulture)}*on)':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d=1:s={p.Width}x{p.Height}:fps={p.Fps}" +
                       $"{Fades(s)},format=yuv420p";
            }
            return $"scale={p.Width}:{p.Height}:force_original_aspect_ratio=increase,crop={p.Width}:{p.Height},fps={p.Fps}{Fades(s)},format=yuv420p";
        }

        /// <summary>
        /// One drawtext per line, stacked around the centre, each fading in 1.5 s after the previous
        /// one and all fading out together 0.4 s before the end (trailer 1's closing card).
        /// </summary>
        public static string TextCardFilter(CutSegment s, CutPlan p)
        {
            float k = p.Height / 720f;
            int n = s.Lines.Count;
            // Always a font file copied into the cut folder: "font=Sans" goes through fontconfig, which
            // has no configuration on Windows and crashes ffmpeg.
            var font = $"fontfile={TitleFont}";
            var sb = new StringBuilder();
            for (int i = 0; i < n; i++)
            {
                float a = 0.4f + 1.5f * i, b = Mathf.Max(a + 0.1f, s.Duration - 0.4f);
                int offset = Mathf.RoundToInt(((i - (n - 1) / 2f) * 58f - 15f) * k);
                var y = offset >= 0 ? $"(h/2)+{offset}" : $"(h/2)-{-offset}";
                sb.Append($"drawtext={font}:textfile={LineFile(s, i)}:expansion=none:fontcolor=0xD8D8D8:fontsize={Mathf.RoundToInt(34f * k)}")
                  .Append($":x=(w-tw)/2:y={y}:enable='between(t,{F(a)},{F(b)})'")
                  .Append($":alpha='if(lt(t,{F(a + 0.6f)}),(t-{F(a)})/0.6,if(gt(t,{F(b - 0.6f)}),({F(b)}-t)/0.6,1))',");
            }
            sb.Append("format=yuv420p").Append(Fades(s));
            return sb.ToString();
        }

        public static string ConcatList(CutPlan p)
        {
            var sb = new StringBuilder();
            foreach (var s in p.Segments) sb.Append("file '").Append(SegmentFile(s)).Append("'\n");
            return sb.ToString();
        }

        /// <summary>Writes video_nosub.mp4 when subtitles follow, else video.mp4 directly.</summary>
        public static FfmpegCommand Concat(CutPlan p) =>
            Cmd("Join segments", p.TotalSeconds).Add("-f", "concat", "-safe", "0", "-i", ListFile, "-c", "copy",
                p.Subtitles.Count > 0 ? NoSubsVideo : Video);

        public static string ForceStyle(KionjoSubtitleStyle st)
        {
            var name = string.IsNullOrWhiteSpace(st?.fontName) ? "Arial" : st.fontName;
            return $"FontName={name},FontSize={st?.size ?? 15},PrimaryColour={st?.colour ?? "&H00EDEDED"},BorderStyle=1,Outline={st?.outline ?? 0},Shadow=0,MarginV={st?.marginV ?? 12}";
        }

        public static FfmpegCommand BurnSubtitles(CutPlan p)
        {
            if (p.Subtitles.Count == 0) return null;
            return Cmd("Burn subtitles", p.TotalSeconds)
                .Add("-i", NoSubsVideo, "-vf", $"subtitles={Subs}:fontsdir=.:force_style='{ForceStyle(p.SubtitleStyle)}'")
                .Add(p.EncoderArgs)
                .Add(Video);
        }

        /// <summary>
        /// One filter_complex for all audio: voice lines (fade in and out, placed with adelay, mixed),
        /// music cues joined by crossfades and ducked under the voice, SFX at their times, then
        /// loudness normalisation. Null when the plan has no audio.
        /// </summary>
        public static FfmpegCommand AudioGraph(CutPlan p)
        {
            if (!p.HasAudio) return null;
            float total = p.TotalSeconds;
            string T = F(total);
            var c = Cmd("Mix audio", total);
            var f = new StringBuilder();
            int input = 0;
            bool duck = p.Voice.Count > 0 && p.Music.Count > 0 && (p.Ducking?.enabled ?? false);
            var finals = new List<string>();

            if (p.Voice.Count > 0)
            {
                var labels = new StringBuilder();
                for (int i = 0; i < p.Voice.Count; i++, input++)
                {
                    var v = p.Voice[i];
                    c.Add("-ss", F(v.TrimIn), "-t", F(v.Length), "-i", v.Path);
                    f.Append($"[{input}:a]{Stereo48},afade=t=in:d={F(v.FadeIn)},afade=t=out:st={F(Mathf.Max(0f, v.Length - v.FadeOut))}:d={F(v.FadeOut)},adelay={Ms(v.FilmAt)}|{Ms(v.FilmAt)}[v{i}];");
                    labels.Append($"[v{i}]");
                }
                f.Append($"{labels}amix=inputs={p.Voice.Count}:normalize=0,apad,atrim=0:{T}");
                f.Append(duck ? ",asplit[vo][key];" : "[vo];");
            }

            if (p.Music.Count > 0)
            {
                for (int i = 0; i < p.Music.Count; i++, input++)
                {
                    var m = p.Music[i];
                    c.Add("-i", m.Path);
                    f.Append($"[{input}:a]{Stereo48},atrim={F(m.TrimIn)}:{F(m.TrimOut)},asetpts=PTS-STARTPTS[m{i}];");
                }
                var first = p.Music[0];
                float xf = Mathf.Max(0f, p.MusicCrossfade);
                float musicLength = p.Music.Sum(m => m.Length) - xf * (p.Music.Count - 1);
                float audible = Mathf.Min(musicLength, total - first.FilmAt);
                float fadeOutAt = Mathf.Max(0f, audible - first.FadeOut);
                // Later cues follow the first, joined by crossfades; only the first cue's time places the music.
                string chain = "[m0]";
                for (int i = 1; i < p.Music.Count; i++)
                {
                    bool last = i == p.Music.Count - 1;
                    chain += $"[m{i}]acrossfade=d={F(xf)}:c1=tri:c2=tri" + (last ? "," : $"[mj{i}];[mj{i}]");
                }
                if (p.Music.Count == 1) chain = "[m0]";
                f.Append(chain)
                 .Append($"afade=t=in:d={F(first.FadeIn)},afade=t=out:st={F(fadeOutAt)}:d={F(first.FadeOut)},volume={F(first.Volume)},adelay={Ms(first.FilmAt)}|{Ms(first.FilmAt)},apad,atrim=0:{T}[mus];");
            }

            if (p.Sfx.Count > 0)
            {
                var labels = new StringBuilder();
                for (int i = 0; i < p.Sfx.Count; i++, input++)
                {
                    var s = p.Sfx[i];
                    c.Add("-i", s.Path);
                    f.Append($"[{input}:a]{Stereo48},volume={F(s.Volume)},adelay={Ms(s.FilmAt)}|{Ms(s.FilmAt)}[x{i}];");
                    labels.Append($"[x{i}]");
                }
                f.Append($"{labels}amix=inputs={p.Sfx.Count}:normalize=0,apad,atrim=0:{T}[sfx];");
            }

            if (duck)
            {
                var d = p.Ducking;
                f.Append($"[mus][key]sidechaincompress=threshold={F(d.threshold)}:ratio={F(d.ratio)}:attack={F(d.attack)}:release={F(d.release)}[duck];");
                finals.Add("[duck]");
            }
            else if (p.Music.Count > 0) finals.Add("[mus]");
            if (p.Voice.Count > 0) finals.Add("[vo]");
            if (p.Sfx.Count > 0) finals.Add("[sfx]");

            var l = p.Loudness ?? new KionjoLoudness();
            string loud = $"loudnorm=I={F(l.integrated)}:TP={F(l.truePeak)}:LRA={F(l.range)}";
            f.Append(string.Concat(finals));
            f.Append(finals.Count > 1 ? $"amix=inputs={finals.Count}:normalize=0,{loud}[a]" : $"{loud}[a]");

            c.Add("-filter_complex", f.ToString(), "-map", "[a]", "-c:a", "pcm_s16le", "-ar", "48000", Audio);
            return c;
        }

        public static FfmpegCommand Mux(CutPlan p, string outputFile)
        {
            var c = Cmd("Mux", p.TotalSeconds);
            if (p.HasAudio)
                c.Add("-i", Video, "-i", Audio, "-map", "0:v", "-map", "1:a", "-c:v", "copy", "-c:a", "aac", "-b:a", "192k", "-shortest", "-movflags", "+faststart", outputFile);
            else
                c.Add("-i", Video, "-c", "copy", "-movflags", "+faststart", outputFile);
            return c;
        }
    }
}
