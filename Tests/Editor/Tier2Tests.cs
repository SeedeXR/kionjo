using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;
using UnityEditor;
using UnityEngine;

namespace SeedeXR.Kionjo.Tests
{
    public class Fcp7XmlWriterTests
    {
        static CutPlan Plan()
        {
            var p = new CutPlan { Width = 1280, Height = 720, Fps = 30, MusicCrossfade = 2f };
            p.Segments.Add(new CutSegment { Label = "01_C01", Kind = SegmentKind.Black, Duration = 7.5f, FilmStart = 0f });
            p.Segments.Add(new CutSegment { Label = "02_S01", Kind = SegmentKind.Video, SourcePath = "D:/T/S03_take06.mp4", SourceIn = 0.5f, Duration = 6f, FilmStart = 7.5f });
            p.Voice.Add(new AudioCue { Path = "D:/VO/welcome-03.wav", TrimIn = 0f, TrimOut = 6.6f, FilmAt = 0.5f });
            p.Music.Add(new AudioCue { Path = "D:/M/track.mp3", TrimIn = 0f, TrimOut = 10f, FilmAt = 7.5f });
            p.Music.Add(new AudioCue { Path = "D:/M/track.mp3", TrimIn = 98.5f, TrimOut = 103.5f });
            p.Sfx.Add(new AudioCue { Path = "D:/S/open.wav", TrimOut = 0.8f, FilmAt = 9f });
            return p;
        }

        [Test]
        public void Write_IsWellFormedXml()
        {
            var doc = XDocument.Parse(Fcp7XmlWriter.Write(Plan(), "Trailer_draft01", "D:/cut/draft01"));
            Assert.AreEqual("xmeml", doc.Root.Name.LocalName);
            Assert.AreEqual(Fcp7XmlWriter.Frames(13.5f, 30).ToString(), doc.Root.Element("sequence").Element("duration").Value);
        }

        [Test]
        public void Video_ClipItem_FrameMath()
        {
            var doc = XDocument.Parse(Fcp7XmlWriter.Write(Plan(), "T", "D:/cut/draft01"));
            var clip = doc.Descendants("video").Elements("track").Elements("clipitem").ElementAt(1);
            Assert.AreEqual("15", clip.Element("in").Value);
            Assert.AreEqual("195", clip.Element("out").Value);
            Assert.AreEqual("225", clip.Element("start").Value);
            Assert.AreEqual("405", clip.Element("end").Value);
        }

        [Test]
        public void Card_References_SegFile()
        {
            var xml = Fcp7XmlWriter.Write(Plan(), "T", "D:/cut/draft01");
            StringAssert.Contains("file://localhost/D:/cut/draft01/seg/01_C01.mp4", xml);
        }

        [Test]
        public void PathUrl_EscapesSpacesAndBrackets() =>
            Assert.AreEqual("file://localhost/D:/BERNARD/UNITY%20PROJECTS%20(GAMES)/x%20%5B1%5D.mp4",
                Fcp7XmlWriter.PathUrl("D:\\BERNARD\\UNITY PROJECTS (GAMES)\\x [1].mp4"));

        [Test]
        public void Audio_Tracks_InOrderVoiceMusicSfx_AndFilesReused()
        {
            var doc = XDocument.Parse(Fcp7XmlWriter.Write(Plan(), "T", "D:/cut/draft01"));
            var tracks = doc.Descendants("audio").First(a => a.Parent?.Name == "media").Elements("track").ToList();
            Assert.AreEqual(3, tracks.Count);
            Assert.AreEqual("welcome-03", tracks[0].Element("clipitem").Element("name").Value);
            var music = tracks[1].Elements("clipitem").ToList();
            Assert.AreEqual(2, music.Count);
            Assert.AreEqual(Fcp7XmlWriter.Frames(15.5f, 30).ToString(), music[1].Element("start").Value); // 7.5 + 10 - 2 crossfade
            Assert.IsNull(music[1].Element("file").Element("pathurl"), "the second use of a file only references its id");
            Assert.AreEqual("open", tracks[2].Element("clipitem").Element("name").Value);
        }
    }

    public class QualityOverrideTests
    {
        float _lod;
        int _mip;

        [SetUp]
        public void SetUp()
        {
            QualityOverride.Restore();
            _lod = QualitySettings.lodBias;
            _mip = QualitySettings.globalTextureMipmapLimit;
        }

        [TearDown]
        public void TearDown()
        {
            QualityOverride.Restore();
            QualitySettings.lodBias = _lod;
            QualitySettings.globalTextureMipmapLimit = _mip;
        }

