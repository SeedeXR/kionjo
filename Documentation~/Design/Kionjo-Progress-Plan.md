# Kionjo Progress Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Kionjo notices progress that agents record, films it at the moment it happens from several frame-locked angles, and turns every step into a post folder with the materials, Kionjo's post, the CapCut handoff and a brief, using little disk and asking for memory first.

**Architecture:** Journal entries (Posts phase 1) are extended to carry progress (`feature`, `version`, `brief`, `captures`). Small pure classes do the logic (backlog, notes, links, brief, rigs, resources) and are tested in EditMode. A thin editor watcher and a capture runner (Play mode, `Time.captureDeltaTime`, one `KionjoFrameCapture` + `FfmpegPipeSink` per rig, `AudioRenderer` to WAV) sit on top. `PostJob` moves to the versioned folder layout.

**Tech Stack:** Unity 6000.0 editor C#, NUnit (Unity Test Framework), ffmpeg/ffprobe (already required), Win32 `CreateHardLink` and `GlobalMemoryStatusEx` via P/Invoke, POSIX `link`.

**Spec:** `Documentation~/Design/Kionjo-Progress-Design.md` (approved 2026-11-17). Also read `Kionjo-Posts-Design.md` sections 7.1, 7.2, 7.4.

## Global Constraints

- Branch: work on `posts` (it already holds Posts phase 1); merge to `main` in Task 11.
- Unity 6000.0+, URP. Everything new is editor-only except `Runtime/Capture/KionjoMultiCapture.cs`.
- Windows first; macOS/Linux must compile and degrade (memory "unknown", links fall back to copies) with a warning, never an exception.
- No new package dependency. ffmpeg is found with `FfmpegLocator.FindFfmpeg()`.
- Public MIT repo: no studio names, accounts, personal paths or strategy in code, tests, docs or commit messages.
- On-screen and brief text in plain words. JSON written by `JsonUtility`, UTF-8 without BOM, pretty printed.
- Kionjo never deletes footage or captures (source). It deletes only: intermediate files of a finished post, and videos (`.mp4 .mov .webm .mkv`) in older `vNN` folders of the same entry once a newer `vNN` is complete.
- Memory rule for heavy jobs: free commit at least 2x the job's estimate AND at least `floorGB` (default 8) left after it.
- Test run (Git Bash): `export PATH="$PATH:/c/Users/Seede Sr/AppData/Local/Unity/bin"; TP="D:/BERNARD/UNITY PROJECTS (GAMES)/Kionjo-TestProject"; unity test "$TP" --mode EditMode --filter SeedeXR.Kionjo --output "$TP/test-results.xml" --timeout 900; echo exit=$?` (0 = pass, 6 = failures). PlayMode: same with `--mode PlayMode`. The TP editor must be closed. Before every run: check free memory, tell the other Claude sessions, note `Get-Process Unity` PIDs, and confirm any Unity you started is gone afterwards. Never write package files while a test run compiles.

## Review Focus

1. A footage file that is still being written when the watcher sees it: it must not be attached until its size is stable for 2 s (Task 7 test `WaitsForStableSize`).
2. Paths with spaces and brackets (`D:/X/UNITY PROJECTS (GAMES)/a [b].mp4`) in links, notes lines, ffmpeg and brief text (Tasks 3, 4, 6 tests use such names).
3. Materials on another volume or a file system without hard links: copy and report, never throw (Task 4 test `FallsBackToCopy`).
4. An agent saving the entry at the same time Kionjo appends media: the agent's text survives (Task 1 test `UpdateKeepsOtherWriterFields`).
5. A capture whose scene has no camera, or whose subject path does not resolve: the request ends `failed` with the reason, nothing is half written, and a batch editor exits (Task 10 test `FailsCleanlyWithoutSubject`).

---

### Task 1: Progress fields on journal entries and a safe update

**Files:**
- Modify: `Editor/Posts/KionjoJournal.cs`
- Create: `Editor/Posts/KionjoCaptureRequest.cs`
- Test: `Tests/Editor/JournalTests.cs`

