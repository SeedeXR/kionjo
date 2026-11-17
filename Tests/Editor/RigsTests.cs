using NUnit.Framework;
using UnityEngine;

namespace SeedeXR.Kionjo.Tests
{
    public class RigsTests
    {
        static readonly Vector3 Centre = new(1, 2, 3);
        static readonly Vector3 MainPos = Centre + new Vector3(0.5f, 0.4f, -5f);
        static Quaternion MainRot => Quaternion.LookRotation(Centre - MainPos);

        [Test]
        public void NoRigInsideSubject()
        {
            foreach (var size in new[] { 0.5f, 2f, 6f })
            foreach (var u in new[] { 0f, 0.5f, 1f })
            foreach (var rig in KionjoRigs.All)
            {
                if (rig.StartsWith("main")) continue;
                var p = KionjoRigs.Place(rig, Centre, size, MainPos, MainRot, u);
                Assert.Greater(Vector3.Distance(p.position, Centre), size, $"{rig} size {size} u {u}");
            }
        }

        [Test]
        public void MainMatchesMain()
        {
            foreach (var rig in new[] { "main", "main-vertical" })
            {
                var p = KionjoRigs.Place(rig, Centre, 2, MainPos, MainRot, 0.3f);
                Assert.AreEqual(0f, Vector3.Distance(MainPos, p.position), 1e-4f);
                Assert.AreEqual(0f, Quaternion.Angle(MainRot, p.rotation), 1e-2f);
            }
        }

        [Test]
        public void OrbitSweeps()
        {
            var a = KionjoRigs.Place("orbit", Centre, 2, MainPos, MainRot, 0).position - Centre;
            var b = KionjoRigs.Place("orbit", Centre, 2, MainPos, MainRot, 1).position - Centre;
            a.y = 0;
            b.y = 0;
            Assert.AreEqual(120f, Vector3.Angle(a, b), 0.5f);
        }

        [Test]
        public void FormatsAreFixed()
        {
            Assert.AreEqual((1920, 1080), Dims("main"));
            Assert.AreEqual((1080, 1920), Dims("main-vertical"));
            Assert.AreEqual((1920, 1080), Dims("wide"));
            Assert.AreEqual((1080, 1920), Dims("low"));
            Assert.AreEqual((1080, 1920), Dims("close"));
            Assert.AreEqual(55f, KionjoRigs.Format("wide", 60).vfov);
        }

        static (int, int) Dims(string rig)
        {
            var f = KionjoRigs.Format(rig, 60);
            return (f.w, f.h);
        }

        [Test]
        public void LowLooksUp()
        {
            var p = KionjoRigs.Place("low", Centre, 2, MainPos, MainRot, 0);
            Assert.Greater((p.rotation * Vector3.forward).y, 0f);
            Assert.Less(p.position.y, Centre.y);
        }
    }
}
