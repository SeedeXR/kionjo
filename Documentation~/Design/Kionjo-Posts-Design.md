# Kionjo Posts: design

Date: 2026-11-08 (general version 2026-11-09). Status: approved by the maintainer. The
implementation plan for phase 1 is `Kionjo-Posts-Phase1-Plan.md` in this folder. A maintainer's
own audience, accounts, brand kits and acceptance footage live in their private notes, never in
this package or its docs.

Kionjo Posts extends Kionjo from "make a trailer" to "share the journey": it turns each step of a
project into a short, ready to post vertical video (Instagram Reels first), keeps a journal of
those steps, and keeps the disk under control by cleaning up iterations safely.

## 1. Why

Developers want to document and share every stage of their work (what they are making, the
reference, the goal, what they achieved, the hurdles, the tricks) so people can follow the journey
and other developers can learn. It rarely happens because the work around a post is heavy:
rendering, cutting, captions, thinking about the format, and remembering what happened by the time
the work is done. Kionjo already renders and cuts trailers; most of that machinery is what a post
needs too.

Iterations also pile up. One real VR project held about 5.4 GB of recordings, most of it
superseded: eleven trailer drafts where only the last shipped, photo shoot rejects, superseded
compilations, takes nobody picked. A developer using Kionjo should never end up out of disk or
with folders nobody dares to delete.

## 2. Who the posts are for

Typical audiences for a studio sharing its work: people new to the technology, students who could
be inspired to make things themselves, partners and funders who need to see the potential in
their own context, and reviewers who browse a profile as a portfolio.

One post serves several audiences through layers: the first 2 seconds for newcomers, the payoff
and the trick for students, the caption text for developers, the cover and the grid for reviewers.

## 3. People and studios

A person sharing their own work makes it feel tangible, so a common setup is a person as the face
and the studio as co-author through Instagram's Collab feature (one post on both profiles, shared
engagement). Kionjo models this as an **owner** per post: a brand kit, with an optional
collaborator handle that adds the studio mark and an "invite as collaborator" checklist item.

## 4. Goals

1. A finished, ready to post vertical video from existing media in about 15 minutes, with the
   proven basics of an engaging post applied by default.
2. A journal of progress entries that an AI agent session doing the work can draft, so the person
   curates instead of writing from scratch.
3. A consistent look across posts (brand kit, cover template, series naming) so the profile grid
   reads as a portfolio.
4. Safe, visible control of disk use: iterations cleaned up with one action, nothing precious ever
   lost.
5. Later: vertical and same-take before/after renders from Kionjo replays, which no clipping tool
   can do.

## 5. Non-goals and anti-goals

Non-goals (now): posting automatically to any platform; scheduling; CapCut project files (CapCut
encrypts its drafts since about 10.8; the handoff is media + SRT); auto-reframing flat footage
(CapCut Pro and others do it); a music library or beat sync (trending sounds only count when added
inside the app); mixed reality capture of a real person (LIV owns that); languages other than
English; music-only clips without text.

Anti-goals (would make it a failure even if it works):
- The tool writes the person's voice or picks their hook. It offers templates; they choose.
- Posts look templated and interchangeable. Defaults set the basics, not the creativity.
- Any hard delete, or any cleanup that can remove an irreplaceable take without a deliberate,
  explicit act.
- Anything that publishes without the person pressing post in the app.
- Kionjo's trailer workflow gets slower or more complicated for people who never use Posts.

## 6. What the research settled (key sources in 14)

Platform-confirmed:
- Instagram ranks by watch time, likes and sends; sends matter most for reaching non-followers
  (Mosseri, Jan 2025). Reposted or low-effort edited content and other apps' watermarks are
  demoted (tightened Apr 2026); your own logo is fine (Mosseri, Oct 2024). At most 5 hashtags,
  which do not drive reach (Dec 2025). Trial Reels test a post on non-followers first.
- TikTok: make the point clear within 3 s; use sound, captions and on-screen text; keep inside the
  UI safe zone.
- Instagram, Threads and Facebook have no caption file field: captions must be burned in. YouTube
  and LinkedIn accept an SRT.
- One 1080x1920, 30 fps, H.264 High, AAC 48 kHz file at about 12 Mbps (cap 20) of 90 s or less is
  valid for Reels, TikTok, Shorts, Facebook Reels and Threads (Instagram API: max 25 Mbps,
  300 MB).

