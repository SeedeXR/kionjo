# Kionjo Posts, Phase 1 (first post) Implementation Plan

> **Status 2026-11-09:** design approved by the maintainer, plan written, nothing implemented yet. START PHASE 1 HERE at Task 1, on branch `posts`. Execution method not chosen yet (recommended: native, one whole-branch review at the end); ask the maintainer first.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Keep the ledger in `Documentation~/Design/progress.md` (git-ignored): one line per finished task with the verify result, commit hash and anything unverified.

**Goal:** From a journal entry and existing media, build a ready to post 1080x1920 Reel bundle (burned video, no-music and clean variants, SRT, cover, caption text, recipe) inside a new Posts tab that works without a storyboard.

**Architecture:** New `Editor/Posts/` folder in the existing Editor assembly. Pure data and builders (journal, brand kit, post model, timing, ASS writer, plan builder, command builder, caption writer, warnings) are EditMode-testable without ffmpeg; `PostJob` runs the commands like `CutJob`; `PostsTab` is a UI Toolkit tab in `KionjoWindow`. All text on screen is one ASS file burned with libass.

**Tech Stack:** Unity 6000.0.70f1, UI Toolkit, NUnit, ffmpeg 7.1 full build (libass, freetype; no librsvg), faster-whisper optional through the existing `WordTimer`.

**Spec:** `Documentation~/Design/Kionjo-Posts-Design.md` (same folder). Phase 1 = section 8 item 1. Read both.

## Global Constraints

- Package root `PKG` = `D:\BERNARD\UNITY PROJECTS (GAMES)\kionjo`, branch `posts`. Test project `TP` = `D:\BERNARD\UNITY PROJECTS (GAMES)\Kionjo-TestProject` (references the package by `file:../../kionjo`, listed in `testables`). Do not touch any consuming project's repo.
- Namespace `SeedeXR.Kionjo.EditorTools` for code, `SeedeXR.Kionjo.Tests` for tests. No new assemblies, no new package dependencies. The Runtime assembly is not touched.
- Trailer tabs (Storyboard, Shoot, Cut) behave exactly as before; only the Posts tab works without a storyboard.
- The public package ships no studio identity: no logos, project names or collaborator handles in code defaults. Defaults are neutral.
- English only. Writing rule: NO em-dashes anywhere (code, UI strings, docs, commits). No AI attribution in commits, no trailers.
- Every ffmpeg call is an `FfmpegCommand`; names inside filtergraphs are relative to the job folder; fonts are copied into the job folder (never `font=` through fontconfig). Logos are PNG (no SVG support in this ffmpeg).
- Vertical preset, exactly: 1080x1920, 30 fps, `-c:v libx264 -profile:v high -pix_fmt yuv420p -b:v 12M -maxrate 20M -bufsize 24M -g 60 -keyint_min 60 -sc_threshold 0`, audio `-c:a aac -b:a 128k -ar 48000 -ac 2`, `-movflags +faststart`, loudness `loudnorm=I=-14:TP=-1:LRA=11`.
- Safe zone on 1080x1920 (scale linearly by height for other sizes): top 270 px, bottom 670 px, right 150 px, left 65 px clear; hook and cover titles inside y 420 to 1500.
- Post defaults: hook visible from 0.5 s, at most 7 words, held at least 2 s; overlay reading time `max(2, words / 3)` seconds; length warning above 90 s, refuse above 180 s; at most 5 hashtags.
- Tests: `[Test]` in `PKG/Tests/Editor`. Temp files under `Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name)`, deleted in `[TearDown]`.
- Verify (Git Bash): `UNITY` means `export PATH="$PATH:/c/Users/Seede Sr/AppData/Local/Unity/bin"; TP="D:/BERNARD/UNITY PROJECTS (GAMES)/Kionjo-TestProject"`. Test run: `UNITY; unity test "$TP" --mode EditMode --filter SeedeXR.Kionjo --output "$TP/test-results.xml" --timeout 900; echo exit=$?` (exit 0 = pass, 6 = failures; read `total/passed/failed` on `<test-run>`). The TP editor must be closed for `unity test`.
- Shared PC: before every `unity test` or editor launch, check free memory (`Get-CimInstance Win32_OperatingSystem`), announce the heavy job to the other Claude sessions with SendMessage, note `Get-Process Unity` PIDs, and close any Unity process you started before reporting. Never write package files while `unity test` compiles.
- Commit after each task from `PKG`: `git add -A && git commit -m "<plain message>"`. Do not push.

## Review Focus

