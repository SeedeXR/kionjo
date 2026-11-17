using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;

namespace SeedeXR.Kionjo.Tests
{
    public class ProgressIntakeTests
    {
        string _dir, _root;
        DateTime _now = new(2026, 11, 17, 12, 0, 0);
        readonly Dictionary<string, long> _sizes = new();

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name, "A (B)");
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            _root = Path.Combine(_dir, "footage");
            Directory.CreateDirectory(Path.Combine(_root, "2026-11-17"));
        }

        [TearDown]
        public void TearDown()
        {
            var top = Path.GetDirectoryName(_dir);
            if (Directory.Exists(top)) Directory.Delete(top, true);
        }

        ProgressIntake Intake() => new(() => _now, p => _sizes.TryGetValue(p, out var s) ? s : 0);

        JournalEntry Entry(string title, string feature, int day = 17)
        {
            var e = KionjoJournal.Create(Path.Combine(_dir, "journal"), title, "Game", new DateTime(2026, 11, day));
            e.feature = feature;
            KionjoJournal.Save(e);
            return e;
        }

        [Test]
        public void WaitsForStableSize()
        {
            var intake = Intake();
            _sizes["f.mp4"] = 10;
            intake.Seen("f.mp4");
            CollectionAssert.IsEmpty(intake.Ready());
            _now = _now.AddSeconds(1);
            _sizes["f.mp4"] = 20;
            CollectionAssert.IsEmpty(intake.Ready());
            _now = _now.AddSeconds(1.9);
            CollectionAssert.IsEmpty(intake.Ready());
            _now = _now.AddSeconds(0.1);
            CollectionAssert.AreEqual(new[] { "f.mp4" }, intake.Ready());
        }

        [Test]
        public void ReadyReturnsOnce()
        {
            var intake = Intake();
            _sizes["f.mp4"] = 10;
            intake.Seen("f.mp4");
            intake.Seen("f.mp4");
            _now = _now.AddSeconds(3);
            Assert.AreEqual(1, intake.Ready().Count);
            Assert.AreEqual(0, intake.Ready().Count);
        }

        /// <summary>Kionjo's own in-flight files are renamed when done; a stalled one must never be attached.</summary>
        [Test]
        public void InFlightFilesAreIgnored()
        {
            var intake = Intake();
            _sizes["hero-v1-main.partial.mp4"] = 10;
            _sizes["hero-v1-main.sound.mp4"] = 10;
            intake.Seen("hero-v1-main.partial.mp4");
            intake.Seen("hero-v1-main.sound.mp4");
            _now = _now.AddSeconds(10);
            CollectionAssert.IsEmpty(intake.Ready());
            _now = _now.AddSeconds(10);
            CollectionAssert.IsEmpty(intake.Ready());
        }

        [Test]
        public void EmptyFileIsNotReady()
        {
            var intake = Intake();
            _sizes["f.mp4"] = 0;
            intake.Seen("f.mp4");
            _now = _now.AddSeconds(5);
            Assert.AreEqual(0, intake.Ready().Count);
        }

        [Test]
        public void MatchPrefersFeaturePrefix()
        {
            var hero = Entry("Hero", "hero");
            var door = Entry("Door", "door");
            var file = Path.Combine(_root, "2026-11-17", "door-wide.mp4");
            Assert.AreSame(door, ProgressIntake.MatchEntry(new List<JournalEntry> { hero, door }, file, _root));
        }

        [Test]
        public void MatchSingleCandidate()
        {
            var hero = Entry("Hero", "hero");
            var old = Entry("Old", "hero", 16);
            var file = Path.Combine(_root, "2026-11-17", "clip.mp4");
            Assert.AreSame(hero, ProgressIntake.MatchEntry(new List<JournalEntry> { hero, old }, file, _root));
            var other = Entry("Door", "door");
            Assert.IsNull(ProgressIntake.MatchEntry(new List<JournalEntry> { hero, other }, file, _root), "two candidates, no prefix");
        }

        [Test]
        public void MatchNoneOutsideDayFolder()
        {
            var all = new List<JournalEntry> { Entry("Hero", "hero") };
            Assert.IsNull(ProgressIntake.MatchEntry(all, Path.Combine(_root, "misc", "hero-a.mp4"), _root));
            Assert.IsNull(ProgressIntake.MatchEntry(all, Path.Combine(_dir, "elsewhere", "2026-11-17", "hero-a.mp4"), _root));
        }

        [Test]
        public void AttachAppendsWithNote()
        {
            var e = Entry("Hero", "hero");
            var day = Path.Combine(_root, "2026-11-17");
            FootageNotes.Append(day, "hero [x] wide.mp4", "wide one");
            var file = Path.Combine(day, "hero [x] wide.mp4");
            var still = Path.Combine(day, "hero-still.png");
            Assert.IsTrue(ProgressIntake.Attach(e, file));
            Assert.IsTrue(ProgressIntake.Attach(e, still));
            var back = KionjoJournal.Load(e.Folder, out _);
            Assert.AreEqual(2, back.media.Count);
            Assert.AreEqual((file, "broll", "found in footage folder: wide one"), (back.media[0].path, back.media[0].role, back.media[0].note));
            Assert.AreEqual("still", back.media[1].role);
        }

        [Test]
        public void AttachSkipsListedFile()
        {
            var e = Entry("Hero", "hero");
            var file = Path.Combine(_root, "2026-11-17", "hero-a.mp4");
            e.media.Add(new JournalMedia { path = file, note = "agent wrote this" });
            KionjoJournal.Save(e);
            Assert.IsFalse(ProgressIntake.Attach(e, file));
            var back = KionjoJournal.Load(e.Folder, out _);
            Assert.AreEqual(1, back.media.Count);
            Assert.AreEqual("agent wrote this", back.media[0].note);
        }
    }
}
