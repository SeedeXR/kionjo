using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// &lt;output folder&gt;/state.json: what Kionjo is doing right now (shots and their takes and
    /// renders, Play mode, recording, the running job). An agent or a script can watch this file
    /// instead of asking the editor. Written by the window, at most twice a second.
    /// </summary>
    public static class KionjoStateFile
    {
        public const string FileName = "state.json";

        [Serializable]
        public class ShotState
        {
            public string id, code, name, type, location, selectedTake, state;
            public int takes;
            public bool hasRender;
            public float plannedSeconds;
        }

        [Serializable]
        public class JobState
        {
            public string title, status, error;
            public float progress;
            public bool running, failed;
            public int queued;
        }

        [Serializable]
        public class State
        {
            public string storyboard, title, root, currentShot, updated;
            public float plannedSeconds, targetSeconds;
            public bool isPlaying, isRecording;
            public List<ShotState> shots = new();
            public JobState job = new();
        }

        public static State Build(KionjoStoryboard sb, string currentShotId = null)
        {
            var state = new State
            {
                storyboard = AssetDatabase.GetAssetPath(sb),
                title = sb.title,
                root = KionjoPaths.Root(sb),
                currentShot = currentShotId,
                plannedSeconds = sb.PlannedSeconds(),
                targetSeconds = sb.targetSeconds,
                isPlaying = EditorApplication.isPlaying,
                isRecording = ShootController.Recorder != null && ShootController.Recorder.IsRecording,
                updated = DateTime.UtcNow.ToString("o"),
            };
            foreach (var s in sb.shots)
            {
                int takes = 0;
                var folder = KionjoPaths.ShotTakes(sb, s);
                if (Directory.Exists(folder))
                    foreach (var d in Directory.GetDirectories(folder))
                        if (KionjoTakePaths.TryParseTakeNumber(Path.GetFileName(d), out _)) takes++;
                state.shots.Add(new ShotState
                {
                    id = s.id, code = s.code, name = s.name, type = s.type.ToString(), location = s.location,
                    selectedTake = s.selectedTake, takes = takes, hasRender = !s.IsCard && KionjoPaths.ExistingRender(sb, s) != null,
                    plannedSeconds = s.PlannedSeconds(), state = ShotCard.State(sb, s, out _),
                });
            }
            var job = KionjoJobs.Current ?? KionjoJobs.Last;
            if (job != null)
                state.job = new JobState
                {
                    title = job.Title, status = job.Status, error = job.Error, progress = job.Progress,
                    running = KionjoJobs.IsBusy, failed = job.Failed, queued = KionjoJobs.Queued,
                };
            return state;
        }

        static double _lastWrite;

        public static string Write(KionjoStoryboard sb, string currentShotId = null, bool force = false)
        {
            if (sb == null) return null;
            double now = EditorApplication.timeSinceStartup;
            if (!force && now - _lastWrite < 0.5) return null;
            _lastWrite = now;
            var root = KionjoPaths.Root(sb);
            try
            {
                Directory.CreateDirectory(root);
                var path = Path.Combine(root, FileName);
                File.WriteAllText(path, JsonUtility.ToJson(Build(sb, currentShotId), true));
                return path;
            }
            catch (IOException)
            {
                return null; // a reader holding the file: the next write catches up
            }
        }
    }
}
