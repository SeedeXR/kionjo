using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Builds a post from the selected entry: hook (templates and the entry's options), overlays,
    /// per item fit, audio, end, cover, a preview frame with the safe zones, live warnings, the build
    /// button, then Open folder and Mark as posted. A grid of the latest covers sits at the bottom.
    /// </summary>
    public class PostBuildView : VisualElement
    {
        const float PreviewW = 270f, PreviewH = 480f;

        JournalEntry _e;
        KionjoBrandKit _kit;
        KionjoPost _post;
        PostJob _lastJob;
        readonly IMediaInfo _info = new FfprobeMediaInfo();
        TextField _hook;
        VisualElement _warnings, _after;
        Button _build;

        /// <summary>Whether a job is running; replaced in tests.</summary>
        public Func<bool> IsBusy = () => KionjoJobs.IsBusy;
        public KionjoPost Post => _post;

        public PostBuildView()
        {
            RegisterCallback<AttachToPanelEvent>(_ => KionjoJobs.Changed += OnJobsChanged);
            RegisterCallback<DetachFromPanelEvent>(_ => KionjoJobs.Changed -= OnJobsChanged);
        }

        public void Bind(JournalEntry e, KionjoBrandKit kit)
        {
            _e = e;
            _kit = kit;
            if (e == null)
            {
                Clear();
                return;
            }
            if (_post == null || !e.posts.Contains(_post)) _post = e.posts.LastOrDefault() ?? NewPost();
            Rebuild();
        }

        KionjoPost NewPost()
        {
            var p = KionjoPost.FromEntry(_e, _info);
            if (_kit != null) p.endText = _kit.callToAction;
            return p;
        }

        void Rebuild()
        {
            Clear();
            Add(KionjoHelp.Section("Post"));
            var names = _e.posts.Select(p => $"{p.name} ({p.id})").Append("New post from entry").ToList();
            int at = _e.posts.IndexOf(_post);
            var picker = new DropdownField("Post", names, at >= 0 ? at : names.Count - 1) { name = "post-picker" };
            picker.RegisterValueChangedCallback(_ =>
            {
                int i = picker.index;
                _post = i < _e.posts.Count ? _e.posts[i] : NewPost();
                Rebuild();
            });
            Add(picker);

            _hook = new TextField("Hook") { name = "post-hook", value = _post.hook ?? "", isDelayed = true, tooltip = "Shown from 0.5 s in the title band: 7 words at most. You choose it; the buttons only offer drafts." };
            _hook.RegisterValueChangedCallback(ev =>
            {
                _post.hook = ev.newValue;
                Changed();
            });
            Add(_hook);
            var hooks = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            foreach (HookKind k in Enum.GetValues(typeof(HookKind)))
            {
                var kind = k;
                hooks.Add(new Button(() => FillHook(kind)) { name = $"hook-{kind.ToString().ToLowerInvariant()}", text = kind == HookKind.Pov ? "POV" : kind.ToString() });
            }
            foreach (var option in _e.hookOptions.Where(o => !string.IsNullOrWhiteSpace(o)))
                hooks.Add(new Button(() => SetHook(option)) { text = option, tooltip = "A hook option from the entry." });
            Add(hooks);

            Add(new Label("Overlays (one sentence on screen at a time)"));
            var overlays = new ListView(_post.overlays, 22, () => new TextField { isDelayed = true }, (el, i) =>
            {
                var f = (TextField)el;
                f.SetValueWithoutNotify(_post.overlays[i]);
                f.userData = i;
            })
            {
                name = "post-overlays", reorderable = true, reorderMode = ListViewReorderMode.Animated, showAddRemoveFooter = true,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
            };
            overlays.RegisterCallback<ChangeEvent<string>>(ev =>
            {
                if (ev.target is TextField f && f.userData is int i && i < _post.overlays.Count)
                {
                    _post.overlays[i] = ev.newValue;
                    Changed();
                }
            });
            overlays.itemIndexChanged += (_, _) => Changed();
            overlays.itemsAdded += _ => Changed();
            overlays.itemsRemoved += _ => Changed();
            Add(overlays);

            Add(KionjoHelp.Section("Footage"));
            foreach (var item in _post.items)
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
                row.Add(new Label(Path.GetFileName(item.path)) { style = { minWidth = 160 } });
                var fit = new EnumField(item.fit) { tooltip = "Crop to 9:16, or show the whole picture over a blurred fill." };
                fit.RegisterValueChangedCallback(ev =>
                {
                    item.fit = (PostFit)ev.newValue;
                    Changed();
                });
                row.Add(fit);
                var centre = new Slider("Crop centre", 0f, 1f) { value = item.cropCentre, tooltip = "Where the crop window sits, left to right." };
                centre.style.minWidth = 180;
                centre.RegisterValueChangedCallback(ev =>
                {
                    item.cropCentre = ev.newValue;
                    Changed(false);
                });
                row.Add(centre);
                var speech = new Toggle("Captions") { value = item.captionSpeech, tooltip = "Transcribe speech in this clip into word by word captions (needs faster-whisper)." };
                speech.RegisterValueChangedCallback(ev =>
                {
                    item.captionSpeech = ev.newValue;
                    Changed();
                });
                row.Add(speech);
                Add(row);
            }

            Add(KionjoHelp.Section("Sound"));
            Add(Bound(new Toggle("Clip sound") { value = _post.clipSound }, v => _post.clipSound = v));
            Add(Bound(new Slider("Clip volume", 0f, 2f) { value = _post.clipVolume }, v => _post.clipVolume = v));
            var musicRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var music = new TextField("Music") { value = _post.musicPath ?? "", isDelayed = true, style = { flexGrow = 1 }, tooltip = "Optional. The bundle always has a no-music version for a trending sound added in the app." };
            music.RegisterValueChangedCallback(ev =>
            {
                _post.musicPath = ev.newValue;
                Changed();
            });
            musicRow.Add(music);
            musicRow.Add(new Button(() =>
            {
                var p = EditorUtility.OpenFilePanel("Music", "", "wav,mp3,m4a,ogg,flac");
                if (!string.IsNullOrEmpty(p)) music.value = p;
            }) { text = "..." });
            Add(musicRow);
            Add(Bound(new Slider("Music volume", 0f, 2f) { value = _post.musicVolume }, v => _post.musicVolume = v));
            Add(Bound(new FloatField("Music start") { value = _post.musicStart }, v => _post.musicStart = v));

            Add(KionjoHelp.Section("End and cover"));
            var end = new EnumField("End", _post.end);
            end.RegisterValueChangedCallback(ev =>
            {
                _post.end = (PostEnd)ev.newValue;
                Changed();
            });
            Add(end);
            Add(Bound(new TextField("End text") { value = _post.endText ?? "", isDelayed = true }, v => _post.endText = v));
            Add(Bound(new TextField("Cover title") { value = _post.coverTitle ?? "", isDelayed = true, tooltip = "1 to 3 words in capitals, drawn in the kit accent." }, v => _post.coverTitle = v));
            Add(Bound(new FloatField("Cover time") { value = _post.coverTime }, v => _post.coverTime = v));

            Add(KionjoHelp.Section("Preview"));
            var previewRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.FlexStart } };
            var time = new FloatField("Time") { value = 1f, style = { minWidth = 120 } };
            var frame = PreviewFrame();
            previewRow.Add(time);
            previewRow.Add(new Button(() => RenderPreview(time.value, frame)) { name = "preview-frame", text = "Preview frame", tooltip = "Render one frame with the text burned in; the bands show where the app covers the picture." });
            Add(previewRow);
            Add(frame);

            _warnings = new VisualElement { name = "post-warnings" };
            Add(_warnings);
            _build = new Button(Build) { name = "post-build", text = "Build post", tooltip = "Render the whole bundle: video with text, clean video, cover, caption text, recipe." };
            Add(_build);
            _after = new VisualElement { name = "post-after" };
            Add(_after);
            Add(Grid());
            RefreshWarnings();
            RefreshBuildState();
            RefreshAfter();
        }

        VisualElement Bound<TV>(BaseField<TV> field, Action<TV> set)
        {
            field.RegisterValueChangedCallback(ev =>
            {
                set(ev.newValue);
                Changed();
            });
            return field;
        }

        public void FillHook(HookKind kind) => SetHook(HookTemplates.Fill(kind, _e));

        public void SetHook(string text)
        {
            _post.hook = text;
            _hook.SetValueWithoutNotify(text);
            Changed();
        }

        void Changed(bool warnings = true)
        {
            // A new post joins the entry on its first edit, so a rebind never throws the draft away.
            if (!_e.posts.Contains(_post)) _e.posts.Add(_post);
            if (!string.IsNullOrEmpty(_e.Folder)) KionjoJournal.SaveEdited(_e);
            if (warnings) RefreshWarnings();
        }

        /// <summary>Warnings of the post as it stands (media probed once per file).</summary>
        public List<string> CurrentWarnings()
        {
            if (_post.items.Any(i => string.IsNullOrEmpty(i.path) || !File.Exists(i.path)))
                return _post.items.Where(i => string.IsNullOrEmpty(i.path) || !File.Exists(i.path)).Select(i => $"Missing media: {i.path}").ToList();
            var plan = PostPlanBuilder.Build(_post, _info, null, _kit);
            var list = PostWarnings.Check(plan, _e, _post, _kit);
            if (PostWarnings.Refuse(plan, out var reason)) list.Insert(0, reason);
            list.Insert(0, $"Length {plan.TotalSeconds:0.0} s");
            return list;
        }

        void RefreshWarnings()
        {
            _warnings.Clear();
            foreach (var w in CurrentWarnings())
            {
                var l = new Label(w) { style = { whiteSpace = WhiteSpace.Normal } };
                if (!w.StartsWith("Length ")) l.AddToClassList("kionjo-bad");
                _warnings.Add(l);
            }
        }

        public void RefreshBuildState() => _build?.SetEnabled(!IsBusy() && _post.items.Count > 0);

        void OnJobsChanged()
        {
            RefreshBuildState();
            if (_lastJob != null && _lastJob.IsDone) RefreshAfter();
        }

        void Build()
        {
            var ffmpeg = FfmpegLocator.FindFfmpeg();
            if (ffmpeg == null)
            {
                EditorUtility.DisplayDialog("Kionjo", "ffmpeg was not found. Set it in Preferences > Kionjo.", "OK");
                return;
            }
            if (!_e.posts.Contains(_post)) _e.posts.Add(_post);
            KionjoJournal.SaveEdited(_e);
            _lastJob = new PostJob(_e, _post, _kit, KionjoPaths.PostsRoot(KionjoPostsSettings.instance.outputFolder), ffmpeg, _info, path => WordTimer.Words(path, out _));
            KionjoJobs.Enqueue(_lastJob);
            RefreshBuildState();
        }

        /// <summary>The entry's latest built vNN folder, or null.</summary>
        static string LatestFolder(JournalEntry e) =>
            PostFolder.Latest(PostFolder.EntryFolder(KionjoPaths.PostsRoot(KionjoPostsSettings.instance.outputFolder), e));

        void RefreshAfter()
        {
            if (_after == null) return;
            _after.Clear();
            if (_lastJob != null && _lastJob.IsDone && _lastJob.Failed)
            {
                var l = new Label(_lastJob.Error) { style = { whiteSpace = WhiteSpace.Normal } };
                l.AddToClassList("kionjo-bad");
                _after.Add(l);
            }
            if (_lastJob != null && _lastJob.IsDone && !_lastJob.Failed)
                foreach (var w in _lastJob.Warnings) _after.Add(new Label(w) { style = { whiteSpace = WhiteSpace.Normal } });
            var folder = LatestFolder(_e);
            if (string.IsNullOrEmpty(_post.id) || folder == null || !Directory.Exists(folder)) return;
            _after.Add(new Button(() => EditorUtility.RevealInFinder(Path.Combine(folder, PostFolder.Brief))) { name = "post-open-folder", text = "Open folder" });
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var url = new TextField("Posted URL") { value = _post.postedUrl ?? "", style = { flexGrow = 1 } };
            row.Add(url);
            row.Add(new Button(() =>
            {
                _post.posted = true;
                _post.postedUrl = url.value;
                _e.status = "Posted";
                KionjoJournal.SaveEdited(_e);
                // Older versions go; anything found nowhere else moves into this version's older/ folder.
                var latest = LatestFolder(_e);
                if (latest != null) PostFolder.CleanAfterPosted(Path.GetDirectoryName(latest), latest);
                RefreshAfter();
            }) { name = "post-mark-posted", text = _post.posted ? "Posted" : "Mark as posted", tooltip = "Records that you posted it yourself in the app. Kionjo never posts." });
            _after.Add(row);
        }

        // ---- Preview -------------------------------------------------------------------------

        static VisualElement PreviewFrame()
        {
            var box = new VisualElement { name = "post-preview", style = { width = PreviewW, height = PreviewH, backgroundColor = Color.black, marginTop = 4, marginBottom = 4 } };
            box.Add(new Image { name = "post-preview-image", scaleMode = ScaleMode.ScaleToFit, style = { position = Position.Absolute, left = 0, top = 0, right = 0, bottom = 0 } });
            var z = SafeZone.For(1080, 1920);
            float k = PreviewH / 1920f;
            var shade = new Color(1f, 0.3f, 0.2f, 0.25f);
            box.Add(new VisualElement { style = { position = Position.Absolute, left = 0, right = 0, top = 0, height = z.Top * k, backgroundColor = shade } });
            box.Add(new VisualElement { style = { position = Position.Absolute, left = 0, right = 0, bottom = 0, height = z.Bottom * k, backgroundColor = shade } });
            box.Add(new VisualElement { style = { position = Position.Absolute, right = 0, top = z.Top * k, bottom = z.Bottom * k, width = z.Right * k, backgroundColor = shade } });
            box.Add(new VisualElement { style = { position = Position.Absolute, left = 0, top = z.Top * k, bottom = z.Bottom * k, width = z.Left * k, backgroundColor = shade } });
            var band = new VisualElement { style = { position = Position.Absolute, left = 0, right = 0, top = z.TitleTop * k, height = (z.TitleBottom - z.TitleTop) * k } };
            band.style.borderTopWidth = band.style.borderBottomWidth = 1;
            band.style.borderTopColor = band.style.borderBottomColor = new Color(0.3f, 0.85f, 1f, 0.9f);
            band.tooltip = "Title band: hook and cover title stay here so the grid crop keeps them.";
            box.Add(band);
            return box;
        }

        void RenderPreview(float filmTime, VisualElement frame)
        {
            var ffmpeg = FfmpegLocator.FindFfmpeg();
            if (ffmpeg == null || _post.items.Count == 0) return;
            var plan = PostPlanBuilder.Build(_post, _info, null, _kit);
            filmTime = Mathf.Clamp(filmTime, 0f, Mathf.Max(0f, plan.TotalSeconds - 0.05f));
            float start = 0f;
            var seg = plan.Segments.Last();
            foreach (var s in plan.Segments)
            {
                if (filmTime < start + s.Duration)
                {
                    seg = s;
                    break;
                }
                start += s.Duration;
            }
            float local = Mathf.Clamp(filmTime - start, 0f, seg.Duration);

            var dir = Path.Combine(KionjoPaths.ProjectRoot, "Library", "KionjoCache", "preview");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, PostCommandBuilder.Ass), AssWriter.Write(plan.Width, plan.Height, plan.Texts, plan.CaptionWords, _kit), new System.Text.UTF8Encoding(false));
            foreach (var font in new[] { _kit != null ? _kit.titleFontPath : null, _kit != null ? _kit.bodyFontPath : null, CutJob.DefaultFont() }.Where(f => !string.IsNullOrEmpty(f) && File.Exists(f)))
                File.Copy(font, Path.Combine(dir, Path.GetFileName(font)), true);

            string t = CutCommandBuilder.F(filmTime);
            string burn = $"setpts=PTS-STARTPTS+{t}/TB,ass={PostCommandBuilder.Ass}:fontsdir=.";
            var fit = PostCommandBuilder.FitFilter(seg, plan);
            var cmd = new FfmpegCommand { Executable = ffmpeg, WorkingDirectory = dir, Label = "Preview frame" }.Add("-v", "error", "-y");
            // A still must loop: fps and tpad give no frame from a single picture without a following one.
            if (FfprobeMediaInfo.IsImage(seg.SourcePath)) cmd.Add("-loop", "1", "-t", "1", "-i", seg.SourcePath);
            else cmd.Add("-ss", CutCommandBuilder.F(seg.SourceIn + local), "-i", seg.SourcePath);
            if (fit.EndsWith("[v]")) cmd.Add("-filter_complex", $"{fit};[v]{burn}[o]", "-map", "[o]");
            else cmd.Add("-vf", $"{fit},{burn}");
            cmd.Add("-frames:v", "1", "-update", "1", "preview.png");
            var result = KionjoProcess.Run(cmd, 30000);
            if (result.ExitCode != 0)
            {
                Debug.LogWarning($"Kionjo: preview frame failed. {result.StdErrTail}");
                return;
            }
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(Path.Combine(dir, "preview.png")));
            frame.Q<Image>("post-preview-image").image = tex;
        }

        // ---- Grid ----------------------------------------------------------------------------

        VisualElement Grid()
        {
            var fold = new Foldout { name = "post-grid", text = "Grid", value = false, tooltip = "The covers of your 9 latest built posts as the profile grid shows them (3:4)." };
            fold.RegisterValueChangedCallback(ev =>
            {
                if (ev.target == fold && ev.newValue) FillGrid(fold);
            });
            return fold;
        }

        void FillGrid(Foldout fold)
        {
            fold.Clear();
            var covers = new List<string>();
            foreach (var e in KionjoJournal.LoadAll(KionjoJournal.Folder, null))
            {
                var latest = e.posts.Count > 0 ? LatestFolder(e) : null;
                var cover = latest == null ? null : Path.Combine(latest, PostFolder.Post, PostCommandBuilder.Cover);
                if (cover != null && File.Exists(cover)) covers.Add(cover);
            }
            var wrap = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, width = 3 * 92 } };
            foreach (var c in covers.Take(9))
            {
                var tex = new Texture2D(2, 2);
                tex.LoadImage(File.ReadAllBytes(c));
                wrap.Add(new Image { image = tex, scaleMode = ScaleMode.ScaleAndCrop, tooltip = c, style = { width = 90, height = 120, marginRight = 2, marginBottom = 2 } });
            }
            if (covers.Count == 0) fold.Add(new Label("No built posts yet."));
            fold.Add(wrap);
        }
    }
}
