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
        public void Timeline_TotalLabel_FormatsSeconds() => Assert.AreEqual("81.5 s / target 60 s", TimelineBar.FormatTotal(81.5f, 60f));
    }
}