Strategist consensus (Hook Point, Hormozi, Kallaway, Galloway, and a study of creators who post
game development content): first 1 to 3 s decide; hook, retain, reward, and the reward pays off
the hook; captions always on; show the result before the process; a human face or voice helps;
consistency beats volume. Myths left out: best posting time, fixed "interrupt every 2 s", ideal
exact lengths.

Patterns from the creator study: a personality-led explainer making one surprising claim per video
with a single cover template (desaturated frame, 1 to 3 word title in bold condensed capitals) far
outperformed short music-only progress clips with no text; a fixed one-sentence identity line in
every caption makes a grid readable; footage of real people trying the work travels further than
work-in-progress montages.

Story types: Morgan Battista's Brand Lore Map (Origin, Villain, Magic, Prophecy, Alchemy) is a
useful default for tagging posts so a feed tells a whole story; it ships as the default story type
list, editable per kit.

## 7. The parts

### 7.0 Home and scope

- **Posts stand on their own, not inside a storyboard.** Today every Kionjo tab needs an open
  storyboard. Posts get their own project-level settings asset (output folder, default `Kionjo`,
  the same folder storyboards use by default; active brand kit) and a Posts tab that works with no
  storyboard open. The trailer tabs keep requiring a storyboard, unchanged.
- **The package ships mechanisms; identity lives with the user.** Brand kits, an owner per post
  (any kit, with an optional collaborator handle), story types as an editable list, caption rules
  as editable text. Neutral defaults; no studio's logos, names or rules in the package.
- **Development and acceptance** happen in `Kionjo-TestProject`, with a real post built from the
  maintainer's own footage referenced by absolute path. Consuming projects get Posts through a
  tagged release.

### 7.1 Journal

One entry per step of the work. An entry is a small JSON file plus references to media; it never
copies media. Fields:

| Field | Meaning |
|---|---|
| `id`, `created` | Stable id, creation date (kept in the file, never shown in post text) |
| `project` | Project name |
| `title` | Working title, e.g. "The ghost never turns round" |
| `story` | One of the kit's story types |
| `owner` | Brand kit name |
| `making`, `reference`, `goal`, `achieved`, `hurdle`, `trick` | Six beats, plain text, each optional |
| `media` | List of { path, role (hook, before, after, broll, still, facecam), in, out, note, notOwn } |
| `hookOptions` | 1 to 4 candidate hook lines (drafted; the person picks or rewrites) |
| `status` | Draft, Ready, Posted |
| `posts` | Posts made from this entry (see 7.2), with platform and URL once posted |

**Who writes it.** An AI agent session that did the work can draft the entry at the end of the
work (it has the context: build log, commits, rendered clips). The format is plain, documented and
agent writable (schema in `Documentation~/AgentGuide.md`, the same pattern as the storyboard JSON
import). Entries can also be created and edited in the Kionjo window.

**Where it lives.** One journal for everything a person posts, not one per Unity project, because
a profile grid mixes projects. The journal folder is a per-machine setting (default
`<user home>/KionjoJournal`); each entry is `<journal>/<yyyy-mm-dd>-<slug>/entry.json` and names
its `project`. Media stay where they are and are referenced by absolute path; a moved file shows as
missing with a Relink button. Making the journal folder a private git repo is recommended, not
required.

**Story balance.** The window shows how recent entries spread across the kit's story types, so the
grid tells the whole story instead of five of the same kind in a row.

### 7.2 Post builder

Builds a post from a journal entry (or from loose media). A post has a **mode**:

| Mode | What it is | Priority |
|---|---|---|
| **Video** | The work on screen with text laid over it; covers both "work + text" and "music + text" (same builder, differing only in the audio choice and how much text there is) | Default, first |
| **Face-cam** | A person explains: 50/50 split (work on top, face below) or a circle bubble over the work, word by word captions | Second |
| **Poster / carousel** | Still images 1080x1350 from a poster template | Last, optional |

