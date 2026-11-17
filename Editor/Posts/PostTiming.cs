using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Where text may sit on a vertical frame. On 1080x1920 the app's buttons and captions cover the
    /// top 270, bottom 670, left 65 and right 150 px (third party guidance); hook and cover titles stay
    /// in y 420 to 1500 so the 3:4 grid crop keeps them. Other sizes scale by height.
    /// </summary>
    public struct SafeZone
    {
        public int Width, Height, Top, Bottom, Left, Right, TitleTop, TitleBottom;

        public static SafeZone For(int width, int height)
        {
            float k = height / 1920f;
            int S(float v) => Mathf.RoundToInt(v * k);
            return new SafeZone
            {
                Width = width, Height = height,
                Top = S(270), Bottom = S(670), Left = S(65), Right = S(150),
                TitleTop = S(420), TitleBottom = S(1500),
            };
        }
    }

    public struct TimedText
    {
        public string Text;
        public float Start, End;
        public bool IsHook;
    }

    /// <summary>When each line of text is on screen: hook from 0.5 s, then one overlay at a time at reading speed.</summary>
    public static class PostTiming
    {
        public const float HookStart = 0.5f, Gap = 0.2f, EndMargin = 0.5f;

        public static int Words(string text) =>
            (text ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;

        /// <summary>About 3 words a second, never under 2 s.</summary>
        public static float ReadingSeconds(string text) => Mathf.Max(2f, Words(text) / 3f);

        /// <summary>Overlays that would end after totalSeconds - 0.5 are left out (the caller warns).</summary>
        public static List<TimedText> Schedule(string hook, IList<string> overlays, float totalSeconds)
        {
            var list = new List<TimedText>();
            float t = HookStart;
            if (!string.IsNullOrWhiteSpace(hook))
            {
                float end = HookStart + ReadingSeconds(hook);
                list.Add(new TimedText { Text = hook.Trim(), Start = HookStart, End = end, IsHook = true });
                t = end + Gap;
            }
            if (overlays == null) return list;
            foreach (var o in overlays)
            {
                if (string.IsNullOrWhiteSpace(o)) continue;
                float end = t + ReadingSeconds(o);
                if (end > totalSeconds - EndMargin) continue;
                list.Add(new TimedText { Text = o.Trim(), Start = t, End = end });
                t = end + Gap;
            }
            return list;
        }
    }
}
