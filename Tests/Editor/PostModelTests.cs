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
