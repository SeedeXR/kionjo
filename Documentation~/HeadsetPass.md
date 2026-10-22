# Headset pass (manual, about 20 minutes)

Everything in Kionjo is tested headless except the part that needs a person in a headset. This
script checks that part. Use any Quest project with a Meta XR (or OpenXR / XRI) rig; Baraza's
MAIN SCENE works, or the sample scene in the test project.

You need: the headset on Link (or Air Link), ffmpeg on PATH, the Kionjo package installed.

1. **Add the recorder.** Select the rig root (for Meta XR: `OVRCameraRig`). Add Component >
   SeedeXR > Kionjo Recorder. Add three tracks:
   - `Head`: the center eye camera (`CenterEyeAnchor`)
   - `LeftHand`: the left hand anchor (`LeftHandAnchor`, or the hand root you see)
   - `RightHand`: the right hand anchor
2. **Open the window.** Window > Kionjo. Create a storyboard (New), add one shot, name it
   "Headset test", leave it as a Performance shot with Offline Render.
3. **Enter Play mode** with the headset on. Shoot tab: the shot is selected.
4. **Stage and check.** Press Stage. Expect the Ready badge (the hide list is empty, so one
   information line says so).
5. **Record a take.** With the Kionjo window focused, press Space, look around slowly for about
   10 seconds, move your hands, press Space again. Expect `S01_take01` in the takes list, about
   10 s long.
   - If Space does nothing because the editor lost focus while the headset was on, use the
     Record button with the mouse.
6. **Take the headset off and unplug Link** (or stop the Link connection), stay in Play mode if
   the editor allows it, otherwise enter Play mode again without the headset.
7. **Replay.** Press Replay on the take. Expect the Game view to move like your head did, with
   no headset connected. A warning line about an active XR device means Link is still running.
8. **Render a preview.** Storyboard tab, select the shot, press Render selected take. The status
   bar shows "frame N of M" and a progress bar. Expect
   `Kionjo/renders/S01/S01_take01_Preview720p30.mp4` and a new thumbnail on the card.
9. **Compare the motion.** Play the render. It should feel like your head: smooth, no roll wobble,
   no jumps except where you teleported or snap turned. Note any judder.
10. **Render HD.** Set the storyboard's render preset to HD1080p60 (select no shot to see the
    storyboard settings), render again, and note how long it took and the frame count
    (`ffprobe -count_frames`).

Report back: did each step behave as written, the render times, and anything that felt wrong in
the motion. Those answers decide the defaults for smoothing (0.1 s) and roll lock (on).
