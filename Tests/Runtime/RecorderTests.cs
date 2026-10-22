using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SeedeXR.Kionjo.Tests
{
    public class RecorderTests
    {
        string _dir;
        GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        KionjoRecorder MakeRig(out Transform head, out Transform hand)
        {
            _root = new GameObject("Rig");
            head = new GameObject("Head").transform;
            head.SetParent(_root.transform);
            hand = new GameObject("RightHand").transform;
            hand.SetParent(_root.transform);
            var rec = _root.AddComponent<KionjoRecorder>();
            rec.tracks.Add(new KionjoTrack { name = "Head", target = head });
            rec.tracks.Add(new KionjoTrack { name = "RightHand", target = hand });
            return rec;
        }

        [UnityTest]
        public IEnumerator Records_FramesAndEvents()
        {
            var rec = MakeRig(out var head, out var hand);
            rec.StartTake(_dir, "id", "S01", 1);
            float markTime = 0f;
            for (int f = 0; f < 10; f++)
            {
                head.position = new Vector3(f, 1.6f, 0);
                hand.rotation = Quaternion.Euler(0, f * 5, 0);
                if (f == 5)
                {
                    KionjoEvents.Mark("beep", "1");
                    markTime = rec.Elapsed;
                }
                yield return null;
            }
            int written = rec.FrameCount;
            rec.StopTake();

            var data = KionjoTakeData.Load(_dir);
            Assert.GreaterOrEqual(written, 10);
            Assert.AreEqual(written, data.Times.Length);
            Assert.Less(Vector3.Distance(new Vector3(9, 1.6f, 0), data.Tracks[0][data.Times.Length - 1].position), 1e-5f);
            Assert.AreEqual("/Rig/Head", data.Meta.tracks[0].path);
            var beep = data.Meta.events.Single(e => e.name == "beep");
            Assert.AreEqual("1", beep.payload);
            Assert.AreEqual(markTime, beep.t, 1e-4);
        }

        [UnityTest]
        public IEnumerator Cap_StopsAndMarks()
        {
            var rec = MakeRig(out _, out _);
            rec.maxTakeSeconds = 0.05f;
            rec.StartTake(_dir, "id", "S01", 1);
            float until = Time.realtimeSinceStartup + 0.5f;
            while (rec.IsRecording && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsFalse(rec.IsRecording);
            Assert.IsTrue(KionjoTakeMeta.Load(_dir).events.Any(e => e.name == "cap"));
        }

        [UnityTest]
        public IEnumerator Destroy_WhileRecording_ClosesFile()
        {
            var rec = MakeRig(out _, out _);
            rec.StartTake(_dir, "id", "S01", 1);
            yield return null;
            yield return null;
            Object.Destroy(_root);
            _root = null;
            yield return null;
            var meta = KionjoTakeMeta.Load(_dir);
            Assert.IsNotNull(meta);
            Assert.Greater(meta.frameCount, 0);
        }

        [UnityTest]
        public IEnumerator Toggle_WithoutPending_DoesNotStart()
        {
            var rec = MakeRig(out _, out _);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no take is pending"));
            rec.ToggleTake();
            yield return null;
            Assert.IsFalse(rec.IsRecording);
        }

        [UnityTest]
        public IEnumerator Toggle_WithPending_StartsThenStops()
        {
            var rec = MakeRig(out _, out _);
            rec.PendingTakeFolder = _dir;
            rec.PendingShotCode = "S01";
            rec.PendingTakeNumber = 1;
            rec.ToggleTake();
            Assert.IsTrue(rec.IsRecording);
            yield return null;
            rec.ToggleTake();
            Assert.IsFalse(rec.IsRecording);
            Assert.IsNull(rec.PendingTakeFolder);
        }
    }
}
