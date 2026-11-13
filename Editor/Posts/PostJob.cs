using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Builds the whole post bundle in &lt;postsRoot&gt;/&lt;entry slug&gt;/&lt;post id&gt;/: renders (text with music,
    /// text without music, clean), SRT, cover, caption text, the recipe (post.json, commands.txt).
    /// Everything that reads the kit or probes media happens before the first await.
    /// </summary>
    public sealed class PostJob : KionjoJob
    {
        [Serializable]
        class Recipe
        {
            public string entryId;
            public KionjoPost post;
            public string kitName;
            public List<string> commands = new();
        }

        readonly JournalEntry _entry;
        readonly KionjoPost _post;
        readonly KionjoBrandKit _kit;
        readonly string _ffmpeg;
        readonly IMediaInfo _info;
        readonly Func<string, List<WordTimer.Word>> _words;
        readonly string _kitName, _titleFontPath, _bodyFontPath, _caption;
        readonly bool _hasKit;

        public string Folder { get; }
        public string OutputMp4 { get; private set; }
        public List<string> Warnings { get; } = new();
        public PostPlan Plan { get; private set; }

        public PostJob(JournalEntry e, KionjoPost p, KionjoBrandKit kit, string postsRoot, string ffmpeg, IMediaInfo info, Func<string, List<WordTimer.Word>> words)
        {
            _entry = e;
            _post = p;
            _kit = kit;
            _ffmpeg = ffmpeg;
            _info = info;
            _words = words;
            if (string.IsNullOrEmpty(p.id)) p.id = Guid.NewGuid().ToString("N").Substring(0, 8);
            if (string.IsNullOrWhiteSpace(p.name)) p.name = KionjoJournal.Slug(e.title);
            Folder = Path.Combine(postsRoot, KionjoJournal.Slug(e.title), p.id);
            Title = $"Post {p.name}";
            _hasKit = kit != null;
            _kitName = _hasKit ? kit.kitName : "";
            _titleFontPath = _hasKit ? kit.titleFontPath : null;
            _bodyFontPath = _hasKit ? kit.bodyFontPath : null;
            _caption = CaptionWriter.Write(e, p, _hasKit ? kit : null);
        }

        void Fail(string error)
        {
            Failed = true;
            Error = error;
            Status = "Failed";
            AddLog(error);
        }

        public override async Task RunAsync(CancellationToken ct)
        {
            var missing = _post.items.Select(i => i.path).Concat(string.IsNullOrWhiteSpace(_post.musicPath) ? new string[0] : new[] { _post.musicPath })
                .FirstOrDefault(path => string.IsNullOrEmpty(path) || !File.Exists(path));
            if (_post.items.Count == 0)
            {
                Fail("Nothing to build: the post has no footage.");
                return;
            }
            if (missing != null || _post.items.Any(i => string.IsNullOrEmpty(i.path)))
            {
                Fail($"Missing media: {missing}");
                return;
            }

            Status = "Probing media";
            var plan = PostPlanBuilder.Build(_post, _info, _words, _hasKit ? _kit : null);
            Plan = plan;
            if (PostWarnings.Refuse(plan, out var reason))
            {
                Fail(reason);
                return;
            }
            Warnings.AddRange(PostWarnings.Check(plan, _entry, _post, _hasKit ? _kit : null));
            string ass = AssWriter.Write(plan.Width, plan.Height, plan.Texts, plan.CaptionWords, _hasKit ? _kit : null);
            var commands = PostCommandBuilder.Build(plan, Folder, _ffmpeg, PostCommandBuilder.Joined);

            var utf8 = new UTF8Encoding(false);
            Directory.CreateDirectory(Path.Combine(Folder, PostCommandBuilder.SegFolder));
            File.WriteAllText(Path.Combine(Folder, PostCommandBuilder.Ass), ass, utf8);
            CopyFont(_titleFontPath, PostCommandBuilder.TitleFont);
            if (!File.Exists(Path.Combine(Folder, PostCommandBuilder.TitleFont))) CopyFont(CutJob.DefaultFont(), PostCommandBuilder.TitleFont);
            // libass finds the kit's fonts by family name among the files in the folder (fontsdir=.).
            foreach (var font in new[] { _titleFontPath, _bodyFontPath }.Where(f => !string.IsNullOrEmpty(f) && File.Exists(f)))
                CopyFont(font, Path.GetFileName(font));
            File.WriteAllText(Path.Combine(Folder, PostCommandBuilder.ListFile), PostCommandBuilder.ConcatList(plan), utf8);
            if (!string.IsNullOrWhiteSpace(plan.CoverTitle))
                File.WriteAllText(Path.Combine(Folder, PostCommandBuilder.CoverTitleFile), plan.CoverTitle.Trim(), utf8);
            if (plan.CaptionWords.Count > 0)
                File.WriteAllText(Path.Combine(Folder, PostCommandBuilder.Srt), SrtWriter.Write(AssWriter.CaptionCues(plan.CaptionWords)), utf8);
            File.WriteAllText(Path.Combine(Folder, "caption.txt"), _caption, utf8);
            var recipe = new Recipe { entryId = _entry.id, post = _post, kitName = _kitName, commands = commands.Select(c => c.ToShellString()).ToList() };
            File.WriteAllText(Path.Combine(Folder, "post.json"), JsonUtility.ToJson(recipe, true), utf8);
            var script = new StringBuilder("# Kionjo post: run from the post folder\n");
            script.Append("cd ").Append(FfmpegCommand.Quote(Folder.Replace('\\', '/'))).Append('\n');
            foreach (var c in commands) script.Append(c.ToShellString()).Append('\n');
            File.WriteAllText(Path.Combine(Folder, "commands.txt"), script.ToString(), utf8);

            using (var log = new StreamWriter(Path.Combine(Folder, "post.log"), false, utf8))
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
                        Fail($"{cmd.Label} failed ({result.ExitCode}):\n{result.StdErrTail}");
                        return;
                    }
                    if (cmd.Args.LastOrDefault() == PostCommandBuilder.Audio) CheckSilence(plan);
                }

            var srt = Path.Combine(Folder, PostCommandBuilder.Srt);
            if (File.Exists(srt)) File.Copy(srt, Path.Combine(Folder, plan.Name + ".srt"), true);
            OutputMp4 = Path.Combine(Folder, PostCommandBuilder.MainFile(plan));
            _post.posted = false;
            _entry.posts ??= new List<KionjoPost>();
            int at = _entry.posts.FindIndex(x => x.id == _post.id);
            if (at >= 0) _entry.posts[at] = _post;
            else _entry.posts.Add(_post);
            if (!string.IsNullOrEmpty(_entry.Folder)) KionjoJournal.Save(_entry);
            Progress = 1f;
            Status = $"Done: {PostCommandBuilder.MainFile(plan)}";
            AddLog(Status);
        }

        /// <summary>Silence over 1 s in the mixed sound: a post needs a sound bed under the picture.</summary>
        void CheckSilence(PostPlan plan)
        {
            var cmd = new FfmpegCommand { Executable = _ffmpeg, WorkingDirectory = Folder, Label = "Silence check" }
                .Add("-hide_banner", "-nostats", "-i", PostCommandBuilder.Audio, "-af", "silencedetect=noise=-50dB:d=1", "-f", "null", "-");
            var result = KionjoProcess.Run(cmd, 60000);
            if (result.ExitCode != 0) return;
            foreach (var r in SilenceDetect.ParseSilences(result.StdErr))
            {
                float end = Mathf.Min(r.End, plan.TotalSeconds), d = end - r.Start;
                if (d < 1f) continue;
                Warnings.Add($"Silence of {d.ToString("0.0", CultureInfo.InvariantCulture)} s at {r.Start.ToString("0.0", CultureInfo.InvariantCulture)} s: keep a sound bed under the picture.");
            }
        }

        void CopyFont(string source, string name)
        {
            if (string.IsNullOrEmpty(source) || !File.Exists(source)) return;
            File.Copy(source, Path.Combine(Folder, name), true);
        }
    }
}
