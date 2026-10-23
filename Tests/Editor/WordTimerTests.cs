using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;

namespace SeedeXR.Kionjo.Tests
{
    public class WordTimerTests
    {
        const string Json = "{\"words\":[{\"w\":\"Human,\",\"s\":0.12,\"e\":0.41},{\"w\":\"you\",\"s\":0.5,\"e\":0.62},{\"w\":\"stand\",\"s\":0.7,\"e\":1.05}]}";

        [Test]
        public void Parse_WordsJson()
        {
            var words = WordTimer.Parse(Json, out var error);
            Assert.IsNull(error);
            Assert.AreEqual(3, words.Count);
            Assert.AreEqual("Human,", words[0].w);
            Assert.AreEqual(1.05f, words[2].e, 1e-5);
        }

        [Test]
        public void Parse_ErrorJson()
        {
            var words = WordTimer.Parse("{\"error\":\"faster-whisper is not installed\"}", out var error);
            Assert.IsEmpty(words);
            StringAssert.Contains("faster-whisper", error);
        }

        [Test]
        public void Bounds_FirstAndLastWord()
        {
            var words = WordTimer.Parse(Json, out _);
            Assert.AreEqual((0.12f, 1.05f), WordTimer.Bounds(words, 0, -1).Value);
            Assert.AreEqual((0.5f, 0.62f), WordTimer.Bounds(words, 1, 1).Value);
            Assert.AreEqual((0.7f, 1.05f), WordTimer.Bounds(words, 9, 9).Value, "out of range clamps to the last word");
            Assert.IsNull(WordTimer.Bounds(new System.Collections.Generic.List<WordTimer.Word>(), 0, -1));
        }

        [Test]
        public void Combine_LineEnds_UseSilenceEdges_InsideUsesWords()
        {
            // whyhere-01: whisper ends "Justice?" at 3.5 s, the voice really ends at 4.0 s
            var words = WordTimer.Parse("{\"words\":[{\"w\":\"Human,\",\"s\":0.0,\"e\":0.4},{\"w\":\"do\",\"s\":0.74,\"e\":0.88},{\"w\":\"Justice?\",\"s\":3.18,\"e\":3.5}]}", out _);
            var whole = WordTimer.Combine(words, 0, -1, (0.02f, 4.0f)).Value;
            Assert.AreEqual(0.02f, whole.start, 1e-5);
            Assert.AreEqual(4.0f, whole.end, 1e-5, "the line's own end comes from the silence edge, not whisper");
            var middle = WordTimer.Combine(words, 1, 1, (0.02f, 4.0f)).Value;
            Assert.AreEqual(0.69f, middle.start, 1e-4);
            Assert.AreEqual(1.0f, middle.end, 1e-4, "inside the line: the word end plus a short pad");
        }

        [Test]
        public void Builder_WordsMode_UsesResolver()
        {
            var sb = UnityEngine.ScriptableObject.CreateInstance<KionjoStoryboard>();
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "KionjoTests", nameof(Builder_WordsMode_UsesResolver));
            System.IO.Directory.CreateDirectory(dir);
            var wav = System.IO.Path.Combine(dir, "line.wav");
            System.IO.File.WriteAllText(wav, "");
            try
            {
                var card = sb.AddShot(KionjoShotType.Card);
                card.voice.Add(new KionjoVoiceLine { audioPath = wav, trimMode = KionjoTrimMode.Words, subtitle = "x" });
                var plan = CutPlanBuilder.Build(sb, KionjoCutPreset.Preview720p30, new ZeroProbe(), _ => null, _ => null, false, _ => (0.82f, 12.88f));
                Assert.AreEqual(0.82f, plan.Voice[0].TrimIn, 1e-5);
                Assert.AreEqual(12.88f, plan.Voice[0].TrimOut, 1e-5);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sb);
                UnityEditor.Undo.ClearAll();
                System.IO.Directory.Delete(dir, true);
            }
        }

        sealed class ZeroProbe : IMediaProbe
        {
            public float DurationSeconds(string path) => 13.7f;
        }
    }
}
