using System;
using System.IO;
using UnityEditor;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>Finds ffmpeg: the path set in Preferences > Kionjo first, then PATH.</summary>
    public static class FfmpegLocator
    {
        public const string PrefKey = "SeedeXR.Kionjo.FfmpegPath";

        public static string PreferredPath
        {
            get => EditorPrefs.GetString(PrefKey, "");
            set => EditorPrefs.SetString(PrefKey, value ?? "");
        }

        static string ExeName(string name) =>
            Environment.OSVersion.Platform == PlatformID.Win32NT ? name + ".exe" : name;

        /// <summary>Absolute path of ffmpeg, or null when it cannot be found.</summary>
        public static string FindFfmpeg()
        {
            var preferred = PreferredPath;
            if (!string.IsNullOrEmpty(preferred))
            {
                if (File.Exists(preferred)) return preferred;
                if (Directory.Exists(preferred) && File.Exists(Path.Combine(preferred, ExeName("ffmpeg"))))
                    return Path.Combine(preferred, ExeName("ffmpeg"));
            }
            return FindOnPath(ExeName("ffmpeg"));
        }

        /// <summary>ffprobe beside ffmpeg, else on PATH, else null.</summary>
        public static string FindFfprobe()
        {
            var ffmpeg = FindFfmpeg();
            if (ffmpeg != null)
            {
                var sibling = Path.Combine(Path.GetDirectoryName(ffmpeg) ?? "", ExeName("ffprobe"));
                if (File.Exists(sibling)) return sibling;
            }
            return FindOnPath(ExeName("ffprobe"));
        }

        public static string FindOnPath(string exe)
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                try
                {
                    var candidate = Path.Combine(dir.Trim().Trim('"'), exe);
                    if (File.Exists(candidate)) return candidate;
                }
                catch (ArgumentException)
                {
                    // a malformed PATH entry: skip it
                }
            }
            return null;
        }

        /// <summary>"7.1" from "ffmpeg version 7.1-full_build-www.gyan.dev ...".</summary>
        public static bool TryGetVersion(string exe, out string version)
        {
            version = null;
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return false;
            try
            {
                var result = KionjoProcess.Run(new FfmpegCommand { Executable = exe, Args = { "-version" } }, 10000);
                if (result.ExitCode != 0) return false;
                version = ParseVersion(result.StdOut);
                return version != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string ParseVersion(string versionOutput)
        {
            if (string.IsNullOrEmpty(versionOutput)) return null;
            var first = versionOutput.Split('\n')[0];
            const string marker = "version ";
            int i = first.IndexOf(marker, StringComparison.Ordinal);
            if (i < 0) return null;
            var rest = first.Substring(i + marker.Length).Trim();
            int end = 0;
            while (end < rest.Length && (char.IsDigit(rest[end]) || rest[end] == '.')) end++;
            return end > 0 ? rest.Substring(0, end) : rest.Split(' ')[0];
        }

        static string _cachedStatus;
        static string _cachedFor;

        /// <summary>"ffmpeg 7.1 at C:\..." or "ffmpeg not found". Cached per path.</summary>
        public static string Status()
        {
            var exe = FindFfmpeg();
            if (exe == null) return "ffmpeg not found: install it or set its path in Preferences > Kionjo";
            if (_cachedFor == exe && _cachedStatus != null) return _cachedStatus;
            _cachedFor = exe;
            _cachedStatus = TryGetVersion(exe, out var v) ? $"ffmpeg {v} at {exe}" : $"ffmpeg at {exe} did not answer -version";
            return _cachedStatus;
        }

        public static bool IsAvailable => FindFfmpeg() != null;

        public static void ClearCache() => _cachedStatus = _cachedFor = null;
    }
}
