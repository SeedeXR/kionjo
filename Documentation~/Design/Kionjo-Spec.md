# Kionjo: design spec, v0.1

Date: 2026-10-23. Status: written overnight by the planner for Bernard's morning review. Every
open decision was closed with the recommended option and recorded in section 12 (choice, why,
alternative). Bernard redirects anything he disagrees with; nothing here is final until he reads it.

Kionjo (Swahili for "trailer") is an open source Unity editor package that turns the pipeline that
produced Baraza's trailer 1 into a tool: perform a shot (in a headset or with any camera), record
the performance, replay it frame-locked in the editor and render it offline at high quality, then
cut a draft with ffmpeg (voice, subtitles, music with ducking, SFX at recorded events, cards) and
export MP4, SRT and a Premiere timeline.

Companion documents: `Kionjo-v0.1-Plan.md` (the implementation plan, same folder); the source
plan `Baraza VR/Design/Trailer/TrailerTool-Plan.md`; the reference cut
`Baraza VR/Design/Trailer/tools/cut/cut_trailer_v1.sh`.

## 1. Problem

Shooting a trailer for a VR experience is slow and manual. Flat-screen teams capture 4K on the
device; VR teams cannot, because the headset caps image quality while the footage must still feel
like a person moving a real head. Trailer 1 of Baraza (2026-10-04 to 10-22) proved a pipeline that
works: perform in the headset, record head, hands and events, re-render offline, cut with ffmpeg.
It took eleven drafts of terminal back and forth and a rig full of Baraza-specific hooks. Kionjo
makes the next trailer quick and makes the pipeline available to any Unity team.

## 2. Users

**VR developer (primary).** Has an XR rig (Meta XR, OpenXR, XRI, anything). Wants to perform
shots in the headset over Link, then get 4K footage with human head motion. Needs: record any set
of transforms plus game events, replay with the headset unplugged, render offline, cut.

**Flat-screen developer.** Has a normal camera, maybe Cinemachine or Timeline. Wants the same
storyboard, shoot, render and cut flow. Their camera is just another transform to record (a WASD
fly-through recorded in Play mode and re-rendered at 4K), or a live camera that already moves on
its own (Cinemachine, animation) and only needs frame-locked rendering.

**AI agent (later).** Drives the same actions through Unity CLI commands and a state file. The
core never needs it.

Kionjo never needs Baraza's editor. It is developed in its own test project and consumed by
Baraza later through a git URL (follow-up).

## 3. Concepts

| Concept | Meaning |
|---|---|
| Storyboard | A ScriptableObject asset: ordered shots, music cues, subtitle style, render and cut settings, output folder. One per trailer. |
| Shot | One card in the storyboard. Has a type (Performance, Live camera, Still, Card), a target length, voice lines, SFX cues, staging data (hide and show lists, a stage command), a selected take, in and out points, notes and a thumbnail. Cards (black, text, image) are shots too, so the opening line and the end cover sit in the same list. |
| Take | One recorded performance of a shot: a folder with `take.json` (small: meta, tracks, events) and `frames.bin` (compact binary poses). Several takes per shot; one is selected. |
| Track | One Transform recorded per frame (head camera, left hand, right hand, anything the user adds). |
| Event | A timestamped named mark in a take (`seal`, `speak`, `button:...`). Raised by game code through `KionjoEvents.Mark` or a `KionjoEventRelay` wired to a UnityEvent. |
| Render | A frame-locked offline video of one take (or of a live camera), produced by Kionjo's own capture: camera to RenderTexture, GPU readback, raw frames piped into ffmpeg. |
| Cut | A draft of the whole trailer assembled by ffmpeg from renders, device captures and cards, with voice, subtitles, music, SFX. Exports MP4 + SRT + FCP7 XML. Drafts are numbered like takes. |
| Animatic | A cut built from thumbnails held for their target lengths, with voice and music, before anything is shot. |
| Job | A long running operation (render, cut, animatic) with progress and cancel. One at a time. |

## 4. Architecture

```
Runtime (SeedeXR.Kionjo, no dependencies)           Editor (SeedeXR.Kionjo.EditorTools)
  KionjoRecorder      tracks -> take folder          KionjoStoryboard (asset) + KionjoShot
  KionjoReplayer      take -> transforms, events     KionjoWindow (Storyboard | Shoot | Cut)
  KionjoEvents        Mark(name, payload)            ShootController + StageVerifier
  KionjoCameraSource  which camera, hooks            RenderJob -> KionjoFrameCapture -> ffmpeg pipe
  KionjoFrameCapture  RT -> readback -> sink         CutPlanBuilder -> CutCommandBuilder -> CutJob
  take format         KionjoTakeMeta/Writer/Data     SrtWriter, Fcp7XmlWriter, SilenceDetect
  IKionjoStageHandler, IKionjoRecordingHook          FfmpegLocator, KionjoProcess, KionjoJobs
                                                     KionjoPaths, settings providers, icons
Agent bridge (SeedeXR.Kionjo.Agent.EditorTools, compiled only when com.unity.pipeline is present)
  [CliCommand] kionjo_* commands, state.json writer, storyboard JSON import/export
```