1. Media paths with spaces, dashes and brackets (`D:/Footage/My Game/Ghost look 1 - Pattern.mp4`, `studio-logo-white 1 [Vectorized].png`): each must reach ffmpeg as one argument and never appear inside a filtergraph. Pinned in Task 6 (`Build_PathWithSpacesAndBrackets_IsOneArgument`).
2. A media file moved or deleted after the entry was written: the entry still loads, the item shows as missing, and building refuses with "Missing media: <path>" instead of throwing. Pinned in Task 1 (`Load_MissingMedia_FlaggedNotThrown`) and Task 9 (`Run_MissingMedia_FailsWithMessage`).
3. A clip with no audio stream while clip sound is on: the audio graph substitutes silence for that item and still builds. Pinned in Task 6 (`AudioGraph_ItemWithoutAudio_UsesSilence`).
4. Text containing ASS specials (`{`, `}`, `\`, line breaks) or very long sentences: escaped, wrapped by libass, never breaks the file. Pinned in Task 5 (`Escape_BracesBackslashNewline`).
5. Source shorter than its out point, or a source already vertical or square: hold the last frame, no crop when the aspect already matches. Pinned in Task 6 (`FitFilter_VerticalSource_NoCrop`, `Segment_OutPastEnd_HoldsLastFrame`).

---

### Task 1: Journal (entries on disk)

**Files:**
- Create: `PKG/Editor/Posts/KionjoJournal.cs`
- Modify: `PKG/Editor/Settings/KionjoPreferences.cs` (journal folder field in the Preferences page)
- Test: `PKG/Tests/Editor/JournalTests.cs`

**Interfaces:**
- Produces:
  - `[Serializable] class JournalMedia { string path; string role; float inPoint; float outPoint; string note; bool notOwn; }` (role one of `hook, before, after, broll, still, facecam`; `outPoint <= inPoint` means whole file; `notOwn` = footage not made by the user).
  - `[Serializable] class JournalEntry { string id; string created; string project; string title; string story; string owner; string making, reference, goal, achieved, hurdle, trick; List<JournalMedia> media; List<string> hookOptions; string status; List<KionjoPost> posts; [NonSerialized] string Folder; }` (`status` one of `Draft, Ready, Posted`; `owner` = brand kit name).
  - `static class KionjoJournal`:
    - `const string FolderKey = "SeedeXR.Kionjo.JournalFolder"`
    - `string Folder { get; set; }` (EditorPrefs, default `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "KionjoJournal")`)
    - `string Slug(string title)` (lowercase, a to z and 0 to 9, other runs become one `-`, trimmed, max 48 chars, empty becomes `entry`)
    - `JournalEntry Create(string root, string title, string project, DateTime date)` (folder `<root>/<yyyy-MM-dd>-<slug>`, suffix `-2`, `-3` when taken; writes `entry.json`; `id` = `Guid.NewGuid().ToString("N")`; `created` = `date.ToString("yyyy-MM-dd")`; `status` = `Draft`)
    - `void Save(JournalEntry e)` (pretty JSON, UTF-8 without BOM, written to `entry.json.partial` then moved)
    - `List<JournalEntry> LoadAll(string root, List<string> warnings)` (newest folder name first; unreadable files add a warning and are skipped)
    - `IEnumerable<JournalMedia> Missing(JournalEntry e)` (media whose `path` does not exist)
- `KionjoPost` is defined in Task 3; in this task declare `posts` with the type and leave Task 3 to fill the class.

- [ ] **Step 1: Write failing tests** in `JournalTests`: `Slug_Examples` (`"The ghost never turns round!"` gives `the-ghost-never-turns-round`; `""` gives `entry`; 60 `a`s give 48), `Create_WritesEntryJson_DatedFolder` (date 2026-11-08, title "Ghost" gives folder `2026-11-08-ghost` containing `entry.json` with `status` `Draft`), `Create_SameTitleTwice_SuffixesFolder` (`-2`), `SaveLoad_RoundTrip_AllFields` (every field including two media and one hook option survives), `LoadAll_NewestFirst_SkipsBrokenWithWarning` (one folder with `{not json` gives one warning and the other entries load), `Load_MissingMedia_FlaggedNotThrown` (an entry pointing at `D:/nope/x.mp4` loads; `Missing` returns it).
- [ ] **Step 2: Run** the test command. Expected: compile errors (types missing).
- [ ] **Step 3: Implement** `KionjoJournal.cs` with `JsonUtility.ToJson(e, true)`; add a "Journal folder" `TextField` plus Browse button to the Preferences page bound to `KionjoJournal.Folder`.
- [ ] **Step 4: Run** the test command. Expected: all pass, previous count unchanged otherwise.
- [ ] **Step 5: Commit** `Journal: entries as JSON in a per-machine folder`.

### Task 2: Brand kit and Posts settings

**Files:**
- Create: `PKG/Editor/Posts/KionjoBrandKit.cs`, `PKG/Editor/Posts/KionjoPostsSettings.cs`
- Test: `PKG/Tests/Editor/BrandKitTests.cs`

**Interfaces:**
- Produces:
  - `[CreateAssetMenu(menuName = "Kionjo/Brand Kit", fileName = "BrandKit", order = 1)] class KionjoBrandKit : ScriptableObject` with fields (defaults in parentheses): `kitName` ("My Studio"), `collaboratorHandle` (""), `logoLightPath` (""), `logoDarkPath` (""), `titleFontPath` (""), `titleFontName` ("Arial"), `bodyFontPath` (""), `bodyFontName` ("Arial"), `accentColour` (`#4FD1E8`), `textColour` (`#FFFFFF`), `identityLine` (""), `callToAction` ("Send this to someone who has never tried VR."), `hashtags` (List<string>, empty), `seriesPrefix` (""), `coverDesaturate` (true), `storyTypes` (List<string> `Origin, Villain, Magic, Prophecy, Alchemy`), `captionRules` (TextArea, ""), `jargon` (List<string> `shader, LOD, URP, FPS, build, mesh, prefab, render pipeline, draw call, normal map`).
  - `static string AssColour(string hex)` on the kit: `#RRGGBB` gives `&H00BBGGRR` (ASS order), invalid input gives `&H00FFFFFF`.
  - `[FilePath("ProjectSettings/KionjoPosts.asset", FilePathAttribute.Location.ProjectFolder)] class KionjoPostsSettings : ScriptableSingleton<KionjoPostsSettings>` with `outputFolder` ("Kionjo"), `activeKitGuid` (""), `KionjoBrandKit ActiveKit { get; set; }` (resolves and stores the GUID), `void SaveNow() => Save(true)`.
  - `static string PostsRoot(string outputFolder) => Path.Combine(KionjoPaths.Root(outputFolder), "posts")` added to `KionjoPaths` in this file as `public static partial class KionjoPaths`.

- [ ] **Step 1: Write failing tests**: `AssColour_Hex_ToBgr` (`#4FD1E8` gives `&H00E8D14F`; `"bad"` gives `&H00FFFFFF`), `NewKit_DefaultsNeutral` (`ScriptableObject.CreateInstance<KionjoBrandKit>()`: five story types in order, `collaboratorHandle` empty, no field contains "Baraza" or "seedexr"), `PostsRoot_UnderOutputFolder`.
- [ ] **Step 2: Run.** Expected: compile errors.
- [ ] **Step 3: Implement** both files.
- [ ] **Step 4: Run.** Expected: pass.
- [ ] **Step 5: Commit** `Brand kit asset and Posts settings`.

### Task 3: Post model and hook templates

**Files:**
- Create: `PKG/Editor/Posts/KionjoPost.cs`
- Test: `PKG/Tests/Editor/PostModelTests.cs`

**Interfaces:**
- Consumes: `JournalEntry`, `JournalMedia` (Task 1).
- Produces:
  - `enum PostFit { Crop, BlurFill }`, `enum PostEnd { CallToAction, Loop }`, `enum HookKind { Claim, Promise, Question, Pov }`.
  - `[Serializable] class PostItem { string path; float inPoint; float outPoint; PostFit fit = PostFit.Crop; float cropCentre = 0.5f; bool captionSpeech; }` (`cropCentre` = horizontal centre of the crop window, 0 to 1).
  - `[Serializable] class KionjoPost { string id; string name; string hook; List<string> overlays; List<PostItem> items; bool clipSound = true; float clipVolume = 1f; string musicPath; float musicVolume = 0.8f; float musicStart; PostEnd end = PostEnd.CallToAction; string endText; string coverTitle; float coverTime; bool posted; string postedUrl; }` (`overlays` = body sentences in order; `name` = file stem, default from entry slug).
  - `static class HookTemplates { string Fill(HookKind kind, JournalEntry e); }` with exact formats: Claim `"Nobody believes this is {X}."`, Promise `"This changes how you see {X}."`, Question `"Can {X}?"`, Pov `"POV: {X}"`, where `{X}` is the first non-empty of `e.achieved`, `e.trick`, `e.goal`, `e.title`, lowercased first letter, trailing period removed, cut to 6 words.
  - `static KionjoPost KionjoPost.FromEntry(JournalEntry e)`: one `PostItem` per media whose role is not `still`, in order (hook role first); `overlays` = non-empty of `making, achieved, hurdle, trick` as single sentences (text up to the first `.`); `hook` = `e.hookOptions[0]` when present, else `HookTemplates.Fill(HookKind.Question, e)`; `name` = `KionjoJournal.Slug(e.title)`; `coverTitle` = first 3 words of `e.title`, upper case.

- [ ] **Step 1: Write failing tests**: `Fill_Question_UsesAchieved` (achieved `"A ghost refuse to turn round."` gives `"Can a ghost refuse to turn round?"`; the templates are mechanical, Bernard rewrites them), `Fill_CutsToSixWords`, `FromEntry_ItemsSkipStills_HookFirst`, `FromEntry_OverlaysFirstSentenceOnly`, `FromEntry_CoverTitleThreeWordsUpper` ("The ghost never turns round" gives `THE GHOST NEVER`).
- [ ] **Step 2: Run.** Expected: compile errors.
- [ ] **Step 3: Implement** `KionjoPost.cs`.
- [ ] **Step 4: Run.** Expected: pass.
- [ ] **Step 5: Commit** `Post model and hook templates`.

### Task 4: Timing and safe zone

**Files:**
- Create: `PKG/Editor/Posts/PostTiming.cs`
- Test: `PKG/Tests/Editor/PostTimingTests.cs`

**Interfaces:**
- Produces:
  - `struct SafeZone { int Width, Height, Top, Bottom, Left, Right, TitleTop, TitleBottom; static SafeZone For(int width, int height); }` (1080x1920 gives 270, 670, 65, 150, 420, 1500; others scale by `height / 1920f`, rounded).
  - `struct TimedText { string Text; float Start; float End; bool IsHook; }`
  - `static class PostTiming`:
    - `float ReadingSeconds(string text)` = `max(2, words / 3f)`
    - `List<TimedText> Schedule(string hook, IList<string> overlays, float totalSeconds)`: hook from 0.5 to `0.5 + max(2, Reading(hook))`; overlays back to back after the hook with 0.2 s gaps, each `ReadingSeconds` long; an overlay that would end after `totalSeconds - 0.5` is dropped (the caller warns).
    - `int Words(string text)` (split on whitespace).

- [ ] **Step 1: Write failing tests**: `SafeZone_1080x1920_Exact`, `SafeZone_1080x1350_Scaled` (top 190, bottom 471), `Reading_ShortIsTwoSeconds`, `Reading_NineWordsIsThree`, `Schedule_HookAtHalfSecond_OverlaysFollow` (hook "Can a ghost refuse to turn?" 0.5 to 2.5; first overlay starts 2.7), `Schedule_DropsOverlayPastEnd`.
- [ ] **Step 2: Run.** Expected: compile errors.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: pass.
- [ ] **Step 5: Commit** `Post timing and safe zone`.

### Task 5: ASS writer (hook, overlays, word captions)

**Files:**
- Create: `PKG/Editor/Posts/AssWriter.cs`
- Test: `PKG/Tests/Editor/AssWriterTests.cs`

**Interfaces:**
- Consumes: `TimedText`, `SafeZone` (Task 4), `KionjoBrandKit.AssColour` (Task 2), `WordTimer.Word` (existing: `w`, `s`, `e`).
- Produces: `static class AssWriter`:
  - `string Escape(string text)`: `\` becomes `\\`, `{` becomes `\{`, `}` becomes `\}`, CR/LF become `\N`.
  - `string Time(float seconds)`: `h:mm:ss.cc` (ASS centiseconds), e.g. 69.6 gives `0:01:09.60`.
  - `string Write(int width, int height, IList<TimedText> texts, IList<WordTimer.Word> captionWords, KionjoBrandKit kit)`.
- Exact file layout: `[Script Info]` with `ScriptType: v4.00+`, `PlayResX: {width}`, `PlayResY: {height}`, `WrapStyle: 0`, `ScaledBorderAndShadow: yes`; `[V4+ Styles]` with three styles:
  - `Hook`: font `kit.titleFontName`, size `round(height * 0.045)`, bold, colour `kit.textColour`, BorderStyle 3 (opaque box), back colour `&H66000000`, Alignment 5, margins L=`zone.Left` R=`zone.Right` V=0;
  - `Overlay`: font `kit.bodyFontName`, size `round(height * 0.034)`, BorderStyle 3, back colour `&H80000000`, Alignment 2, MarginV = `height - zone.TitleBottom`;
  - `Caption`: font `kit.bodyFontName`, size `round(height * 0.04)`, bold, BorderStyle 1, Outline 0, Shadow 2, Alignment 2, MarginV = `zone.Bottom + round(height * 0.02)`.
  - `[Events]`: hook as `Dialogue: 0,{start},{end},Hook,,0,0,0,,{\pos(width/2, zone.TitleTop + (zone.TitleBottom - zone.TitleTop)/3)}{\fad(150,150)}{text}`; overlays as `Overlay` with `{\fad(150,150)}`; captions grouped 3 words per event, each word tagged `{\kf<cs>}` where cs = centiseconds of that word, primary colour white and secondary `kit.accentColour` so the spoken word lights in the accent.

- [ ] **Step 1: Write failing tests**: `Escape_BracesBackslashNewline` (`a{b}\c` plus newline plus `d` gives `a\{b\}\\c\Nd`), `Time_Centiseconds`, `Write_HookOverlay_ExactEvents` (two TimedTexts give exactly the two expected `Dialogue:` lines), `Write_Captions_GroupedByThree_KfTags` (5 words give 2 events; first event text `{\kf30}It {\kf25}was {\kf40}her`), `Write_Header_PlayResAndStyles` (contains `PlayResX: 1080`, `PlayResY: 1920`, `Style: Hook,`).
- [ ] **Step 2: Run.** Expected: compile errors.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: pass.
- [ ] **Step 5: Commit** `ASS writer: hook, overlays, word by word captions`.

### Task 6: Media info, post plan and ffmpeg commands

**Files:**
- Create: `PKG/Editor/Ffmpeg/MediaInfo.cs`, `PKG/Editor/Posts/PostPlan.cs`, `PKG/Editor/Posts/PostCommandBuilder.cs`
- Test: `PKG/Tests/Editor/PostCommandBuilderTests.cs`

**Interfaces:**
- Consumes: `KionjoPost`, `PostItem` (Task 3), `PostTiming`, `SafeZone` (Task 4), `AssWriter` (Task 5), `FfmpegCommand`, `CutCommandBuilder.F`, `CutCommandBuilder.AudioFades` (existing).
- Produces:
  - `interface IMediaInfo { float Duration(string path); (int w, int h) Size(string path); bool HasAudio(string path); }` and `sealed class FfprobeMediaInfo : IMediaInfo` (ffprobe `-show_entries stream=codec_type,width,height:format=duration -of json`, cached by path and write time like `FfprobeMediaProbe`).
  - `class PostSegment { string Label; string SourcePath; float SourceIn; float Duration; PostFit Fit; float CropCentre; int SrcW, SrcH; bool HasAudio; }`
  - `class PostPlan { int Width = 1080, Height = 1920, Fps = 30; List<PostSegment> Segments; List<TimedText> Texts; List<WordTimer.Word> CaptionWords; bool ClipSound; float ClipVolume; string MusicPath; float MusicVolume; float MusicStart; bool DuckMusicUnderSpeech; float TotalSeconds; string Name; List<string> Warnings; }`
  - `static class PostPlanBuilder { PostPlan Build(KionjoPost post, IMediaInfo info, Func<string, List<WordTimer.Word>> words); }`: segment duration = `outPoint - inPoint` when `outPoint > inPoint`, else file duration minus `inPoint`; a loop end appends the first 0.5 s of the first item; texts from `PostTiming.Schedule(post.hook, post.overlays, total)`, plus `post.endText` (or nothing for Loop) as a final overlay ending at `total - 0.1`; caption words from `words(path)` for items with `captionSpeech`, shifted into film time and clipped to the segment; `DuckMusicUnderSpeech` = any item has `captionSpeech` and music is set.
  - `static class PostCommandBuilder`:
    - `const string SegFolder = "seg", ListFile = "list.txt", Joined = "joined.mp4", Ass = "post.ass", Audio = "audio.wav", AudioNoMusic = "audio_nomusic.wav", Srt = "captions.srt"`
    - `string FitFilter(PostSegment s, PostPlan p)`: when `|srcW/srcH - W/H| < 0.01`: `scale=W:H`; Crop: `crop=ih*W/H:ih:(iw-ih*W/H)*{cropCentre}:0,scale=W:H`; BlurFill: `split[bg][fg];[bg]scale=W:H:force_original_aspect_ratio=increase,crop=W:H,boxblur=20:2[b];[fg]scale=W:H:force_original_aspect_ratio=decrease[f];[b][f]overlay=(W-w)/2:(H-h)/2` (written as a `-filter_complex` with output label `[v]`). All end with `,fps=30,format=yuv420p,tpad=stop_mode=clone:stop_duration={D}`.
    - `FfmpegCommand Segment(PostSegment s, PostPlan p)`: `-ss {in} -t {D} -i {src} -an` plus the filter, `-frames:v round(D*30)`, video encoder args from the Vertical preset, output `seg/{Label}.mp4`.
    - `FfmpegCommand Concat(PostPlan p)` to `joined.mp4` (`-f concat -safe 0 -i list.txt -c copy`).
    - `FfmpegCommand AudioGraph(PostPlan p, bool withMusic)`: clip audio per segment (`-ss -t -i src` or `-f lavfi -t D -i anullsrc=r=48000:cl=stereo` when `!HasAudio` or `!ClipSound`), concatenated, `volume={ClipVolume}`; music `atrim={MusicStart}:{MusicStart+Total}`, `volume={MusicVolume}`, fade out the last 1 s; when `DuckMusicUnderSpeech` the music goes through `sidechaincompress=threshold=0.05:ratio=8:attack=20:release=400` keyed by the clip audio; final `amix=inputs=N:normalize=0,loudnorm=I=-14:TP=-1:LRA=11` to `audio.wav` (or `audio_nomusic.wav` when `withMusic` is false). Returns null when there is neither clip sound nor music.
    - `FfmpegCommand Render(PostPlan p, bool burnText, string audioFile, string output)`: `-i joined.mp4` (+ `-i {audioFile}`), `-vf ass=post.ass:fontsdir=.` when `burnText`, Vertical video args, audio args, `-shortest -movflags +faststart {output}`.
    - `FfmpegCommand Cover(PostPlan p, string source, float time, string titleFile, KionjoBrandKit kit)`: `-ss {time} -i {source} -frames:v 1`, filter = fit as Crop, then `hue=s=0` when `kit.coverDesaturate`, `eq=brightness=-0.08`, then `drawtext=fontfile=title.ttf:textfile={titleFile}:expansion=none:fontcolor={kit.accentColour as 0xRRGGBB}:fontsize={round(H*0.075)}:x=(w-tw)/2:y=h*0.5-th/2`, output `cover.jpg` with `-q:v 2`.
    - `List<FfmpegCommand> Build(PostPlan p, string folder, string ffmpeg, string coverSource)`: segments, concat, audio with music, audio without music (only when music is set), render `{Name}_9x16.mp4` (burned, with music), `{Name}_nomusic_9x16.mp4` (burned, no music, only when music is set), `{Name}_clean_9x16.mp4` (not burned, with music), cover. Sets `Executable` and `WorkingDirectory` on all.

- [ ] **Step 1: Write failing tests** (fake `IMediaInfo` returning fixed values): `FitFilter_Crop_16x9Source` (1920x1080, centre 0.5: exact string), `FitFilter_VerticalSource_NoCrop` (1080x1920 gives `scale=1080:1920,...`), `FitFilter_BlurFill_Exact`, `Segment_OutPastEnd_HoldsLastFrame` (tpad stop_duration equals D), `Build_PathWithSpacesAndBrackets_IsOneArgument` (source `D:/BERNARD/UNITY PROJECTS (GAMES)/Ghost look 1 - Pattern [v2].mp4` appears exactly once in `Args` as an element, and no filter string contains `UNITY PROJECTS`), `AudioGraph_ItemWithoutAudio_UsesSilence` (contains `anullsrc=r=48000:cl=stereo`), `AudioGraph_DuckWhenSpeech_HasSidechain`, `AudioGraph_NoSoundNoMusic_ReturnsNull`, `Build_NoMusic_SkipsNoMusicVariant` (no `_nomusic_` output), `PlanBuilder_LoopEnd_AppendsFirstHalfSecond`, `PlanBuilder_CaptionWords_ShiftedToFilmTime` (word at source 3.0 s in an item with inPoint 2.0 placed after a 4 s first item lands at 5.0).
- [ ] **Step 2: Run.** Expected: compile errors.
- [ ] **Step 3: Implement** the three files.
- [ ] **Step 4: Run.** Expected: pass.
- [ ] **Step 5: Commit** `Post plan and ffmpeg commands: fit to 9:16, audio, renders, cover`.

### Task 7: Caption text

**Files:**
- Create: `PKG/Editor/Posts/CaptionWriter.cs`
- Test: `PKG/Tests/Editor/CaptionWriterTests.cs`

**Interfaces:**
- Consumes: `JournalEntry`, `KionjoPost`, `KionjoBrandKit`.
- Produces: `static class CaptionWriter { string Write(JournalEntry e, KionjoPost p, KionjoBrandKit kit); }` with this exact layout (blank lines between blocks, empty blocks omitted):
  1. `p.hook`
  2. `kit.identityLine`
  3. what changed: `e.achieved`, cut to its first two sentences
  4. `kit.callToAction`
  5. hashtags: the first 5 of `kit.hashtags`, each prefixed `#` when missing, space separated
  6. `Alt text: ` + `e.title` + `. ` + first overlay
  7. `Checklist:` then one `- ` line each: `Invite {kit.collaboratorHandle} as collaborator` when the handle is set; `Consider posting as a Trial Reel` when `e.story` is empty or the entry has never been posted; `Add a trending sound in the app to the no-music version` when music is set.

- [ ] **Step 1: Write failing tests**: `Write_FullKit_ExactText` (a full example asserting the whole string), `Write_SixHashtags_KeepsFive`, `Write_AchievedThreeSentences_KeepsTwo`, `Write_NoCollaborator_NoInviteLine`.
- [ ] **Step 2: Run.** Expected: compile errors.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: pass.
- [ ] **Step 5: Commit** `Caption text for the post bundle`.

### Task 8: Warnings

**Files:**
- Create: `PKG/Editor/Posts/PostWarnings.cs`
- Test: `PKG/Tests/Editor/PostWarningsTests.cs`

**Interfaces:**
- Consumes: `PostPlan` (Task 6), `JournalEntry`, `KionjoPost`, `KionjoBrandKit`, `PostTiming.Words`.
- Produces: `static class PostWarnings { List<string> Check(PostPlan plan, JournalEntry e, KionjoPost p, KionjoBrandKit kit); bool Refuse(PostPlan plan, out string reason); }` with these exact messages:
  - `TotalSeconds > 90`: `"Longer than 90 s: most Reels that travel are 20 to 45 s."`
  - `Refuse` when `TotalSeconds > 180`: `"Longer than 3 minutes: Instagram will not recommend it."`
  - no hook and no overlays: `"No text on screen: most viewers watch muted."`
  - hook words > 7: `"Hook has {n} words: keep it to 7 or fewer."`
  - any segment longer than 5 s: `"Shot {label} holds for {d} s: consider a cut or a change every few seconds."`
  - jargon (case-insensitive whole word from `kit.jargon`) in hook or overlays: `"Jargon on screen: {word}. Say it in plain words."`
  - any media path containing `tiktok`, `capcut` or `snaptik` (case-insensitive): `"{file} may carry another app's watermark: Instagram demotes those."`
  - an item whose journal media has `notOwn` while the post has no overlays: `"{file} is not your footage and has no commentary on top: add text or voice."`
  - every overlay dropped by the timing: `"{n} overlay(s) did not fit and were left out."` (from `plan.Warnings`).

- [ ] **Step 1: Write failing tests**, one per message above, each asserting the exact string.
- [ ] **Step 2: Run.** Expected: compile errors.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: pass.
- [ ] **Step 5: Commit** `Post warnings from the platform basics`.

### Task 9: Post job (the bundle on disk)

**Files:**
- Create: `PKG/Editor/Posts/PostJob.cs`
- Test: `PKG/Tests/Editor/PostJobTests.cs`

**Interfaces:**
- Consumes: everything above, `KionjoJob`, `KionjoProcess.RunAsync`, `FfmpegLocator`, `CutJob.DefaultFont()`, `SrtWriter`.
- Produces: `sealed class PostJob : KionjoJob { PostJob(JournalEntry e, KionjoPost p, KionjoBrandKit kit, string postsRoot, string ffmpeg, IMediaInfo info, Func<string, List<WordTimer.Word>> words); string Folder; string OutputMp4; List<string> Warnings; }`.
- Behaviour: folder `<postsRoot>/<slug of entry title>/<p.id>/` (an `id` is assigned when empty). Before any ffmpeg: missing media fails with `Error = "Missing media: <path>"`; `PostWarnings.Refuse` fails with its reason. Writes `post.ass`, `title.ttf` (from `kit.titleFontPath`, else `CutJob.DefaultFont()`), the ASS fonts (`kit.titleFontPath`, `kit.bodyFontPath` copied under their own file names for `fontsdir=.`), `list.txt`, `cover_title.txt`, `captions.srt` (only when caption words exist; one cue per 3-word group), `caption.txt`, `post.json` (`JsonUtility.ToJson` of `{ entryId, post, kitName, commands }`), `commands.txt` (same format as the cut's). Runs commands with progress; on failure keeps the folder and sets `Error` to the step label plus the stderr tail. After the audio step, runs `SilenceDetect` (existing) with `silencedetect=noise=-50dB:d=1` on `audio.wav` (build the command like `SilenceDetect.Command` but with these values; parse with `SilenceDetect.ParseSilences`) and adds `"Silence of {d} s at {t} s: keep a sound bed under the picture."` per range to `Warnings` (test `Run_SilentClips_WarnsSilence`, both inputs without audio and no music). On success copies `{Name}.srt` from `captions.srt` when present, sets `p.posted = false`, and saves the entry with the post added or replaced by `id`.

- [ ] **Step 1: Write failing tests** (real ffmpeg, like `CutJobTests`; inputs generated in the test temp folder with `ffmpeg -f lavfi -i testsrc=size=1920x1080:rate=30:duration=3 -f lavfi -i sine=frequency=440:duration=3 -shortest a.mp4` and a second clip `b clip [x].mp4` without audio): `Run_TwoClips_BundleComplete` (folder has `{Name}_9x16.mp4`, `{Name}_clean_9x16.mp4`, `cover.jpg`, `caption.txt`, `post.json`, `commands.txt`; ffprobe on the 9x16 file gives 1080x1920, 30 fps, h264 high, duration 6.0 within 0.2), `Run_WithMusic_AlsoWritesNoMusic`, `Run_MissingMedia_FailsWithMessage`, `Run_Over180s_Refused`.
- [ ] **Step 2: Run.** Expected: compile errors.
- [ ] **Step 3: Implement** `PostJob.cs`.
- [ ] **Step 4: Run.** Expected: pass. Open one output and the cover by eye (`start` the folder) and note the result in the ledger.
- [ ] **Step 5: Commit** `Post job: writes the whole bundle`.

### Task 10: Posts tab (journal side)

**Files:**
- Create: `PKG/Editor/UI/PostsTab.cs`, `PKG/Editor/UI/JournalEntryView.cs`
- Modify: `PKG/Editor/UI/KionjoWindow.cs` (`Tab` gains `Posts`; toolbar toggle "Posts" with tooltip "Turn progress into ready to post Reels."; `ApplyTab` shows the Posts body whether or not a storyboard is open and hides the empty state on the Posts tab; Posts toggle always enabled), `PKG/Editor/UI/KionjoHelp.cs` (`Tab.Posts => "posts-tab"`)
- Test: `PKG/Tests/Editor/PostsTabTests.cs`

**Interfaces:**
- Consumes: `KionjoJournal`, `JournalEntry`, `KionjoPostsSettings`, `KionjoBrandKit`.
- Produces: `class PostsTab : VisualElement { PostsTab(KionjoWindow w); void Refresh(); JournalEntry Selected { get; } }`; `class JournalEntryView : VisualElement { void Bind(JournalEntry e, KionjoBrandKit kit); event Action Changed; }`.
- Layout: a top row with the journal folder (label plus "Change" opening Preferences), the active brand kit (`ObjectField` bound to `KionjoPostsSettings.ActiveKit`), and "New entry" (asks for a title in a small inline text field, then `KionjoJournal.Create`). Left: entry list (title, project, story, status) and below it the story balance: one line per kit story type with the count of the last 12 entries (for example `Magic 5 · Origin 1 · Villain 0`). Right: `JournalEntryView` with fields for every `JournalEntry` text field (story as a dropdown of `kit.storyTypes`, owner as a text field defaulting to the kit name), the media list (path with "Add file" picker, role dropdown, in, out, notOwn toggle, missing items shown in the warning colour with a "Relink" picker), hook options list. Edits save on change through `KionjoJournal.Save`.

- [ ] **Step 1: Write failing tests**: `Window_PostsTab_WorksWithoutStoryboard` (open `KionjoWindow` with no storyboard, set `CurrentTab = Tab.Posts`; the element named `posts-body` is displayed and `empty-state` is hidden), `PostsTab_ListsEntries_NewestFirst` (journal folder pointed at a temp folder with two entries), `StoryBalance_CountsLastTwelve`. Restore `KionjoJournal.Folder` in TearDown.
- [ ] **Step 2: Run.** Expected: fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: pass; trailer tab tests still pass.
- [ ] **Step 5: Commit** `Posts tab: journal list, entry editor, story balance`.

### Task 11: Posts tab (build side), docs

**Files:**
- Create: `PKG/Editor/UI/PostBuildView.cs`
- Modify: `PKG/Editor/UI/PostsTab.cs`, `PKG/Documentation~/AgentGuide.md`, `PKG/README.md`, `PKG/CHANGELOG.md`
- Test: `PKG/Tests/Editor/PostBuildViewTests.cs`

**Interfaces:**
- Consumes: `KionjoPost.FromEntry`, `HookTemplates`, `PostPlanBuilder`, `PostWarnings`, `PostJob`, `KionjoJobs`, `FfprobeMediaInfo`, `WordTimer.Words`.
- Produces: `class PostBuildView : VisualElement { void Bind(JournalEntry e, KionjoBrandKit kit); }`.
- Layout under the entry editor: a post picker (existing posts of the entry plus "New post from entry"); hook field with four buttons (Claim, Promise, Question, POV) that fill it from `HookTemplates`, plus each entry hook option as a button; overlays list (editable, reorderable); per item fit (Crop or Blur fill), crop centre slider, caption speech toggle; clip sound toggle and volume; music file picker, volume, start; end (Call to action with text, or Loop); cover title and cover time; a preview: "Preview frame" renders one frame at a chosen time through ffmpeg (fit filter plus `ass=` burn) into `Library/KionjoCache/preview.png` and shows it with the safe zone drawn as translucent rectangles (top, bottom, right bands and the title band outline); a live warnings list (recomputed from `PostPlanBuilder` plus `PostWarnings` on every change, probing only once per file); "Build post" (enqueues `PostJob`; disabled while a job runs); after success "Open folder" and "Mark as posted" (sets `posted`, asks for the URL in an inline field, entry status `Posted`). A "Grid" foldout shows the covers of the 9 most recent built posts across the journal, 3 per row at 3:4 (cropped to the middle band), newest top left.
- Docs: `AgentGuide.md` gains "Journal entries": the folder, the `entry.json` schema (every field of Task 1 with meaning), one complete example entry, and the rule that agents write Draft entries and never set `posted`. `README.md` gains a short "Posts" section. `CHANGELOG.md` gains an Unreleased section listing Phase 1.

- [ ] **Step 1: Write failing tests**: `HookButtons_FillFromTemplates` (Question button sets the hook to `HookTemplates.Fill(HookKind.Question, e)`), `Warnings_RecomputeOnEdit` (setting a 9 word hook adds the "Hook has 9 words" warning), `BuildButton_DisabledWhileJobRuns`.
- [ ] **Step 2: Run.** Expected: fail.
- [ ] **Step 3: Implement** the view and docs.
- [ ] **Step 4: Run.** Expected: pass. Then open the TP editor (announce to other sessions first), open Window > Kionjo > Posts with no storyboard, create an entry, add two generated clips, build, check the preview and the grid by eye; close the editor and confirm its PID is gone. Note results in the ledger.
- [ ] **Step 5: Commit** `Posts tab: build view, preview with safe zones, grid; agent guide`.

### Task 12: Acceptance, a real post from the maintainer's own footage (not committed to the package)

The maintainer's kit values, footage and journal entry are in their private notes (for SeedeXR: the private Baraza repo, `Design/Posts/Kionjo-Posts-Strategy.md`, section 8). Nothing from this task is committed to `PKG`.

- [ ] **Step 1: Brand kit** asset in TP (`Assets/Kionjo Posts/`) with the maintainer's values; logos converted to PNG.
- [ ] **Step 2: Journal entry** drafted from the work's build log and commits: story type, owner, the six beats in plain words (no jargon, no dates), 3 to 5 clips by absolute path chosen for the strongest first frame, three hook options.
- [ ] **Step 3: Build** the post in the Posts tab, Vertical, clip sound on, no music. Check against the spec defaults (hook by 0.5 s, safe zones, 20 to 45 s, cover title in the grid band).
- [ ] **Step 4: Deliver** to the maintainer: open the post folder, print the caption text, list what to judge (hook, pacing, text look, cover). Never post anything.
- [ ] **Step 5: Ledger** line with the verdict; requested changes become a follow-up list before the branch is reviewed.

---

## After the last task

Whole-branch review in a fresh context (`/code-review` on the `posts` branch diff, correctness and requirement gaps only), fix findings, then report. Releasing (version bump to 0.2.0, tag, push, consuming projects' git URL update) happens only when the maintainer says so.
