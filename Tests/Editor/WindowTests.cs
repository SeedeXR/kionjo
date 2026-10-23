using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeedeXR.Kionjo.Tests
{
    public class WindowTests
    {
        KionjoWindow _w;
        KionjoStoryboard _sb;

        [TearDown]
        public void TearDown()
        {
            if (_w != null) _w.Close();
            if (_sb != null) Object.DestroyImmediate(_sb);
            Undo.ClearAll();
        }

        static readonly string[] Ids =
        {
            "toolbar", "storyboard-field", "tab-storyboard", "tab-shoot", "tab-cut", "empty-state", "shot-list",
            "shot-inspector", "timeline-bar", "status-bar", "ffmpeg-status", "job-progress", "job-cancel",
            "shoot-body", "cut-body", "shoot-record", "shoot-takes", "cut-build", "cut-preset", "cut-log",
        };

        [Test]
        public void Window_Opens_AndHasNamedElements()
        {
            _w = KionjoWindow.Open();
            foreach (var id in Ids) Assert.IsNotNull(_w.rootVisualElement.Q(id), id);
            Assert.AreEqual("Kionjo", _w.titleContent.text);
        }

        [Test]
        public void Window_NoStoryboard_ShowsEmptyState()
        {
            _w = KionjoWindow.Open();
            _w.Storyboard = null;
            Assert.IsFalse(_w.rootVisualElement.Q("empty-state").ClassListContains("kionjo-hidden"));
            Assert.IsTrue(_w.rootVisualElement.Q("storyboard-body").ClassListContains("kionjo-hidden"));
        }

        [Test]
        public void Window_WithStoryboard_ListsShots()
        {
            _sb = ScriptableObject.CreateInstance<KionjoStoryboard>();
            _sb.AddShot();
            _sb.AddShot(KionjoShotType.Card);
            _w = KionjoWindow.Open();
            _w.Storyboard = _sb;
            Assert.AreEqual(2, _w.rootVisualElement.Q<ListView>("shot-list").itemsSource.Count);
            Assert.IsTrue(_w.rootVisualElement.Q("empty-state").ClassListContains("kionjo-hidden"));
            Assert.AreEqual(0, _w.SelectedIndex);
            Assert.IsNotNull(_w.rootVisualElement.Q("field-name"), "the inspector shows the selected shot");
        }

        [Test]
        public void Window_AddShot_InsertsAfterSelectionAndUndoes()
        {
            _sb = ScriptableObject.CreateInstance<KionjoStoryboard>();
            _sb.AddShot();
            _sb.AddShot();
            Undo.IncrementCurrentGroup();
            _w = KionjoWindow.Open();
            _w.Storyboard = _sb;
            _w.SelectedIndex = 0;
            _w.AddShot(KionjoShotType.Card);
            Assert.AreEqual(3, _sb.shots.Count);
            Assert.AreEqual(KionjoShotType.Card, _sb.shots[1].type);
            Assert.AreEqual(1, _w.SelectedIndex);
            Undo.PerformUndo();
            Assert.AreEqual(2, _sb.shots.Count);
        }

        [Test]
        public void Window_Tabs_Switch()
        {
            _sb = ScriptableObject.CreateInstance<KionjoStoryboard>();
            _w = KionjoWindow.Open();
            _w.Storyboard = _sb;
            _w.CurrentTab = KionjoWindow.Tab.Cut;
            Assert.IsFalse(_w.rootVisualElement.Q("cut-body").ClassListContains("kionjo-hidden"));
            Assert.IsTrue(_w.rootVisualElement.Q("storyboard-body").ClassListContains("kionjo-hidden"));
            _w.CurrentTab = KionjoWindow.Tab.Storyboard;
        }

        [Test]
        public void Inspector_SectionsHaveHelp_AndPictureSources()
        {
            _sb = ScriptableObject.CreateInstance<KionjoStoryboard>();
            _sb.AddShot();
            _w = KionjoWindow.Open();
            _w.Storyboard = _sb;
            foreach (var id in new[] { "help-shot", "help-source", "help-timing", "help-staging", "help-voice", "help-picture", "picture-preview", "capture-scene-view", "capture-thumbnail" })
                Assert.IsNotNull(_w.rootVisualElement.Q(id), id);
            StringAssert.EndsWith("reference.html#timing", KionjoHelp.Reference(KionjoHelp.SectionAnchors["Timing"]));
            StringAssert.EndsWith("reference.html#cut-tab", KionjoHelp.TabUrl(KionjoWindow.Tab.Cut));
        }

        [Test]
        public void Tour_WalksTheSteps_SwitchesTabs_AndCloses()
        {
            bool seen = EditorPrefs.GetBool(KionjoTour.SeenKey, false);
            try
            {
                _sb = ScriptableObject.CreateInstance<KionjoStoryboard>();
                _sb.AddShot();
                _w = KionjoWindow.Open();
                _w.Storyboard = _sb;
                var tour = _w.ShowTour();
                Assert.AreEqual("Step 1 of " + KionjoTour.Steps.Length, _w.rootVisualElement.Q<Label>("tour-count").text);
                tour.Go(4);
                Assert.AreEqual(KionjoWindow.Tab.Shoot, _w.CurrentTab);
                tour.Go(KionjoTour.Steps.Length);
                Assert.IsNull(_w.rootVisualElement.Q<KionjoTour>(), "Finish closes the tour");
            }
            finally
            {
                EditorPrefs.SetBool(KionjoTour.SeenKey, seen);
            }
        }

        [Test]
        public void Picture_FromFile_IsStoredAsTheShotsPng()
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name);
            if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
            System.IO.Directory.CreateDirectory(dir);
            var src = System.IO.Path.Combine(dir, "frame.jpg");
            var tex = new Texture2D(8, 4);
            System.IO.File.WriteAllBytes(src, tex.EncodeToJPG());
            Object.DestroyImmediate(tex);
            _sb = ScriptableObject.CreateInstance<KionjoStoryboard>();
            _sb.outputFolder = dir;
            var shot = _sb.AddShot();

            Assert.IsTrue(KionjoShotPicture.TryFromFile(_sb, shot, src, out var message), message);
            Assert.AreEqual(KionjoPaths.ThumbnailPath(_sb, shot), shot.thumbnailPath);
            Assert.AreEqual(8, KionjoThumbnails.Get(shot.thumbnailPath).width);
            Assert.IsFalse(KionjoShotPicture.TryFromFile(_sb, shot, System.IO.Path.Combine(dir, "notes.txt"), out _));
        }

        [Test]
        public void Timeline_TotalLabel_FormatsSeconds() => Assert.AreEqual("81.5 s / target 60 s", TimelineBar.FormatTotal(81.5f, 60f));
    }
}
