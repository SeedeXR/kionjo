using UnityEngine;
using UnityEngine.Events;

namespace SeedeXR.Kionjo
{
    /// <summary>
    /// Tells Kionjo which camera renders the trailer. Without one, Kionjo uses Camera.main.
    /// The events let a flat screen project start its own camera logic (a Cinemachine shot, a
    /// scripted move) when a take or a render begins.
    /// </summary>
    [AddComponentMenu("SeedeXR/Kionjo Camera Source")]
    public class KionjoCameraSource : MonoBehaviour
    {
        [Tooltip("The camera Kionjo renders through. Empty uses the Camera on this GameObject.")]
        public new Camera camera;
        [Tooltip("Raised when a take starts recording, so your camera logic can start with it.")]
        public UnityEvent onRecordingStarted = new();
        [Tooltip("Raised when a take stops recording.")]
        public UnityEvent onRecordingStopped = new();
        [Tooltip("Raised when Kionjo starts rendering through this camera.")]
        public UnityEvent onRenderStarted = new();
        [Tooltip("Raised when the render ends.")]
        public UnityEvent onRenderStopped = new();

        public Camera Camera => camera != null ? camera : GetComponent<Camera>();

        public static KionjoCameraSource Find() =>
            FindFirstObjectByType<KionjoCameraSource>(FindObjectsInactive.Exclude);

        /// <summary>The source's camera, else Camera.main, else null.</summary>
        public static Camera Resolve()
        {
            var source = Find();
            if (source != null && source.Camera != null) return source.Camera;
            return Camera.main;
        }
    }
}
