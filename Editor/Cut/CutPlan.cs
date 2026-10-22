using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    public enum SegmentKind { Video, Image, Black, Text }

    /// <summary>One piece of the picture track, in film order.</summary>
    public sealed class CutSegment
    {
        public string Label;          // "01_S03", also the seg/ file name
        public SegmentKind Kind;
        public string SourcePath;     // absolute; video or image
        public float SourceIn;
        public float Duration;
        public float FilmStart;
        public float FadeIn, FadeOut;
        public Rect Crop;             // normalised; zero = none
        public float PushIn = 1f;     // image cards
        public List<string> Lines = new();
        public string ShotId;
    }

    public sealed class AudioCue
    {
        public string Path;           // absolute
        public float TrimIn, TrimOut; // seconds in the file; TrimOut is resolved (never 0 in a plan)
        public float FilmAt;
        public float FadeIn, FadeOut;
        public float Volume = 1f;
        public float Length => Mathf.Max(0f, TrimOut - TrimIn);
    }

    public sealed class SubtitleCue
    {
        public float Start, End;
        public string Text;
        public string OverrideTags;
    }

    /// <summary>Everything ffmpeg needs to assemble a draft, with no file system access left to do.</summary>
    public sealed class CutPlan
    {
        public int Width, Height, Fps;
        public string[] EncoderArgs;
        public string BaseName;
        public List<CutSegment> Segments = new();
        public List<AudioCue> Voice = new(), Music = new(), Sfx = new();
        public float MusicCrossfade;
        public KionjoDucking Ducking = new();
        public KionjoLoudness Loudness = new();
        public KionjoSubtitleStyle SubtitleStyle = new();
        public List<SubtitleCue> Subtitles = new();
        public List<string> Warnings = new();
        public List<string> Notes = new();

        public float TotalSeconds => Segments.Sum(s => s.Duration);
        public bool HasAudio => Voice.Count + Music.Count + Sfx.Count > 0;
    }
}
