using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// The platform basics as advice, never blocking (except a post over 3 minutes, which Instagram
    /// will not recommend). Messages are plain sentences the window shows as they are.
    /// </summary>
    public static class PostWarnings
    {
        public const float WarnSeconds = 90f, RefuseSeconds = 180f, LongShotSeconds = 5f;
        public const int MaxHookWords = 7;
        static readonly string[] Watermarks = { "tiktok", "capcut", "snaptik" };

        public static List<string> Check(PostPlan plan, JournalEntry e, KionjoPost p, KionjoBrandKit kit)
        {
            var list = new List<string>();
            if (plan.TotalSeconds > WarnSeconds) list.Add("Longer than 90 s: most Reels that travel are 20 to 45 s.");
            var overlays = (p.overlays ?? new List<string>()).Where(o => !string.IsNullOrWhiteSpace(o)).ToList();
            if (string.IsNullOrWhiteSpace(p.hook) && overlays.Count == 0) list.Add("No text on screen: most viewers watch muted.");
            int hookWords = PostTiming.Words(p.hook);
            if (hookWords > MaxHookWords) list.Add($"Hook has {hookWords} words: keep it to 7 or fewer.");
            foreach (var s in plan.Segments.Where(s => s.Duration > LongShotSeconds))
                list.Add($"Shot {s.Label} holds for {CutCommandBuilder.F(s.Duration)} s: consider a cut or a change every few seconds.");

            if (kit != null)
            {
                var screen = string.Join("\n", new[] { p.hook ?? "" }.Concat(overlays));
                foreach (var word in kit.jargon.Where(j => !string.IsNullOrWhiteSpace(j)))
                    if (Regex.IsMatch(screen, $@"\b{Regex.Escape(word.Trim())}\b", RegexOptions.IgnoreCase))
                        list.Add($"Jargon on screen: {word.Trim()}. Say it in plain words.");
            }

            foreach (var path in p.items.Select(i => i.path).Where(x => !string.IsNullOrEmpty(x)).Distinct())
            {
                var file = Path.GetFileName(path);
                if (Watermarks.Any(w => path.IndexOf(w, System.StringComparison.OrdinalIgnoreCase) >= 0))
                    list.Add($"{file} may carry another app's watermark: Instagram demotes those.");
                if (overlays.Count == 0 && (e?.media ?? new List<JournalMedia>()).Any(m => m.notOwn && m.path == path))
                    list.Add($"{file} is not your footage and has no commentary on top: add text or voice.");
            }
            list.AddRange(plan.Warnings);
            return list;
        }

        public static bool Refuse(PostPlan plan, out string reason)
        {
            reason = plan.TotalSeconds > RefuseSeconds ? "Longer than 3 minutes: Instagram will not recommend it." : null;
            return reason != null;
        }
    }
}
