using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;

namespace SeedeXR.Kionjo.Tests
{
    public class PostFolderTests
    {
        string _dir, _footage, _day, _posts;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name, "X (Y)");
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            _footage = Path.Combine(_dir, "footage");
            _day = FootageNotes.DayFolder(_footage, new DateTime(2026, 11, 17));
            _posts = Path.Combine(_dir, "posts");
            Directory.CreateDirectory(_day);
        }

        [TearDown]
        public void TearDown()
        {
            var top = Path.GetDirectoryName(_dir);
            if (Directory.Exists(top)) Directory.Delete(top, true);
        }

        string Footage(string name)
        {
            var path = Path.Combine(_day, name);
            File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
            return path;
        }

        JournalEntry Entry(params JournalMedia[] media)
        {
            var e = KionjoJournal.Create(Path.Combine(_dir, "journal"), "Hero rises!", "Game", new DateTime(2026, 11, 17));
            e.media.AddRange(media);
            KionjoJournal.Save(e);
            return e;
        }

        [Test]
        public void EntryFolderUsesDateAndSlug()
        {
            Assert.AreEqual(Path.Combine(_posts, "2026-11-17_hero-rises"), PostFolder.EntryFolder(_posts, Entry()));
        }

        [Test]
        public void NextCountsUp()
        {
            var entry = PostFolder.EntryFolder(_posts, Entry());
            Assert.IsNull(PostFolder.Latest(entry));
            Assert.AreEqual(Path.Combine(entry, "v01"), PostFolder.Next(entry));
            Directory.CreateDirectory(Path.Combine(entry, "v01"));
            Assert.AreEqual(Path.Combine(entry, "v01"), PostFolder.Latest(entry));
            Assert.AreEqual(Path.Combine(entry, "v02"), PostFolder.Next(entry));
        }

        [Test]
        public void RefreshLinksMaterialsAndWritesBrief()
        {
            var wide = Footage("hero-wide.mp4");
            var side = Footage("hero side [x].mp4");
            FootageNotes.Append(_day, "hero side [x].mp4", "side from notes");
            var e = Entry(new JournalMedia { path = wide, note = "wide note" }, new JournalMedia { path = side });
            var v = PostFolder.Next(PostFolder.EntryFolder(_posts, e));
            PostFolder.Refresh(v, e, _footage);
            Assert.IsTrue(File.Exists(Path.Combine(v, "materials", "hero-wide.mp4")));
            Assert.IsTrue(File.Exists(Path.Combine(v, "materials", "hero side [x].mp4")));
            var brief = File.ReadAllText(Path.Combine(v, "BRIEF.md"));
            StringAssert.Contains("- `hero-wide.mp4`: wide note", brief);
            StringAssert.Contains("- `hero side [x].mp4`: side from notes", brief);
        }

        [Test]
        public void RefreshListsMissingMedia()
        {
            var e = Entry(new JournalMedia { path = Path.Combine(_day, "gone.mp4") });
            var v = PostFolder.Next(PostFolder.EntryFolder(_posts, e));
            PostFolder.Refresh(v, e, _footage);
            var brief = File.ReadAllText(Path.Combine(v, "BRIEF.md"));
            StringAssert.Contains("## Missing\n", brief);
            StringAssert.Contains("gone.mp4", brief);
            Assert.IsFalse(File.Exists(Path.Combine(v, "materials", "gone.mp4")));
        }

        [Test]
        public void RefreshRemovesDroppedMaterials()
        {
            var a = Footage("a.mp4");
            var b = Footage("b.mp4");
            var e = Entry(new JournalMedia { path = a }, new JournalMedia { path = b });
            var v = PostFolder.Next(PostFolder.EntryFolder(_posts, e));
            PostFolder.Refresh(v, e, _footage);
            e.media.RemoveAt(1);
            PostFolder.Refresh(v, e, _footage);
            Assert.IsTrue(File.Exists(Path.Combine(v, "materials", "a.mp4")));
            Assert.IsFalse(File.Exists(Path.Combine(v, "materials", "b.mp4")));
            Assert.IsTrue(File.Exists(b), "the footage itself stays");
        }

        [Test]
        public void PruneDeletesOnlyOlderVideos()
        {
            var a = Footage("a.mp4");
            var e = Entry(new JournalMedia { path = a });
            var entry = PostFolder.EntryFolder(_posts, e);
            var v1 = PostFolder.Next(entry);
            PostFolder.Refresh(v1, e, _footage);
            Directory.CreateDirectory(Path.Combine(v1, "post"));
            File.WriteAllBytes(Path.Combine(v1, "post", "x.mp4"), new byte[100]);
            File.WriteAllBytes(Path.Combine(v1, "post", "x.png"), new byte[10]);
            var v2 = PostFolder.Next(entry);
            PostFolder.Refresh(v2, e, _footage);
            File.WriteAllBytes(Path.Combine(v2, "y.mp4"), new byte[5]);
            Assert.AreEqual(100, PostFolder.PruneOlderVideos(entry, v2));
            Assert.IsFalse(File.Exists(Path.Combine(v1, "post", "x.mp4")));
            Assert.IsFalse(File.Exists(Path.Combine(v1, "materials", "a.mp4")));
            Assert.IsTrue(File.Exists(Path.Combine(v1, "post", "x.png")));
            Assert.IsTrue(File.Exists(Path.Combine(v1, "BRIEF.md")));
            Assert.IsTrue(File.Exists(Path.Combine(v2, "y.mp4")));
            Assert.IsTrue(File.Exists(Path.Combine(v2, "materials", "a.mp4")));
            Assert.IsTrue(File.Exists(a), "pruning a linked material keeps the footage");
        }
    }
}
