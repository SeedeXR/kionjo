# Changelog

All notable changes to Kionjo are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [0.1.0] - unreleased

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
