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

## Journal entries

An agent session that did the work can draft the journal entry at the end of it, so the person curates instead of
writing from scratch. The journal is one folder per machine (Preferences > Kionjo > Journal folder, default
`<user home>/KionjoJournal`). Each entry is `<journal>/<yyyy-MM-dd>-<slug>/entry.json`, UTF-8, written by
`JsonUtility` (every field below; unknown fields are ignored). Media are never copied: they are referenced by
absolute path.

| Field | Meaning |
|---|---|
| `id` | 32 hex characters, stable |
| `created` | `yyyy-MM-dd`; kept in the file, never shown in post text |
| `project` | Project name |
| `title` | Working title; the folder slug and the post file names come from it |
| `story` | One of the brand kit's story types (default list: Origin, Villain, Magic, Prophecy, Alchemy) |
| `owner` | Brand kit name of whoever posts it |
| `stage` | `idea`, `prototype`, `build` or `complete` |
| `making`, `reference`, `goal`, `achieved`, `hurdle`, `trick` | The six beats, plain sentences, each optional. The first sentence of `making`, `achieved`, `hurdle` and `trick` becomes on-screen text |
| `decisions` | Short sentences, the choices made at this step; each becomes on-screen text after the beats |
| `media` | List of `{ path, role, inPoint, outPoint, note, notOwn }`. `role` is `hook`, `before`, `after`, `broll`, `still` or `facecam` (`still` items are not put in the video; the `hook` item goes first). `outPoint` at or below `inPoint` means the whole file; a picture holds 3 s unless an out point is set. `notOwn` marks footage someone else made |
| `hookOptions` | 1 to 4 candidate hook lines of 7 words or fewer; the person picks or rewrites |
| `status` | `Draft`, `Ready` or `Posted` |
| `posts` | Posts built from the entry (written by Kionjo) |
| `feature` | The arc the step belongs to (for example `hero`). A later entry of the same feature (and project) supersedes this one |
| `version` | Where the version lives, `branch@commit` or a tag. Information only: Kionjo never checks anything out |
| `brief` | Free Markdown for whoever edits the post (approach, why, shot ideas); copied into `BRIEF.md` as written |
| `captures` | The capture backlog of this step: requests (see below) with `state` `pending`, `taken`, `failed` or `dropped` and a `reason` |

Rules for agents: write `Draft` entries only; never set `posted` or `postedUrl` and never mark an entry `Posted`
(only the person does, after posting in the app themselves); plain words on screen, no jargon and no dates; leave
`posts` empty.

Example:

```json
{
    "id": "3f2a9c1e5b7d4e8f9a0b1c2d3e4f5a6b",
    "created": "2026-11-08",
    "project": "My Game",
    "title": "The ghost never turns round",
    "story": "Magic",
    "owner": "My Studio",
    "stage": "prototype",
    "making": "A ghost girl who runs ahead of you in the dark.",
    "reference": "Old stories of spirits that never show their face.",
    "goal": "She must never turn to look at you.",
    "achieved": "She runs, stops and waits, and her face stays hidden.",
    "hurdle": "Her head kept turning toward the camera.",
    "trick": "We lock the head and turn only the body.",
    "decisions": ["She stays grey, like everything outside."],
    "media": [
        { "path": "D:/Footage/My Game/2026-11-08/ghost-run.mp4", "role": "hook", "inPoint": 1.0, "outPoint": 4.0, "note": "Best first frame", "notOwn": false },
        { "path": "D:/Footage/My Game/2026-11-08/ghost-wait.mp4", "role": "after", "inPoint": 0.0, "outPoint": 0.0, "note": "", "notOwn": false }
    ],
    "hookOptions": ["Can a ghost refuse to turn round?", "POV: she never looks back"],
    "status": "Draft",
    "posts": []
}
```

Build it from the Posts tab, or with Tools > Kionjo > Build post from journal entry.

## Progress

