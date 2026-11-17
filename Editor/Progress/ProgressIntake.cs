using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// What the watcher saw, held until each file has stopped growing; and the rules that put a found
    /// footage file into the right journal entry. Pure: the clock and file sizes are passed in.
    /// </summary>
    public class ProgressIntake
    {
        /// <summary>A file counts as written once its size has not changed for this long.</summary>
        public static readonly TimeSpan Settle = TimeSpan.FromSeconds(2);

        readonly Func<DateTime> _now;
        readonly Func<string, long> _size;
        readonly Dictionary<string, (long size, DateTime since)> _seen = new();
        readonly object _lock = new();

        public ProgressIntake(Func<DateTime> now, Func<string, long> size)
        {
            _now = now;
            _size = size;
        }

        /// <summary>Called from file system events (any thread).</summary>
        public void Seen(string path)
        {
            lock (_lock)
                if (!_seen.ContainsKey(path)) _seen[path] = (_size(path), _now());
        }

        /// <summary>Paths whose size stayed the same and above zero for Settle; each is returned once.</summary>
        public List<string> Ready()
        {
            var ready = new List<string>();
            lock (_lock)
            {
                var now = _now();
                foreach (var path in _seen.Keys.ToList())
                {
                    var (last, since) = _seen[path];
                    long size = _size(path);
                    if (size != last) _seen[path] = (size, now);
                    else if (size > 0 && now - since >= Settle)
                    {
                        ready.Add(path);
                        _seen.Remove(path);
                    }
                }
            }
            return ready;
        }

        /// <summary>
        /// The entry a footage file belongs to: the file must sit in a yyyy-MM-dd folder directly under
        /// footageRoot; entries created that day are the candidates; the one whose feature prefixes the
        /// file name ("feature-...") wins, else the only candidate, else none.
        /// </summary>
        public static JournalEntry MatchEntry(IReadOnlyList<JournalEntry> all, string footageFile, string footageRoot)
        {
            if (all == null || string.IsNullOrEmpty(footageFile) || string.IsNullOrEmpty(footageRoot)) return null;
            var dayFolder = Path.GetDirectoryName(Full(footageFile));
            if (!SamePath(Path.GetDirectoryName(dayFolder), Full(footageRoot))) return null;
            var day = Path.GetFileName(dayFolder);
            if (!DateTime.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return null;
            var candidates = all.Where(e => e != null && e.created == day).ToList();
            var name = Path.GetFileName(footageFile);
            var byFeature = candidates.Where(e => !string.IsNullOrEmpty(e.feature) && name.StartsWith(e.feature + "-", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.feature.Length).FirstOrDefault();
            return byFeature ?? (candidates.Count == 1 ? candidates[0] : null);
        }

        /// <summary>
        /// Appends the file to the entry's media (saved with a load-merge-save, so nothing else is lost),
        /// noted "found in footage folder" plus its notes.md line. False when the entry already lists it.
        /// </summary>
        public static bool Attach(JournalEntry e, string file)
        {
            if (e == null || Listed(e, file)) return false;
            var line = FootageNotes.LineFor(Path.GetDirectoryName(file), Path.GetFileName(file));
            var media = new JournalMedia
            {
                path = file,
                role = FfprobeMediaInfo.IsImage(file) ? "still" : "broll",
                note = "found in footage folder" + (string.IsNullOrWhiteSpace(line) ? "" : ": " + line),
            };
            bool added = false;
            var saved = KionjoJournal.Update(e.Folder, x =>
            {
                if (Listed(x, file)) return;
                x.media.Add(media);
                added = true;
            });
            if (saved != null) e.media = saved.media;
            return added;
        }

        static bool Listed(JournalEntry e, string file) =>
            (e.media ?? new List<JournalMedia>()).Any(m => !string.IsNullOrEmpty(m.path) && SamePath(Full(m.path), Full(file)));

        static string Full(string path)
        {
            try { return Path.GetFullPath(path).TrimEnd('/', '\\'); }
            catch (Exception) { return path; }
        }

        static bool SamePath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