        static KionjoQualityOverride Q() => new() { enabled = true, lodBias = 7.5f, textureMipLimit = 0 };

        [Test]
        public void Apply_ThenRestore_RoundTrips()
        {
            QualitySettings.globalTextureMipmapLimit = 1;
            QualityOverride.Apply(Q());
            Assert.AreEqual(7.5f, QualitySettings.lodBias, 1e-4);
            Assert.AreEqual(0, QualitySettings.globalTextureMipmapLimit);
            QualityOverride.Restore();
            Assert.AreEqual(_lod, QualitySettings.lodBias, 1e-4);
            Assert.AreEqual(1, QualitySettings.globalTextureMipmapLimit);
        }

        [Test]
        public void Apply_Twice_RestoresOriginal()
        {
            QualityOverride.Apply(Q());
            QualityOverride.Apply(new KionjoQualityOverride { enabled = true, lodBias = 3f });
            QualityOverride.Restore();
            Assert.AreEqual(_lod, QualitySettings.lodBias, 1e-4);
            Assert.IsFalse(QualityOverride.IsApplied);
        }

        [Test]
        public void Disabled_ChangesNothing()
        {
            QualityOverride.Apply(new KionjoQualityOverride { enabled = false, lodBias = 9f });
            Assert.AreEqual(_lod, QualitySettings.lodBias, 1e-4);
            Assert.IsFalse(QualityOverride.IsApplied);
        }
    }

    public class CaptureAndAnimaticTests
    {
        string _dir;
        KionjoStoryboard _sb;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(_dir);
            _sb = ScriptableObject.CreateInstance<KionjoStoryboard>();
            _sb.outputFolder = _dir;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sb);
            Undo.ClearAll();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void Import_CopiesAndNumbers()
        {
            var shot = _sb.AddShot();
            shot.outputMode = KionjoOutputMode.DeviceCapture;
            var src = Path.Combine(_dir, "quest capture (1).mp4");
            File.WriteAllText(src, "x");
            var a = CaptureImporter.Import(_sb, shot, src);
            var b = CaptureImporter.Import(_sb, shot, src);
            Assert.AreEqual("S01_capture01.mp4", Path.GetFileName(a));
            Assert.AreEqual("S01_capture02.mp4", Path.GetFileName(b));
            Assert.AreEqual(b, shot.deviceCapturePath);
        }

        [Test]
        public void Index_ListsEveryCapture()
        {
            var shot = _sb.AddShot();
            shot.notes = "the take with the elephant";
            var src = Path.Combine(_dir, "c.mp4");
            File.WriteAllText(src, "x");
            CaptureImporter.Import(_sb, shot, src);
            var md = File.ReadAllText(CaptureImporter.WriteIndex(_sb, new FfprobeMediaProbe()));
            StringAssert.Contains("| S01 | S01_capture01.mp4 |", md);
            StringAssert.Contains("the take with the elephant", md);
        }

        [Test]
        public void Animatic_Builds_FromThumbnailsAndTextCards()
        {
            var ffmpeg = FfmpegLocator.FindFfmpeg();
            if (ffmpeg == null) Assert.Ignore("ffmpeg is not installed on this machine");
            var a = _sb.AddShot();
            a.targetSeconds = 1.5f;
            var tex = new Texture2D(64, 36);
            var thumb = Path.Combine(_dir, "thumb.png");
            File.WriteAllBytes(thumb, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            a.thumbnailPath = thumb;
            var b = _sb.AddShot();
            b.name = "Crane";
            b.targetSeconds = 1f;

            var plan = CutPlanBuilder.Build(_sb, KionjoCutPreset.Preview720p30, new FfprobeMediaProbe(), animatic: true);
            Assert.AreEqual(SegmentKind.Image, plan.Segments[0].Kind);
            Assert.AreEqual(SegmentKind.Text, plan.Segments[1].Kind);
            var folder = KionjoPaths.NextNumbered(KionjoPaths.Animatics(_sb), "animatic");
            var job = new CutJob(plan, folder, ffmpeg);
            Assert.IsTrue(job.RunAsync(CancellationToken.None).Wait(120000));
            Assert.IsFalse(job.Failed, job.Error);
            Assert.AreEqual(2.5f, new FfprobeMediaProbe().DurationSeconds(job.OutputMp4), 0.2f);
            Assert.IsTrue(File.Exists(job.OutputXml), "the Premiere timeline is written beside the draft");
            StringAssert.Contains("animatic01", job.OutputMp4);
        }
    }
}
