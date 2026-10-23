# Kionjo v0.1: overnight report (2026-10-23)

Planned by a Fable subagent (Kionjo-Spec.md, Kionjo-v0.1-Plan.md), built and verified by Opus.
All three tiers of the plan built. Private repo github.com/SeedeXR/kionjo, branch main.

## What works

- Window > Kionjo with its own icon. Storyboard tab: shot cards with thumbnails, inspector,
  timeline bar against the target length. Shoot tab: Stage, Ready check, Space to record, takes.
  Cut tab: Render missing shots, Build animatic, Build draft. Status bar with job progress and Cancel.
- Kionjo Recorder: any rig's head, hands and game events (no Meta, OpenXR or XRI dependency).
- Offline render: replays a take frame locked through your own camera (post processing
  included), up to 4K, into ffmpeg.
- Drafts like trailer 1: text, black and image cards, burned subtitles plus SRT, voice placed per
  shot, music crossfade and ducking, SFX at recorded presses, loudness, a Premiere/Resolve XML
  timeline and a re-runnable commands.txt.
- Voice trim at -55 dB, or to chosen words with faster-whisper.
- Quick Start sample (no headset needed), `unity command kionjo ...` for agents, state.json,
  headset test script (HeadsetPass.md), agent guide (AgentGuide.md).
- Unity 6000.0 and up, tested on 6000.0.70f1.

## How it was checked

- 139 of 139 tests (122 EditMode, 17 PlayMode). EditMode also green without the pipeline package.
- Live run in a real editor: staged a shot (Ready), recorded a 7.7 s take, rendered it (231
  frames, 720p), built a 25 s draft (opening line, shot, two closing lines), ran the CLI commands.
- A fresh-context code review found 10 problems; all fixed. The worst two: a possible editor hang
  when ffmpeg dies mid render, and a cancelled re-render destroying the previous good file.

## Decisions made overnight (point at any to change)

1. Rendering only in Play mode: Kionjo entering Play mode itself would drop the running render;
   you shoot in Play mode anyway.
2. Renders are always the whole take: ranges would shift every in point in the cut (follow up).
3. Each frame is rendered on demand instead of at end of frame, which never happens headless.
4. Cuts always use a real font file: ffmpeg's system font lookup crashes on Windows.
5. Word trim uses whisper only inside a line; at a line's own ends it uses the silence edge,
   because whisper ended "Justice?" at 3.5 s when it really ends at 4.0 s.
6. "Done, next shot" is N, because Unity does not allow Enter as a shortcut.
7. Not built: a Project Settings page (the storyboard and Preferences > Kionjo cover it).

Every change against the plan: Kionjo-Spec.md section 12b.

## Not verified yet

- The headset pass (about 20 minutes, Documentation~/HeadsetPass.md).
- Importing the XML into Premiere.
- The light editor theme by eye (only the dark one was seen).

## Next

Bernard's review and pivots, the headset pass, making the repo public when he says so
(`gh repo edit SeedeXR/kionjo --visibility public --accept-visibility-change-consequences`),
the trailer study, then porting Baraza onto Kionjo for trailer 2.
