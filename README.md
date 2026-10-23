# Kionjo

**Kionjo** (Swahili for "trailer") is a Unity editor tool for making a high quality trailer of
your experience. Plan the shots on a storyboard, perform each shot in the headset (or with any
camera), replay the performance frame locked and render it offline at up to 4K, then let ffmpeg
cut a draft with voice, subtitles, music and sound effects.

Built by [SeedeXR](https://github.com/SeedeXR) after cutting the trailer of the VR courtroom drama
Baraza by hand. Made for VR developers first, useful for any Unity project.

## Why

A VR trailer cannot be faked: the footage has to move like a real head, and a headset's own
capture is low quality. Kionjo records what the headset and hands did, then replays it in the
editor and renders it at whatever quality you want, as many times as you want.

## Install

Unity Package Manager > Add package from git URL:

```
https://github.com/SeedeXR/kionjo.git
```

Requirements:

- Unity 6000.0 or newer (tested on 6000.0.70f1, URP).
- [ffmpeg](https://ffmpeg.org/download.html) on PATH, or its path set in Preferences > Kionjo.
  On Windows the gyan.dev "full" build has everything Kionjo uses.
- Optional: Python with `faster-whisper`, to trim voice lines to their first and last word.
- Optional: `com.unity.pipeline` 0.9 or newer, for the AI agent commands.

No dependency on Meta XR, OpenXR, XRI, Cinemachine, Timeline or the Recorder package.

## Quick start

Import the **Quick Start** sample (Package Manager > Kionjo > Samples) and run Tools > Kionjo >
Build Quick Start Scene to try everything without a headset. With your own project:

1. **Record transforms.** Add Component > SeedeXR > **Kionjo Recorder** to your rig. Add a track
   for the head camera (name it `Head`) and one per hand. Any Transform works, on any rig.
2. **Plan.** Window > **Kionjo** > New. Add shots and cards, give each a name, a location, a
   planned length, the voice line that plays over it and its subtitle. The timeline bar shows the
   running length against your target.
3. **Shoot.** Enter Play mode with the headset on. Shoot tab: Stage (applies the shot's hide and
   show lists and checks the scene), then **Space** to start and stop a take. Your game can call
   `KionjoRecorder.ToggleTake()` from a controller button, and mark moments with
   `KionjoEvents.Mark("seal")`.
4. **Render.** Unplug the headset, stay in Play mode, and press Render selected take (or Cut tab >
   Render missing shots). Kionjo replays the take frame locked and renders through your own
   camera, post processing included.
5. **Cut.** Cut tab > Build draft. Each draft gets its own numbered folder.

Flat screen projects: put the recorder on your camera and fly it, or make the shot a Live Camera
shot so Kionjo renders your camera (Cinemachine, Timeline, animation) as it moves on its own. A
**Kionjo Camera Source** picks the camera and raises events when a recording or render starts, so
your own camera logic can start with it.

## What goes where

Everything heavy lives outside Assets, under the storyboard's output folder (default `Kionjo/` in
the project folder; add it to `.gitignore`):

| Folder | Contents |
|---|---|
| `takes/S07/S07_take03/` | `take.json` (tracks, events) and `frames.bin` (poses, about 1 MB for 2 minutes) |
| `renders/S07/` | `S07_take03_HD1080p60.mp4` and a `.json` sidecar |
| `captures/S07/` | Imported headset captures, `index.md` |
| `thumbs/` | Shot thumbnails |
| `cut/draft03/` | The draft MP4, `subs.srt`, a Premiere / Resolve XML timeline, `commands.txt` (re-run the cut by hand), `cut.log` |
| `animatic/animatic01/` | The storyboard as thumbnails, timed, with voice and music |
| `state.json` | What Kionjo is doing now, for scripts and agents |

## Extending

- `IKionjoStageHandler`: set the scene up for a shot (jump the story to a beat, open a panel).
- `KionjoReplayer.onEvent`: turn recorded moments back into UI states on replay. Kionjo never
  presses anything for real.
- `IKionjoRecordingHook`: start your own device capture together with a take.
- `IKionjoFrameSink`: take the raw frames somewhere else.

## AI agents

With `com.unity.pipeline` installed, every verb is a CLI command:
`unity command kionjo status --project-path "<project>"`. See
[Documentation~/AgentGuide.md](Documentation~/AgentGuide.md).

## Status

v0.1, in development. Tested headless (EditMode and PlayMode, 139 tests) and live in the editor;
the headset pass is [Documentation~/HeadsetPass.md](Documentation~/HeadsetPass.md).

## License

MIT, see [LICENSE.md](LICENSE.md).
