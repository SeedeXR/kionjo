using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>One piece of footage on the post's timeline, already resolved against the file.</summary>
    public class PostSegment
    {
        public string Label;
        public string SourcePath;
        public float SourceIn;
        public float Duration;
        public PostFit Fit;
        public float CropCentre = 0.5f;
        public int SrcW, SrcH;
        public bool HasAudio;
    }

    /// <summary>Everything ffmpeg needs to build a post, with no file system access left to do.</summary>
    public class PostPlan
    {
        public int Width = 1080, Height = 1920, Fps = 30;
        public List<PostSegment> Segments = new();
        public List<TimedText> Texts = new();
        public List<WordTimer.Word> CaptionWords = new();
        public bool ClipSound;
        public float ClipVolume = 1f;
        public string MusicPath;
        public float MusicVolume = 0.8f;
        public float MusicStart;
        public bool DuckMusicUnderSpeech;
        public float TotalSeconds;
        public string Name = "post";
        public float CoverTime;
        public string CoverTitle;
        public KionjoBrandKit Kit;
        public List<string> Warnings = new();

        public bool HasMusic => !string.IsNullOrEmpty(MusicPath);
    }

    public static class PostPlanBuilder
    {
        /// <summary>How long a still image holds when its item has no out point.</summary>
        public const float StillSeconds = 3f;
        public const float LoopSeconds = 0.5f;

        public static PostPlan Build(KionjoPost post, IMediaInfo info, Func<string, List<WordTimer.Word>> words, KionjoBrandKit kit = null)
        {
            var plan = new PostPlan
            {
                ClipSound = post.clipSound,
                ClipVolume = post.clipVolume,
                MusicPath = string.IsNullOrWhiteSpace(post.musicPath) ? null : post.musicPath,
                MusicVolume = post.musicVolume,
                MusicStart = post.musicStart,
                Name = string.IsNullOrWhiteSpace(post.name) ? "post" : post.name,
                CoverTitle = post.coverTitle,
                Kit = kit,
            };
            float t = 0f;
            var starts = new List<float>();
            for (int i = 0; i < post.items.Count; i++)
            {
                var item = post.items[i];
                float fileSeconds = info.Duration(item.path);
                float d = item.outPoint > item.inPoint ? item.outPoint - item.inPoint : fileSeconds - item.inPoint;
                if (d <= 0f) d = StillSeconds;
                var size = info.Size(item.path);
                plan.Segments.Add(new PostSegment
                {
                    Label = $"{i + 1:00}-{KionjoJournal.Slug(Path.GetFileNameWithoutExtension(item.path ?? ""))}",
                    SourcePath = item.path,
                    SourceIn = item.inPoint,
                    Duration = d,
                    Fit = item.fit,
                    CropCentre = item.cropCentre,
                    SrcW = size.w,
                    SrcH = size.h,
                    HasAudio = info.HasAudio(item.path),
                });
                starts.Add(t);
                t += d;
            }
            if (post.end == PostEnd.Loop && plan.Segments.Count > 0)
            {
                var first = plan.Segments[0];
                float d = Mathf.Min(LoopSeconds, first.Duration);
                plan.Segments.Add(new PostSegment
                {
                    Label = $"{plan.Segments.Count + 1:00}-loop",
                    SourcePath = first.SourcePath, SourceIn = first.SourceIn, Duration = d,
                    Fit = first.Fit, CropCentre = first.CropCentre, SrcW = first.SrcW, SrcH = first.SrcH, HasAudio = first.HasAudio,
                });
                t += d;
            }
            plan.TotalSeconds = t;

            // The call to action ends 0.1 s before the end; the body has to finish 0.2 s before it starts.
            string endText = post.end == PostEnd.CallToAction && !string.IsNullOrWhiteSpace(post.endText) ? post.endText.Trim() : null;
            float endStart = endText != null ? t - 0.1f - PostTiming.ReadingSeconds(endText) : t;
            if (endText != null && endStart < PostTiming.HookStart) endText = null;
            float bodyLimit = endText != null ? endStart - PostTiming.Gap + PostTiming.EndMargin : t;
            plan.Texts = PostTiming.Schedule(post.hook, post.overlays, bodyLimit);
            int wanted = (post.overlays ?? new List<string>()).Count(o => !string.IsNullOrWhiteSpace(o));
            int dropped = wanted - plan.Texts.Count(x => !x.IsHook);
            if (dropped > 0) plan.Warnings.Add($"{dropped} overlay(s) did not fit and were left out.");
            if (endText != null) plan.Texts.Add(new TimedText { Text = endText, Start = endStart, End = t - 0.1f });

            for (int i = 0; i < post.items.Count; i++)
            {
                if (!post.items[i].captionSpeech || words == null) continue;
                var seg = plan.Segments[i];
                foreach (var w in words(seg.SourcePath) ?? new List<WordTimer.Word>())
                {
                    if (w.s < seg.SourceIn || w.s >= seg.SourceIn + seg.Duration) continue;
                    plan.CaptionWords.Add(new WordTimer.Word
                    {
                        w = w.w,
                        s = starts[i] + w.s - seg.SourceIn,
                        e = Mathf.Min(starts[i] + w.e - seg.SourceIn, starts[i] + seg.Duration),
                    });
                }
            }
            plan.DuckMusicUnderSpeech = plan.HasMusic && post.items.Any(i => i.captionSpeech);
            plan.CoverTime = Mathf.Clamp(post.coverTime, 0f, Mathf.Max(0f, t - 0.1f));
            return plan;
        }
    }
}
