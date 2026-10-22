using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// The Shoot tab's logic: stage and verify a shot, record takes with the scene's Kionjo
    /// Recorder, list, select, replay and delete takes, advance with Done.
    /// </summary>
    public sealed class ShootController
    {
        public struct TakeInfo
        {
            public string Folder, Name;
            public int Number;
            public float Duration;
            public int Events;
            public bool IsSelected;
        }

        public KionjoStoryboard Storyboard { get; }
        public KionjoShot Current { get; private set; }
        public List<TakeInfo> Takes { get; } = new();
        public StageReport Report { get; private set; }
        public bool IsStaging { get; private set; }
        public event Action Changed;

        KionjoReplayer _replayer;
        bool _ownsReplayer;

        public ShootController(KionjoStoryboard sb) => Storyboard = sb;

        public static KionjoRecorder Recorder =>
            KionjoRecorder.Active != null ? KionjoRecorder.Active : Object.FindFirstObjectByType<KionjoRecorder>();

        public bool IsRecording => Recorder != null && Recorder.IsRecording;
        public bool IsReplaying => _replayer != null && _replayer.IsPlaying;

        public void Select(KionjoShot shot)
        {
            Current = shot;
            Report = null;
            RefreshTakes();
            Arm();
            Changed?.Invoke();
        }

        /// <summary>Lets the game's own button call KionjoRecorder.ToggleTake for the selected shot.</summary>
        public void Arm()
        {
            var rec = Recorder;
            if (rec == null || Current == null || rec.IsRecording) return;
            var folder = KionjoPaths.ShotTakes(Storyboard, Current);
            int n = KionjoTakePaths.NextTakeNumber(folder);
            rec.PendingTakeFolder = Path.Combine(folder, KionjoTakePaths.TakeFolderName(Current.code, n));
            rec.PendingShotId = Current.id;
            rec.PendingShotCode = Current.code;
            rec.PendingTakeNumber = n;
        }

        public void RefreshTakes()
        {
            Takes.Clear();
            if (Current == null) return;
            var folder = KionjoPaths.ShotTakes(Storyboard, Current);
            if (!Directory.Exists(folder)) return;
            var selected = KionjoPaths.SelectedTakeFolder(Storyboard, Current);
            foreach (var dir in Directory.GetDirectories(folder))
            {
                var name = Path.GetFileName(dir);
                if (!KionjoTakePaths.TryParseTakeNumber(name, out int n)) continue;
                KionjoTakeMeta meta = null;
                try
                {
                    meta = KionjoTakeMeta.Load(dir);
                }
                catch (ArgumentException)
                {
                    // a damaged take.json still lists, with no length
                }
                Takes.Add(new TakeInfo
                {
                    Folder = dir, Name = name, Number = n,
                    Duration = meta?.durationSeconds ?? 0f,
                    Events = meta?.events.Count ?? 0,
                    IsSelected = selected != null && Path.GetFullPath(selected) == Path.GetFullPath(dir),
                });
            }
            Takes.Sort((a, b) => a.Number.CompareTo(b.Number));
        }

        public bool CanRecord(out string reason)
        {
            reason = null;
            if (!EditorApplication.isPlaying) reason = "Enter Play mode to record.";
            else if (Current == null) reason = "Select a shot.";
            else if (Current.type == KionjoShotType.Card) reason = "Cards are not recorded.";
            else if (Recorder == null) reason = "Add a Kionjo Recorder to your rig (Add Component > SeedeXR > Kionjo Recorder).";
            else if (Recorder.tracks.Count == 0) reason = "The Kionjo Recorder has no tracks: add the head camera and the hands.";
            return reason == null;
        }

        public void ToggleRecord()
        {
            if (!CanRecord(out var reason))
            {
                Debug.LogWarning($"Kionjo: {reason}");
                return;
            }
            var rec = Recorder;
            if (rec.IsRecording)
            {
                rec.StopTake();
                RefreshTakes();
                Arm();
            }
            else
            {
                StopReplay();
                Arm();
                rec.ToggleTake();
            }
            Changed?.Invoke();
        }

        public void SelectTake(TakeInfo t)
        {
            if (Current == null) return;
            Undo.RecordObject(Storyboard, "Select take");
            Current.selectedTake = t.Name;
            EditorUtility.SetDirty(Storyboard);
            RefreshTakes();
            Changed?.Invoke();
        }

        public void DeleteTake(TakeInfo t)
        {
            if (Directory.Exists(t.Folder)) FileUtil.DeleteFileOrDirectory(t.Folder);
            if (Current != null && Current.selectedTake == t.Name)
            {
                Undo.RecordObject(Storyboard, "Delete take");
                Current.selectedTake = null;
                EditorUtility.SetDirty(Storyboard);
            }
            RefreshTakes();
            Arm();
            Changed?.Invoke();
        }

        /// <summary>Plays a take back onto the recorder's transforms (headset unplugged).</summary>
        public void Replay(TakeInfo t)
        {
            if (!EditorApplication.isPlaying) return;
            StopReplay();
            _replayer = Object.FindFirstObjectByType<KionjoReplayer>();
            _ownsReplayer = _replayer == null;
            if (_ownsReplayer)
            {
                var go = new GameObject("Kionjo Replay") { hideFlags = HideFlags.DontSave };
                _replayer = go.AddComponent<KionjoReplayer>();
                var rec = Recorder;
                if (rec != null)
                    foreach (var tr in rec.tracks)
                        _replayer.tracks.Add(new KionjoTrack { name = tr.name, target = tr.target, recordScale = tr.recordScale });
                _replayer.lockFrameRate = false; // a replay runs in real time
            }
            _replayer.Finished += () => Changed?.Invoke();
            _replayer.Load(t.Folder);
            _replayer.Play();
            Changed?.Invoke();
        }

        public void StopReplay()
        {
            if (_replayer == null) return;
            _replayer.Stop();
            if (_ownsReplayer) Object.Destroy(_replayer.gameObject);
            _replayer = null;
            Changed?.Invoke();
        }

        /// <summary>Applies the hide and show lists, runs the stage handlers, then verifies.</summary>
        public void Stage()
        {
            if (!EditorApplication.isPlaying || Current == null || IsStaging) return;
            IsStaging = true;
            Changed?.Invoke();
            KionjoRunner.Run(StageRoutine(Current));
        }

        IEnumerator StageRoutine(KionjoShot shot)
        {
            KionjoStaging.Apply(shot.hidePaths, shot.showPaths);
            yield return KionjoStaging.RunHandlers(shot.id, shot.stageCommand);
            yield return null;
            IsStaging = false;
            Verify(forRender: false);
        }

        public void Verify(bool forRender)
        {
            if (Current == null) return;
            var rec = Recorder;
            Report = StageVerifier.Verify(Current, KionjoStaging.Resolve, KionjoCameraSource.Resolve() != null,
                rec != null && rec.tracks.Count > 0, XrDeviceActive(), forRender);
            Changed?.Invoke();
        }

        /// <summary>XRSettings.isDeviceActive through reflection: the VR module is optional in flat projects.</summary>
        public static bool XrDeviceActive()
        {
            var type = Type.GetType("UnityEngine.XR.XRSettings, UnityEngine.VRModule");
            var prop = type?.GetProperty("isDeviceActive");
            return prop != null && (bool)prop.GetValue(null);
        }

        /// <summary>Moves to the next shot that is not a card; false when this was the last.</summary>
        public bool Done()
        {
            if (Current == null) return false;
            int i = Storyboard.shots.IndexOf(Current);
            for (int j = i + 1; j < Storyboard.shots.Count; j++)
                if (Storyboard.shots[j].type != KionjoShotType.Card)
                {
                    Select(Storyboard.shots[j]);
                    return true;
                }
            return false;
        }
    }
}
