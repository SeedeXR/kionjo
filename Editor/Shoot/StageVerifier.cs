using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    public enum StageSeverity { Info, Warning, Error }

    public sealed class StageReport
    {
        public readonly List<(StageSeverity Severity, string Text)> Lines = new();
        public bool Ready => Lines.All(l => l.Severity != StageSeverity.Error);
    }

    /// <summary>
    /// Checks a staged shot against the live scene: every hide path found and inactive, every show
    /// path found and active, a camera, a recorder with tracks, and no headset active for a replay.
    /// Trailer 1 taught this: a hide list that was silently empty put the wrong panels in a shot.
    /// </summary>
    public static class StageVerifier
    {
        public static StageReport Verify(KionjoShot shot, Func<string, GameObject> resolve, bool hasCamera,
            bool hasRecorderWithTracks, bool xrDeviceActive, bool forRender)
        {
            var report = new StageReport();
            foreach (var p in shot.hidePaths)
            {
                var go = resolve(p);
                if (go == null) report.Lines.Add((StageSeverity.Error, $"Hide: {p} not found"));
                else if (go.activeInHierarchy) report.Lines.Add((StageSeverity.Error, $"Hide: {p} is still active"));
            }
            foreach (var p in shot.showPaths)
            {
                var go = resolve(p);
                if (go == null) report.Lines.Add((StageSeverity.Error, $"Show: {p} not found"));
                else if (!go.activeInHierarchy) report.Lines.Add((StageSeverity.Error, $"Show: {p} is inactive"));
            }
            if (shot.hidePaths.Count == 0 && shot.showPaths.Count == 0)
                report.Lines.Add((StageSeverity.Info, "Hide list is empty: everything in the scene will be visible"));
            if (!hasCamera) report.Lines.Add((StageSeverity.Error, "No camera source: add a Kionjo Camera Source or tag a camera MainCamera"));
            if (!hasRecorderWithTracks)
            {
                bool liveRender = forRender && shot.type == KionjoShotType.LiveCamera;
                report.Lines.Add((liveRender ? StageSeverity.Warning : StageSeverity.Error, "No Kionjo Recorder with tracks in the scene"));
            }
            if (xrDeviceActive && forRender)
                report.Lines.Add((StageSeverity.Warning, "An XR device is active: unplug the headset for a replay or a render"));
            if (!string.IsNullOrEmpty(shot.stageCommand))
                report.Lines.Add((StageSeverity.Info, $"Stage command: {shot.stageCommand}"));
            return report;
        }
    }
}
