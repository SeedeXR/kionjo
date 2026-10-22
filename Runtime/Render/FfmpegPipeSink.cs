#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Unity.Collections;

namespace SeedeXR.Kionjo
{
    /// <summary>
    /// Encodes raw RGBA frames with ffmpeg reading stdin. A bounded queue and a writer thread keep
    /// the main thread from blocking on the pipe; stderr is drained asynchronously so ffmpeg never
    /// stalls on a full error pipe. Editor only (it lives in Runtime so Play Mode tests reach it).
    /// </summary>
    public sealed class FfmpegPipeSink : IKionjoFrameSink
    {
        readonly string _ffmpeg, _output;
        readonly string[] _encoderArgs;
        readonly int _queueFrames;
        Process _process;
        BlockingCollection<byte[]> _queue;
        Thread _writer;
        readonly Queue<string> _stderr = new();
        volatile string _error;

        public string Error => _error;
        public int ExitCode { get; private set; } = -1;

        public FfmpegPipeSink(string ffmpegExe, string outputMp4, string[] encoderArgs, int queueFrames = 3)
        {
            _ffmpeg = ffmpegExe;
            _output = outputMp4;
            _encoderArgs = encoderArgs ?? new[] { "-c:v", "libx264", "-crf", "18", "-preset", "medium" };
            _queueFrames = Math.Max(1, queueFrames);
        }

        public void Begin(int width, int height, float fps, bool flipVertically)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_output) ?? ".");
            var args = new List<string>
            {
                "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgba",
                "-s", $"{width}x{height}", "-r", fps.ToString("0.###", CultureInfo.InvariantCulture), "-i", "-",
            };
            if (flipVertically) args.AddRange(new[] { "-vf", "vflip" });
            args.AddRange(new[] { "-pix_fmt", "yuv420p" });
            args.AddRange(_encoderArgs);
            args.AddRange(new[] { "-movflags", "+faststart", _output });

            var psi = new ProcessStartInfo(_ffmpeg, JoinArgs(args))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardErrorEncoding = Encoding.UTF8,
            };
            _process = new Process { StartInfo = psi };
            _process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                lock (_stderr)
                {
                    _stderr.Enqueue(e.Data);
                    while (_stderr.Count > 20) _stderr.Dequeue();
                }
            };
            _process.OutputDataReceived += (_, _) => { };
            _process.Start();
            _process.BeginErrorReadLine();
            _process.BeginOutputReadLine();

            _queue = new BlockingCollection<byte[]>(_queueFrames);
            var stdin = _process.StandardInput.BaseStream;
            _writer = new Thread(() =>
            {
                try
                {
                    foreach (var frame in _queue.GetConsumingEnumerable()) stdin.Write(frame, 0, frame.Length);
                }
                catch (IOException e)
                {
                    _error = $"ffmpeg stopped reading frames: {e.Message}\n{Tail()}";
                }
            }) { IsBackground = true, Name = "Kionjo ffmpeg writer" };
            _writer.Start();
        }

        public void Push(NativeArray<byte> rgba)
        {
            if (_queue == null || _queue.IsAddingCompleted || _error != null) return;
            _queue.Add(rgba.ToArray());
        }

        public void End()
        {
            if (_queue == null) return;
            _queue.CompleteAdding();
            _writer.Join();
            try
            {
                _process.StandardInput.Close();
            }
            catch (IOException)
            {
                // ffmpeg already closed its end; the exit code tells the story
            }
            if (!_process.WaitForExit(120000))
            {
                Kill();
                _error ??= "ffmpeg did not finish within 2 minutes after the last frame.";
            }
            else
            {
                _process.WaitForExit();
                ExitCode = _process.ExitCode;
                if (ExitCode != 0) _error ??= $"ffmpeg exited with {ExitCode}:\n{Tail()}";
            }
            _queue = null;
            _process.Dispose();
            _process = null;
        }

        public void Dispose()
        {
            if (_queue != null && !_queue.IsAddingCompleted) _queue.CompleteAdding();
            Kill();
            _writer?.Join(2000);
            _process?.Dispose();
            _process = null;
            _queue = null;
        }

        void Kill()
        {
            try
            {
                if (_process != null && !_process.HasExited) _process.Kill();
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
        }

        string Tail()
        {
            lock (_stderr) return string.Join("\n", _stderr);
        }

        static string JoinArgs(List<string> args)
        {
            var sb = new StringBuilder();
            foreach (var a in args)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(a.IndexOf(' ') >= 0 || a.Length == 0 ? "\"" + a.Replace("\"", "\\\"") + "\"" : a);
            }
            return sb.ToString();
        }
    }
}
#endif
