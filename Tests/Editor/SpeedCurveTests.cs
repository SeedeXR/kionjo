using System.Collections.Generic;
using NUnit.Framework;

namespace SeedeXR.Kionjo.Tests
{
    public class SpeedCurveTests
    {
        static List<KionjoSpeedKey> Keys(params float[] tAndSpeed)
        {
            var keys = new List<KionjoSpeedKey>();
            for (int i = 0; i < tAndSpeed.Length; i += 2) keys.Add(new KionjoSpeedKey { t = tAndSpeed[i], speed = tAndSpeed[i + 1] });
            return keys;
        }

        [Test]
        public void NoKeysIsRealTime()
        {
            Assert.AreEqual(1f, KionjoSpeed.Evaluate(null, 3f));
            Assert.AreEqual(1f, KionjoSpeed.Evaluate(new List<KionjoSpeedKey>(), 3f));
            Assert.AreEqual(2f, KionjoSpeed.SourceSeconds(null, 120, 60f), 1e-5);
            Assert.AreEqual(120, KionjoSpeed.FramesFor(null, 2f, 60f));
        }

        [Test]
        public void HoldsTheEndKeysOutsideTheirRange()
        {
            var k = Keys(1f, 0.2f, 2f, 1f);
            Assert.AreEqual(0.2f, KionjoSpeed.Evaluate(k, 0f), 1e-5);
            Assert.AreEqual(1f, KionjoSpeed.Evaluate(k, 5f), 1e-5);
        }

        [Test]
        public void EasesBetweenKeys()
        {
            var k = Keys(0f, 0f, 1f, 1f);
            Assert.AreEqual(0.5f, KionjoSpeed.Evaluate(k, 0.5f), 1e-5);
            Assert.Less(KionjoSpeed.Evaluate(k, 0.25f), 0.25f, "slow in");
            Assert.Greater(KionjoSpeed.Evaluate(k, 0.75f), 0.75f, "slow out");
        }

        [Test]
        public void FrozenTimeDoesNotAdvanceTheSource()
        {
            var k = Keys(0f, 0f);
            Assert.AreEqual(0f, KionjoSpeed.SourceSeconds(k, 600, 60f), 1e-6);
        }

        [Test]
        public void HalfSpeedTakesTwiceTheFrames()
        {
            var k = Keys(0f, 0.5f);
            Assert.AreEqual(1f, KionjoSpeed.SourceSeconds(k, 120, 60f), 1e-5);
            Assert.AreEqual(240, KionjoSpeed.FramesFor(k, 2f, 60f));
        }

        [Test]
        public void RampOutOfAFreezeFinishesTheSource()
        {
            // frozen for 1 s, then real time: 2 s of take need 1 s + 2 s plus the ease between
            var k = Keys(1f, 0f, 1.5f, 1f);
            int n = KionjoSpeed.FramesFor(k, 2f, 60f);
            Assert.GreaterOrEqual(KionjoSpeed.SourceSeconds(k, n, 60f), 2f - 1e-4);
            Assert.Less(KionjoSpeed.SourceSeconds(k, n - 1, 60f), 2f - 1e-4);
            Assert.That(n, Is.InRange(60 * 3, 60 * 4));
        }

        [Test]
        public void AFreezeThatNeverEndsIsCapped()
        {
            Assert.AreEqual(500, KionjoSpeed.FramesFor(Keys(0f, 0f), 1f, 60f, 500));
        }
    }
}
