using System.IO;
using System.Text;
using UnityEditor;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Device capture and organise only modes: copies the headset's (or any device's) video into
    /// captures/&lt;Code&gt;/ with a numbered name, points the shot at it, and keeps an index.md.
    /// </summary>
    public static class CaptureImporter
    {
        public static string Import(KionjoStoryboard sb, KionjoShot shot, string sourcePath)
        {
            var folder = KionjoPaths.ShotCaptures(sb, shot);
            Directory.CreateDirectory(folder);
            var ext = Path.GetExtension(sourcePath);
            var code = KionjoTakePaths.Sanitize(shot.code);
            int n = 1;
            string target;
            do target = Path.Combine(folder, $"{code}_capture{n++:00}{ext}");
            while (File.Exists(target));
            File.Copy(sourcePath, target);
            Undo.RecordObject(sb, "Import capture");
            shot.deviceCapturePath = target;
            EditorUtility.SetDirty(sb);
            return target;
        }

        /// <summary>captures/index.md: every capture per shot, its length and the shot's notes.</summary>
        public static string WriteIndex(KionjoStoryboard sb, IMediaProbe probe)
        {
            var root = Path.Combine(KionjoPaths.Root(sb), "captures");
            Directory.CreateDirectory(root);
            var md = new StringBuilder($"# Captures: {sb.title}\n\n| Shot | File | Length | Notes |\n|---|---|---|---|\n");
            foreach (var shot in sb.shots)
            {
                var folder = KionjoPaths.ShotCaptures(sb, shot);
                if (!Directory.Exists(folder)) continue;
                foreach (var f in Directory.GetFiles(folder))
                {
                    float d = probe.DurationSeconds(f);
                    var notes = (shot.notes ?? "").Replace("\n", " ").Replace("|", "/");
                    md.Append($"| {shot.code} | {Path.GetFileName(f)} | {(d > 0 ? $"{d:0.0} s" : "?")} | {notes} |\n");
                }
            }
            var path = Path.Combine(root, "index.md");
            File.WriteAllText(path, md.ToString());
            return path;
        }
    }
}