**Interfaces:**
- Produces: `JournalEntry.feature : string`, `JournalEntry.version : string`, `JournalEntry.brief : string`, `JournalEntry.captures : List<KionjoCaptureRequest>` (never null after `Load`).
- Produces: `[Serializable] class KionjoCaptureRequest { string id; string scene; float seconds = 10; float fps = 30; string subject; List<string> rigs; float stills = 0.5f; string label; string version; bool dirty; string state = "pending"; string reason; List<string> outputs; }`. States: `pending`, `taken`, `failed`, `dropped`. `const string` names in `KionjoCaptureRequest.States`.
- Produces: `KionjoJournal.Update(string folder, Action<JournalEntry> change) : JournalEntry`: load, apply, save, all under `SaveLock`; returns the saved entry or null when the folder holds no readable entry.

- [ ] **Step 1: Write the failing tests** in `JournalTests.cs`:
  - `ProgressFieldsRoundTrip`: create an entry, set `feature="hero"`, `version="main@abc123"`, `brief="## Why\nBecause."`, add a request `{ scene="Assets/S.unity", rigs={"main","wide"}, label="v3" }`, save, load: all equal; `captures[0].state == "pending"`.
  - `OldEntryLoadsWithEmptyCaptures`: write a phase 1 JSON without the new fields; `Load` gives `captures` not null and empty, `feature == null`.
  - `UpdateKeepsOtherWriterFields`: entry A saved; a second writer loads it, sets `achieved="Agent text"`, saves; then `KionjoJournal.Update(folder, e => e.media.Add(new JournalMedia{path="x.mp4"}))`; reload has both `achieved == "Agent text"` and one media item.
- [ ] **Step 2: Run EditMode tests, expect the three to fail to compile/fail.**
- [ ] **Step 3: Implement the fields, `KionjoCaptureRequest`, the null guard in `Load` and `Update`.**
- [ ] **Step 4: Run EditMode tests, expect all green (phase 1 count + 3).**
- [ ] **Step 5: Commit** `Journal: progress fields (feature, version, brief, captures) and a load-merge-save update`.

### Task 2: Backlog rules

**Files:**
- Create: `Editor/Progress/KionjoBacklog.cs`
- Test: `Tests/Editor/BacklogTests.cs`

**Interfaces:**
- Consumes: Task 1 types.
- Produces: `static class KionjoBacklog { bool IsCurrent(JournalEntry e, IReadOnlyList<JournalEntry> all); List<(JournalEntry entry, KionjoCaptureRequest request)> Pending(IReadOnlyList<JournalEntry> all); int DropSuperseded(IReadOnlyList<JournalEntry> all); }`
- Rule: an entry is current when no other entry with the same non-empty `project` and `feature` has a later `created` (ties broken by folder name, ordinal). An entry without `feature` is always current. `DropSuperseded` sets every `pending` request of a non-current entry to `dropped` with reason `"A later step of <feature> exists; this version is gone."`, saves through `KionjoJournal.Update`, returns how many.

- [ ] **Step 1: Write the failing tests:** `LaterStepSupersedes` (two entries feature "hero", 11-16 and 11-17: only 11-17 current), `OtherFeatureDoesNotSupersede`, `NoFeatureIsAlwaysCurrent`, `PendingListsOnlyCurrentPending` (taken/failed/dropped excluded), `DropSupersededSavesReason` (reload shows `dropped` + reason, returns 1).
- [ ] **Step 2: Run, expect failures.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run, expect green.**
- [ ] **Step 5: Commit** `Backlog: a step's pending captures are dropped once a later step of the feature exists`.

### Task 3: Footage notes

**Files:**
- Create: `Editor/Progress/FootageNotes.cs`
- Test: `Tests/Editor/FootageNotesTests.cs`

**Interfaces:**
- Produces: `static class FootageNotes { const string FileName = "notes.md"; void Append(string dayFolder, string fileName, string text); string LineFor(string dayFolder, string fileName); string DayFolder(string footageRoot, DateTime day); }`
- `Append` writes `- <fileName>: <text>` as one line (creates the file with `# <yyyy-MM-dd>` heading when missing; newlines in text become spaces). `LineFor` returns the text after the colon of the first line starting with `- ` that contains the file name, or with the file's stem followed by `/` or ` ` (agents write `a-clean/lingering.mp4` style groups); null when none. `DayFolder` = `<root>/<yyyy-MM-dd>`.

