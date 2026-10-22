using System;
using System.IO;
using UnityEngine;

namespace SeedeXR.Kionjo
{
    /// <summary>A whole take in memory: meta, frame times and per track poses.</summary>
    public sealed class KionjoTakeData
    {
        public KionjoTakeMeta Meta;
        public float[] Times = Array.Empty<float>();
        public KionjoPoseSample[][] Tracks = Array.Empty<KionjoPoseSample[]>(); // [track][frame]

        public float Duration => Times.Length == 0 ? 0f : Times[^1];

        public int TrackIndex(string name)
        {
            if (Meta == null) return -1;
            for (int i = 0; i < Meta.tracks.Count; i++)
                if (Meta.tracks[i].name == name) return i;
            return -1;
        }

        public static KionjoTakeData FromArrays(KionjoTakeMeta meta, float[] times, KionjoPoseSample[][] tracks) =>
            new() { Meta = meta, Times = times, Tracks = tracks };

        public static int FrameBytes(KionjoTakeMeta meta, int trackCount)
        {
            int bytes = 4;
            for (int i = 0; i < trackCount; i++)
            {
                bool scale = meta != null && i < meta.tracks.Count && meta.tracks[i].recordScale;
                bytes += 1 + 7 * 4 + (scale ? 3 * 4 : 0);
            }
            return bytes;
        }

        /// <summary>
        /// Reads take.json and frames.bin. Tolerates a missing frames file (zero frames) and a file
        /// cut short mid frame (whole frames only, count derived from the length).
        /// </summary>
        public static KionjoTakeData Load(string takeFolder)
        {
            var meta = KionjoTakeMeta.Load(takeFolder) ?? throw new FileNotFoundException($"No {KionjoTakeMeta.MetaFile} in {takeFolder}");
            var data = new KionjoTakeData { Meta = meta };
            var framesPath = Path.Combine(takeFolder, KionjoTakeMeta.FramesFile);
            int trackCount = meta.tracks.Count;
            if (!File.Exists(framesPath))
            {
                data.Tracks = NewTracks(trackCount, 0);
                return data;
            }

            using var stream = new FileStream(framesPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16);
            using var reader = new BinaryReader(stream);
            if (stream.Length < KionjoTakeWriter.HeaderBytes)
            {
                data.Tracks = NewTracks(trackCount, 0);
                return data;
            }
            var magic = reader.ReadBytes(4);
            for (int i = 0; i < 4; i++)
                if (magic[i] != KionjoTakeWriter.Magic[i]) throw new InvalidDataException($"{framesPath} is not a Kionjo take");
            reader.ReadUInt16(); // version
            int fileTracks = reader.ReadUInt16();
            uint headerCount = reader.ReadUInt32();
            if (fileTracks != trackCount) trackCount = fileTracks;

            int frameBytes = FrameBytes(meta, trackCount);
            long fit = (stream.Length - KionjoTakeWriter.HeaderBytes) / frameBytes;
            int count = headerCount > 0 && headerCount <= fit ? (int)headerCount : (int)fit;

            data.Times = new float[count];
            data.Tracks = NewTracks(trackCount, count);
            for (int f = 0; f < count; f++)
            {
                data.Times[f] = reader.ReadSingle();
                for (int k = 0; k < trackCount; k++)
                {
                    byte flags = reader.ReadByte();
                    var s = new KionjoPoseSample
                    {
                        active = (flags & 1) != 0,
                        position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                        rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                        scale = Vector3.one,
                    };
                    if (k < meta.tracks.Count && meta.tracks[k].recordScale)
                    {
                        s.scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        s.hasScale = true;
                    }
                    data.Tracks[k][f] = s;
                }
            }
            return data;
        }

        static KionjoPoseSample[][] NewTracks(int tracks, int frames)
        {
            var result = new KionjoPoseSample[tracks][];
            for (int i = 0; i < tracks; i++) result[i] = new KionjoPoseSample[frames];
            return result;
        }
    }
}
