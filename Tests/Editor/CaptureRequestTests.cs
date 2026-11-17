using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SeedeXR.Kionjo.Tests
{
    public class CaptureRequestTests
    {
        string _dir, _footage, _lock;
        Func<(double, double)?> _free;
        Func<string> _footageRoot, _lockFolder;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name, "C (D)");
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(_dir);
            _footage = Path.Combine(_dir, "footage");
            _lock = Path.Combine(_dir, "lock");
            (_free, _footageRoot, _lockFolder) = (KionjoCapture.FreeMemory, KionjoCapture.FootageRoot, KionjoCapture.LockFolder);
            KionjoCapture.FreeMemory = () => (100.0, 100.0);
            KionjoCapture.FootageRoot = () => _footage;
            KionjoCapture.LockFolder = () => _lock;
        }

        [TearDown]
        public void TearDown()
        {
            (KionjoCapture.FreeMemory, KionjoCapture.FootageRoot, KionjoCapture.LockFolder) = (_free, _footageRoot, _lockFolder);
            var top = Path.GetDirectoryName(_dir);
            if (Directory.Exists(top)) Directory.Delete(top, true);
        }

        JournalEntry Entry(string title, int day)
        {
            var e = KionjoJournal.Create(Path.Combine(_dir, "journal"), title, "Game", new DateTime(2026, 11, day));
            e.feature = "hero";
            KionjoJournal.Save(e);
            return e;
        }

        static KionjoCaptureRequest Request() => new() { scene = "", rigs = new List<string> { "main", "close" }, seconds = 1, label = "v3" };

        [Test]
        public void ParsesRequestFile()
        {
            var r = KionjoCapture.Parse("{\"entry\":\"D:/j/2026-11-17-hero\",\"scene\":\"Assets/S.unity\",\"seconds\":5,\"fps\":24,\"subject\":\"Root/Hero\",\"rigs\":[\"main\",\"close\"],\"stills\":1,\"label\":\"v3\"}", out var entry);
            Assert.AreEqual("D:/j/2026-11-17-hero", entry);
            Assert.AreEqual(("Assets/S.unity", 5f, 24f, "Root/Hero", 1f, "v3"), (r.scene, r.seconds, r.fps, r.subject, r.stills, r.label));
            CollectionAssert.AreEqual(new[] { "main", "close" }, r.rigs);
            Assert.AreEqual("pending", r.state);
        }

        [Test]
        public void DroppedWhenSuperseded()
        {
            var old = Entry("Hero one", 16);
            Entry("Hero two", 17);
            var scene = SceneManager.GetActiveScene().path;
            var r = Request();
            r.scene = "Assets/DoesNotExist.unity";
            KionjoCaptureRequest result = null;
            KionjoCapture.Run(old.Folder, r, x => result = x);
            Assert.AreEqual("dropped", r.state);
            Assert.AreSame(r, result);
            Assert.AreEqual(scene, SceneManager.GetActiveScene().path, "no scene was opened");
            var back = KionjoJournal.Load(old.Folder, out _);
            Assert.AreEqual("dropped", back.captures[0].state);
        }

        /// <summary>A feature or label with : / * must not break the file name after the lock is taken.</summary>
        [Test]
        public void BaseNameIsSafeForFiles()
        {
            var baseName = typeof(KionjoCapture).GetMethod("BaseName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(baseName, "no safe base name");
            var e = new JournalEntry { title = "Hero", feature = "hero:act/1*" };
            var name = (string)baseName.Invoke(null, new object[] { e, new KionjoCaptureRequest { label = "v 3: final?" } });
            Assert.AreEqual(-1, name.IndexOfAny(Path.GetInvalidFileNameChars()), name);
            Assert.AreEqual(-1, name.IndexOfAny(new[] { ':', '/', '\\', '*', '?' }), name);
        }

        /// <summary>A request file has no id: running it again continues the pending item, and the result says which.</summary>
        [Test]
        public void RetryContinuesThePendingItem()
        {
            var e = Entry("Hero", 17);
            KionjoCapture.FreeMemory = () => (7.0, 7.0);
            var file = Path.Combine(_dir, "request.json");
            File.WriteAllText(file, "{\"entry\":\"" + e.Folder.Replace("\\", "/") + "\",\"label\":\"v3\",\"seconds\":1,\"rigs\":[\"main\"]}");
            KionjoCapture.RunFile(file);
            KionjoCapture.RunFile(file);
            var back = KionjoJournal.Load(e.Folder, out _);
            Assert.AreEqual(1, back.captures.Count);
            StringAssert.Contains("\"id\": \"" + back.captures[0].id + "\"", File.ReadAllText(file + ".result.json"));
        }

        [Test]
        public void PendingWhenNoRoom()
        {
            var e = Entry("Hero", 17);
            KionjoCapture.FreeMemory = () => (7.0, 7.0);
            var r = Request();
            KionjoCapture.Run(e.Folder, r, null);
            Assert.AreEqual("pending", r.state);
            StringAssert.Contains("GB", r.reason);
            Assert.AreEqual("pending", KionjoJournal.Load(e.Folder, out _).captures[0].state);
            Assert.IsFalse(Directory.Exists(_footage));
        }

        [Test]
        public void FailsCleanlyWithoutSubject()
        {
            var e = Entry("Hero", 17);
            // Single, not Additive: the headless runner starts on an unsaved untitled scene, which refuses additive scenes.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            {
                var r = Request();
                KionjoCapture.Run(e.Folder, r, null);
                Assert.AreEqual("failed", r.state);
                StringAssert.Contains("camera", r.reason);
                Assert.IsFalse(Directory.Exists(_footage) && Directory.GetFileSystemEntries(_footage).Length > 0, "nothing written");
                Assert.IsFalse(File.Exists(Path.Combine(_lock, HeavyLock.FileName)), "lock released");
                Assert.AreEqual("failed", KionjoJournal.Load(e.Folder, out _).captures[0].state);

                new GameObject("Camera").AddComponent<Camera>();
                var r2 = Request();
                r2.subject = "Nothing/Here";
                KionjoCapture.Run(e.Folder, r2, null);
                Assert.AreEqual("failed", r2.state);
                StringAssert.Contains("Nothing/Here", r2.reason);
                Assert.IsFalse(File.Exists(Path.Combine(_lock, HeavyLock.FileName)), "lock released");
            }
        }
    }
}
