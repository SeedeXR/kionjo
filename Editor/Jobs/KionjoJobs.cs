using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Runs one job at a time, queues the rest (render all missing), and tells the window to repaint
    /// while something runs. No exception from a job ever reaches the editor loop.
    /// </summary>
    public static class KionjoJobs
    {
        public static KionjoJob Current { get; private set; }
        public static KionjoJob Last { get; private set; }
        public static int Queued => Queue.Count;
        public static event Action Changed;

        static readonly Queue<KionjoJob> Queue = new();
        static CancellationTokenSource _cts;
        static bool _pumping;

        public static bool IsBusy => Current != null && !Current.IsDone;

        /// <summary>A script reload would drop the job but leave its ffmpeg running: cancel first.</summary>
        [InitializeOnLoadMethod]
        static void HookReload() => AssemblyReloadEvents.beforeAssemblyReload += () =>
        {
            if (IsBusy) Cancel();
        };

        /// <summary>Starts the job now, or queues it behind the running one.</summary>
        public static void Enqueue(KionjoJob job)
        {
            if (IsBusy) Queue.Enqueue(job);
            else Start(job);
            Changed?.Invoke();
        }

        /// <summary>False when another job is running.</summary>
        public static bool Start(KionjoJob job)
        {
            if (IsBusy) return false;
            Current = job;
            _cts = new CancellationTokenSource();
            job.IsRunning = true;
            job.IsDone = job.Failed = false;
            job.Status = "Starting";
            var token = _cts.Token;
            Run(job, token);
            if (!_pumping)
            {
                _pumping = true;
                EditorApplication.update += Pump;
            }
            Changed?.Invoke();
            return true;
        }

        static async void Run(KionjoJob job, CancellationToken token)
        {
            try
            {
                await job.RunAsync(token);
            }
            catch (OperationCanceledException)
            {
                job.Failed = true;
                job.Error = "Cancelled";
            }
            catch (Exception e)
            {
                job.Failed = true;
                job.Error = e.Message;
                Debug.LogException(e);
            }
            finally
            {
                job.IsRunning = false;
                job.IsDone = true;
            }
        }

        /// <summary>Cancels the running job and clears the queue.</summary>
        public static void Cancel()
        {
            Queue.Clear();
            _cts?.Cancel();
            Changed?.Invoke();
        }

        static void Pump()
        {
            if (Current != null && Current.IsDone)
            {
                Last = Current;
                Current = null;
                if (Last.Failed && Last.Error != "Cancelled") Debug.LogWarning($"Kionjo: {Last.Title} failed. {Last.Error}");
                if (Last.Failed && Last.Error == "Cancelled") Queue.Clear(); // e.g. Play mode ended mid render
                else if (Queue.Count > 0) Start(Queue.Dequeue());
            }
            Changed?.Invoke();
            if (Current == null)
            {
                EditorApplication.update -= Pump;
                _pumping = false;
            }
        }
    }
}
