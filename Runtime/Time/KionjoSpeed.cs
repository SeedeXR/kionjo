using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeedeXR.Kionjo
{
    /// <summary>One point of a shot's speed curve: at <see cref="t"/> seconds into the render, time runs at <see cref="speed"/>.</summary>
    [Serializable]
    public struct KionjoSpeedKey
    {
        [Tooltip("Seconds into the rendered shot.")]
        public float t;
        [Tooltip("1 = real time, 0.2 = slow motion, 0 = frozen (bullet time: the camera keeps moving).")]
        public float speed;
    }

    /// <summary>
    /// The speed curve of a shot (time remap): eased between keys, held before the first key and after the last.
    /// No keys means real time. Output time is the rendered video's; source time is the take's and the game's.
    /// </summary>
    public static class KionjoSpeed
    {
        public static bool IsRealTime(IReadOnlyList<KionjoSpeedKey> keys) => keys == null || keys.Count == 0;

        /// <summary>Speed at <paramref name="t"/> output seconds. Keys are in ascending t.</summary>
        public static float Evaluate(IReadOnlyList<KionjoSpeedKey> keys, float t)
        {
            if (IsRealTime(keys)) return 1f;
            if (t <= keys[0].t) return Mathf.Max(0f, keys[0].speed);
            for (int i = 1; i < keys.Count; i++)
            {
                if (t > keys[i].t) continue;
                var a = keys[i - 1];
                var b = keys[i];
                float u = b.t > a.t ? (t - a.t) / (b.t - a.t) : 1f;
                return Mathf.Max(0f, Mathf.Lerp(a.speed, b.speed, u * u * (3f - 2f * u)));
            }
            return Mathf.Max(0f, keys[keys.Count - 1].speed);
        }

        /// <summary>Source seconds played after <paramref name="frames"/> output frames: frame k advances speed(k / fps) / fps.</summary>
        public static double SourceSeconds(IReadOnlyList<KionjoSpeedKey> keys, int frames, float fps)
        {
            if (IsRealTime(keys)) return frames / (double)fps;
            double s = 0;
            for (int k = 0; k < frames; k++) s += Evaluate(keys, k / fps) / (double)fps;
            return s;
        }

        /// <summary>Output frames needed to play <paramref name="sourceSeconds"/> of take, capped (a freeze that never ends).</summary>
        public static int FramesFor(IReadOnlyList<KionjoSpeedKey> keys, float sourceSeconds, float fps, int maxFrames = 216000)
        {
            const double eps = 1e-4;
            if (IsRealTime(keys)) return Mathf.Clamp(Mathf.CeilToInt(sourceSeconds * fps - 1e-3f), 1, maxFrames);
            double s = 0;
            for (int n = 0; n < maxFrames; n++)
            {
                if (s >= sourceSeconds - eps) return Mathf.Max(1, n);
                s += Evaluate(keys, n / fps) / (double)fps;
            }
            return maxFrames;
        }
    }
}
