using System;
using System.Collections.Generic;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// One capture of a step: which scene, how long, which camera angles. Lives in the entry's
    /// captures list as the backlog; Kionjo only ever changes state, reason, version, dirty and outputs.
    /// </summary>
    [Serializable]
    public class KionjoCaptureRequest
    {
        public static class States
        {
            public const string Pending = "pending", Taken = "taken", Failed = "failed", Dropped = "dropped";
        }

        public string id;
        public string scene;
        public float seconds = 10;
        public float fps = 30;
        /// <summary>Hierarchy path of the subject; empty = the largest renderer in the scene.</summary>
        public string subject;
        public List<string> rigs = new();
        /// <summary>A still from every rig this often, in seconds; 0 = none.</summary>
        public float stills = 0.5f;
        public string label;
        /// <summary>branch@commit when it was taken.</summary>
        public string version;
        /// <summary>The working tree had uncommitted changes when it was taken.</summary>
        public bool dirty;
        public string state = States.Pending;
        public string reason;
        public List<string> outputs = new();
    }
}
