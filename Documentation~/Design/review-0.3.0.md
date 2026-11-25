# Kionjo 0.3.0: review handoff (do NOT release before Bernard's review)

Status (25 Nov 2026): branch `time-remap`, unreleased. Bernard wants a separate review of every feature added since
0.2.3 before any release, tag or merge to main. Baraza stays on v0.2.3. Chronicles of Kilwa VR uses this branch
through `"com.seedexr.kionjo": "file:../../kionjo"` (its trailer v01 and v02 were made with it).

## What the branch adds (review each)

| Commit | Feature | Where | Tests |
|---|---|---|---|
| 27bb191 | Time remap: per-shot speed keys (slow motion, ramps, frozen time) during render; take replay follows the curve | `Runtime/Replay/KionjoReplayer.cs` (`speedKeys`, `_source`), `KionjoSpeed`, `KionjoTimeRemap` | 7 speed + 5 render host tests (24 Nov) |
| 27bb191 | Kionjo Camera Move: path through waypoints / orbit around a pivot, on render time | `Runtime/Camera/KionjoCameraMove.cs` | EditMode suite |
| 2b7f028 | Cut: per-shot aspect (letterbox/pillarbox), grade (ffmpeg filters), flash-in | `Editor/Cut/*` | 264 EditMode + 22 PlayMode pass (25 Nov 01:38) |
| 2b7f028 | Documentation~/Techniques.md | docs | - |

Review questions: speed curve vs physics step (Time.fixedDeltaTime shrink) on a physics-heavy scene; Camera Move
interruption when a render is aborted; grade filters escaping on Windows paths; changelog wording.

## Field findings from the first headset (Performance) takes in a real game (Kilwa, 25 Nov 2026)

Bernard recorded 11 takes (7 shots) over Link with the Headset pass sequence. Three things broke when rendering
them; Kilwa worked around all three on its side, without touching Kionjo. Each is a candidate Kionjo fix.

### 1. The render camera does not follow the Head track when the game renders through its own camera
`KionjoRenderHost.BindReplayer` copies the recorder's tracks. The recorder's `Head` target is the rig's eye
(`CenterEyeAnchor`), so `hasHead` is true and `headOverride` is never set: the head moves the rig's eye, while
`KionjoCameraSource.Resolve()` returns the game's separate trailer camera, which nothing moves (Kilwa's render showed
the world origin, at sea level). Baraza never hit this: its render camera was the head camera.
- Suggested fix: when the resolved render camera is not the Head track's target, set `headOverride = camera.transform`
  (render through the render camera at the head's pose). One condition in `BindReplayer`. Add a RenderHost test with
  a recorder whose Head target differs from the source camera.
- Kilwa's workaround: stage verb `boat` creates a scene `KionjoReplayer` named KilwaVrReplayer with Head bound to the
  trailer camera; `BindReplayer` uses an existing scene replayer that has tracks
  (Kilwa: `Assets/Kilwa/Trailer/Editor/KilwaTrailer.cs`, `VrReplayer`).

### 2. The render host's own replayer cannot be found from game code
The host GameObject is created with `HideFlags.DontSave`, and its replayer did not show up in
`Object.FindObjectsByType<KionjoReplayer>()` from a stage handler's per-frame hook, so game code cannot read the
replay clock (needed for finding 3).
- Suggested fix: expose the active replay (`KionjoReplayer.Active`, like `KionjoRecorder.Active`), or pass it to
  `IKionjoStageHandler` (a second hook called every replayed frame with the clock).

### 3. Takes are world space; a moving platform (a sailing ship) puts the replay somewhere else
Tracks are recorded as world poses. In Kilwa the player stands on a ship that NorthStar's FakeMovement moves every
LateUpdate (order 8000) and restores at the end of the frame; any difference in the ship's pose between recording and
render puts the camera off the deck.
- Kilwa's workaround: the take also records the ship's rigid parts (ladder rungs, door frames, prop stands); a stage
  verb solves the ship pose per frame (Horn best-fit, 12 parts, fit error 0.0 cm on V02_take01) and writes it into
  FakeMovement before order 8000.
- Suggested Kionjo feature: an optional "relative to" Transform per recorder (record poses in the platform's space and
  replay them into the platform's current pose), or a `platform` track that the replayer uses to re-base the others.

### Also noticed
- Shots imported as Organise Only cannot render ("This shot uses a device capture or is organise only"). Fine, but the
  Shoot tab let Bernard record into them without a hint.
- Rendering a specific take needs the shot's `selectedTake` set first, and RenderJob reads it when it starts, so takes
  of one shot render one at a time (Kilwa bridge verb `KilwaTrailerKionjo.RenderTake`). A "render all takes of this
  shot" button would make reviewing takes easier: Bernard asked for every take to be watched before choosing, not the
  newest by default.
- Body-tracked games: the hands the player sees may be a body rig's bones, not the hand anchors. The Headset pass doc
  says "LeftHand / RightHand: the hand anchor (or the hand root you see)"; worth a line that any visible bones and
  moved props must be tracks too (Kilwa records 70 body bones and 86 rigidbodies, 159 tracks, ~23 s takes fine).

## Not done here
No release, no tag, no merge, no version bump. CHANGELOG 0.3.0 stays "Unreleased" with these findings under Known.

## Feature idea for the next version: trailer templates (Bernard, 25 Nov 2026; brainstorm before building)
Bernard: "have trailer templates or types, so next time when I'm creating a trailer on another project, I could just
select the template and Kionjo already knows the kind of trailer and what kind of shots to take."
- A template = a storyboard skeleton: the structure (cold open / prologue with narration, bumper of flashes, title
  card, beats, montage, climax burst, end card), each slot's kind (Performance take, Live Camera move, card, piece of
  another shot), target lengths, transitions (flash, whip, match cut), aspect/grade per world, and the shot list a
  person must perform in the headset vs what the camera can do alone.
- First worked example: Chronicles of Kilwa trailer v02 (Kilwa repo docs/trailer/make_storyboard.py and
  docs/trailer/techniques.md): narrated prologue over illustrations, VR performance takes (pickup, turn, look through,
  pull, steer, carry, place), camera-only beats, bullet time, era burst, title. Baraza's posts are another family.
- Open questions for the brainstorm: template as a ScriptableObject vs JSON; how a template binds to a new project's
  objects (named slots the user fills); genre presets (story/arrival, action, cosy, explainer, store 30 s); how much the
  Shoot tab guides the headset person through the template's performance list.
