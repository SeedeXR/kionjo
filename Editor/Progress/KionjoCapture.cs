using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Capture at the moment: films a step while its version still exists, from several frame-locked
    /// angles, into the footage folder, and records the files in the step's journal entry. A step that
    /// was superseded is dropped, a machine without room keeps it pending, a scene without a camera or
    /// subject fails cleanly. The Play mode part survives the domain reload through SessionState.
    /// </summary>
    public static class KionjoCapture
    {
        // Test seams.
        internal static Func<(double, double)?> FreeMemory = () => KionjoResources.Free();
        internal static Func<string> FootageRoot = () => KionjoPreferences.FootageRoot;
        internal static Func<string> LockFolder = () => KionjoPreferences.LockFolder;

        const string StateKey = "SeedeXR.Kionjo.Capture", ResultKey = "SeedeXR.Kionjo.Capture.Result";
        static readonly string[] DefaultEncoder = { "-c:v", "libx264", "-crf", "16", "-preset", "medium" };
        static Action<KionjoCaptureRequest> _done;

        [Serializable]
        class EntryRef
        {
            public string entry;
        }

        /// <summary>Carried across the Play mode domain reload.</summary>
        [Serializable]
        class Pending
        {
            public string entryFolder, subjectPath, cameraPath, outFolder, baseName, resultPath, lockFolder, ffmpeg;
            public string[] encoderArgs;
            public bool batch;
            public KionjoCaptureRequest request;
        }

        [Serializable]
        class ResultFile
        {
            public string id, state, reason;
            public List<string> outputs = new();
            public int pid;
        }

        /// <summary>A request file: { "entry": "&lt;entry folder&gt;", ...request fields }.</summary>
        public static KionjoCaptureRequest Parse(string json, out string entryFolder)
        {
            entryFolder = JsonUtility.FromJson<EntryRef>(json)?.entry;
            var r = JsonUtility.FromJson<KionjoCaptureRequest>(json) ?? new KionjoCaptureRequest();
            r.rigs ??= new List<string>();
            r.outputs ??= new List<string>();
            return r;
        }

        /// <summary>
        /// Runs the request for the entry in entryFolder. done is called at once when the request ends in
        /// edit mode (dropped, pending, failed); a capture ends after Play mode, in the entry (and the
        /// result file of a batch run), and calls done only when no domain reload happened in between.
        /// </summary>
        public static void Run(string entryFolder, KionjoCaptureRequest r, Action<KionjoCaptureRequest> done) => Run(entryFolder, r, done, null, false);

        static void Run(string entryFolder, KionjoCaptureRequest r, Action<KionjoCaptureRequest> done, string resultPath, bool exitWhenDone)
        {
            var requestedId = r.id;
            if (string.IsNullOrEmpty(r.id)) r.id = Guid.NewGuid().ToString("N").Substring(0, 8);
            if (r.rigs == null || r.rigs.Count == 0) r.rigs = KionjoRigs.All.ToList();
            void End(string state, string reason)
            {
                r.state = state;
                r.reason = reason;
                Record(entryFolder, r, null);
                WriteResult(resultPath, r);
                done?.Invoke(r);
                if (exitWhenDone) EditorApplication.Exit(state == KionjoCaptureRequest.States.Taken ? 0 : 1);
            }

            var entry = KionjoJournal.Load(entryFolder ?? "", out var error);
            if (entry == null)
            {
                r.state = KionjoCaptureRequest.States.Failed;
                r.reason = error ?? "No journal entry.";
                WriteResult(resultPath, r);
                done?.Invoke(r);
                if (exitWhenDone) EditorApplication.Exit(1);
                return;
            }
            if (string.IsNullOrEmpty(requestedId))
            {
                // A retried request (no id in the file) continues the pending item it left, instead of adding a second one.
                var waiting = entry.captures.FirstOrDefault(c => c.state == KionjoCaptureRequest.States.Pending
                    && string.Equals(c.label ?? "", r.label ?? "") && string.Equals(c.scene ?? "", r.scene ?? ""));
                if (waiting != null) r.id = waiting.id;
            }
            var all = KionjoJournal.LoadAll(Path.GetDirectoryName(entry.Folder), null);
            var self = all.FirstOrDefault(e => e.Folder == entry.Folder) ?? entry;
            if (!KionjoBacklog.IsCurrent(self, all))
            {
                End(KionjoCaptureRequest.States.Dropped, $"A later step of {entry.feature} exists; this version is gone.");
                return;
            }

            double need = 2.5 + 0.15 * r.rigs.Count;
            if (!KionjoResources.CanRun(need, KionjoPreferences.FloorGB, FreeMemory(), out var room))
            {
                End(KionjoCaptureRequest.States.Pending, $"Waiting for room: {room}.");
                return;
            }
            var lockFolder = LockFolder();
            var heavy = HeavyLock.TryAcquire(lockFolder, $"Kionjo capture {entry.feature ?? entry.title} {r.label}".Trim(), need, out var holder);
            if (heavy == null)
            {
                End(KionjoCaptureRequest.States.Pending, $"Waiting for the heavy-work lock: {holder}.");
                return;
            }

            // From here on the lock is held: every way out releases it, records the request and (batch) exits.
            try
            {
                string fail = Prepare(r, out var subject, out var cam, out var ffmpeg);
                if (fail != null)
                {
                    heavy.Dispose();
                    End(KionjoCaptureRequest.States.Failed, fail);
                    return;
                }

                ReadVersion(r);
                var root = FootageRoot();
                if (string.IsNullOrWhiteSpace(root)) root = Path.Combine(KionjoPaths.Root(KionjoPostsSettings.instance.outputFolder), "captures");
                var day = FootageNotes.DayFolder(root, DateTime.Today);
                var pending = new Pending
                {
                    entryFolder = entry.Folder,
                    subjectPath = PathOf(subject.transform),
                    cameraPath = PathOf(cam.transform),
                    outFolder = day,
                    baseName = FreeBaseName(day, BaseName(entry, r)),
                    resultPath = resultPath,
                    lockFolder = lockFolder,
                    ffmpeg = ffmpeg,
                    encoderArgs = KionjoPreferences.EncoderArgs(DefaultEncoder),
                    batch = exitWhenDone,
                    request = r,
                };
                // Not disposed here: the lock outlives the domain reload as a file owned by this process; Finish releases it.
                r.reason = "Capturing";
                Record(entry.Folder, r, null);
                _done = done;
                SessionState.SetString(StateKey, JsonUtility.ToJson(pending));
                SessionState.EraseString(ResultKey);
                EditorApplication.EnterPlaymode();
                WatchPlayStart();
            }
            catch (Exception ex)
            {
                SessionState.EraseString(StateKey);
                heavy.Dispose();
                End(KionjoCaptureRequest.States.Failed, $"The capture could not start: {ex.Message}");
            }
        }

        /// <summary>The footage base name, made safe for any file system: feature (or title) and label, slugged.</summary>
        internal static string BaseName(JournalEntry entry, KionjoCaptureRequest r)
        {
            var feature = KionjoJournal.Slug(string.IsNullOrWhiteSpace(entry.feature) ? entry.title : entry.feature);
            var label = string.IsNullOrWhiteSpace(r.label) ? "take" : KionjoJournal.Slug(r.label);
            return $"{feature}-{label}";
        }

        /// <summary>
        /// Play mode can be refused (compile errors, another tool): then no domain reload comes, the capture
        /// would stay armed for the next manual Play session and keep the lock. Give up after a few seconds.
        /// </summary>
        static void WatchPlayStart()
        {
            double until = EditorApplication.timeSinceStartup + 10.0;
            void Check()
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetString(StateKey, "").Length == 0)
                {
                    EditorApplication.update -= Check;
                    return;
                }
                if (EditorApplication.timeSinceStartup < until) return;
                EditorApplication.update -= Check;
                var json = SessionState.GetString(StateKey, "");
                Store(new KionjoMultiCaptureResult { error = "Play mode did not start (compile errors?)." });
                Finish(JsonUtility.FromJson<Pending>(json));
            }
            EditorApplication.update += Check;
        }

        /// <summary>Opens the scene and finds the camera, the subject and ffmpeg; the reason when one is missing.</summary>
        static string Prepare(KionjoCaptureRequest r, out GameObject subject, out Camera cam, out string ffmpeg)
        {
            subject = null;
            cam = null;
            ffmpeg = FfmpegLocator.FindFfmpeg();
            if (ffmpeg == null) return "ffmpeg was not found (Preferences > Kionjo).";
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "The editor is in Play mode; capture starts from edit mode.";
            var active = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(r.scene) && active.path != r.scene)
            {
                if (!File.Exists(Path.Combine(KionjoPaths.ProjectRoot, r.scene))) return $"No scene at {r.scene}.";
                if (SceneManager.GetActiveScene().isDirty && !Application.isBatchMode) return "The open scene has unsaved changes: save it first.";
                active = EditorSceneManager.OpenScene(r.scene, OpenSceneMode.Single);
            }
            var objects = active.GetRootGameObjects();
            var cameras = objects.SelectMany(g => g.GetComponentsInChildren<Camera>()).Where(c => c.enabled && c.gameObject.activeInHierarchy).ToList();
            cam = cameras.FirstOrDefault(c => c.CompareTag("MainCamera")) ?? cameras.FirstOrDefault();
            if (cam == null) return "The scene has no camera to film from.";
            if (!string.IsNullOrWhiteSpace(r.subject))
            {
                subject = GameObject.Find(r.subject.StartsWith("/") ? r.subject : "/" + r.subject);
                if (subject == null) return $"No subject at {r.subject} in the scene.";
                return null;
            }
            subject = objects.SelectMany(g => g.GetComponentsInChildren<Renderer>()).Where(x => x.enabled)
                .OrderByDescending(x => x.bounds.extents.sqrMagnitude).Select(x => x.gameObject).FirstOrDefault();
            return subject == null ? "The scene has nothing to film (no renderer) and the request names no subject." : null;
        }

        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (; t != null; t = t.parent) parts.Insert(0, t.name);
            return "/" + string.Join("/", parts);
        }

        /// <summary>base, or base-2, base-3...: earlier steps' footage is never overwritten.</summary>
        static string FreeBaseName(string day, string name)
        {
            var b = name;
            for (int n = 2; Directory.Exists(day) && Directory.GetFiles(day, b + "-*.mp4").Length > 0; n++) b = $"{name}-{n}";
            return b;
        }

        static void ReadVersion(KionjoCaptureRequest r)
        {
            var branch = Git("rev-parse --abbrev-ref HEAD");
            var commit = Git("rev-parse --short HEAD");
            if (branch == null || commit == null)
            {
                Debug.LogWarning("Kionjo: git not found or not a repository; the capture has no version.");
                return;
            }
            r.version = $"{branch.Trim()}@{commit.Trim()}";
            r.dirty = !string.IsNullOrWhiteSpace(Git("status --porcelain"));
        }

        static string Git(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("git", args)
                {
                    WorkingDirectory = KionjoPaths.ProjectRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var p = Process.Start(psi);
                var output = p.StandardOutput.ReadToEndAsync();
                if (!p.WaitForExit(10000)) return null;
                return p.ExitCode == 0 ? output.Result : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The request's state in the entry (added when the entry does not hold it yet), plus media.</summary>
        static void Record(string entryFolder, KionjoCaptureRequest r, List<JournalMedia> media)
        {
            KionjoJournal.Update(entryFolder, e =>
            {
                int at = e.captures.FindIndex(c => c.id == r.id);
                if (at >= 0) e.captures[at] = r;
                else e.captures.Add(r);
                if (media == null) return;
                foreach (var m in media)
                    if (!e.media.Any(x => string.Equals(x.path, m.path, StringComparison.OrdinalIgnoreCase))) e.media.Add(m);
            });
        }

        static void WriteResult(string resultPath, KionjoCaptureRequest r)
        {
            if (string.IsNullOrEmpty(resultPath)) return;
            var result = new ResultFile { id = r.id, state = r.state, reason = r.reason, outputs = r.outputs ?? new List<string>(), pid = Process.GetCurrentProcess().Id };
            File.WriteAllText(resultPath, JsonUtility.ToJson(result, true), new UTF8Encoding(false));
        }

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.playModeStateChanged += OnPlayMode;

        static void OnPlayMode(PlayModeStateChange change)
        {
            var json = SessionState.GetString(StateKey, "");
            if (json.Length == 0) return;
            var p = JsonUtility.FromJson<Pending>(json);
            if (change == PlayModeStateChange.EnteredPlayMode) StartInPlay(p);
            else if (change == PlayModeStateChange.EnteredEditMode) Finish(p);
        }

        static void StartInPlay(Pending p)
        {
            var subject = GameObject.Find(p.subjectPath);
            var cam = GameObject.Find(p.cameraPath)?.GetComponent<Camera>();
            if (subject == null || cam == null)
            {
                Store(new KionjoMultiCaptureResult { error = subject == null ? $"The subject {p.subjectPath} is not there in Play mode." : $"The camera {p.cameraPath} is not there in Play mode." });
                EditorApplication.ExitPlaymode();
                return;
            }
            var host = new GameObject("Kionjo capture").AddComponent<KionjoMultiCapture>();
            host.subject = subject.transform;
            host.main = cam;
            host.rigs = p.request.rigs.ToArray();
            host.seconds = p.request.seconds;
            host.fps = p.request.fps;
            host.stillsEvery = p.request.stills;
            host.outFolder = p.outFolder;
            host.baseName = p.baseName;
            host.ffmpeg = p.ffmpeg;
            host.encoderArgs = p.encoderArgs;
            host.Done += result =>
            {
                Store(result);
                EditorApplication.ExitPlaymode();
            };
            try
            {
                host.Begin();
            }
            catch (Exception ex)
            {
                Store(new KionjoMultiCaptureResult { error = ex.Message });
                EditorApplication.ExitPlaymode();
            }
        }

        static void Store(KionjoMultiCaptureResult result) => SessionState.SetString(ResultKey, JsonUtility.ToJson(result));

        /// <summary>Back in edit mode: notes, entry media, state, lock, result file, and the exit of a batch run.</summary>
        static void Finish(Pending p)
        {
            SessionState.EraseString(StateKey);
            var json = SessionState.GetString(ResultKey, "");
            SessionState.EraseString(ResultKey);
            var result = json.Length > 0 ? JsonUtility.FromJson<KionjoMultiCaptureResult>(json) : new KionjoMultiCaptureResult { error = "Play mode ended before the capture finished." };
            var r = p.request;
            // Whatever throws below (a locked notes.md, a full disk), the lock is released, the result file
            // written and a batch editor exits: a capture never leaves an editor or a lock behind.
            try
            {
                var media = new List<JournalMedia>();
                r.outputs = new List<string>();
                foreach (var video in result.videos)
                {
                    var rig = Path.GetFileNameWithoutExtension(video).Substring(p.baseName.Length + 1);
                    FootageNotes.Append(p.outFolder, Path.GetFileName(video),
                        string.Format(CultureInfo.InvariantCulture, "{0} angle of {1}, {2:0.#} s, frame-locked with the other angles", rig, string.IsNullOrWhiteSpace(r.label) ? p.baseName : r.label, r.seconds));
                    media.Add(new JournalMedia { path = video, role = rig == "main" ? "hook" : "broll", note = $"{rig} angle, captured by Kionjo" });
                    r.outputs.Add(video);
                }
                foreach (var still in result.stills)
                {
                    media.Add(new JournalMedia { path = still, role = "still" });
                    r.outputs.Add(still);
                }
                r.state = result.ok ? KionjoCaptureRequest.States.Taken : KionjoCaptureRequest.States.Failed;
                r.reason = result.ok ? (result.warnings.Count > 0 ? string.Join(" ", result.warnings) : null) : result.error;
                Record(p.entryFolder, r, media);
            }
            catch (Exception ex)
            {
                r.state = KionjoCaptureRequest.States.Failed;
                r.reason = $"The capture could not be recorded: {ex.Message}";
                Debug.LogException(ex);
            }
            finally
            {
                try { HeavyLock.ReleaseOwn(p.lockFolder); } catch (Exception ex) { Debug.LogException(ex); }
                try { WriteResult(p.resultPath, r); } catch (Exception ex) { Debug.LogException(ex); }
                Debug.Log($"Kionjo: capture {r.state}: {r.reason ?? string.Join(", ", result.videos.Select(Path.GetFileName))}");
                var done = _done;
                _done = null;
                try { done?.Invoke(r); } catch (Exception ex) { Debug.LogException(ex); }
                if (p.batch) EditorApplication.Exit(r.state == KionjoCaptureRequest.States.Taken ? 0 : 1);
            }
        }

        /// <summary>
        /// Batch entry point: Unity -batchmode -projectPath P -executeMethod
        /// SeedeXR.Kionjo.EditorTools.KionjoCapture.RunFromCommandLine -kionjoCapture request.json
        /// (no -quit: the editor exits itself). Writes request.json.result.json { state, reason, outputs, pid }.
        /// </summary>
        public static void RunFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-kionjoCapture");
            if (at < 0 || at + 1 >= args.Length || !File.Exists(args[at + 1]))
            {
                Debug.LogError("Kionjo: -kionjoCapture <request.json> is missing or not found.");
                EditorApplication.Exit(2);
                return;
            }
            var file = args[at + 1];
            var r = Parse(File.ReadAllText(file), out var entry);
            Run(entry, r, null, file + ".result.json", exitWhenDone: true);
        }

        /// <summary>Starts a capture from a request file (the agent command); the state is in the entry and the result file.</summary>
        public static KionjoCaptureRequest RunFile(string requestFile)
        {
            var r = Parse(File.ReadAllText(requestFile), out var entry);
            Run(entry, r, null, requestFile + ".result.json", exitWhenDone: false);
            return r;
        }
    }
}
