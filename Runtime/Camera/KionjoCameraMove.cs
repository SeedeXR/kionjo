using System.Collections.Generic;
using UnityEngine;

namespace SeedeXR.Kionjo
{
    /// <summary>
    /// A camera move for Live Camera shots, on render time: it keeps moving when a shot's speed curve slows or freezes
    /// the world, which is what bullet time is. Path glides through waypoints (dolly, crane, push in, crash zoom with a
    /// short time and a field of view change); Orbit circles a pivot. Starts when Kionjo starts rendering.
    /// </summary>
    [AddComponentMenu("SeedeXR/Kionjo Camera Move")]
    [DefaultExecutionOrder(31900)] // before the capture renders (32001)
    public class KionjoCameraMove : MonoBehaviour
    {
        public enum MoveMode { Path, Orbit }

        [Tooltip("Path: through the waypoints in order. Orbit: around the pivot.")]
        public MoveMode mode = MoveMode.Path;
        [Tooltip("Path: empty transforms placed where the camera should pass (two or more). Their rotations are used when Look At is empty.")]
        public List<Transform> waypoints = new();
        [Tooltip("Orbit: what the camera circles and looks at.")]
        public Transform pivot;
        [Tooltip("Orbit: degrees turned during the move. 180 = half way round.")]
        public float orbitDegrees = 180f;
        [Tooltip("Orbit: starting angle, degrees around the pivot's up axis from its forward.")]
        public float startAngle;
        [Tooltip("Orbit: distance from the pivot, in metres.")]
        public float radius = 1.5f;
        [Tooltip("Orbit: height above the pivot, in metres.")]
        public float height = 0.2f;
        [Tooltip("Keep looking at this while moving. Empty: Path uses the waypoints' rotations, Orbit looks at the pivot.")]
        public Transform lookAt;
        [Tooltip("Seconds of render before the move starts.")]
        public float delay;
        [Tooltip("Seconds the move takes. A crash zoom is short (0.2 to 0.4).")]
        public float seconds = 4f;
        [Tooltip("Progress over the move. The default eases in and out.")]
        public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Vertical field of view at the start and the end. 0 keeps the camera's own.")]
        public float startFov, endFov;
        [Tooltip("Start when Kionjo starts rendering.")]
        public bool playOnRender = true;
        [Tooltip("Start on enable too, to preview the move in Play mode.")]
        public bool playOnEnable;

        public bool Playing { get; private set; }
        float _t;
        Camera _camera;
        KionjoCameraSource _source;

        void OnEnable()
        {
            _camera = GetComponent<Camera>();
            if (playOnRender)
            {
                _source = KionjoCameraSource.Find();
                if (_source != null) _source.onRenderStarted.AddListener(Play);
            }
            if (playOnEnable) Play();
        }

        void OnDisable()
        {
            if (_source != null) _source.onRenderStarted.RemoveListener(Play);
            Playing = false;
        }

        public void Play()
        {
            _t = 0f;
            Playing = true;
            Pose(0f);
        }

        // Render time: Kionjo's fixed step while rendering (never scaled by the speed curve), real time otherwise.
        void LateUpdate()
        {
            if (!Playing) return;
            Pose(seconds > 0f ? Mathf.Clamp01((_t - delay) / seconds) : 1f); // pose, then advance: frame 0 is the start
            _t += Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
        }

        /// <summary>Puts the camera where the move is at progress <paramref name="u"/> (0 to 1, before easing).</summary>
        public void Pose(float u)
        {
            float e = ease != null && ease.length > 0 ? ease.Evaluate(u) : u;
            Vector3 pos;
            Quaternion rot;
            if (mode == MoveMode.Orbit)
            {
                if (pivot == null) return;
                var dir = Quaternion.AngleAxis(startAngle + orbitDegrees * e, pivot.up) * pivot.forward;
                pos = pivot.position + dir * radius + pivot.up * height;
                rot = Quaternion.LookRotation((lookAt != null ? lookAt.position : pivot.position) - pos, pivot.up);
            }
            else
            {
                if (waypoints.Count == 0 || waypoints.Exists(w => w == null)) return;
                PathPose(e, out pos, out rot);
                if (lookAt != null) rot = Quaternion.LookRotation(lookAt.position - pos, Vector3.up);
            }
            transform.SetPositionAndRotation(pos, rot);
            if (_camera != null && startFov > 0f && endFov > 0f) _camera.fieldOfView = Mathf.Lerp(startFov, endFov, e);
        }

        // Catmull-Rom through the waypoints, so the camera does not kink at the middle ones.
        void PathPose(float e, out Vector3 pos, out Quaternion rot)
        {
            int n = waypoints.Count;
            if (n == 1)
            {
                pos = waypoints[0].position;
                rot = waypoints[0].rotation;
                return;
            }
            float f = e * (n - 1);
            int i = Mathf.Min(Mathf.FloorToInt(f), n - 2);
            float u = f - i;
            Vector3 P(int k) => waypoints[Mathf.Clamp(k, 0, n - 1)].position;
            Vector3 p0 = P(i - 1), p1 = P(i), p2 = P(i + 1), p3 = P(i + 2);
            pos = 0.5f * (2f * p1 + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u + (3f * p1 - p0 - 3f * p2 + p3) * u * u * u);
            rot = Quaternion.Slerp(waypoints[i].rotation, waypoints[i + 1].rotation, u);
        }
    }
}
