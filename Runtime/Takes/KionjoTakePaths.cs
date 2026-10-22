using System.IO;
using System.Text;

namespace SeedeXR.Kionjo
{
    /// <summary>Naming of take folders: "S10_take03" inside a per shot folder.</summary>
    public static class KionjoTakePaths
    {
        const string TakeMarker = "_take";

        public static string TakeFolderName(string shotCode, int take) =>
            $"{Sanitize(shotCode)}{TakeMarker}{take:00}";

        /// <summary>Highest existing take number plus one; 1 when the folder is missing or empty.</summary>
        public static int NextTakeNumber(string shotFolder)
        {
            int highest = 0;
            if (Directory.Exists(shotFolder))
                foreach (var dir in Directory.GetDirectories(shotFolder))
                    if (TryParseTakeNumber(Path.GetFileName(dir), out int n) && n > highest)
                        highest = n;
            return highest + 1;
        }

        /// <summary>Full path of the take folder with the highest number, null when there is none.</summary>
        public static string NewestTakeFolder(string shotFolder)
        {
            if (!Directory.Exists(shotFolder)) return null;
            string newest = null;
            int highest = 0;
            foreach (var dir in Directory.GetDirectories(shotFolder))
                if (TryParseTakeNumber(Path.GetFileName(dir), out int n) && n > highest)
                {
                    highest = n;
                    newest = dir;
                }
            return newest;
        }

        public static bool TryParseTakeNumber(string folderName, out int number)
        {
            number = 0;
            if (string.IsNullOrEmpty(folderName)) return false;
            int i = folderName.LastIndexOf(TakeMarker, System.StringComparison.Ordinal);
            if (i < 0) return false;
            return int.TryParse(folderName.Substring(i + TakeMarker.Length), out number) && number > 0;
        }

        /// <summary>Letters, digits, dash and underscore survive; anything else becomes "_"; empty becomes "shot".</summary>
        public static string Sanitize(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return "shot";
            var sb = new StringBuilder(code.Length);
            foreach (char c in code.Trim())
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            return sb.ToString();
        }
    }
}