- [ ] **Step 1: Write the failing tests:** `AppendCreatesHeadingAndLine`, `AppendFlattensNewlines`, `LineForFindsExactName` (name `hero [v3] wide.mp4`), `LineForFindsGroupedLine` (`- hero-film-clean/lingering.mp4: first films` matches `hero-film-clean.mp4`), `LineForReturnsNullWhenAbsent`.
- [ ] **Step 2: Run, expect failures.** **Step 3: Implement.** **Step 4: Run, expect green.**
- [ ] **Step 5: Commit** `Footage notes: append and look up the one-line note per file`.

### Task 4: Hard links with copy fallback

**Files:**
- Create: `Editor/Progress/KionjoLinks.cs`
- Test: `Tests/Editor/LinksTests.cs`

**Interfaces:**
- Produces: `static class KionjoLinks { bool LinkOrCopy(string source, string destination, out string note); }` returns true when hard linked, false when copied (note says why, e.g. "another drive"). Overwrites an existing destination. Windows: `kernel32 CreateHardLinkW`; macOS/Linux: libc `link`. Any failure falls back to `File.Copy`.
- Test seam: `internal static Func<string, string, bool> TryLink` (replaced in a test to force the fallback).

- [ ] **Step 1: Write the failing tests:** `LinksOnSameVolume` (temp dir, file `a b [c].mp4`; returns true; same bytes; writing the source changes the destination on Windows), `DeletingLinkKeepsSource`, `FallsBackToCopy` (TryLink returns false: returns false, bytes equal, note not empty), `OverwritesExisting`.
- [ ] **Step 2: Run, expect failures.** **Step 3: Implement.** **Step 4: Run, expect green.**
- [ ] **Step 5: Commit** `Links: hard link a file into a post folder, copy when the volume cannot link`.

### Task 5: Brief writer

**Files:**
- Create: `Editor/Progress/BriefWriter.cs`
- Test: `Tests/Editor/BriefWriterTests.cs`

**Interfaces:**
- Produces: `static class BriefWriter { string Write(JournalEntry e, IReadOnlyList<(string fileName, string note)> materials); }`
- Sections in this order, each omitted when empty: `# <title>`, a line `Stage: <stage>. Feature: <feature>. Version: <version>.`, `## In one sentence` (first sentence of `achieved`, else `making`), `## Why` (`goal`), `## Approach` (`making`, `reference`), `## Decisions` (bullets), `## Hurdle and trick` (`hurdle` -> `trick`), `## Materials` (bullet per file: `` `name`: note ``), `## Hook options` (numbered), then `e.brief` verbatim under `## Notes from the build`. No dates other than the stage line (none there either).

- [ ] **Step 1: Write the failing tests:** `FullEntryHasSectionsInOrder` (index of each heading increasing), `EmptyFieldsOmitSections`, `MaterialsListNotes` (file with brackets), `AgentBriefIsVerbatim` (Markdown with `|` table survives).
- [ ] **Step 2–4: fail, implement, green.**
- [ ] **Step 5: Commit** `Brief: a Markdown brief per step from the entry and its materials`.

### Task 6: Versioned post folder and PostJob migration

**Files:**
- Create: `Editor/Progress/PostFolder.cs`
- Modify: `Editor/Posts/PostJob.cs`, `Editor/Posts/PostCommandBuilder.cs` (output sub-folders), `Editor/UI/PostBuildView.cs:267-273`, `Editor/Posts/PostMenu.cs`
- Test: `Tests/Editor/PostFolderTests.cs`, `Tests/Editor/PostJobTests.cs`

