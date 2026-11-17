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

        /// <summary>
        /// &lt;created&gt;_&lt;slug&gt;, taken from the entry's journal folder name (2026-11-17-hero-2 gives
        /// 2026-11-17_hero-2), so two entries with the same day and title never share a post folder.
        /// </summary>
        public static string EntryFolder(string postsRoot, JournalEntry e)
        {
            var name = string.IsNullOrEmpty(e.Folder) ? "" : Path.GetFileName(e.Folder.TrimEnd('/', '\\'));
            var created = e.created ?? "";
            var slug = created.Length > 0 && name.StartsWith(created + "-") && name.Length > created.Length + 1
                ? name.Substring(created.Length + 1)
                : KionjoJournal.Slug(e.title);
            return Path.Combine(postsRoot, $"{created}_{slug}");
        }

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
        /// After a post is marked posted: every vNN other than keepVersionFolder goes, without losing anything
        /// unique. A file is deleted only when its data survives elsewhere: a hard link with another name (the
        /// footage), or an identical file (same relative path, size and time) in keepVersionFolder. Everything else
        /// (briefs, recipes, covers, a copy whose original is gone) moves to keepVersionFolder/older/vNN/.
        /// Returns the number of files moved.
        /// </summary>
        public static int CleanAfterPosted(string entryFolder, string keepVersionFolder)
        {
            int moved = 0;
            var keep = Path.GetFullPath(keepVersionFolder ?? "").TrimEnd('/', '\\');
            foreach (var (dir, n) in Versions(entryFolder).ToList())
            {
                var full = Path.GetFullPath(dir).TrimEnd('/', '\\');
                if (string.Equals(full, keep, StringComparison.OrdinalIgnoreCase)) continue;
                var older = Path.Combine(keep, "older", Path.GetFileName(full));
                foreach (var f in Directory.GetFiles(full, "*", SearchOption.AllDirectories))
                {
                    var rel = f.Substring(full.Length + 1);
                    var twin = Path.Combine(keep, rel);
                    bool survives = KionjoLinks.LinkCount(f) > 1
                        || (File.Exists(twin) && new FileInfo(twin).Length == new FileInfo(f).Length && File.GetLastWriteTimeUtc(twin) == File.GetLastWriteTimeUtc(f));
                    if (survives)
                    {
                        File.Delete(f);
                        continue;
                    }
                    var to = Path.Combine(older, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(to));
                    if (File.Exists(to)) File.Delete(to);
                    File.Move(f, to);
                    moved++;
                }
                // Only folders are left; a file still here (it could not be moved) keeps its version folder.
                if (Directory.GetFiles(full, "*", SearchOption.AllDirectories).Length == 0) Directory.Delete(full, true);
            }
            return moved;
        }

        /// <summary>
        /// Deletes the videos in every vNN of the entry other than keepVersionFolder (pictures, briefs and
        /// recipes stay). Other posts' outputs (a name keepVersionFolder's post/ or capcut/ does not have) are
        /// first linked into it, so different posts of one step sit side by side in the latest version.
        /// Returns the bytes freed by files that were not materials links.
        /// </summary>
        public static long PruneOlderVideos(string entryFolder, string keepVersionFolder)
        {
            long freed = 0;
            var keep = Path.GetFullPath(keepVersionFolder ?? "").TrimEnd('/', '\\');
            var older = Versions(entryFolder).Where(v => !string.Equals(Path.GetFullPath(v.dir).TrimEnd('/', '\\'), keep, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(v => v.n).ToList();
            foreach (var (dir, _) in older)
                foreach (var sub in new[] { Post, CapCut })
                {
                    var from = Path.Combine(dir, sub);
                    if (!Directory.Exists(from)) continue;
                    foreach (var f in Directory.GetFiles(from))
                    {
                        var to = Path.Combine(keep, sub, Path.GetFileName(f));
                        if (!File.Exists(to)) KionjoLinks.LinkOrCopy(f, to, out _);
                    }
                }
            foreach (var (dir, _) in older)
            {
                var materials = Path.GetFullPath(Path.Combine(dir, Materials));
                var kept = Path.GetFullPath(Path.Combine(dir, "older"));
                foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    if (!VideoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())) continue;
                    // Never lose the only copy: older/ holds files kept on purpose, and a material that is not a hard
                    // link (a copy, perhaps of footage that is gone) stays; Kionjo's own outputs are rebuildable.
                    var full = Path.GetFullPath(f);
                    if (full.StartsWith(kept, StringComparison.OrdinalIgnoreCase)) continue;
                    if (full.StartsWith(materials, StringComparison.OrdinalIgnoreCase) && KionjoLinks.LinkCount(f) <= 1) continue;
                    // ponytail: files under materials/ count as links (normally they are); a copied one frees disk uncounted.
                    if (!Path.GetFullPath(f).StartsWith(materials, StringComparison.OrdinalIgnoreCase)) freed += new FileInfo(f).Length;
                    File.Delete(f);
                }
            }
            return freed;
        }
    }
}
