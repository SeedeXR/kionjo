using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;
using UnityEditor;
using UnityEngine;

namespace SeedeXR.Kionjo.Tests
{
    public class CutPlanBuilderTests
    {
        sealed class FakeProbe : IMediaProbe
        {
            public readonly Dictionary<string, float> Durations = new();
            public float Default = -1f;
            public float DurationSeconds(string path) => Durations.TryGetValue(path, out var d) ? d : Default;
        }

        string _dir;
        KionjoStoryboard _sb;
        FakeProbe _probe;
        Dictionary<KionjoShot, string> _sources;
        Dictionary<KionjoShot, KionjoTakeMeta> _metas;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(_dir);
            _sb = ScriptableObject.CreateInstance<KionjoStoryboard>();
            _probe = new FakeProbe();
            _sources = new Dictionary<KionjoShot, string>();
            _metas = new Dictionary<KionjoShot, KionjoTakeMeta>();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            Object.DestroyImmediate(_sb);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        string File_(string name, float duration = -1f)
        {
            var p = Path.Combine(_dir, name);
            File.WriteAllText(p, "");
            if (duration > 0f) _probe.Durations[p] = duration;
            return p;
        }

        CutPlan Build(bool animatic = false) => CutPlanBuilder.Build(_sb, KionjoCutPreset.Preview720p30, _probe,
            s => _sources.TryGetValue(s, out var src) ? src : null,
            s => _metas.TryGetValue(s, out var m) ? m : null, animatic);

        KionjoShot Card(float seconds)
        {
            var c = _sb.AddShot(KionjoShotType.Card);
            c.targetSeconds = seconds;
            return c;
        }

        KionjoShot Video(string src, float inPoint, float outPoint)
        {
            var s = _sb.AddShot();
            s.inPoint = inPoint;
            s.outPoint = outPoint;
            _sources[s] = src;
            return s;
        }

        [Test]
        public void FilmStarts_AreRunningSums()
        {
            Card(7.5f);
            Video(File_("a.mp4"), 0.5f, 6.5f);
            Video(File_("b.mp4"), 6.5f, 10f);
            var plan = Build();
            CollectionAssert.AreEqual(new[] { 0f, 7.5f, 13.5f }, plan.Segments.Select(s => s.FilmStart).ToArray());
            Assert.AreEqual(17f, plan.TotalSeconds, 1e-4);
            Assert.AreEqual(SegmentKind.Black, plan.Segments[0].Kind);
            Assert.AreEqual(0.5f, plan.Segments[1].SourceIn);
            Assert.AreEqual("02_S01", plan.Segments[1].Label);
            Assert.AreEqual(1280, plan.Width);
        }

        [Test]
        public void Video_WithoutOutPoint_UsesProbe()
        {
            Video(File_("a.mp4", 9f), 2f, 0f);
            Assert.AreEqual(7f, Build().Segments[0].Duration, 1e-4);
        }

        [Test]
        public void Voice_AtIsRelativeToShot()
        {
            Card(7.5f);
            var s = Video(File_("a.mp4"), 0f, 6f);
            s.voice.Add(new KionjoVoiceLine { audioPath = File_("line.wav", 3f), at = 0.5f, subtitle = "Why here?", trimMode = KionjoTrimMode.Manual });
            var plan = Build();
            Assert.AreEqual(8f, plan.Voice[0].FilmAt, 1e-4);
            Assert.AreEqual(3f, plan.Voice[0].TrimOut, 1e-4);
        }

        [Test]
        public void Sfx_BoundToEvent_MapsThroughInPoint()
        {
            Card(51.5f);
            var s = Video(File_("card.mp4"), 5.8f, 8.8f);
            s.sfx.Add(new KionjoSfxCue { audioPath = File_("open.wav", 1f), eventName = "seal" });
            _metas[s] = new KionjoTakeMeta { events = { new KionjoTakeEvent { t = 6.37f, name = "seal" } } };
            var plan = Build();
            Assert.AreEqual(52.07f, plan.Sfx[0].FilmAt, 1e-3);
        }

        [Test]
        public void Sfx_EventOutsideRange_Warns()
        {
            var s = Video(File_("card.mp4"), 5.8f, 8.8f);
            s.sfx.Add(new KionjoSfxCue { audioPath = File_("open.wav", 1f), eventName = "seal" });
            _metas[s] = new KionjoTakeMeta { events = { new KionjoTakeEvent { t = 12f, name = "seal" } } };
            var plan = Build();
            Assert.AreEqual(0, plan.Sfx.Count);
            Assert.IsTrue(plan.Warnings.Any(w => w.Contains("outside")), string.Join("\n", plan.Warnings));
        }

        [Test]
        public void Sourceless_Shot_SkippedWithWarning()
        {
            Card(2f);
            _sb.AddShot(); // no source
            var plan = Build();
            Assert.AreEqual(1, plan.Segments.Count);
            Assert.IsTrue(plan.Warnings.Any(w => w.StartsWith("S01: no source")), string.Join("\n", plan.Warnings));
        }

        [Test]
        public void OnlyCards_BuildsPlan()
        {
            Card(1f);
            var t = Card(1.5f);
            t.cardKind = KionjoCardKind.Text;
            t.cardLines.Add("The end");
            var plan = Build();
            Assert.AreEqual(2, plan.Segments.Count);
            Assert.AreEqual(SegmentKind.Text, plan.Segments[1].Kind);
            Assert.AreEqual(2.5f, plan.TotalSeconds, 1e-4);
            Assert.IsEmpty(plan.Warnings);
        }

        [Test]
        public void Subtitle_UsesProbeWhenNoTrimOut()
        {
            var s = Video(File_("a.mp4"), 0f, 6f);
            s.voice.Add(new KionjoVoiceLine { audioPath = File_("whyhere.wav", 4.51f), at = 1f, subtitle = "Why here?", trimMode = KionjoTrimMode.Manual });
            var sub = Build().Subtitles.Single();
            Assert.AreEqual(1f, sub.Start, 1e-4);
            Assert.AreEqual(5.51f, sub.End, 1e-4);
        }

        [Test]
        public void Animatic_UsesTargetSecondsAndThumbnails()
        {
            var a = Video(File_("a.mp4"), 0f, 6f);
            a.targetSeconds = 4f;
            a.thumbnailPath = File_("thumb.png");
            var b = _sb.AddShot();
            b.targetSeconds = 3f;
            b.name = "Crane";
            var plan = Build(animatic: true);
            Assert.AreEqual(SegmentKind.Image, plan.Segments[0].Kind);
            Assert.AreEqual(4f, plan.Segments[0].Duration);
            Assert.AreEqual(SegmentKind.Text, plan.Segments[1].Kind);
            Assert.AreEqual("S02  Crane", plan.Segments[1].Lines[0]);
            Assert.AreEqual(7f, plan.TotalSeconds, 1e-4);
        }

        [Test]
        public void OrganiseOnly_IsANoteNotAWarning()
        {
            var s = Video(File_("a.mp4"), 0f, 2f);
            s.outputMode = KionjoOutputMode.OrganiseOnly;
            var plan = Build();
            Assert.IsEmpty(plan.Segments);
            Assert.IsEmpty(plan.Warnings);
            Assert.AreEqual(1, plan.Notes.Count);
        }
    }
}
