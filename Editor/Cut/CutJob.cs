using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Assembles a draft in its own numbered folder: writes subs.srt, list.txt, card line files and
    /// commands.txt (re-runnable by hand), then runs the ffmpeg steps in order, logging stderr to
    /// cut.log. A failing step stops the job and keeps the folder.
    /// </summary>
    public sealed class CutJob : KionjoJob
    {
        readonly CutPlan _plan;
        readonly string _folder, _ffmpeg;

        public string OutputMp4 { get; private set; }
        public string OutputSrt { get; private set; }
        public string Folder => _folder;

        public CutJob(CutPlan plan, string cutFolder, string ffmpegExe, string title = null)
        {
            _plan = plan;
            _folder = cutFolder;
            _ffmpeg = ffmpegExe;
            Title = title ?? $"Cut {Path.GetFileName(cutFolder)}";
        }

        public string OutputName => $"{_plan.BaseName}_{Path.GetFileName(_folder)}.mp4";

        public override async Task RunAsync(CancellationToken ct)
        {
            if (_plan.Segments.Count == 0) throw new InvalidOperationException("Nothing to cut: no shot has a source yet.");
            Directory.CreateDirectory(Path.Combine(_folder, CutCommandBuilder.SegFolder));
            bool needsTitleFont = _plan.Segments.Exists(s => s.Kind == SegmentKind.Text && s.Lines.Count > 0);
            if (!CopyFont(_plan.SubtitleStyle?.titleFontPath, CutCommandBuilder.TitleFont) && !CopyFont(DefaultFont(), CutCommandBuilder.TitleFont) && needsTitleFont)
                throw new InvalidOperationException("No font for text cards: set Title Font in the storyboard's subtitle style.");
            if (!CopyFont(_plan.SubtitleStyle?.fontPath, CutCommandBuilder.SubsFont)) CopyFont(DefaultFont(), CutCommandBuilder.SubsFont);
            if (_plan.Subtitles.Count > 0)
                File.WriteAllText(Path.Combine(_folder, CutCommandBuilder.Subs), SrtWriter.Write(_plan.Subtitles), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(_folder, CutCommandBuilder.ListFile), CutCommandBuilder.ConcatList(_plan));
            foreach (var kv in CutCommandBuilder.TextFiles(_plan))
                File.WriteAllText(Path.Combine(_folder, kv.Key), kv.Value, new UTF8Encoding(false));

            var commands = CutCommandBuilder.Build(_plan, _folder, _ffmpeg, OutputName);
            var script = new StringBuilder();
            script.Append("# Kionjo cut: run from the cut folder\n");
            script.Append("cd ").Append(FfmpegCommand.Quote(_folder.Replace('\\', '/'))).Append('\n');
            foreach (var c in commands) script.Append(c.ToShellString()).Append('\n');
            File.WriteAllText(Path.Combine(_folder, "commands.txt"), script.ToString());

            using var log = new StreamWriter(Path.Combine(_folder, "cut.log"), false, new UTF8Encoding(false));
            for (int i = 0; i < commands.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var cmd = commands[i];
                Status = $"{cmd.Label} ({i + 1}/{commands.Count})";
                AddLog(Status);
                lock (log) log.WriteLine($"== {cmd.Label}");
                var result = await KionjoProcess.RunAsync(cmd, ct, new StepProgress(this, i, commands.Count), log).ConfigureAwait(false);
                if (result.Cancelled) throw new OperationCanceledException();
                if (result.ExitCode != 0)
                {
                    Failed = true;
                    Error = $"{cmd.Label} failed ({result.ExitCode}):\n{result.StdErrTail}";
                    AddLog(Error);
                    Status = "Failed";
                    return;
                }
            }
            OutputMp4 = Path.Combine(_folder, OutputName);
            OutputSrt = _plan.Subtitles.Count > 0 ? Path.Combine(_folder, CutCommandBuilder.Subs) : null;
            Progress = 1f;
            Status = $"Done: {OutputName}";
            AddLog(Status);
        }

        bool CopyFont(string source, string name)
        {
            if (string.IsNullOrEmpty(source) || !File.Exists(source)) return false;
            File.Copy(source, Path.Combine(_folder, name), true);
            return true;
        }

        /// <summary>A system sans serif whose family name is Arial or close to it; null when none is found.</summary>
        public static string DefaultFont()
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            string[] candidates =
            {
                Path.Combine(windows ?? "", "arial.ttf"),
                Path.Combine(windows ?? "", "segoeui.ttf"),
                "/System/Library/Fonts/Supplemental/Arial.ttf",
                "/Library/Fonts/Arial.ttf",
                "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                "/usr/share/fonts/TTF/DejaVuSans.ttf",
            };
            foreach (var c in candidates)
                if (!string.IsNullOrEmpty(c) && File.Exists(c)) return c;
            return null;
        }
    }
}
