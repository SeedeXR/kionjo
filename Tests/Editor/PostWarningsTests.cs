using System.Collections.Generic;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;
using UnityEngine;

namespace SeedeXR.Kionjo.Tests
{
    public class PostWarningsTests
    {
        KionjoBrandKit _kit;

        [SetUp]
        public void SetUp() => _kit = ScriptableObject.CreateInstance<KionjoBrandKit>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_kit);

        static PostPlan Plan(float seconds)
        {
            var plan = new PostPlan { TotalSeconds = seconds };
            plan.Segments.Add(new PostSegment { Label = "01-a", Duration = 4f });
            return plan;
        }

        static KionjoPost Post(string path = "D:/a.mp4") => new() { hook = "Can it turn?", overlays = { "She never does." }, items = { new PostItem { path = path } } };

        List<string> Check(PostPlan plan, KionjoPost p, JournalEntry e = null) => PostWarnings.Check(plan, e ?? new JournalEntry(), p, _kit);

        [Test]
        public void Over90s_Warns() => CollectionAssert.Contains(Check(Plan(91f), Post()), "Longer than 90 s: most Reels that travel are 20 to 45 s.");

        [Test]
        public void Over180s_Refused()
        {
            Assert.IsTrue(PostWarnings.Refuse(Plan(181f), out var reason));
            Assert.AreEqual("Longer than 3 minutes: Instagram will not recommend it.", reason);
            Assert.IsFalse(PostWarnings.Refuse(Plan(30f), out _));
        }

        [Test]
        public void NoText_Warns()
        {
            var p = Post();
            p.hook = "";
            p.overlays.Clear();
            CollectionAssert.Contains(Check(Plan(30f), p), "No text on screen: most viewers watch muted.");
        }

        [Test]
        public void LongHook_Warns()
        {
            var p = Post();
            p.hook = "one two three four five six seven eight nine";
            CollectionAssert.Contains(Check(Plan(30f), p), "Hook has 9 words: keep it to 7 or fewer.");
        }

        [Test]
        public void LongShot_Warns()
        {
            var plan = Plan(30f);
            plan.Segments.Add(new PostSegment { Label = "02-b", Duration = 6.5f });
            CollectionAssert.Contains(Check(plan, Post()), "Shot 02-b holds for 6.5 s: consider a cut or a change every few seconds.");
        }

        [Test]
        public void Jargon_Warns()
        {
            var p = Post();
            p.overlays.Add("The Shader does it.");
            var list = Check(Plan(30f), p);
            CollectionAssert.Contains(list, "Jargon on screen: shader. Say it in plain words.");
            CollectionAssert.DoesNotContain(list, "Jargon on screen: build. Say it in plain words.");
        }

        [Test]
        public void Watermark_Warns() =>
            CollectionAssert.Contains(Check(Plan(30f), Post("D:/dl/snaptik_123.mp4")), "snaptik_123.mp4 may carry another app's watermark: Instagram demotes those.");

        [Test]
        public void NotOwnWithoutCommentary_Warns()
        {
            var p = Post("D:/x/theirs.mp4");
            p.overlays.Clear();
            var e = new JournalEntry { media = { new JournalMedia { path = "D:/x/theirs.mp4", notOwn = true } } };
            CollectionAssert.Contains(Check(Plan(30f), p, e), "theirs.mp4 is not your footage and has no commentary on top: add text or voice.");
            CollectionAssert.DoesNotContain(Check(Plan(30f), Post("D:/x/theirs.mp4"), e), "theirs.mp4 is not your footage and has no commentary on top: add text or voice.");
        }

        [Test]
        public void DroppedOverlays_Warns()
        {
            var plan = Plan(30f);
            plan.Warnings.Add("2 overlay(s) did not fit and were left out.");
            CollectionAssert.Contains(Check(plan, Post()), "2 overlay(s) did not fit and were left out.");
        }

        [Test]
        public void CleanPost_NoWarnings() => CollectionAssert.IsEmpty(Check(Plan(30f), Post()));
    }
}