Principles:

1. **Deterministic core.** Plain C# plus ffmpeg. Every ffmpeg invocation is built as an argument
   list (never a shell string) and unit-tested as strings. No AI anywhere in the core.
2. **No hard dependencies.** No Meta XR, OpenXR, XRI, Cinemachine, Recorder, Timeline, uGUI.
   Tracks are plain Transforms; the camera is a plain Camera; events are strings.
3. **Replay never touches live hardware.** The replayer moves transforms and raises events. It
   never presses a button, opens a microphone or drives an interactor. The game decides what an
   event means through `KionjoReplayer.onEvent`.
4. **Headless first.** Everything except "a human in a headset" runs under `unity test` or the
   CLI. The window is smoke-tested by opening it in EditMode.
5. **Boring over clever.** One window, one job at a time, folders you can read with Explorer,
   a `commands.txt` beside every cut so the ffmpeg steps can be re-run by hand.

## 5. Data model

### 5.1 Storyboard (editor asset)

```csharp
namespace SeedeXR.Kionjo.EditorTools
[CreateAssetMenu(menuName = "Kionjo/Storyboard", fileName = "Storyboard")]
[Icon("Packages/com.seedexr.kionjo/Editor/Icons/kionjo.png")]
public class KionjoStoryboard : ScriptableObject
{
    public string title = "Trailer";
    public string outputFolder = "Kionjo";          // project-relative, outside Assets
    public float targetSeconds = 60f;
    public List<KionjoShot> shots = new();
    public List<KionjoMusicCue> music = new();
    public float musicCrossfadeSeconds = 2f;
    public KionjoDucking ducking = new();           // threshold 0.05, ratio 3, attack 40, release 700, enabled
    public KionjoLoudness loudness = new();         // I -14, TP -1.5, LRA 11
    public KionjoSubtitleStyle subtitles = new();   // fontPath, fontName, size 15, colour "&H00EDEDED", outline 0, marginV 12
    public KionjoRenderPreset renderPreset = KionjoRenderPreset.Preview720p30;
    public KionjoCutPreset cutPreset = KionjoCutPreset.Preview720p30;
    public KionjoQualityOverride renderQuality = new(); // Tier 2: quality level name, lodBias, mip limit, aniso, skin weights
    // API (all with Undo.RecordObject + EditorUtility.SetDirty):
    public KionjoShot AddShot(KionjoShotType type = KionjoShotType.Performance);
    public KionjoShot Duplicate(int index);        // new id, code suffixed "copy"
    public void RemoveAt(int index);
    public void Move(int from, int to);
    public float PlannedSeconds();                  // sum of shot planned lengths
}
```

```csharp
public enum KionjoShotType { Performance, LiveCamera, Still, Card }
public enum KionjoOutputMode { OfflineRender, DeviceCapture, OrganiseOnly }
public enum KionjoCardKind { Black, Text, Image }
public enum KionjoTrimMode { Manual, Silence, Words }
public enum KionjoRenderPreset { Preview720p30, HD1080p60, UHD2160p60 }
public enum KionjoCutPreset { Preview720p30, HD1080p30, UHD2160p60 }

[Serializable] public class KionjoShot
{
    public string id;                   // GUID string, assigned on creation, never changes
    public string code = "S01";         // short label used in folder names (sanitised)
    public string name;                 // "Crane down on the dome"
    public string beat;                 // "AND", "BUT", "THEREFORE", free text
    public string location;             // "outside", "inside", free text, shown as a tag
    public KionjoShotType type;
    public KionjoOutputMode outputMode;
    public float targetSeconds = 4f;    // planned length; used by animatic, cards, stills, live camera
    public string selectedTake;         // take folder name, e.g. "S10_take03"; empty = newest
    public float inPoint;               // seconds into the selected render or device capture
    public float outPoint;              // 0 = end
    public float fadeIn, fadeOut;       // seconds, video fade from and to black
    public float horizontalFov;         // 0 = keep the camera's own lens (Baraza POV used 100)
    public Rect crop;                   // normalised; zero rect = none (Baraza S04 cropped left)
    public string stageCommand;         // passed to IKionjoStageHandler.Stage
    public List<string> hidePaths = new(), showPaths = new();   // hierarchy paths, "/Root/Child"
    public List<KionjoVoiceLine> voice = new();
    public List<KionjoSfxCue> sfx = new();
    public string notes;
    public string deviceCapturePath;    // OutputMode.DeviceCapture: absolute path of the headset mp4
    public KionjoCardKind cardKind;     // type == Card
    public List<string> cardLines = new();
    public string cardImagePath;
    public float cardPushIn = 1.04f;    // zoompan end scale for image cards, 1 = none
    public string thumbnailPath;        // under <root>/thumbs/, PNG; empty = none
}
[Serializable] public class KionjoVoiceLine { public string audioPath; public string subtitle; public float at; /* seconds after the shot's film start */ public float trimIn; public float trimOut; /* 0 = end */ public KionjoTrimMode trimMode = KionjoTrimMode.Silence; public string overrideTags; /* "{\an5}{\fad(500,1100)}" */ public float fadeIn = 0.03f, fadeOut = 0.25f; }
[Serializable] public class KionjoSfxCue { public string audioPath; public float at; /* seconds after shot film start, used when eventName is empty */ public string eventName; /* film time = take event time, mapped through in/out */ public float volume = 0.9f; }
[Serializable] public class KionjoMusicCue { public string audioPath; public float trimIn; public float trimOut; /* 0 = end */ public float at; /* absolute film time */ public float fadeIn = 1.5f, fadeOut = 2f; public float volume = 0.5f; }
```

