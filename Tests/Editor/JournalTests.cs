using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;

namespace SeedeXR.Kionjo.Tests
{
    public class JournalTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        static readonly DateTime Day = new(2026, 11, 8);

        [Test]
        public void Slug_Examples()
        {
            Assert.AreEqual("the-ghost-never-turns-round", KionjoJournal.Slug("The ghost never turns round!"));
            Assert.AreEqual("entry", KionjoJournal.Slug(""));
            Assert.AreEqual(48, KionjoJournal.Slug(new string('a', 60)).Length);
        }

        [Test]
        public void Create_WritesEntryJson_DatedFolder()
        {
            var e = KionjoJournal.Create(_dir, "Ghost", "Game", Day);
            Assert.AreEqual(Path.Combine(_dir, "2026-11-08-ghost"), e.Folder);
            var json = File.ReadAllText(Path.Combine(e.Folder, "entry.json"));
            StringAssert.Contains("\"status\": \"Draft\"", json);
            StringAssert.Contains("\"stage\": \"idea\"", json);
            Assert.AreEqual("2026-11-08", e.created);
            Assert.AreEqual(32, e.id.Length);
        }

        [Test]
        public void Create_SameTitleTwice_SuffixesFolder()
        {
            KionjoJournal.Create(_dir, "Ghost", "Game", Day);
            var second = KionjoJournal.Create(_dir, "Ghost", "Game", Day);
            Assert.AreEqual("2026-11-08-ghost-2", Path.GetFileName(second.Folder));
        }

        [Test]
        public void SaveLoad_RoundTrip_AllFields()
        {
            var e = KionjoJournal.Create(_dir, "Ghost", "Game", Day);
            e.story = "Magic";
            e.owner = "Kit";
            e.stage = "prototype";
            e.making = "m";
            e.reference = "r";
            e.goal = "g";
            e.achieved = "a";
            e.hurdle = "h";
            e.trick = "t";
            e.decisions = new List<string> { "Keep it grey.", "One accent." };
            e.media.Add(new JournalMedia { path = "D:/x/a.mp4", role = "hook", inPoint = 1f, outPoint = 3f, note = "n", notOwn = true });
            e.media.Add(new JournalMedia { path = "D:/x/b.mp4", role = "still" });
            e.hookOptions.Add("Can a ghost refuse to turn?");
            e.status = "Ready";
            KionjoJournal.Save(e);

            var back = KionjoJournal.Load(e.Folder, out var error);
            Assert.IsNull(error);
            Assert.AreEqual(e.id, back.id);
            Assert.AreEqual(e.created, back.created);
            Assert.AreEqual("Game", back.project);
            Assert.AreEqual("Ghost", back.title);
            Assert.AreEqual("Magic", back.story);
            Assert.AreEqual("Kit", back.owner);
            Assert.AreEqual("prototype", back.stage);
            Assert.AreEqual("m r g a h t", string.Join(" ", back.making, back.reference, back.goal, back.achieved, back.hurdle, back.trick));
            CollectionAssert.AreEqual(e.decisions, back.decisions);
            Assert.AreEqual(2, back.media.Count);
            var m = back.media[0];
            Assert.AreEqual(("D:/x/a.mp4", "hook", 1f, 3f, "n", true), (m.path, m.role, m.inPoint, m.outPoint, m.note, m.notOwn));
            Assert.AreEqual("still", back.media[1].role);
            CollectionAssert.AreEqual(e.hookOptions, back.hookOptions);
            Assert.AreEqual("Ready", back.status);
            Assert.AreEqual(e.Folder, back.Folder);
            Assert.IsFalse(File.Exists(Path.Combine(e.Folder, "entry.json.partial")));
        }

        [Test]
        public void LoadAll_NewestFirst_SkipsBrokenWithWarning()
        {
            KionjoJournal.Create(_dir, "Old", "Game", new DateTime(2026, 1, 2));
            KionjoJournal.Create(_dir, "New", "Game", new DateTime(2026, 11, 2));
            var broken = Path.Combine(_dir, "2026-05-05-broken");
            Directory.CreateDirectory(broken);
            File.WriteAllText(Path.Combine(broken, "entry.json"), "{not json");

            var warnings = new List<string>();
            var all = KionjoJournal.LoadAll(_dir, warnings);
            CollectionAssert.AreEqual(new[] { "New", "Old" }, all.Select(e => e.title).ToArray());
            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains("2026-05-05-broken", warnings[0]);
        }

        [Test]
        public void Load_MissingMedia_FlaggedNotThrown()
        {
            var e = KionjoJournal.Create(_dir, "Ghost", "Game", Day);
            e.media.Add(new JournalMedia { path = "D:/nope/x.mp4" });
            KionjoJournal.Save(e);
            var back = KionjoJournal.Load(e.Folder, out var error);
            Assert.IsNull(error);
            var missing = KionjoJournal.Missing(back).ToList();
            Assert.AreEqual(1, missing.Count);
            Assert.AreEqual("D:/nope/x.mp4", missing[0].path);
        }

