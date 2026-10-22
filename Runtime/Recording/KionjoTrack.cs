using System;
using UnityEngine;

namespace SeedeXR.Kionjo
{
    /// <summary>One Transform recorded every frame (head camera, a hand, a prop).</summary>
    [Serializable]
    public class KionjoTrack
    {
        [Tooltip("Name stored in the take. The replayer binds tracks by this name.")]
        public string name;
        [Tooltip("The Transform to record or drive.")]
        public Transform target;
        [Tooltip("Also record local scale (costs 12 bytes per frame).")]
        public bool recordScale;
    }
}
