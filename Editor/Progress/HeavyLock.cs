using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// A machine-wide lock for heavy work: &lt;folder&gt;/heavy.lock holds who runs what and how many GB.
    /// Created exclusively; a lock whose process is gone is stale and replaced. Any tool or agent can
    /// read it to see who is busy.
    /// </summary>
    public sealed class HeavyLock : IDisposable
    {
        public const string FileName = "heavy.lock";

        [Serializable]
        class Holder
        {
            public string owner;
            public int pid;
            public double gb;
            public string since;
        }

        readonly string _file;
        readonly int _pid;
        bool _released;

        HeavyLock(string file, int pid)
        {
            _file = file;
            _pid = pid;
        }

        /// <summary>The lock, or null with the current holder described in holder.</summary>
        public static HeavyLock TryAcquire(string folder, string owner, double gb, out string holder)
        {
            holder = null;
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, FileName);
            int pid = Process.GetCurrentProcess().Id;
            var json = JsonUtility.ToJson(new Holder { owner = owner, pid = pid, gb = gb, since = DateTime.Now.ToString("s", CultureInfo.InvariantCulture) }, true);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    using (var s = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        var bytes = new UTF8Encoding(false).GetBytes(json);
                        s.Write(bytes, 0, bytes.Length);
                    }
                    return new HeavyLock(file, pid);
                }
                catch (IOException) when (File.Exists(file))
                {
                    var h = Read(file);
                    if (h != null && Alive(h.pid))
                    {
                        holder = Describe(h);
                        return null;
                    }
                    // Stale (its process is gone) or unreadable: replace it once.
                    try { File.Delete(file); }
                    catch (IOException) { }
                }
            }
            holder = Describe(folder);
            return null;
        }

        /// <summary>"capture hero (pid 1234, 3.0 GB, since ...)", or "free".</summary>
        public static string Describe(string folder)
        {
            var file = Path.Combine(folder ?? "", FileName);
            var h = File.Exists(file) ? Read(file) : null;
            return h == null ? "free" : Describe(h);
        }

        static string Describe(Holder h) =>
            string.Format(CultureInfo.InvariantCulture, "{0} (pid {1}, {2:0.0} GB, since {3})", h.owner, h.pid, h.gb, h.since);

        static Holder Read(string file)
        {
            try { return JsonUtility.FromJson<Holder>(File.ReadAllText(file)); }
            catch (Exception) { return null; }
        }

        static bool Alive(int pid)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                return !p.HasExited;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Releases this process's lock in folder (after a domain reload lost the instance).</summary>
        public static void ReleaseOwn(string folder) =>
            new HeavyLock(Path.Combine(folder ?? "", FileName), Process.GetCurrentProcess().Id).Dispose();

        /// <summary>Deletes the lock file only while it is still this process's lock.</summary>
        public void Dispose()
        {
            if (_released) return;
            _released = true;
            var h = File.Exists(_file) ? Read(_file) : null;
            if (h != null && h.pid == _pid)
                try { File.Delete(_file); }
                catch (IOException) { }
        }
    }
}
