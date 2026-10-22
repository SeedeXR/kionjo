# Kionjo v0.1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task, natively, in one session (no human and no reviewer are available overnight). Steps use checkbox (`- [ ]`) syntax for tracking. Keep a progress ledger at `Documentation~/Design/progress.md` (git-ignored by adding it to `.gitignore` in Task 1) with one line per finished task: task number, verify result, commit hash, anything unverified.

**Goal:** A Unity 6000.0 editor package (`com.seedexr.kionjo`) that records and replays transform performances, renders them offline through ffmpeg, and auto-cuts a draft trailer (MP4 + SRT, later FCP7 XML) from a storyboard asset edited in a UI Toolkit window.

**Architecture:** Runtime assembly with the recorder, replayer, take format and frame capture (plain Transforms, no XR dependency). Editor assembly with the storyboard asset, the window, the render job, and a pure C# ffmpeg command builder (cut plan to argument lists). ffmpeg does all encoding and mixing; Kionjo never shells out through a command string. Agent bridge in a third assembly compiled only when `com.unity.pipeline` is present.

**Tech Stack:** Unity 6000.0.70f1 (floor and test editor), URP test project, UI Toolkit, NUnit via `com.unity.test-framework`, ffmpeg 7.1 on PATH, Python 3.12 (Pillow for icon export, faster-whisper optional), Unity CLI 1.0.0-beta.13.

**Spec:** `Documentation~/Design/Kionjo-Spec.md` (same folder). The plan argues from the spec; read both.

## Global Constraints

- Package root = repo root `D:\BERNARD\UNITY PROJECTS (GAMES)\kionjo` (call it `PKG`). Package id `com.seedexr.kionjo`, version `0.1.0`, `"unity": "6000.0"`, MIT, author SeedeXR.
- Test project `D:\BERNARD\UNITY PROJECTS (GAMES)\Kionjo-TestProject` (call it `TP`), URP, references the package with `"com.seedexr.kionjo": "file:../../kionjo"` and lists it in `testables`. Kionjo never needs Baraza's editor; do not touch the Baraza repo.
- Build and test only on editor 6000.0.70f1. Every `unity` call passes the project path explicitly (two editors may be open). `unity` is at `C:\Users\Seede Sr\AppData\Local\Unity\bin`; add it to PATH in every shell call.
- Namespaces: `SeedeXR.Kionjo` (Runtime), `SeedeXR.Kionjo.EditorTools` (Editor), `SeedeXR.Kionjo.Tests` (tests), `SeedeXR.Kionjo.Agent` (bridge). Asmdefs: `SeedeXR.Kionjo`, `SeedeXR.Kionjo.Editor`, `SeedeXR.Kionjo.Tests`, `SeedeXR.Kionjo.Editor.Tests`, `SeedeXR.Kionjo.Agent.Editor`.
- No hard dependency on Meta XR, OpenXR, XRI, Cinemachine, Recorder, Timeline or uGUI. Runtime asmdef has no references. Editor asmdef references only `SeedeXR.Kionjo`.
- Writing rule (hard): NO em-dashes anywhere (code comments, UI strings, docs, commit messages). Use a colon, a comma or parentheses. No AI attribution in commits; plain messages, no trailers.
- ffmpeg: every invocation is an `FfmpegCommand` (executable, argument list, working directory). Paths that appear inside a filtergraph are relative to the working directory. Never build a shell string for execution (only for `commands.txt`).
- Tests: `[Test]` in `Tests/Editor` for pure code, `[UnityTest]` in `Tests/Runtime` for MonoBehaviours. Test files write under `Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name)` and delete it in `[TearDown]`.
- Verify commands below are Git Bash. `UNITY` means `export PATH="$PATH:/c/Users/Seede Sr/AppData/Local/Unity/bin"; TP="D:/BERNARD/UNITY PROJECTS (GAMES)/Kionjo-TestProject"`. `unity test` exits 6 on failure, 0 on success, and writes NUnit XML; read the `total`/`passed`/`failed` attributes on the `<test-run>` element.
- `unity test` needs the test project's editor closed (`unity close "$TP" --force` first if `unity status` lists it). Open it again with `unity open "$TP"` only for the CLI smoke checks that say so.
- Commit after every task from `PKG` (`git add -A && git commit -m "<message>"`). First commit creates `main`. Do not push tonight.

## Review Focus

1. Paths with spaces, parentheses and brackets (Bernard's project path has all three): an ffmpeg segment whose source is `D:/BERNARD/UNITY PROJECTS (GAMES)/x [1].mp4` must appear as one argument, unquoted, in `FfmpegCommand.Args`, and the `subtitles=` filter must name `subs.srt` relatively. Test pinned in Task 9.
2. An empty or single-frame take (Space pressed twice): `KionjoTakeData.Load` returns zero or one frame without throwing, `KionjoReplayer.Play` finishes on the first frame, the Shoot tab lists the take with "0.0 s". Tests pinned in Tasks 2 and 5.
3. ffmpeg missing or exiting non-zero mid job: `CutJob` stops, keeps the folder, surfaces the last 20 stderr lines, no exception reaches `EditorApplication.update`. Test pinned in Task 11 with a bogus input path.
4. A storyboard with no usable source for some shots (no take, no render, no capture): `CutPlanBuilder` skips them with a warning each, still builds the plan, and a storyboard of only cards still cuts. Tests pinned in Tasks 8 and 11.
5. Play mode exit or domain reload during a record or a render: the recorder patches the frame count on `OnDestroy`, the reader tolerates a truncated `frames.bin`, `KionjoFrameCapture.OnDestroy` restores `Time.captureDeltaTime = 0` and the camera's `targetTexture`, and the ffmpeg process is killed. Tests pinned in Tasks 4 and 12.

---

## Phase 0: skeleton and test project

### Task 1: Package skeleton

**Files:**
- Create: `PKG/package.json`, `PKG/README.md`, `PKG/CHANGELOG.md`, `PKG/LICENSE.md`, `PKG/.gitignore`
- Create: `PKG/Runtime/SeedeXR.Kionjo.asmdef`, `PKG/Editor/SeedeXR.Kionjo.Editor.asmdef`
- Create: `PKG/Tests/Editor/SeedeXR.Kionjo.Editor.Tests.asmdef`, `PKG/Tests/Runtime/SeedeXR.Kionjo.Tests.asmdef`
- Create: `PKG/Tests/Editor/SmokeTests.cs`, `PKG/Runtime/KionjoVersion.cs`

**Interfaces:**
- Produces: `public static class KionjoVersion { public const string Value = "0.1.0"; }` in `SeedeXR.Kionjo`.

- [ ] **Step 1: Write the files**

`package.json` mirrors Hadithi's with name `com.seedexr.kionjo`, displayName `Kionjo`, version `0.1.0`, description "Trailer tool for Unity: storyboard, record a performance in the headset or with any camera, replay it frame locked, render offline, auto cut with ffmpeg.", `"unity": "6000.0"`, author `{ "name": "SeedeXR", "url": "https://github.com/SeedeXR" }`, license MIT, keywords `trailer, vr, capture, recorder, ffmpeg, storyboard`, and `"documentationUrl": "https://github.com/SeedeXR/kionjo"`.

Asmdefs (names above). Runtime: `references: []`, no platform limits. Editor: `references: ["SeedeXR.Kionjo"]`, `includePlatforms: ["Editor"]`. Editor tests: `references: ["SeedeXR.Kionjo", "SeedeXR.Kionjo.Editor", "UnityEngine.TestRunner", "UnityEditor.TestRunner"]`, `includePlatforms: ["Editor"]`, `precompiledReferences: ["nunit.framework.dll"]`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]`, `autoReferenced: false`. Runtime tests: same but references `["SeedeXR.Kionjo", "UnityEngine.TestRunner", "UnityEditor.TestRunner"]` and no platform limit. `rootNamespace` per the Global Constraints.

`LICENSE.md`: "MIT License", "Copyright (c) 2026 SeedeXR", standard text. `CHANGELOG.md`: Keep a Changelog header plus `## [0.1.0] - unreleased` with an empty `### Added`. `README.md`: title, one paragraph, "Install" (git URL `https://github.com/SeedeXR/kionjo.git`, requires Unity 6000.0 and ffmpeg on PATH), "Status: v0.1 in development". `.gitignore`: `Documentation~/Design/progress.md`, `*.log`, `test-results*.xml`.

`SmokeTests.cs`: `[Test] public void Version_IsSemver() => StringAssert.IsMatch(@"^\d+\.\d+\.\d+$", KionjoVersion.Value);`

- [ ] **Step 2: Create the test project**

Run:
```bash
UNITY; unity projects create Kionjo-TestProject --path "D:/BERNARD/UNITY PROJECTS (GAMES)" --editor-version 6000.0.70f1 --template com.unity.template.urp-blank --with-pipeline --no-cloud --non-interactive
```
Expected: exit 0, folder `TP` exists with `Packages/manifest.json` and `ProjectSettings/ProjectVersion.txt` saying `6000.0.70f1`. If the template id is rejected, run `unity templates list --editor 6000.0.70f1 --no-pager` and pick the "Universal 3D" CORE entry.

- [ ] **Step 3: Reference the package**

Edit `TP/Packages/manifest.json`: add `"com.seedexr.kionjo": "file:../../kionjo"` to `dependencies` and a top-level `"testables": ["com.seedexr.kionjo"]`. Confirm `com.unity.test-framework` is present (add `"com.unity.test-framework": "1.6.0"` if not).

- [ ] **Step 4: Run the smoke test**

Run: `UNITY; unity test "$TP" --mode EditMode --filter SeedeXR.Kionjo --output "$TP/test-results.xml" --timeout 900; echo exit=$?`
Expected: `exit=0`, XML `total="1" passed="1"`. If the import log shows the package was not found, check the `file:` path resolves from `TP/Packages/` (two levels up, then `kionjo`).

- [ ] **Step 5: Commit**

```bash
cd "$PKG" && git add -A && git commit -m "Package skeleton: manifest, assemblies, licence, first test"
```

### Task 2: Test project sample scene and fake rig

**Files:**
- Create: `TP/Assets/Kionjo Sample/FakeRigMover.cs`, `TP/Assets/Kionjo Sample/KionjoSample.unity` (built by an editor script, then saved)
- Create: `TP/Assets/Kionjo Sample/Editor/BuildSampleScene.cs`

**Interfaces:**
- Produces: a scene with `Head` (Camera, tag MainCamera, child of `Rig`), `LeftHand`, `RightHand` (cubes 0.08 m), a ground plane, a 2 m red cube at (0, 1, 3) and sky blue background, and `FakeRigMover` moving Head on a slow figure eight (`position = (sin(t*0.5)*0.3, 1.6, cos(t*0.25)*0.3)`, yaw `sin(t*0.3)*20` degrees) and hands on small circles. It calls `KionjoEvents.Mark("beep", t.ToString("F1"))` every 2 s. Nothing in the package depends on this scene; it is the headless stand-in for a headset.

- [ ] **Step 1: Write `FakeRigMover`** (`public Transform head, leftHand, rightHand; public bool move = true;` motion as above in `Update`).
- [ ] **Step 2: Write `BuildSampleScene`** with `[MenuItem("Tools/Kionjo Sample/Build Scene")]` and a `public static void Build()` that creates the objects, saves the scene to `Assets/Kionjo Sample/KionjoSample.unity`, and adds it to `EditorBuildSettings.scenes` (index 0).
- [ ] **Step 3: Build it headless**

Run: `UNITY; unity run "$TP" --timeout 600 -- -executeMethod BuildSampleScene.Build -quit` (if `unity run` does not forward `-executeMethod`, call the editor directly: `"C:/Program Files/Unity/Hub/Editor/6000.0.70f1/Editor/Unity.exe" -batchmode -quit -projectPath "$TP" -executeMethod BuildSampleScene.Build -logFile "$TP/build-scene.log"`).
Expected: exit 0, `TP/Assets/Kionjo Sample/KionjoSample.unity` exists, log contains no `error CS`.

- [ ] **Step 4: Commit** (the test project is not a git repo; nothing to commit here. Note the step in the ledger.)

---

## Tier 1: vertical slice

### Task 3: Naming and paths

**Files:**
- Create: `PKG/Runtime/Takes/KionjoTakePaths.cs`, `PKG/Editor/KionjoPaths.cs`
- Test: `PKG/Tests/Editor/TakePathsTests.cs`, `PKG/Tests/Editor/KionjoPathsTests.cs`

