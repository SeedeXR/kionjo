# Kionjo Progress: design

Status: draft for the maintainer's approval, 2026-11-17. Extends Kionjo Posts (`Kionjo-Posts-Design.md`);
Posts phase 1 (journal, post builder, brand kits) is built on branch `posts` and is reused here.

## 1. Intent and why

Production is increasingly driven by AI agents working in the project. Every step they take is a
chapter worth documenting: a feature that rose from the ground, then came from the air, then was
judged "not good enough yet". The step-by-step improvement is the story, not only the finished
build. Today that documenting is done by hand (or by an agent outside Kionjo, with ad hoc scripts),
so it is inconsistent, heavy on disk, and lost when a session ends.

Kionjo Progress makes Kionjo the project's documentarian: it notices progress, captures it at the
moment it happens, keeps a backlog of what is still worth filming, and turns each step into a post
folder that offers three ways in:

1. **Materials**: every raw clip and still of the step, untouched, to edit from scratch.
2. **Post**: Kionjo's own finished edit, ready to use as it is.
3. **CapCut handoff**: the same edit without burned text, plus subtitles and the brief, to modify.

The maintainer uses Kionjo inside their production project exactly like any other user (installed
from the public git URL, tagged release), so the bugs a user would meet are found and fixed.

## 2. Goals

- An agent that makes progress can, in one step, record that progress (a journal entry) and capture
  it (video and stills) while the version still exists.
- Kionjo notices new progress on its own (new or changed journal entries, new files in the footage
  folder) without a polling loop and without the person asking.
- Every step becomes a post folder with materials, Kionjo's post, the CapCut handoff and a brief.
- Captures the agent could not take at the moment go into a backlog and are taken later, when the
  machine has room.
- Disk stays small: no duplicate video, no frame sequences left behind, older versions' videos
  deleted automatically.
- Heavy work (an editor, a render) asks for resources first and gives them back.
- A manual mode later: the person opens the editor and captures a moment through the same pipeline.

## 3. Non-goals and anti-goals

- Not a scheduler or an agent framework. Kionjo defines files and tools; the agents (any vendor) do
  the reasoning.
- No duplicating whole projects or checking out old commits to film old versions (tens of GB and
  hours of import per copy). A version is filmed while it exists, or not at all.
- No automatic posting to any platform.
- Anti-goal: a capture that silently films the wrong version (for example a duplicated scene that
  runs newer code). If a capture cannot be trusted to show its version, it is not taken.
- Anti-goal: the documentarian slowing or freezing the machine the builders work on.
- Anti-goal: anything studio-specific in this public package (names, accounts, strategy, paths).

## 4. Constraints

- Unity 6000.0+, URP; editor-only for everything except the existing runtime capture components.
- Windows first (the maintainer's machine); macOS and Linux must not break (features that need
  Windows APIs degrade to "unknown" and proceed with a warning).
- ffmpeg is already required by Kionjo; no new dependency.
- Several agent sessions may share one project and one machine: one Unity editor per project at a
  time, and other heavy jobs (builds, transcription, photogrammetry) may be running.
- Public repo (MIT): mechanisms only. Identity and paths live in per-machine preferences and in the
  consuming project.

## 5. Approaches considered

How an old version gets onto video:

| | Approach | Trade-off |
|---|---|---|
| **a** | **Film at the moment of progress.** The building agent, which already has the editor open, runs a quick Kionjo capture at its checkpoint. | Always the true version, no extra disk. Needs the building agent to do one more step. **Chosen.** |
| b | Pin a commit, film later from a separate checkout. | Nothing missed, but every checkout is a second project (tens of GB, hours of import). Rejected. |
| c | The building agent keeps old behaviour switchable (a setting, not deleted code). | Best for planned before/after shots, but costs work on every feature. Kept as an option for moments the person flags; not required. |

Duplicating a scene was also considered: it only freezes changes that live in scene data, not in
code, so it would film the wrong version for most agent work (an anti-goal).

How agents talk to Kionjo: **files in a known folder** (durable, readable by any tool, survive the
end of a session), with direct messages between sessions only as an optional nudge. Chosen over
messages alone, which vanish with the session.

## 6. The parts

### 6.1 Progress = journal entries (extended)

Agents already write journal entries (`AgentGuide.md`). A step of progress is one entry. Added fields:

| Field | Meaning |
|---|---|
| `feature` | The arc the step belongs to (for example "hero"); groups steps into a series |
| `version` | Where the version lives: branch and commit, or a tag. Informational; never checked out by Kionjo |
| `captures` | The backlog for this step: list of capture requests (6.3) with state |
| `brief` | Optional free text (Markdown) the agent writes for the editor: approach, why, shot ideas. Kionjo puts it in the brief file as is |