Scene references are hierarchy paths, not object references, because a ScriptableObject cannot
reference scene objects (Baraza kept its shot list on a scene component for that reason). Paths are
resolved against the live scene at staging time, which is exactly the verification trailer 1
taught (S07: the saved hide list was empty, the tool must say so).

### 5.2 Take (runtime, on disk)

Folder `<root>/takes/<ShotCode>/<ShotCode>_takeNN/`:

- `take.json`: `KionjoTakeMeta` via `JsonUtility`: `version`, `shotId`, `shotCode`, `takeNumber`,
  `startedAt` (ISO 8601), `durationSeconds`, `frameCount`, `nominalFps`, `tracks[]`
  (`name`, `path`, `recordScale`), `events[]` (`t`, `name`, `payload`), `notes`.
- `frames.bin`: little-endian. Header `KJTK` (4 bytes), `u16 version = 1`, `u16 trackCount`,
  `u32 frameCount` (patched on close; a reader that finds 0 derives the count from the file
  length). Per frame: `f32 t`, then per track `u8 flags` (bit 0 active, bit 1 has scale),
  `f32 x y z`, `f32 qx qy qz qw`, and `f32 sx sy sz` when bit 1 is set. Three tracks without
  scale = 91 bytes per frame, 8.2 KB/s at 90 Hz, about 1 MB for a 120 s take. Baraza's JSON was
  0.5 to 1 MB per second and a 96 s take froze shot selection; selection in Kionjo reads only
  `take.json`.
- `thumb.png` (optional, from a render), `audio.wav` (Tier 2, game audio of the render).

Time stamps are `Time.time - startTime` (scaled game time), sampled in `LateUpdate` at execution
order 32000 so XR rigs have written their poses. The recorder streams frames to disk as it goes;
memory stays flat. `maxTakeSeconds` (default 120) auto-stops a take and marks the event `cap`.

### 5.3 Render and cut outputs

```
<ProjectRoot>/<outputFolder>/                      default "Kionjo", add it to .gitignore
  takes/<ShotCode>/<ShotCode>_takeNN/              take.json, frames.bin, thumb.png
  renders/<ShotCode>/<ShotCode>_takeNN_<preset>.mp4 (+ .json sidecar: take, fps, size, range)
  captures/<ShotCode>/                             imported device captures (OutputMode.DeviceCapture)
  thumbs/<ShotId>.png                              card thumbnails (storyboard references by path)
  cut/draftNN/                                     seg/*.mp4, list.txt, subs.srt, audio.wav,
                                                   <Title>_draftNN.mp4, <Title>_draftNN.xml,
                                                   commands.txt, cut.log, fonts copied in
  animatic/animaticNN/                             same layout as a cut
  state.json                                       Tier 3: what the agent watches
```

One storyboard, one root. Storyboard assets live in `Assets/`; everything heavy lives outside.

## 6. Workflows

### 6.1 Storyboard

Window > Kionjo. Empty state: "No storyboard open. Create one, or drop a Storyboard asset here."
Create writes `Assets/Kionjo/Storyboard.asset` (user picks the path) and opens it. The Storyboard
tab shows the shot list (cards with thumbnail, code, name, type and location tags, planned length,
state chip), an inspector for the selected shot, and a timeline bar (segments proportional to
planned length, total versus target coloured when over). Add, duplicate, remove, drag to reorder,
all undoable. Shot thumbnail: "Capture thumbnail" grabs one frame from the camera source (edit or
Play mode) through a RenderTexture; renders overwrite it with their middle frame.

### 6.2 Animatic (Tier 2)

Cut tab > Animatic. Builds a cut where every shot is its thumbnail (or a text card with its code
and name when there is no thumbnail) held for `targetSeconds`, with voice lines, subtitles and
music exactly as the real cut would place them. Output `animatic/animaticNN/`. Watching it before
shooting is how the team agrees on pacing.

### 6.3 Shoot

Shoot tab. Requires Play mode (button "Enter Play mode"). Flow per shot:

1. **Select** a shot card. Kionjo shows its staging data and the takes already on disk.
2. **Stage.** Resolve `hidePaths` and `showPaths` against the live scene and apply them; run every
   `IKionjoStageHandler.Stage(shotId, stageCommand)` coroutine in the scene; wait for them.