**Interfaces:**
- Produces (Runtime): `public static class KionjoTakePaths { public static string TakeFolderName(string shotCode, int take); /* "S10_take03" */ public static int NextTakeNumber(string shotFolder); /* highest existing + 1, 1 when none or missing */ public static string NewestTakeFolder(string shotFolder); /* null when none */ public static string Sanitize(string code); /* letters, digits, dash, underscore; others become "_" ; empty -> "shot" */ }`
- Produces (Editor): `public static class KionjoPaths { public static string ProjectRoot { get; } /* parent of Application.dataPath */ public static string Root(KionjoStoryboard sb); public static string ShotTakes(KionjoStoryboard sb, KionjoShot shot); public static string ShotRenders(...); public static string ShotCaptures(...); public static string Thumbs(KionjoStoryboard sb); public static string Cuts(KionjoStoryboard sb); public static string Animatics(KionjoStoryboard sb); public static string NextNumbered(string parent, string prefix); /* parent/prefix01.. */ public static string ThumbnailPath(KionjoStoryboard sb, KionjoShot shot); }` (the storyboard types arrive in Task 6; declare `KionjoPaths` with `string outputFolder, string shotCode` overloads now and add the storyboard overloads in Task 6).

- [ ] **Step 1: Write failing tests**: `TakeFolderName_PadsToTwoDigits` (`"S1", 3` gives `"S1_take03"`), `NextTakeNumber_MissingFolder_IsOne`, `NextTakeNumber_SkipsGaps` (folders take01, take05 give 6), `NewestTakeFolder_PicksHighestNumber`, `Sanitize_ReplacesSpacesAndSlashes` (`"S 07/a"` gives `"S_07_a"`), `NextNumbered_StartsAt01AndIncrements`.
- [ ] **Step 2: Run** `UNITY; unity test "$TP" --mode EditMode --filter SeedeXR.Kionjo --output "$TP/test-results.xml" --timeout 900` Expected: compile error or failures (classes missing).
- [ ] **Step 3: Implement** both classes. Take numbers parse the digits after `_take` with `int.TryParse`, ignoring folders that do not match.
- [ ] **Step 4: Run** the same command. Expected: exit 0, all tests passed.
- [ ] **Step 5: Commit** `"Take and output path helpers"`.

### Task 4: Take format (meta JSON + binary frames)

**Files:**
- Create: `PKG/Runtime/Takes/KionjoTakeMeta.cs`, `PKG/Runtime/Takes/KionjoPoseSample.cs`, `PKG/Runtime/Takes/KionjoTakeWriter.cs`, `PKG/Runtime/Takes/KionjoTakeData.cs`
- Test: `PKG/Tests/Editor/TakeFormatTests.cs`

**Interfaces:**
- `public struct KionjoPoseSample { public bool active; public Vector3 position; public Quaternion rotation; public Vector3 scale; public bool hasScale; }`
- `[Serializable] public class KionjoTrackInfo { public string name; public string path; public bool recordScale; }`
- `[Serializable] public class KionjoTakeEvent { public float t; public string name; public string payload; }`
- `[Serializable] public class KionjoTakeMeta { public int version = 1; public string shotId; public string shotCode; public int takeNumber; public string startedAt; public float durationSeconds; public int frameCount; public float nominalFps; public List<KionjoTrackInfo> tracks = new(); public List<KionjoTakeEvent> events = new(); public string notes; public const string MetaFile = "take.json", FramesFile = "frames.bin", ThumbFile = "thumb.png", AudioFile = "audio.wav"; public static KionjoTakeMeta Load(string takeFolder); public void Save(string takeFolder); }` (JsonUtility, pretty printed).
- `public sealed class KionjoTakeWriter : IDisposable { public KionjoTakeWriter(string takeFolder, KionjoTakeMeta meta); public int FrameCount { get; } public void WriteFrame(float t, KionjoPoseSample[] samples); /* samples.Length == meta.tracks.Count */ public void AddEvent(float t, string name, string payload); public void Close(float durationSeconds); /* patches frameCount in the header and saves meta */ public void Dispose(); /* Close if not closed */ }`
- `public sealed class KionjoTakeData { public KionjoTakeMeta Meta; public float[] Times; public KionjoPoseSample[][] Tracks; /* [track][frame] */ public float Duration => Times.Length == 0 ? 0 : Times[^1]; public static KionjoTakeData Load(string takeFolder); public static KionjoTakeData FromArrays(KionjoTakeMeta meta, float[] times, KionjoPoseSample[][] tracks); }`
- Binary layout: spec section 5.2 (`KJTK`, u16 version 1, u16 trackCount, u32 frameCount, frames of `f32 t` + per track `u8 flags, 7 floats, [3 floats]`). Little-endian `BinaryWriter`/`BinaryReader` over a `FileStream` with a 64 KB buffer.

- [ ] **Step 1: Write failing tests**: `RoundTrip_ThreeTracks_TenFrames` (write with a `recordScale` track, read, assert times, positions, rotations within 1e-6, scale present only on that track), `Header_FrameCount_PatchedOnClose` (read bytes 8..11 after Close), `Load_TruncatedFile_DerivesFrameCountFromLength` (write 10 frames, truncate the file to header + 3.5 frames, Load gives 3 frames and no exception), `Load_EmptyTake_ZeroFrames` (writer closed immediately: `Times.Length == 0`, `Duration == 0`), `Meta_RoundTrip_KeepsEvents`, `FrameSize_ThreeTracksNoScale_Is91Bytes` (file length == 12 + 91 * frames).
- [ ] **Step 2: Run tests.** Expected: fail (types missing).
- [ ] **Step 3: Implement.** Writer keeps the stream open; `Close` seeks to offset 8 and writes the count. Reader: if header count is 0 or exceeds what the length allows, use `(length - 12) / frameBytes` where `frameBytes` depends on the per-track scale flags declared in meta (`recordScale`), so a track's `hasScale` flag is constant for the take.
- [ ] **Step 4: Run tests.** Expected: pass.
- [ ] **Step 5: Commit** `"Take format: JSON meta and compact binary frames with truncation tolerance"`.

### Task 5: Replay math

**Files:**
- Create: `PKG/Runtime/Replay/KionjoReplayMath.cs`
- Test: `PKG/Tests/Editor/ReplayMathTests.cs`

**Interfaces:**
- `public static class KionjoReplayMath { public const float DefaultCutDistance = 0.5f, DefaultCutDegrees = 25f; public static bool IsCut(in KionjoPoseSample a, in KionjoPoseSample b, float cutDistance, float cutDegrees); /* position jump or yaw step */ public static int FindFrame(float[] times, float t); /* index of the last time <= t, -1 before the first */ public static KionjoPoseSample Sample(float[] times, KionjoPoseSample[] track, float t, float cutDistance = DefaultCutDistance, float cutDegrees = DefaultCutDegrees); /* lerp + slerp; holds the earlier frame across a cut or when either frame is inactive; clamps outside the range */ public static KionjoPoseSample[] Stabilize(float[] times, KionjoPoseSample[] track, float smoothingSeconds, bool rollLock, float cutDistance = DefaultCutDistance, float cutDegrees = DefaultCutDegrees); /* centred box average of position and of yaw/pitch over the window, never crossing a cut; rollLock rebuilds rotation from yaw and pitch only */ public static float UnwrapYaw(float previous, float current); public static float VerticalFov(float horizontalDegrees, float aspect); }`

- [ ] **Step 1: Write failing tests**: `Sample_Midway_BlendsPositionAndRotation` (two frames 0 and 1 s, t 0.5 gives midpoint and half-way slerp within 1e-4), `Sample_AcrossTeleport_HoldsEarlierFrame` (jump 2 m), `Sample_SnapTurn_IsCut` (yaw step 45 degrees), `Sample_IntoInactiveFrame_Holds`, `Sample_OutsideRange_Clamps`, `Sample_EmptyTrack_ReturnsInactive`, `Stabilize_RollLock_RemovesRoll` (rotation with 30 degrees roll gives roll within 0.01), `Stabilize_SmoothingStopsAtCut` (a jitter before a teleport does not leak over), `UnwrapYaw_AcrossSouth` (170 to -170 gives 190), `VerticalFov_80Horizontal16by9_Is50_5`.
- [ ] **Step 2: Run tests.** Expected: fail.
- [ ] **Step 3: Implement.** Yaw and pitch from `rotation.eulerAngles` with unwrap; `Stabilize` works on arrays of yaw and pitch and positions, window `[i-k, i+k]` with `k` frames covering `smoothingSeconds / 2` by time, bounded by the nearest cut on either side.
- [ ] **Step 4: Run tests.** Expected: pass.
- [ ] **Step 5: Commit** `"Replay math: sampling with cuts, stabilisation, yaw unwrap"`.

### Task 6: Recorder and events (runtime, PlayMode tests)

**Files:**
- Create: `PKG/Runtime/Recording/KionjoEvents.cs`, `PKG/Runtime/Recording/KionjoEventRelay.cs`, `PKG/Runtime/Recording/KionjoRecorder.cs`, `PKG/Runtime/Recording/KionjoTrack.cs`, `PKG/Runtime/Camera/KionjoCameraSource.cs`, `PKG/Runtime/Camera/IKionjoRecordingHook.cs`
- Test: `PKG/Tests/Runtime/RecorderTests.cs`

**Interfaces:**
- `public static class KionjoEvents { public static void Mark(string name, string payload = null); public static event Action<string, string> Marked; }`
- `[AddComponentMenu("SeedeXR/Kionjo Event Relay")] public class KionjoEventRelay : MonoBehaviour { public string eventName = "event"; public void Mark(); public void Mark(string payload); }`
- `[Serializable] public class KionjoTrack { public string name; public Transform target; public bool recordScale; }`
- `[AddComponentMenu("SeedeXR/Kionjo Recorder")] [DefaultExecutionOrder(32000)] public class KionjoRecorder : MonoBehaviour { public List<KionjoTrack> tracks = new(); public float maxTakeSeconds = 120f; public bool IsRecording { get; } public float Elapsed { get; } public int FrameCount { get; } public string CurrentTakeFolder { get; } public event Action<string> TakeStarted, TakeFinished; public static KionjoRecorder Active { get; } /* the enabled instance */ public void StartTake(string takeFolder, string shotId, string shotCode, int takeNumber); public void StopTake(); public void ToggleTake(); /* StartTake needs a pending folder: set PendingTakeFolder, PendingShotId, PendingShotCode, PendingTakeNumber first; ToggleTake with nothing pending logs a warning */ public string PendingTakeFolder; public string PendingShotId, PendingShotCode; public int PendingTakeNumber; }`
- `[AddComponentMenu("SeedeXR/Kionjo Camera Source")] public class KionjoCameraSource : MonoBehaviour { public new Camera camera; public UnityEvent onRecordingStarted, onRecordingStopped, onRenderStarted, onRenderStopped; public static KionjoCameraSource Find(); public static Camera Resolve(); /* source camera, else Camera.main, else null */ }`
- `public interface IKionjoRecordingHook { void OnKionjoRecordingStarted(string takeFolder); void OnKionjoRecordingStopped(string takeFolder); }`
- Behaviour: `StartTake` creates the folder, builds `KionjoTakeMeta` (tracks from `tracks`, `path` = hierarchy path of the target, `startedAt` = `DateTime.UtcNow.ToString("o")`, `nominalFps = 0`), opens the writer, records `_start = Time.time`, subscribes to `KionjoEvents.Marked`, notifies hooks (`FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)` filtered by the interface) and the camera source events. `LateUpdate` writes a frame when recording (inactive target = `active = false`). At `Elapsed >= maxTakeSeconds` it adds event `cap` and stops. `StopTake` closes the writer with the elapsed duration, computes `nominalFps = frames / duration`, saves meta, raises `TakeFinished(folder)`. `OnDestroy` and `OnDisable` call `StopTake` if recording.

