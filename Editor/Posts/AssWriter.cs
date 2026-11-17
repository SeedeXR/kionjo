using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// All text of a post as one ASS file burned by libass: the hook (boxed, in the title band), the
    /// overlays (one sentence at a time above the bottom zone) and word by word captions (\kf).
    /// </summary>
    public static class AssWriter
    {
        public static string Escape(string text)
        {
            // libass has no backslash escape: a word joiner (U+2060) after each backslash keeps it literal.
            var s = (text ?? "").Replace("\\", "\\⁠").Replace("{", "\\{").Replace("}", "\\}");
            return s.Replace("\r\n", "\\N").Replace("\r", "\\N").Replace("\n", "\\N");
        }

        /// <summary>h:mm:ss.cc, 69.6 gives 0:01:09.60.</summary>
        public static string Time(float seconds)
        {
            long cs = (long)Mathf.Round(Mathf.Max(0f, seconds) * 100f);
            long h = cs / 360000; cs -= h * 360000;
            long m = cs / 6000; cs -= m * 6000;
            long s = cs / 100; cs -= s * 100;
            return $"{h}:{m:00}:{s:00}.{cs:00}";
        }

        static string I(int v) => v.ToString(CultureInfo.InvariantCulture);

        public static string Write(int width, int height, IList<TimedText> texts, IList<WordTimer.Word> captionWords, KionjoBrandKit kit)
        {
            var z = SafeZone.For(width, height);
            string text = KionjoBrandKit.AssColour(kit != null ? kit.textColour : "#FFFFFF");
            string accent = KionjoBrandKit.AssColour(kit != null ? kit.accentColour : "#4FD1E8");
            string titleFont = kit != null && !string.IsNullOrWhiteSpace(kit.titleFontName) ? kit.titleFontName : "Arial";
            string bodyFont = kit != null && !string.IsNullOrWhiteSpace(kit.bodyFontName) ? kit.bodyFontName : "Arial";
            int R(float k) => Mathf.RoundToInt(height * k);

            var sb = new StringBuilder();
            sb.Append("[Script Info]\n")
              .Append("ScriptType: v4.00+\n")
              .Append($"PlayResX: {I(width)}\n")
              .Append($"PlayResY: {I(height)}\n")
              .Append("WrapStyle: 0\n")
              .Append("ScaledBorderAndShadow: yes\n\n");
            sb.Append("[V4+ Styles]\n")
              .Append("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\n");
            // BorderStyle 3 draws a box; libass paints it in OutlineColour (BackColour is its shadow). Outline is the padding.
            sb.Append($"Style: Hook,{titleFont},{I(R(0.045f))},{text},{text},&H66000000,&H66000000,-1,0,0,0,100,100,0,0,3,{I(R(0.008f))},0,5,{I(z.Left)},{I(z.Right)},0,1\n");
            sb.Append($"Style: Overlay,{bodyFont},{I(R(0.034f))},{text},{text},&H80000000,&H80000000,0,0,0,0,100,100,0,0,3,{I(R(0.006f))},0,2,{I(z.Left)},{I(z.Right)},{I(z.Bottom)},1\n");
            // \kf fills a word from SecondaryColour to PrimaryColour as it is spoken: spoken words light in the accent.
            sb.Append($"Style: Caption,{bodyFont},{I(R(0.04f))},{accent},&H00FFFFFF,&H00000000,&H80000000,-1,0,0,0,100,100,0,0,1,0,2,2,{I(z.Left)},{I(z.Right)},{I(z.Bottom + R(0.12f))},1\n\n");

            sb.Append("[Events]\n")
              .Append("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\n");
            int x = width / 2, y = z.TitleTop + (z.TitleBottom - z.TitleTop) / 3;
            foreach (var t in texts ?? new List<TimedText>())
            {
                if (t.IsHook)
                    sb.Append($"Dialogue: 0,{Time(t.Start)},{Time(t.End)},Hook,,0,0,0,,{{\\pos({I(x)},{I(y)})}}{{\\fad(150,150)}}{Escape(t.Text)}\n");
                else
                    sb.Append($"Dialogue: 0,{Time(t.Start)},{Time(t.End)},Overlay,,0,0,0,,{{\\fad(150,150)}}{Escape(t.Text)}\n");
            }
            if (captionWords != null)
                for (int i = 0; i < captionWords.Count; i += 3)
                {
                    int last = Mathf.Min(i + 3, captionWords.Count) - 1;
                    var line = new StringBuilder();
                    for (int j = i; j <= last; j++)
                    {
                        var w = captionWords[j];
                        // Up to the next word's start inside the group, so pauses do not shift the fill.
                        float until = j < last ? captionWords[j + 1].s : w.e;
                        if (j > i) line.Append(' ');
                        line.Append($"{{\\kf{I(Mathf.Max(0, Mathf.RoundToInt((until - w.s) * 100f)))}}}{Escape((w.w ?? "").Trim())}");
                    }
                    sb.Append($"Dialogue: 0,{Time(captionWords[i].s)},{Time(captionWords[last].e)},Caption,,0,0,0,,{line}\n");
                }
            return sb.ToString();
        }

        /// <summary>The same caption groups as SRT cues (three words each), for the clean variant.</summary>
        public static List<SubtitleCue> CaptionCues(IList<WordTimer.Word> words)
        {
            var cues = new List<SubtitleCue>();
            if (words == null) return cues;
            for (int i = 0; i < words.Count; i += 3)
            {
                int last = Mathf.Min(i + 3, words.Count) - 1;
                var parts = new List<string>();
                for (int j = i; j <= last; j++) parts.Add((words[j].w ?? "").Trim());
                cues.Add(new SubtitleCue { Start = words[i].s, End = words[last].e, Text = string.Join(" ", parts) });
            }
            return cues;
        }
    }
}
