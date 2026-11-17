using System;
using System.IO;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;

namespace SeedeXR.Kionjo.Tests
{
    public class FootageNotesTests
    {
        string _root, _day;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name, "FOOTAGE (X)");
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
            _day = FootageNotes.DayFolder(_root, new DateTime(2026, 11, 17));
        }

        [TearDown]
        public void TearDown()
        {
            var top = Path.GetDirectoryName(_root);
            if (Directory.Exists(top)) Directory.Delete(top, true);
        }

        [Test]
        public void AppendCreatesHeadingAndLine()
        {
            Assert.AreEqual(Path.Combine(_root, "2026-11-17"), _day);
            FootageNotes.Append(_day, "hero-wide.mp4", "wide angle of v3");
            FootageNotes.Append(_day, "hero-side.mp4", "side angle");
            var lines = File.ReadAllLines(Path.Combine(_day, "notes.md"));
            Assert.AreEqual("# 2026-11-17", lines[0]);
            CollectionAssert.Contains(lines, "- hero-wide.mp4: wide angle of v3");
            CollectionAssert.Contains(lines, "- hero-side.mp4: side angle");
        }

        [Test]
        public void AppendFlattensNewlines()
        {
            FootageNotes.Append(_day, "a.mp4", "one\ntwo\r\nthree");
            StringAssert.Contains("- a.mp4: one two three\n", File.ReadAllText(Path.Combine(_day, "notes.md")));
        }

        [Test]
        public void LineForFindsExactName()
        {
            FootageNotes.Append(_day, "other.mp4", "not this");
            FootageNotes.Append(_day, "hero [v3] wide.mp4", "the wide one: with a colon");
            Assert.AreEqual("the wide one: with a colon", FootageNotes.LineFor(_day, "hero [v3] wide.mp4"));
        }

        [Test]
        public void LineForFindsGroupedLine()
        {
            Directory.CreateDirectory(_day);
            File.WriteAllText(Path.Combine(_day, "notes.md"), "# 2026-11-17\n\n- hero-film-clean/lingering.mp4: first films\n");
            Assert.AreEqual("first films", FootageNotes.LineFor(_day, "hero-film-clean.mp4"));
        }

        [Test]
        public void LineForReturnsNullWhenAbsent()
        {
            Assert.IsNull(FootageNotes.LineFor(_day, "x.mp4"));
            FootageNotes.Append(_day, "a.mp4", "a");
            Assert.IsNull(FootageNotes.LineFor(_day, "x.mp4"));
        }
    }
}
