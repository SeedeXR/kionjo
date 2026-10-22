using System.Collections.Generic;
using System.Text;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// One ffmpeg (or ffprobe) call as an argument list. Each argument stays one argument however
    /// many spaces, parentheses or brackets it holds; quoting happens once, in Quote.
    /// </summary>
    public sealed class FfmpegCommand
    {
        public string Executable = "ffmpeg";
        public List<string> Args = new();
        public string WorkingDirectory;
        public string Label;
        /// <summary>Length of the output in seconds, for the progress bar. 0 = unknown.</summary>
        public float ExpectedSeconds;

        public FfmpegCommand Add(params string[] args)
        {
            Args.AddRange(args);
            return this;
        }

        /// <summary>Executable and arguments, quoted for a shell or a Windows command line.</summary>
        public string ToShellString()
        {
            var sb = new StringBuilder(Quote(Executable));
            foreach (var a in Args) sb.Append(' ').Append(Quote(a));
            return sb.ToString();
        }

        /// <summary>The arguments as one Windows command line string (Process.Arguments).</summary>
        public string ArgumentString()
        {
            var sb = new StringBuilder();
            foreach (var a in Args)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(Quote(a));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Double quotes an argument when it holds a space, a quote or a shell special character.
        /// Inner quotes get a backslash; backslashes are doubled only where they precede a quote
        /// (the Windows C runtime rule), so ordinary Windows paths stay readable.
        /// </summary>
        public static string Quote(string arg)
        {
            if (arg == null) return "\"\"";
            if (arg.Length == 0) return "\"\"";
            bool needs = false;
            foreach (char c in arg)
                if (char.IsWhiteSpace(c) || "\"'()[]{};&|<>*?$`!#=,".IndexOf(c) >= 0)
                {
                    needs = true;
                    break;
                }
            // A filtergraph full of "=" and "," is fine unquoted for a process, but quoting keeps
            // commands.txt safe to paste into a shell. Plain flags and numbers stay bare.
            if (!needs) return arg;
            var sb = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in arg)
            {
                if (c == '\\')
                {
                    slashes++;
                    continue;
                }
                if (c == '"')
                {
                    sb.Append('\\', slashes * 2 + 1).Append('"');
                }
                else
                {
                    sb.Append('\\', slashes).Append(c);
                }
                slashes = 0;
            }
            sb.Append('\\', slashes * 2).Append('"');
            return sb.ToString();
        }
    }
}
