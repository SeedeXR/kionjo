using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>Preferences > Kionjo: per user settings (ffmpeg path, Python path, encoder, cut behaviour).</summary>
    public static class KionjoPreferences
    {
        public const string EncoderKey = "SeedeXR.Kionjo.EncoderArgs";
        public const string PythonKey = "SeedeXR.Kionjo.PythonPath";
        public const string OpenFolderKey = "SeedeXR.Kionjo.OpenFolderAfterCut";
        public const string Path_ = "Preferences/Kionjo";

        public static string EncoderOverride
        {
            get => EditorPrefs.GetString(EncoderKey, "");
            set => EditorPrefs.SetString(EncoderKey, value ?? "");
        }

        public static string PythonPath
        {
            get => EditorPrefs.GetString(PythonKey, "python");
            set => EditorPrefs.SetString(PythonKey, string.IsNullOrWhiteSpace(value) ? "python" : value);
        }

        public static bool OpenFolderAfterCut
        {
            get => EditorPrefs.GetBool(OpenFolderKey, true);
            set => EditorPrefs.SetBool(OpenFolderKey, value);
        }

        public const string FootageRootKey = "SeedeXR.Kionjo.FootageRoot";
        public const string WatchProgressKey = "SeedeXR.Kionjo.WatchProgress";
        public const string FloorKey = "SeedeXR.Kionjo.FloorGB";
        public const string LockFolderKey = "SeedeXR.Kionjo.LockFolder";

        /// <summary>Folder of footage day folders (yyyy-MM-dd) the watcher reads; empty = off.</summary>
        public static string FootageRoot
        {
            get => EditorPrefs.GetString(FootageRootKey, "");
            set => EditorPrefs.SetString(FootageRootKey, value ?? "");
        }

        public static bool WatchProgress
        {
            get => EditorPrefs.GetBool(WatchProgressKey, true);
            set => EditorPrefs.SetBool(WatchProgressKey, value);
        }

        /// <summary>Free memory (GB) that must stay free after a heavy job.</summary>
        public static float FloorGB
        {
            get => EditorPrefs.GetFloat(FloorKey, 8f);
            set => EditorPrefs.SetFloat(FloorKey, Mathf.Max(0f, value));
        }

        /// <summary>Where the machine-wide heavy-work lock lives (shared by every tool that honours it).</summary>
        public static string LockFolder
        {
            get => EditorPrefs.GetString(LockFolderKey, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kionjo"));
            set
            {
                if (string.IsNullOrWhiteSpace(value)) EditorPrefs.DeleteKey(LockFolderKey);
                else EditorPrefs.SetString(LockFolderKey, value);
            }
        }

        /// <summary>The user's encoder arguments when set (for example "-c:v h264_nvenc -cq 19"), else the preset's.</summary>
        public static string[] EncoderArgs(string[] presetArgs)
        {
            var custom = EncoderOverride;
            if (string.IsNullOrWhiteSpace(custom)) return presetArgs;
            // ponytail: whitespace split, no quoting; encoder arguments never contain spaces in practice
            return custom.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        }

        [SettingsProvider]
        static SettingsProvider Create() => new(Path_, SettingsScope.User)
        {
            label = "Kionjo",
            keywords = new HashSet<string> { "ffmpeg", "trailer", "encoder", "python", "Kionjo" },
            activateHandler = (_, root) =>
            {
                root.style.paddingLeft = 10;
                root.style.paddingTop = 10;
                root.style.paddingRight = 10;
                var title = new Label("Kionjo") { style = { fontSize = 19, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 10 } };
                root.Add(title);

                var status = new Label(FfmpegLocator.Status()) { name = "ffmpeg-status", style = { whiteSpace = WhiteSpace.Normal, marginBottom = 8 } };
                var ffmpeg = new TextField("ffmpeg") { value = FfmpegLocator.PreferredPath, tooltip = "Path to ffmpeg (the file or its folder). Empty searches PATH." };
                ffmpeg.RegisterValueChangedCallback(e =>
                {
                    FfmpegLocator.PreferredPath = e.newValue;
                    FfmpegLocator.ClearCache();
                    status.text = FfmpegLocator.Status();
                });
                var browse = new Button(() =>
                {
                    var picked = EditorUtility.OpenFilePanel("Find ffmpeg", "", Application.platform == RuntimePlatform.WindowsEditor ? "exe" : "");
                    if (!string.IsNullOrEmpty(picked)) ffmpeg.value = picked;
                }) { text = "Browse", tooltip = "Pick the ffmpeg executable." };
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                ffmpeg.style.flexGrow = 1;
                row.Add(ffmpeg);
                row.Add(browse);
                root.Add(row);
                root.Add(status);
                root.Add(new HelpBox("ffmpeg does the rendering and the cutting. Download it from ffmpeg.org (Windows: the gyan.dev full build), then put it on PATH or point to it here.", HelpBoxMessageType.Info));

                var encoder = new TextField("Encoder arguments") { value = EncoderOverride, tooltip = "Replaces the preset's encoder, for example \"-c:v h264_nvenc -cq 19 -preset p6\". Empty uses the preset (libx264)." };
                encoder.RegisterValueChangedCallback(e => EncoderOverride = e.newValue);
                encoder.style.marginTop = 12;
                root.Add(encoder);

                var python = new TextField("Python") { value = PythonPath, tooltip = "Python with faster-whisper, used to trim voice lines to their first and last word. Optional." };
                python.RegisterValueChangedCallback(e => PythonPath = e.newValue);
                root.Add(python);

                var open = new Toggle("Open the folder after a cut") { value = OpenFolderAfterCut, tooltip = "Reveal the new draft in Explorer or Finder when a cut finishes." };
                open.RegisterValueChangedCallback(e => OpenFolderAfterCut = e.newValue);
                root.Add(open);

                var journal = new TextField("Journal folder") { value = KionjoJournal.Folder, tooltip = "Where Posts keeps its journal entries (one folder per entry). One journal for all projects on this machine." };
                journal.RegisterValueChangedCallback(e => KionjoJournal.Folder = e.newValue);
                journal.style.flexGrow = 1;
                var browseJournal = new Button(() =>
                {
                    var picked = EditorUtility.OpenFolderPanel("Journal folder", journal.value, "");
                    if (!string.IsNullOrEmpty(picked)) journal.value = picked;
                }) { text = "Browse", tooltip = "Pick the journal folder." };
                var journalRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 12 } };
                journalRow.Add(journal);
                journalRow.Add(browseJournal);
                root.Add(journalRow);

                var progress = new Label("Progress") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 12 } };
                root.Add(progress);
                var watch = new Toggle("Watch for progress") { value = WatchProgress, tooltip = "While the editor is open, notice changed journal entries and new footage, and refresh each step's materials and brief. Never renders." };
                watch.RegisterValueChangedCallback(e =>
                {
                    WatchProgress = e.newValue;
                    KionjoWatcher.Restart();
                });
                root.Add(watch);
                var footage = new TextField("Footage folder") { value = FootageRoot, isDelayed = true, tooltip = "Holds one folder per day (yyyy-MM-dd) with the clips and a notes.md line per file. Captures go here. Empty: watching footage is off." };
                footage.RegisterValueChangedCallback(e =>
                {
                    FootageRoot = e.newValue;
                    KionjoWatcher.Restart();
                });
                root.Add(footage);
                var floor = new FloatField("Keep free (GB)") { value = FloorGB, tooltip = "A capture or render waits in the backlog unless free memory is twice its need and this much stays free after it." };
                floor.RegisterValueChangedCallback(e => FloorGB = e.newValue);
                root.Add(floor);
                var lockFolder = new TextField("Heavy-work lock folder") { value = LockFolder, isDelayed = true, tooltip = "One machine-wide lock file for heavy jobs, so tools and agents take turns." };
                lockFolder.RegisterValueChangedCallback(e => LockFolder = e.newValue);
                root.Add(lockFolder);
                root.Add(new Label($"Lock: {HeavyLock.Describe(LockFolder)}") { style = { whiteSpace = WhiteSpace.Normal } });
            },
        };

        public static void Open() => SettingsService.OpenUserPreferences(Path_);
    }
}
