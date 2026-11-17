using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Notices progress while the editor is open: a changed entry.json in the journal, or a new media
    /// file in a footage day folder. Operating system events only (no polling loop over the folders);
    /// the queue is looked at twice a second. It refreshes materials and briefs, attaches found footage
    /// and drops captures of superseded steps. It never renders.
    /// </summary>
    [InitializeOnLoad]
    public static class KionjoWatcher
    {
        static readonly string[] MediaExtensions = { ".mp4", ".mov", ".webm", ".mkv", ".png", ".jpg", ".jpeg", ".wav" };
        static readonly HashSet<string> NewEntries = new(StringComparer.OrdinalIgnoreCase);
        static FileSystemWatcher _journal, _footage;
        static ProgressIntake _intake;
        static double _nextLook;
        static string _journalRoot, _footageRoot;

        /// <summary>Raised on the main thread after the watcher changed or noticed an entry.</summary>
        public static event Action Changed;

        static KionjoWatcher()
        {
            // Test runs and builds have no one to show progress to.
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += Restart;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
        }

        /// <summary>The entry changed since it was last selected in the Posts tab.</summary>
        public static bool IsNew(string entryFolder) => entryFolder != null && NewEntries.Contains(entryFolder);

        /// <summary>Clears the new mark; true when there was one.</summary>
        public static bool MarkSeen(string entryFolder) => entryFolder != null && NewEntries.Remove(entryFolder);

        /// <summary>Starts (or restarts after a preference changed) the watchers.</summary>
        public static void Restart()
        {
            Stop();
            if (!KionjoPreferences.WatchProgress) return;
            _intake = new ProgressIntake(() => DateTime.UtcNow, SizeOf);
            _journalRoot = KionjoJournal.Folder;
            _footageRoot = KionjoPreferences.FootageRoot;
            _journal = Watch(_journalRoot, KionjoJournal.FileName);
            if (!string.IsNullOrWhiteSpace(_footageRoot)) _footage = Watch(_footageRoot, "*");
            EditorApplication.update += Look;
        }

        static void Stop()
        {
            EditorApplication.update -= Look;
            _journal?.Dispose();
            _footage?.Dispose();
            _journal = _footage = null;
        }

        static FileSystemWatcher Watch(string folder, string filter)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return null;
            try
            {
                var w = new FileSystemWatcher(folder, filter)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                };
                // Events arrive on a worker thread: only note the path here.
                w.Created += (_, e) => Note(e.FullPath);
                w.Changed += (_, e) => Note(e.FullPath);
                w.Renamed += (_, e) => Note(e.FullPath);
                w.EnableRaisingEvents = true;
                return w;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Kionjo: cannot watch {folder}: {ex.Message}");
                return null;
            }
        }

        static void Note(string path)
        {
            var name = Path.GetFileName(path);
            if (name == KionjoJournal.FileName || MediaExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
                _intake?.Seen(path);
        }

        static long SizeOf(string path)
        {
            try { return File.Exists(path) ? new FileInfo(path).Length : 0; }
            catch (Exception) { return -1; }
        }

        static void Look()
        {
            if (EditorApplication.timeSinceStartup < _nextLook || _intake == null) return;
            _nextLook = EditorApplication.timeSinceStartup + 0.5;
            var ready = _intake.Ready();
            if (ready.Count == 0) return;
            try
            {
                var all = KionjoJournal.LoadAll(_journalRoot, null);
                bool changed = false;
                foreach (var path in ready)
                {
                    if (Path.GetFileName(path) == KionjoJournal.FileName)
                    {
                        var e = all.FirstOrDefault(x => string.Equals(Path.GetFullPath(x.Folder), Path.GetFullPath(Path.GetDirectoryName(path)), StringComparison.OrdinalIgnoreCase));
                        if (e == null) continue;
                        RefreshPostFolder(e);
                        // The window's own save: refreshing would rebind the form and drop what is still being typed.
                        if (KionjoJournal.IsEditorSave(e.Folder)) continue;
                        NewEntries.Add(e.Folder);
                        changed = true;
                    }
                    else
                    {
                        var e = ProgressIntake.MatchEntry(all, path, _footageRoot);
                        // Its entry.json change event refreshes the post folder.
                        if (ProgressIntake.Attach(e, path)) changed = true;
                    }
                }
                if (KionjoBacklog.DropSuperseded(all) > 0) changed = true;
                if (changed) Changed?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Kionjo: progress watcher: {ex.Message}");
            }
        }

        /// <summary>Materials and brief of the entry's latest version (v01 when none yet). Cheap; no render.</summary>
        public static void RefreshPostFolder(JournalEntry e)
        {
            var entryFolder = PostFolder.EntryFolder(KionjoPaths.PostsRoot(KionjoPostsSettings.instance.outputFolder), e);
            PostFolder.Refresh(PostFolder.Latest(entryFolder) ?? PostFolder.Next(entryFolder), e, KionjoPreferences.FootageRoot);
        }
    }
}
