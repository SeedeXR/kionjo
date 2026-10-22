# Kionjo for AI agents

Kionjo works without any AI. When an agent (Claude Code, or any tool that can run a command)
helps, it uses the same verbs a person uses in the window, through the Unity CLI and the
`com.unity.pipeline` package (0.9 or newer). Without that package these commands are simply not
compiled; everything else works.

## Commands

Every command takes `--project-path "<your project>"` (pass it always: with two editors open the
CLI may otherwise answer from the other one). Every answer has `ok` and `message`.

| Command | What it does |
|---|---|
| `unity command kionjo status` | The storyboard, every shot (takes, render, state), Play mode, recording, the running job |
| `unity command kionjo create --storyboard Assets/Trailer/Storyboard.asset --title "My Trailer"` | New storyboard, opened in the window |
| `unity command kionjo open --storyboard Assets/Trailer/Storyboard.asset` | Open an existing storyboard |
| `unity command kionjo select --shot S03` | Select a shot by code or id |
| `unity command kionjo stage` | Play mode: apply hide and show lists, run stage handlers |
| `unity command kionjo check` | Is the selected shot ready? Lists what is wrong |
| `unity command kionjo record --start true` | Play mode: start a take (`--start false` stops it) |
| `unity command kionjo render --shot S03 --preset HD1080p60` | Play mode: render one shot, or every shot missing a render when `--shot` is empty |
| `unity command kionjo cut` | Assemble the next numbered draft (`--animatic true` for an animatic) |
| `unity command kionjo export --json storyboard.json` | The storyboard as JSON |
| `unity command kionjo import --json storyboard.json --replace false` | Append shots from JSON (`--replace true` replaces everything; undoable) |

## The state file

The window writes `<output folder>/state.json` (default `Kionjo/state.json` in the project folder)
whenever something changes, at most twice a second. An agent can watch it instead of polling the
editor:

```json
{
  "storyboard": "Assets/Trailer/Storyboard.asset",
  "title": "Baraza Trailer",
  "isPlaying": true,
  "isRecording": false,
  "currentShot": "ab72ffac99ba4865805fe10b224a88a7",
  "plannedSeconds": 64.0,
  "targetSeconds": 80.0,
  "shots": [
    { "code": "S01", "name": "Crane down on the dome", "takes": 2, "hasRender": true, "state": "rendered" }
  ],
  "job": { "title": "Render S03", "running": true, "progress": 0.44, "status": "frame 108 of 231" }
}
```

## Storyboard JSON

`kionjo export` writes Unity's serialised form of the storyboard. The fields that matter:

- Storyboard: `title`, `outputFolder`, `targetSeconds`, `shots`, `music`, `musicCrossfadeSeconds`,
  `ducking`, `loudness`, `subtitles`, `renderPreset`, `cutPreset`.
- Shot: `id` (leave empty for new shots, one is made), `code` (S01, C01), `name`, `beat`,
  `location`, `type` (0 Performance, 1 LiveCamera, 2 Still, 3 Card), `outputMode` (0
  OfflineRender, 1 DeviceCapture, 2 OrganiseOnly), `targetSeconds`, `inPoint`, `outPoint`,
  `fadeIn`, `fadeOut`, `horizontalFov`, `stageCommand`, `hidePaths`, `showPaths` (hierarchy paths
  like `/Environment/Outside UI`), `voice`, `sfx`, `notes`, `cardKind` (0 Black, 1 Text, 2 Image),
  `cardLines`, `cardImagePath`.
- Voice line: `audioPath`, `subtitle`, `at` (seconds after the shot starts), `trimIn`, `trimOut`,
  `trimMode` (0 Manual, 1 Silence, 2 Words), `overrideTags` (each tag in its own braces, for
  example `{\an5}{\fad(500,1100)}`).
- SFX cue: `audioPath`, `at`, or `eventName` to play at a recorded moment of the take, `volume`.
- Music cue: `audioPath`, `trimIn`, `trimOut`, `at` (absolute), `fadeIn`, `fadeOut`, `volume`.

## Recipe: suggest a storyboard

1. Read the project's narration script (voice line files and their text) and list the scene's
   main places: `unity command eval --project-path "<p>" "return string.Join(\"\\n\", UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.name));"`.
2. Write a storyboard JSON: an opening card, shots grouped by beat (AND, BUT, THEREFORE), each with
   a location, a target length, the voice line that plays over it with its subtitle, and a closing
   card. Keep the total near the target length.
3. `kionjo import --json draft.json --replace true`, then `kionjo cut --animatic true` so the team
   can watch the pacing before anything is shot.
4. The person edits the storyboard in the window. The agent never records: a take needs a person
   in the headset.

## Recipe: render and cut overnight

After a shoot, in Play mode with the headset unplugged: `kionjo render` (every shot missing a
render, queued one after another), watch `state.json` until `job.running` is false and nothing is
queued, then `kionjo cut`. The draft folder holds the MP4, the SRT, a Premiere XML timeline and
`commands.txt`, which re-runs the cut by hand.
