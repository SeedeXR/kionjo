using System.IO;
using UnityEditor;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Tools > Kionjo > Build post from journal entry: builds the entry's latest post (or a first one
    /// made from the entry) with the active brand kit, then reveals the bundle. No storyboard needed.
    /// </summary>
    public static class PostMenu
    {
        [MenuItem("Tools/Kionjo/Build post from journal entry")]
        static void BuildFromEntry()
        {
            var file = EditorUtility.OpenFilePanel("Journal entry", KionjoJournal.Folder, "json");
            if (string.IsNullOrEmpty(file)) return;
            var job = CreateJob(Path.GetDirectoryName(file), out var error);
            if (job == null)
            {
                EditorUtility.DisplayDialog("Kionjo", error, "OK");
                return;
            }
            void Watch()
            {
                if (!job.IsDone) return;
                KionjoJobs.Changed -= Watch;
                if (job.Failed)
                {
                    Debug.LogWarning($"Kionjo: post failed. {job.Error}");
                    return;
                }
                foreach (var w in job.Warnings) Debug.LogWarning($"Kionjo post: {w}");
                Debug.Log($"Kionjo: post built in {job.Folder}");
                EditorUtility.RevealInFinder(job.OutputMp4);
            }
            KionjoJobs.Changed += Watch;
            KionjoJobs.Enqueue(job);
        }

        /// <summary>
        /// Builds the entry's latest post (or a first one made from the entry) now, on this thread: for agents and
        /// batch runs. Returns the finished job, or null with the reason.
        /// </summary>
        public static PostJob BuildNow(string entryFolder, out string error)
        {
            var job = CreateJob(entryFolder, out error);
            if (job == null) return null;
            try
            {
                job.RunAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (System.Exception ex)
            {
                job.Failed = true;
                job.Error = ex.Message;
            }
            job.IsDone = true;
            if (job.Failed) error = job.Error;
            return job;
        }

        /// <summary>
        /// Batch entry point: Unity -batchmode -projectPath P -executeMethod
        /// SeedeXR.Kionjo.EditorTools.PostMenu.RunFromCommandLine -kionjoPost &lt;entry folder&gt; (no -quit: the editor
        /// exits by itself, 0 when the post was built, 1 when not).
        /// </summary>
        public static void RunFromCommandLine()
        {
            var args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, "-kionjoPost");
            if (at < 0 || at + 1 >= args.Length || !Directory.Exists(args[at + 1]))
            {
                Debug.LogError("Kionjo: -kionjoPost <entry folder> is missing or not found.");
                EditorApplication.Exit(2);
                return;
            }
            var job = BuildNow(args[at + 1], out var error);
            if (job == null || job.Failed)
            {
                Debug.LogError($"Kionjo: post failed. {error}");
                EditorApplication.Exit(1);
                return;
            }
            foreach (var w in job.Warnings) Debug.LogWarning($"Kionjo post: {w}");
            Debug.Log($"Kionjo: post built in {job.Folder}: {job.OutputMp4}");
            EditorApplication.Exit(0);
        }

        /// <summary>A job for the entry in entryFolder, or null with the reason.</summary>
        public static PostJob CreateJob(string entryFolder, out string error)
        {
            var e = KionjoJournal.Load(entryFolder, out error);
            if (e == null) return null;
            var ffmpeg = FfmpegLocator.FindFfmpeg();
            if (ffmpeg == null)
            {
                error = "ffmpeg was not found. Set it in Preferences > Kionjo.";
                return null;
            }
            var settings = KionjoPostsSettings.instance;
            var kit = settings.ActiveKit;
            var post = e.posts.Count > 0 ? e.posts[e.posts.Count - 1] : KionjoPost.FromEntry(e);
            if (post.end == PostEnd.CallToAction && string.IsNullOrWhiteSpace(post.endText) && kit != null) post.endText = kit.callToAction;
            return new PostJob(e, post, kit, KionjoPaths.PostsRoot(settings.outputFolder), ffmpeg, new FfprobeMediaInfo(), path => WordTimer.Words(path, out _));
        }
    }
}
