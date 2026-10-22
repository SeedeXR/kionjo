using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>SubRip subtitles. Override tags (each in its own braces) go in front of the first line.</summary>
    public static class SrtWriter
    {
        /// <summary>69.6 gives "00:01:09,600".</summary>
        public static string Timestamp(float seconds)
        {
            long ms = (long)Mathf.Round(Mathf.Max(0f, seconds) * 1000f);
            long h = ms / 3_600_000; ms -= h * 3_600_000;
            long m = ms / 60_000; ms -= m * 60_000;
            long s = ms / 1000; ms -= s * 1000;
            return $"{h:00}:{m:00}:{s:00},{ms:000}";
        }

        public static string Write(IEnumerable<SubtitleCue> cues)
        {
            var sb = new StringBuilder();
            int i = 1;
            foreach (var c in cues)
            {
                sb.Append(i++).Append('\n');
                sb.Append(Timestamp(c.Start)).Append(" --> ").Append(Timestamp(c.End)).Append('\n');
                sb.Append(c.OverrideTags ?? "").Append(c.Text).Append("\n\n");
            }
            return sb.ToString();
        }
    }
}
