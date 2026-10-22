using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace SeedeXR.Kionjo.Tests
{
    public class TakeFormatTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        static KionjoTakeMeta Meta(bool scaleOnSecond = false) => new()
        {
            shotId = "id",
            shotCode = "S01",
            takeNumber = 1,
            tracks = new List<KionjoTrackInfo>
            {
                new() { name = "Head", path = "/Rig/Head" },
                new() { name = "LeftHand", path = "/Rig/LeftHand", recordScale = scaleOnSecond },
                new() { name = "RightHand", path = "/Rig/RightHand" },
            },
        };

        static KionjoPoseSample[] Frame(int f) => new[]
        {
            new KionjoPoseSample { active = true, position = new Vector3(f, 1.6f, -f), rotation = Quaternion.Euler(0, f * 10, 0), scale = Vector3.one },
            new KionjoPoseSample { active = f % 2 == 0, position = new Vector3(-0.2f, f * 0.1f, 0.3f), rotation = Quaternion.Euler(f, 0, 0), scale = new Vector3(f, 2, 3) },
            new KionjoPoseSample { active = true, position = new Vector3(0.2f, 1, f), rotation = Quaternion.identity, scale = Vector3.one },
        };

        void WriteFrames(KionjoTakeMeta meta, int frames)
        {
            using var w = new KionjoTakeWriter(_dir, meta);
            for (int f = 0; f < frames; f++) w.WriteFrame(f / 90f, Frame(f));
            w.Close(frames / 90f);
        }

        [Test]
        public void RoundTrip_ThreeTracks_TenFrames()
        {
            WriteFrames(Meta(scaleOnSecond: true), 10);
            var data = KionjoTakeData.Load(_dir);
            Assert.AreEqual(10, data.Times.Length);
            for (int f = 0; f < 10; f++)
            {
                var expected = Frame(f);
                Assert.AreEqual(f / 90f, data.Times[f], 1e-6);
                for (int k = 0; k < 3; k++)
                {
                    var s = data.Tracks[k][f];
                    Assert.AreEqual(expected[k].active, s.active);
                    Assert.Less(Vector3.Distance(expected[k].position, s.position), 1e-6f);
                    Assert.Less(Quaternion.Angle(expected[k].rotation, s.rotation), 1e-3f);
                    Assert.AreEqual(k == 1, s.hasScale);
                }
                Assert.Less(Vector3.Distance(expected[1].scale, data.Tracks[1][f].scale), 1e-6f);
            }
        }

        [Test]
        public void Header_FrameCount_PatchedOnClose()
        {
            WriteFrames(Meta(), 7);
            var bytes = File.ReadAllBytes(Path.Combine(_dir, KionjoTakeMeta.FramesFile));
            Assert.AreEqual(7u, BitConverter.ToUInt32(bytes, 8));
            Assert.AreEqual(7, KionjoTakeMeta.Load(_dir).frameCount);
        }

        [Test]
        public void Load_TruncatedFile_DerivesFrameCountFromLength()
        {
            var meta = Meta();
            WriteFrames(meta, 10);
            var path = Path.Combine(_dir, KionjoTakeMeta.FramesFile);
            int frameBytes = KionjoTakeData.FrameBytes(meta, 3);
            using (var fs = new FileStream(path, FileMode.Open))
                fs.SetLength(KionjoTakeWriter.HeaderBytes + frameBytes * 3 + frameBytes / 2);
            var data = KionjoTakeData.Load(_dir);
            Assert.AreEqual(3, data.Times.Length);
        }

        [Test]
        public void Load_EmptyTake_ZeroFrames()
        {
            WriteFrames(Meta(), 0);
            var data = KionjoTakeData.Load(_dir);
            Assert.AreEqual(0, data.Times.Length);
            Assert.AreEqual(0f, data.Duration);
            Assert.AreEqual(3, data.Tracks.Length);
        }

        [Test]
        public void Meta_RoundTrip_KeepsEvents()
        {
            var meta = Meta();
            using (var w = new KionjoTakeWriter(_dir, meta))
            {
                w.WriteFrame(0, Frame(0));
                w.AddEvent(0.5f, "seal", "Vanijaliwa");
                w.Close(1f);
            }
            var loaded = KionjoTakeMeta.Load(_dir);
            Assert.AreEqual(1, loaded.events.Count);
            Assert.AreEqual("seal", loaded.events[0].name);
            Assert.AreEqual("Vanijaliwa", loaded.events[0].payload);
            Assert.AreEqual(0.5f, loaded.events[0].t, 1e-6);
            Assert.AreEqual("/Rig/LeftHand", loaded.tracks[1].path);
        }

        [Test]
        public void FrameSize_ThreeTracksNoScale_Is91Bytes()
        {
            WriteFrames(Meta(), 5);
            Assert.AreEqual(12 + 91 * 5, new FileInfo(Path.Combine(_dir, KionjoTakeMeta.FramesFile)).Length);
        }
    }
}
