using System;
using System.IO;
using System.Text;

namespace SeedeXR.Kionjo
{
    /// <summary>
    /// Streams frames to frames.bin as they are recorded, so memory stays flat and a take that is
    /// cut short (Play mode exit, crash) is still readable up to its last whole frame.
    /// Layout (little endian): "KJTK", u16 version, u16 trackCount, u32 frameCount, then per frame
    /// f32 t and per track u8 flags (bit 0 active, bit 1 scale), 3 floats position, 4 floats
    /// rotation, 3 floats scale when the track records scale.
    /// </summary>
    public sealed class KionjoTakeWriter : IDisposable
    {
        public const int HeaderBytes = 12;
        public static readonly byte[] Magic = Encoding.ASCII.GetBytes("KJTK");

        readonly string _folder;
        readonly KionjoTakeMeta _meta;
        FileStream _stream;
        BinaryWriter _writer;

        public int FrameCount { get; private set; }
        public bool IsClosed => _writer == null;

        public KionjoTakeWriter(string takeFolder, KionjoTakeMeta meta)
        {
            _folder = takeFolder;
            _meta = meta;
            Directory.CreateDirectory(takeFolder);
            _meta.frameCount = 0;
            _meta.Save(takeFolder); // early save: a take cut short still has its meta
            _stream = new FileStream(Path.Combine(takeFolder, KionjoTakeMeta.FramesFile), FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16);
            _writer = new BinaryWriter(_stream);
            _writer.Write(Magic);
            _writer.Write((ushort)1);
            _writer.Write((ushort)meta.tracks.Count);
            _writer.Write(0u);
        }

        public void WriteFrame(float t, KionjoPoseSample[] samples)
        {
            if (_writer == null) throw new ObjectDisposedException(nameof(KionjoTakeWriter));
            if (samples.Length != _meta.tracks.Count)
                throw new ArgumentException($"Expected {_meta.tracks.Count} samples, got {samples.Length}");
            _writer.Write(t);
            for (int i = 0; i < samples.Length; i++)
            {
                var s = samples[i];
                bool scale = _meta.tracks[i].recordScale;
                _writer.Write((byte)((s.active ? 1 : 0) | (scale ? 2 : 0)));
                _writer.Write(s.position.x); _writer.Write(s.position.y); _writer.Write(s.position.z);
                _writer.Write(s.rotation.x); _writer.Write(s.rotation.y); _writer.Write(s.rotation.z); _writer.Write(s.rotation.w);
                if (scale) { _writer.Write(s.scale.x); _writer.Write(s.scale.y); _writer.Write(s.scale.z); }
            }
            FrameCount++;
        }

        public void AddEvent(float t, string name, string payload) =>
            _meta.events.Add(new KionjoTakeEvent { t = t, name = name, payload = payload });

        /// <summary>Patches the frame count into the header and saves take.json.</summary>
        public void Close(float durationSeconds)
        {
            if (_writer == null) return;
            _writer.Flush();
            _stream.Seek(8, SeekOrigin.Begin);
            _writer.Write((uint)FrameCount);
            _writer.Dispose();
            _writer = null;
            _stream = null;
            _meta.frameCount = FrameCount;
            _meta.durationSeconds = durationSeconds;
            if (_meta.nominalFps <= 0 && durationSeconds > 0) _meta.nominalFps = FrameCount / durationSeconds;
            _meta.Save(_folder);
        }

        public void Dispose() => Close(_meta.durationSeconds);
    }
}