- [ ] **Step 1: Write failing PlayMode tests** (`[UnityTest]`): `Records_FramesAndEvents` (two transforms moved each frame by the test for 10 frames, `Mark("beep","1")` on frame 5, Stop; `KionjoTakeData.Load` gives 10 frames, head position of frame 9 equals the transform's last position within 1e-5, one event named `beep` with `t` between frames 4 and 6), `Cap_StopsAndMarks` (`maxTakeSeconds = 0.05`, wait 0.2 s real time, `IsRecording == false`, events contain `cap`), `Destroy_WhileRecording_ClosesFile` (start, destroy the GameObject next frame, meta exists and `frameCount > 0`), `Toggle_WithoutPending_DoesNotStart` (`LogAssert.Expect(LogType.Warning, ...)`).
- [ ] **Step 2: Run** `UNITY; unity test "$TP" --mode PlayMode --filter SeedeXR.Kionjo --output "$TP/test-results-play.xml" --timeout 1200` Expected: fail.
- [ ] **Step 3: Implement** the six files.
- [ ] **Step 4: Run** the PlayMode command. Expected: exit 0, all passed. Also re-run EditMode to be sure nothing broke.
- [ ] **Step 5: Commit** `"Recorder: tracks to a take folder, events, cap, camera source and recording hooks"`.

### Task 7: Replayer (frame-locked, PlayMode tests)

**Files:**
- Create: `PKG/Runtime/Replay/KionjoReplayer.cs`
- Test: `PKG/Tests/Runtime/ReplayerTests.cs`

**Interfaces:**
- `[AddComponentMenu("SeedeXR/Kionjo Replayer")] [DefaultExecutionOrder(32000)] public class KionjoReplayer : MonoBehaviour { public List<KionjoTrack> tracks = new(); /* bind by name to the take's tracks */ public string headTrackName = "Head"; public Transform headOverride; /* when set, receives the head track instead of the bound target */ public float smoothingSeconds = 0.1f; public bool rollLock = true; public bool lockFrameRate = true; public float fps = 60f; public UnityEvent<string, string> onEvent; public event Action<string, string> EventFired; public event Action Finished; public KionjoTakeData Take { get; } public bool IsPlaying { get; } public bool IsDone { get; } public float Clock { get; } public int Frame { get; } public void Load(string takeFolder); public void Load(KionjoTakeData take); public void Play(float from = 0f, float to = -1f); /* to < 0 = end */ public void Stop(); /* restores Time.captureDeltaTime to its previous value */ }`
- Behaviour: `Play` snapshots `Time.captureDeltaTime`, sets `1f / fps` when `lockFrameRate`, sets `Clock = from`, `Frame = 0`, computes stabilised head samples once (`KionjoReplayMath.Stabilize` on the head track only), fires events whose `t <= Clock` in order (each once). `LateUpdate`: if playing, write every bound track (`Sample(...)` at `Clock`; head uses the stabilised array), fire events up to `Clock`, then `Clock += Time.deltaTime; Frame++`; when `Clock > to` set `IsDone`, raise `Finished`, `Stop`. `Application.onBeforeRender` writes the head pose again (same value) while playing. A target that is null is skipped. `OnDisable` and `OnDestroy` call `Stop`.

- [ ] **Step 1: Write failing PlayMode tests**: `Play_FrameLocked_PoseMatchesSample` (build a `KionjoTakeData.FromArrays` with 2 s of synthetic motion at 90 Hz, replay at `fps = 30` onto a fresh transform; after `WaitForEndOfFrame` on frame k assert `transform.position == Sample(..., k / 30f)` within 1e-4 for k in 0..5), `Events_FireOnceAtTheirFrame` (event at 0.1 s fires once, on the frame where Clock crosses 0.1), `Stop_RestoresCaptureDeltaTime` (set `Time.captureDeltaTime = 0` before, play, stop, equals 0), `Play_EmptyTake_FinishesImmediately` (`IsDone` true after one frame, no exception), `HeadOverride_ReceivesHeadTrack`.
- [ ] **Step 2: Run PlayMode tests.** Expected: fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run PlayMode tests.** Expected: pass.
- [ ] **Step 5: Commit** `"Replayer: frame locked playback of a take onto transforms with events"`.

### Task 8: Storyboard asset and shot model

**Files:**
- Create: `PKG/Editor/Storyboard/KionjoShot.cs` (all `[Serializable]` classes and enums from spec 5.1), `PKG/Editor/Storyboard/KionjoStoryboard.cs`, `PKG/Editor/Storyboard/KionjoPresets.cs`
- Modify: `PKG/Editor/KionjoPaths.cs` (storyboard overloads)
- Test: `PKG/Tests/Editor/StoryboardTests.cs`

**Interfaces:**
- Exactly the types in spec section 5.1 plus: `public static class KionjoPresets { public readonly struct Video { public int Width, Height, Fps; public string[] EncoderArgs; } public static Video Get(KionjoRenderPreset p); public static Video Get(KionjoCutPreset p); }` with Preview720p30 = 1280x720, 30, `["-c:v","libx264","-crf","23","-preset","veryfast"]`; HD1080p60 = 1920x1080, 60, crf 18 medium; HD1080p30 = 1920x1080, 30, crf 18 medium; UHD2160p60 = 3840x2160, 60, crf 16 medium.
- `KionjoShot.PlannedSeconds()` = `outPoint > inPoint ? outPoint - inPoint : targetSeconds`.
- `KionjoStoryboard.AddShot` assigns `id = Guid.NewGuid().ToString("N")`, `code = "S" + (shots.Count + 1).ToString("00")` (cards get `"C" + n`), and calls `Undo.RecordObject(this, "Add shot")` then `EditorUtility.SetDirty(this)`. `Duplicate` deep copies through `JsonUtility.ToJson/FromJson` and assigns a new id and `code + "b"`. `Move` uses `Undo.RecordObject(this, "Move shot")`.

- [ ] **Step 1: Write failing tests** (EditMode, `ScriptableObject.CreateInstance<KionjoStoryboard>()`): `AddShot_AssignsIdAndCode`, `Duplicate_NewIdSameName`, `Move_ReordersWithoutLoss`, `Undo_RestoresCount` (add two, `Undo.PerformUndo()`, count is one; needs `Undo.IncrementCurrentGroup()` before the second add), `PlannedSeconds_UsesRangeWhenSet`, `Presets_Preview_Is1280x720x30`.
- [ ] **Step 2: Run EditMode tests.** Expected: fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: pass.
- [ ] **Step 5: Commit** `"Storyboard asset and shot model with undoable list operations"`.

### Task 9: ffmpeg locator, command and process runner

**Files:**
- Create: `PKG/Editor/Ffmpeg/FfmpegCommand.cs`, `PKG/Editor/Ffmpeg/FfmpegLocator.cs`, `PKG/Editor/Ffmpeg/KionjoProcess.cs`, `PKG/Editor/Ffmpeg/IMediaProbe.cs`, `PKG/Editor/Ffmpeg/FfprobeMediaProbe.cs`
- Test: `PKG/Tests/Editor/FfmpegCommandTests.cs`, `PKG/Tests/Editor/FfmpegLocatorTests.cs`, `PKG/Tests/Editor/KionjoProcessTests.cs`

**Interfaces:**
- `public sealed class FfmpegCommand { public string Executable = "ffmpeg"; public List<string> Args = new(); public string WorkingDirectory; public string Label; public float ExpectedSeconds; /* for progress */ public FfmpegCommand Add(params string[] args); public string ToShellString(); /* each arg double-quoted when it contains space, quote, parenthesis or bracket; inner quotes escaped with backslash */ }`
- `public static class FfmpegLocator { public const string PrefKey = "SeedeXR.Kionjo.FfmpegPath"; public static string PreferredPath { get => EditorPrefs.GetString(PrefKey, ""); set => ... } public static string FindFfmpeg(); /* preferred path if it exists, else first "ffmpeg.exe" (or "ffmpeg") on PATH, else null */ public static string FindFfprobe(); /* sibling of ffmpeg */ public static bool TryGetVersion(string exe, out string version); /* first line "ffmpeg version 7.1..." -> "7.1" */ public static string Status(); /* "ffmpeg 7.1 at C:\..." or "ffmpeg not found" */ }`
- `public static class KionjoProcess { public sealed class Result { public int ExitCode; public string StdErrTail; /* last 20 lines */ public string StdOut; } public static Task<Result> RunAsync(FfmpegCommand cmd, CancellationToken ct, IProgress<float> progress = null, StreamWriter log = null); /* adds "-progress pipe:1 -nostats -v error -y" when Executable ends with ffmpeg; parses out_time_ms into progress 0..1 against ExpectedSeconds; stderr read asynchronously; cancel kills the process tree */ public static Result Run(FfmpegCommand cmd, int timeoutMs = 60000); /* synchronous helper for probes */ }`
- `public interface IMediaProbe { float DurationSeconds(string path); /* -1 when unknown */ }` and `public sealed class FfprobeMediaProbe : IMediaProbe` using `ffprobe -v error -show_entries format=duration -of csv=p=0`.

- [ ] **Step 1: Write failing tests**: `ToShellString_QuotesSpacesAndBrackets` (`Args = ["-i","D:/BERNARD/UNITY PROJECTS (GAMES)/x [1].mp4"]` gives `ffmpeg -i "D:/BERNARD/UNITY PROJECTS (GAMES)/x [1].mp4"`), `Args_KeepPathsAsSingleEntries`, `Locator_PrefersPreferencePath` (write a temp file named `ffmpeg.exe`, set `PreferredPath`, `FindFfmpeg` returns it; restore the pref in TearDown), `Locator_FindsVersionOfInstalledFfmpeg` (`TryGetVersion(FindFfmpeg())` true and starts with a digit), `Process_RunsFfmpegVersion_ExitZero` (`Args = ["-version"]`, `Run`, `ExitCode == 0`), `Process_Cancel_KillsLongRun` (`-f lavfi -i color=black:d=60 -f null -` with `-re` to slow it, cancel after 300 ms, task completes within 3 s), `Probe_Duration_OfLavfiGeneratedFile` (generate a 1.5 s black mp4 into temp with `KionjoProcess.Run`, `FfprobeMediaProbe.DurationSeconds` is 1.5 within 0.1).
- [ ] **Step 2: Run EditMode tests.** Expected: fail.
- [ ] **Step 3: Implement.** `ProcessStartInfo` with `UseShellExecute = false`, `CreateNoWindow = true`, `RedirectStandardOutput`, `RedirectStandardError`, `ArgumentList` filled from `Args`. Progress lines come on stdout (`out_time_ms=`), errors on stderr; both via `BeginOutputReadLine`/`BeginErrorReadLine`.
- [ ] **Step 4: Run.** Expected: pass.
- [ ] **Step 5: Commit** `"ffmpeg command model, locator with preferences, async process runner, ffprobe"`.

### Task 10: Cut plan builder

**Files:**
- Create: `PKG/Editor/Cut/CutPlan.cs`, `PKG/Editor/Cut/CutPlanBuilder.cs`
- Test: `PKG/Tests/Editor/CutPlanBuilderTests.cs`

**Interfaces:**
- `public enum SegmentKind { Video, Image, Black, Text }`
- `public sealed class CutSegment { public string Label; /* "01_S03_crane" */ public SegmentKind Kind; public string SourcePath; public float SourceIn; public float Duration; public float FilmStart; public float FadeIn, FadeOut; public Rect Crop; public float PushIn; public List<string> Lines = new(); public string ShotId; }`
- `public sealed class AudioCue { public string Path; public float TrimIn, TrimOut; /* TrimOut 0 = end */ public float FilmAt; public float FadeIn, FadeOut, Volume; }`
- `public sealed class SubtitleCue { public float Start, End; public string Text; public string OverrideTags; }`
- `public sealed class CutPlan { public int Width, Height, Fps; public string[] EncoderArgs; public string BaseName; public List<CutSegment> Segments = new(); public List<AudioCue> Voice = new(), Music = new(), Sfx = new(); public float MusicCrossfade; public KionjoDucking Ducking; public KionjoLoudness Loudness; public KionjoSubtitleStyle SubtitleStyle; public List<SubtitleCue> Subtitles = new(); public List<string> Warnings = new(); public float TotalSeconds => Segments.Sum(s => s.Duration); }`
- `public static class CutPlanBuilder { public static CutPlan Build(KionjoStoryboard sb, KionjoCutPreset preset, IMediaProbe probe, Func<KionjoShot, string> sourceResolver, Func<KionjoShot, KionjoTakeMeta> takeMetaResolver, bool animatic = false); }` where `sourceResolver` returns the render or capture path for a shot (null = none) so tests never touch disk, and `takeMetaResolver` returns the selected take's meta (for SFX bound to events). A convenience overload `Build(sb, preset, probe)` wires the real resolvers (`KionjoPaths` newest render for the selected take, `deviceCapturePath`, `KionjoTakeMeta.Load`).
- Rules (spec 6.5): Video segments from OfflineRender and DeviceCapture with `Duration = (outPoint > inPoint ? outPoint : probe duration) - inPoint`; Still = Image segment of the thumbnail (Text card with the code when missing); Card by `cardKind`; OrganiseOnly and sourceless shots skipped with `Warnings.Add($"{code}: no source, skipped")`; animatic turns every non-card shot into an Image (thumbnail) or Text segment of `targetSeconds`. Voice `FilmAt = segment.FilmStart + line.at`; subtitle `Start = FilmAt`, `End = FilmAt + (trimOut > 0 ? trimOut - trimIn : probe duration - trimIn)`. SFX with `eventName`: find the first event of that name in the take meta; `FilmAt = FilmStart + (event.t - inPoint)` if `inPoint <= event.t <= inPoint + Duration`, else a warning. Music copied with absolute `FilmAt`.

- [ ] **Step 1: Write failing tests** pinned to trailer 1 numbers: `FilmStarts_AreRunningSums` (Black 7.5, then video 6.0 from in 0.5, then video 3.5: starts 0, 7.5, 13.5; total 17.0), `Voice_AtIsRelativeToShot` (line `at 0.5` on the second segment lands at 8.0), `Sfx_BoundToEvent_MapsThroughInPoint` (segment in 5.8 dur 3.0 at film 51.5, event `seal` at 6.37 gives 52.07 within 1e-3), `Sfx_EventOutsideRange_Warns`, `Sourceless_Shot_SkippedWithWarning`, `OnlyCards_BuildsPlan`, `Subtitle_UsesProbeWhenNoTrimOut` (fake probe 4.51 gives End = FilmAt + 4.51), `Animatic_UsesTargetSecondsAndThumbnails`.
- [ ] **Step 2: Run.** Expected: fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: pass.
- [ ] **Step 5: Commit** `"Cut plan builder: film times, voice, event bound SFX, warnings, animatic mode"`.

### Task 11: ffmpeg command builder (video, subtitles) and SRT writer

**Files:**
- Create: `PKG/Editor/Cut/CutCommandBuilder.cs`, `PKG/Editor/Cut/SrtWriter.cs`
- Test: `PKG/Tests/Editor/CutCommandBuilderTests.cs`, `PKG/Tests/Editor/SrtWriterTests.cs`

**Interfaces:**
- `public static class SrtWriter { public static string Timestamp(float seconds); /* "00:01:09,600" */ public static string Write(IEnumerable<SubtitleCue> cues); /* 1-based index, blank line between cues, tags prefixed to the first line, CRLF not required: use "\n" */ }`
- `public static class CutCommandBuilder { public const string SegFolder = "seg", ListFile = "list.txt", NoSubsVideo = "video_nosub.mp4", Video = "video.mp4", Audio = "audio.wav", Subs = "subs.srt"; public static List<FfmpegCommand> Build(CutPlan plan, string cutFolder, string ffmpegExe); public static FfmpegCommand Segment(CutSegment s, CutPlan p); public static FfmpegCommand Concat(CutPlan p); public static FfmpegCommand BurnSubtitles(CutPlan p); public static FfmpegCommand AudioGraph(CutPlan p); /* Task 12 fills this */ public static FfmpegCommand Mux(CutPlan p); public static string ConcatList(CutPlan p); /* "file 'seg/01_x.mp4'\n..." */ public static string VideoFilter(CutSegment s, CutPlan p); public static string TextCardFilter(CutSegment s, CutPlan p); public static string ForceStyle(KionjoSubtitleStyle st); }`
- Exact strings (from `cut_trailer_v1.sh`): base filter `scale={W}:{H},fps={F},format=yuv420p`; crop before scale as `crop={w}:{h}:{x}:{y}` in pixels of the source (the builder needs source size: take it from `Crop` as normalised and let ffmpeg compute with `crop=iw*{w}:ih*{h}:iw*{x}:ih*{y}`); fades `fade=t=in:st=0:d={FadeIn}` and `fade=t=out:st={Duration-FadeOut}:d={FadeOut}`; Black: `-f lavfi -i color=c=black:s={W}x{H}:r={F}:d={D}`; Text: Black input plus one `drawtext=fontfile=title.ttf:text='{line}':fontcolor=0xD8D8D8:fontsize={34*H/720}:x=(w-tw)/2:y=(h/2)+{offset}:enable='between(t,{a},{b})':alpha='if(lt(t,{a+0.6}),(t-{a})/0.6,if(gt(t,{b-0.6}),({b}-t)/0.6,1))'` per line with lines stacked 58 px apart (scaled by H/720), line i enabled from `0.4 + 1.5*i` to `D - 0.4`; text escaping for drawtext: `'` becomes `\'`, `:` becomes `\:`, `%` becomes `%%`; Image: `-loop 1 -framerate {F} -t {D} -i {path}` with `scale={2W}:{2H},zoompan=z='min({PushIn},1+{(PushIn-1)/(D*F)}*on)':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d=1:s={W}x{H}:fps={F}` then fades; Concat: `-f concat -safe 0 -i list.txt -c copy video_nosub.mp4`; Subtitles: `-i video_nosub.mp4 -vf subtitles=subs.srt:fontsdir=.:force_style='FontName={name},FontSize={size},PrimaryColour={colour},BorderStyle=1,Outline={outline},Shadow=0,MarginV={marginV}' {EncoderArgs} video.mp4`; Mux: `-i video.mp4 -i audio.wav -map 0:v -map 1:a -c:v copy -c:a aac -b:a 192k -shortest {BaseName}.mp4` (when the plan has no audio cues at all: `-i video.mp4 -c copy {BaseName}.mp4`; when no subtitles: the concat output is renamed instead of burnt, by emitting no BurnSubtitles command and having CutJob copy the file). Every command gets `WorkingDirectory = cutFolder`, `Executable = ffmpeg`, `Args` starting with `-v error -y` (the runner adds progress flags). Segment file names `seg/{index:00}_{Sanitize(label)}.mp4`. Fonts: `title.ttf` and `subs.ttf` copied into the cut folder by CutJob from `SubtitleStyle.fontPath` and `titleFontPath` (defaults: none; when empty, drawtext uses `font=Sans` and subtitles use `FontName=Arial`).

- [ ] **Step 1: Write failing tests**: `Timestamp_Formats_HoursMinutesSecondsComma` (69.6 gives `00:01:09,600`), `Write_TwoCues_ExactText` (expected literal including `{\an5}{\fad(500,1100)}` with the braces separate), `Segment_Video_ArgsExact` (in 0.5 dur 6 fade in 3: args list equals `["-v","error","-y","-ss","0.5","-t","6","-i","<abs source>","-an","-vf","scale=1280:720,fps=30,format=yuv420p,fade=t=in:st=0:d=3","-c:v","libx264","-crf","23","-preset","veryfast","seg/01_S03_crane.mp4"]`), `Segment_Black_UsesLavfi`, `Segment_Text_EscapesApostrophe` (`"Nothing's"` yields `Nothing\'s`), `Concat_ListUsesRelativeSegPaths`, `BurnSubtitles_ForceStyleExact`, `Mux_NoAudio_CopiesVideo`, `Build_Order_IsSegmentsConcatSubsAudioMux`, `SourceWithSpacesAndBrackets_IsOneArg`. Numbers use `CultureInfo.InvariantCulture` with `"0.###"`.
- [ ] **Step 2: Run.** Expected: fail.
- [ ] **Step 3: Implement** (AudioGraph returns null for now; `Build` skips nulls).
- [ ] **Step 4: Run.** Expected: pass.
- [ ] **Step 5: Commit** `"ffmpeg command builder for segments, cards, concat, subtitles and mux; SRT writer"`.

### Task 12: Audio graph (voice, loudness) and the cut job

**Files:**
- Modify: `PKG/Editor/Cut/CutCommandBuilder.cs` (AudioGraph)
- Create: `PKG/Editor/Jobs/KionjoJob.cs`, `PKG/Editor/Jobs/KionjoJobs.cs`, `PKG/Editor/Cut/CutJob.cs`
- Test: `PKG/Tests/Editor/AudioGraphTests.cs`, `PKG/Tests/Editor/CutJobTests.cs`

**Interfaces:**
- `AudioGraph(plan)`: inputs are every voice, music and SFX file (`-ss trimIn -t (trimOut-trimIn)` per voice input, `-i path`); filter_complex built exactly like v1: per voice `[n:a]aformat=sample_rates=48000:channel_layouts=stereo,afade=t=in:d={FadeIn},afade=t=out:st={dur-FadeOut}:d={FadeOut},adelay={ms}|{ms}[v{n}]`, then `{all}amix=inputs={n}:normalize=0,apad,atrim=0:{Total},asplit[vo][key]`; music and SFX parts in Task 20 (Tier 2); when there is no music or SFX the final chain is `[vo]loudnorm=I={I}:TP={TP}:LRA={LRA}[a]`; output `-map [a] -c:a pcm_s16le -ar 48000 audio.wav`. Return null when the plan has no audio cues.
- `public abstract class KionjoJob { public string Title; public float Progress; /* 0..1 */ public string Status; public bool IsRunning, IsDone, Failed; public string Error; public List<string> Log = new(); public abstract Task RunAsync(CancellationToken ct); }`
- `public static class KionjoJobs { public static KionjoJob Current { get; } public static event Action Changed; public static bool Start(KionjoJob job); /* false when one is running */ public static void Cancel(); }` (pumps `Changed` from `EditorApplication.update` while a job runs; catches every exception into `Failed` + `Error`).
- `public sealed class CutJob : KionjoJob { public CutJob(CutPlan plan, string cutFolder, string ffmpegExe, string ffprobeExe); public string OutputMp4 { get; } public string OutputSrt { get; } }` steps: create folder and `seg/`, copy fonts, write `subs.srt`, `list.txt`, `commands.txt` (every command's `ToShellString()`, one per line, after a comment line with the cut folder), run commands sequentially writing stderr to `cut.log`, map progress as `(index + stepProgress) / count`, rename `video_nosub.mp4` to `video.mp4` when no subtitles step exists, set `OutputMp4` and `OutputSrt` on success. On a non-zero exit: `Failed = true`, `Error = $"{cmd.Label} failed ({exit}):\n{tail}"`, stop.

- [ ] **Step 1: Write failing tests**: `AudioGraph_TwoVoiceLines_FilterExact` (v1 numbers: line 0 at 0.5 s dur 6.6 and line 1 at 21.5 dur 4.51, total 30: expect the exact filter string), `AudioGraph_NoCues_ReturnsNull`, `CutJob_OnlyCards_ProducesMp4AndSrt` (plan: Black 1.0, Text 1.5 with one line, one SubtitleCue 0.2..1.0; run the job to completion with `Task.Wait` in the test (EditMode, synchronous wait is acceptable); `OutputMp4` exists, ffprobe duration 2.5 within 0.15, `subs.srt` text equals `SrtWriter.Write`), `CutJob_MissingSource_FailsWithStderrTail` (a Video segment whose path does not exist: `Failed`, `Error` contains "No such file" or "Error opening", folder kept with `commands.txt`), `CutJob_Cancel_StopsEarly` (plan with a Black segment of 600 s, cancel after 500 ms, `IsDone` within 5 s, `Failed` true with Error "Cancelled").
- [ ] **Step 2: Run.** Expected: fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: pass (this proves the auto cut end to end with no renders).
- [ ] **Step 5: Commit** `"Audio graph for voice lines, job runner, cut job writing draft folders"`.

### Task 13: Frame capture and ffmpeg pipe sink (PlayMode)

**Files:**
- Create: `PKG/Runtime/Render/IKionjoFrameSink.cs`, `PKG/Runtime/Render/KionjoFrameCapture.cs`, `PKG/Runtime/Render/KionjoStill.cs`
- Create: `PKG/Editor/Render/FfmpegPipeSink.cs`
- Test: `PKG/Tests/Runtime/FrameCaptureTests.cs`

**Interfaces:**
- `public interface IKionjoFrameSink : IDisposable { void Begin(int width, int height, float fps, bool flipVertically); void Push(NativeArray<byte> rgba); /* copies synchronously; may block when the queue is full */ void End(); /* flush and wait */ string Error { get; } }`
- `[DefaultExecutionOrder(32001)] public class KionjoFrameCapture : MonoBehaviour { public Camera camera; public int width = 1280, height = 720; public float fps = 30f; public int targetFrames; /* 0 = until Abort */ public IKionjoFrameSink sink; public int FramesCaptured { get; } public bool IsCapturing { get; } public event Action Finished; public void Begin(); /* snapshots camera.targetTexture, camera.fieldOfView, Time.captureDeltaTime; creates RT (ARGB32, depth 24, default sRGB); camera.targetTexture = rt; sets captureDeltaTime = 1/fps when lockFrameRate; starts the coroutine */ public bool lockFrameRate = true; public float horizontalFovOverride; /* 0 = none; sets camera.fieldOfView = VerticalFov(h, width/height) */ public void Abort(); /* End the sink, restore everything */ }` Coroutine: each frame `yield return new WaitForEndOfFrame()`, then `AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, OnRead)`; keep requests in a queue and push results in order (wait for each request with `WaitForCompletion` is acceptable in v0.1: `req.WaitForCompletion(); sink.Push(req.GetData<byte>())`, which is simple and ordered; note it as `// ponytail: synchronous readback, pipeline later if render time matters`). Stop after `targetFrames`. `OnDestroy` and `OnDisable` restore `camera.targetTexture`, `fieldOfView`, `captureDeltaTime`, release the RT, dispose the sink. `flipVertically = SystemInfo.graphicsUVStartsAtTop`.
- `public static class KionjoStill { public static void CapturePng(Camera camera, string path, int width, int height); /* temp RT, Render(), ReadPixels, EncodeToPNG; restores targetTexture; works in edit mode */ }`
- `public sealed class FfmpegPipeSink : IKionjoFrameSink { public FfmpegPipeSink(string ffmpegExe, string outputMp4, string[] encoderArgs, int queueFrames = 3); }` starts `ffmpeg -v error -y -f rawvideo -pix_fmt rgba -s {W}x{H} -r {fps} -i - [-vf vflip] -pix_fmt yuv420p {encoderArgs} -movflags +faststart {out}` with stdin redirected; a writer thread drains a `BlockingCollection<byte[]>` (bounded) into `StandardInput.BaseStream`; stderr read asynchronously into `Error`; `End` completes adding, joins the thread, closes stdin, `WaitForExit`; `Dispose` kills the process if still alive.

- [ ] **Step 1: Write failing PlayMode tests**: `Capture_TenFrames_ProducesMp4WithTenFrames` (scene: camera at origin looking +Z, background colour pure blue (0,0,1), a quad 10 m wide filling the lower half of the view coloured pure red via an unlit material (`Shader.Find("Universal Render Pipeline/Unlit")`, fall back to `"Unlit/Color"`); 320x180, fps 30, `targetFrames = 10`; wait for `Finished`; ffprobe `-count_frames -show_entries stream=nb_read_frames` gives 10; dump frame 1 as raw rgb24 with `ffmpeg -i out.mp4 -frames:v 1 -f rawvideo -pix_fmt rgb24 -` into bytes; top-left pixel is blue-ish (`b > 150 && r < 100`), bottom-left is red-ish; a fully black frame fails with the message "frame is black: was the editor started with -nographics?"), `Abort_RestoresCameraAndCaptureDeltaTime`, `Destroy_MidCapture_KillsFfmpeg` (no process named ffmpeg with that output path left after 2 s; check with `Process.GetProcessesByName("ffmpeg")` count unchanged from before the test).
- [ ] **Step 2: Run PlayMode tests.** Expected: fail.
- [ ] **Step 3: Implement.** The test asmdef for Runtime cannot reference the Editor asmdef, so `FfmpegPipeSink` needs a home the test can reach: place it in Runtime under `#if UNITY_EDITOR` (`PKG/Runtime/Render/FfmpegPipeSink.cs`), not in Editor. Adjust the Files list accordingly.
- [ ] **Step 4: Run PlayMode tests.** Expected: pass. If the frame is black and the editor log shows `-nographics`, run the fallback: `unity open "$TP"`, `unity status --until-ready --project-path "$TP"`, `unity command run_tests --project-path "$TP" --mode PlayMode --filter FrameCapture --timeout 600`, then `unity close "$TP"`, and record which route passed in the ledger.
- [ ] **Step 5: Commit** `"Frame capture to an ffmpeg raw video pipe, still capture, orientation handled"`.

### Task 14: Render job

**Files:**
- Create: `PKG/Editor/Render/RenderSpec.cs`, `PKG/Editor/Render/RenderJob.cs`, `PKG/Editor/Render/RenderHost.cs`, `PKG/Editor/Shoot/Staging.cs`
- Test: `PKG/Tests/Editor/RenderSpecTests.cs`, `PKG/Tests/Runtime/RenderHostTests.cs`

**Interfaces:**
- `[Serializable] public class RenderSpec { public string storyboardPath; /* asset path */ public string shotId; public string takeFolder; public KionjoRenderPreset preset; public bool wholeTake; public float from, to; public string outputMp4; public string ffmpegExe; public string[] encoderArgs; public static string SessionKey = "SeedeXR.Kionjo.RenderSpec"; public string ToJson(); public static RenderSpec FromJson(string json); }`
- `public static class Staging { public static GameObject Resolve(string hierarchyPath); /* "/Root/Child", includes inactive, null when missing */ public static string PathOf(Transform t); public static List<string> Apply(KionjoShot shot); /* sets hide paths inactive and show paths active, returns unresolved paths */ public static IEnumerator RunHandlers(KionjoShot shot); /* every IKionjoStageHandler in loaded scenes */ }`
- `public class RenderHost : MonoBehaviour { public static RenderHost Create(RenderSpec spec, KionjoShot shot); public IEnumerator Run(); public bool Done, Failed; public string Error; public int Frames, TargetFrames; }` sequence: `Staging.Apply`, `RunHandlers`, two settle frames, find `KionjoReplayer` (create one on the host when the scene has none, binding tracks by name to the recorder's tracks if a `KionjoRecorder` exists, else to the camera only), `Load(takeFolder)`, camera = `KionjoCameraSource.Resolve()` (fail with "No camera: add a Kionjo Camera Source or tag a MainCamera"), `KionjoFrameCapture` with the preset and `FfmpegPipeSink`, `targetFrames = ceil((to - from) * fps)`, `replayer.Play(from, to)`, capture `Begin()`, wait for both, then `KionjoStill`-free thumbnail via ffmpeg (`-ss mid -frames:v 1 thumbs/<shotId>.png`) and the sidecar JSON. LiveCamera shots skip the replayer and run for `targetSeconds`. Still shots capture one PNG with `KionjoStill` and skip ffmpeg.
- `public sealed class RenderJob : KionjoJob { public RenderJob(RenderSpec spec); }` : if `EditorApplication.isPlaying` create the host now; else write the spec to `SessionState`, subscribe `playModeStateChanged`, `EditorApplication.EnterPlaymode()`, and on `EnteredPlayMode` create the host (`[InitializeOnLoadMethod]` re-subscribes after the domain reload and reads the spec from SessionState). Progress from `host.Frames / host.TargetFrames`. On finish: clear SessionState, exit Play mode if the job entered it (`exitPlayModeWhenDone`), mark done.

- [ ] **Step 1: Write failing tests**: EditMode `RenderSpec_JsonRoundTrip`; `Staging_Resolve_FindsInactiveChild` (build a hierarchy in the test, deactivate, resolve by path); `Staging_Apply_ReturnsUnresolved`. PlayMode `RenderHost_RendersRecordedTake` (record a 1 s take from a moving transform with `KionjoRecorder` into temp, then `RenderHost.Create` with a spec for 320x180 preview from 0 to 1 s, wait for `Done`, mp4 exists with 30 frames by ffprobe, thumbnail PNG exists).
- [ ] **Step 2: Run both modes.** Expected: fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run both modes.** Expected: pass.
- [ ] **Step 5: Commit** `"Render job: stage, replay and capture a take into an mp4 with sidecar and thumbnail"`.

### Task 15: Icon

**Files:**
- Create: `PKG/Editor/Icons/kionjo.svg`, `PKG/Editor/Icons/kionjo.png`, `kionjo@2x.png`, `d_kionjo.png`, `d_kionjo@2x.png`, `kionjo-asset.png` (64x64), `kionjo-asset@2x.png` (128x128), `PKG/Editor/Icons/KionjoIcons.cs`, `PKG/Tools~/make_icons.py`
- Test: `PKG/Tests/Editor/IconTests.cs`

**Interfaces:**
- `public static class KionjoIcons { public const string Folder = "Packages/com.seedexr.kionjo/Editor/Icons/"; public static Texture2D Window => EditorGUIUtility.IconContent(Folder + "kionjo.png").image as Texture2D; public static Texture2D Asset => AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "kionjo-asset.png"); }`
- Design (spec section 8): on a 16 unit grid, a rounded rectangle outline from (1.5, 3.5) to (14.5, 12.5), corner radius 2.5, stroke 1.5; two filled circles radius 1.6 at (6, 8) and (10, 8). Light skin colour `#555555`, dark skin `#C4C4C4`, transparent background. The SVG holds exactly these shapes. `make_icons.py` draws them with Pillow at 8x and downsamples with LANCZOS to 16, 32, 64, 128.

- [ ] **Step 1: Write the SVG and the Python script; run** `python "D:/BERNARD/UNITY PROJECTS (GAMES)/kionjo/Tools~/make_icons.py"`. Expected: the six PNGs exist; `python -c "from PIL import Image; print(Image.open(r'...kionjo@2x.png').size)"` prints `(32, 32)`.
- [ ] **Step 2: Write the failing test** `Icons_LoadFromPackage` (`KionjoIcons.Window` and `KionjoIcons.Asset` not null, `Asset.width == 64`).
- [ ] **Step 3: Add `KionjoIcons.cs`, set the import settings** of the PNGs via a one-off: in the test project the package is read-only in the Project window but `.meta` files live in the package folder; create the `.meta` files by letting Unity import once (run the EditMode tests) and then confirm `textureType` is Editor GUI and Legacy GUI (`textureType: 2`) and `mipmaps` off in each `.meta`; edit the `.meta` files directly if not.
- [ ] **Step 4: Run EditMode tests.** Expected: pass.
- [ ] **Step 5: Commit** `"Tool icon: frame and visor mark, light and dark, 1x and 2x, asset icon"`.

### Task 16: The window: shell, storyboard tab, shot cards, inspector, timeline

**Files:**
- Create: `PKG/Editor/UI/KionjoWindow.cs`, `PKG/Editor/UI/KionjoWindow.uxml`, `PKG/Editor/UI/KionjoWindow.uss`, `PKG/Editor/UI/ShotCard.uxml`, `PKG/Editor/UI/ShotInspector.cs`, `PKG/Editor/UI/TimelineBar.cs`, `PKG/Editor/UI/KionjoThumbnails.cs`
- Test: `PKG/Tests/Editor/WindowTests.cs`

**Interfaces:**
- `[EditorWindowTitle(title = "Kionjo", icon = "Packages/com.seedexr.kionjo/Editor/Icons/kionjo.png")] public class KionjoWindow : EditorWindow { [MenuItem("Window/Kionjo", priority = 2000)] public static KionjoWindow Open(); public KionjoStoryboard Storyboard { get; set; } public int SelectedIndex { get; set; } public enum Tab { Storyboard, Shoot, Cut } public Tab CurrentTab { get; set; } public void Rebuild(); }` Element names (tests query them): `#toolbar`, `#storyboard-field`, `#tab-storyboard`, `#tab-shoot`, `#tab-cut`, `#empty-state`, `#shot-list` (ListView), `#shot-inspector`, `#timeline-bar`, `#status-bar`, `#ffmpeg-status`, `#job-progress` (ProgressBar), `#job-cancel`. Selected storyboard persisted in `EditorPrefs` key `SeedeXR.Kionjo.LastStoryboard` (asset GUID).
- Shot list: `ListView` with `bindingPath = "shots"`, `reorderable = true`, `reorderMode = ListViewReorderMode.Animated`, `fixedItemHeight = 64`, `selectionType = Single`, `showAddRemoveFooter = false`, `makeItem` clones `ShotCard.uxml` (thumbnail `Image #thumb`, `Label #code`, `Label #name`, `Label #type-tag`, `Label #location-tag`, `Label #length`, `Label #state`), `bindItem` fills from `Storyboard.shots[i]` (no child binding paths needed) and loads the thumbnail through `KionjoThumbnails.Get(path)` (cache keyed by path and write time). Footer buttons under the list: Add shot, Add card, Duplicate, Remove. After any list change: `so.Update()`, `Rebuild()` of the timeline and chips. `Undo.undoRedoEvent += _ => Rebuild()`.
- Fallback (spec decision 12) if the bound ListView refuses custom `bindItem` or reorder misbehaves: unbound `itemsSource = Storyboard.shots`, register `PointerDownEvent` with `TrickleDown` on the list calling `Undo.RecordObject(Storyboard, "Reorder shots")`, and `itemIndexChanged += (_, _) => { EditorUtility.SetDirty(Storyboard); Rebuild(); }`.
- Inspector: `ShotInspector : VisualElement` built from `SerializedProperty` of the selected shot through `PropertyField`s (name, code, beat, location, type, outputMode, targetSeconds, inPoint, outPoint, fadeIn, fadeOut, horizontalFov, stageCommand, notes, hidePaths, showPaths, voice, sfx, card fields when type is Card, deviceCapturePath when DeviceCapture) with tooltips on each, plus a drop zone label "Drop scene objects here to add them to the hide list" handling `DragUpdatedEvent`/`DragPerformEvent` (`DragAndDrop.objectReferences` GameObjects become `Staging.PathOf`), a "Capture thumbnail" button (`KionjoStill.CapturePng(KionjoCameraSource.Resolve(), KionjoPaths.ThumbnailPath(...), 480, 270)`), and a "Render selected take" button (Task 17 wires it).
- Timeline: `TimelineBar : VisualElement` with one child per shot, `flexGrow = PlannedSeconds()`, label `code`, total label `"{total:0.0} s / target {target:0} s"`, class `over-target` when total exceeds target.
- USS: use `--unity-colors-*` variables (for example `--unity-colors-window-background`, `--unity-colors-default-text`, `--unity-colors-highlight-background`); custom variables `--kionjo-record: #d9433b; --kionjo-ready: #4caf50; --kionjo-warn: #e0a030;` declared on `:root` and repeated under `.unity-theme-env--dark` with slightly brighter values. No hard-coded greys elsewhere.

- [ ] **Step 1: Write failing tests**: `Window_Opens_AndHasNamedElements` (`KionjoWindow.Open()`, `rootVisualElement.Q("shot-list") != null` and the other ids, `Close()`), `Window_NoStoryboard_ShowsEmptyState` (`#empty-state` display flex, `#shot-list` display none), `Window_WithStoryboard_ListsShots` (create a storyboard with two shots in memory, assign, `Rebuild()`, `(Q<ListView>("shot-list")).itemsSource.Count == 2` or the bound count), `Timeline_TotalLabel_FormatsSeconds`.
- [ ] **Step 2: Run EditMode tests.** Expected: fail.
- [ ] **Step 3: Implement.** Load UXML with `AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.seedexr.kionjo/Editor/UI/KionjoWindow.uxml")` and USS likewise. Set `titleContent` in `OnEnable` with `KionjoIcons.Window` too (belt and braces with the attribute).
- [ ] **Step 4: Run EditMode tests.** Expected: pass. Then the live smoke: `UNITY; unity open "$TP"; unity status --until-ready --project-path "$TP" --timeout 600; unity command eval --project-path "$TP" --result-only 'var w = SeedeXR.Kionjo.EditorTools.KionjoWindow.Open(); return w.rootVisualElement.Q("shot-list") != null ? "ok" : "missing";'` Expected: `"ok"`. Take a screenshot for the morning report if the CLI exposes a capture command (`unity command --project-path "$TP" --query capture`; use the listed command, save under `TP/Screenshots/`), then `unity close "$TP"`.
- [ ] **Step 5: Commit** `"Kionjo window: storyboard tab with reorderable shot cards, inspector, timeline bar"`.

### Task 17: Shoot tab (Tier 1 scope: record, stop, replay, take list) and render button

**Files:**
- Create: `PKG/Editor/Shoot/ShootController.cs`, `PKG/Editor/UI/ShootTab.cs`, `PKG/Editor/UI/KionjoShortcuts.cs`
- Modify: `PKG/Editor/UI/KionjoWindow.cs`, `PKG/Editor/UI/ShotInspector.cs`
- Test: `PKG/Tests/Editor/ShootControllerTests.cs` (EditMode, on the pure parts), `PKG/Tests/Runtime/ShootControllerPlayTests.cs`

**Interfaces:**
- `public sealed class ShootController { public ShootController(KionjoStoryboard sb); public KionjoShot Current { get; private set; } public void Select(KionjoShot shot); public List<TakeInfo> Takes { get; } /* from disk: folder, number, duration, event count, isSelected */ public struct TakeInfo { public string Folder, Name; public int Number; public float Duration; public int Events; public bool IsSelected; } public void RefreshTakes(); public bool CanRecord(out string reason); /* Play mode, recorder found, shot selected */ public void ToggleRecord(); /* sets the recorder's pending folder from KionjoPaths + NextTakeNumber, then recorder.ToggleTake() */ public void SelectTake(TakeInfo t); /* Undo.RecordObject, shot.selectedTake = t.Name */ public void DeleteTake(TakeInfo t); public void Replay(TakeInfo t); /* finds or creates a KionjoReplayer bound to the recorder's tracks, Load, Play */ public void StopReplay(); public bool Done(); /* marks nothing persistent; selects the next shot; false at the end */ public event Action Changed; }`
- `KionjoShortcuts`: `[Shortcut("Kionjo/Toggle Take", typeof(KionjoWindow), KeyCode.Space)] static void ToggleTake(ShortcutArguments a)`, `[Shortcut("Kionjo/Done", typeof(KionjoWindow), KeyCode.Return)]`, `[Shortcut("Kionjo/Duplicate Shot", typeof(KionjoWindow), KeyCode.D, ShortcutModifiers.Control)]`, `[Shortcut("Kionjo/Remove Shot", typeof(KionjoWindow), KeyCode.Delete)]`, `[Shortcut("Kionjo/Move Shot Up", typeof(KionjoWindow), KeyCode.UpArrow, ShortcutModifiers.Control)]`, `Move Shot Down` likewise. Each calls into the focused window (`a.context as KionjoWindow`).
- Shoot tab elements: `#shoot-not-playing` (label + "Enter Play mode" button), `#shoot-body`, `#shoot-shot-title`, `#shoot-stage` (button, Task 19 fills it), `#shoot-verify-list`, `#shoot-ready-badge`, `#shoot-record` (button, class `recording` while recording, text "Record (Space)" / "Stop (Space)"), `#shoot-elapsed`, `#shoot-takes` (ListView of TakeInfo rows: name, duration, events, Select, Replay, Reveal, Delete), `#shoot-done`. Refresh on `EditorApplication.update` while recording (elapsed) and on `Changed`.
- Render button in the inspector: builds a `RenderSpec` for the selected take (or newest) with the storyboard's preset and `KionjoJobs.Start(new RenderJob(spec))`; disabled with a tooltip reason when ffmpeg is missing or no take exists.

- [ ] **Step 1: Write failing tests**: EditMode `Takes_ListedFromDisk_SortedByNumber` (create two fake take folders with metas in a temp root; the controller uses a storyboard whose `outputFolder` is absolute for the test: make `KionjoPaths.Root` accept an absolute `outputFolder` as is), `SelectTake_SetsShotSelectedTake`, `Done_AdvancesAndStopsAtEnd`. PlayMode `ToggleRecord_CreatesNextTakeFolder` (scene with a `KionjoRecorder` and one track; `ToggleRecord` twice over 5 frames; one take folder exists and `Takes.Count == 1`).
- [ ] **Step 2: Run both modes.** Expected: fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run both modes.** Expected: pass. Live smoke as in Task 16 step 4: `unity command editor_play --project-path "$TP"`, then eval `var w = SeedeXR.Kionjo.EditorTools.KionjoWindow.Open(); w.CurrentTab = SeedeXR.Kionjo.EditorTools.KionjoWindow.Tab.Shoot; return "ok";`, then `unity command editor_stop --project-path "$TP"`, then close the editor.
- [ ] **Step 5: Commit** `"Shoot tab: record, stop, takes, replay, done; keyboard shortcuts; render button"`.

### Task 18: Cut tab and the Tier 1 end to end test

**Files:**
- Create: `PKG/Editor/UI/CutTab.cs`
- Modify: `PKG/Editor/UI/KionjoWindow.cs`
- Test: `PKG/Tests/Runtime/Tier1EndToEndTests.cs`

**Interfaces:**
- Cut tab elements: `#cut-ffmpeg-line` (status text and a "Preferences" button opening `SettingsService.OpenUserPreferences("Preferences/Kionjo")`, Task 27 adds the provider; until then the button opens the Preferences window root), `#cut-preset` (EnumField bound to `cutPreset`), `#cut-render-missing` (button: queue a RenderJob for every OfflineRender shot whose selected take has no render; jobs run one after another through a small queue in `KionjoJobs`), `#cut-build` (button), `#cut-warnings` (ListView of strings), `#cut-log` (TextField multiline read-only showing the last job's log), `#cut-open-mp4`, `#cut-open-folder` (`EditorUtility.RevealInFinder`). Build: `CutPlanBuilder.Build(sb, preset, new FfprobeMediaProbe())`, folder `KionjoPaths.NextNumbered(Cuts(sb), "draft")`, `KionjoJobs.Start(new CutJob(...))`. Disabled states with tooltips: no ffmpeg, no storyboard, job running, nothing to cut.
- End to end test (PlayMode, `[UnityTest]`, `[Timeout(600000)]`): create a storyboard in memory with `outputFolder` = a temp absolute path; shots: Card Black 1.0 s; Performance shot `S01` with one voice line (generate a 1 s 440 Hz tone wav with ffmpeg `-f lavfi -i sine=frequency=440:duration=1`) at 0.2 with subtitle "Hello" and in 0 out 1.0; Card Text 1.5 s with line "The end". Scene: camera, moving `Head` transform, `KionjoRecorder` with track Head. Steps: `ShootController.ToggleRecord` on/off over 1.2 s; `RenderHost` renders the newest take 0..1 s at 320x180 (direct host use, no Play mode entry needed since the test is in Play mode); `CutPlanBuilder.Build` with the real probe; `CutJob` run to completion; assertions: mp4 exists, ffprobe duration 3.5 within 0.2, has one audio stream, `subs.srt` contains "Hello", `commands.txt` has at least 5 lines.

- [ ] **Step 1: Write the failing end to end test.** Run PlayMode. Expected: fail on the Cut tab pieces missing or compile.
- [ ] **Step 2: Implement the Cut tab.**
- [ ] **Step 3: Run both modes.** Expected: pass. Record the ffprobe duration in the ledger.
- [ ] **Step 4: Update README** with Quick start (install, add Kionjo Recorder with Head and hands, add Kionjo Camera Source, Window > Kionjo, create storyboard, Shoot, Render, Cut), Requirements (Unity 6000.0, ffmpeg on PATH or in Preferences), a "What is generated where" table (spec 5.3), and the `.gitignore` advice. Update CHANGELOG `0.1.0` Added bullets. Write `Documentation~/HeadsetPass.md`: a 10 step manual script for Bernard (add recorder to the XR rig: Head = the center eye camera, LeftHand, RightHand roots; Play over Link; Kionjo window Shoot; Space twice for a 10 s take; unplug; Replay (expect "XR device active" warning gone); Render Preview; compare motion; then HD; report frame count and any judder). Mark every headset-dependent behaviour with "needs headset pass".
- [ ] **Step 5: Commit** `"Cut tab, Tier 1 end to end test, README quick start, headset pass script"`.

**Tier 1 gate:** all EditMode and PlayMode tests pass on 6000.0.70f1; the end to end mp4 plays (`ffprobe` reports video and audio streams); the window opens via the CLI smoke. Write the ledger entry "Tier 1 complete" with both XML paths.

---

## Tier 2: shoot verification, music and SFX, trimming, XML, animatic, settings

### Task 19: Stage verification and Ready/Done

**Files:**
- Create: `PKG/Editor/Shoot/StageVerifier.cs`, `PKG/Runtime/Staging/IKionjoStageHandler.cs`
- Modify: `PKG/Editor/Shoot/ShootController.cs`, `PKG/Editor/UI/ShootTab.cs`
- Test: `PKG/Tests/Editor/StageVerifierTests.cs`, `PKG/Tests/Runtime/StageHandlerTests.cs`

**Interfaces:**
- `public interface IKionjoStageHandler { IEnumerator Stage(string shotId, string stageCommand); }`
- `public enum StageSeverity { Info, Warning, Error }` `public sealed class StageReport { public bool Ready => Lines.All(l => l.Severity != StageSeverity.Error); public List<(StageSeverity Severity, string Text)> Lines = new(); }`
- `public static class StageVerifier { public static StageReport Verify(KionjoShot shot, Func<string, GameObject> resolve, bool hasCamera, bool hasRecorderWithTracks, bool xrDeviceActive, bool forRender); }` lines: each hide path: Error `"Hide: {path} not found"` or `"Hide: {path} is still active"` when the resolved object is active; same for show paths ("inactive"); Info `"Hide list is empty"` when both lists are empty; Error `"No camera source"`; Error `"No Kionjo Recorder with tracks"` (Warning when `forRender` and the shot is LiveCamera); Warning `"An XR device is active: unplug the headset for a replay"` when `xrDeviceActive && forRender`; Info `"Stage command: {cmd}"` when set.
- `ShootController.Stage()`: `Staging.Apply`, run handlers via a coroutine host (`EditorCoroutine` is not a dependency: use a hidden `KionjoCoroutineHost` MonoBehaviour created in Play mode), then `Report = StageVerifier.Verify(...)` with `xrDeviceActive = UnityEngine.XR.XRSettings.isDeviceActive`. `Done()` requires nothing; the badge shows `Ready` or `Not ready`.

- [ ] **Step 1: Write failing tests**: `Verify_UnresolvedHide_IsError`, `Verify_ResolvedButActive_IsError`, `Verify_EmptyLists_Info`, `Verify_NoCamera_Error`, `Verify_XrActiveForRender_Warning`, `Verify_AllGood_Ready`; PlayMode `Handlers_RunInOrder_AndAwaited` (two handler components that each wait one frame and append to a static list).
- [ ] **Step 2: Run.** Expected: fail. **Step 3: Implement.** **Step 4: Run.** Expected: pass.
- [ ] **Step 5: Commit** `"Shoot: stage handlers, hide list verification against the live scene, Ready badge"`.

### Task 20: Music cues, crossfade, ducking, SFX at events

**Files:**
- Modify: `PKG/Editor/Cut/CutCommandBuilder.cs` (AudioGraph), `PKG/Editor/UI/ShotInspector.cs` (SFX cue editor already bound; add an "event" dropdown fed by the selected take's event names), `PKG/Editor/UI/KionjoWindow.cs` (storyboard level music list in a foldout under the Cut tab)
- Test: `PKG/Tests/Editor/AudioGraphTests.cs`

**Interfaces:** AudioGraph grows to the full v1 graph: music inputs `-i path` (one per cue), per cue `[m:a]aformat=...,atrim={trimIn}:{trimOut},asetpts=PTS-STARTPTS[m{i}]`; consecutive cues joined with `[m0][m1]acrossfade=d={Crossfade}:c1=tri:c2=tri[mj]` (chained for more), then `afade=t=in:d={fadeIn},afade=t=out:st={totalMusicLen-fadeOut}:d={fadeOut},volume={volume},adelay={at ms}|{at ms},apad,atrim=0:{Total}[mus]`; SFX `[s:a]aformat=...,adelay={ms}|{ms}[x{i}]`, `{all}amix=inputs={n}:normalize=0,volume={volume},apad,atrim=0:{Total}[sfx]`; ducking `[mus][key]sidechaincompress=threshold={t}:ratio={r}:attack={a}:release={rel}[duck]`; final `[duck][vo][sfx]amix=inputs=3:normalize=0,loudnorm=I=-14:TP=-1.5:LRA=11[a]` (inputs present only; `[vo]` alone handled in Task 12; music without voice skips the sidechain).

- [ ] **Step 1: Write failing tests** pinned to v1: `Music_TwoCues_CrossfadeChain` (cue A trim 0..60.5 at 7.5 s, cue B trim 98.5..118.5, crossfade 2, fades 1.5/2, volume 0.5, total 86: expect the exact `[ma][mb]acrossfade=d=2:c1=tri:c2=tri,afade=t=in:d=1.5,afade=t=out:st=84:d=2,volume=0.5,adelay=7500|7500,apad,atrim=0:86[mus]` tail), `Sfx_TwoCues_AdelayMilliseconds` (52.07 and 55.01 give `adelay=52070|52070` and `adelay=55010|55010`), `Ducking_UsesStoryboardValues`, `MusicOnly_NoSidechain`.
- [ ] **Step 2: Run.** Expected: fail. **Step 3: Implement.** **Step 4: Run.** Expected: pass.
- [ ] **Step 5: Commit** `"Audio graph: music cues with crossfade, ducking, SFX at recorded events"`.

### Task 21: Silence trim at -55 dB

**Files:**
- Create: `PKG/Editor/Cut/SilenceDetect.cs`
- Modify: `PKG/Editor/Cut/CutPlanBuilder.cs` (apply `trimMode == Silence`)
- Test: `PKG/Tests/Editor/SilenceDetectTests.cs`

**Interfaces:** `public static class SilenceDetect { public const float NoiseDb = -55f; public const float MinSilence = 0.1f; public static FfmpegCommand Command(string ffmpeg, string audioPath); /* -i path -af silencedetect=noise=-55dB:d=0.1 -f null - */ public struct Range { public float Start, End; } public static List<Range> ParseSilences(string stderr); /* pairs silence_start / silence_end */ public static (float speechStart, float speechEnd) SpeechBounds(List<Range> silences, float duration); /* start = end of a silence that starts at or before 0.02, else 0; end = start of a silence that ends within 0.02 of duration, else duration */ }` The builder gets an `Func<string, (float, float)> speechBounds` resolver (real one runs ffmpeg once per line and caches by path and write time in `Library/KionjoCache/silence.json`).

- [ ] **Step 1: Write failing tests**: `Parse_TwoSilences_FromCannedStderr` (canned lines in the ffmpeg 7 format `[silencedetect @ ...] silence_start: 0` / `silence_end: 0.82 | silence_duration: 0.82`), `SpeechBounds_LeadingAndTrailing` (duration 13.7, silences 0..0.82 and 12.88..13.7 give 0.82 and 12.88, the judge-charge-03 numbers), `SpeechBounds_NoSilence_FullRange`, `Builder_SilenceMode_TrimsLine`. Plus one real run: generate `sine` 1 s padded with 0.5 s silence each side (`-af adelay=500|500,apad=pad_dur=0.5`) and assert bounds 0.5 and 1.5 within 0.1.
- [ ] **Step 2: Run.** Expected: fail. **Step 3: Implement.** **Step 4: Run.** Expected: pass.
- [ ] **Step 5: Commit** `"Voice trimming at -55 dB with silencedetect"`.

### Task 22: Word timing through faster-whisper (optional dependency)

**Files:**
- Create: `PKG/Tools~/kionjo_words.py`, `PKG/Editor/Cut/WordTimer.cs`
- Modify: `PKG/Editor/UI/ShotInspector.cs` (first and last word pickers when `trimMode == Words`), `PKG/Editor/Cut/CutPlanBuilder.cs`
- Test: `PKG/Tests/Editor/WordTimerTests.cs`

**Interfaces:** `kionjo_words.py <audio> [--model base.en] [--python-utf8]` prints JSON `{"words":[{"w":"Human,","s":0.12,"e":0.41}, ...]}`; decodes audio with `ffmpeg -i audio -f f32le -ac 1 -ar 16000 -` into a numpy array and calls `WhisperModel(model).transcribe(audio, word_timestamps=True)`; exits 3 with a JSON `{"error": "..."}` when faster_whisper is not importable. `public static class WordTimer { public static string PythonPath => EditorPrefs.GetString("SeedeXR.Kionjo.PythonPath", "python"); public static bool IsAvailable(out string reason); public struct Word { public string Text; public float Start, End; } public static List<Word> Parse(string json); public static Task<List<Word>> RunAsync(string audioPath, CancellationToken ct); }` Voice line gains `int firstWord = 0, lastWord = -1` and a cached words JSON under `Library/KionjoCache/words/<hash>.json`.

- [ ] **Step 1: Write failing tests**: `Parse_WordsJson`, `Builder_WordsMode_TrimsToWordBounds` (fake resolver), and an availability test that only asserts `IsAvailable` returns a reason string when unavailable (no model download in tests).
- [ ] **Step 2: Run.** Expected: fail. **Step 3: Implement.** **Step 4: Run.** Expected: pass. Also run the script once by hand on the sine wav from Task 21 with `PYTHONUTF8=1 python "$PKG/Tools~/kionjo_words.py" <wav>`; expected: JSON with zero or more words and exit 0 (the model downloads on first use; if offline, the error JSON and exit 3 are also acceptable, note it).
- [ ] **Step 5: Commit** `"Word level trimming through faster-whisper, optional"`.

### Task 23: FCP7 XML timeline

**Files:**
- Create: `PKG/Editor/Cut/Fcp7XmlWriter.cs`
- Modify: `PKG/Editor/Cut/CutJob.cs` (write `<BaseName>.xml`)
- Test: `PKG/Tests/Editor/Fcp7XmlWriterTests.cs`

**Interfaces:** `public static class Fcp7XmlWriter { public static string Write(CutPlan plan, string sequenceName, string cutFolder); }` producing `<?xml version="1.0" encoding="UTF-8"?><!DOCTYPE xmeml><xmeml version="5"><sequence id="seq1"><name/><duration>{frames}</duration><rate><timebase>{fps}</timebase><ntsc>FALSE</ntsc></rate><media><video><format><samplecharacteristics><width/><height/><pixelaspectratio>square</pixelaspectratio><rate>...</rate></samplecharacteristics></format><track>{clipitems}</track></video><audio><track>{voice}</track><track>{music}</track><track>{sfx}</track></audio></media></sequence></xmeml>`. Video clipitems reference the source file for Video segments (`in/out` = source frames, `start/end` = film frames) and the generated `seg/*.mp4` for cards and images (in 0). Audio clipitems reference the cue files with `in = trimIn frames`. Each `<file id="file-N">` carries `<name>`, `<pathurl>file://localhost/{path with forward slashes, URI escaped, drive like D:}</pathurl>`, `<rate>`, `<duration>`, `<media><video/></media>` or `<audio><channelcount>2</channelcount></audio>`. Frames = `Mathf.RoundToInt(seconds * fps)`.

- [ ] **Step 1: Write failing tests**: `Write_IsWellFormedXml` (`XDocument.Parse`), `Video_ClipItem_FrameMath` (segment in 0.5 dur 6 at film 7.5 at 30 fps: in 15, out 195, start 225, end 405), `Card_References_SegFile`, `PathUrl_EscapesSpacesAndParentheses` (`D:/BERNARD/UNITY PROJECTS (GAMES)/x.mp4` gives `file://localhost/D:/BERNARD/UNITY%20PROJECTS%20(GAMES)/x.mp4`), `Audio_Tracks_InOrderVoiceMusicSfx`.
- [ ] **Step 2: Run.** Expected: fail. **Step 3: Implement.** **Step 4: Run.** Expected: pass. Note in the ledger: "XML import into Premiere: Bernard".
- [ ] **Step 5: Commit** `"FCP7 XML timeline export beside every draft"`.

### Task 24: Animatic

**Files:**
- Modify: `PKG/Editor/UI/CutTab.cs` (`#cut-animatic` button), `PKG/Editor/Cut/CutJob.cs` (folder prefix `animatic`)
- Test: `PKG/Tests/Editor/AnimaticTests.cs`

- [ ] **Step 1: Write the failing test** `Animatic_Builds_FromThumbnailsAndTextCards` (storyboard with one shot with a thumbnail PNG written by the test (Pillow-free: `Texture2D` 64x36 encoded with `ImageConversion.EncodeToPNG`) and one without; plan has an Image and a Text segment of their `targetSeconds`; run `CutJob` to completion at Preview; mp4 duration equals the sum within 0.2).
- [ ] **Step 2: Run.** Expected: fail. **Step 3: Implement** the button and folder naming (`KionjoPaths.NextNumbered(Animatics(sb), "animatic")`). **Step 4: Run.** Expected: pass.
- [ ] **Step 5: Commit** `"Animatic: the storyboard as a cut of thumbnails with voice and music"`.

### Task 25: Render quality override

**Files:**
- Create: `PKG/Editor/Render/QualityOverride.cs`
- Modify: `PKG/Editor/Storyboard/KionjoStoryboard.cs` (`KionjoQualityOverride renderQuality`), `PKG/Editor/Render/RenderJob.cs`
- Test: `PKG/Tests/Editor/QualityOverrideTests.cs`

**Interfaces:** `[Serializable] public class KionjoQualityOverride { public bool enabled; public string qualityLevelName; public float lodBias = 0; /* 0 = keep */ public bool forceLod0; public int textureMipLimit = -1; /* -1 keep */ public bool anisotropicForce; public bool unlimitedSkinWeights; }` `public static class QualityOverride { public static void Apply(KionjoQualityOverride q); /* snapshots to SessionState keys SeedeXR.Kionjo.Quality.* then applies */ public static void Restore(); /* idempotent */ [InitializeOnLoadMethod] static void Hook(); /* Restore on ExitingPlayMode and when a snapshot is stranded after a domain reload */ }` (Baraza's `TrailerCaptureQuality`, generic subset: `QualitySettings.lodBias`, `globalTextureMipmapLimit`, `anisotropicFiltering`, `skinWeights`, `QualitySettings.SetQualityLevel(index by name)`).

- [ ] **Step 1: Write failing tests**: `Apply_ThenRestore_RoundTrips` (change lodBias and mip limit, restore, equal to the start), `Apply_Twice_RestoresOriginal`, `Stranded_Snapshot_IsRestoredByHook`.
- [ ] **Step 2: Run.** Expected: fail. **Step 3: Implement** and call `Apply` at the start of `RenderHost.Run` and `Restore` in its finish and `OnDestroy`. **Step 4: Run.** Expected: pass.
- [ ] **Step 5: Commit** `"Render quality override with snapshot and restore"`.

### Task 26: Device capture and organise only modes

**Files:**
- Create: `PKG/Editor/Cut/CaptureImporter.cs`
- Modify: `PKG/Editor/UI/ShotInspector.cs` (Import capture button, drop zone for files via `DragAndDrop.paths`)
- Test: `PKG/Tests/Editor/CaptureImporterTests.cs`

**Interfaces:** `public static class CaptureImporter { public static string Import(KionjoStoryboard sb, KionjoShot shot, string sourcePath); /* copies into ShotCaptures, name "<code>_capture<NN>.mp4", sets shot.deviceCapturePath with Undo, returns the new path */ public static void WriteIndex(KionjoStoryboard sb); /* captures/index.md: table of shot code, file, duration via probe, notes */ }` OrganiseOnly shots are included in the index and excluded from the plan (already a warning in Task 10; downgrade it to Info "organise only").

- [ ] **Step 1: Write failing tests**: `Import_CopiesAndNumbers`, `Index_ListsEveryCapture`, `Plan_OrganiseOnly_IsInfoNotWarning`.
- [ ] **Step 2: Run.** Expected: fail. **Step 3: Implement.** **Step 4: Run.** Expected: pass.
- [ ] **Step 5: Commit** `"Device capture import and organise only mode with an index"`.

### Task 27: Preferences, project settings, help, polish pass

**Files:**
- Create: `PKG/Editor/Settings/KionjoPreferences.cs` (`SettingsProvider` at `Preferences/Kionjo`: ffmpeg path with Browse and a live status line, python path, encoder arguments string (default `-c:v libx264 -crf 18 -preset medium`), open folder after cut toggle), `PKG/Editor/Settings/KionjoProjectSettings.cs` (`ScriptableSingleton<KionjoProjectSettings>` with `[FilePath("ProjectSettings/KionjoSettings.asset", FilePathAttribute.Location.ProjectFolder)]`: default output folder, default presets; provider at `Project/Kionjo`)
- Modify: `PKG/Editor/UI/KionjoWindow.cs` (Help button opens `https://github.com/SeedeXR/kionjo#readme`; every button has a tooltip; status bar shows the job title and progress; `#job-cancel` calls `KionjoJobs.Cancel`), USS pass for both skins (check with `EditorGUIUtility.isProSkin` toggled through `Preferences > General > Editor Theme` by hand is impossible headless: instead assert that the USS declares every custom colour under both `:root` and `.unity-theme-env--dark` in a test that reads the USS text)
- Test: `PKG/Tests/Editor/SettingsTests.cs`

- [ ] **Step 1: Write failing tests**: `Preferences_Provider_Exists` (`SettingsService` lookup via `SettingsProvider` creation method returns a provider with path `Preferences/Kionjo`), `ProjectSettings_Singleton_Persists` (set, `Save(true)`, file exists), `Uss_DeclaresDarkVariants` (regex for `--kionjo-record` appears twice).
- [ ] **Step 2: Run.** Expected: fail. **Step 3: Implement.** **Step 4: Run.** Expected: pass. Live smoke: open the test editor, `unity command eval` that opens the window, switches each tab, and returns the count of elements with an empty `tooltip` among `Button`s (expected 0), then close the editor.
- [ ] **Step 5: Commit** `"Preferences and project settings, help link, tooltips, theme variables for both skins"`.

**Tier 2 gate:** rerun both test modes; update CHANGELOG; ledger entry "Tier 2 complete" or the list of tasks not reached.

---

## Tier 3: agent bridge

### Task 28: CLI commands and the state file

**Files:**
- Create: `PKG/Editor/Agent/SeedeXR.Kionjo.Agent.Editor.asmdef` (`references: ["SeedeXR.Kionjo", "SeedeXR.Kionjo.Editor", "Unity.Pipeline.Attributes"]`, `includePlatforms: ["Editor"]`, `defineConstraints: ["KIONJO_PIPELINE"]`, `versionDefines: [{ "name": "com.unity.pipeline", "expression": "0.1.0", "define": "KIONJO_PIPELINE" }]`), `PKG/Editor/Agent/KionjoCommands.cs`, `PKG/Editor/Agent/KionjoStateFile.cs`, `PKG/Editor/Storyboard/KionjoStoryboardJson.cs` (export and import of the whole storyboard as JSON, in the Editor assembly so tests reach it)
- Test: `PKG/Tests/Editor/StoryboardJsonTests.cs`

**Interfaces:**
- `KionjoStoryboardJson.Export(KionjoStoryboard sb) : string` and `Import(KionjoStoryboard sb, string json, bool replace)` (JsonUtility of a wrapper; ids preserved when present, generated when missing).
- `KionjoStateFile.Write(KionjoStoryboard sb)` to `<root>/state.json`: `{ storyboard, shots:[{id, code, name, type, takes, selectedTake, hasRender, state}], currentShot, isPlaying, isRecording, job:{title, progress, status, failed, error}, updated }`; called from `KionjoJobs.Changed`, `ShootController.Changed`, and after storyboard edits (debounced 0.5 s through `EditorApplication.delayCall`).
- Commands (`Unity.Pipeline.Commands`, all static in `public static class KionjoCommands`, `Tags = new[] { "kionjo" }`): `kionjo_status()` returns the state object; `kionjo_open(string storyboard)` opens the window on the asset path; `kionjo_list_shots()`; `kionjo_select(string shot)` (id or code); `kionjo_stage()`; `kionjo_record(bool start)`; `kionjo_replay(string take = null)`; `kionjo_render(string shot = null, string preset = "Preview720p30")`; `kionjo_cut(string preset = "Preview720p30")`; `kionjo_animatic()`; `kionjo_import_storyboard(string jsonPath, bool replace = false)`; `kionjo_export_storyboard(string jsonPath)`. Each returns a small anonymous object `{ ok, message, ... }` and never throws for user errors.

- [ ] **Step 1: Write failing tests**: `Json_RoundTrip_PreservesIdsAndOrder`, `Import_MissingIds_Generated`, `Import_Replace_ClearsOldShots`.
- [ ] **Step 2: Run.** Expected: fail. **Step 3: Implement.** The test project has the pipeline package (`--with-pipeline`), so the bridge compiles there. **Step 4: Run** both modes. Expected: pass. Live smoke: open the test editor, `unity command kionjo_status --project-path "$TP" --result-only`; expected JSON with `"ok": true`; `unity command --project-path "$TP" --query kionjo` lists the commands; close the editor. Then the no-pipeline check: temporarily remove `com.unity.pipeline` from `TP/Packages/manifest.json`, run the EditMode tests (expected: pass, bridge not compiled, no errors about `Unity.Pipeline.Attributes`), and put the package back. If Unity errors on the missing reference even with the constraint unsatisfied, move the bridge to `PKG/Samples~/AgentBridge/` and document "import the sample when the pipeline package is installed"; record which.
- [ ] **Step 5: Commit** `"Agent bridge: kionjo_* CLI commands, state file, storyboard JSON import and export"`.

### Task 29: Agent guide and suggest storyboard recipe

**Files:**
- Create: `PKG/Documentation~/AgentGuide.md`
- Modify: `PKG/README.md` (Agent section)

- [ ] **Step 1: Write the guide**: the JSON schema of a storyboard (field by field from `KionjoShot`), the `kionjo_*` command list with examples using `--project-path`, the `state.json` shape and how to watch it, and the "suggest storyboard" recipe: the agent reads the project's narration script and a scene inventory (`unity command eval` listing root objects), writes a storyboard JSON draft with beats, locations, target lengths, voice lines and subtitle text, imports it with `kionjo_import_storyboard`, and the human edits it in the window. No code: the deterministic core already has every verb.
- [ ] **Step 2: Verify** the guide's commands by running two of them against the open test editor (`kionjo_export_storyboard` to a temp path, `kionjo_import_storyboard` back with `replace=true`); expected `ok: true` for both.
- [ ] **Step 3: Commit** `"Agent guide with the storyboard JSON schema and the suggest storyboard recipe"`.

---

## Closing (every night, whatever tier was reached)

- [ ] Run both test modes one final time; put the two XML summaries in the ledger.
- [ ] Update `CHANGELOG.md` with what landed; update `README.md` status line.
- [ ] Commit `"Session close: changelog and readme"`.
- [ ] Write the morning report at `Documentation~/Design/progress.md` top block: tasks done with hashes, tests passed per mode, what is unverified (headset pass, Premiere import, dark skin by eye, `-nographics` route used), the two screenshots if taken, and the three decisions most worth Bernard's eyes (decisions 3, 4 and 12 in the spec).

## Self-review notes (planner, 2026-10-23)

- Spec coverage: every spec section maps to a task (5.1 to Task 8; 5.2 to Tasks 4, 6; 5.3 to Task 3; 6.1 to Task 16; 6.2 to Task 24; 6.3 to Tasks 17, 19; 6.4 to Tasks 13, 14, 25; 6.5 to Tasks 10, 11, 12, 20, 21, 22, 23; 6.6 to Task 26; 7 to Tasks 16, 17, 18, 27; 8 to Task 15; 9 to Tasks 6, 7, 19; agent to Tasks 28, 29).
- Type consistency: `KionjoPoseSample`, `KionjoTakeMeta`, `KionjoTakeData`, `KionjoTrack`, `KionjoRecorder`, `KionjoReplayer`, `KionjoCameraSource`, `KionjoFrameCapture`, `IKionjoFrameSink`, `FfmpegPipeSink`, `FfmpegCommand`, `KionjoProcess`, `IMediaProbe`, `CutPlan`, `CutSegment`, `AudioCue`, `SubtitleCue`, `CutPlanBuilder`, `CutCommandBuilder`, `SrtWriter`, `CutJob`, `KionjoJob`, `KionjoJobs`, `RenderSpec`, `RenderHost`, `RenderJob`, `Staging`, `StageVerifier`, `ShootController`, `KionjoWindow`, `KionjoIcons`, `KionjoPaths`, `KionjoTakePaths` are used with the same names throughout.
- Known soft spots left on purpose: `FfmpegPipeSink` lives in Runtime under `#if UNITY_EDITOR` so PlayMode tests can reach it (Task 13 step 3); the bound ListView has a documented fallback (Task 16); the `-nographics` question has a fallback route (Task 13 step 4); the agent asmdef has a fallback location (Task 28 step 4).