A step of progress is one journal entry. Kionjo watches the journal and the footage folder (Preferences > Kionjo >
Footage folder: one `yyyy-MM-dd` folder per day, with a `notes.md` holding one line per file,
`- <file name>: what it shows, the hurdle or trick`) while the editor is open, with no polling loop. A new media file
in a day folder is added to that day's entry (the one whose `feature` starts the file name, `<feature>-...`, or the
only entry of that day), noted "found in footage folder" plus its notes line. Kionjo only ever appends to `media`,
changes the `state` and `reason` of `captures` and writes `posts`; every write loads the entry from disk, merges and
saves, so text you saved in between is kept.

Each step gets a post folder, `<output>/posts/<created>_<slug>/vNN/`:

| Part | What |
|---|---|
| `materials/` | Every media file of the entry, hard linked (no extra disk) or copied on another drive |
| `post/` | Kionjo's edit: `<name>_9x16.mp4`, `<name>_nomusic_9x16.mp4`, `cover.jpg`, `caption.txt` |
| `capcut/` | The same edit without burned text, `<name>_clean_9x16.mp4`, and `<name>.srt` |
| `BRIEF.md` | One sentence, why, approach, decisions, hurdle and trick, each material with its note, hooks, your `brief` |
| `recipe/` | `post.json`, `commands.txt` and the files they need to rebuild the edit |

Materials and the brief refresh when the entry changes. The edit is rendered only on Build post (never by the
watcher). A new build writes the next `vNN` and deletes the videos of the older ones (pictures, briefs and recipes
stay; footage is never deleted).

### Capture request

```json
{
    "entry": "D:/KionjoJournal/2026-11-17-the-hero-rises",
    "scene": "Assets/Scenes/Hero dev.unity",
    "seconds": 30,
    "fps": 30,
    "subject": "Stage/Hero",
    "rigs": ["main", "main-vertical", "wide", "orbit", "side", "low", "close"],
    "stills": 0.5,
    "label": "rises-v2"
}
```

`subject` is a hierarchy path (empty = the largest renderer); `rigs` empty = all seven; `stills` = a still from
every rig this often in seconds (0 = none). Kionjo enters Play mode, films every rig from the same simulated frame,
records the scene's sound into every file and writes `<footage>/<today>/<feature>-<label>-<rig>.mp4` (a free name
is picked, earlier footage is never overwritten), stills under `stills/`, one notes line per video, and adds them
to the entry's media. Run it:

- in a running editor: `unity command kionjo capture --request <file.json> --project-path "<project>"`;
- in batch mode: `Unity -batchmode -projectPath "<project>" -executeMethod SeedeXR.Kionjo.EditorTools.KionjoCapture.RunFromCommandLine -kionjoCapture <file.json>` (no `-quit`; the editor exits by itself with 0 when taken).

Both write `<file.json>.result.json`: `{ state, reason, outputs, pid }`. Check that `pid` is gone afterwards.
A request ends `dropped` when a later step of the feature exists, `pending` (with the numbers) when free memory is
under twice its need plus the floor (Preferences > Kionjo > Keep free) or another tool holds the heavy-work lock,
and `failed` (with the reason) when the scene has no camera or the subject is not there.

### Heavy-work lock

`<lock folder>/heavy.lock` (Preferences > Kionjo, default `<user home>/.kionjo`) holds `{ owner, pid, gb, since }`
of whoever runs heavy work. Kionjo takes it for captures. Create it with exclusive create, delete it when done; a
lock whose `pid` is not running is stale and may be replaced.

### For agents that build

- At each checkpoint, write or update the step's entry: `feature`, `version`, what changed, `decisions`, `hurdle`
  and `trick`, and `brief` for the editor.
- Capture while the version exists: run a capture request now. If there is no room, add the request to `captures`
  with `state` `pending` instead; never film an older version later from another checkout.
- New footage gets new names; never reuse or overwrite an earlier step's footage.
- Take the heavy-work lock for your own heavy jobs (builds, renders, transcription) and release it.

### For a documentarian agent

- Act only on the pending requests of current steps (a step is current until a later entry of its feature exists);
  never film a step that is not current.
- Take pending captures when the lock is free and memory allows; fill gaps in briefs.
- Delete only what you created yourself, once it has been used.

