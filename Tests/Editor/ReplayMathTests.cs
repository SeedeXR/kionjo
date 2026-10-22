using NUnit.Framework;
using UnityEngine;

namespace SeedeXR.Kionjo.Tests
{
    public class ReplayMathTests
    {
        static KionjoPoseSample Pose(Vector3 p, float yaw = 0f, bool active = true) =>
            new() { active = active, position = p, rotation = Quaternion.Euler(0, yaw, 0), scale = Vector3.one };

        [Test]
        public void Sample_Midway_BlendsPositionAndRotation()
        {
            var times = new[] { 0f, 1f };
            var track = new[] { Pose(Vector3.zero, 0), Pose(new Vector3(0.2f, 0, 0), 20) };
            var s = KionjoReplayMath.Sample(times, track, 0.5f);
            Assert.Less(Vector3.Distance(new Vector3(0.1f, 0, 0), s.position), 1e-4f);
            Assert.AreEqual(10f, s.rotation.eulerAngles.y, 1e-3);
        }

        [Test]
        public void Sample_AcrossTeleport_HoldsEarlierFrame()
        {
            var times = new[] { 0f, 1f };
            var track = new[] { Pose(Vector3.zero), Pose(new Vector3(2, 0, 0)) };
            Assert.AreEqual(Vector3.zero, KionjoReplayMath.Sample(times, track, 0.7f).position);
        }

        [Test]
        public void Sample_SnapTurn_IsCut()
        {
            Assert.IsTrue(KionjoReplayMath.IsCut(Pose(Vector3.zero, 0), Pose(Vector3.zero, 45), 0.5f, 25f));
            var s = KionjoReplayMath.Sample(new[] { 0f, 1f }, new[] { Pose(Vector3.zero, 0), Pose(Vector3.zero, 45) }, 0.5f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(0, s.rotation.eulerAngles.y), 1e-3);
        }

        [Test]
        public void Sample_IntoInactiveFrame_Holds()
        {
            var track = new[] { Pose(Vector3.zero), Pose(new Vector3(0.1f, 0, 0), 0, active: false) };
            var s = KionjoReplayMath.Sample(new[] { 0f, 1f }, track, 0.5f);
            Assert.IsTrue(s.active);
            Assert.AreEqual(Vector3.zero, s.position);
        }

        [Test]
        public void Sample_OutsideRange_Clamps()
        {
            var times = new[] { 1f, 2f };
            var track = new[] { Pose(Vector3.zero), Pose(new Vector3(0.1f, 0, 0)) };
            Assert.AreEqual(Vector3.zero, KionjoReplayMath.Sample(times, track, -5f).position);
            Assert.AreEqual(new Vector3(0.1f, 0, 0), KionjoReplayMath.Sample(times, track, 9f).position);
        }

        [Test]
        public void Sample_EmptyTrack_ReturnsInactive() =>
            Assert.IsFalse(KionjoReplayMath.Sample(new float[0], new KionjoPoseSample[0], 1f).active);

        [Test]
        public void Stabilize_RollLock_RemovesRoll()
        {
            var track = new[] { new KionjoPoseSample { active = true, rotation = Quaternion.Euler(10, 20, 30) } };
            var r = KionjoReplayMath.Stabilize(new[] { 0f }, track, 0.1f, rollLock: true)[0].rotation.eulerAngles;
            Assert.AreEqual(0f, Mathf.DeltaAngle(0, r.z), 0.01);
            Assert.AreEqual(10f, Mathf.DeltaAngle(0, r.x), 0.01);
            Assert.AreEqual(20f, Mathf.DeltaAngle(0, r.y), 0.01);
        }

        [Test]
        public void Stabilize_SmoothingStopsAtCut()
        {
            int n = 20;
            var times = new float[n];
            var track = new KionjoPoseSample[n];
            for (int i = 0; i < n; i++)
            {
                times[i] = i / 90f;
                track[i] = i < 10 ? Pose(new Vector3(i % 2 == 0 ? 0f : 0.02f, 0, 0)) : Pose(new Vector3(5, 0, 0));
            }
            var result = KionjoReplayMath.Stabilize(times, track, 0.1f, rollLock: true);
            for (int i = 10; i < n; i++) Assert.AreEqual(5f, result[i].position.x, 1e-5, $"frame {i}");
            Assert.Greater(result[4].position.x, 0f); // jitter before the cut was averaged
            Assert.Less(result[4].position.x, 0.02f);
        }

        [Test]
        public void UnwrapYaw_AcrossSouth() => Assert.AreEqual(190f, KionjoReplayMath.UnwrapYaw(170f, -170f), 1e-4);

        [Test]
        public void VerticalFov_80Horizontal16by9_Is50_5() =>
            Assert.AreEqual(50.5f, KionjoReplayMath.VerticalFov(80f, 16f / 9f), 0.1);

        [Test]
        public void FindFrame_BinarySearch()
        {
            var times = new[] { 0f, 0.1f, 0.2f, 0.3f };
            Assert.AreEqual(-1, KionjoReplayMath.FindFrame(times, -0.01f));
            Assert.AreEqual(0, KionjoReplayMath.FindFrame(times, 0.05f));
            Assert.AreEqual(2, KionjoReplayMath.FindFrame(times, 0.2f));
            Assert.AreEqual(3, KionjoReplayMath.FindFrame(times, 5f));
        }
    }
}
