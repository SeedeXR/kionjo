using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Renders one shot's selected take (or a live camera shot) in Play mode through a
    /// KionjoRenderHost, then writes a sidecar JSON and the shot's thumbnail from the middle frame.
    /// Runs on the main thread: it polls the host between editor frames.
    /// </summary>
    public sealed class RenderJob : KionjoJob
    {
        readonly KionjoStoryboard _sb;
        readonly KionjoShot _shot;
        readonly KionjoRenderPreset _preset;

        public string OutputMp4 { get; private set; }

        public RenderJob(KionjoStoryboard sb, KionjoShot shot, KionjoRenderPreset preset)
        {
            _sb = sb;
            _shot = shot;
            _preset = preset;
            Title = $"Render {shot.code}";
        }

        /// <summary>Why this shot cannot be rendered right now, or null when it can.</summary>
        public static string CannotRender(KionjoStoryboard sb, KionjoShot shot)
        {
            if (!EditorApplication.isPlaying) return "Enter Play mode to render.";
            if (FfmpegLocator.FindFfmpeg() == null) return "ffmpeg not found: set its path in Preferences > Kionjo.";
            if (shot.type == KionjoShotType.Card) return "Cards are drawn by the cut, there is nothing to render.";
            if (shot.outputMode != KionjoOutputMode.OfflineRender) return "This shot uses a device capture or is organise only.";
            if (shot.type == KionjoShotType.Performance && KionjoPaths.SelectedTakeFolder(sb, shot) == null) return "Record a take first.";
            if (KionjoCameraSource.Resolve() == null) return "No camera: add a Kionjo Camera Source or tag a camera MainCamera.";
            return null;
        }

        public static KionjoRenderRequest BuildRequest(KionjoStoryboard sb, KionjoShot shot, KionjoRenderPreset preset, string ffmpeg)
        {
            var video = KionjoPresets.Get(preset);
            var take = shot.type == KionjoShotType.Performance ? KionjoPaths.SelectedTakeFolder(sb, shot) : null;
            var name = take != null ? Path.GetFileName(take) : KionjoTakePaths.Sanitize(shot.code);
            return new KionjoRenderRequest
            {
                shotId = shot.id,
                shotCode = shot.code,
                takeFolder = take,
                from = 0f,
                to = -1f, // ponytail: whole take; range plus handles needs the cut to offset its in points
                liveSeconds = shot.targetSeconds,
                width = video.Width,
                height = video.Height,
                fps = video.Fps,
                encoderArgs = KionjoPreferences.EncoderArgs(video.EncoderArgs),
                ffmpegExe = ffmpeg,
                outputMp4 = KionjoPaths.RenderPath(sb, shot, name, preset),
                horizontalFov = shot.horizontalFov,
                hidePaths = new(shot.hidePaths),
                showPaths = new(shot.showPaths),
                stageCommand = shot.stageCommand,
            };
        }

        public override async Task RunAsync(CancellationToken ct)
        {
            var reason = CannotRender(_sb, _shot);
            if (reason != null) throw new InvalidOperationException(reason);
            var ffmpeg = FfmpegLocator.FindFfmpeg();
            var request = BuildRequest(_sb, _shot, _preset, ffmpeg);
            OutputMp4 = request.outputMp4;
            Status = $"Rendering {_shot.code} at {_preset}";
            AddLog(Status);

            var host = KionjoRenderHost.Create(request);
            try
            {
                while (!host.Done)
                {
                    if (ct.IsCancellationRequested || !EditorApplication.isPlaying)
                    {
                        host.Abort();
                        throw new OperationCanceledException();
                    }
                    Progress = host.Progress * 0.95f;
                    Status = $"Rendering {_shot.code}: frame {host.Frames} of {host.TargetFrames}";
                    await Task.Delay(100);
                }
                foreach (var w in host.Warnings) AddLog(w);
                if (host.Failed)
                {
                    Failed = true;
                    Error = host.Error;
                    Status = "Failed";
                    return;
                }
            }
            finally
            {
                if (host != null) Object.Destroy(host.gameObject);
            }

            WriteSidecar(request, host.TargetFrames);
            Status = "Thumbnail";
            var thumb = KionjoPaths.ThumbnailPath(_sb, _shot);
            Directory.CreateDirectory(Path.GetDirectoryName(thumb) ?? ".");
            float mid = host.TargetFrames / request.fps * 0.5f;
            var result = await KionjoProcess.RunAsync(new FfmpegCommand { Executable = ffmpeg }
                .Add("-v", "error", "-y", "-ss", mid.ToString("0.###", CultureInfo.InvariantCulture), "-i", request.outputMp4,
                    "-frames:v", "1", "-vf", "scale=480:-2", thumb), ct);
            if (result.ExitCode == 0)
            {
                Undo.RecordObject(_sb, "Shot thumbnail");
                _shot.thumbnailPath = thumb;
                EditorUtility.SetDirty(_sb);
                KionjoThumbnails.Forget(thumb);
            }
            else AddLog($"Thumbnail failed: {result.StdErrTail}");
            Progress = 1f;
            Status = $"Rendered {Path.GetFileName(request.outputMp4)}";
            AddLog(Status);
        }

        static void WriteSidecar(KionjoRenderRequest r, int frames)
        {
            var json = JsonUtility.ToJson(new Sidecar
            {
                take = r.takeFolder != null ? Path.GetFileName(r.takeFolder) : null,
                fps = r.fps, width = r.width, height = r.height, from = r.from, frames = frames,
                renderedAt = DateTime.UtcNow.ToString("o"),
            }, true);
            File.WriteAllText(Path.ChangeExtension(r.outputMp4, ".json"), json);
        }

        [Serializable]
        class Sidecar
        {
            public string take;
            public float fps;
            public int width, height;
            public float from;
            public int frames;
            public string renderedAt;
        }
    }
}
