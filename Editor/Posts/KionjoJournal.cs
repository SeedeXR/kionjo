using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>One media file an entry points at. outPoint at or below inPoint means the whole file.</summary>
    [Serializable]
    public class JournalMedia
    {
        public string path;
        /// <summary>hook, before, after, broll, still or facecam.</summary>
        public string role = "broll";
        public float inPoint;
        public float outPoint;
        public string note;
        /// <summary>Footage the user did not make (originality warning when it has no commentary).</summary>
        public bool notOwn;
    }

    /// <summary>One step of the work: what was made, why, what it took, and the posts made from it.</summary>
    [Serializable]
    public class JournalEntry
    {
        public string id;
        public string created;
        public string project;
        public string title;
        /// <summary>One of the kit's story types.</summary>
        public string story;
        /// <summary>Brand kit name.</summary>
        public string owner;
        /// <summary>idea, prototype, build or complete.</summary>
        public string stage = "idea";
        public string making, reference, goal, achieved, hurdle, trick;
        /// <summary>Choices made at this step, one short sentence each.</summary>
        public List<string> decisions = new();
        public List<JournalMedia> media = new();
        public List<string> hookOptions = new();
        /// <summary>Draft, Ready or Posted.</summary>
        public string status = "Draft";
        public List<KionjoPost> posts = new();
        /// <summary>The arc this step belongs to (for example "hero"); a later entry of the same feature supersedes it.</summary>
        public string feature;
        /// <summary>Where the version lives (branch@commit or a tag). Information only; Kionjo never checks it out.</summary>
        public string version;
        /// <summary>Free Markdown from the agent for whoever edits the post; copied into the brief as is.</summary>
        public string brief;
        /// <summary>Captures of this step: the backlog.</summary>
        public List<KionjoCaptureRequest> captures = new();
        [NonSerialized] public string Folder;
        /// <summary>Media paths as loaded, so SaveEdited can tell files attached since from files the user removed.</summary>
        [NonSerialized] public HashSet<string> LoadedMedia;
        /// <summary>Every top-level member of the file as loaded (raw), so fields Kionjo does not know survive a save.</summary>
        [NonSerialized] public List<KeyValuePair<string, string>> Extra;
    }

    /// <summary>
    /// The journal: one folder per entry (yyyy-MM-dd-slug/entry.json) under a per-machine root, so one
    /// journal serves every project. Media stay where they are, referenced by absolute path.
    /// </summary>
    public static class KionjoJournal
    {
        public const string FolderKey = "SeedeXR.Kionjo.JournalFolder";
        public const string FileName = "entry.json";
        static readonly object SaveLock = new();
        public static readonly string[] Stages = { "idea", "prototype", "build", "complete" };

        public static string Folder
        {
            get => EditorPrefs.GetString(FolderKey, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "KionjoJournal"));
            set => EditorPrefs.SetString(FolderKey, value ?? "");
        }

        /// <summary>"The ghost never turns round!" gives "the-ghost-never-turns-round".</summary>
        public static string Slug(string title)
        {
            var sb = new StringBuilder();
            bool dash = false;
            foreach (char ch in (title ?? "").ToLowerInvariant())
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                {
                    if (dash && sb.Length > 0) sb.Append('-');
                    sb.Append(ch);
                    dash = false;
                }
                else dash = true;
            }
            var s = sb.ToString();
            if (s.Length > 48) s = s.Substring(0, 48).TrimEnd('-');
            return s.Length == 0 ? "entry" : s;
        }

        public static JournalEntry Create(string root, string title, string project, DateTime date)
        {
            var baseName = $"{date:yyyy-MM-dd}-{Slug(title)}";
            var folder = Path.Combine(root, baseName);
            for (int n = 2; Directory.Exists(folder); n++) folder = Path.Combine(root, $"{baseName}-{n}");
            Directory.CreateDirectory(folder);
            var e = new JournalEntry
            {
                id = Guid.NewGuid().ToString("N"),
                created = date.ToString("yyyy-MM-dd"),
                title = title,
                project = project,
                Folder = folder,
            };
            Save(e);
            return e;
        }

        /// <summary>Pretty JSON, UTF-8 without BOM, written beside the file then moved into place.</summary>
        /// <summary>Kionjo's fields, then every member the file had that Kionjo does not know, exactly as it was.</summary>
        static string ToJson(JournalEntry e)
        {
            var json = JsonUtility.ToJson(e, true);
            if (e.Extra == null || e.Extra.Count == 0) return json;
            var known = new HashSet<string>(JsonMembers.Object(json).Select(m => m.Key));
            var extra = e.Extra.Where(m => !known.Contains(m.Key)).ToList();
            if (extra.Count == 0) return json;
            var s = new StringBuilder(json.Substring(0, json.LastIndexOf('}')).TrimEnd());
            foreach (var m in extra) s.Append(",\n    \"").Append(EscapeKey(m.Key)).Append("\": ").Append(m.Value);
            return s.Append("\n}").ToString();
        }

        static string EscapeKey(string key) => (key ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        public static void Save(JournalEntry e)
        {
            if (string.IsNullOrEmpty(e?.Folder)) throw new ArgumentException("The entry has no folder.");
            Directory.CreateDirectory(e.Folder);
            var target = Path.Combine(e.Folder, FileName);
            // A post job saves from a worker thread while the window may save too: one writer at a time.
            lock (SaveLock)
            {
                var partial = target + ".partial";
                File.WriteAllText(partial, ToJson(e), new UTF8Encoding(false));
                if (File.Exists(target)) File.Replace(partial, target, null);
                else File.Move(partial, target);
            }
        }

        /// <summary>The entry in folder, or null with the reason in error.</summary>
        public static JournalEntry Load(string folder, out string error)
        {
            error = null;
            var file = Path.Combine(folder, FileName);
            try
            {
                var text = File.ReadAllText(file);
                // Entries written by hand or by other tools list media as plain paths (or a mix) and carry fields
                // Kionjo does not know: read media here, keep every member raw so Save can write them back.
                var members = JsonMembers.Object(text);
                List<JournalMedia> media = null;
                var mediaRaw = members.FirstOrDefault(m => m.Key == "media").Value;
                if (mediaRaw != null)
                {
                    media = new List<JournalMedia>();
                    foreach (var item in JsonMembers.Array(mediaRaw))
                    {
                        var path = JsonMembers.Unquote(item);
                        if (path != null) media.Add(new JournalMedia { path = path });
                        else if (item.StartsWith("{")) media.Add(JsonUtility.FromJson<JournalMedia>(item));
                    }
                    text = "{" + string.Join(",", members.Where(m => m.Key != "media").Select(m => $"\"{EscapeKey(m.Key)}\":{m.Value}")) + "}";
                }
                var e = JsonUtility.FromJson<JournalEntry>(text);
                if (e == null)
                {
                    error = $"{file} is empty.";
                    return null;
                }
                e.Folder = folder;
                e.Extra = members;
                if (media != null) e.media = media;
                if (string.IsNullOrEmpty(e.created)) e.created = JsonMembers.Unquote(members.FirstOrDefault(m => m.Key == "date").Value);
                e.decisions ??= new List<string>();
                e.media ??= new List<JournalMedia>();
                e.hookOptions ??= new List<string>();
                e.posts ??= new List<KionjoPost>();
                e.captures ??= new List<KionjoCaptureRequest>();
                foreach (var c in e.captures)
                {
                    c.rigs ??= new List<string>();
                    c.outputs ??= new List<string>();
                }
                e.LoadedMedia = new HashSet<string>(e.media.Select(m => m.path ?? ""), StringComparer.OrdinalIgnoreCase);
                return e;
            }
            catch (Exception ex)
            {
                error = $"{file} could not be read: {ex.Message}";
                return null;
            }
        }

        static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> EditorWrites = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Saves the editor's copy of an entry without losing what other writers saved since it was loaded:
        /// the editor's fields win; media attached on disk since (not in LoadedMedia) are kept and added to the
        /// copy; captures come from disk; posts on disk the copy does not hold are kept.
        /// </summary>
        public static void SaveEdited(JournalEntry edited)
        {
            if (string.IsNullOrEmpty(edited?.Folder)) throw new ArgumentException("The entry has no folder.");
            var merged = Update(edited.Folder, disk =>
            {
                var known = edited.LoadedMedia ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in disk.media)
                    if (!known.Contains(m.path ?? "") && !edited.media.Any(x => string.Equals(x.path, m.path, StringComparison.OrdinalIgnoreCase)))
                        edited.media.Add(m);
                edited.captures = disk.captures;
                foreach (var p in disk.posts)
                    if (!string.IsNullOrEmpty(p.id) && !edited.posts.Any(x => x.id == p.id)) edited.posts.Add(p);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(edited), disk);
            });
            if (merged == null) Save(edited);
            edited.LoadedMedia = new HashSet<string>(edited.media.Select(m => m.path ?? ""), StringComparer.OrdinalIgnoreCase);
            var file = Path.Combine(edited.Folder, FileName);
            EditorWrites[Path.GetFullPath(edited.Folder)] = File.GetLastWriteTimeUtc(file);
        }

        /// <summary>
        /// True while the entry file is exactly as the editor's SaveEdited left it: the watcher does not
        /// rebind the form for the user's own saves (that would throw away what they are still typing).
        /// </summary>
        public static bool IsEditorSave(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return false;
            var file = Path.Combine(folder, FileName);
            return EditorWrites.TryGetValue(Path.GetFullPath(folder), out var t) && File.Exists(file) && File.GetLastWriteTimeUtc(file) == t;
        }

        /// <summary>
        /// Load the entry as it is on disk now, apply change, save: one writer at a time, so text another
        /// writer saved in between is kept. Null when the folder holds no readable entry.
        /// </summary>
        public static JournalEntry Update(string folder, Action<JournalEntry> change)
        {
            if (string.IsNullOrEmpty(folder) || !File.Exists(Path.Combine(folder, FileName))) return null;
            lock (SaveLock)
            {
                var e = Load(folder, out _);
                if (e == null) return null;
                change(e);
                Save(e);
                return e;
            }
        }

        /// <summary>Every entry under root, newest folder name first. Unreadable files become warnings.</summary>
        public static List<JournalEntry> LoadAll(string root, List<string> warnings)
        {
            var list = new List<JournalEntry>();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return list;
            foreach (var dir in Directory.GetDirectories(root).OrderByDescending(Path.GetFileName, StringComparer.Ordinal))
            {
                if (!File.Exists(Path.Combine(dir, FileName))) continue;
                var e = Load(dir, out var error);
                if (e != null) list.Add(e);
                else warnings?.Add(error);
            }
            return list;
        }

        /// <summary>Media whose file is not there any more (moved or deleted).</summary>
        public static IEnumerable<JournalMedia> Missing(JournalEntry e) =>
            (e?.media ?? new List<JournalMedia>()).Where(m => string.IsNullOrEmpty(m.path) || !File.Exists(m.path));
    }
}
