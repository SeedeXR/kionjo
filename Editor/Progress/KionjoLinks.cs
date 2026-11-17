using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

[assembly: InternalsVisibleTo("SeedeXR.Kionjo.Editor.Tests")]

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Puts a file into a post folder without using more disk: a hard link on the same volume, a copy
    /// otherwise. Deleting the link never deletes the footage.
    /// </summary>
    public static class KionjoLinks
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateHardLinkW(string newFile, string existingFile, IntPtr security);

        [DllImport("libc", SetLastError = true)]
        static extern int link(string existing, string newFile);

        /// <summary>Test seam: true when destination is now a hard link to source.</summary>
        internal static Func<string, string, bool> TryLink = HardLink;

        static bool HardLink(string source, string destination)
        {
            try
            {
                return Application.platform == RuntimePlatform.WindowsEditor
                    ? CreateHardLinkW(destination, source, IntPtr.Zero)
                    : link(source, destination) == 0;
            }
            catch (Exception)
            {
                // No such entry point (an unusual system): the caller copies instead.
                return false;
            }
        }

        /// <summary>
        /// True when destination is a hard link to source, false when it was copied (or could not be),
        /// with the reason in note. Replaces an existing destination. Never throws for file system reasons.
        /// </summary>
        public static bool LinkOrCopy(string source, string destination, out string note)
        {
            note = null;
            string src = Path.GetFullPath(source), dst = Path.GetFullPath(destination);
            if (string.Equals(src, dst, StringComparison.OrdinalIgnoreCase)) return true;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                bool sameVolume = string.Equals(Path.GetPathRoot(src), Path.GetPathRoot(dst), StringComparison.OrdinalIgnoreCase);
                if (File.Exists(dst))
                {
                    // Already there and unchanged (a link shares its size and time; a copy keeps the source's time):
                    // leave it, or every refresh re-copies gigabytes from another drive.
                    // ponytail: on one volume an unchanged file is taken to be a link; true for NTFS, APFS, ext4.
                    FileInfo s = new(src), d = new(dst);
                    if (s.Length == d.Length && s.LastWriteTimeUtc == d.LastWriteTimeUtc)
                    {
                        if (sameVolume) return true;
                        note = $"Copied {Path.GetFileName(src)}: it is on another drive.";
                        return false;
                    }
                    File.Delete(dst);
                }
                if (TryLink(src, dst)) return true;
                File.Copy(src, dst, true);
                note = string.Equals(Path.GetPathRoot(src), Path.GetPathRoot(dst), StringComparison.OrdinalIgnoreCase)
                    ? $"Copied {Path.GetFileName(src)}: this volume cannot hard link."
                    : $"Copied {Path.GetFileName(src)}: it is on another drive.";
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                note = $"Could not put {Path.GetFileName(src)} into {Path.GetDirectoryName(dst)}: {ex.Message}";
            }
            return false;
        }
    }
}
