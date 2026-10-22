using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Shoot one shot at a time: stage it, see whether it is ready, record takes (Space or the big
    /// button), replay and pick takes, then Done for the next shot. Recording needs Play mode.
    /// </summary>
    public class ShootTab : VisualElement, IDisposable
    {
        readonly KionjoWindow _window;
        ShootController _controller;
        KionjoShot _shot;
        readonly VisualElement _notPlaying, _body, _verify;
        readonly Label _title, _subtitle, _badge, _elapsed, _hint, _lastShot;
        readonly Button _record, _stage, _done, _addRecorder;
        readonly ListView _takes;
        bool _wasRecording;

        public ShootController Controller => _controller;

        public ShootTab(KionjoWindow window)
        {
            _window = window;
            AddToClassList("kionjo-shoot");

            _notPlaying = new VisualElement { name = "shoot-not-playing" };
            var why = new Label("Shooting happens in Play mode: put the headset on (or use your camera rig), enter Play mode, then record each shot here.");
            why.AddToClassList("kionjo-empty__text");
            why.style.marginTop = 16;
            _notPlaying.Add(why);
            _notPlaying.Add(new Button(EditorApplication.EnterPlaymode) { name = "enter-play-mode", text = "Enter Play mode", tooltip = "Start Play mode to record and replay takes." });
            _addRecorder = new Button(AddRecorderToMainCamera)
            {
                name = "add-recorder",
                text = "Add a Kionjo Recorder (head = main camera)",
                tooltip = "Adds a Kionjo Recorder to the main camera's top parent, with the camera as the Head track. Add your hand transforms as more tracks.",
            };
            _notPlaying.Add(_addRecorder);
            Add(_notPlaying);

            _body = new VisualElement { name = "shoot-body-content" };
            _title = new Label { name = "shoot-shot-title" };
            _title.AddToClassList("kionjo-shoot__header");
            _body.Add(_title);
            _subtitle = new Label { name = "shoot-shot-info" };
            _subtitle.AddToClassList("kionjo-verify-line--info");
            _body.Add(_subtitle);

            var stageRow = new VisualElement();
            stageRow.AddToClassList("kionjo-row");
            _stage = new Button(() => _controller?.Stage()) { name = "shoot-stage", text = "Stage", tooltip = "Apply the hide and show lists, run your stage handlers, then check the scene." };
            stageRow.Add(_stage);
            stageRow.Add(new Button(() => _controller?.Verify(forRender: false)) { name = "shoot-verify", text = "Check", tooltip = "Check the scene against this shot without changing it." });
            _badge = new Label { name = "shoot-ready-badge" };
            _badge.AddToClassList("kionjo-badge");
            _badge.style.marginLeft = 8;
            stageRow.Add(_badge);
            _body.Add(stageRow);
            _verify = new VisualElement { name = "shoot-verify-list" };
            _body.Add(_verify);

            _record = new Button(() => _controller?.ToggleRecord()) { name = "shoot-record", text = "Record (Space)" };
            _record.AddToClassList("kionjo-record");
            _body.Add(_record);
            _elapsed = new Label { name = "shoot-elapsed" };
            _elapsed.AddToClassList("kionjo-elapsed");
            _body.Add(_elapsed);

            var takesHead = new Label("Takes");
            takesHead.AddToClassList("kionjo-section");
            _body.Add(takesHead);
            _takes = new ListView
            {
                name = "shoot-takes",
                fixedItemHeight = 26,
                selectionType = SelectionType.None,
                makeItem = MakeTakeRow,
                bindItem = BindTakeRow,
            };
            _takes.style.minHeight = 80;
            _takes.style.maxHeight = 220;
            _body.Add(_takes);

            var doneRow = new VisualElement();
            doneRow.AddToClassList("kionjo-row");
            _done = new Button(PressDone) { name = "shoot-done", text = "Done, next shot (N)", tooltip = "This shot is done: move to the next shot that is not a card." };
            doneRow.Add(_done);
            _lastShot = new Label { name = "shoot-last-shot" };
            _lastShot.style.marginLeft = 8;
            _lastShot.style.alignSelf = Align.Center;
            doneRow.Add(_lastShot);
            _body.Add(doneRow);

            _hint = new Label("Space starts and stops a take while this window has focus. Your game can also call KionjoRecorder.ToggleTake() from a controller button. Replay with the headset unplugged.");
            _hint.AddToClassList("kionjo-verify-line--info");
            _hint.style.whiteSpace = WhiteSpace.Normal;
            _hint.style.marginTop = 10;
            _body.Add(_hint);
            Add(_body);

            schedule.Execute(Tick).Every(100);
        }

        public void SetStoryboard(KionjoStoryboard sb)
        {
            if (_controller?.Storyboard == sb) return;
            _controller?.StopReplay();
            _controller = sb != null ? new ShootController(sb) : null;
            if (_controller != null) _controller.Changed += Refresh;
            _shot = null;
        }

        public void SetShot(KionjoShot shot)
        {
            if (_controller == null) return;
            if (shot != _shot || _controller.Current != shot)
            {
                _shot = shot;
                _lastShot.text = "";
                _controller.Select(shot);
            }
            Refresh();
        }

        public void RefreshIfIdle()
        {
            if (_controller != null && !_controller.IsRecording) Refresh();
        }

        public void PressDone()
        {
            if (_controller == null || _controller.Current == null) return;
            int before = _window.SelectedIndex;
            if (_controller.Done())
            {
                _window.SelectedIndex = _window.Storyboard.shots.IndexOf(_controller.Current);
                _lastShot.text = $"{_window.Storyboard.shots[before].code} done.";
            }
            else _lastShot.text = "That was the last shot. Go to Cut to render and assemble.";
        }

        void Tick()
        {
            if (_controller == null) return;
            bool recording = _controller.IsRecording;
            if (recording != _wasRecording)
            {
                // a take ended by the cap or by the game's own button
                _controller.RefreshTakes();
                _controller.Arm();
                _wasRecording = recording;
                Refresh();
            }
            if (recording)
            {
                var rec = ShootController.Recorder;
                _elapsed.text = $"Recording {rec.Elapsed:0.0} s  (stops by itself at {rec.maxTakeSeconds:0} s)";
            }
        }

        void Refresh()
        {
            bool playing = EditorApplication.isPlaying;
            _notPlaying.style.display = playing ? DisplayStyle.None : DisplayStyle.Flex;
            _body.style.display = playing ? DisplayStyle.Flex : DisplayStyle.None;
            bool hasRecorder = UnityEngine.Object.FindFirstObjectByType<KionjoRecorder>() != null;
            _addRecorder.style.display = hasRecorder ? DisplayStyle.None : DisplayStyle.Flex;
            if (_controller == null) return;

            var shot = _controller.Current;
            _title.text = shot == null ? "Select a shot" : $"{shot.code}  {shot.name}";
            _subtitle.text = shot == null ? "" : $"{shot.type}, {shot.PlannedSeconds():0.#} s planned{(string.IsNullOrEmpty(shot.location) ? "" : $", {shot.location}")}";

            _verify.Clear();
            var report = _controller.Report;
            _badge.style.display = report == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (report != null)
            {
                _badge.text = report.Ready ? "Ready" : "Not ready";
                _badge.EnableInClassList("kionjo-badge--ready", report.Ready);
                _badge.EnableInClassList("kionjo-badge--not-ready", !report.Ready);
                foreach (var (severity, text) in report.Lines)
                {
                    var line = new Label((severity == StageSeverity.Error ? "x " : severity == StageSeverity.Warning ? "! " : "i ") + text);
                    line.AddToClassList("kionjo-verify-line");
                    line.AddToClassList($"kionjo-verify-line--{severity.ToString().ToLowerInvariant()}");
                    _verify.Add(line);
                }
            }
            _stage.SetEnabled(playing && shot != null && !_controller.IsStaging);

            bool can = _controller.CanRecord(out var reason);
            bool recording = _controller.IsRecording;
            _record.SetEnabled(can);
            _record.text = recording ? "Stop (Space)" : "Record (Space)";
            _record.EnableInClassList("kionjo-record--recording", recording);
            _record.tooltip = can ? (recording ? "Stop this take." : "Start a new take of this shot.") : reason;
            if (!recording) _elapsed.text = can ? "" : reason;

            _takes.itemsSource = _controller.Takes;
            _takes.RefreshItems();
            _done.SetEnabled(shot != null);
        }

        VisualElement MakeTakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("kionjo-take");
            var name = new Label { name = "take-name" };
            name.AddToClassList("kionjo-take__name");
            row.Add(name);
            var info = new Label { name = "take-info" };
            info.AddToClassList("kionjo-take__info");
            row.Add(info);
            row.Add(new Button { name = "take-select", text = "Use", tooltip = "Use this take in renders and the cut." });
            row.Add(new Button { name = "take-replay", text = "Replay", tooltip = "Play this take back in the scene (unplug the headset first)." });
            row.Add(new Button { name = "take-reveal", text = "Show", tooltip = "Show the take folder in Explorer or Finder." });
            row.Add(new Button { name = "take-delete", text = "Delete", tooltip = "Delete this take from disk." });
            return row;
        }

        void BindTakeRow(VisualElement row, int i)
        {
            if (_controller == null || i >= _controller.Takes.Count) return;
            var t = _controller.Takes[i];
            var name = row.Q<Label>("take-name");
            name.text = t.IsSelected && !t.IsRecording ? $"{t.Name}  (used)" : t.Name;
            name.EnableInClassList("kionjo-take__selected", t.IsSelected);
            row.Q<Label>("take-info").text = t.IsRecording ? "recording" : $"{t.Duration:0.0} s, {t.Events} event{(t.Events == 1 ? "" : "s")}";
            Rebind(row.Q<Button>("take-select"), () => _controller.SelectTake(t));
            var replay = row.Q<Button>("take-replay");
            Rebind(replay, () => _controller.Replay(t));
            replay.SetEnabled(EditorApplication.isPlaying && !_controller.IsRecording);
            Rebind(row.Q<Button>("take-reveal"), () => EditorUtility.RevealInFinder(t.Folder));
            Rebind(row.Q<Button>("take-delete"), () =>
            {
                if (EditorUtility.DisplayDialog("Delete take?", $"Delete {t.Name} from disk? This cannot be undone.", "Delete", "Keep"))
                    _controller.DeleteTake(t);
            });
        }

        static void Rebind(Button b, Action action)
        {
            b.clickable = new Clickable(action);
        }

        void AddRecorderToMainCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                EditorUtility.DisplayDialog("Kionjo", "No camera is tagged MainCamera. Add a Kionjo Recorder to your rig by hand (Add Component > SeedeXR > Kionjo Recorder).", "OK");
                return;
            }
            var root = cam.transform.root.gameObject;
            var rec = Undo.AddComponent<KionjoRecorder>(root);
            rec.tracks.Add(new KionjoTrack { name = "Head", target = cam.transform });
            EditorUtility.SetDirty(rec);
            Selection.activeGameObject = root;
            Refresh();
        }

        public void Dispose() => _controller?.StopReplay();
    }
}