3. **Verify (Tier 2).** `StageVerifier` reports lines with a severity: every hide path resolved
   and inactive, every show path resolved and active, camera source found, recorder present with
   at least one track, no XR device active while rendering, "hide list is empty" as information.
   Any error = **Not ready** with the list; otherwise **Ready**.
4. **Record.** Space (window focused), the Record button, a CLI command, or the game's own call to
   `KionjoRecorder.ToggleTake()`. The recorder opens `takes/<Code>/<Code>_takeNN/`, calls
   `IKionjoRecordingHook.OnKionjoRecordingStarted` and `KionjoCameraSource.onRecordingStarted`,
   streams frames and events, and stops on Space, on the cap, or on Stop.
5. **Takes list.** Each take: number, length, event count, Select (radio), Replay, Reveal in
   Explorer, Delete. Replay drives the tracks from the file (headset unplugged; a warning shows
   if `XRSettings.isDeviceActive`).
6. **Done** advances to the next shot and marks this one `Shot`. The tool, not the terminal, says
   "shot 7 done".

Baraza's lesson about state that survives story jumps (record S10 last) is the user's call; the
`notes` field and the free order of cards carry it.

### 6.4 Render

Shot inspector > Render selected take (or Cut tab > Render all missing). `RenderJob` stores its
spec in `SessionState`, enters Play mode if needed, and on `EnteredPlayMode` resumes: stage the
shot, load the take into the `KionjoReplayer`, set `Time.captureDeltaTime = 1 / fps`, point the
camera source's camera at a RenderTexture of the preset size, override the lens when
`horizontalFov > 0`, and run `KionjoFrameCapture`: after `WaitForEndOfFrame` each frame,
`AsyncGPUReadback` the texture and push the RGBA bytes to an `IKionjoFrameSink`. The sink is an
ffmpeg process reading `-f rawvideo -pix_fmt rgba -s WxH -r fps -i -` on stdin (a bounded queue
and a writer thread; `-vf vflip` is added when `SystemInfo.graphicsUVStartsAtTop`), encoding to
`renders/<Code>/<Code>_takeNN_<preset>.mp4`. Range: whole take for previews; selected range plus
one second handles for HD and UHD (`renderWholeTake` toggle). At the end: close stdin, wait,
restore camera and `captureDeltaTime = 0`, write the sidecar, make the thumbnail, exit Play mode
if Kionjo entered it. Progress = frames / total; cancel kills ffmpeg and restores the camera.

Live camera shots render the camera as it moves on its own for `targetSeconds` (Cinemachine,
Timeline, animation: deterministic under frame lock). Still shots render one frame held for
`targetSeconds` (an image card made from the still). Audio from the game during a render is Tier 2
through `UnityEngine.AudioRenderer` (mutes live output, renders in step with the capture clock).

Render presets: Preview720p30 (1280x720, 30, `libx264 crf 23 veryfast`), HD1080p60, UHD2160p60
(3840x2160, 60, `libx264 crf 16 medium`). Encoder arguments are a Preferences string so a user
with NVENC can swap them.

### 6.5 Cut

Cut tab. Dependency line at the top: "ffmpeg 7.1 found at ..." (green) or "ffmpeg not found. Set
the path in Preferences > Kionjo" (red, link). Build draft:

1. `CutPlanBuilder.Build(storyboard, preset, probe)` walks the shots in order. Per shot it picks
   the source: the selected (or newest) render for OfflineRender, `deviceCapturePath` for
   DeviceCapture, nothing for OrganiseOnly (skipped with a warning), a generated card for Card,
   the thumbnail for Still. Segment length = out minus in, or `targetSeconds` for cards and stills.
   Film start = running sum. Voice lines land at `filmStart + at`; subtitles span the trimmed line
   length (durations from ffprobe through `IMediaProbe`, faked in tests). SFX cues bound to an
   event land at `filmStart + (eventTime - inPoint)` when the event falls inside the range, else
   they are dropped with a warning. Music cues are absolute; consecutive cues crossfade over
   `musicCrossfadeSeconds`. Shots without a source are skipped and listed.
2. `CutCommandBuilder.Build(plan, cutFolder)` emits the ffmpeg steps of `cut_trailer_v1.sh`,
   each with `WorkingDirectory = cutFolder` and relative file names for anything that appears
   inside a filtergraph (subtitles file, fonts), so Windows paths with spaces, brackets and drive
   colons never need escaping:
   - one trim and scale command per segment (`-ss in -t dur -i src -an -vf crop?,scale,fps,
     format=yuv420p,fade?`), black cards from `lavfi color`, text cards with one `drawtext` per
     line and the v1 fade-alpha expressions, image cards with `zoompan` push-in;
   - `concat` demuxer over `list.txt` to `video_nosub.mp4`;
   - `subtitles=subs.srt:fontsdir=.:force_style=...` to `video.mp4` (skipped when no cues);
   - one `filter_complex` audio graph: voice (`aformat`, `afade` in and out, `adelay`, `amix`,
     `asplit` into mix and sidechain key), music (`atrim`, `asetpts`, `acrossfade`, `afade`,
     `volume`, `adelay`), SFX (`adelay`, `amix`), `sidechaincompress` ducking, final `amix` and
     `loudnorm`, to `audio.wav`;
   - mux: `-c:v copy -c:a aac -b:a 192k -shortest` to `<Title>_draftNN.mp4`.
