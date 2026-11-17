using System;
using System.IO;
using System.Text;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// A footage day folder's notes.md: one line per file, "- name: what it shows + the hurdle or trick".
    /// </summary>
    public static class FootageNotes
    {
        public const string FileName = "notes.md";

        public static string DayFolder(string footageRoot, DateTime day) => Path.Combine(footageRoot ?? "", day.ToString("yyyy-MM-dd"));

        public static void Append(string dayFolder, string fileName, string text)
        {
            Directory.CreateDirectory(dayFolder);
            var file = Path.Combine(dayFolder, FileName);
            var flat = (text ?? "").Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
            var sb = new StringBuilder();
            if (!File.Exists(file)) sb.Append("# ").Append(Path.GetFileName(dayFolder.TrimEnd('/', '\\'))).Append("\n\n");
            else
            {
                var existing = File.ReadAllText(file);
                if (existing.Length > 0 && !existing.EndsWith("\n")) sb.Append('\n');
            }
            sb.Append("- ").Append(fileName).Append(": ").Append(flat).Append('\n');
            File.AppendAllText(file, sb.ToString(), new UTF8Encoding(false));
        }

        /// <summary>
        /// The note for fileName: the text after the colon of the first "- " line naming the file, or its
        /// stem followed by "/" or " " (grouped lines such as "- a-clean/lingering.mp4: ..."). Null when none.
        /// </summary>
        public static string LineFor(string dayFolder, string fileName)
        {
            var file = Path.Combine(dayFolder ?? "", FileName);
            if (string.IsNullOrEmpty(fileName) || !File.Exists(file)) return null;
            string stem = Path.GetFileNameWithoutExtension(fileName);
            foreach (var raw in File.ReadAllLines(file))
            {
                var line = raw.TrimStart();
                if (!line.StartsWith("- ")) continue;
                if (line.Contains(fileName) || line.Contains(stem + "/") || line.Contains(stem + " "))
                {
                    // The note follows the first ": " after the name (names may hold colons only in drive letters).
                    int at = line.IndexOf(": ", StringComparison.Ordinal);
                    return at < 0 ? "" : line.Substring(at + 2).Trim();
                }
            }
            return null;
        }
    }
}