Entries keep living in the per-machine journal folder. Unknown fields stay ignored, so older entries
load unchanged.

### 6.2 Watcher

An editor-side watcher (`FileSystemWatcher`, operating system events, no polling) on the journal
folder and on the footage root (a per-machine preference, default empty = off). It runs while the
editor is open and Kionjo is installed; it costs no memory to speak of.

On a change it:
- reloads the entry and marks it **new progress** in the Posts tab;
- for a new file in a footage day folder, reads that folder's `notes.md` line for the file (the line
  that names it) and offers it as media for the entry of that day and feature; an agent that
  already listed the file in the entry needs nothing more;
- refreshes the entry's post folder (6.4): materials and brief only. It never renders by itself.

Events are debounced (a file that is still being written is retried until its size is stable).
Outside the editor, agents read the same files directly; nothing depends on the watcher running.

### 6.3 Capture at the moment

One entry point, callable by an agent from a running editor (`unity command kionjo capture`) or in
batch mode (`-executeMethod SeedeXR.Kionjo.EditorTools.KionjoCapture.RunFromCommandLine
-kionjoCapture <request.json>`), and from the window later (manual mode):

```json
{
  "entry": "<journal entry folder or id>",
  "scene": "Assets/Scenes/Feature dev.unity",
  "seconds": 30,
  "fps": 30,
  "subject": "Path/To/Subject",
  "rigs": ["main", "main-vertical", "wide", "orbit", "side", "low", "close"],
  "stills": 0.5,
  "label": "portal-v2"
}
```

- Opens the scene, enters Play mode with `Time.captureFramerate = fps` so every camera sees the same
  simulated frame (frame-locked angles that cut together), and films all requested rigs in one run.
  Projects with their own deterministic stepping can register a stage hook instead (an interface
  with `Begin()`, `Step(dt)` and `Subject` bounds), used in edit mode; Play mode is the default.
- Rigs are placed from the **subject's bounds**, never fixed distances (a fixed distance put a lens
  inside a large subject once): `main` = the scene's main camera; `main-vertical` = the same view
  at 1080x1920; `wide`, `orbit` (slow arc), `side`, `low` (looking up) and `close` at multiples of
  the bounds' size.
- Frames stream straight into ffmpeg through the existing `FfmpegPipeSink` (no frame sequences on
  disk), 1080p, H.264 CRF 16, with the scene's audio when there is any. Stills (PNG) every `stills`
  seconds from each rig.
- Output goes to `<footage root>/<yyyy-MM-dd>/<feature>-<label>-<rig>.mp4`, one `notes.md` line per
  file, and the files are added to the entry's media (role `broll`, the main rig as `hook`
  candidate). Without a footage root, output goes to `<output folder>/captures/`.
- A capture request the agent cannot run now (no room, editor busy) is written into the entry's
  `captures` with state `pending`; states are `pending`, `taken`, `failed` (with the reason) and
  `dropped` (the version is gone: a pending request whose `version` commit is no longer the current
  one is dropped, not filmed, per the anti-goal).

### 6.4 The post folder

Per step: `<posts root>/<yyyy-MM-dd>_<entry slug>/vNN/`

| Folder or file | What |
|---|---|
| `materials/` | Every media file of the entry. Hard links when on the same volume (no extra disk), otherwise copies. Stills included |
| `post/` | Kionjo's edit: `<name>_9x16.mp4`, `<name>_nomusic_9x16.mp4`, `cover.jpg`, `caption.txt` (Posts phase 1 bundle) |
| `capcut/` | `<name>_clean_9x16.mp4` and `<name>.srt` |
| `BRIEF.md` | Generated from the entry: one sentence, why, approach, decisions and why, hurdle and trick, media list with each file's note, hook options, and the agent's `brief` text |
| `recipe/` | `post.json`, `commands.txt`, subtitles: enough to rebuild `post/` and `capcut/` |

- Materials and brief refresh whenever the entry changes (cheap). `post/` and `capcut/` are rendered
  when the person presses Build post, or when an agent asks for a build (command).
- A rebuild after the entry changed writes `vNN+1`. When `vNN+1` is complete, the **videos** of every
  older version of that step are deleted (pictures, briefs and recipes stay). Materials are links,
  so deleting them never deletes footage. When an entry is marked Posted, older versions are removed
  entirely (existing Posts rule).

### 6.5 Resources

- Before a capture or a render, Kionjo checks free commit and physical memory (Windows API; unknown
  elsewhere) against the job's estimate and a floor (preference, default 8 GB free after the job).
  If short, the request goes to the backlog as `pending` with the numbers, instead of running.