3. `CutJob` runs the commands in order with `-progress pipe:1 -nostats`, parses `out_time_ms`
   for the progress bar, writes `commands.txt` (shell-quoted, re-runnable), `cut.log` (stderr),
   `subs.srt` and `<Title>_draftNN.xml` (Tier 2). Cancel kills the current process. A failing
   step stops the job, keeps the folder, and shows the last 20 lines of stderr.

Voice trimming (Tier 2): `KionjoTrimMode.Silence` runs `silencedetect=noise=-55dB:d=0.1` (never
-35 dB, which clipped word tails) and trims to the first speech start and last speech end;
`Words` runs `Tools~/kionjo_words.py` (faster-whisper, audio decoded through ffmpeg into float32,
the PyAV route failed in trailer 1) and lets the user pick first and last word. Both fall back to
Manual when the tool is missing, with a visible note.

### 6.6 Output modes

Per shot: **OfflineRender** (above), **DeviceCapture** (the user drops the headset or device mp4;
"Import" copies it to `captures/<Code>/`; the cut trims it with in and out, no render), and
**OrganiseOnly** (Kionjo only copies the files it is given into the layout and writes
`captures/index.md`; the shot is excluded from the cut).

## 7. UI

One `EditorWindow`, `KionjoWindow`, under `Window/Kionjo` (Unity puts its own panels under
Window; Recorder, Timeline and Profiler live there). UI Toolkit, UXML and USS under `Editor/UI/`,
colours only through Unity's editor theme variables (`--unity-colors-*`) so light and dark skins
come for free; the few custom colours (Record red, Ready green, Not ready amber) are USS variables
redefined for `.unity-theme-env--dark`. `[EditorWindowTitle(title = "Kionjo", icon =
"Packages/com.seedexr.kionjo/Editor/Icons/kionjo.png")]`; Unity picks `d_` and `@2x` variants.

Layout: a toolbar (storyboard `ObjectField`, New, tab buttons Storyboard | Shoot | Cut, Help) and
a status bar (ffmpeg state, current job progress bar with Cancel).

**Storyboard tab.** Left: `ListView` bound to `shots` (`reorderable`, `reorderMode = Animated`,
`fixedItemHeight = 64`, custom card item: 96x54 thumbnail, code and name, type and location
tags, planned length chip, state chip), footer buttons Add shot, Add card, Duplicate, Remove.
Right: inspector for the selected shot (fields bound through `SerializedObject`; hide and show
path lists with an `ObjectField` drop zone that converts a dropped scene object to its hierarchy
path; voice and SFX cue lists; a Render selected take button). Bottom: timeline bar. States:
no storyboard (empty state with Create and drop zone), empty storyboard ("Add your first shot"),
normal. Tooltips on every field. Keyboard: Delete removes, Ctrl+D duplicates, Ctrl+Up and
Ctrl+Down move, F2 renames.

**Shoot tab.** Not in Play mode: explanation plus "Enter Play mode". In Play mode: shot header
(code, name, type), Stage button, verification list (icon per line), Ready or Not ready badge,
Record button (large, red while recording, elapsed and cap shown), takes list, Done. Space
toggles recording while the window has focus (`[Shortcut("Kionjo/Toggle Take",
typeof(KionjoWindow), KeyCode.Space)]`), Enter is Done.

**Cut tab.** Cut preset, Render all missing, Build animatic (Tier 2), Build draft, Open last
draft, Open folder, warnings list from the last plan (skipped shots, dropped cues), log view.
States: ffmpeg missing (every build button disabled with the reason), no shots with sources
("Nothing to cut yet"), building (progress, Cancel), done (links), failed (stderr tail).

Preferences > Kionjo (user, `EditorPrefs`): ffmpeg path, python path, encoder arguments, open
the folder after a cut. Project Settings > Kionjo (`ScriptableSingleton`,
`ProjectSettings/KionjoSettings.asset`): default output folder, default presets.

Undo: field edits go through bindings (undoable by Unity); list operations call
`Undo.RecordObject(storyboard, ...)` first; the window listens to `Undo.undoRedoEvent` to refresh
derived views (timeline bar, chips). No blocking modal dialogs: errors show in the status bar and
tab bodies; the only `EditorUtility.DisplayDialog` is "Delete take?".

## 8. Icon

