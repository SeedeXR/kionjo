using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Render what is missing, then assemble a draft (or an animatic of thumbnails) with ffmpeg.
    /// Shows what the cut will use, the warnings of the last plan and the log of the last job.
    /// </summary>
    public class CutTab : VisualElement
    {
        readonly KionjoWindow _window;
        readonly Label _ffmpegLine, _summary;
        readonly EnumField _preset;
        readonly Button _renderMissing, _animatic, _build, _openMp4, _openFolder;
        readonly ListView _warnings;
        readonly TextField _log;
        readonly List<string> _warningItems = new() { "Build a draft or an animatic: shots left out and dropped cues are listed here." };
        CutJob _last;
        bool _revealed;

        public CutTab(KionjoWindow window)
        {
            _window = window;
            AddToClassList("kionjo-cut");

            var top = new VisualElement();
            top.AddToClassList("kionjo-row");
            _ffmpegLine = new Label { name = "cut-ffmpeg-line" };
            _ffmpegLine.style.flexGrow = 1;
            _ffmpegLine.style.alignSelf = Align.Center;
            top.Add(_ffmpegLine);
            top.Add(new Button(KionjoPreferences.Open) { name = "cut-preferences", text = "Preferences", tooltip = "Set the ffmpeg path and encoder in Preferences > Kionjo." });
            Add(top);

            _preset = new EnumField("Cut preset", KionjoCutPreset.Preview720p30) { name = "cut-preset", tooltip = "Frame size and quality of the assembled draft." };
            _preset.RegisterValueChangedCallback(e =>
            {
                var sb = _window.Storyboard;
                if (sb == null || e.newValue == null) return;
                Undo.RecordObject(sb, "Cut preset");
                sb.cutPreset = (KionjoCutPreset)e.newValue;
                EditorUtility.SetDirty(sb);
            });
            _preset.style.marginTop = 6;
            Add(_preset);

            _summary = new Label { name = "cut-summary", style = { whiteSpace = WhiteSpace.Normal, marginTop = 4 } };
            _summary.AddToClassList("kionjo-verify-line--info");
            Add(_summary);

            var actions = new VisualElement();
            actions.AddToClassList("kionjo-row");
            _renderMissing = new Button(RenderMissing) { name = "cut-render-missing", text = "Render missing shots" };
            _animatic = new Button(() => Build(animatic: true)) { name = "cut-animatic", text = "Build animatic", tooltip = "Every shot as its thumbnail (or a title card) for its planned length, with voice, subtitles and music. Watch the pacing before shooting." };
            _build = new Button(() => Build(animatic: false)) { name = "cut-build", text = "Build draft" };
            _build.style.unityFontStyleAndWeight = FontStyle.Bold;
            actions.Add(_renderMissing);
            actions.Add(_animatic);
            actions.Add(_build);
            Add(actions);

            var open = new VisualElement();
            open.AddToClassList("kionjo-row");
            _openMp4 = new Button(() => EditorUtility.OpenWithDefaultApp(_last?.OutputMp4)) { name = "cut-open-mp4", text = "Play last draft", tooltip = "Open the last draft in your video player." };
            _openFolder = new Button(() => EditorUtility.RevealInFinder(_last?.OutputMp4 ?? _last?.Folder)) { name = "cut-open-folder", text = "Show folder", tooltip = "Show the draft folder: MP4, SRT, commands.txt (re-runnable) and cut.log." };
            open.Add(_openMp4);
            open.Add(_openFolder);
            Add(open);

            var warnHead = new Label("Warnings");
            warnHead.AddToClassList("kionjo-section");
            Add(warnHead);
            _warnings = new ListView(_warningItems, 18, () => new Label { style = { whiteSpace = WhiteSpace.NoWrap } }, (e, i) => ((Label)e).text = _warningItems[i])
            {
                name = "cut-warnings",
                selectionType = SelectionType.None,
            };
            _warnings.style.minHeight = 40;
            _warnings.style.maxHeight = 140;
            Add(_warnings);

            var logHead = new Label("Log");
            logHead.AddToClassList("kionjo-section");
            Add(logHead);
            _log = new TextField { name = "cut-log", multiline = true, isReadOnly = true, tooltip = "Output of the last job. A cut also keeps it as cut.log in its draft folder." };
            _log.AddToClassList("kionjo-log");
            Add(_log);
        }

        public void Refresh()
        {
            var sb = _window.Storyboard;
            bool ffmpeg = FfmpegLocator.IsAvailable;
            _ffmpegLine.text = ffmpeg ? FfmpegLocator.Status() : "ffmpeg not found. Set its path in Preferences > Kionjo.";
            _ffmpegLine.EnableInClassList("kionjo-ok", ffmpeg);
            _ffmpegLine.EnableInClassList("kionjo-bad", !ffmpeg);
            if (sb == null) return;
            _preset.SetValueWithoutNotify(sb.cutPreset);

            int ready = 0, missing = 0, cards = 0;
            foreach (var s in sb.shots)
            {
                if (s.IsCard) cards++;
                else
                {
                    ShotCard.State(sb, s, out bool ok);
                    if (ok) ready++;
                    else if (s.outputMode != KionjoOutputMode.OrganiseOnly) missing++;
                }
            }
            _summary.text = $"{ready} shot{(ready == 1 ? "" : "s")} ready, {missing} without a render or capture, {cards} card{(cards == 1 ? "" : "s")}. Planned length {sb.PlannedSeconds():0.0} s.";

            bool busy = KionjoJobs.IsBusy;
            string why = !ffmpeg ? "ffmpeg not found: set its path in Preferences > Kionjo." : busy ? "A job is running." : null;
            _build.SetEnabled(why == null && ready + cards > 0);
            _build.tooltip = why ?? (ready + cards > 0 ? "Assemble a numbered draft: MP4, SRT and commands.txt." : "Nothing to cut yet: render a shot or add a card.");
            _animatic.SetEnabled(why == null && sb.shots.Count > 0);
            string renderWhy = why ?? (!EditorApplication.isPlaying ? "Enter Play mode to render." : missing == 0 ? "Every shot has a render." : null);
            _renderMissing.SetEnabled(renderWhy == null);
            _renderMissing.tooltip = renderWhy ?? $"Render the selected take of {missing} shot{(missing == 1 ? "" : "s")} at {sb.renderPreset}, one after another.";

            bool hasOutput = _last != null && _last.OutputMp4 != null && File.Exists(_last.OutputMp4);
            _openMp4.SetEnabled(hasOutput);
            _openFolder.SetEnabled(_last != null && Directory.Exists(_last.Folder));
            if (_last != null)
            {
                _log.SetValueWithoutNotify(_last.Failed ? $"{_last.LogText()}\n\n{_last.Error}" : _last.LogText());
                if (_last.IsDone && hasOutput && !_revealed && KionjoPreferences.OpenFolderAfterCut)
                {
                    _revealed = true;
                    EditorUtility.RevealInFinder(_last.OutputMp4);
                }
            }
        }

        public CutJob LastJob => _last;
        public void BuildDraft() => Build(animatic: false);
        public void BuildAnimatic() => Build(animatic: true);
        public void RenderMissingShots() => RenderMissing();

        void Build(bool animatic)
        {
            var sb = _window.Storyboard;
            var ffmpeg = FfmpegLocator.FindFfmpeg();
            if (sb == null || ffmpeg == null) return;
            if (KionjoJobs.IsBusy)
            {
                // the plan picks its sources now: building behind running renders would miss them
                _log.SetValueWithoutNotify("A job is running. Build the draft when it has finished.");
                return;
            }
            var plan = CutPlanBuilder.Build(sb, sb.cutPreset, new FfprobeMediaProbe(), animatic, WordTimer.Resolver(ffmpeg));
            _warningItems.Clear();
            _warningItems.AddRange(plan.Warnings);
            _warningItems.AddRange(plan.Notes);
            if (_warningItems.Count == 0) _warningItems.Add("None.");
            _warnings.RefreshItems();
            if (plan.Segments.Count == 0)
            {
                _log.SetValueWithoutNotify("Nothing to cut yet: render a shot, import a capture or add a card.");
                return;
            }
            var folder = animatic
                ? KionjoPaths.NextNumbered(KionjoPaths.Animatics(sb), "animatic")
                : KionjoPaths.NextNumbered(KionjoPaths.Cuts(sb), "draft");
            _last = new CutJob(plan, folder, ffmpeg, $"{(animatic ? "Animatic" : "Draft")} {Path.GetFileName(folder)}");
            _revealed = false;
            KionjoJobs.Enqueue(_last);
            Refresh();
        }

        void RenderMissing()
        {
            var sb = _window.Storyboard;
            if (sb == null) return;
            int queued = 0;
            foreach (var s in sb.shots)
            {
                if (s.IsCard || s.outputMode != KionjoOutputMode.OfflineRender) continue;
                if (KionjoPaths.ExistingRender(sb, s) != null) continue;
                if (RenderJob.CannotRender(sb, s) != null) continue;
                KionjoJobs.Enqueue(new RenderJob(sb, s, sb.renderPreset));
                queued++;
            }
            _log.SetValueWithoutNotify(queued == 0 ? "No shot could be rendered: record takes first (Shoot tab)." : $"Queued {queued} render{(queued == 1 ? "" : "s")}.");
        }
    }
}
