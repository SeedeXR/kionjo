using System.Collections.Generic;
using System.Linq;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// The ready to paste caption: hook, identity line, what changed, call to action, hashtags, alt
    /// text and a short checklist. Empty blocks are left out.
    /// </summary>
    public static class CaptionWriter
    {
        public const int MaxHashtags = 5;

        public static string Write(JournalEntry e, KionjoPost p, KionjoBrandKit kit)
        {
            var blocks = new List<string>
            {
                p.hook,
                kit != null ? kit.identityLine : null,
                HookTemplates.FirstSentences(e.achieved, 2),
                kit != null ? kit.callToAction : null,
                kit != null
                    ? string.Join(" ", kit.hashtags.Where(h => !string.IsNullOrWhiteSpace(h)).Take(MaxHashtags).Select(h => h.Trim().StartsWith("#") ? h.Trim() : "#" + h.Trim()))
                    : null,
            };
            var title = (e.title ?? "").Trim();
            var first = p.overlays?.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o));
            if (title.Length > 0 || first != null)
                blocks.Add(("Alt text: " + (title.Length > 0 ? title + ". " : "") + (first ?? "").Trim()).Trim());

            var checks = new List<string>();
            if (kit != null && !string.IsNullOrWhiteSpace(kit.collaboratorHandle)) checks.Add($"- Invite {kit.collaboratorHandle.Trim()} as collaborator");
            bool everPosted = e.posts != null && e.posts.Any(x => x.posted);
            if (string.IsNullOrWhiteSpace(e.story) || !everPosted) checks.Add("- Consider posting as a Trial Reel");
            if (!string.IsNullOrWhiteSpace(p.musicPath)) checks.Add("- Add a trending sound in the app to the no-music version");
            if (checks.Count > 0) blocks.Add("Checklist:\n" + string.Join("\n", checks));

            return string.Join("\n\n", blocks.Where(b => !string.IsNullOrWhiteSpace(b)).Select(b => b.Trim()));
        }
    }
}