        [Test]
        public void ProgressFieldsRoundTrip()
        {
            var e = KionjoJournal.Create(_dir, "Hero", "Game", Day);
            e.feature = "hero";
            e.version = "main@abc123";
            e.brief = "## Why\nBecause.";
            e.captures.Add(new KionjoCaptureRequest { scene = "Assets/S.unity", rigs = new List<string> { "main", "wide" }, label = "v3" });
            KionjoJournal.Save(e);
            var back = KionjoJournal.Load(e.Folder, out var error);
            Assert.IsNull(error);
            Assert.AreEqual(("hero", "main@abc123", "## Why\nBecause."), (back.feature, back.version, back.brief));
            Assert.AreEqual(1, back.captures.Count);
            var c = back.captures[0];
            Assert.AreEqual(("Assets/S.unity", "v3", "pending"), (c.scene, c.label, c.state));
            CollectionAssert.AreEqual(new[] { "main", "wide" }, c.rigs);
            Assert.AreEqual(KionjoCaptureRequest.States.Pending, c.state);
        }

        [Test]
        public void OldEntryLoadsWithEmptyCaptures()
        {
            var folder = Path.Combine(_dir, "2026-11-08-old");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "entry.json"),
                "{\"id\":\"x\",\"created\":\"2026-11-08\",\"title\":\"Old\",\"stage\":\"idea\",\"media\":[],\"status\":\"Draft\"}");
            var back = KionjoJournal.Load(folder, out var error);
            Assert.IsNull(error);
            Assert.IsNotNull(back.captures);
            Assert.AreEqual(0, back.captures.Count);
            Assert.IsTrue(string.IsNullOrEmpty(back.feature));
        }

        [Test]
        public void UpdateKeepsOtherWriterFields()
        {
            var a = KionjoJournal.Create(_dir, "Hero", "Game", Day);
            var other = KionjoJournal.Load(a.Folder, out _);
            other.achieved = "Agent text";
            KionjoJournal.Save(other);
            var saved = KionjoJournal.Update(a.Folder, e => e.media.Add(new JournalMedia { path = "x.mp4" }));
            Assert.IsNotNull(saved);
            var back = KionjoJournal.Load(a.Folder, out _);
            Assert.AreEqual("Agent text", back.achieved);
            Assert.AreEqual(1, back.media.Count);
            Assert.IsNull(KionjoJournal.Update(Path.Combine(_dir, "nothing-here"), e => { }));
        }

        static void Call(string method, params object[] args)
        {
            var m = typeof(KionjoJournal).GetMethod(method, new[] { typeof(JournalEntry) }) ?? typeof(KionjoJournal).GetMethod(method);
            Assert.IsNotNull(m, $"KionjoJournal.{method} is missing");
            m.Invoke(null, args);
        }

        /// <summary>The window saves its copy: media, captures and posts written since it loaded survive; its text and removals win.</summary>
        [Test]
        public void EditorSaveKeepsWhatOthersWroteSince()
        {
            var a = KionjoJournal.Create(_dir, "Hero", "Game", Day);
            KionjoJournal.Update(a.Folder, e => e.media.Add(new JournalMedia { path = "old.mp4" }));
            var window = KionjoJournal.Load(a.Folder, out _);
            KionjoJournal.Update(a.Folder, e =>
            {
                e.media.Add(new JournalMedia { path = "attached.mp4" });
                e.captures.Add(new KionjoCaptureRequest { id = "c1", state = "taken" });
                e.posts.Add(new KionjoPost { id = "p1", name = "reel" });
            });
            window.achieved = "Typed in the window";
            window.media.Clear();
            Call("SaveEdited", window);
            var back = KionjoJournal.Load(a.Folder, out _);
            Assert.AreEqual("Typed in the window", back.achieved);
            CollectionAssert.AreEqual(new[] { "attached.mp4" }, back.media.Select(m => m.path).ToList(), "removed old.mp4 stays removed, attached.mp4 is kept");
            Assert.AreEqual("c1", back.captures.Single().id);
            Assert.AreEqual("p1", back.posts.Single().id);
        }

        /// <summary>The watcher must recognise the window's own save, and only that one.</summary>
        [Test]
        public void EditorSaveIsRecognised()
        {
            var a = KionjoJournal.Create(_dir, "Hero", "Game", Day);
            var m = typeof(KionjoJournal).GetMethod("IsEditorSave");
            Assert.IsNotNull(m, "KionjoJournal.IsEditorSave is missing");
            var window = KionjoJournal.Load(a.Folder, out _);
            window.goal = "g";
            Call("SaveEdited", window);
            Assert.IsTrue((bool)m.Invoke(null, new object[] { a.Folder }));
            System.Threading.Thread.Sleep(20);
            KionjoJournal.Update(a.Folder, e => e.trick = "agent");
            Assert.IsFalse((bool)m.Invoke(null, new object[] { a.Folder }));
        }
    }
}
