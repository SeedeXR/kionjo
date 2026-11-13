using System;
using System.Collections.Generic;
using System.Linq;

namespace SeedeXR.Kionjo.EditorTools
{
    public enum PostFit { Crop, BlurFill }
    public enum PostEnd { CallToAction, Loop }
    public enum HookKind { Claim, Promise, Question, Pov }

    /// <summary>One piece of footage in a post. outPoint at or below inPoint means to the end of the file.</summary>
    [Serializable]
    public class PostItem
    {
        public string path;
        public float inPoint;
        public float outPoint;
        public PostFit fit = PostFit.Crop;
        /// <summary>Horizontal centre of the crop window, 0 (left) to 1 (right).</summary>
        public float cropCentre = 0.5f;
        /// <summary>Transcribe the item's speech into word by word captions.</summary>
        public bool captionSpeech;
    }

    /// <summary>One vertical post made from a journal entry: text, footage, audio, end and cover.</summary>
    [Serializable]
    public class KionjoPost
    {
        public string id;
        /// <summary>Output file stem.</summary>
        public string name;
        public string hook;
        /// <summary>Body sentences, one on screen at a time, in order.</summary>
        public List<string> overlays = new();
        public List<PostItem> items = new();
        public bool clipSound = true;
        public float clipVolume = 1f;
        public string musicPath;
        public float musicVolume = 0.8f;
        public float musicStart;
        public PostEnd end = PostEnd.CallToAction;
        public string endText;
        public string coverTitle;
        /// <summary>Film time of the cover frame.</summary>
        public float coverTime;
        public bool posted;
        public string postedUrl;

        /// <summary>
        /// A first post from an entry: every non-still media item (hook role first), the beats and then
        /// the decisions as overlays (first sentence each), the first hook option or a question hook.
        /// </summary>
        public static KionjoPost FromEntry(JournalEntry e)
        {
            var p = new KionjoPost
            {
                name = KionjoJournal.Slug(e.title),
                hook = e.hookOptions != null && e.hookOptions.Count > 0 && !string.IsNullOrWhiteSpace(e.hookOptions[0])
                    ? e.hookOptions[0].Trim()
                    : HookTemplates.Fill(HookKind.Question, e),
                coverTitle = string.Join(" ", HookTemplates.Words(e.title).Take(3)).ToUpperInvariant(),
            };
            var media = (e.media ?? new List<JournalMedia>()).Where(m => m.role != "still").ToList();
            foreach (var m in media.Where(m => m.role == "hook").Concat(media.Where(m => m.role != "hook")))
                p.items.Add(new PostItem { path = m.path, inPoint = m.inPoint, outPoint = m.outPoint });
            foreach (var beat in new[] { e.making, e.achieved, e.hurdle, e.trick }.Concat(e.decisions ?? new List<string>()))
            {
                var s = HookTemplates.FirstSentences(beat, 1);
                if (s.Length > 0) p.overlays.Add(s);
            }
            return p;
        }
    }

    /// <summary>The four hook shapes, filled mechanically from an entry. The person rewrites them; the tool never picks.</summary>
    public static class HookTemplates
    {
        public static string Fill(HookKind kind, JournalEntry e)
        {
            var x = Subject(e);
            return kind switch
            {
                HookKind.Claim => $"Nobody believes this is {x}.",
                HookKind.Promise => $"This changes how you see {x}.",
                HookKind.Question => $"Can {x}?",
                _ => $"POV: {x}",
            };
        }

        /// <summary>First non-empty of achieved, trick, goal, title: first letter lowered, end punctuation off, at most 6 words.</summary>
        static string Subject(JournalEntry e)
        {
            var text = new[] { e.achieved, e.trick, e.goal, e.title }.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? "";
            var words = Words(text).Take(6).ToList();
            if (words.Count == 0) return "";
            var x = string.Join(" ", words).TrimEnd('.', ',', ';', ':', '!', '?');
            return char.ToLowerInvariant(x[0]) + x.Substring(1);
        }

        public static IEnumerable<string> Words(string text) =>
            (text ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

        /// <summary>The first n sentences (ending in . ! or ? before a space or the end), trimmed.</summary>
        public static string FirstSentences(string text, int n)
        {
            text = (text ?? "").Trim();
            int found = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if ((c == '.' || c == '!' || c == '?') && (i + 1 == text.Length || char.IsWhiteSpace(text[i + 1])) && ++found == n)
                    return text.Substring(0, i + 1);
            }
            return text;
        }
    }
}