Concept: **a film frame that is also a visor.** A rounded rectangle outline (the frame) with two
filled circles side by side inside it (the lenses of a headset, which also read as eyes looking
out of the frame). Flat, single colour, no gradients, 1.5 px strokes at 16 px. It ties trailer
(the frame) to VR (the visor) and stays legible at 16 px because it is three shapes. Source
`Editor/Icons/kionjo.svg`; exports `kionjo.png` (16x16, grey `#555555` for the light skin),
`kionjo@2x.png` (32x32), `d_kionjo.png` and `d_kionjo@2x.png` (grey `#C4C4C4` for the dark
skin), plus `kionjo-asset.png` 64x64 for the storyboard asset icon through `[Icon]`. Exports are
rendered from the shapes with PIL at 4x and downsampled, so no SVG renderer is needed on the
executor's machine; the SVG is the source of truth for a designer later.

## 9. Extensibility points

- `KionjoCameraSource` (MonoBehaviour): `Camera camera`; UnityEvents `onRecordingStarted`,
  `onRecordingStopped`, `onRenderStarted`, `onRenderStopped`. `KionjoCameraSource.Resolve()`
  returns the first source's camera or `Camera.main`.
- `IKionjoRecordingHook` (interface on any MonoBehaviour): `OnKionjoRecordingStarted(string
  takeFolder)`, `OnKionjoRecordingStopped(string takeFolder)`; for teams with their own capture
  (a device recorder, a replay buffer).
- `IKionjoStageHandler`: `IEnumerator Stage(string shotId, string stageCommand)`; how a game
  jumps to a beat, opens a panel, seeds its random layout. Baraza's `TrailerStory` becomes one.
- `KionjoEvents.Mark(string name, string payload = null)` and `KionjoEventRelay` (UnityEvent
  target with a fixed name, optional payload argument) for recording game moments.
