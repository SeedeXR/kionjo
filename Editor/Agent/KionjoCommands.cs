using System.IO;
using SeedeXR.Kionjo.EditorTools;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeedeXR.Kionjo.Agent
{
    /// <summary>
    /// Kionjo's verbs for an AI agent or a script, through the Unity CLI:
    /// unity command kionjo status --project-path "&lt;project&gt;". Compiled only when the
    /// com.unity.pipeline package (0.9 or newer) is installed. Every command answers with ok and a
    /// message; none throws for a user error.
    /// </summary>
    public static class KionjoCommands
    {
        const string Group = "kionjo";

        static object Fail(string message) => new { ok = false, message };

        static KionjoWindow Window(bool open = true)
        {
            var all = Resources.FindObjectsOfTypeAll<KionjoWindow>();
            if (all.Length > 0) return all[0];
            return open ? KionjoWindow.Open() : null;
        }

        static KionjoStoryboard Storyboard(string path = null)
        {
            if (!string.IsNullOrEmpty(path)) return AssetDatabase.LoadAssetAtPath<KionjoStoryboard>(path);
            return Window(open: false)?.Storyboard;
        }

        [CliCommand(Group, "status", "What Kionjo is doing: the storyboard, every shot with its takes and renders, Play mode, recording, the running job.", Tags = new[] { "kionjo" })]
        public static object Status([CliArg("storyboard", "Asset path of a storyboard. Empty uses the one open in the Kionjo window.")] string storyboard = null)
        {
            var sb = Storyboard(storyboard);
            if (sb == null) return Fail("No storyboard: open one with 'kionjo open --storyboard Assets/.../X.asset'.");
            var w = Window(open: false);
            return new { ok = true, message = "status", state = KionjoStateFile.Build(sb, w?.SelectedShot?.id) };
        }

        [CliCommand(Group, "open", "Open the Kionjo window on a storyboard asset.", Tags = new[] { "kionjo" })]
        public static object Open([CliArg("storyboard", "Asset path, for example Assets/Trailer/Storyboard.asset.", Required = true)] string storyboard)
        {
            var sb = AssetDatabase.LoadAssetAtPath<KionjoStoryboard>(storyboard);
            if (sb == null) return Fail($"No Kionjo storyboard at {storyboard}.");
            KionjoWindow.Open().Storyboard = sb;
            return new { ok = true, message = $"Opened {storyboard} ({sb.shots.Count} shots)." };
        }

        [CliCommand(Group, "create", "Create a new storyboard asset and open it.", Tags = new[] { "kionjo" })]
        public static object Create([CliArg("storyboard", "Asset path to create, ending in .asset.", Required = true)] string storyboard,
            [CliArg("title", "Title used in output file names.")] string title = null)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(storyboard) != null) return Fail($"{storyboard} already exists.");
            Directory.CreateDirectory(Path.GetDirectoryName(storyboard) ?? "Assets");
            var sb = ScriptableObject.CreateInstance<KionjoStoryboard>();
            sb.title = string.IsNullOrEmpty(title) ? Path.GetFileNameWithoutExtension(storyboard) : title;
            AssetDatabase.CreateAsset(sb, storyboard);
            AssetDatabase.SaveAssets();
            KionjoWindow.Open().Storyboard = sb;
            return new { ok = true, message = $"Created {storyboard}." };
        }

        [CliCommand(Group, "select", "Select a shot by id or code (S07, C01) in the Kionjo window.", Tags = new[] { "kionjo" })]
        public static object Select([CliArg("shot", "Shot id or code.", Required = true)] string shot)
        {
            var w = Window(open: false);
            var sb = w?.Storyboard;
            if (sb == null) return Fail("No storyboard open.");
            var s = sb.FindShot(shot);
            if (s == null) return Fail($"No shot {shot}.");
            w.SelectedIndex = sb.shots.IndexOf(s);
            return new { ok = true, message = $"Selected {s.code} {s.name}." };
        }

        [CliCommand(Group, "stage", "Stage the selected shot (hide and show lists, stage handlers) and report whether it is ready. Play mode only.", Tags = new[] { "kionjo" })]
        public static object Stage()
        {
            var tab = Window(open: false)?.rootVisualElement.Q<ShootTab>();
            if (tab?.Controller?.Current == null) return Fail("Select a shot first.");
            if (!EditorApplication.isPlaying) return Fail("Enter Play mode to stage.");
            tab.Controller.Stage();
            return new { ok = true, message = "Staging. Read 'kionjo status' or call 'kionjo check' in a moment." };
        }

        [CliCommand(Group, "check", "Check the selected shot against the live scene and list what is wrong.", Tags = new[] { "kionjo" })]
        public static object Check()
        {
            var c = Window(open: false)?.rootVisualElement.Q<ShootTab>()?.Controller;
            if (c?.Current == null) return Fail("Select a shot first.");
            c.Verify(forRender: false);
            var lines = new System.Collections.Generic.List<string>();
            foreach (var (s, t) in c.Report.Lines) lines.Add($"{s}: {t}");
            return new { ok = true, message = c.Report.Ready ? "Ready" : "Not ready", ready = c.Report.Ready, lines };
        }

        [CliCommand(Group, "record", "Start or stop a take of the selected shot. Play mode only.", Tags = new[] { "kionjo" })]
        public static object Record([CliArg("start", "true starts a take, false stops it.")] bool start = true)
        {
            var c = Window(open: false)?.rootVisualElement.Q<ShootTab>()?.Controller;
            if (c == null) return Fail("Open the Kionjo window on a storyboard first.");
            if (!c.CanRecord(out var why)) return Fail(why);
            if (c.IsRecording != start) c.ToggleRecord();
            return new { ok = true, message = c.IsRecording ? "Recording." : $"Stopped. {c.Takes.Count} take(s) for {c.Current.code}." };
        }

        [CliCommand(Group, "render", "Render a shot's selected take (or every shot missing a render). Play mode only; queued behind any running job.", Tags = new[] { "kionjo" })]
        public static object Render([CliArg("shot", "Shot id or code. Empty renders every shot that has a take and no render.")] string shot = null,
            [CliArg("preset", "Preview720p30, HD1080p60 or UHD2160p60. Empty uses the storyboard's render preset.")] string preset = null)
        {
            var sb = Storyboard();
            if (sb == null) return Fail("No storyboard open.");
            var p = sb.renderPreset;
            if (!string.IsNullOrEmpty(preset) && !System.Enum.TryParse(preset, out p)) return Fail($"Unknown preset {preset}.");
            int queued = 0;
            foreach (var s in sb.shots)
            {
                if (!string.IsNullOrEmpty(shot) ? s.id != shot && s.code != shot : KionjoPaths.ExistingRender(sb, s) != null) continue;
                var why = RenderJob.CannotRender(sb, s);
                if (why != null)
                {
                    if (!string.IsNullOrEmpty(shot)) return Fail($"{s.code}: {why}");
                    continue;
                }
                KionjoJobs.Enqueue(new RenderJob(sb, s, p));
                queued++;
            }
            return new { ok = queued > 0, message = queued > 0 ? $"Queued {queued} render(s) at {p}. Watch 'kionjo status' job." : "Nothing to render." };
        }

        [CliCommand(Group, "cut", "Assemble a numbered draft (or an animatic) with ffmpeg: MP4, SRT, Premiere XML.", Tags = new[] { "kionjo" })]
        public static object Cut([CliArg("animatic", "true builds an animatic of thumbnails instead of a draft.")] bool animatic = false,
            [CliArg("preset", "Preview720p30, HD1080p30 or UHD2160p60. Empty uses the storyboard's cut preset.")] string preset = null)
        {
            var w = Window(open: false);
            var sb = w?.Storyboard;
            if (sb == null) return Fail("No storyboard open.");
            if (!string.IsNullOrEmpty(preset))
            {
                if (!System.Enum.TryParse(preset, out KionjoCutPreset p)) return Fail($"Unknown preset {preset}.");
                Undo.RecordObject(sb, "Cut preset");
                sb.cutPreset = p;
            }
            if (FfmpegLocator.FindFfmpeg() == null) return Fail("ffmpeg not found: set its path in Preferences > Kionjo.");
            var tab = w.rootVisualElement.Q<CutTab>();
            if (animatic) tab.BuildAnimatic();
            else tab.BuildDraft();
            var job = tab.LastJob;
            return job == null
                ? Fail("Nothing to cut yet: render a shot, import a capture or add a card.")
                : new { ok = true, message = $"Building {job.Title}. Watch 'kionjo status' job.", folder = job.Folder };
        }

        [CliCommand(Group, "export", "Write the open storyboard as JSON.", Tags = new[] { "kionjo" })]
        public static object Export([CliArg("json", "Output file path.", Required = true)] string json)
        {
            var sb = Storyboard();
            if (sb == null) return Fail("No storyboard open.");
            File.WriteAllText(json, KionjoStoryboardJson.Export(sb));
            return new { ok = true, message = $"Wrote {json}." };
        }

        [CliCommand(Group, "import", "Read shots from JSON into the open storyboard (append, or replace everything). Undoable.", Tags = new[] { "kionjo" })]
        public static object Import([CliArg("json", "JSON file in the format 'kionjo export' writes.", Required = true)] string json,
            [CliArg("replace", "true replaces the storyboard's shots and settings; false appends the shots.")] bool replace = false)
        {
            var w = Window(open: false);
            var sb = w?.Storyboard;
            if (sb == null) return Fail("No storyboard open.");
            if (!File.Exists(json)) return Fail($"No file {json}.");
            int n = KionjoStoryboardJson.Import(sb, File.ReadAllText(json), replace);
            AssetDatabase.SaveAssets();
            w.Rebuild();
            return new { ok = true, message = $"Imported {n} shot(s){(replace ? ", replacing the storyboard" : "")}." };
        }
    }
}
