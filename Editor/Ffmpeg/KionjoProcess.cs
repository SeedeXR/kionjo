using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Runs an FfmpegCommand without a shell. Output is read asynchronously (no pipe deadlock),
    /// progress comes from ffmpeg's -progress lines, and cancelling kills the process.
    /// </summary>
    public static class KionjoProcess
    {
        public sealed class Result
        {
            public int ExitCode;
            public string StdErrTail = "";
            public string StdOut = "";
            public bool Cancelled;
            public bool TimedOut;
        }

        const int TailLines = 20;

        static bool IsFfmpeg(string exe)
        {
            var name = Path.GetFileNameWithoutExtension(exe ?? "").ToLowerInvariant();
            return name == "ffmpeg";
        }

        static ProcessStartInfo StartInfo(FfmpegCommand cmd, bool progress)
        {
            var args = new List<string>();
            if (IsFfmpeg(cmd.Executable))
            {
                args.Add("-nostdin");
                if (progress) args.AddRange(new[] { "-progress", "pipe:1", "-nostats" });
            }
            args.AddRange(cmd.Args);
            var line = new FfmpegCommand { Args = args }.ArgumentString();
            var psi = new ProcessStartInfo(cmd.Executable, line)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            if (!string.IsNullOrEmpty(cmd.WorkingDirectory)) psi.WorkingDirectory = cmd.WorkingDirectory;
            return psi;
        }

        /// <summary>Synchronous run for quick probes. Kills the process after timeoutMs.</summary>
        public static Result Run(FfmpegCommand cmd, int timeoutMs = 60000)
        {
            var stdout = new StringBuilder();
            var stderr = new Queue<string>();
            using var p = new Process { StartInfo = StartInfo(cmd, progress: false) };
            p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (stdout) stdout.AppendLine(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data != null) Enqueue(stderr, e.Data); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            bool exited = p.WaitForExit(timeoutMs);
            if (!exited) TryKill(p);
            p.WaitForExit();
            return new Result
            {
                ExitCode = exited ? p.ExitCode : -1,
                StdOut = stdout.ToString(),
                StdErrTail = Tail(stderr),
                TimedOut = !exited,
            };
        }

        /// <summary>
        /// Runs in the background. Progress is 0..1 against cmd.ExpectedSeconds when ffmpeg reports
        /// out_time. Every stderr line is also written to log when given.
        /// </summary>
        public static Task<Result> RunAsync(FfmpegCommand cmd, CancellationToken ct, IProgress<float> progress = null, TextWriter log = null)
        {
            return Task.Run(() =>
            {
                var stdout = new StringBuilder();
                var stderr = new Queue<string>();
                using var p = new Process { StartInfo = StartInfo(cmd, progress: true) };
                p.OutputDataReceived += (_, e) =>
                {
                    if (e.Data == null) return;
                    if (progress != null && cmd.ExpectedSeconds > 0f && TryParseOutTime(e.Data, out double seconds))
                        progress.Report((float)Math.Min(1.0, seconds / cmd.ExpectedSeconds));
                    if (!e.Data.Contains("=")) lock (stdout) stdout.AppendLine(e.Data);
                };
                p.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data == null) return;
                    Enqueue(stderr, e.Data);
                    if (log != null) lock (log) log.WriteLine(e.Data);
                };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                bool cancelled = false;
                using (ct.Register(() =>
                       {
                           cancelled = true;
                           TryKill(p);
                       }))
                {
                    p.WaitForExit();
                }
                p.WaitForExit(); // drains the async readers
                return new Result
                {
                    ExitCode = cancelled ? -1 : p.ExitCode,
                    StdOut = stdout.ToString(),
                    StdErrTail = Tail(stderr),
                    Cancelled = cancelled,
                };
            });
        }

        /// <summary>Parses "out_time_us=1234567" or "out_time_ms=1234567" (both microseconds in ffmpeg 7).</summary>
        public static bool TryParseOutTime(string line, out double seconds)
        {
            seconds = 0;
            int eq = line.IndexOf('=');
            if (eq < 0) return false;
            var key = line.Substring(0, eq);
            if (key != "out_time_us" && key != "out_time_ms") return false;
            if (!long.TryParse(line.Substring(eq + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long us)) return false;
            seconds = us / 1_000_000.0;
            return true;
        }

        static void Enqueue(Queue<string> q, string line)
        {
            lock (q)
            {
                q.Enqueue(line);
                while (q.Count > TailLines) q.Dequeue();
            }
        }

        static string Tail(Queue<string> q)
        {
            lock (q) return string.Join("\n", q);
        }

        static void TryKill(Process p)
        {
            try
            {
                if (!p.HasExited) p.Kill();
            }
            catch (InvalidOperationException)
            {
                // already gone
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // exiting while we killed it
            }
        }
    }
}