- `KionjoReplayer.onEvent (UnityEvent<string, string>)` and `EventFired` (C# event) for turning
  recorded moments back into UI states on replay (never real presses).
- `IKionjoFrameSink`: anything that wants the raw frames (a PNG writer, a different encoder).
- Tracks: any Transform; add hand joints as extra tracks; scale optional per track.
- Agent bridge: `[CliCommand]` methods and `state.json` (Tier 3).

## 10. Testing strategy

- EditMode tests (`Tests/Editor`): take format round trip and truncation, replay math (lerp,
  slerp, hold on inactive, cut rule, stabilisation), naming and paths, storyboard operations with
  Undo, ffmpeg argument lists and filtergraphs as exact strings, SRT text, FCP7 XML, cut plan
  film-time math pinned to the trailer 1 numbers, silence parser on canned stderr, stage
  verifier on a temporary hierarchy, window opens and has its named elements, icons load.
- PlayMode tests (`Tests/Runtime`): recorder on fake transforms (frames, events, cap, header
  patch), replayer frame lock (pose at frame k equals sample at k/fps), event firing, restore of
  `captureDeltaTime` on stop, frame capture of a coloured scene through the ffmpeg pipe (frame
  count by ffprobe, orientation by sampling the top pixel), the Tier 1 end to end scenario.
- Headset pass (Bernard, `Documentation~/HeadsetPass.md`): record over Link with real head and
  hands, unplug, replay and render, compare motion. Marked "needs headset pass" in the plan.

Run: `unity test "<TestProject>" --mode EditMode --filter SeedeXR.Kionjo` and `--mode PlayMode`.
The package is consumed by the test project through `"com.seedexr.kionjo": "file:../../kionjo"`
and listed in `testables`, otherwise Unity does not compile package tests.

## 11. Verified facts the design rests on

All checked 2026-10-23 against 6000.0 docs, UnityCsReference 6000.0, local package sources or the
installed tools (planner's research agent and direct CLI calls):

- `ListView.reorderable`, `reorderMode` (`Simple`, `Animated`), `itemIndexChanged : Action<int,int>`,
  `showAddRemoveFooter`, `headerTitle`; a `bindingPath` to an array makes reorder and footer add
  and remove write through `SerializedProperty.MoveArrayElement` and `ApplyModifiedProperties`
  (undoable). Manual page "Bind to a list with ListView".
- `EditorWindowTitleAttribute(title, icon)`; icons must be PNG; `d_` prefix for the dark theme,
  `@2x` for HiDPI, same folder, nearest variant fallback. `EditorGUIUtility.IconContent` applies
  the same rules to a `Packages/...` path (source: `EditorGUIUtility.LoadIcon`).
  `UnityEngine.IconAttribute(path)` for MonoBehaviour and ScriptableObject icons.
- Unity Recorder 5.1.7 is the 6000.0 line, Play mode only, sets `captureDeltaTime` itself. Not
  used (section 12), so there is no conflict.
- `Time.captureDeltaTime`: `Time.time` advances by it regardless of real time; `unscaledTime`
  unaffected. `AsyncGPUReadback.Request(Texture, mip, TextureFormat, callback)`; check
  `hasError`. `Camera.targetTexture` works in URP Play mode without extra components.
- `UnityEngine.AudioRenderer` exists in 6000.0: `Start`, `Stop`, `GetSampleCountForCaptureFrame`,
  `Render(NativeArray<float>)`; mutes live output.
- asmdef `versionDefines` `{name, expression, define}`, bare version means "at least";
  `defineConstraints` supports `!` and `||`; resource name `Unity` for the editor version.
- `com.unity.pipeline` 0.8.0-exp.1: `Unity.Pipeline.Commands.CliCommandAttribute(name,
  description)` with `Tags`, `CliArgAttribute(name, description)` with `Required`; public static
  methods returning string, number or object; asmdef reference `Unity.Pipeline.Attributes`.
- `Undo.RecordObject`, `Undo.undoRedoEvent (UndoRedoInfo)`, `BindingExtensions.Bind`,
  `TrackSerializedObjectValue`. `ShortcutAttribute(id, Type context, KeyCode)`.
- `-nographics` gives a Null graphics device: never pass it for renders. `unity test --mode
  PlayMode` is the supported headless Play mode route; whether it adds `-nographics` could not be
  read from the CLI binary, so the render test asserts a non-black frame and the plan has a
  fallback (`unity command run_tests` against the open test editor).
- Installed: Unity CLI 1.0.0-beta.13, editors 6000.0.70f1 / 6000.3.8f1 / 6000.4.2f1 / 6000.6.6f1,
  ffmpeg 7.1 (gyan full build) with `subtitles`, `sidechaincompress`, `acrossfade`,
  `silencedetect`, `loudnorm`, `zoompan`, `drawtext`, `adelay`, `amix`, `concat`; Python 3.12.6
  with faster-whisper 1.2.1 and Pillow 12.3. URP template id for `unity projects create` on
  6000.0.70f1: `com.unity.template.urp-blank` (17.0.12).
- Hadithi pattern: repo root is the package, `package.json` with `"unity": "6000.0"`, author
  SeedeXR, MIT `LICENSE.md`, Keep a Changelog `CHANGELOG.md`, asmdef `SeedeXR.X` and
  `SeedeXR.X.Editor` with `rootNamespace`, editor namespace `SeedeXR.X.EditorTools` (avoids the
  `Editor` type clash), README with Install via git URL. Hadithi has no tests; Kionjo adds the
  standard `Tests/Editor` and `Tests/Runtime` layout.

Side finding for Bernard (not in scope): Baraza's `Packages/manifest.json` asks for URP 17.4.0
while `Library/PackageCache` resolves 17.0.4; CLAUDE.md says 17.0.4.

## 12. Decisions

| # | Decision | Choice | Why | Alternative considered |
|---|---|---|---|---|
| 1 | Storyboard storage | ScriptableObject asset in `Assets/`, scene references as hierarchy paths | An asset survives scene changes and ships with the plan; paths make "verify against the live scene" natural | Shot list on a scene component (Baraza): simple refs, but one scene per trailer and no asset to share |
| 2 | Take format | `take.json` + `frames.bin` (binary poses, streamed) | 10x smaller than JSON frames, selection reads only the meta, no memory growth | Chunked JSON (still text, still slow); AnimationClip (Assets only, smooths across cuts) |
| 3 | Offline render path | Own capture: camera `targetTexture`, `AsyncGPUReadback`, raw RGBA piped to ffmpeg | No package dependency, 4K independent of the Game view, ffmpeg is required anyway, headless friendly | Unity Recorder 5.1.7 (public API, Play mode only, adds a dependency, MP4 encoder fixed); PNG sequence then ffmpeg (slow encode, huge disk) |
| 4 | Which camera renders | The user's own camera (`KionjoCameraSource.camera` or `Camera.main`) driven in place | A proxy would need per-pipeline copying of URP camera data and post processing; in place keeps the user's look | Proxy camera cloned from the source |
| 5 | Replay clock | `Time.captureDeltaTime = 1/fps`, transforms written in `LateUpdate` (order 32000) and again in `Application.onBeforeRender` | Deterministic; the second write wins over XR pose drivers that update before render | Record and replay in `Update` (XR rigs overwrite) |
| 6 | Head processing | Roll lock on, 0.1 s centred smoothing, cuts at 0.5 m or 25 degrees, all per replayer and exposed | Trailer 1 values that Bernard approved; hardware jitter needs a knob | Raw replay only |
| 7 | Events on replay | Raised as `(name, payload)` to the game; Kionjo never presses anything | Trailer 1 lesson: an offline render has no microphone; the game owns the UI state | Reflection into game UI (Baraza did this; not generic) |
| 8 | Cards | A shot type, in the same list | Opening line, closing lines and cover are beats with lengths; one list, one timeline | Separate opening and closing card lists |
| 9 | Animatic | A cut with image sources | Reuses the whole cut path, zero new ffmpeg code beyond image segments | A Play mode slideshow (new code, no export) |
| 10 | ffmpeg invocation | `ProcessStartInfo.ArgumentList`, working directory = output folder, relative names inside filtergraphs | Bernard's paths contain spaces, parentheses and brackets; v1 hit a Git Bash path bug on brackets | Shell strings with escaping |
| 11 | Window structure | One window, three tabs, `Window/Kionjo` | Fewer windows to dock; Unity's own panels live under Window | Separate Storyboard, Shoot and Cut windows; `Tools/Kionjo` |
| 12 | Shot list implementation | `ListView` bound to the `shots` array with a custom card item | Reorder, add, remove and Undo come from the binding | Unbound `itemsSource` with `Undo.RecordObject` on pointer down (the plan's fallback if custom items fight the binding) |
| 13 | Render audio | Tier 2 through `AudioRenderer` to WAV, muxed by ffmpeg | Only route that stays in step with the capture clock | `OnAudioFilterRead` (wall clock, drifts under frame lock) |
| 14 | Voice trim thresholds | Silence at -55 dB, `d=0.1`; words via faster-whisper fed ffmpeg-decoded float32 | Both learned in trailer 1 | -35 dB (clipped tails); PyAV decoding (version mismatch) |
| 15 | Agent bridge packaging | Separate asmdef with `defineConstraints: ["KIONJO_PIPELINE"]` and a `versionDefines` entry on `com.unity.pipeline` | Compiles only where the pipeline package exists; the core never depends on it | `#if` inside the editor assembly (reference still hard) |
| 16 | Package tests | `Tests/Editor` and `Tests/Runtime` asmdefs, test project lists the package in `testables` | Standard layout; `unity test` runs them headless | Tests in the test project (would not ship with the package) |
| 17 | Input for Record | Window shortcut (Space), button, CLI, and a public `ToggleTake()` for the game's own binding | No Input System or legacy Input dependency | Kionjo reading a key itself |
| 18 | Thumbnails | PNG files under `<root>/thumbs/`, loaded by path | No asset import churn, no Force Text bloat in `Assets/` | Texture2D sub-assets in the storyboard |
| 19 | Default render range | Whole take for Preview; selected range plus 1 s handles for HD and UHD | Previews are for choosing; masters are expensive at 4K | Always whole take |
| 20 | Executor method | `superpowers:executing-plans`, native, one agent | No human for review gates overnight; the plan carries the design | Subagent-driven (needs a reviewer loop) |

## 13. Risks

1. **Window smoke test under batchmode.** UI Toolkit windows usually build their tree without a
   screen; if `GetWindow` misbehaves under `unity test`, the fallback is `unity command eval` in
   the open test editor. Both commands are in the plan.
2. **Readback orientation and format.** DX11 readbacks are top-down; the plan adds `vflip`
   conditionally and the render test samples the top pixel of a two-colour scene.
3. **Bound ListView with custom items.** If the binding and `makeItem`/`bindItem` fight, use the
   unbound fallback (decision 12). The plan allots a step for it.
4. **ffmpeg pipe deadlock.** stderr must be drained asynchronously and stdin writes must be on a
   worker thread with a bounded queue; the plan specifies both and a kill on cancel or exit.
5. **`-nographics` under `unity test`.** Unknown. The render test fails loudly on a black frame;
   fallback route documented.
6. **Scope versus night.** 30 tasks. Tier 1 (18 tasks) is the must; Tier 2 and 3 are ordered so
   that stopping anywhere leaves a working package and a commit. Each task ends with its own
   commit.
7. **FCP7 XML cannot be import-tested tonight.** No Premiere or Resolve on the path. The writer
   is tested structurally; Bernard imports the first XML.
8. **Headset behaviour unverified.** Recorder and replayer are tested on fake transforms. The
   headset pass script is for Bernard.
9. **Two editors.** Bernard's Baraza editor may be open. Every CLI call in the plan passes the
   test project path explicitly; `unity test` needs the test project's editor closed first.

## 14. Out of scope for v0.1

- Cloning and driving hand or controller visuals (Baraza's `Strip` clone trick). Tracks are
  transforms; a hand mesh driven by extra tracks is the user's prefab.
- Scene loading during staging; the storyboard works in the open scene(s).
- A shot start pose marker (`KionjoShotMarker`); staging moves nothing by itself.
- Recorder package bridge, Cinemachine sample, Timeline integration.
- Device capture over adb, YouTube upload, loudness reports.
- AI "suggest storyboard" beyond JSON import and an agent guide (Tier 3).

## 15. Follow-ups

1. **Port Baraza onto Kionjo** (separate task, not tonight): `TrailerStory` becomes an
   `IKionjoStageHandler`; ISDK visuals (hands, rays, cursors) become extra tracks plus a
   Baraza-side clone driver; `TrialVoiceFlow` UI states listen to `KionjoReplayer.onEvent`;
   `Recordings/Trailer` moves to `Kionjo/`; the draft 11 storyboard is re-created as an asset and
   the v1 cut is reproduced from it as the acceptance test.
2. Hand skeleton tracks (26 joints per hand) with the joint-delta trick from Baraza's performer.
3. `KionjoShotMarker` and rig root placement at stage time.
4. Render audio in Tier 2 if time runs out; motion blur option; supersampled stills.
5. Cinemachine sample scene and a Recorder bridge if a team asks.
6. Agent bridge polish: watch mode, `kionjo_suggest` that reads a script and a scene inventory.
7. OpenUPM listing, GitHub Actions running `unity test` on 6000.0, README screenshots.
8. Trailer study (Bernard's links) feeding default pacing presets.