**Text is laid over the footage**, not on separate full-screen cards: a sentence box (white text,
optional dark box behind it) placed inside the safe zone, one sentence at a time. Full-screen text
cards (Kionjo's existing card segments) stay available for an opening or closing line.

**Audio in a Video post**, chosen per post:
- the clips' own sound (game audio), on by default;
- an optional music file picked for this post (no library; trending sounds are added in the app,
  which is why the bundle includes a no-music version); when the clips carry speech, the music
  ducks under them (the cut's sidechain ducking); otherwise both play at their set volumes;
- speech already in the clips can be transcribed into word by word captions. Recording a voiceover
  arrives with face-cam in phase 3.

Structure of a video post (defaults, all editable):
1. **Hook, 0 to 1.5 s, over the most striking frame.** Frame 0 is the result, never a logo or a
   fade from black. Hook text appears by 0.5 s, at most 7 words, held at least 2 s. Four hook
   templates: a claim to disprove ("One of these is fake"), a promise ("This changes how you see
   VR"), a question ("Can a ghost refuse to turn round?"), and "POV:". The tool offers them filled
   from the entry; the person picks or rewrites. It never picks.
2. **Body.** Result, then how, then the result again; the payoff lands before the last 20 %. Text
   overlays hold one sentence each, timed by reading speed (at least 2 s, about 3 words per
   second).
3. **End.** One soft call to action framed as a send ("Send this to someone who has never tried
   VR"), or a loop: the last 0.5 s match-cuts into frame 0. No "thanks for watching" card. When the
   owner kit has a collaborator, its mark sits on the end frame.

Defaults: 20 to 45 s, warn above 90 s, hard stop at 3:00 (Instagram). English only.

**Captions.** Speech is transcribed with Kionjo's existing word timer (faster-whisper) and burned
in as word by word captions: ASS events with `\kf` timing through libass, white with a soft
shadow, at most 2 lines. Words can be fixed before the render.

**Safe zones on 1080x1920.** Text and logos stay out of the top 270 px, the bottom 670 px and the
right 150 px (third party and ad guidance, not official; to confirm on a phone). Hook and title
cards sit inside the middle square (y 420 to 1500) so they survive the feed and grid crops. The
window shows a preview frame with the zones drawn.

**Warnings, never blocking:** a shot unchanged for more than 5 s; silence longer than 1 s; jargon
in overlay text (an editable word list); length over 90 s; no text at all; another platform's
watermark detected by filename; footage marked as not one's own without commentary on top
(originality rule); a suggestion to post an experimental cut as a Trial Reel.

**Exports (the bundle).** Per post, in `<outputFolder>/posts/<entry-slug>/<post-id>/`:

| File | For |
|---|---|
| `<name>_9x16.mp4` | Reels, TikTok, Shorts, Facebook Reels, Threads (preset below) |
| `<name>_4x5.mp4` (later) | Feed, LinkedIn |
| `<name>_16x9.mp4` (later) | YouTube, LinkedIn |
| `<name>_nomusic_9x16.mp4` | To add a trending sound inside the app |
| `<name>_clean_9x16.mp4` + `<name>.srt` | CapCut handoff: no burned text or captions |
| `cover.jpg` | 1080x1920, title inside the 3:4 grid crop (1080x1440 middle band) |
| `caption.txt` | Ready to paste: hook line, identity line, what changed, call to action, at most 5 hashtags; plus alt text; plus a checklist (collaborator invite, Trial Reel suggestion) |
| `post.json` | Recipe: every input, setting and ffmpeg command, so the post can be rebuilt |

Encode presets (all MP4 faststart, H.264 High, closed GOP, yuv420p, constant fps, AAC-LC 48 kHz
stereo 128 kbps, loudness -14 LUFS integrated, -1 dBTP; loudness is common practice, not a platform
rule):

| Preset | Size | fps | Video |
|---|---|---|---|
| Vertical | 1080x1920 | 30 | VBR about 12 Mbps, max 20 |
| Feed | 1080x1350 | 30 | about 10 Mbps |
| Landscape | 1920x1080 | 30 | 8 Mbps |
| X | 1280x720 or 720x1280 | 30 | 5 Mbps |

**Fitting existing footage into 9:16.** Most existing media is 16:9. Per media item: crop (a crop
window, default centred), or fit with a blurred fill above and below, or split (two items stacked;
built in phase 3 with the face-cam split, which uses the same compositing).

**Caption text formula:** hook line; one fixed identity line per project ("<Project> is <what it
is>, made by <studio>."); what changed, in at most two sentences; one call to action; at most 5
hashtags; the first line carries plain keywords people search for. Per-kit caption rules (for
example "no timeline anchors") are editable text. The tool fills the template from the entry; the
drafting agent can write a better caption into the entry.

**Grid preview.** Shows the covers of the last posts as the 3:4 profile grid, plus the story
balance, so consistency and story spread are visible before posting.

### 7.3 Brand kits

A brand kit asset per owner and project: logo images (light and dark, PNG; ffmpeg builds without
librsvg cannot read SVG, so SVG logos are converted once), fonts (title, body, captions), colours,
cover template, identity line, default call to action, hashtags, collaborator handle, series
prefix, story types, caption rules, jargon list.

Kits ship with neutral defaults. A studio's own kits (logos, colours, identity line, rules) live in
that studio's project, never in this package. Fonts: a kit points at font files the user has the
right to use; text renders need the file, so the kit stores both path and family name.

### 7.4 Cleanup (housekeeping)

Every file Kionjo produces belongs to a class decided by **how it would be recovered**, not by
age:

| Class | Examples | Rule |
|---|---|---|
| **Source** (irreplaceable) | Takes (performances), face-cam recordings, imported originals | Never a cleanup candidate. Deleted only one by one, by an explicit act, with a confirmation naming what is lost |
| **Derived, expensive** | Renders of takes | Candidate when not selected by any shot, not used by a posted post, and not among the newest N per shot (default 2) |
| **Derived, cheap** | Cut and animatic drafts, post exports not posted | Candidate when not among the newest N (default 2) and not pinned; the recipe (`commands.txt`, `post.json`, subtitles, plan) is always kept so it can be rebuilt with one click |
| **Intermediate** | `seg/*.mp4`, `video_nosub.mp4`, `audio.wav` inside a finished draft or post | Candidate as soon as the final file of that draft or post exists |
| **Pinned** | Anything marked pinned, anything posted (final file, cover, caption) | Never a candidate |

Photo shoots are not a Kionjo concept and are out of scope for cleanup; stills Kionjo itself
renders follow the rules above.

The cleanup view lists candidates grouped by class with sizes, a total, and one action: move to
the operating system's recycle bin (reversible). If the recycle bin is not reachable, files move
to `<outputFolder>/.trash/` with a manifest and stay there until the user empties it. No code path
ever hard deletes.

A **disk budget** (default 20 GB per output folder, a setting) shows in the status bar and warns
when exceeded, with a link to the cleanup view.

Scope: cleanup manages folders in Kionjo's layout. Folders a project kept before adopting Kionjo
are out of scope.

### 7.5 Renders from replays (later phase)

Because Kionjo replays a take frame by frame, it can render the same take again:
- **Native vertical:** a second camera at 1080x1920 with a smoothed (steadicam style) version of
  the head path, or an orbit or third person framing, instead of cropping 16:9.
- **Same-take before/after:** render one take twice with a named toggle (a feature, material or
  lighting state switched by the stage handler) off then on, joined by a wipe or split.

This needs the project running on Kionjo's recorder and stage handler, so it comes after the parts
above. Until then, posts use existing media.

## 8. Phases (reels and videos first, posters last)

1. **First post.** Posts settings and tab without a storyboard, journal (format, agent guide,
   window list and editor), brand kits, post builder in Video mode (text overlays, clip sound,
   optional music), hook templates, word by word captions, safe zone preview, warnings, Vertical
   preset, full bundle, grid preview. Acceptance: a real post from the maintainer's own footage
   that they are happy to post.
2. **Cleanup.** Classes, candidates, recycle bin, recipes kept, disk budget.
3. **Face-cam.** Record from a webcam inside the tool, split and bubble layouts, captions on the
   seam, split fit for two items. Feed and Landscape presets.
4. **Renders from replays:** native vertical, same-take before/after.
5. **Posters and carousels**, optional.
6. Later, if wanted: YouTube upload (the easiest API since 2026-11-02), then LinkedIn and X;
   tracking likes and sends per view to learn what works.

## 9. Constraints

- Lives inside Kionjo (`com.seedexr.kionjo`, MIT), Unity 6000.0.70f1, URP 17.0.4. Runtime assembly
  stays dependency free; Posts lives in the Editor assembly.
- ffmpeg is invoked, never bundled (the reference build has libass and freetype, no librsvg).
  faster-whisper stays optional; without it, captions are typed by hand.
- Same patterns as v0.1: commands built as data (testable in EditMode without ffmpeg), relative
  paths inside filtergraphs, fonts copied into the job folder (ffmpeg `drawtext font=` crashes on
  Windows without fontconfig), jobs with progress and cancel, `.partial` files moved into place on
  success.
- People who never open Posts see no change in the trailer workflow.
- Posts must work fully from the window (projects on pipeline 0.8 have no Kionjo CLI commands).
- Test in `Kionjo-TestProject`; never write package files while `unity test` compiles; screenshots
  never with `InternalEditorUtility.ReadScreenPixel` (it reads the physical screen).
- Docs in plain sentences, no em-dashes.

## 10. Decided by the maintainer

- English only.
- Format: the work speaks with on-screen text; sometimes a face on camera explaining.
- Music + text clips: yes. Music only without text: no.
- Posters: possible but rare; reels and videos come first.
- A person as the face, the studio as Collab co-author for studio posts.
- The tool bakes in the basics; creativity goes on top.
- Cleanup of iterations is part of the tool.
- Extend Kionjo rather than build a separate tool.

## 11. Calls made in design, for review

1. An AI agent session that did the work drafts the journal entry.
2. A story type on every entry, with a balance view (Lore Map as the default list).
3. Collaborator kits carry the studio mark on cover and end card; personal kits carry none.
4. Cover: desaturated frame with a 1 to 3 word title in bold condensed capitals, in the kit accent.
5. Series label by chapter, not by number.
6. Default length 20 to 45 s.
7. Cleanup classes by recoverability, recycle bin only, recipes kept, 20 GB default budget.
8. One journal for all projects in a per-machine folder, media by absolute path with Relink; git
   recommended, not required.
9. Webcam recording inside the tool for face-cam (phase 3).
10. Posts stand alone with their own settings; the Posts tab works without a storyboard.
11. The package ships only generic mechanisms and neutral defaults.
12. Work + text and music + text merged into one Video mode; text laid over footage.
13. Phase 1 audio: clip sound plus optional music per post; no voiceover recording until face-cam.
14. Photo-shoot selects out of cleanup.
15. Develop and accept in Kionjo-TestProject; consuming projects update through a tagged release.

## 12. Left to the builder

File and class names; the exact UI layout of the Posts tab; how the crop window and safe zone
preview are drawn; the reading speed formula details; the jargon list's first contents; how the
recycle bin is reached on each OS; JSON schema details beyond the fields in 7.1; how `post.json`
records commands (reuse the cut's `commands.txt` pattern); test structure (follow the v0.1
EditMode pattern).

## 13. Open risks

- Safe zone numbers are not official; confirm on a phone with the first post.
- Platform rules change often (Instagram changed originality rules twice in two years); presets
  and warnings should be data, easy to update.
- A Unity editor window is a heavy place to make posts about non-Unity work. Acceptable for now; a
  standalone front end is a possible follow-up, not in scope.
- Font licences are the user's responsibility; the package ships no fonts in phase 1.

## 14. Key sources

- Instagram ranking signals: socialmediatoday.com/news/instagram-shares-algorithm-insights-2025/738034 (2025-01-22)
- Originality crackdown: techcrunch.com/2026/04/30/instagram-restricts-reach-of-content-aggregators-in-new-crackdown
- Own logo allowed: socialmediatoday.com/news/instagram-clarifies-that-including-your-own-logo-on-a-reel-is-ok/730852
- Hashtag cap: socialmediatoday.com/news/instagram-implements-new-limits-on-hashtag-use/808309
- Reels API specs: developers.facebook.com/docs/instagram-platform/instagram-graph-api/reference/ig-user/media
- TikTok creative practices: ads.tiktok.com/help/article/creative-best-practices
- YouTube upload settings: support.google.com/youtube/answer/1722171
- YouTube API changes: developers.google.com/youtube/v3/revision_history
- Safe zones (third party): revid.ai/tiktok-safe-zone, admanage.ai/instagram-reels-safe-zone
- Captions in CapCut: capcut.com/help/how-to-import-subtitles
- Brand Lore Map: Morgan Battista, morganbattista.com
