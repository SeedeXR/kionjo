using System.Linq;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeedeXR.Kionjo.Tests
{
    public class PostBuildViewTests
    {
        KionjoBrandKit _kit;

        [SetUp]
        public void SetUp() => _kit = ScriptableObject.CreateInstance<KionjoBrandKit>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_kit);

        // An entry without a folder: nothing is saved to disk, media are not probed (no items).
        static JournalEntry Entry() => new() { title = "Ghost", achieved = "A ghost refuse to turn round." };

        [Test]
        public void HookButtons_FillFromTemplates()
        {
            var e = Entry();
            var view = new PostBuildView();
            view.Bind(e, _kit);
            Assert.IsNotNull(view.Q<Button>("hook-question"));
            view.FillHook(HookKind.Question);
            Assert.AreEqual(HookTemplates.Fill(HookKind.Question, e), view.Q<TextField>("post-hook").value);
            Assert.AreEqual("Can a ghost refuse to turn round?", view.Post.hook);
        }

        [Test]
        public void Warnings_RecomputeOnEdit()
        {
            var view = new PostBuildView();
            view.Bind(Entry(), _kit);
            view.SetHook("one two three four five six seven eight nine");
            var shown = view.Q("post-warnings").Children().OfType<Label>().Select(l => l.text).ToList();
            CollectionAssert.Contains(shown, "Hook has 9 words: keep it to 7 or fewer.");
        }

        [Test]
        public void BuildButton_DisabledWhileJobRuns()
        {
            var e = Entry();
            e.media.Add(new JournalMedia { path = "D:/nope/a.mp4" });
            var view = new PostBuildView();
            bool busy = true;
            view.IsBusy = () => busy;
            view.Bind(e, _kit);
            var build = view.Q<Button>("post-build");
            Assert.IsFalse(build.enabledSelf);
            busy = false;
            view.RefreshBuildState();
            Assert.IsTrue(build.enabledSelf);
        }
    }
}
