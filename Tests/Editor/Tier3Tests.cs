using System.IO;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;
using UnityEditor;
using UnityEngine;

namespace SeedeXR.Kionjo.Tests
{
    public class StoryboardJsonTests
    {
        KionjoStoryboard _a, _b;
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _a = ScriptableObject.CreateInstance<KionjoStoryboard>();
            _b = ScriptableObject.CreateInstance<KionjoStoryboard>();
            _dir = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_a);
            Object.DestroyImmediate(_b);
            Undo.ClearAll();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void Json_RoundTrip_PreservesIdsAndOrder()
        {
            _a.title = "Baraza";
            var s1 = _a.AddShot();
            s1.name = "Crane";
            s1.voice.Add(new KionjoVoiceLine { subtitle = "Why here?", at = 0.5f });
            var c = _a.AddShot(KionjoShotType.Card);
            c.cardLines.Add("You will be judged by why.");
            int n = KionjoStoryboardJson.Import(_b, KionjoStoryboardJson.Export(_a), replace: true);
            Assert.AreEqual(2, n);
            Assert.AreEqual("Baraza", _b.title);
            Assert.AreEqual(s1.id, _b.shots[0].id);
            Assert.AreEqual("Why here?", _b.shots[0].voice[0].subtitle);
            Assert.AreEqual("You will be judged by why.", _b.shots[1].cardLines[0]);
        }

        [Test]
        public void Import_MissingIds_Generated()
        {
            const string json = "{\"MonoBehaviour\":{\"shots\":[{\"code\":\"S01\",\"name\":\"From an agent\"}]}}";
            KionjoStoryboardJson.Import(_b, json, replace: false);
            Assert.AreEqual(1, _b.shots.Count);
            Assert.AreEqual("From an agent", _b.shots[0].name);
            Assert.AreEqual(32, _b.shots[0].id.Length);
        }

        [Test]
        public void Import_Append_KeepsExisting_Replace_ClearsOld()
        {
            _b.AddShot();
            _a.AddShot();
            _a.AddShot();
            KionjoStoryboardJson.Import(_b, KionjoStoryboardJson.Export(_a), replace: false);
            Assert.AreEqual(3, _b.shots.Count);
            KionjoStoryboardJson.Import(_b, KionjoStoryboardJson.Export(_a), replace: true);
            Assert.AreEqual(2, _b.shots.Count);
        }

        [Test]
        public void StateFile_ListsShotsAndTakes()
        {
            _a.outputFolder = _dir;
            var s = _a.AddShot();
            new KionjoTakeMeta { shotCode = "S01", takeNumber = 1 }.Save(Path.Combine(KionjoPaths.ShotTakes(_a, s), "S01_take01"));
            var path = KionjoStateFile.Write(_a, s.id, force: true);
            var state = JsonUtility.FromJson<KionjoStateFile.State>(File.ReadAllText(path));
            Assert.AreEqual(1, state.shots.Count);
            Assert.AreEqual(1, state.shots[0].takes);
            Assert.AreEqual("1 take", state.shots[0].state);
            Assert.AreEqual(s.id, state.currentShot);
        }
    }
}
