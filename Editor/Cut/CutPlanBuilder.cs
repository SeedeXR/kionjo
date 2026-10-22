using System;
using System.IO;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Walks a storyboard in order and turns it into a CutPlan: picture segments with film start
    /// times, voice lines and subtitles relative to their shot, SFX at recorded events, music.
    /// Sources and take metadata come through resolvers so tests never touch disk.
    /// </summary>
    public static class CutPlanBuilder
    {
        /// <summary>Speech bounds of an audio file (start, end) in seconds; null resolver = manual trims only.</summary>
        public delegate (float start, float end)? SpeechBoundsResolver(KionjoVoiceLine line);

        /// <summary>Real resolvers: newest render or the device capture, the selected take's meta.</summary>
        public static CutPlan Build(KionjoStoryboard sb, KionjoCutPreset preset, IMediaProbe probe, bool animatic = false,
            SpeechBoundsResolver speech = null) =>
            Build(sb, preset, probe, shot => DefaultSource(sb, shot), shot => DefaultTakeMeta(sb, shot), animatic, speech);

        public static CutPlan Build(KionjoStoryboard sb, KionjoCutPreset preset, IMediaProbe probe,
            Func<KionjoShot, string> sourceResolver, Func<KionjoShot, KionjoTakeMeta> takeMetaResolver,
            bool animatic = false, SpeechBoundsResolver speech = null)
        {
            var video = KionjoPresets.Get(preset);
            var plan = new CutPlan
            {
                Width = video.Width,
                Height = video.Height,
                Fps = video.Fps,
                EncoderArgs = video.EncoderArgs,
                BaseName = KionjoTakePaths.Sanitize(string.IsNullOrWhiteSpace(sb.title) ? "Trailer" : sb.title),
                MusicCrossfade = sb.musicCrossfadeSeconds,
                Ducking = sb.ducking,
                Loudness = sb.loudness,
                SubtitleStyle = sb.subtitles,
            };

            float film = 0f;
            int index = 0;
            foreach (var shot in sb.shots)
            {
                var seg = MakeSegment(shot, plan, probe, sourceResolver, animatic);
                if (seg == null) continue;
                index++;
                seg.Label = $"{index:00}_{KionjoTakePaths.Sanitize(shot.code)}";
                seg.FilmStart = film;
                seg.ShotId = shot.id;
                plan.Segments.Add(seg);
                film += seg.Duration;

                AddVoice(shot, seg, plan, probe, speech);
                AddSfx(shot, seg, plan, probe, animatic ? null : takeMetaResolver);
            }

            foreach (var m in sb.music)
            {
                if (string.IsNullOrEmpty(m.audioPath) || !File.Exists(m.audioPath))
                {
                    plan.Warnings.Add($"Music {Path.GetFileName(m.audioPath)}: file not found, skipped");
                    continue;
                }
                float end = m.trimOut > 0f ? m.trimOut : probe.DurationSeconds(m.audioPath);
                if (end <= m.trimIn)
                {
                    plan.Warnings.Add($"Music {Path.GetFileName(m.audioPath)}: length unknown, skipped");
                    continue;
                }
                plan.Music.Add(new AudioCue { Path = m.audioPath, TrimIn = m.trimIn, TrimOut = end, FilmAt = m.at, FadeIn = m.fadeIn, FadeOut = m.fadeOut, Volume = m.volume });
            }
            return plan;
        }

        static CutSegment MakeSegment(KionjoShot shot, CutPlan plan, IMediaProbe probe, Func<KionjoShot, string> sourceResolver, bool animatic)
        {
            if (shot.type == KionjoShotType.Card) return CardSegment(shot, plan);

            if (animatic || shot.type == KionjoShotType.Still)
            {
                if (!string.IsNullOrEmpty(shot.thumbnailPath) && File.Exists(shot.thumbnailPath))
                    return new CutSegment { Kind = SegmentKind.Image, SourcePath = shot.thumbnailPath, Duration = shot.targetSeconds, FadeIn = shot.fadeIn, FadeOut = shot.fadeOut };
                if (shot.type == KionjoShotType.Still && !animatic)
                    plan.Warnings.Add($"{shot.code}: still has no thumbnail, shown as a title card");
                return new CutSegment { Kind = SegmentKind.Text, Duration = shot.targetSeconds, Lines = { Title(shot) } };
            }

            if (shot.outputMode == KionjoOutputMode.OrganiseOnly)
            {
                plan.Notes.Add($"{shot.code}: organise only, left out of the cut");
                return null;
            }

            var src = sourceResolver(shot);
            if (string.IsNullOrEmpty(src))
            {
                plan.Warnings.Add($"{shot.code}: no source, skipped (render the take or import a capture)");
                return null;
            }
            float end = shot.outPoint > shot.inPoint ? shot.outPoint : probe.DurationSeconds(src);
            if (end <= shot.inPoint)
            {
                plan.Warnings.Add($"{shot.code}: source length unknown, using the target length");
                end = shot.inPoint + shot.targetSeconds;
            }
            return new CutSegment
            {
                Kind = SegmentKind.Video,
                SourcePath = src,
                SourceIn = shot.inPoint,
                Duration = end - shot.inPoint,
                FadeIn = shot.fadeIn,
                FadeOut = shot.fadeOut,
                Crop = shot.crop,
            };
        }

        static CutSegment CardSegment(KionjoShot shot, CutPlan plan)
        {
            var seg = new CutSegment { Duration = shot.targetSeconds, FadeIn = shot.fadeIn, FadeOut = shot.fadeOut };
            switch (shot.cardKind)
            {
                case KionjoCardKind.Text:
                    seg.Kind = SegmentKind.Text;
                    seg.Lines.AddRange(shot.cardLines);
                    break;
                case KionjoCardKind.Image when !string.IsNullOrEmpty(shot.cardImagePath) && File.Exists(shot.cardImagePath):
                    seg.Kind = SegmentKind.Image;
                    seg.SourcePath = shot.cardImagePath;
                    seg.PushIn = Mathf.Max(1f, shot.cardPushIn);
                    break;
                case KionjoCardKind.Image:
                    plan.Warnings.Add($"{shot.code}: card image not found, shown as black");
                    seg.Kind = SegmentKind.Black;
                    break;
                default:
                    seg.Kind = SegmentKind.Black;
                    break;
            }
            return seg;
        }

        static void AddVoice(KionjoShot shot, CutSegment seg, CutPlan plan, IMediaProbe probe, SpeechBoundsResolver speech)
        {
            foreach (var line in shot.voice)
            {
                if (string.IsNullOrEmpty(line.audioPath) || !File.Exists(line.audioPath))
                {
                    plan.Warnings.Add($"{shot.code}: voice {Path.GetFileName(line.audioPath)} not found, skipped");
                    continue;
                }
                float trimIn = line.trimIn, trimOut = line.trimOut;
                if (line.trimMode != KionjoTrimMode.Manual && speech != null)
                {
                    var bounds = speech(line);
                    if (bounds.HasValue)
                    {
                        trimIn = bounds.Value.start;
                        trimOut = bounds.Value.end;
                    }
                }
                if (trimOut <= 0f) trimOut = probe.DurationSeconds(line.audioPath);
                if (trimOut <= trimIn)
                {
                    plan.Warnings.Add($"{shot.code}: voice {Path.GetFileName(line.audioPath)} has no length, skipped");
                    continue;
                }
                var cue = new AudioCue
                {
                    Path = line.audioPath,
                    TrimIn = trimIn,
                    TrimOut = trimOut,
                    FilmAt = seg.FilmStart + line.at,
                    FadeIn = line.fadeIn,
                    FadeOut = line.fadeOut,
                };
                plan.Voice.Add(cue);
                if (!string.IsNullOrWhiteSpace(line.subtitle))
                    plan.Subtitles.Add(new SubtitleCue { Start = cue.FilmAt, End = cue.FilmAt + cue.Length, Text = line.subtitle, OverrideTags = line.overrideTags });
            }
        }

        static void AddSfx(KionjoShot shot, CutSegment seg, CutPlan plan, IMediaProbe probe, Func<KionjoShot, KionjoTakeMeta> takeMetaResolver)
        {
            if (shot.sfx.Count == 0) return;
            KionjoTakeMeta meta = null;
            bool metaLoaded = false;
            foreach (var cue in shot.sfx)
            {
                if (string.IsNullOrEmpty(cue.audioPath) || !File.Exists(cue.audioPath))
                {
                    plan.Warnings.Add($"{shot.code}: SFX {Path.GetFileName(cue.audioPath)} not found, skipped");
                    continue;
                }
                float at;
                if (string.IsNullOrEmpty(cue.eventName))
                {
                    at = seg.FilmStart + cue.at;
                }
                else
                {
                    if (!metaLoaded)
                    {
                        meta = takeMetaResolver?.Invoke(shot);
                        metaLoaded = true;
                    }
                    var e = meta?.events.Find(ev => ev.name == cue.eventName);
                    if (e == null)
                    {
                        plan.Warnings.Add($"{shot.code}: SFX event \"{cue.eventName}\" not in the take, skipped");
                        continue;
                    }
                    float rel = e.t - seg.SourceIn;
                    if (rel < 0f || rel > seg.Duration)
                    {
                        plan.Warnings.Add($"{shot.code}: SFX event \"{cue.eventName}\" at {e.t:0.00} s is outside the shot's range, skipped");
                        continue;
                    }
                    at = seg.FilmStart + rel;
                }
                float len = probe.DurationSeconds(cue.audioPath);
                plan.Sfx.Add(new AudioCue { Path = cue.audioPath, TrimIn = 0f, TrimOut = len > 0f ? len : 0f, FilmAt = at, Volume = cue.volume });
            }
        }

        static string Title(KionjoShot shot) => string.IsNullOrWhiteSpace(shot.name) ? shot.code : $"{shot.code}  {shot.name}";

        public static string DefaultSource(KionjoStoryboard sb, KionjoShot shot)
        {
            if (shot.outputMode == KionjoOutputMode.DeviceCapture)
                return !string.IsNullOrEmpty(shot.deviceCapturePath) && File.Exists(shot.deviceCapturePath) ? shot.deviceCapturePath : null;
            return KionjoPaths.ExistingRender(sb, shot);
        }

        public static KionjoTakeMeta DefaultTakeMeta(KionjoStoryboard sb, KionjoShot shot)
        {
            var take = KionjoPaths.SelectedTakeFolder(sb, shot);
            return take != null ? KionjoTakeMeta.Load(take) : null;
        }
    }
}
