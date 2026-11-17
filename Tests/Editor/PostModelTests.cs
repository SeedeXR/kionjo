using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;

namespace SeedeXR.Kionjo.Tests
{
    public class PostModelTests
    {
        [Test]
        public void Fill_Question_UsesAchieved()
        {
            var e = new JournalEntry { title = "Ghost", achieved = "A ghost refuse to turn round.", trick = "t" };
            Assert.AreEqual("Can a ghost refuse to turn round?", HookTemplates.Fill(HookKind.Question, e));
            Assert.AreEqual("POV: a ghost refuse to turn round", HookTemplates.Fill(HookKind.Pov, e));
            Assert.AreEqual("Nobody believes this is a ghost refuse to turn round.", HookTemplates.Fill(HookKind.Claim, e));
            Assert.AreEqual("This changes how you see a ghost refuse to turn round.", HookTemplates.Fill(HookKind.Promise, e));
        }

        [Test]
        public void Fill_CutsToSixWords()
        {
            var e = new JournalEntry { title = "One two three four five six seven eight" };
            Assert.AreEqual("Can one two three four five six?", HookTemplates.Fill(HookKind.Question, e));
        }

        sealed class Lengths : IMediaInfo
        {
            public Dictionary<string, float> Seconds = new();
            public float Duration(string path) => Seconds.TryGetValue(path, out var s) ? s : -1f;
            public (int w, int h) Size(string path) => (1920, 1080);
            public bool HasAudio(string path) => true;
        }

        static KionjoPost FromEntry(JournalEntry e, IMediaInfo info)
        {
            var m = typeof(KionjoPost).GetMethod("FromEntry", new[] { typeof(JournalEntry), typeof(IMediaInfo) });
            Assert.IsNotNull(m, "KionjoPost.FromEntry(entry, info) is missing");
            return (KionjoPost)m.Invoke(null, new object[] { e, info });
        }

        /// <summary>
        /// Several angles of one moment (same length) are cut across in time, hook angle first: the post is as long as
        /// the moment, not the angles one after another (7 angles of 28.8 s made a 201 s post that Kionjo refused).
        /// </summary>
        [Test]
        public void FromEntry_AnglesOfOneMoment_CutAcross()
        {
            var e = new JournalEntry { title = "Hero" };
            e.media.Add(new JournalMedia { path = "side.mp4", role = "broll" });
            e.media.Add(new JournalMedia { path = "main.mp4", role = "hook" });
            e.media.Add(new JournalMedia { path = "close.mp4", role = "broll" });
            var info = new Lengths { Seconds = { ["main.mp4"] = 28.8f, ["side.mp4"] = 28.8f, ["close.mp4"] = 28.79f } };
            var p = FromEntry(e, info);
            CollectionAssert.AreEqual(new[] { "main.mp4", "side.mp4", "close.mp4" }, p.items.Select(i => i.path).ToArray());
            Assert.AreEqual(0f, p.items[0].inPoint, 1e-3f);
            Assert.AreEqual(p.items[0].outPoint, p.items[1].inPoint, 1e-3f, "each angle starts where the last one stopped");
            Assert.AreEqual(p.items[1].outPoint, p.items[2].inPoint, 1e-3f);
            Assert.AreEqual(28.79f, p.items[2].outPoint, 0.02f, "the cut runs to the end of the moment");
        }

        [Test]
        public void FromEntry_DifferentClips_StayWhole()
        {
            var e = new JournalEntry { title = "Hero" };
            e.media.Add(new JournalMedia { path = "a.mp4", role = "broll" });
            e.media.Add(new JournalMedia { path = "b.mp4", role = "broll" });
            var p = FromEntry(e, new Lengths { Seconds = { ["a.mp4"] = 4f, ["b.mp4"] = 9f } });
            Assert.IsTrue(p.items.All(i => i.inPoint == 0f && i.outPoint == 0f));
        }

        [Test]
        public void FromEntry_ItemsSkipStills_HookFirst()
        {
            var e = new JournalEntry { title = "Ghost" };
            e.media.Add(new JournalMedia { path = "a.mp4", role = "broll" });
            e.media.Add(new JournalMedia { path = "s.png", role = "still" });
            e.media.Add(new JournalMedia { path = "h.mp4", role = "hook", inPoint = 1f, outPoint = 2f });
            var p = KionjoPost.FromEntry(e);
            CollectionAssert.AreEqual(new[] { "h.mp4", "a.mp4" }, p.items.Select(i => i.path).ToArray());
            Assert.AreEqual(1f, p.items[0].inPoint);
            Assert.AreEqual(2f, p.items[0].outPoint);
            Assert.AreEqual("ghost", p.name);
        }

        [Test]
        public void FromEntry_OverlaysFirstSentenceOnly()
        {
            var e = new JournalEntry { title = "Ghost", making = "We built a ghost. It was hard.", achieved = "", hurdle = "She turned round! Always.", trick = "Lock the head" };
            e.hookOptions.Add("Can a ghost refuse to turn?");
            var p = KionjoPost.FromEntry(e);
            CollectionAssert.AreEqual(new[] { "We built a ghost.", "She turned round!", "Lock the head" }, p.overlays);
            Assert.AreEqual("Can a ghost refuse to turn?", p.hook);
        }

        [Test]
        public void FromEntry_DecisionsFollowBeats()
        {
            var e = new JournalEntry { title = "Ghost", trick = "Lock the head.", decisions = new List<string> { "Keep it grey. Always.", "", "One accent only." } };
            var p = KionjoPost.FromEntry(e);
            CollectionAssert.AreEqual(new[] { "Lock the head.", "Keep it grey.", "One accent only." }, p.overlays);
            Assert.AreEqual("Can lock the head?", p.hook);
        }

        [Test]
        public void FromEntry_CoverTitleThreeWordsUpper()
        {
            var p = KionjoPost.FromEntry(new JournalEntry { title = "The ghost never turns round" });
            Assert.AreEqual("THE GHOST NEVER", p.coverTitle);
        }
    }
}
