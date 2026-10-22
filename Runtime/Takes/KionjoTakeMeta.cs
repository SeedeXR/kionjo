using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SeedeXR.Kionjo
{
    [Serializable]
    public class KionjoTrackInfo
    {
        public string name;
        public string path;
        public bool recordScale;
    }

    [Serializable]
    public class KionjoTakeEvent
    {
        public float t;
        public string name;
        public string payload;
    }

    /// <summary>The small, human readable half of a take (take.json). Poses live in frames.bin.</summary>
    [Serializable]
    public class KionjoTakeMeta
    {
        public const string MetaFile = "take.json", FramesFile = "frames.bin", ThumbFile = "thumb.png", AudioFile = "audio.wav";

        public int version = 1;
        public string shotId;
        public string shotCode;
        public int takeNumber;
        public string startedAt;
        public float durationSeconds;
        public int frameCount;
        public float nominalFps;
        public List<KionjoTrackInfo> tracks = new();
        public List<KionjoTakeEvent> events = new();
        public string notes;

        /// <summary>Null when the folder has no take.json.</summary>
        public static KionjoTakeMeta Load(string takeFolder)
        {
            var path = Path.Combine(takeFolder, MetaFile);
            return File.Exists(path) ? JsonUtility.FromJson<KionjoTakeMeta>(File.ReadAllText(path)) : null;
        }

        public void Save(string takeFolder)
        {
            Directory.CreateDirectory(takeFolder);
            File.WriteAllText(Path.Combine(takeFolder, MetaFile), JsonUtility.ToJson(this, true));
        }
    }
}
