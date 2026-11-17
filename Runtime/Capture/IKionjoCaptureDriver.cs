using UnityEngine;

namespace SeedeXR.Kionjo
{
    /// <summary>
    /// Put on a MonoBehaviour in a scene that runs its own simulation (stepped by hand, not by Update) so a
    /// Kionjo capture films it moving. The capture finds every driver in the loaded scenes, calls CaptureBegin
    /// once before frame 0, then CaptureStep once per captured frame before the rigs are placed and render.
    /// Optional: TryGetFocus gives the rigs the point to frame (for example a soft body's centroid) instead
    /// of the subject's renderer bounds.
    /// </summary>
    public interface IKionjoCaptureDriver
    {
        /// <summary>Called once before the first captured frame, with the capture's frame rate.</summary>
        void CaptureBegin(float fps);

        /// <summary>Called once per captured frame, before the rigs are placed; dt is 1 / fps.</summary>
        void CaptureStep(float dt);

        /// <summary>
        /// The centre the rigs should frame, and its radius (0 or less: keep the size measured from the
        /// subject's renderers). False when this driver has no opinion.
        /// </summary>
        bool TryGetFocus(out Vector3 centre, out float radius);
    }
}
