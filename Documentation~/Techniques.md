# Cinematic techniques with Kionjo

Recipes for the shots that make a trailer feel like cinema, with numbers measured from real trailers (Nov 2026: The
Matrix, Kung Fu Hustle and Everything Everywhere All at Once trailers, cut detection with ffmpeg, plus the makers' own
talks transcribed). Borrow the technique, never the shot, the music or the characters.

## The two tools

- **Speed** (shot > Timing > Speed): keys of `t` (seconds into the render) and `speed` (1 real time, 0.2 slow motion,
  0 frozen), eased between keys. The game runs at that speed while the shot renders (`Time.timeScale`, with the
  physics step shrunk to match, so slow motion stays smooth), and a recorded take replays at the same speed. A take
  shot gets as many frames as its curve needs; a live shot keeps its length.
- **Kionjo Camera Move** (component, next to the Kionjo Camera Source): Path through waypoints (dolly, crane, push in,
  whip pan, crash zoom with a field of view change) or Orbit around a pivot. It runs on render time, so it keeps
  moving when Speed slows or freezes the world. That pair is bullet time.

Because every frame is rendered, slow motion is real frames, never interpolation.

## Recipes

| Technique | Measured in the references | Kionjo |
|---|---|---|
| Bullet time | Matrix: ~3.7 s in total, three angles (0.8, 0.5, 1.2 s) glued with 2-frame white flashes, after 1.5 s of black | Live shot, Speed `0:0`, Camera Move Orbit 120-180 degrees over 3-4 s, eased. Render 2-3 angles, add white frames in the cut |
| Speed ramp | "Coming up to speed, moving at a speed, coming off a speed" (Matrix making-of) | Speed `0:1, 0.8:1, 1.1:0.2, 2.0:0.2, 2.3:1` |
| Freeze on a performance | the hold before a burst: 3.9 s slow push in (EEAAO) | Performance shot, Speed `0:1, 0.6:0.05`, a Camera Source that is not the head, Camera Move Path push in |
| Whip pan | a blur between two subjects, 0.2-0.4 s | Path, two waypoints 60-120 degrees apart, `seconds` 0.3, motion blur in the volume |
| Crash zoom | a fast push to a face | Path with a short move (0.2-0.4 s) and Start/End FOV, e.g. 40 to 18 |
| Flash frames | Matrix: 48 white flashes of 1-2 frames in 152 s | In the cut: 2 white frames at 24-30 fps, 4 at 60 fps |
| World per aspect ratio | EEAAO: life 1.85, action 2.39, flashbacks 4:3 ("so that you know what movie you're in") | Shot crop, or letterbox in the cut |
| Burst (verse jump) | EEAAO: 21 shots in 2.6 s (about 3 frames at 24 fps), same centred framing, grade and ratio change each cut | Short live renders of one framing, cut at 8 frames (60 fps) each |
| Black breath | 1.4-1.5 s of black before the climax, in both | Card, black, 1.5 s |
| Title over black | cards over black with sound still running (trailer editor, Vox) | Card, text |

## Pace

Median shot length is about 0.9 s in all three trailers. Action stretches run shorter (Kung Fu Hustle 0.67 s), the
climax shortest (EEAAO's last third 0.38 s, its burst 0.13 s), with 2-4 s holds placed between bursts so the eye
rests. Plan the storyboard's Target Seconds with that curve: longer shots early, short late, one long hold before the
final burst.
