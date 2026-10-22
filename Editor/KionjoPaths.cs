using System.IO;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Where Kionjo writes on disk. Everything heavy lives under one root outside Assets:
    /// takes/, renders/, captures/, thumbs/, cut/, animatic/.
    /// </summary>
    public static partial class KionjoPaths
    {
        /// <summary>The Unity project folder (parent of Assets).</summary>
        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        /// <summary>An absolute output folder is used as is; a relative one hangs off the project root.</summary>
        public static string Root(string outputFolder)
        {
            if (string.IsNullOrWhiteSpace(outputFolder)) outputFolder = "Kionjo";
            return Path.IsPathRooted(outputFolder) ? outputFolder : Path.Combine(ProjectRoot, outputFolder);
        }

        public static string ShotTakes(string root, string shotCode) =>
            Path.Combine(root, "takes", KionjoTakePaths.Sanitize(shotCode));

        public static string ShotRenders(string root, string shotCode) =>
            Path.Combine(root, "renders", KionjoTakePaths.Sanitize(shotCode));

        public static string ShotCaptures(string root, string shotCode) =>
            Path.Combine(root, "captures", KionjoTakePaths.Sanitize(shotCode));

        public static string ThumbsFolder(string root) => Path.Combine(root, "thumbs");
        public static string CutsFolder(string root) => Path.Combine(root, "cut");
        public static string AnimaticsFolder(string root) => Path.Combine(root, "animatic");

        /// <summary>parent/prefix01, or the next free number after the highest existing one. Creates nothing.</summary>
        public static string NextNumbered(string parent, string prefix)
        {
            int highest = 0;
            if (Directory.Exists(parent))
                foreach (var dir in Directory.GetDirectories(parent))
                {
                    var name = Path.GetFileName(dir);
                    if (name.StartsWith(prefix) && int.TryParse(name.Substring(prefix.Length), out int n) && n > highest)
                        highest = n;
                }
            return Path.Combine(parent, $"{prefix}{highest + 1:00}");
        }
    }
}