- A machine-wide **heavy-work lock** (a lock file in a per-machine folder, preference) holds who is
  running what and how many GB; Kionjo takes it for captures and renders and releases it after. Any
  agent can read it to see who is busy; agents are asked (AgentGuide) to take it for their own heavy
  jobs too.
- A batch capture closes its editor when done (`-quit`) and reports its process id in the result,
  so the caller can confirm it exited.
- Disk: no frame sequences on disk, links instead of copies, superseded videos deleted, and the
  footage root's size shown in the Posts tab.

### 6.6 Agent guide

`AgentGuide.md` gains two short sections:
- **For agents that build:** at each checkpoint write or update the entry (with `feature`,
  `version`, decisions, hurdle and trick), run a capture while the version exists, or add a
  `pending` request if there is no room. Do not overwrite the footage of an earlier step; new
  footage gets new names.
- **For a documentarian agent:** read new progress, fill gaps in briefs, take pending captures when
  the lock and memory allow, never touch a version that is no longer current, and delete what it
  created once it is used.

### 6.7 Manual mode (later phase)

The Posts tab gets "Capture this moment": the same request form (scene = open scene, subject =
selection, rigs, seconds), run in the open editor. Same output, same entry.

## 7. Phases

1. **Progress loop (this round):** entry fields, watcher, capture (Play mode path, rigs, stills,
   footage notes), backlog states, post folder with materials, brief and version cleanup, resource
   check and lock, agent guide. Merge Posts and Progress to `main`, release 0.2.0, update the
   consuming project to it and use it there as an ordinary user.
2. Manual mode in the window; stage hook path; footage size in the tab.
3. The rest of the Posts design phases (cleanup view, face-cam, posters) as already planned.

## 8. Calls made in design (for review)

- Progress is a journal entry, not a new file type: agents already write entries; one format.
- The watcher never renders; rendering waits for Build post. Each render is several large videos and
  heavy CPU, often while other heavy jobs run.
- Capture defaults to Play mode with a fixed capture frame rate (works in any project with no code);
  deterministic stage hooks are an option for projects that have them.
- Older versions' videos are deleted outright, not moved to the recycle bin: the recycle bin does
  not free disk, and the recipe can rebuild them. This overrides the "never hard delete" line of the
  Posts design for superseded post videos only; footage and captures follow the source rule (never
  deleted by Kionjo).
- Hard links are a Windows/NTFS and POSIX feature; on volumes without them Kionjo copies and says so.

### Settled after the builder check (calls, for review)

1. **When a version stops existing.** A step's version exists until the next entry of the same
   `feature` is written (the next step supersedes it), not until the next commit: agents commit
   often, and a commit-strict rule would empty the backlog. A pending capture of a superseded step is
   `dropped`. A capture records the commit and whether the working tree had uncommitted changes
   (`dirty`); a dirty tree is the version being worked on and is filmed, the flag is information.
2. **Post folder vs the phase 1 bundle.** The new layout replaces `posts/<entry-slug>/<post-id>/`.
   Posts root stays `KionjoPaths.PostsRoot(outputFolder)` (the output folder may point outside the
   project, a per-project setting). One `vNN` per build of an entry; several posts of one entry
   (other kits or cuts) sit side by side in `post/` and `capcut/`, named by post name. Version
   cleanup looks at sibling `vNN` folders of the same entry folder. Phase 1 code and tests are
   migrated, not duplicated.
3. **Who writes `entry.json`.** Kionjo owns only appends to `media` (never edits or removes a media
   item someone else wrote) and the `state`/`reason` of `captures` items, plus `posts` (as today).
   Every write is load, merge, save under the existing save lock, so an agent's text is never
   overwritten. The watcher writes found footage automatically (appended with note "found in
   footage folder" plus the notes.md line), so nothing waits for the person.
4. **Rigs follow the subject.** The subject's centre is tracked every frame with smoothing (about
   0.5 s); distances come from the largest bounds size seen so far, so the cameras never move closer
   when the subject shrinks and never end up inside it. `orbit` follows the centre; `wide` and `side`
   stay fixed after the first frame, aimed at the smoothed centre.
5. **Audio.** In phase 1, through `UnityEngine.AudioRenderer` to a WAV in step with the capture clock
   (the route `Kionjo-Spec.md` decision 13 picked), muxed into every rig's file. If audio rendering
   fails, the capture is kept silent with a warning. Synced voice and sound matter for the materials.

## 9. Left to the builder

- Debounce timings, notes.md line matching details, rig multipliers and FOVs.
- How `unity command kionjo capture` returns while Play mode runs (job id plus state file, like
  render).
- The memory estimate per capture (rigs x resolution) and the lock file format.
