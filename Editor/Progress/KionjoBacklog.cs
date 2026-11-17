using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// The capture backlog. A step's version exists until the next entry of the same feature (same
    /// project) is written; pending captures of a superseded step are dropped, never filmed.
    /// </summary>
    public static class KionjoBacklog
    {
        public static bool IsCurrent(JournalEntry e, IReadOnlyList<JournalEntry> all)
        {
            if (e == null || string.IsNullOrEmpty(e.feature) || all == null) return true;
            return !all.Any(o => o != null && o != e && o.feature == e.feature && (o.project ?? "") == (e.project ?? "") && IsLater(o, e));
        }

        static bool IsLater(JournalEntry a, JournalEntry b)
        {
            int c = string.CompareOrdinal(a.created ?? "", b.created ?? "");
            if (c != 0) return c > 0;
            return string.CompareOrdinal(Path.GetFileName(a.Folder ?? ""), Path.GetFileName(b.Folder ?? "")) > 0;
        }

        public static List<(JournalEntry entry, KionjoCaptureRequest request)> Pending(IReadOnlyList<JournalEntry> all)
        {
            var list = new List<(JournalEntry, KionjoCaptureRequest)>();
            if (all == null) return list;
            foreach (var e in all.Where(e => e != null && IsCurrent(e, all)))
                foreach (var r in e.captures ?? new List<KionjoCaptureRequest>())
                    if (r.state == KionjoCaptureRequest.States.Pending) list.Add((e, r));
            return list;
        }

        /// <summary>Marks every pending capture of a superseded step dropped (saved). Returns how many.</summary>
        public static int DropSuperseded(IReadOnlyList<JournalEntry> all)
        {
            int count = 0;
            if (all == null) return 0;
            foreach (var e in all.Where(e => e != null && !IsCurrent(e, all) && (e.captures?.Any(r => r.state == KionjoCaptureRequest.States.Pending) ?? false)).ToList())
            {
                string reason = $"A later step of {e.feature} exists; this version is gone.";
                int n = 0;
                var saved = KionjoJournal.Update(e.Folder, x =>
                {
                    foreach (var r in x.captures.Where(r => r.state == KionjoCaptureRequest.States.Pending))
                    {
                        r.state = KionjoCaptureRequest.States.Dropped;
                        r.reason = reason;
                        n++;
                    }
                });
                if (saved != null) e.captures = saved.captures;
                count += n;
            }
            return count;
        }
    }
}
