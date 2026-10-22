using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// A long operation (render, cut, animatic) with progress and cancel. RunAsync may continue on a
    /// worker thread: it only touches files and its own fields after its first await.
    /// </summary>
    public abstract class KionjoJob
    {
        public string Title = "Job";
        public volatile float Progress;
        public volatile string Status = "";
        public volatile bool IsRunning, IsDone, Failed;
        public string Error;
        public readonly List<string> Log = new();

        public abstract Task RunAsync(CancellationToken ct);

        protected void AddLog(string line)
        {
            lock (Log) Log.Add(line);
        }

        public string LogText()
        {
            lock (Log) return string.Join("\n", Log);
        }

        /// <summary>IProgress that writes straight into Progress (no SynchronizationContext hop).</summary>
        protected sealed class StepProgress : IProgress<float>
        {
            readonly KionjoJob _job;
            readonly int _index, _count;

            public StepProgress(KionjoJob job, int index, int count)
            {
                _job = job;
                _index = index;
                _count = Math.Max(1, count);
            }

            public void Report(float value) => _job.Progress = (_index + Math.Max(0f, Math.Min(1f, value))) / _count;
        }
    }
}
