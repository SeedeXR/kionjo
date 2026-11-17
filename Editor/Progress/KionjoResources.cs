using System;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Asks the machine for room before heavy work (a capture, a render): free commit must be at least
    /// twice the job's estimate and leave the floor free after it. Windows only; elsewhere it is unknown
    /// and the job runs with a warning.
    /// </summary>
    public static class KionjoResources
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        class MemoryStatus
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MemoryStatus));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatus buffer);

        const double GB = 1024.0 * 1024 * 1024;

        /// <summary>Free commit and free physical memory in GB, or null when this system cannot tell.</summary>
        public static (double freeCommitGB, double freePhysicalGB)? Free()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor) return null;
            try
            {
                var m = new MemoryStatus();
                if (!GlobalMemoryStatusEx(m)) return null;
                return (m.ullAvailPageFile / GB, m.ullAvailPhys / GB);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool CanRun(double needGB, double floorGB, (double, double)? free, out string reason)
        {
            if (free == null)
            {
                reason = "memory unknown on this system";
                return true;
            }
            double commit = free.Value.Item1;
            reason = string.Format(CultureInfo.InvariantCulture, "needs {0:0.0} GB, {1:0.0} GB free commit, {2:0.0} GB must stay free", needGB, commit, floorGB);
            return commit >= 2 * needGB && commit - needGB >= floorGB;
        }
    }
}
