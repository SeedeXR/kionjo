# Changelog

All notable changes to Kionjo are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [0.2.2] - 2026-11-17

### Added

- Build a post without the window: `kionjo post --entry <folder>`, and a batch entry point
  (`-executeMethod SeedeXR.Kionjo.EditorTools.PostMenu.RunFromCommandLine -kionjoPost <entry folder>`).

### Fixed

- Journal entries written by hand or by other tools are read and saved safely: media may be plain paths (or a
  mix with objects), `date` stands in for `created`, and every field Kionjo does not know is written back exactly.
  Before, such media read as empty and unknown fields were dropped on the first save.

## [0.2.1] - 2026-11-17

### Added

- `IKionjoCaptureDriver`: a scene that steps its own simulation is started before frame 0, stepped once per
  captured frame and can tell the rigs what to frame.
- Mark as posted removes the step's older versions without losing anything: a file is deleted only when its data
  survives elsewhere; the rest moves to `<latest>/older/vNN/`.

### Fixed

- A build no longer deletes a material that is a copy (it may be the only one left).
- Design notes and tests use neutral names and paths.

## [0.2.0] - 2026-11-17

### Added

- Progress: Kionjo documents a feature step by step.
  - Journal entries carry `feature`, `version`, `brief` and a capture backlog (`captures`); a later entry of the same
    feature supersedes the step and drops its pending captures.
  - Watcher (editor, file system events, no polling loop): changed entries and new footage in the footage folder's
    day folders are noticed; found footage is added to the right entry with its `notes.md` line. It never renders.
  - Capture at the moment: one Play mode run filmed by up to seven frame-locked rigs placed from the subject's size
    (main, main-vertical, wide, orbit, side, low, close), the scene's sound in every file, stills, notes lines and
    entry media. `kionjo capture` command and a batch entry point with a result file.
  - Post folder per step, `<created>_<slug>/vNN/`: `materials/` (hard links, copies across drives), `post/`,
    `capcut/` (clean edit and SRT), `recipe/`, `BRIEF.md`. A new build deletes the older versions' videos.
  - Resources: a free memory check (twice the need, a floor left free) and a machine-wide heavy-work lock.
  - Preferences: footage folder, watch progress, keep free (GB), heavy-work lock folder.

- Posts (phase 1): turn a step of the work into a ready to post 1080x1920 Reel, with no storyboard needed.
  - Journal: one `entry.json` per step in a per-machine folder (Preferences > Kionjo > Journal folder): the six
    beats, stage (idea, prototype, build, complete), decisions, story type, media by absolute path, hook options.
  - Brand kit asset (Create > Kionjo > Brand Kit) and Posts settings (output folder, active kit), neutral defaults.
  - Post builder: hook templates (claim, promise, question, POV), overlays timed by reading speed, the safe zones,
    crop or blur fill to 9:16, clip sound with silence for clips without sound, optional music ducked under speech,
    word by word captions, loudness -14 LUFS, a call to action or a loop end.
  - The bundle: video with text, a no-music version (when music is set), a clean version plus SRT, a cover,
    caption text, `post.json` and `commands.txt` so the post can be rebuilt.
  - Warnings from the platform basics (length, no text, long hook, long shots, jargon, other apps' watermarks,
    footage that is not yours, silence); posts over 3 minutes are refused.
  - Posts tab in the Kionjo window: journal list with story balance, entry editor, post builder with a preview
    frame showing the safe zones, live warnings, Build post, Mark as posted, and a grid of the latest covers.
  - Tools > Kionjo > Build post from journal entry.

### Changed

- Post bundles moved from `posts/<slug>/<post id>/` to the versioned step folder (Progress).

## [0.1.0] - 2026-10-23

### Added

- Storyboard asset: shots and cards in one reorderable list, music cues, subtitle style, loudness
  and ducking settings, render and cut presets, a render quality override.
- Kionjo window (Window > Kionjo): Storyboard tab with thumbnail shot cards, a grouped inspector,
  drop zones for hide and show lists and a timeline bar against the target length; Shoot tab with
  Stage, a Ready check, Record (Space), takes (use, replay, show, delete) and Done (N); Cut tab
  with Render missing shots, Build animatic and Build draft; a status bar with the running job,
  progress and Cancel. Light and dark skins, a custom icon, undo for every edit.
- Kionjo Recorder: any Transforms plus timestamped events to a take folder (`take.json` and a
  compact binary `frames.bin` that survives a take cut short).
- Kionjo Replayer: frame locked playback with cut detection, roll lock and head smoothing; events
  raised to the game, never pressed.
- Offline render: the user's own camera rendered on demand each frame (URP render request or
  Camera.Render), read back and piped to ffmpeg. Works headless.
- Cut: ffmpeg drafts with cards (black, text, image with push in), burned subtitles and an SRT,
  voice lines placed per shot and trimmed at -55 dB or to words (faster-whisper), music with
  crossfades and ducking, SFX at recorded events, loudness normalisation, an FCP7 XML timeline for
  Premiere Pro and DaVinci Resolve, and a re-runnable `commands.txt`.
- Device capture import and organise only mode with an index.
- Preferences > Kionjo: ffmpeg path, encoder override (for example NVENC), Python path.
- Help that teaches as you go: a tooltip on every field and button, a ? next to each inspector
  heading that opens its page on the documentation site, a Help menu (guide, this tab's docs,
  troubleshooting) and a guided tour on the first open.
- Shot pictures that fill themselves: a new shot takes its picture from the Scene view; From Scene
  view, From camera, or drop a PNG or JPG on the picture to change it.
- Agent bridge (with `com.unity.pipeline` 0.9 or newer): the `kionjo` CLI command group,
  `state.json`, storyboard JSON import and export.
- Quick Start sample; headset pass script; agent guide.
