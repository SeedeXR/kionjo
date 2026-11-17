using System.Collections.Generic;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;

namespace SeedeXR.Kionjo.Tests
{
    public class BriefWriterTests
    {
        static JournalEntry Full() => new JournalEntry
        {
            title = "The hero rises", stage = "prototype", feature = "hero", version = "main@abc123",
            making = "A hero that rises out of the floor.", reference = "Old films.", goal = "Make the entrance felt.",
            achieved = "He rises. Then he speaks.", hurdle = "The floor clipped him.", trick = "A clip plane at the floor.",
            decisions = new List<string> { "Rise, not fall: it reads as power." },
            hookOptions = new List<string> { "He came from below.", "Floor or portal?" },
            brief = "| shot | why |\n|---|---|\n| low | power |",
        };

        static readonly List<(string, string)> Materials = new() { ("hero [v3] wide.mp4", "wide angle"), ("still.png", "") };

        [Test]
        public void FullEntryHasSectionsInOrder()
        {
            var s = BriefWriter.Write(Full(), Materials);
            StringAssert.StartsWith("# The hero rises\n", s);
            StringAssert.Contains("Stage: prototype. Feature: hero. Version: main@abc123.", s);
            int last = -1;
            foreach (var h in new[] { "## In one sentence", "## Why", "## Approach", "## Decisions", "## Hurdle and trick", "## Materials", "## Hook options", "## Notes from the build" })
            {
                int at = s.IndexOf(h + "\n", System.StringComparison.Ordinal);
                Assert.Greater(at, last, h);
                last = at;
            }
            StringAssert.Contains("## In one sentence\nHe rises.\n", s);
            StringAssert.Contains("The floor clipped him. -> A clip plane at the floor.", s);
            StringAssert.Contains("1. He came from below.\n2. Floor or portal?", s);
        }

        [Test]
        public void EmptyFieldsOmitSections()
        {
            var s = BriefWriter.Write(new JournalEntry { title = "Bare", making = "Something." }, new List<(string, string)>());
            StringAssert.Contains("## In one sentence\nSomething.", s);
            foreach (var h in new[] { "## Why", "## Decisions", "## Hurdle and trick", "## Materials", "## Hook options", "## Notes from the build" })
                StringAssert.DoesNotContain(h, s);
        }

        [Test]
        public void MaterialsListNotes()
        {
            var s = BriefWriter.Write(Full(), Materials);
            StringAssert.Contains("- `hero [v3] wide.mp4`: wide angle\n", s);
            StringAssert.Contains("- `still.png`\n", s);
        }

        [Test]
        public void AgentBriefIsVerbatim()
        {
            var s = BriefWriter.Write(Full(), Materials);
            StringAssert.Contains("## Notes from the build\n| shot | why |\n|---|---|\n| low | power |", s);
        }
    }
}
