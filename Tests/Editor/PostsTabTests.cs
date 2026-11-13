using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeedeXR.Kionjo.Tests
{
    public class PostsTabTests
    {
        string _dir, _oldFolder;
        bool _hadFolder;
        KionjoWindow _w;
        KionjoBrandKit _kit;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(_dir);
            _hadFolder = EditorPrefs.HasKey(KionjoJournal.FolderKey);
            _oldFolder = KionjoJournal.Folder;
            KionjoJournal.Folder = _dir;
        }

        [TearDown]
        public void TearDown()
        {
            if (_w != null) _w.Close();
            if (_kit != null) UnityEngine.Object.DestroyImmediate(_kit);
            if (_hadFolder) KionjoJournal.Folder = _oldFolder;
            else EditorPrefs.DeleteKey(KionjoJournal.FolderKey);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void Window_PostsTab_WorksWithoutStoryboard()
        {
            _w = KionjoWindow.Open();
            _w.Storyboard = null;
            _w.CurrentTab = KionjoWindow.Tab.Posts;
            Assert.IsFalse(_w.rootVisualElement.Q("posts-body").ClassListContains("kionjo-hidden"));
            Assert.IsTrue(_w.rootVisualElement.Q("empty-state").ClassListContains("kionjo-hidden"));
            Assert.IsTrue(_w.rootVisualElement.Q("tab-posts").enabledSelf);
            _w.CurrentTab = KionjoWindow.Tab.Storyboard;
            Assert.IsTrue(_w.rootVisualElement.Q("posts-body").ClassListContains("kionjo-hidden"));
            Assert.IsFalse(_w.rootVisualElement.Q("empty-state").ClassListContains("kionjo-hidden"));
        }

        [Test]
        public void PostsTab_ListsEntries_NewestFirst()
        {
            KionjoJournal.Create(_dir, "Older", "Game", new DateTime(2026, 1, 1));
            KionjoJournal.Create(_dir, "Newer", "Game", new DateTime(2026, 6, 1));
            _w = KionjoWindow.Open();
            _w.CurrentTab = KionjoWindow.Tab.Posts;
            var tab = _w.rootVisualElement.Q<PostsTab>("posts-body");
            tab.Refresh();
            CollectionAssert.AreEqual(new[] { "Newer", "Older" }, tab.Entries.Select(e => e.title).ToArray());
            Assert.AreEqual("Newer", tab.Selected.title);
        }

        [Test]
        public void StoryBalance_CountsLastTwelve()
        {
            var entries = Enumerable.Range(0, 14).Select(i => new JournalEntry { story = i < 5 ? "Magic" : i < 6 ? "Origin" : i < 12 ? "" : "Villain" }).ToList();
            _kit = ScriptableObject.CreateInstance<KionjoBrandKit>();
            Assert.AreEqual("Magic 5 · Origin 1 · Villain 0 · Prophecy 0 · Alchemy 0", PostsTab.StoryBalance(entries, _kit.storyTypes));
        }
    }
}
