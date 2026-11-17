using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// A step's post folder: &lt;posts root&gt;/&lt;created&gt;_&lt;slug&gt;/vNN/ with materials/ (links to every
    /// media file), post/ (Kionjo's edit), capcut/ (clean edit + subtitles), recipe/ and BRIEF.md.
    /// </summary>
    public static class PostFolder
    {
        public const string Materials = "materials", Post = "post", CapCut = "capcut", Recipe = "recipe", Brief = "BRIEF.md";
        public static readonly string[] VideoExtensions = { ".mp4", ".mov", ".webm", ".mkv" };
        static readonly Regex Version = new(@"^v(\d+)$");

        public static string EntryFolder(string postsRoot, JournalEntry e) =>
            Path.Combine(postsRoot, $"{e.created}_{KionjoJournal.Slug(e.title)}");

        static IEnumerable<(string dir, int n)> Versions(string entryFolder) =>
            !Directory.Exists(entryFolder) ? Enumerable.Empty<(string, int)>()
                : Directory.GetDirectories(entryFolder).Select(d => (d, m: Version.Match(Path.GetFileName(d))))
                    .Where(x => x.m.Success).Select(x => (x.d, int.Parse(x.m.Groups[1].Value)));

        /// <summary>The highest vNN, or null when there is none.</summary>
        public static string Latest(string entryFolder) => Versions(entryFolder).OrderByDescending(v => v.n).Select(v => v.dir).FirstOrDefault();

        public static string Next(string entryFolder) => KionjoPaths.NextNumbered(entryFolder, "v");

        /// <summary>
        /// Links every media file of the entry into materials/, removes materials the entry no longer lists,
        /// and writes BRIEF.md (each file's note from the entry, else from its folder's notes.md). Cheap; never renders.
        /// </summary>
        public static void Refresh(string versionFolder, JournalEntry e, string footageRoot)
        {
            var materials = Path.Combine(versionFolder, Materials);
            Directory.CreateDirectory(materials);
            var listed = new List<(string fileName, string note)>();
            var missing = new List<string>();
            var copied = new List<string>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in e.media ?? new List<JournalMedia>())
            {
                if (string.IsNullOrEmpty(m.path) || !File.Exists(m.path))
                {
                    missing.Add(m.path ?? "(no path)");
                    continue;
                }
                var name = Path.GetFileName(m.path);
                // Two files of the same name from different days: keep both, prefixed with their folder.
                if (!names.Add(name)) names.Add(name = $"{Path.GetFileName(Path.GetDirectoryName(m.path))}-{name}");
                if (!KionjoLinks.LinkOrCopy(m.path, Path.Combine(materials, name), out var why) && why != null) copied.Add(why);
                var note = !string.IsNullOrWhiteSpace(m.note) ? m.note : FootageNotes.LineFor(Path.GetDirectoryName(m.path), Path.GetFileName(m.path));
                listed.Add((name, note));
            }
            foreach (var f in Directory.GetFiles(materials))
                if (!names.Contains(Path.GetFileName(f))) File.Delete(f);

            var brief = new StringBuilder(BriefWriter.Write(e, listed));
            if (missing.Count > 0)
            {
                brief.Append("\n## Missing\n");
                foreach (var p in missing) brief.Append("- `").Append(p).Append("`\n");
            }
            if (copied.Count > 0)
            {
                brief.Append("\n## Copied, not linked\n");
                foreach (var c in copied) brief.Append("- ").Append(c).Append('\n');
            }
            File.WriteAllText(Path.Combine(versionFolder, Brief), brief.ToString(), new UTF8Encoding(false));
        }

        /// <summary>
        /// Deletes the videos in every vNN of the entry other than keepVersionFolder (pictures, briefs and
        /// recipes stay). Returns the bytes freed by files that were not materials links.
        /// </summary>
        public static long PruneOlderVideos(string entryFolder, string keepVersionFolder)
        {
            long freed = 0;
            var keep = Path.GetFullPath(keepVersionFolder ?? "").TrimEnd('/', '\\');
            foreach (var (dir, _) in Versions(entryFolder).ToList())
            {
                if (string.Equals(Path.GetFullPath(dir).TrimEnd('/', '\\'), keep, StringComparison.OrdinalIgnoreCase)) continue;
                var materials = Path.GetFullPath(Path.Combine(dir, Materials));
                foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    if (!VideoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())) continue;
                    // ponytail: files under materials/ count as links (normally they are); a copied one frees disk uncounted.
                    if (!Path.GetFullPath(f).StartsWith(materials, StringComparison.OrdinalIgnoreCase)) freed += new FileInfo(f).Length;
                    File.Delete(f);
                }
            }
            return freed;
        }
    }
}
