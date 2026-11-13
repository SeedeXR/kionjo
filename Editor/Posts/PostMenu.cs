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
