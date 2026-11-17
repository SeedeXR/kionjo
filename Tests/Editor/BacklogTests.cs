using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;

namespace SeedeXR.Kionjo.Tests
{
    public class BacklogTests
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

        JournalEntry Entry(string title, string feature, int day, params string[] states)
        {
            var e = KionjoJournal.Create(_dir, title, "Game", new DateTime(2026, 11, day));
            e.feature = feature;
            foreach (var s in states) e.captures.Add(new KionjoCaptureRequest { label = s, state = s });
            KionjoJournal.Save(e);
            return e;
        }

        [Test]
        public void LaterStepSupersedes()
        {
            var a = Entry("Hero one", "hero", 16);
            var b = Entry("Hero two", "hero", 17);
            var all = new List<JournalEntry> { a, b };
            Assert.IsFalse(KionjoBacklog.IsCurrent(a, all));
            Assert.IsTrue(KionjoBacklog.IsCurrent(b, all));
        }

        [Test]
        public void OtherFeatureDoesNotSupersede()
        {
            var a = Entry("Hero one", "hero", 16);
            var b = Entry("Door", "door", 17);
            Assert.IsTrue(KionjoBacklog.IsCurrent(a, new List<JournalEntry> { a, b }));
        }

        [Test]
        public void NoFeatureIsAlwaysCurrent()
        {
            var a = Entry("Loose one", null, 16);
            var b = Entry("Loose two", null, 17);
            Assert.IsTrue(KionjoBacklog.IsCurrent(a, new List<JournalEntry> { a, b }));
        }

        [Test]
        public void PendingListsOnlyCurrentPending()
        {
            var old = Entry("Hero one", "hero", 16, "pending");
            var now = Entry("Hero two", "hero", 17, "pending", "taken", "failed", "dropped");
            var list = KionjoBacklog.Pending(new List<JournalEntry> { old, now });
            Assert.AreEqual(1, list.Count);
            Assert.AreSame(now, list[0].entry);
            Assert.AreEqual("pending", list[0].request.state);
        }

        [Test]
        public void DropSupersededSavesReason()
        {
            var old = Entry("Hero one", "hero", 16, "pending", "taken");
            var now = Entry("Hero two", "hero", 17, "pending");
            Assert.AreEqual(1, KionjoBacklog.DropSuperseded(new List<JournalEntry> { old, now }));
            var back = KionjoJournal.Load(old.Folder, out _);
            Assert.AreEqual("dropped", back.captures[0].state);
            StringAssert.Contains("A later step of hero exists", back.captures[0].reason);
            Assert.AreEqual("taken", back.captures[1].state);
            Assert.AreEqual("pending", KionjoJournal.Load(now.Folder, out _).captures[0].state);
        }
    }
}
