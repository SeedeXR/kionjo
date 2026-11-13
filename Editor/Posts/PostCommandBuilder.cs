using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Turns a PostPlan into ffmpeg steps: one 9:16 encode per segment, concat, the audio mix (with and
    /// without music), the renders (text burned with music, burned without music, clean) and the cover.
    /// Every command runs in the post folder; filtergraphs only ever name files relative to it.
    /// </summary>
    public static class PostCommandBuilder
    {
        public const string SegFolder = "seg", ListFile = "list.txt", Joined = "joined.mp4", Ass = "post.ass", Audio = "audio.wav",
            AudioNoMusic = "audio_nomusic.wav", Srt = "captions.srt", TitleFont = "title.ttf", CoverTitleFile = "cover_title.txt", Cover = "cover.jpg";

        public static readonly string[] VideoArgs =
            { "-c:v", "libx264", "-profile:v", "high", "-pix_fmt", "yuv420p", "-b:v", "12M", "-maxrate", "20M", "-bufsize", "24M", "-g", "60", "-keyint_min", "60", "-sc_threshold", "0" };

        public static readonly string[] AudioArgs = { "-c:a", "aac", "-b:a", "128k", "-ar", "48000", "-ac", "2" };

        const string Stereo48 = "aformat=sample_rates=48000:channel_layouts=stereo";
        const string Loudness = "loudnorm=I=-14:TP=-1:LRA=11";

        static string F(float v) => CutCommandBuilder.F(v);
        static string I(int v) => v.ToString(CultureInfo.InvariantCulture);

        public static string MainFile(PostPlan p) => $"{p.Name}_9x16.mp4";
        public static string NoMusicFile(PostPlan p) => $"{p.Name}_nomusic_9x16.mp4";
        public static string CleanFile(PostPlan p) => $"{p.Name}_clean_9x16.mp4";
        public static string SegmentFile(PostSegment s) => $"{SegFolder}/{s.Label}.mp4";

        static FfmpegCommand Cmd(string label, float seconds) =>
            new FfmpegCommand { Label = label, ExpectedSeconds = seconds }.Add("-v", "error", "-y");

        /// <summary>
        /// Fits a segment to the frame: plain scale when the aspect already matches, else a crop window
        /// (horizontal, at CropCentre) or a blurred fill behind the whole picture. Holds the last frame
        /// when the source ends early. A blur fill is a filter_complex ending in [v].
        /// </summary>
        public static string FitFilter(PostSegment s, PostPlan p)
        {
            int W = p.Width, H = p.Height;
            string tail = $",setsar=1,fps={I(p.Fps)},format=yuv420p,tpad=stop_mode=clone:stop_duration={F(s.Duration)}";
            if (s.SrcW <= 0 || s.SrcH <= 0) return $"scale={W}:{H}:force_original_aspect_ratio=increase,crop={W}:{H}{tail}";
            float src = s.SrcW / (float)s.SrcH, target = W / (float)H;
            if (Mathf.Abs(src - target) < 0.01f) return $"scale={W}:{H}{tail}";
            if (s.Fit == PostFit.BlurFill)
                return $"[0:v]split[bg][fg];[bg]scale={W}:{H}:force_original_aspect_ratio=increase,crop={W}:{H},boxblur=20:2[b];" +
                       $"[fg]scale={W}:{H}:force_original_aspect_ratio=decrease:force_divisible_by=2[f];[b][f]overlay=(W-w)/2:(H-h)/2{tail}[v]";
            if (src > target)
                return $"crop=ih*{W}/{H}:ih:(iw-ih*{W}/{H})*{F(Mathf.Clamp01(s.CropCentre))}:0,scale={W}:{H}{tail}";
            // Taller than the frame: crop top and bottom evenly.
            return $"crop=iw:iw*{H}/{W}:0:(ih-iw*{H}/{W})/2,scale={W}:{H}{tail}";
        }

        public static FfmpegCommand Segment(PostSegment s, PostPlan p)
        {
            var c = Cmd($"Segment {s.Label}", s.Duration);
            if (FfprobeMediaInfo.IsImage(s.SourcePath))
                c.Add("-loop", "1", "-framerate", I(p.Fps), "-t", F(s.Duration), "-i", s.SourcePath);
            else
                c.Add("-ss", F(s.SourceIn), "-t", F(s.Duration), "-i", s.SourcePath);
            c.Add("-an");
            var filter = FitFilter(s, p);
            if (filter.EndsWith("[v]")) c.Add("-filter_complex", filter, "-map", "[v]");
            else c.Add("-vf", filter);
            c.Add("-frames:v", I(Mathf.Max(1, Mathf.RoundToInt(s.Duration * p.Fps))));
            c.Add(VideoArgs);
            c.Add(SegmentFile(s));
            return c;
        }

        public static string ConcatList(PostPlan p)
        {
            var sb = new StringBuilder();
            foreach (var s in p.Segments) sb.Append("file '").Append(SegmentFile(s)).Append("'\n");
            return sb.ToString();
        }

        public static FfmpegCommand Concat(PostPlan p) =>
            Cmd("Join segments", p.TotalSeconds).Add("-f", "concat", "-safe", "0", "-i", ListFile, "-c", "copy", Joined);

        /// <summary>
        /// Clip sound per segment (silence where a clip has none or clip sound is off), joined and set to
        /// ClipVolume; music trimmed from MusicStart, faded out over the last second and ducked under the
        /// clips when they carry speech; mixed and normalised to -14 LUFS. Null when there is nothing to hear.
        /// </summary>
        public static FfmpegCommand AudioGraph(PostPlan p, bool withMusic)
        {
            bool music = withMusic && p.HasMusic;
            if (!p.ClipSound && !music) return null;
            float total = p.TotalSeconds;
            string T = F(total);
            var c = Cmd(withMusic ? "Mix audio" : "Mix audio without music", total);
            var f = new StringBuilder();
            var labels = new StringBuilder();
            for (int i = 0; i < p.Segments.Count; i++)
            {
                var s = p.Segments[i];
                string D = F(s.Duration);
                if (p.ClipSound && s.HasAudio) c.Add("-ss", F(s.SourceIn), "-t", D, "-i", s.SourcePath);
                else c.Add("-f", "lavfi", "-t", D, "-i", "anullsrc=r=48000:cl=stereo");
                f.Append($"[{i}:a]{Stereo48},apad,atrim=0:{D},asetpts=PTS-STARTPTS[c{i}];");
                labels.Append($"[c{i}]");
            }
            bool duck = music && p.DuckMusicUnderSpeech;
            f.Append($"{labels}concat=n={p.Segments.Count}:v=0:a=1,volume={F(p.ClipVolume)}");
            f.Append(duck ? ",asplit[clip][key];" : "[clip];");
            var finals = "[clip]";
            int inputs = 1;
            if (music)
            {
                c.Add("-i", p.MusicPath);
                f.Append($"[{p.Segments.Count}:a]{Stereo48},atrim={F(p.MusicStart)}:{F(p.MusicStart + total)},asetpts=PTS-STARTPTS,volume={F(p.MusicVolume)}," +
                         $"afade=t=out:st={F(Mathf.Max(0f, total - 1f))}:d=1,apad,atrim=0:{T}[mus];");
                if (duck) f.Append("[mus][key]sidechaincompress=threshold=0.05:ratio=8:attack=20:release=400[duck];");
                finals += duck ? "[duck]" : "[mus]";
                inputs = 2;
            }
            f.Append(inputs > 1 ? $"{finals}amix=inputs={inputs}:normalize=0,{Loudness}[a]" : $"{finals}{Loudness}[a]");
            c.Add("-filter_complex", f.ToString(), "-map", "[a]", "-c:a", "pcm_s16le", "-ar", "48000", withMusic ? Audio : AudioNoMusic);
            return c;
        }

        /// <summary>The joined picture with the audio file; text burned through libass, or the video copied as is.</summary>
        public static FfmpegCommand Render(PostPlan p, bool burnText, string audioFile, string output)
        {
            var c = Cmd($"Render {output}", p.TotalSeconds).Add("-i", Joined);
            if (audioFile != null) c.Add("-i", audioFile);
            c.Add("-map", "0:v");
            if (audioFile != null) c.Add("-map", "1:a");
            if (burnText) c.Add("-vf", $"ass={Ass}:fontsdir=.").Add(VideoArgs);
            else c.Add("-c:v", "copy");
            if (audioFile != null) c.Add(AudioArgs);
            c.Add("-shortest", "-movflags", "+faststart", output);
            return c;
        }

        /// <summary>A darkened (and desaturated) frame with the title in the kit accent at mid height. No title when titleFile is null.</summary>
        public static FfmpegCommand CoverCommand(PostPlan p, string source, float time, string titleFile, KionjoBrandKit kit)
        {
            int W = p.Width, H = p.Height;
            var filter = new StringBuilder($"scale={W}:{H}:force_original_aspect_ratio=increase,crop={W}:{H}");
            if (kit == null || kit.coverDesaturate) filter.Append(",hue=s=0");
            filter.Append(",eq=brightness=-0.08");
            if (titleFile != null)
                filter.Append($",drawtext=fontfile={TitleFont}:textfile={titleFile}:expansion=none:fontcolor={KionjoBrandKit.FfmpegColour(kit != null ? kit.accentColour : "#4FD1E8")}" +
                              $":fontsize={I(Mathf.RoundToInt(H * 0.075f))}:x=(w-tw)/2:y=h*0.5-th/2");
            return Cmd("Cover", 0f).Add("-ss", F(time), "-i", source, "-frames:v", "1", "-update", "1", "-vf", filter.ToString(), "-q:v", "2", Cover);
        }

        /// <summary>All steps in order. The cover is taken from coverSource (normally the joined picture, before any text).</summary>
        public static List<FfmpegCommand> Build(PostPlan p, string folder, string ffmpeg, string coverSource)
        {
            var list = new List<FfmpegCommand>();
            foreach (var s in p.Segments) list.Add(Segment(s, p));
            list.Add(Concat(p));
            var audio = AudioGraph(p, true);
            if (audio != null) list.Add(audio);
            FfmpegCommand noMusic = p.HasMusic ? AudioGraph(p, false) : null;
            if (noMusic != null) list.Add(noMusic);
            string a = audio != null ? Audio : null;
            list.Add(Render(p, true, a, MainFile(p)));
            if (p.HasMusic) list.Add(Render(p, true, noMusic != null ? AudioNoMusic : null, NoMusicFile(p)));
            list.Add(Render(p, false, a, CleanFile(p)));
            list.Add(CoverCommand(p, coverSource, p.CoverTime, string.IsNullOrWhiteSpace(p.CoverTitle) ? null : CoverTitleFile, p.Kit));
            foreach (var c in list)
            {
                c.Executable = ffmpeg;
                c.WorkingDirectory = folder;
            }
            return list;
        }
    }
}
