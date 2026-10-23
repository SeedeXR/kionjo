using System;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Fields of the selected shot, bound through SerializedObject (every edit is undoable), grouped
    /// by what they do, with drop zones that turn scene objects into hide or show paths, a thumbnail
    /// preview, and the Capture thumbnail and Render buttons.
    /// </summary>
    public class ShotInspector : VisualElement
    {
        readonly KionjoWindow _window;
        readonly SerializedObject _so;
        readonly SerializedProperty _shot;
        readonly KionjoShot _target;
        Button _render;
        Image _preview;
        Label _message;
        public event Action Changed;

        public ShotInspector(KionjoWindow window, SerializedObject so, int index)
        {
            _window = window;
            _so = so;
            _shot = so.FindProperty("shots").GetArrayElementAtIndex(index);
            _target = window.Storyboard.shots[index];
            AddToClassList("kionjo-inspector");
            Build();
            this.Bind(so);
            this.TrackSerializedObjectValue(so, _ => Changed?.Invoke());
            // The layout depends on these: rebuild when they change.
            this.TrackPropertyValue(_shot.FindPropertyRelative("type"), _ => _window.Rebuild());
            this.TrackPropertyValue(_shot.FindPropertyRelative("outputMode"), _ => _window.Rebuild());
            this.TrackPropertyValue(_shot.FindPropertyRelative("cardKind"), _ => _window.Rebuild());
            schedule.Execute(UpdateButtons).Every(500);
        }

        void Section(string title) => Add(KionjoHelp.Section(title));

        void Field(string relative)
        {
            var p = _shot.FindPropertyRelative(relative);
            if (p != null) Add(new PropertyField(p) { name = $"field-{relative}" });
        }

        void Build()
        {
            Section("Shot");
            Field("code");
            Field("name");
            Field("type");
            Field("beat");
            Field("location");

            if (_target.IsCard)
            {
                Section("Card");
                Field("cardKind");
                Field("targetSeconds");
                if (_target.cardKind == KionjoCardKind.Text) Field("cardLines");
                if (_target.cardKind == KionjoCardKind.Image)
                {
                    Field("cardImagePath");
                    Field("cardPushIn");
                }
                Field("fadeIn");
                Field("fadeOut");
            }
            else
            {
                Section("Source");
                Field("outputMode");
                if (_target.outputMode == KionjoOutputMode.DeviceCapture)
                {
                    Field("deviceCapturePath");
                    Add(new Button(ImportCapture)
                    {
                        name = "import-capture",
                        text = "Import capture",
                        tooltip = "Copy a headset or device video into this shot's captures folder and use it. Set In Point and Out Point to trim it.",
                    });
                }
                if (_target.type == KionjoShotType.Performance && _target.outputMode == KionjoOutputMode.OfflineRender) Field("selectedTake");

                Section("Timing");
                Field("targetSeconds");
                Field("inPoint");
                Field("outPoint");
                Field("fadeIn");
                Field("fadeOut");
                Field("horizontalFov");
                Field("crop");

                Section("Staging");
                Field("stageCommand");
                Field("hidePaths");
                Add(DropZone("Drop scene objects here to hide them for this shot", "hidePaths"));
                Field("showPaths");
                Add(DropZone("Drop scene objects here to show them for this shot", "showPaths"));
            }

            Section("Voice and subtitles");
            Field("voice");
            if (!_target.IsCard)
            {
                Section("Sound effects");
                Field("sfx");
            }

            Section("Notes");
            Field("notes");

            if (_target.IsCard)
            {
                UpdateButtons();
                return; // cards are drawn by the cut: no camera picture, no render
            }
            Section("Picture");
            _preview = new Image
            {
                name = "picture-preview",
                scaleMode = ScaleMode.ScaleToFit,
                image = KionjoThumbnails.Get(_target.thumbnailPath),
                tooltip = "The shot's picture, on its card and in the animatic. Drop a PNG or JPG here to use it instead.",
            };
            _preview.AddToClassList("kionjo-thumb-preview");
            RegisterImageDrop(_preview);
            Add(_preview);
            var row = new VisualElement();
            row.AddToClassList("kionjo-row");
            row.Add(new Button(() => Picture(KionjoShotPicture.TryFromSceneView)) { name = "capture-scene-view", text = "From Scene view", tooltip = "Take the picture from the Scene view as it is framed now. New shots do this on their own." });
            row.Add(new Button(() => Picture(KionjoShotPicture.TryFromCamera)) { name = "capture-thumbnail", text = "From camera", tooltip = "Take the picture from the game camera (Kionjo Camera Source, else the main camera)." });
            if (_target.type is KionjoShotType.Performance or KionjoShotType.LiveCamera && _target.outputMode == KionjoOutputMode.OfflineRender)
            {
                _render = new Button(Render) { name = "render-take", text = "Render selected take" };
                row.Add(_render);
            }
            Add(row);
            _message = new Label { name = "inspector-message", style = { whiteSpace = WhiteSpace.Normal, marginTop = 4 } };
            _message.AddToClassList("kionjo-verify-line--info");
            Add(_message);
            UpdateButtons();
        }

        VisualElement DropZone(string text, string listProperty)
        {
            var zone = new Label(text) { tooltip = "Objects are stored as hierarchy paths and checked against the live scene when the shot is staged." };
            zone.AddToClassList("kionjo-drop");
            zone.RegisterCallback<DragUpdatedEvent>(_ =>
            {
                if (SceneObjects().Length > 0)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                    zone.AddToClassList("kionjo-drop--hover");
                }
            });
            zone.RegisterCallback<DragLeaveEvent>(_ => zone.RemoveFromClassList("kionjo-drop--hover"));
            zone.RegisterCallback<DragPerformEvent>(_ =>
            {
                var objects = SceneObjects();
                if (objects.Length == 0) return;
                DragAndDrop.AcceptDrag();
                zone.RemoveFromClassList("kionjo-drop--hover");
                _so.Update();
                var list = _shot.FindPropertyRelative(listProperty);
                foreach (var go in objects)
                {
                    var path = KionjoStaging.PathOf(go.transform);
                    bool exists = false;
                    for (int i = 0; i < list.arraySize; i++)
                        if (list.GetArrayElementAtIndex(i).stringValue == path) exists = true;
                    if (exists) continue;
                    list.InsertArrayElementAtIndex(list.arraySize);
                    list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = path;
                }
                _so.ApplyModifiedProperties();
            });
            return zone;
        }

        static GameObject[] SceneObjects()
        {
            var result = new System.Collections.Generic.List<GameObject>();
            foreach (var o in DragAndDrop.objectReferences)
                if (o is GameObject go && go.scene.IsValid()) result.Add(go);
            return result.ToArray();
        }

        delegate bool PictureSource(KionjoStoryboard sb, KionjoShot shot, out string message);

        void Picture(PictureSource source)
        {
            source(_window.Storyboard, _target, out var message);
            _so.Update();
            _preview.image = KionjoThumbnails.Get(_target.thumbnailPath);
            _message.text = message;
            Changed?.Invoke();
        }

        void RegisterImageDrop(VisualElement zone)
        {
            zone.RegisterCallback<DragUpdatedEvent>(_ =>
            {
                if (DroppedImage() != null) DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            });
            zone.RegisterCallback<DragPerformEvent>(_ =>
            {
                var file = DroppedImage();
                if (file == null) return;
                DragAndDrop.AcceptDrag();
                Picture((KionjoStoryboard sb, KionjoShot shot, out string message) => KionjoShotPicture.TryFromFile(sb, shot, file, out message));
            });
        }

        static string DroppedImage()
        {
            foreach (var p in DragAndDrop.paths)
                if (KionjoShotPicture.IsImage(p)) return p;
            return null;
        }

        void ImportCapture()
        {
            var picked = EditorUtility.OpenFilePanel("Import capture", "", "mp4,mov,mkv");
            if (string.IsNullOrEmpty(picked)) return;
            var target = CaptureImporter.Import(_window.Storyboard, _target, picked);
            CaptureImporter.WriteIndex(_window.Storyboard, new FfprobeMediaProbe());
            _window.Rebuild();
            Debug.Log($"Kionjo: imported {Path.GetFileName(picked)} as {target}");
        }

        void Render()
        {
            var sb = _window.Storyboard;
            KionjoJobs.Enqueue(new RenderJob(sb, _target, sb.renderPreset));
            _message.text = $"Rendering {_target.code} at {sb.renderPreset}. Progress is in the status bar.";
        }

        void UpdateButtons()
        {
            if (_render == null || _window.Storyboard == null) return;
            var reason = RenderJob.CannotRender(_window.Storyboard, _target);
            _render.SetEnabled(reason == null && !KionjoJobs.IsBusy);
            _render.tooltip = reason ?? (KionjoJobs.IsBusy ? "A job is running." : $"Replay the selected take and render it at {_window.Storyboard.renderPreset}.");
            var tex = KionjoThumbnails.Get(_target.thumbnailPath);
            if (_preview != null && _preview.image != tex) _preview.image = tex;
        }
    }
}
