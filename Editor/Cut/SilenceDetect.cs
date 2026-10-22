using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Voice line trimming by silence. -55 dB, never -35 dB: in trailer 1, -35 dB cut the tails of
    /// words ("the Curator line felt cut short"). Trims only leading and trailing silence.
    /// </summary>
    public static class SilenceDetect
    {
        public const float NoiseDb = -55f;
        public const float MinSilence = 0.1f;

        public struct Range
        {
            public float Start, End;
        }

        public static FfmpegCommand Command(string ffmpeg, string audioPath) =>
            new FfmpegCommand { Executable = ffmpeg, Label = "Silence detect" }
                .Add("-hide_banner", "-nostats", "-i", audioPath, "-af",
                    $"silencedetect=noise={NoiseDb.ToString(CultureInfo.InvariantCulture)}dB:d={MinSilence.ToString(CultureInfo.InvariantCulture)}", "-f", "null", "-");

        static readonly Regex StartRx = new(@"silence_start:\s*(-?[0-9.]+)", RegexOptions.Compiled);
        static readonly Regex EndRx = new(@"silence_end:\s*(-?[0-9.]+)", RegexOptions.Compiled);

        /// <summary>Pairs silence_start and silence_end lines; an open start runs to the end (float.MaxValue).</summary>
        public static List<Range> ParseSilences(string stderr)
        {
            var list = new List<Range>();
            float? open = null;
            foreach (var line in (stderr ?? "").Split('\n'))
            {
                var s = StartRx.Match(line);
                if (s.Success)
                {
                    open = float.Parse(s.Groups[1].Value, CultureInfo.InvariantCulture);
                    continue;
                }
                var e = EndRx.Match(line);
                if (e.Success)
                {
                    list.Add(new Range { Start = open ?? 0f, End = float.Parse(e.Groups[1].Value, CultureInfo.InvariantCulture) });
                    open = null;
                }
            }
            if (open.HasValue) list.Add(new Range { Start = open.Value, End = float.MaxValue });
            return list;
        }

        /// <summary>Speech start = end of a silence that starts at the top; speech end = start of a silence that reaches the end.</summary>
        public static (float speechStart, float speechEnd) SpeechBounds(List<Range> silences, float duration)
        {
            float start = 0f, end = duration;
            foreach (var r in silences)
            {
                if (r.Start <= 0.02f) start = System.Math.Max(start, r.End);
                if (r.End >= duration - 0.02f) end = System.Math.Min(end, r.Start);
            }
            if (end <= start) return (0f, duration); // all silence: keep the line as it is
            return (start, end);
        }

        /// <summary>
        /// The resolver the cut uses for Silence and Words lines: runs ffmpeg once per file and
        /// caches by path and write time for the rest of the session.
        /// </summary>
        public static CutPlanBuilder.SpeechBoundsResolver Resolver(string ffmpeg, IMediaProbe probe = null)
        {
            probe ??= new FfprobeMediaProbe();
            return line =>
            {
                if (ffmpeg == null || string.IsNullOrEmpty(line.audioPath) || !File.Exists(line.audioPath)) return null;
                var key = $"{line.audioPath}|{File.GetLastWriteTimeUtc(line.audioPath).Ticks}";
                if (Cache.TryGetValue(key, out var hit)) return hit;
                float duration = probe.DurationSeconds(line.audioPath);
                if (duration <= 0f) return null;
                var result = KionjoProcess.Run(Command(ffmpeg, line.audioPath), 60000);
                if (result.ExitCode != 0) return null;
                var bounds = SpeechBounds(ParseSilences(result.StdErr), duration);
                Cache[key] = bounds;
                return bounds;
            };
        }

        static readonly Dictionary<string, (float, float)> Cache = new();
    }
}
