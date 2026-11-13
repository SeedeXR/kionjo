using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;

namespace SeedeXR.Kionjo.Tests
{
    public class AssWriterTests
    {
        static string[] Dialogues(string ass) => ass.Split('\n').Where(l => l.StartsWith("Dialogue:")).ToArray();

        [Test]
        public void Escape_BracesBackslashNewline() => Assert.AreEqual("a\\{b\\}\\\\c\\Nd", AssWriter.Escape("a{b}\\c\nd"));

        [Test]
        public void Time_Centiseconds()
        {
            Assert.AreEqual("0:01:09.60", AssWriter.Time(69.6f));
            Assert.AreEqual("0:00:00.50", AssWriter.Time(0.5f));
            Assert.AreEqual("1:00:00.00", AssWriter.Time(3600f));
        }

        [Test]
        public void Write_HookOverlay_ExactEvents()
        {
            var texts = new List<TimedText>
            {
                new() { Text = "Can a ghost refuse to turn?", Start = 0.5f, End = 2.5f, IsHook = true },
                new() { Text = "She never turns {round}.", Start = 2.7f, End = 4.7f },
            };
            var lines = Dialogues(AssWriter.Write(1080, 1920, texts, null, null));
            CollectionAssert.AreEqual(new[]
            {
                "Dialogue: 0,0:00:00.50,0:00:02.50,Hook,,0,0,0,,{\\pos(540,780)}{\\fad(150,150)}Can a ghost refuse to turn?",
                "Dialogue: 0,0:00:02.70,0:00:04.70,Overlay,,0,0,0,,{\\fad(150,150)}She never turns \\{round\\}.",
            }, lines);
        }

        [Test]
        public void Write_Captions_GroupedByThree_KfTags()
        {
            var words = new List<WordTimer.Word>
            {
                new() { w = "It", s = 0f, e = 0.3f },
                new() { w = "was", s = 0.3f, e = 0.55f },
                new() { w = "her", s = 0.55f, e = 0.95f },
                new() { w = "all", s = 1.2f, e = 1.4f },
                new() { w = "along", s = 1.4f, e = 1.9f },
            };
            var lines = Dialogues(AssWriter.Write(1080, 1920, new List<TimedText>(), words, null));
            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual("Dialogue: 0,0:00:00.00,0:00:00.95,Caption,,0,0,0,,{\\kf30}It {\\kf25}was {\\kf40}her", lines[0]);
            Assert.AreEqual("Dialogue: 0,0:00:01.20,0:00:01.90,Caption,,0,0,0,,{\\kf20}all {\\kf50}along", lines[1]);
        }

        [Test]
        public void Write_Header_PlayResAndStyles()
        {
            var ass = AssWriter.Write(1080, 1920, new List<TimedText>(), null, null);
            StringAssert.Contains("PlayResX: 1080", ass);
            StringAssert.Contains("PlayResY: 1920", ass);
            StringAssert.Contains("Style: Hook,Arial,86,", ass);
            StringAssert.Contains("Style: Overlay,Arial,65,", ass);
            StringAssert.Contains("Style: Caption,Arial,77,", ass);
            // overlay sits on the title band's bottom edge, captions above the bottom zone
            StringAssert.Contains(",2,65,150,420,1\n", ass);
            StringAssert.Contains(",2,65,150,708,1\n", ass);
        }
    }
}
