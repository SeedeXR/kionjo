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
        [NonSerialized] public string Folder;
    }

    /// <summary>
    /// The journal: one folder per entry (yyyy-MM-dd-slug/entry.json) under a per-machine root, so one
    /// journal serves every project. Media stay where they are, referenced by absolute path.
    /// </summary>
    public static class KionjoJournal
    {
        public const string FolderKey = "SeedeXR.Kionjo.JournalFolder";
        public const string FileName = "entry.json";
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
        public static void Save(JournalEntry e)
        {
            if (string.IsNullOrEmpty(e?.Folder)) throw new ArgumentException("The entry has no folder.");
            Directory.CreateDirectory(e.Folder);
            var target = Path.Combine(e.Folder, FileName);
            var partial = target + ".partial";
            File.WriteAllText(partial, JsonUtility.ToJson(e, true), new UTF8Encoding(false));
            if (File.Exists(target)) File.Replace(partial, target, null);
            else File.Move(partial, target);
        }

        /// <summary>The entry in folder, or null with the reason in error.</summary>
        public static JournalEntry Load(string folder, out string error)
        {
            error = null;
            var file = Path.Combine(folder, FileName);
            try
            {
                var e = JsonUtility.FromJson<JournalEntry>(File.ReadAllText(file));
                if (e == null)
                {
                    error = $"{file} is empty.";
                    return null;
                }
                e.Folder = folder;
                e.decisions ??= new List<string>();
                e.media ??= new List<JournalMedia>();
                e.hookOptions ??= new List<string>();
                e.posts ??= new List<KionjoPost>();
                return e;
            }
            catch (Exception ex)
            {
                error = $"{file} could not be read: {ex.Message}";
                return null;
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
