using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Word timings through Tools~/kionjo_words.py (faster-whisper, optional). Results are cached
    /// under Library/KionjoCache/words by file path and write time, so a line is transcribed once.
    /// </summary>
    public static class WordTimer
    {
        [Serializable]
        public struct Word
        {
            public string w;
            public float s, e;
        }

        [Serializable]
        class Result
        {
            public List<Word> words = new();
            public string error;
        }

        public const string ScriptPath = "Packages/com.seedexr.kionjo/Tools~/kionjo_words.py";
        static string CacheFolder => Path.Combine(KionjoPaths.ProjectRoot, "Library", "KionjoCache", "words");

        public static List<Word> Parse(string json, out string error)
        {
            var r = JsonUtility.FromJson<Result>(json ?? "{}") ?? new Result();
            error = string.IsNullOrEmpty(r.error) ? null : r.error;
            return r.words ?? new List<Word>();
        }

        /// <summary>Word timings of a file, or null with a reason (no Python, no faster-whisper, failed).</summary>
        public static List<Word> Words(string audioPath, out string reason)
        {
            reason = null;
            var key = $"{Path.GetFileName(audioPath)}_{File.GetLastWriteTimeUtc(audioPath).Ticks:x}_{audioPath.GetHashCode():x}.json";
            var cache = Path.Combine(CacheFolder, key);
            if (File.Exists(cache)) return Parse(File.ReadAllText(cache), out reason);

            var script = Path.GetFullPath(ScriptPath);
            if (!File.Exists(script))
            {
                reason = "kionjo_words.py is missing from the package.";
                return null;
            }
            var ffmpeg = FfmpegLocator.FindFfmpeg() ?? "ffmpeg";
            var cmd = new FfmpegCommand { Executable = KionjoPreferences.PythonPath, Label = "Word timings" }
                .Add("-I", script, audioPath, "--ffmpeg", ffmpeg);
            KionjoProcess.Result run;
            try
            {
                run = KionjoProcess.Run(cmd, 300000);
            }
            catch (Exception e)
            {
                reason = $"Python not found ({e.Message}). Set it in Preferences > Kionjo.";
                return null;
            }
            var words = Parse(run.StdOut.Trim(), out reason);
            if (run.ExitCode != 0)
            {
                reason ??= $"kionjo_words.py failed ({run.ExitCode}): {run.StdErrTail}";
                return null;
            }
            Directory.CreateDirectory(CacheFolder);
            File.WriteAllText(cache, run.StdOut.Trim());
            return words;
        }

        /// <summary>Start of the first kept word and end of the last kept word; null when out of range.</summary>
        public static (float start, float end)? Bounds(List<Word> words, int firstWord, int lastWord)
        {
            if (words == null || words.Count == 0) return null;
            int first = Mathf.Clamp(firstWord, 0, words.Count - 1);
            int last = lastWord < 0 ? words.Count - 1 : Mathf.Clamp(lastWord, first, words.Count - 1);
            return (words[first].s, words[last].e);
        }

        /// <summary>
        /// The speech resolver for the cut. Word timings decide only where a trim falls inside the
        /// line; at the line's own start and end the -55 dB silence edge wins, because whisper ends
        /// words early (trailer 1: "Justice?" ends at 4.0 s, whisper said 3.5 s, and the clipped tail
        /// was audible). Falls back to silence detection when faster-whisper is unavailable.
        /// </summary>
        public static CutPlanBuilder.SpeechBoundsResolver Resolver(string ffmpeg)
        {
            var silence = SilenceDetect.Resolver(ffmpeg);
            return line =>
            {
                var edges = silence(line);
                if (line.trimMode != KionjoTrimMode.Words || string.IsNullOrEmpty(line.audioPath) || !File.Exists(line.audioPath))
                    return edges;
                var words = Words(line.audioPath, out var reason);
                if (words == null || words.Count == 0)
                {
                    if (reason != null) Debug.LogWarning($"Kionjo: word trim of {Path.GetFileName(line.audioPath)} fell back to silence: {reason}");
                    return edges;
                }
                return Combine(words, line.firstWord, line.lastWord, edges);
            };
        }

        /// <summary>Word bounds inside the line, silence edges at the line's own first and last word.</summary>
        public static (float start, float end)? Combine(List<Word> words, int firstWord, int lastWord, (float start, float end)? silenceEdges)
        {
            var b = Bounds(words, firstWord, lastWord);
            if (!b.HasValue) return silenceEdges;
            int first = Mathf.Clamp(firstWord, 0, words.Count - 1);
            int last = lastWord < 0 ? words.Count - 1 : Mathf.Clamp(lastWord, first, words.Count - 1);
            float start = first == 0 && silenceEdges.HasValue ? silenceEdges.Value.start : Mathf.Max(0f, b.Value.start - 0.05f);
            float end = last == words.Count - 1 && silenceEdges.HasValue
                ? Mathf.Max(silenceEdges.Value.end, b.Value.end)
                : last + 1 < words.Count ? Mathf.Min(b.Value.end + 0.12f, words[last + 1].s) : b.Value.end + 0.12f;
            return (start, end);
        }
    }
}
