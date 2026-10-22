using UnityEngine;

namespace SeedeXR.Kionjo
{
    /// <summary>
    /// Pure functions behind replay: sampling a track at any time, cut detection (teleports and
    /// snap turns are held, never blended), and the head stabilisation trailer 1 used
    /// (roll lock plus a short centred average that never crosses a cut).
    /// </summary>
    public static class KionjoReplayMath
    {
        public const float DefaultCutDistance = 0.5f, DefaultCutDegrees = 25f;

        /// <summary>True when a to b is a teleport (position jump) or a snap turn (yaw step).</summary>
        public static bool IsCut(in KionjoPoseSample a, in KionjoPoseSample b, float cutDistance, float cutDegrees)
        {
            if (Vector3.Distance(a.position, b.position) > cutDistance) return true;
            return Mathf.Abs(Mathf.DeltaAngle(a.rotation.eulerAngles.y, b.rotation.eulerAngles.y)) > cutDegrees;
        }

        /// <summary>Index of the last time at or before t; -1 before the first or when empty.</summary>
        public static int FindFrame(float[] times, float t)
        {
            if (times == null || times.Length == 0 || t < times[0]) return -1;
            int lo = 0, hi = times.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (times[mid] <= t) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        /// <summary>
        /// Blends between the two frames around t. Holds the earlier frame across a cut or when
        /// either frame is inactive. Clamps outside the recorded range. Empty track gives an
        /// inactive sample.
        /// </summary>
        public static KionjoPoseSample Sample(float[] times, KionjoPoseSample[] track, float t,
            float cutDistance = DefaultCutDistance, float cutDegrees = DefaultCutDegrees)
        {
            if (track == null || track.Length == 0 || times == null || times.Length == 0) return default;
            int i = FindFrame(times, t);
            if (i < 0) return track[0];
            if (i >= track.Length - 1) return track[track.Length - 1];
            var a = track[i];
            var b = track[i + 1];
            if (!a.active || !b.active || IsCut(a, b, cutDistance, cutDegrees)) return a;
            float span = times[i + 1] - times[i];
            float u = span > 0f ? Mathf.Clamp01((t - times[i]) / span) : 0f;
            return new KionjoPoseSample
            {
                active = true,
                position = Vector3.Lerp(a.position, b.position, u),
                rotation = Quaternion.Slerp(a.rotation, b.rotation, u),
                scale = Vector3.Lerp(a.scale, b.scale, u),
                hasScale = a.hasScale,
            };
        }

        /// <summary>
        /// Centred box average of position, yaw and pitch over smoothingSeconds, bounded by the
        /// nearest cut or inactive frame on either side. rollLock rebuilds rotation from yaw and
        /// pitch only, which removes the head tilt that reads as a wobble on a flat screen.
        /// </summary>
        public static KionjoPoseSample[] Stabilize(float[] times, KionjoPoseSample[] track, float smoothingSeconds, bool rollLock,
            float cutDistance = DefaultCutDistance, float cutDegrees = DefaultCutDegrees)
        {
            int n = track?.Length ?? 0;
            var result = new KionjoPoseSample[n];
            if (n == 0) return result;

            // Segment id per frame: a new segment starts at every cut and around inactive frames.
            var segment = new int[n];
            var yaw = new float[n];
            var pitch = new float[n];
            var roll = new float[n];
            for (int i = 0; i < n; i++)
            {
                var e = track[i].rotation.eulerAngles;
                pitch[i] = Mathf.DeltaAngle(0f, e.x);
                roll[i] = Mathf.DeltaAngle(0f, e.z);
                if (i == 0)
                {
                    yaw[i] = e.y;
                    continue;
                }
                bool broken = !track[i].active || !track[i - 1].active || IsCut(track[i - 1], track[i], cutDistance, cutDegrees);
                segment[i] = segment[i - 1] + (broken ? 1 : 0);
                yaw[i] = broken ? e.y : UnwrapYaw(yaw[i - 1], e.y);
            }

            float half = Mathf.Max(0f, smoothingSeconds) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                var s = track[i];
                if (!s.active)
                {
                    result[i] = s;
                    continue;
                }
                int lo = i, hi = i;
                if (half > 0f)
                {
                    while (lo > 0 && segment[lo - 1] == segment[i] && times[i] - times[lo - 1] <= half) lo--;
                    while (hi < n - 1 && segment[hi + 1] == segment[i] && times[hi + 1] - times[i] <= half) hi++;
                }
                Vector3 pos = Vector3.zero;
                float y = 0f, p = 0f;
                for (int j = lo; j <= hi; j++)
                {
                    pos += track[j].position;
                    y += yaw[j];
                    p += pitch[j];
                }
                int count = hi - lo + 1;
                s.position = pos / count;
                s.rotation = Quaternion.Euler(p / count, y / count, rollLock ? 0f : roll[i]);
                result[i] = s;
            }
            return result;
        }

        /// <summary>current shifted by whole turns to sit closest to previous (170 then -170 gives 190).</summary>
        public static float UnwrapYaw(float previous, float current) => previous + Mathf.DeltaAngle(previous, current);

        /// <summary>Vertical field of view for a horizontal one at the given aspect (width / height).</summary>
        public static float VerticalFov(float horizontalDegrees, float aspect) =>
            2f * Mathf.Atan(Mathf.Tan(horizontalDegrees * 0.5f * Mathf.Deg2Rad) / aspect) * Mathf.Rad2Deg;
    }
}
