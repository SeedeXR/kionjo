using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SeedeXR.Kionjo.Tests
{
    public class ReplayerTests
    {
        GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            Time.captureDeltaTime = 0f;
        }

        static KionjoTakeData SyntheticTake(float seconds, float hz, List<KionjoTakeEvent> events = null)
        {
            int n = Mathf.RoundToInt(seconds * hz) + 1;
            var times = new float[n];
            var head = new KionjoPoseSample[n];
            var hand = new KionjoPoseSample[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / hz;
                times[i] = t;
                head[i] = new KionjoPoseSample { active = true, position = new Vector3(t * 0.3f, 1.6f, 0), rotation = Quaternion.Euler(0, t * 10f, 0), scale = Vector3.one };
                hand[i] = new KionjoPoseSample { active = true, position = new Vector3(0.2f, 1f, t), rotation = Quaternion.identity, scale = Vector3.one };
            }
            var meta = new KionjoTakeMeta
            {
                tracks = new List<KionjoTrackInfo> { new() { name = "Head" }, new() { name = "Hand" } },
                events = events ?? new List<KionjoTakeEvent>(),
            };
            return KionjoTakeData.FromArrays(meta, times, new[] { head, hand });
        }

        KionjoReplayer MakeReplayer(out Transform head, out Transform hand)
        {
            _root = new GameObject("Replay");
            head = new GameObject("Head").transform;
            hand = new GameObject("Hand").transform;
            head.SetParent(_root.transform);
            hand.SetParent(_root.transform);
            var r = _root.AddComponent<KionjoReplayer>();
            r.tracks.Add(new KionjoTrack { name = "Head", target = head });
            r.tracks.Add(new KionjoTrack { name = "Hand", target = hand });
            r.smoothingSeconds = 0f;
            r.fps = 30f;
            return r;
        }

        [UnityTest]
        public IEnumerator Play_FrameLocked_PoseMatchesSample()
        {
            var r = MakeReplayer(out _, out var hand);
            var take = SyntheticTake(2f, 90f);
            r.Load(take);
            r.Play();
            // yield null resumes after the next Update: the replayer's LateUpdate for frame k has run.
            // (WaitForEndOfFrame never fires in batchmode.)
            for (int k = 0; k < 6; k++)
            {
                yield return null;
                var expected = KionjoReplayMath.Sample(take.Times, take.Tracks[1], k / 30f);
                Assert.Less(Vector3.Distance(expected.position, hand.position), 1e-4f, $"frame {k}");
            }
        }

        [UnityTest]
        public IEnumerator Events_FireOnceAtTheirFrame()
        {
            var r = MakeReplayer(out _, out _);
            r.Load(SyntheticTake(1f, 90f, new List<KionjoTakeEvent> { new() { t = 0.1f, name = "seal", payload = "x" } }));
            var fired = new List<float>();
            r.EventFired += (n, p) => fired.Add(r.Clock);
            r.Play();
            for (int k = 0; k < 10; k++) yield return null;
            Assert.AreEqual(1, fired.Count);
            Assert.GreaterOrEqual(fired[0], 0.1f - 1e-4f);
            Assert.Less(fired[0], 0.1f + 0.5f / 30f); // the frame where Clock reaches 0.1, not the next
        }

        [UnityTest]
        public IEnumerator Stop_RestoresCaptureDeltaTime()
        {
            Time.captureDeltaTime = 0f;
            var r = MakeReplayer(out _, out _);
            r.Load(SyntheticTake(1f, 90f));
            r.Play();
            Assert.AreEqual(1f / 30f, Time.captureDeltaTime, 1e-6);
            yield return null;
            r.Stop();
            Assert.AreEqual(0f, Time.captureDeltaTime);
        }

        [UnityTest]
        public IEnumerator Play_EmptyTake_FinishesImmediately()
        {
            var r = MakeReplayer(out _, out _);
            r.Load(KionjoTakeData.FromArrays(new KionjoTakeMeta(), new float[0], new KionjoPoseSample[0][]));
            r.Play();
            yield return null;
            Assert.IsTrue(r.IsDone);
            Assert.IsFalse(r.IsPlaying);
        }

        [UnityTest]
        public IEnumerator HeadOverride_ReceivesHeadTrack()
        {
            var r = MakeReplayer(out var head, out _);
            var cam = new GameObject("RenderCam").transform;
            cam.SetParent(_root.transform);
            r.headOverride = cam;
            var take = SyntheticTake(1f, 90f);
            r.Load(take);
            r.Play(0.5f);
            yield return null;
            Assert.Less(Vector3.Distance(take.Tracks[0][45].position, cam.position), 1e-3f);
            Assert.AreEqual(Vector3.zero, head.position);
        }
    }
}
