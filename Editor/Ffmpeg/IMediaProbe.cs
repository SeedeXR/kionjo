using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>Media length lookup, faked in tests so the cut plan never touches disk.</summary>
    public interface IMediaProbe
    {
        /// <summary>Duration in seconds, -1 when unknown.</summary>
        float DurationSeconds(string path);
    }

    /// <summary>ffprobe based probe with a cache keyed by path and write time.</summary>
    public sealed class FfprobeMediaProbe : IMediaProbe
    {
        readonly string _ffprobe;
        readonly Dictionary<string, (long ticks, float seconds)> _cache = new();

        public FfprobeMediaProbe(string ffprobe = null) => _ffprobe = ffprobe ?? FfmpegLocator.FindFfprobe();

        public float DurationSeconds(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || _ffprobe == null) return -1f;
            long ticks = File.GetLastWriteTimeUtc(path).Ticks;
            if (_cache.TryGetValue(path, out var hit) && hit.ticks == ticks) return hit.seconds;
            var result = KionjoProcess.Run(new FfmpegCommand
            {
                Executable = _ffprobe,
                Args = { "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path },
            }, 20000);
            float seconds = -1f;
            if (result.ExitCode == 0 && float.TryParse(result.StdOut.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
                seconds = s;
            _cache[path] = (ticks, seconds);
            return seconds;
        }
    }
}
