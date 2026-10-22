using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;
using UnityEditor;
using UnityEngine;

namespace SeedeXR.Kionjo.Tests
{
    public class ShootTests
    {
        string _dir;
        KionjoStoryboard _sb;
        readonly List<GameObject> _objects = new();

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(_dir);
            _sb = ScriptableObject.CreateInstance<KionjoStoryboard>();
            _sb.outputFolder = _dir;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects) if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
            Object.DestroyImmediate(_sb);
            Undo.ClearAll();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        GameObject Go(string name, bool active = true)
        {
            var go = new GameObject(name);
            go.SetActive(active);
            _objects.Add(go);
            return go;
        }

        void FakeTake(KionjoShot shot, int n, float seconds)
        {
            var folder = Path.Combine(KionjoPaths.ShotTakes(_sb, shot), KionjoTakePaths.TakeFolderName(shot.code, n));
            new KionjoTakeMeta { shotCode = shot.code, takeNumber = n, durationSeconds = seconds, events = { new KionjoTakeEvent { name = "x" } } }.Save(folder);
        }

        [Test]
        public void Verify_UnresolvedHide_IsError()
        {
            var shot = new KionjoShot { hidePaths = { "/Nope" } };
            var r = StageVerifier.Verify(shot, KionjoStaging.Resolve, true, true, false, false);
            Assert.IsFalse(r.Ready);
            Assert.IsTrue(r.Lines.Any(l => l.Text == "Hide: /Nope not found"));
        }

        [Test]
        public void Verify_ResolvedButActive_IsError()
        {
            Go("KionjoVerifyPanel");
            var shot = new KionjoShot { hidePaths = { "/KionjoVerifyPanel" } };
            var r = StageVerifier.Verify(shot, KionjoStaging.Resolve, true, true, false, false);
            Assert.IsTrue(r.Lines.Any(l => l.Severity == StageSeverity.Error && l.Text.Contains("still active")));
        }

        [Test]
        public void Verify_EmptyLists_Info()
        {
            var r = StageVerifier.Verify(new KionjoShot(), _ => null, true, true, false, false);
            Assert.IsTrue(r.Ready);
            Assert.AreEqual(StageSeverity.Info, r.Lines.Single().Severity);
        }

        [Test]
        public void Verify_NoCamera_Error() =>
            Assert.IsFalse(StageVerifier.Verify(new KionjoShot(), _ => null, false, true, false, false).Ready);

        [Test]
        public void Verify_XrActiveForRender_Warning()
        {
            var r = StageVerifier.Verify(new KionjoShot(), _ => null, true, true, true, true);
            Assert.IsTrue(r.Ready);
            Assert.IsTrue(r.Lines.Any(l => l.Severity == StageSeverity.Warning && l.Text.Contains("XR device")));
        }

        [Test]
        public void Verify_AllGood_Ready()
        {
            Go("KionjoVerifyHidden", active: false);
            var shot = new KionjoShot { hidePaths = { "/KionjoVerifyHidden" } };
            Assert.IsTrue(StageVerifier.Verify(shot, KionjoStaging.Resolve, true, true, false, false).Ready);
        }

        [Test]
        public void Takes_ListedFromDisk_SortedByNumber()
        {
            var shot = _sb.AddShot();
            FakeTake(shot, 3, 4.5f);
            FakeTake(shot, 1, 2f);
            Directory.CreateDirectory(Path.Combine(KionjoPaths.ShotTakes(_sb, shot), "notes"));
            var c = new ShootController(_sb);
            c.Select(shot);
            CollectionAssert.AreEqual(new[] { 1, 3 }, c.Takes.Select(t => t.Number).ToArray());
            Assert.AreEqual(4.5f, c.Takes[1].Duration);
            Assert.IsTrue(c.Takes[1].IsSelected, "with nothing chosen, the newest take is the one used");
        }

        [Test]
        public void SelectTake_SetsShotSelectedTake()
        {
            var shot = _sb.AddShot();
            FakeTake(shot, 1, 2f);
            FakeTake(shot, 2, 2f);
            var c = new ShootController(_sb);
            c.Select(shot);
            c.SelectTake(c.Takes[0]);
            Assert.AreEqual("S01_take01", shot.selectedTake);
            Assert.IsTrue(c.Takes[0].IsSelected);
            Assert.IsFalse(c.Takes[1].IsSelected);
        }

        [Test]
        public void Done_AdvancesPastCardsAndStopsAtEnd()
        {
            var a = _sb.AddShot();
            _sb.AddShot(KionjoShotType.Card);
            var b = _sb.AddShot();
            var c = new ShootController(_sb);
            c.Select(a);
            Assert.IsTrue(c.Done());
            Assert.AreSame(b, c.Current);
            Assert.IsFalse(c.Done());
        }

        [Test]
        public void CanRecord_OutsidePlayMode_SaysWhy()
        {
            var c = new ShootController(_sb);
            c.Select(_sb.AddShot());
            Assert.IsFalse(c.CanRecord(out var reason));
            StringAssert.Contains("Play mode", reason);
        }
    }
}
