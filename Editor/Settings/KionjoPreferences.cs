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
            },
        };

        public static void Open() => SettingsService.OpenUserPreferences(Path_);
    }
}
