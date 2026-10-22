using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;
using UnityEditor;
using UnityEngine;

namespace SeedeXR.Kionjo.Tests
{
    public class StoryboardTests
    {
        KionjoStoryboard _sb;

        [SetUp]
        public void SetUp() => _sb = ScriptableObject.CreateInstance<KionjoStoryboard>();

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            Object.DestroyImmediate(_sb);
        }

        [Test]
        public void AddShot_AssignsIdAndCode()
        {
            var a = _sb.AddShot();
            var b = _sb.AddShot();
            var c = _sb.AddShot(KionjoShotType.Card);
            Assert.AreEqual("S01", a.code);
            Assert.AreEqual("S02", b.code);
            Assert.AreEqual("C01", c.code);
            Assert.AreEqual(32, a.id.Length);
            Assert.AreNotEqual(a.id, b.id);
        }

        [Test]
        public void Duplicate_NewIdSameName()
        {
            var a = _sb.AddShot();
            a.name = "Crane";
            a.voice.Add(new KionjoVoiceLine { subtitle = "Hello" });
            var copy = _sb.Duplicate(0);
            Assert.AreEqual(2, _sb.shots.Count);
            Assert.AreSame(copy, _sb.shots[1]);
            Assert.AreEqual("Crane", copy.name);
            Assert.AreEqual("S01b", copy.code);
            Assert.AreNotEqual(a.id, copy.id);
            copy.voice[0].subtitle = "Changed";
            Assert.AreEqual("Hello", a.voice[0].subtitle); // deep copy
        }

        [Test]
        public void Move_ReordersWithoutLoss()
        {
            var a = _sb.AddShot();
            var b = _sb.AddShot();
            var c = _sb.AddShot();
            _sb.Move(0, 2);
            CollectionAssert.AreEqual(new[] { b, c, a }, _sb.shots);
        }

        [Test]
        public void Undo_RestoresCount()
        {
            _sb.AddShot();
            Undo.IncrementCurrentGroup();
            _sb.AddShot();
            Undo.PerformUndo();
            Assert.AreEqual(1, _sb.shots.Count);
        }

        [Test]
        public void PlannedSeconds_UsesRangeWhenSet()
        {
            var a = _sb.AddShot();
            a.targetSeconds = 4f;
            Assert.AreEqual(4f, a.PlannedSeconds());
            a.inPoint = 6.5f;
            a.outPoint = 10f;
            Assert.AreEqual(3.5f, a.PlannedSeconds(), 1e-5);
            _sb.AddShot(KionjoShotType.Card); // 3 s
            Assert.AreEqual(6.5f, _sb.PlannedSeconds(), 1e-5);
        }

        [Test]
        public void Presets_Preview_Is1280x720x30()
        {
            var p = KionjoPresets.Get(KionjoRenderPreset.Preview720p30);
            Assert.AreEqual(1280, p.Width);
            Assert.AreEqual(720, p.Height);
            Assert.AreEqual(30, p.Fps);
            Assert.AreEqual(3840, KionjoPresets.Get(KionjoCutPreset.UHD2160p60).Width);
        }
    }
}
