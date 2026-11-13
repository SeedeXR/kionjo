using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;

namespace SeedeXR.Kionjo.Tests
{
    public class PostTimingTests
    {
        [Test]
        public void SafeZone_1080x1920_Exact()
        {
            var z = SafeZone.For(1080, 1920);
            Assert.AreEqual((270, 670, 65, 150, 420, 1500), (z.Top, z.Bottom, z.Left, z.Right, z.TitleTop, z.TitleBottom));
        }

        [Test]
        public void SafeZone_1080x1350_Scaled()
        {
            var z = SafeZone.For(1080, 1350);
            Assert.AreEqual(190, z.Top);
            Assert.AreEqual(471, z.Bottom);
        }

        [Test]
        public void Reading_ShortIsTwoSeconds() => Assert.AreEqual(2f, PostTiming.ReadingSeconds("Hi there"));

        [Test]
        public void Reading_NineWordsIsThree() => Assert.AreEqual(3f, PostTiming.ReadingSeconds("one two three four five six seven eight nine"), 1e-5);

        [Test]
        public void Schedule_HookAtHalfSecond_OverlaysFollow()
        {
            var t = PostTiming.Schedule("Can a ghost refuse to turn?", new[] { "First overlay here.", "one two three four five six seven eight nine" }, 30f);
            Assert.AreEqual(3, t.Count);
            Assert.IsTrue(t[0].IsHook);
            Assert.AreEqual(0.5f, t[0].Start, 1e-5);
            Assert.AreEqual(2.5f, t[0].End, 1e-5);
            Assert.AreEqual(2.7f, t[1].Start, 1e-5);
            Assert.AreEqual(4.7f, t[1].End, 1e-5);
            Assert.AreEqual(4.9f, t[2].Start, 1e-5);
            Assert.AreEqual(7.9f, t[2].End, 1e-5);
        }

        [Test]
        public void Schedule_DropsOverlayPastEnd()
        {
            var t = PostTiming.Schedule("Hook", new[] { "a b", "c d" }, 5.5f);
            // hook 0.5-2.5, first 2.7-4.7 fits (limit 5.0), second 4.9-6.9 does not
            Assert.AreEqual(2, t.Count);
            Assert.AreEqual("a b", t[1].Text);
        }
    }
}
