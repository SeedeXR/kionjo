using UnityEngine;

namespace SeedeXR.Kionjo
{
    /// <summary>
    /// Where each capture camera stands, from the subject's size (never a fixed distance: a fixed one
    /// once put a lens inside a large subject). main and main-vertical are the scene's own camera.
    /// </summary>
    public static class KionjoRigs
    {
        public static readonly string[] All = { "main", "main-vertical", "wide", "orbit", "side", "low", "close" };

        /// <summary>Frame size and vertical field of view of a rig.</summary>
        public static (int w, int h, float vfov) Format(string rig, float mainVFov)
        {
            switch (rig)
            {
                case "main": return (1920, 1080, mainVFov);
                case "main-vertical":
                    // Keep roughly the main view's width of the scene: 0.6 of its horizontal angle, used vertically.
                    float hfov = 2f * Mathf.Atan(Mathf.Tan(mainVFov * 0.5f * Mathf.Deg2Rad) * 16f / 9f) * Mathf.Rad2Deg;
                    return (1080, 1920, hfov * 0.6f);
                case "wide": return (1920, 1080, 55f);
                case "orbit": return (1920, 1080, 45f);
                case "side": return (1920, 1080, 45f);
                case "low": return (1080, 1920, 50f);
                case "close": return (1080, 1920, 35f);
                default: return (1920, 1080, mainVFov);
            }
        }

        /// <summary>
        /// The pose of rig at progress u (0..1) through the capture. size = the largest bounds extent of
        /// the subject seen so far (the caller keeps the maximum, so cameras never close in on it).
        /// </summary>
        public static Pose Place(string rig, Vector3 centre, float size, Vector3 mainPos, Quaternion mainRot, float u)
        {
            if (rig == "main" || rig == "main-vertical") return new Pose(mainPos, mainRot);
            size = Mathf.Max(size, 0.1f);
            // "back" = from the subject towards the main camera, on the floor plane.
            var toMain = mainPos - centre;
            toMain.y = 0f;
            var back = toMain.sqrMagnitude > 1e-6f ? toMain.normalized : Vector3.back;
            Vector3 Around(float degrees, float distance) => Quaternion.AngleAxis(degrees, Vector3.up) * back * distance;

            Vector3 pos, target = centre;
            switch (rig)
            {
                case "wide":
                    // Behind the main camera (or 3 sizes out when the main camera is closer).
                    pos = centre + back * Mathf.Max(3f * size, toMain.magnitude + 0.5f * size) + Vector3.up * 0.4f * size;
                    break;
                case "orbit":
                    pos = centre + Around(-60f + 120f * Mathf.Clamp01(u), 4f * size) + Vector3.up * 0.3f * size;
                    break;
                case "side":
                    pos = centre + Around(60f, 3f * size);
                    break;
                case "low":
                    pos = centre + back * 2.5f * size + Vector3.down * 0.8f * size;
                    target = centre + Vector3.up * 0.2f * size;
                    break;
                case "close":
                    pos = centre + Around(-25f, 2.2f * size) + Vector3.down * 0.3f * size;
                    target = centre - Vector3.up * 0.35f * size;
                    break;
                default:
                    return new Pose(mainPos, mainRot);
            }
            return new Pose(pos, Quaternion.LookRotation(target - pos, Vector3.up));
        }
    }
}
