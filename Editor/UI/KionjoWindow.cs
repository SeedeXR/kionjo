using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Window > Kionjo. One window, three tabs: Storyboard (plan the shots), Shoot (stage, record,
    /// replay takes in Play mode), Cut (render and assemble drafts). A status bar shows ffmpeg and
    /// the running job with Cancel.
    /// </summary>
    public class KionjoWindow : EditorWindow
    {
        public enum Tab { Storyboard, Shoot, Cut }

        public const string HelpUrl = "https://github.com/SeedeXR/kionjo#readme";
        const string LastKey = "SeedeXR.Kionjo.LastStoryboard";
        const string UssPath = "Packages/com.seedexr.kionjo/Editor/UI/KionjoWindow.uss";

        [SerializeField] KionjoStoryboard storyboard;
        [SerializeField] Tab tab;
        [SerializeField] int selected = -1;

        ObjectField _storyboardField;
        ToolbarToggle _tabStoryboard, _tabShoot, _tabCut;
        VisualElement _empty, _storyboardBody, _shootBody, _cutBody;
        ListView _shots;
        ScrollView _inspectorScroll;
        ShotInspector _inspector;
        TimelineBar _timeline;
        ShootTab _shootTab;
        CutTab _cutTab;
        Label _ffmpegStatus, _jobLabel;
        ProgressBar _jobProgress;
        Button _jobCancel;
        SerializedObject _so;

        [MenuItem("Window/Kionjo", priority = 2000)]
        public static KionjoWindow Open()
        {
            var w = GetWindow<KionjoWindow>();
            w.Show();
            return w;
        }

        /// <summary>Opens the window on a storyboard when one is double clicked.</summary>
        [UnityEditor.Callbacks.OnOpenAsset]
        static bool OnOpenAsset(int instanceId, int line)
        {
            if (EditorUtility.InstanceIDToObject(instanceId) is not KionjoStoryboard sb) return false;
            Open().Storyboard = sb;
            return true;
        }

        public KionjoStoryboard Storyboard
        {
            get => storyboard;
            set
            {
                storyboard = value;
                selected = value != null && value.shots.Count > 0 ? 0 : -1;
                if (value != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out var guid, out long _))
                    EditorPrefs.SetString(LastKey, guid);
                Rebuild();
            }
        }

        public int SelectedIndex
        {
            get => selected;
            set
            {
                selected = storyboard == null ? -1 : Mathf.Clamp(value, -1, storyboard.shots.Count - 1);
                if (_shots != null && _shots.selectedIndex != selected) _shots.SetSelectionWithoutNotify(new[] { selected });
                RefreshInspector();
                _timeline?.Refresh(storyboard, selected);
                _shootTab?.SetShot(SelectedShot);
            }
        }

        public KionjoShot SelectedShot => storyboard != null && selected >= 0 && selected < storyboard.shots.Count ? storyboard.shots[selected] : null;

        public Tab CurrentTab
        {
            get => tab;
            set
            {
                tab = value;
                ApplyTab();
            }
        }

        void OnEnable()
        {
            titleContent = new GUIContent("Kionjo", KionjoIcons.Window, "Kionjo: storyboard, shoot and cut your trailer");
            minSize = new Vector2(520, 320);
            if (storyboard == null)
            {
                var guid = EditorPrefs.GetString(LastKey, "");
                if (!string.IsNullOrEmpty(guid))
                    storyboard = AssetDatabase.LoadAssetAtPath<KionjoStoryboard>(AssetDatabase.GUIDToAssetPath(guid));
            }
            Undo.undoRedoEvent += OnUndoRedo;
            KionjoJobs.Changed += RefreshStatus;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        void OnDisable()
        {
            Undo.undoRedoEvent -= OnUndoRedo;
            KionjoJobs.Changed -= RefreshStatus;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            _shootTab?.Dispose();
        }

        void OnFocus()
        {
            FfmpegLocator.ClearCache();
            RefreshStatus();
            if (storyboard != null && _shots != null) _shots.RefreshItems();
        }

        void OnUndoRedo(in UndoRedoInfo _) => Rebuild();

        void OnPlayModeChanged(PlayModeStateChange _)
        {
            _shootTab?.SetShot(SelectedShot);
            _cutTab?.Refresh();
        }

        void CreateGUI()
        {
            var root = rootVisualElement;
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (uss != null) root.styleSheets.Add(uss);
            root.AddToClassList("kionjo-root");
            root.AddToClassList(EditorGUIUtility.isProSkin ? "kionjo-dark" : "kionjo-light");

            root.Add(BuildToolbar());

            var body = new VisualElement { name = "body" };
            body.AddToClassList("kionjo-body");
            root.Add(body);

            _empty = BuildEmptyState();
            body.Add(_empty);

            _storyboardBody = BuildStoryboardTab();
            body.Add(_storyboardBody);

            _shootTab = new ShootTab(this) { name = "shoot-body" };
            _shootBody = _shootTab;
            _shootBody.AddToClassList("kionjo-tab-body");
            body.Add(_shootBody);

            _cutTab = new CutTab(this) { name = "cut-body" };
            _cutBody = _cutTab;
            _cutBody.AddToClassList("kionjo-tab-body");
            body.Add(_cutBody);

            root.Add(BuildStatusBar());
            Rebuild();
        }

        VisualElement BuildToolbar()
        {
            var bar = new Toolbar { name = "toolbar" };
            _storyboardField = new ObjectField { name = "storyboard-field", objectType = typeof(KionjoStoryboard), allowSceneObjects = false, tooltip = "The storyboard this window edits." };
            _storyboardField.style.minWidth = 180;
            _storyboardField.style.maxWidth = 280;
            _storyboardField.RegisterValueChangedCallback(e => Storyboard = e.newValue as KionjoStoryboard);
            bar.Add(_storyboardField);
            bar.Add(new ToolbarButton(CreateStoryboard) { name = "new-storyboard", text = "New", tooltip = "Create a new storyboard asset." });
            bar.Add(new ToolbarSpacer());

            _tabStoryboard = TabToggle("tab-storyboard", "Storyboard", "Plan the shots: order, length, voice, subtitles.", Tab.Storyboard);
            _tabShoot = TabToggle("tab-shoot", "Shoot", "Stage, record and replay takes in Play mode.", Tab.Shoot);
            _tabCut = TabToggle("tab-cut", "Cut", "Render takes and assemble a draft with ffmpeg.", Tab.Cut);
            bar.Add(_tabStoryboard);
            bar.Add(_tabShoot);
            bar.Add(_tabCut);

            bar.Add(new VisualElement { style = { flexGrow = 1 } });
            bar.Add(new ToolbarButton(KionjoPreferences.Open) { name = "preferences", text = "Settings", tooltip = "Preferences > Kionjo: ffmpeg path, encoder, Python." });
            bar.Add(new ToolbarButton(() => Application.OpenURL(HelpUrl)) { name = "help", text = "Help", tooltip = "Open the Kionjo guide." });
            return bar;
        }

        ToolbarToggle TabToggle(string elementName, string label, string tip, Tab value)
        {
            var t = new ToolbarToggle { name = elementName, text = label, tooltip = tip };
            t.RegisterValueChangedCallback(e =>
            {
                if (e.newValue) CurrentTab = value;
                else if (tab == value) t.SetValueWithoutNotify(true); // a tab stays pressed until another is chosen
            });
            return t;
        }

        VisualElement BuildEmptyState()
        {
            var empty = new VisualElement { name = "empty-state" };
            empty.AddToClassList("kionjo-empty");
            var icon = new Image { image = AssetDatabase.LoadAssetAtPath<Texture2D>(KionjoIcons.Folder + "kionjo-logo.png") };
            icon.AddToClassList("kionjo-empty__icon");
            empty.Add(icon);
            var title = new Label("No storyboard open");
            title.AddToClassList("kionjo-empty__title");
            empty.Add(title);
            var text = new Label("A storyboard is your trailer on one page: the shots in order, how long each runs, what is said over it. Create one to start, or open an existing one.");
            text.AddToClassList("kionjo-empty__text");
            empty.Add(text);
            empty.Add(new Button(CreateStoryboard) { name = "create-storyboard", text = "Create storyboard", tooltip = "Create a storyboard asset in your project." });
            var drop = new Label("or drop a Storyboard asset here");
            drop.AddToClassList("kionjo-empty__drop");
            drop.RegisterCallback<DragUpdatedEvent>(_ =>
            {
                if (DraggedStoryboard() != null)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                    drop.AddToClassList("kionjo-empty__drop--hover");
                }
            });
            drop.RegisterCallback<DragLeaveEvent>(_ => drop.RemoveFromClassList("kionjo-empty__drop--hover"));
            drop.RegisterCallback<DragPerformEvent>(_ =>
            {
                var sb = DraggedStoryboard();
                if (sb == null) return;
                DragAndDrop.AcceptDrag();
                Storyboard = sb;
            });
            empty.Add(drop);
            return empty;
        }

        static KionjoStoryboard DraggedStoryboard()
        {
            foreach (var o in DragAndDrop.objectReferences)
                if (o is KionjoStoryboard sb) return sb;
            return null;
        }

        VisualElement BuildStoryboardTab()
        {
            var tabBody = new VisualElement { name = "storyboard-body" };
            tabBody.AddToClassList("kionjo-tab-body");
            var split = new TwoPaneSplitView(0, 330, TwoPaneSplitViewOrientation.Horizontal) { name = "storyboard-split" };
            split.style.flexGrow = 1;

            var listPane = new VisualElement();
            listPane.AddToClassList("kionjo-list-pane");
            _shots = new ListView
            {
                name = "shot-list",
                fixedItemHeight = 64,
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                selectionType = SelectionType.Single,
                showAddRemoveFooter = false,
                makeItem = ShotCard.Make,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
            };
            _shots.style.flexGrow = 1;
            _shots.bindItem = (e, i) =>
            {
                if (storyboard != null && i < storyboard.shots.Count) ShotCard.Bind(e, storyboard, storyboard.shots[i]);
            };
            _shots.selectionChanged += _ => SelectedIndex = _shots.selectedIndex;
            _shots.itemIndexChanged += OnShotMoved;
            listPane.Add(_shots);

            var footer = new VisualElement();
            footer.AddToClassList("kionjo-list-footer");
            footer.Add(new Button(() => AddShot(KionjoShotType.Performance)) { name = "add-shot", text = "Add shot", tooltip = "Add a shot after the selected one." });
            footer.Add(new Button(() => AddShot(KionjoShotType.Card)) { name = "add-card", text = "Add card", tooltip = "Add a black, text or image card (an opening line, closing lines, the cover)." });
            footer.Add(new Button(DuplicateSelected) { name = "duplicate-shot", text = "Duplicate", tooltip = "Duplicate the selected shot (Ctrl+D)." });
            footer.Add(new Button(RemoveSelected) { name = "remove-shot", text = "Remove", tooltip = "Remove the selected shot (Delete). Takes on disk are kept." });
            listPane.Add(footer);
            split.Add(listPane);

            _inspectorScroll = new ScrollView(ScrollViewMode.Vertical) { name = "shot-inspector" };
            split.Add(_inspectorScroll);
            tabBody.Add(split);

            _timeline = new TimelineBar { name = "timeline-bar" };
            _timeline.ShotClicked += i => SelectedIndex = i;
            tabBody.Add(_timeline);
            return tabBody;
        }

        VisualElement BuildStatusBar()
        {
            var bar = new VisualElement { name = "status-bar" };
            bar.AddToClassList("kionjo-status");
            _ffmpegStatus = new Label { name = "ffmpeg-status" };
            _ffmpegStatus.AddToClassList("kionjo-status__ffmpeg");
            _ffmpegStatus.RegisterCallback<ClickEvent>(_ =>
            {
                if (!FfmpegLocator.IsAvailable) KionjoPreferences.Open();
            });
            bar.Add(_ffmpegStatus);
            _jobLabel = new Label { name = "job-label" };
            _jobLabel.AddToClassList("kionjo-status__job");
            bar.Add(_jobLabel);
            _jobProgress = new ProgressBar { name = "job-progress", lowValue = 0, highValue = 1 };
            _jobProgress.AddToClassList("kionjo-status__progress");
            bar.Add(_jobProgress);
            _jobCancel = new Button(KionjoJobs.Cancel) { name = "job-cancel", text = "Cancel", tooltip = "Stop the running job (and any queued renders)." };
            bar.Add(_jobCancel);
            return bar;
        }

        // ---- Actions -------------------------------------------------------------------------

        void CreateStoryboard()
        {
            var path = EditorUtility.SaveFilePanelInProject("New storyboard", "Storyboard", "asset", "Where should the storyboard asset live?");
            if (string.IsNullOrEmpty(path)) return;
            var sb = CreateInstance<KionjoStoryboard>();
            sb.title = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(sb, path);
            AssetDatabase.SaveAssets();
            Storyboard = sb;
            EditorGUIUtility.PingObject(sb);
        }

        public void AddShot(KionjoShotType type)
        {
            if (storyboard == null) return;
            var shot = storyboard.AddShot(type);
            int at = selected >= 0 ? selected + 1 : storyboard.shots.Count - 1;
            if (at != storyboard.shots.Count - 1)
            {
                storyboard.shots.RemoveAt(storyboard.shots.Count - 1); // inserted after the selection, same undo step
                storyboard.shots.Insert(at, shot);
            }
            selected = at;
            Rebuild();
        }

        public void DuplicateSelected()
        {
            if (SelectedShot == null) return;
            storyboard.Duplicate(selected);
            selected++;
            Rebuild();
        }

        public void RemoveSelected()
        {
            if (SelectedShot == null) return;
            storyboard.RemoveAt(selected);
            selected = Mathf.Min(selected, storyboard.shots.Count - 1);
            Rebuild();
        }

        public void MoveSelected(int delta)
        {
            if (SelectedShot == null) return;
            int to = selected + delta;
            if (to < 0 || to >= storyboard.shots.Count) return;
            storyboard.Move(selected, to);
            selected = to;
            Rebuild();
        }

        /// <summary>
        /// The list has already moved the item; put it back, then move it through the storyboard so
        /// the move is recorded for Undo.
        /// </summary>
        void OnShotMoved(int from, int to)
        {
            var list = storyboard.shots;
            var moved = list[to];
            list.RemoveAt(to);
            list.Insert(from, moved);
            storyboard.Move(from, to);
            selected = to;
            Rebuild();
        }

        // ---- Refresh -------------------------------------------------------------------------

        public void Rebuild()
        {
            if (_shots == null) return;
            if (storyboard != null) selected = Mathf.Clamp(selected, -1, storyboard.shots.Count - 1);
            _storyboardField.SetValueWithoutNotify(storyboard);
            _so = storyboard != null ? new SerializedObject(storyboard) : null;
            _shots.itemsSource = storyboard != null ? storyboard.shots : null;
            _shots.Rebuild();
            if (selected >= 0) _shots.SetSelectionWithoutNotify(new[] { selected });
            else _shots.ClearSelection();
            RefreshInspector();
            _timeline.Refresh(storyboard, selected);
            _shootTab.SetStoryboard(storyboard);
            _shootTab.SetShot(SelectedShot);
            _cutTab.Refresh();
            ApplyTab();
            RefreshStatus();
        }

        void RefreshInspector()
        {
            if (_inspectorScroll == null) return;
            _inspectorScroll.Clear();
            if (storyboard == null) return;
            if (SelectedShot == null)
            {
                var hint = new Label(storyboard.shots.Count == 0 ? "Add your first shot." : "Select a shot to edit it.");
                hint.AddToClassList("kionjo-empty__text");
                hint.style.marginTop = 20;
                hint.style.alignSelf = Align.Center;
                _inspectorScroll.Add(hint);
                _inspectorScroll.Add(StoryboardSettings());
                return;
            }
            _so.Update();
            _inspector = new ShotInspector(this, _so, selected);
            _inspector.Changed += () =>
            {
                _shots.RefreshItem(selected);
                _timeline.Refresh(storyboard, selected);
            };
            _inspectorScroll.Add(_inspector);
        }

        /// <summary>Storyboard wide settings, shown when no shot is selected.</summary>
        VisualElement StoryboardSettings()
        {
            var box = new VisualElement { style = { marginTop = 12 } };
            box.AddToClassList("kionjo-inspector");
            var head = new Label("Storyboard");
            head.AddToClassList("kionjo-section");
            box.Add(head);
            foreach (var p in new[] { "title", "outputFolder", "targetSeconds", "renderPreset", "cutPreset", "subtitles", "music", "musicCrossfadeSeconds", "ducking", "loudness", "renderQuality" })
                box.Add(new UnityEditor.UIElements.PropertyField(_so.FindProperty(p)));
            box.Bind(_so);
            box.TrackSerializedObjectValue(_so, _ => _timeline.Refresh(storyboard, selected));
            return box;
        }

        void ApplyTab()
        {
            if (_empty == null) return;
            bool has = storyboard != null;
            Show(_empty, !has);
            Show(_storyboardBody, has && tab == Tab.Storyboard);
            Show(_shootBody, has && tab == Tab.Shoot);
            Show(_cutBody, has && tab == Tab.Cut);
            _tabStoryboard.SetValueWithoutNotify(tab == Tab.Storyboard);
            _tabShoot.SetValueWithoutNotify(tab == Tab.Shoot);
            _tabCut.SetValueWithoutNotify(tab == Tab.Cut);
            _tabStoryboard.SetEnabled(has);
            _tabShoot.SetEnabled(has);
            _tabCut.SetEnabled(has);
            if (tab == Tab.Cut) _cutTab.Refresh();
            if (tab == Tab.Shoot) _shootTab.SetShot(SelectedShot);
        }

        static void Show(VisualElement e, bool visible) => e.EnableInClassList("kionjo-hidden", !visible);

        void RefreshStatus()
        {
            if (_ffmpegStatus == null) return;
            bool ok = FfmpegLocator.IsAvailable;
            _ffmpegStatus.text = ok ? FfmpegLocator.Status() : "ffmpeg not found: click to set its path";
            _ffmpegStatus.EnableInClassList("kionjo-bad", !ok);
            _ffmpegStatus.tooltip = ok ? "ffmpeg renders and cuts. Change it in Preferences > Kionjo." : "Kionjo needs ffmpeg to render and cut. Click to open Preferences > Kionjo.";
            var job = KionjoJobs.Current ?? KionjoJobs.Last;
            bool running = KionjoJobs.IsBusy;
            _jobLabel.text = job == null ? "" : running
                ? $"{job.Title}: {job.Status}{(KionjoJobs.Queued > 0 ? $" (+{KionjoJobs.Queued} queued)" : "")}"
                : job.Failed ? $"{job.Title} failed" : job.Status;
            _jobLabel.EnableInClassList("kionjo-bad", job != null && !running && job.Failed);
            _jobLabel.tooltip = job != null && job.Failed ? job.Error : "";
            Show(_jobProgress, running);
            _jobProgress.value = job?.Progress ?? 0f;
            _jobProgress.title = running ? $"{Mathf.RoundToInt((job?.Progress ?? 0f) * 100f)}%" : "";
            Show(_jobCancel, running);
            if (!running) _cutTab?.Refresh();
            _shootTab?.RefreshIfIdle();
            if (storyboard != null) KionjoStateFile.Write(storyboard, SelectedShot?.id, force: !running);
        }
    }
}
