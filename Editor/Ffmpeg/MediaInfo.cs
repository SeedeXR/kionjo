using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>Length, picture size and audio presence of a media file; faked in tests.</summary>
    public interface IMediaInfo
    {
        /// <summary>Seconds; 0 for a still image, -1 when unknown.</summary>
        float Duration(string path);
        (int w, int h) Size(string path);
        bool HasAudio(string path);
    }

    /// <summary>ffprobe based media info, one probe per file, cached by path and write time.</summary>
    public sealed class FfprobeMediaInfo : IMediaInfo
    {
        [Serializable] class Stream { public string codec_type; public int width, height; }
        [Serializable] class Format { public string duration; }
        [Serializable] class Probe { public List<Stream> streams = new(); public Format format; }

        struct Info
        {
            public long Ticks;
            public float Seconds;
            public int W, H;
            public bool Audio;
        }

        readonly string _ffprobe;
        readonly Dictionary<string, Info> _cache = new();

        public FfprobeMediaInfo(string ffprobe = null) => _ffprobe = ffprobe ?? FfmpegLocator.FindFfprobe();

        public float Duration(string path) => Get(path).Seconds;
        public (int w, int h) Size(string path) => (Get(path).W, Get(path).H);
        public bool HasAudio(string path) => Get(path).Audio;

        Info Get(string path)
        {
            var info = new Info { Seconds = -1f };
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || _ffprobe == null) return info;
            long ticks = File.GetLastWriteTimeUtc(path).Ticks;
            lock (_cache)
                if (_cache.TryGetValue(path, out var hit) && hit.Ticks == ticks) return hit;
            var result = KionjoProcess.Run(new FfmpegCommand
            {
                Executable = _ffprobe,
                Args = { "-v", "error", "-show_entries", "stream=codec_type,width,height:format=duration", "-of", "json", path },
            }, 20000);
            info.Ticks = ticks;
            if (result.ExitCode == 0)
            {
                var probe = JsonUtility.FromJson<Probe>(result.StdOut);
                foreach (var s in probe?.streams ?? new List<Stream>())
                {
                    if (s.codec_type == "video" && info.W == 0)
                    {
                        info.W = s.width;
                        info.H = s.height;
                    }
                    if (s.codec_type == "audio") info.Audio = true;
                }
                // A still image has a video stream and no meaningful duration.
                info.Seconds = float.TryParse(probe?.format?.duration, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && !IsImage(path) ? d : IsImage(path) ? 0f : -1f;
            }
            lock (_cache) _cache[path] = info;
            return info;
        }

        public static bool IsImage(string path)
        {
            var ext = Path.GetExtension(path ?? "").ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp" || ext == ".bmp" || ext == ".tif" || ext == ".tiff";
        }
    }
}
