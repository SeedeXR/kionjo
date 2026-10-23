# Kionjo: next session plan (from Bernard, 2026-10-23 morning)

Bernard saw the window in Baraza, likes it, and wants to try Kionjo as a complete newcomer would,
with no AI agent, then fix what he finds. This batch makes that possible. Read this file, the
overnight report (Overnight-Report-2026-10-23.md) and spec section 12b first.

## 1. Public and installable

- Polish README.md first (see 2), then make the repo public:
  `gh repo edit SeedeXR/kionjo --visibility public --accept-visibility-change-consequences`
  (Bernard asked for this on 10-23; confirm the README is ready before flipping).
- Install path for any project: Package Manager > Add package from git URL >
  `https://github.com/SeedeXR/kionjo.git`. Verify it in a brand new, empty Unity 6 URP project
  (NOT Baraza, NOT Kionjo-TestProject which uses a file: link): fresh install, Quick Start sample,
  record, render, cut. This is also Bernard's own trial project, so leave it clean for him.
- Consider a version tag (v0.1.0) so people can pin `https://github.com/SeedeXR/kionjo.git#v0.1.0`.
- LICENSE.md is MIT already; check the README states it, plus third party notes (ffmpeg is
  invoked, not bundled; faster-whisper optional).

## 2. README, very well articulated

What it is, who it is for (VR first, flat too), why (VR trailers cannot be faked), install, the
five minute quick start, requirements, a link to the docs site, license, contact. No em-dashes.

## 3. Documentation website (GitHub Pages for now, may move later)

- Borrow the design and flavour of the existing SeedeXR release-notes sites for consistency:
  `D:\BERNARD\baraza-releases` (seedexr.github.io/baraza-releases) and
  `D:\BERNARD\paintball255-releases` (both public SeedeXR repos). Free to improve on them.
- Host (recommended): a separate public repo `SeedeXR/kionjo-docs` served by GitHub Pages at
  seedexr.github.io/kionjo-docs, the same pattern as baraza-releases. Why: Pages only serves the
  repo root or `/docs`, and a `docs/` folder inside the package would be imported into every
  user's project by Unity. Moving to a custom domain later is a CNAME file.
- Content:
  - Step by step guide with real screenshots taken in the live editor (the editor is usually
    open; capture method: `KionjoDemo.Capture` style ReadScreenPixel on a floating window, see
    Kionjo-TestProject/Assets/Kionjo Sample/Editor/KionjoDemo.cs).
  - **"I want to..." section, organised by goal** so a reader jumps straight to their case
    without reading everything. Candidates: make a VR trailer with a headset; make a flat game
    trailer with my own camera or Cinemachine; plan the trailer before shooting (animatic);
    use my headset's own recording instead of re-rendering; add voice over and subtitles; add
    music that ducks under the voice; put sounds on button presses; render in 4K; edit the
    draft further in Premiere or Resolve; set the scene up per shot (hide objects, stage
    handlers); drive Kionjo from an AI agent or a script.
  - Reference: every window element explained (what it does and why).
  - Troubleshooting: ffmpeg not found, black render, fonts, WaitForEndOfFrame note, etc.
  - Contact section and the official website domain: https://seedexr.com/ (email from the
    site or Bernard: b.masika@seedexr.com is Bernard's own address; ask before publishing it,
    prefer the site's contact page).
- Point the window's Help button (KionjoWindow.HelpUrl) at the docs site, ideally with per tab
  anchors (Storyboard, Shoot, Cut) and per section "?" links.

## 4. In editor help (learn as you go)

- Tooltips exist on most fields and buttons; audit for every interactive element, explain what
  and why, consistent tone.
- Add a guided first run overlay: numbered steps ("1. Create a storyboard, 2. Add a shot,
  3. ...") that highlight the relevant element, dismissible, re-openable from Help.
- Small "?" buttons per section that open the matching docs page.

## 5. Thumbnails that fill themselves

Today: "Capture thumbnail" in the inspector (camera source or main camera) and renders replace
it with their middle frame. Bernard wants storyboard-style thumbnails that fill up easily.
Options to build: capture from the Scene view camera (frame the shot in the Scene view, click
"Use Scene view"), auto capture when a shot is added (from the Scene view), auto capture from
the first frame of a new take, drop an image file on the card. Pick a sensible default and
record why.

## 6. After this batch

Bernard uses Kionjo by hand on his own story, finds loopholes, we fix them together. Then the
trailer study or the remaining features.
