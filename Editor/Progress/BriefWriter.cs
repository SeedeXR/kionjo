using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// BRIEF.md of a step, for whoever edits the post: what it is, why, how, the calls made, what was
    /// hard, what the materials show, hooks, and the build agent's own notes as written. Empty parts are left out.
    /// </summary>
    public static class BriefWriter
    {
        public static string Write(JournalEntry e, IReadOnlyList<(string fileName, string note)> materials)
        {
            var sb = new StringBuilder();
            sb.Append("# ").Append(string.IsNullOrWhiteSpace(e.title) ? "Untitled step" : e.title.Trim()).Append("\n\n");
            var facts = new[] { ("Stage", e.stage), ("Feature", e.feature), ("Version", e.version) }
                .Where(f => !string.IsNullOrWhiteSpace(f.Item2)).Select(f => $"{f.Item1}: {f.Item2.Trim()}.").ToList();
            if (facts.Count > 0) sb.Append(string.Join(" ", facts)).Append("\n\n");

            Section(sb, "In one sentence", FirstSentence(!string.IsNullOrWhiteSpace(e.achieved) ? e.achieved : e.making));
            Section(sb, "Why", e.goal);
            var approach = new List<string>();
            if (!string.IsNullOrWhiteSpace(e.making)) approach.Add(e.making.Trim());
            if (!string.IsNullOrWhiteSpace(e.reference)) approach.Add("Reference: " + e.reference.Trim());
            Section(sb, "Approach", string.Join("\n\n", approach));
            Section(sb, "Decisions", string.Join("\n", (e.decisions ?? new List<string>()).Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => "- " + d.Trim())));
            string hurdle = e.hurdle?.Trim(), trick = e.trick?.Trim();
            Section(sb, "Hurdle and trick", !string.IsNullOrEmpty(hurdle) && !string.IsNullOrEmpty(trick) ? $"{hurdle} -> {trick}" : hurdle ?? trick);
            Section(sb, "Materials", string.Join("\n", (materials ?? new List<(string, string)>())
                .Select(m => string.IsNullOrWhiteSpace(m.note) ? $"- `{m.fileName}`" : $"- `{m.fileName}`: {m.note.Trim()}")));
            Section(sb, "Hook options", string.Join("\n", (e.hookOptions ?? new List<string>()).Where(h => !string.IsNullOrWhiteSpace(h)).Select((h, i) => $"{i + 1}. {h.Trim()}")));
            if (!string.IsNullOrWhiteSpace(e.brief)) sb.Append("## Notes from the build\n").Append(e.brief.TrimEnd('\r', '\n')).Append("\n\n");
            return sb.ToString().TrimEnd('\n') + "\n";
        }

        static void Section(StringBuilder sb, string heading, string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return;
            sb.Append("## ").Append(heading).Append('\n').Append(body.Trim()).Append("\n\n");
        }

        /// <summary>Text up to the first sentence end (". ", "! ", "? "), else all of it.</summary>
        static string FirstSentence(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = text.Trim();
            for (int i = 0; i < text.Length - 1; i++)
                if ((text[i] == '.' || text[i] == '!' || text[i] == '?') && char.IsWhiteSpace(text[i + 1]))
                    return text.Substring(0, i + 1);
            return text;
        }
    }
}
