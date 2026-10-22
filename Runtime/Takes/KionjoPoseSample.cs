using UnityEngine;

namespace SeedeXR.Kionjo
{
    /// <summary>One track's pose in one frame.</summary>
    public struct KionjoPoseSample
    {
        public bool active;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public bool hasScale;
    }
}