**Interfaces:**
- Consumes: Tasks 3–5.
- Produces: `static class PostFolder`:
  - `string EntryFolder(string postsRoot, JournalEntry e)` = `<postsRoot>/<created>_<slug(title)>`.
  - `string Latest(string entryFolder)` = highest `vNN` or null; `string Next(string entryFolder)` = `KionjoPaths.NextNumbered(entryFolder, "v")`.
  - `void Refresh(string versionFolder, JournalEntry e, string footageRoot)`: links every existing media file into `materials/` (via `KionjoLinks`), removes `materials/` files no longer in the entry, writes `BRIEF.md` (notes from `JournalMedia.note`, else `FootageNotes.LineFor` of the file's own folder). Missing media are listed in the brief under `## Missing` instead of failing.
  - `long PruneOlderVideos(string entryFolder, string keepVersionFolder)`: deletes video files under every other `vNN` (recursively, `materials/` included: those are links), returns bytes of non-link files freed; keeps everything else.
  - Sub-folder names as constants: `Materials = "materials"`, `Post = "post"`, `CapCut = "capcut"`, `Recipe = "recipe"`, `Brief = "BRIEF.md"`.
- `PostJob(JournalEntry, KionjoPost, KionjoBrandKit, string postsRoot, ...)`: unchanged signature; `Folder` becomes `PostFolder.Next(EntryFolder)`; renders go to `post/` (main, nomusic, cover.jpg, caption.txt), `capcut/` (clean + `<name>.srt`), `recipe/` (post.json, commands.txt, post.ass, list.txt, fonts); `Refresh` runs first; after success the intermediates (`seg/`, `joined.mp4`, `audio.wav`, `video_nosub.mp4` if present) are deleted, then `PruneOlderVideos`. `OutputMp4` points at `post/<name>_9x16.mp4`.

- [ ] **Step 1: Write the failing tests** (`PostFolderTests`): `EntryFolderUsesDateAndSlug`, `NextCountsUp` (v01, v02), `RefreshLinksMaterialsAndWritesBrief` (two media, one with a notes.md line only: brief contains it), `RefreshListsMissingMedia`, `RefreshRemovesDroppedMaterials`, `PruneDeletesOnlyOlderVideos` (v01 has `x.mp4`, `x.png`, `BRIEF.md`; v02 kept: v01 keeps png+brief, loses mp4; v02 untouched; source footage file still exists after pruning a linked material).
- [ ] **Step 2: Update `PostJobTests`** to the new layout: main file under `post/`, clean + srt under `capcut/`, `recipe/post.json` exists, `seg/` gone, `BRIEF.md` and `materials/a.mp4` exist; a second build writes `v02` and `v01/post/*.mp4` is gone while `v01/recipe/post.json` remains.
- [ ] **Step 3: Run, expect failures.**
- [ ] **Step 4: Implement `PostFolder`, migrate `PostJob`, `PostCommandBuilder` output paths, `PostBuildView` folder link and `PostMenu` reveal.**
- [ ] **Step 5: Run all EditMode tests, expect green.**
- [ ] **Step 6: Commit** `Post folder: vNN with materials (links), post, capcut, recipe and brief; older versions lose their videos`.

### Task 7: Watcher

**Files:**
- Create: `Editor/Progress/KionjoWatcher.cs`, `Editor/Progress/ProgressIntake.cs`
- Modify: `Editor/Settings/KionjoPreferences.cs` (two preferences), `Editor/UI/` Posts tab entry list (a "new progress" dot)
- Test: `Tests/Editor/ProgressIntakeTests.cs`

**Interfaces:**
- Produces: preferences `KionjoPreferences.FootageRoot : string` (key `SeedeXR.Kionjo.FootageRoot`, default `""` = off) and `KionjoPreferences.WatchProgress : bool` (default true).
- Produces: `class ProgressIntake` (pure, clock injected): `ProgressIntake(Func<DateTime> now, Func<string,long> size)`; `void Seen(string path)`; `List<string> Ready()` returns paths whose size was unchanged for 2 s and is above 0, each once.
- Produces: `static JournalEntry ProgressIntake.MatchEntry(IReadOnlyList<JournalEntry> all, string footageFile, string footageRoot)`: the file's parent folder name must parse as `yyyy-MM-dd` under `footageRoot`; candidates are entries with that `created`; prefer the one whose `feature` is a prefix of the file name (`<feature>-`), else the single candidate, else null.
- Produces: `static bool ProgressIntake.Attach(JournalEntry e, string file)`: when the entry does not list the file, appends `JournalMedia { path, role = still for png/jpg else broll, note = "found in footage folder: " + notes line }` through `KionjoJournal.Update`; returns false when already listed.
- `KionjoWatcher` (`[InitializeOnLoad]`): two `FileSystemWatcher`s (journal folder: `entry.json`, subdirectories; footage root: media extensions, subdirectories), events only enqueue paths; `EditorApplication.update` polls `Ready()` at most every 0.5 s, then: footage file -> `MatchEntry` + `Attach`; entry file -> marks it new in the Posts tab and `PostFolder.Refresh(Latest ?? Next, ...)`, plus `KionjoBacklog.DropSuperseded`. Disposed on `AssemblyReloadEvents.beforeAssemblyReload` and `EditorApplication.quitting`. Never renders.

- [ ] **Step 1: Write the failing tests:** `WaitsForStableSize` (size grows at t0, t1; ready only at t1 + 2 s), `ReadyReturnsOnce`, `MatchPrefersFeaturePrefix`, `MatchSingleCandidate`, `MatchNoneOutsideDayFolder`, `AttachAppendsWithNote` (notes line copied), `AttachSkipsListedFile`.
- [ ] **Step 2–4: fail, implement (`ProgressIntake` first, then the watcher plumbing and preferences UI), green.**
- [ ] **Step 5: Manual check in Kionjo-TestProject:** set Footage root to a temp folder, drop a 1 s mp4 into `<root>/<today>/hero-test.mp4` next to an entry dated today with feature `hero`; within ~3 s the entry lists it and `BRIEF.md` of its latest version names it. Record the result in `progress.md`.
- [ ] **Step 6: Commit** `Watcher: new footage and entry changes refresh materials and briefs, no polling loop, never renders`.

### Task 8: Resources: memory check and heavy-work lock

**Files:**
- Create: `Editor/Progress/KionjoResources.cs`, `Editor/Progress/HeavyLock.cs`
- Modify: `Editor/Settings/KionjoPreferences.cs` (`FloorGB` float default 8, `LockFolder` default `<user home>/.kionjo`)
- Test: `Tests/Editor/ResourcesTests.cs`

**Interfaces:**
- Produces: `static class KionjoResources { (double freeCommitGB, double freePhysicalGB)? Free(); bool CanRun(double needGB, double floorGB, (double, double)? free, out string reason); }`. Windows: `GlobalMemoryStatusEx` (`ullAvailPageFile` = free commit, `ullAvailPhys`). Elsewhere `Free()` is null and `CanRun` returns true with reason `"memory unknown on this system"`. Rule: `free >= 2 * need && free - need >= floor`; reason names the numbers ("needs 6.0 GB, 7.2 GB free commit, 8.0 GB must stay free").
- Produces: `sealed class HeavyLock : IDisposable { static HeavyLock TryAcquire(string folder, string owner, double gb, out string holder); }`, file `<folder>/heavy.lock` = JSON `{ owner, pid, gb, since }`, created with `FileMode.CreateNew`; a lock whose pid is not running is stale and replaced; `Dispose` deletes it only if it is still ours. `static string HeavyLock.Describe(string folder)` for UI and agents.

- [ ] **Step 1: Write the failing tests:** `CanRunNeedsTwiceAndFloor` (need 6: free 13.3 -> false, reason contains "13.3"; free 14.5 -> true), `UnknownMemoryRuns`, `LockIsExclusive` (second acquire null, holder names first owner), `StaleLockIsReplaced` (lock file with pid 999999), `DisposeReleases`.
- [ ] **Step 2–4: fail, implement, green.**
- [ ] **Step 5: Commit** `Resources: free memory check (2x need, floor) and a machine-wide heavy-work lock`.

### Task 9: Capture rigs

**Files:**
- Create: `Runtime/Capture/KionjoRigs.cs`
- Test: `Tests/Editor/RigsTests.cs`

**Interfaces:**
- Produces (runtime assembly, namespace `SeedeXR.Kionjo`): `static class KionjoRigs { static readonly string[] All = { "main", "main-vertical", "wide", "orbit", "side", "low", "close" }; static (int w, int h, float vfov) Format(string rig, float mainVFov); static Pose Place(string rig, Vector3 centre, float size, Vector3 mainPos, Quaternion mainRot, float u); }`
  - `size` = largest bounds extent seen so far (caller keeps the max). `u` = 0..1 progress through the capture.
  - `main`/`main-vertical`: the main camera's pose; formats 1920x1080 and 1080x1920 (vertical keeps the main horizontal field of view x 0.6, as a vertical fov). Others: 1920x1080 except `low` and `close` 1080x1920. FOVs: wide 55, orbit 45, side 45, low 50, close 35.
  - Distances (multiples of `size`, measured from `centre`, horizontal direction `back` = main position to centre projected on the floor, reversed): wide 3 + behind main, up 0.4 size; orbit 4 size, angle -60 + 120u degrees, up 0.3 size; side 3 size at +60 degrees; low 2.5 size, down 0.8 size looking up at centre + 0.2 size; close 2.2 size at -25 degrees, down 0.3 size, aimed at the lower half.
  - Every placement looks at the centre (close: centre - 0.35 size up).

- [ ] **Step 1: Write the failing tests:** `NoRigInsideSubject` (for sizes 0.5, 2, 6 and u 0, 0.5, 1: distance from centre > size for every non-main rig), `MainMatchesMain`, `OrbitSweeps` (u 0 and u 1 differ by 120 degrees around centre), `FormatsAreFixed` (vertical rigs 1080x1920), `LowLooksUp` (forward.y > 0).
- [ ] **Step 2–4: fail, implement, green.**
- [ ] **Step 5: Commit** `Rigs: camera placements from the subject's size, never fixed distances`.

### Task 10: Capture at the moment

**Files:**
- Create: `Runtime/Capture/KionjoMultiCapture.cs`, `Runtime/Capture/KionjoAudioCapture.cs`, `Editor/Progress/KionjoCapture.cs`
- Modify: `Editor/Agent/KionjoCommands.cs` (command `capture`)
- Test: `Tests/Runtime/MultiCaptureTests.cs` (PlayMode), `Tests/Editor/CaptureRequestTests.cs`

**Interfaces:**
- Consumes: Tasks 1, 3, 8, 9; `KionjoFrameCapture`, `FfmpegPipeSink`, `KionjoStill`, `KionjoPreferences.EncoderArgs`.
- Produces (runtime): `class KionjoMultiCapture : MonoBehaviour { Transform subject; Camera main; string[] rigs; float seconds, fps, stillsEvery; string outFolder, baseName, ffmpeg; string[] encoderArgs; event Action<KionjoMultiCaptureResult> Done; void Begin(); }` and `class KionjoMultiCaptureResult { bool ok; string error; List<string> videos; List<string> stills; string audioWav; int frames; }`. Per rig: a child camera (`CopyFrom(main)`), a `KionjoFrameCapture` (`lockFrameRate` only on the first, the others with `lockFrameRate = false`, all `targetFrames = ceil(seconds * fps)`), a `FfmpegPipeSink` writing `<outFolder>/<baseName>-<rig>.mp4` with `-c:v libx264 -crf 16 -pix_fmt yuv420p`. In `LateUpdate` (order -100, before the captures) it tracks the subject centre (exponential smoothing, time constant 0.5 s), keeps the max renderer bounds extent, places each rig with `KionjoRigs.Place`. Stills: every `stillsEvery` s, `KionjoStill.CapturePng` per rig to `<outFolder>/stills/<baseName>-<rig>-t<sss.s>s.png`.
- Produces (runtime): `class KionjoAudioCapture` wrapping `AudioRenderer.Start/Render/Stop`, writing a 16-bit WAV of `GetSampleCountForCaptureFrame` samples per frame; failure leaves `audioWav` null and adds a warning.
- Produces (editor): `static class KionjoCapture { static void Run(string entryFolder, KionjoCaptureRequest r, Action<KionjoCaptureRequest> done); static void RunFromCommandLine(); }`. Steps: `KionjoBacklog.IsCurrent` else `dropped`; `KionjoResources.CanRun(estimate, floor)` (estimate GB = 2.5 + 0.15 x rigs) and `HeavyLock.TryAcquire` else stays `pending` with the reason; open scene, resolve `subject` (`GameObject.Find` path, else the largest renderer in the scene) and `Camera.main` (else first enabled camera) else `failed`; record `version` = `git rev-parse --abbrev-ref HEAD`@`git rev-parse --short HEAD` and `dirty` = `git status --porcelain` non-empty (skipped with a warning when git is absent); enter Play mode; add `KionjoMultiCapture`; on Done: exit Play mode, mux the WAV into each video (`-c:v copy -c:a aac -b:a 160k -shortest`, then replace), move outputs into `FootageNotes.DayFolder(footageRoot or <output>/captures, today)` named `<feature>-<label>-<rig>.mp4`, write one notes line per file ("<rig> angle of <label>, <seconds> s, frame-locked with the other angles"), attach media through `KionjoJournal.Update` (main as role `hook`, others `broll`, stills `still`), set `taken` with `outputs`; always release the lock. `RunFromCommandLine` reads `-kionjoCapture <request.json>` (`{ "entry": "<folder>", ...request fields }`), runs, writes `<request>.result.json` `{ state, reason, outputs, pid }` and calls `EditorApplication.Exit(state == "taken" ? 0 : 1)`.
- Command: `unity command kionjo capture --request <file.json>` starts `Run` and returns `{ ok, message, state }` at once; progress goes to `state.json` `job` as for renders.

- [ ] **Step 1: Write the failing EditMode tests** (`CaptureRequestTests`): `ParsesRequestFile` (entry + fields), `DroppedWhenSuperseded` (Run on a superseded entry sets `dropped` without opening a scene), `PendingWhenNoRoom` (inject `free` 7 GB: stays `pending`, reason contains "GB"), `FailsCleanlyWithoutSubject` (scene with no renderer and no camera: `failed`, reason names the missing part, no files in the output folder, lock released).
- [ ] **Step 2: Write the failing PlayMode test** (`MultiCaptureTests.CapturesFrameLockedRigs`): scene with a camera, a rotating cube and an `AudioSource` playing a generated sine clip; capture 1 s at 30 fps with rigs `main`, `orbit`, `close`, stills every 0.5 s; asserts 3 videos exist, each `ffprobe` frame count 30, each has an audio stream, 6 stills exist, `KionjoRigs` distance check holds for the camera positions logged.
- [ ] **Step 3: Run both, expect failures.**
- [ ] **Step 4: Implement runtime capture, audio, editor runner, command.**
- [ ] **Step 5: Run EditMode and PlayMode, expect green.**
- [ ] **Step 6: Batch check in Kionjo-TestProject:** write a request for the PlayMode test scene, run `Unity.exe -batchmode -projectPath "$TP" -executeMethod SeedeXR.Kionjo.EditorTools.KionjoCapture.RunFromCommandLine -kionjoCapture <file> -logFile <log>` (no `-quit`); expect exit 0, result json `taken`, the editor PID gone, footage files + notes lines present.
- [ ] **Step 7: Commit** `Capture at the moment: one Play mode run filmed by several frame-locked rigs with sound and stills, into the footage folder and the entry`.

### Task 11: Agent guide, release 0.2.0

**Files:**
- Modify: `Documentation~/AgentGuide.md` (new fields, capture request, "For agents that build", "For a documentarian agent", heavy lock), `README.md` (Posts and Progress in one paragraph each), `package.json` (`0.2.0`, description adds "posts from your progress"), `CHANGELOG.md` (0.2.0), `Editor/KionjoVersion`/`Runtime/KionjoVersion.cs` if it holds the number.
- [ ] **Step 1: Write the docs.** The guide's rules: build agents write or update the entry at each checkpoint and run a capture while the version exists, else add a `pending` request; never reuse an earlier step's footage names; take the heavy lock for their own heavy jobs; documentarian agents act only on `KionjoBacklog.Pending`, never film a non-current step, and delete only what they created.
- [ ] **Step 2: Full EditMode + PlayMode run, expect green; record totals in `progress.md`.**
- [ ] **Step 3: Privacy scan:** `git diff main...posts` must contain no personal names, accounts, absolute personal paths, or studio strategy (grep for the maintainer's name, drive-letter paths outside tests' temp dirs, `@` handles). Fix any hit.
- [ ] **Step 4: Fresh-context code review** of `main...posts` (correctness and requirement gaps only); fix findings with tests.
- [ ] **Step 5: Merge `posts` into `main` (no squash of phase 1 history), tag `v0.2.0`, push `main`, the tag, and delete the remote `posts` branch** (the maintainer asked for the push on 2026-11-17).

### Task 12: Use it in the production project as an ordinary user

**Files (consuming project, private):** `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings/KionjoPosts.asset`.
- [ ] **Step 1: Update the package** with Package Manager (git URL, `#v0.2.0`), let it resolve, compile check (no errors).
- [ ] **Step 2: Set preferences** as a user would through the window: Footage root, Posts output folder, floor 8 GB, lock folder.
- [ ] **Step 3: Acceptance:** with the other sessions told and memory free, run one capture of the feature's dev scene through `kionjo capture`, then build a post from that entry; check: footage files + notes lines + entry media, `vNN/{materials,post,capcut,recipe,BRIEF.md}`, no frame folders left, Unity PIDs gone. Note every bug met as a user in `progress.md`, fix in the package (back to the task that owns it), release `0.2.x`.
- [ ] **Step 4: Retire the ad hoc B-roll script** once the Kionjo capture covers it (keep a note where it was).
