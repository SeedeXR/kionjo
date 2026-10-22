using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Final Cut Pro 7 XML (xmeml v5) of a draft, which Premiere Pro and DaVinci Resolve import as
    /// an editable timeline: picture on V1 (renders and captures by their own files, cards by their
    /// generated seg/ files), then voice, music and SFX on their own audio tracks.
    /// </summary>
    public static class Fcp7XmlWriter
    {
        public static int Frames(float seconds, int fps) => Mathf.RoundToInt(seconds * fps);

        public static string Write(CutPlan plan, string sequenceName, string cutFolder)
        {
            int fps = plan.Fps;
            var files = new Dictionary<string, string>(); // path -> file id
            int clipId = 0;

            XElement Rate() => new("rate", new XElement("timebase", fps), new XElement("ntsc", "FALSE"));

            XElement FileRef(string path, bool video, int durationFrames)
            {
                if (files.TryGetValue(path, out var id)) return new XElement("file", new XAttribute("id", id));
                id = $"file-{files.Count + 1}";
                files[path] = id;
                var media = video
                    ? new XElement("media", new XElement("video", new XElement("samplecharacteristics",
                        new XElement("width", plan.Width), new XElement("height", plan.Height))))
                    : new XElement("media", new XElement("audio", new XElement("channelcount", 2)));
                return new XElement("file", new XAttribute("id", id),
                    new XElement("name", Path.GetFileName(path)),
                    new XElement("pathurl", PathUrl(path)),
                    Rate(),
                    new XElement("duration", durationFrames),
                    media);
            }

            XElement Clip(string name, string path, bool video, float filmStart, float length, float sourceIn)
            {
                int start = Frames(filmStart, fps), end = Frames(filmStart + length, fps);
                int inF = Frames(sourceIn, fps), outF = inF + (end - start);
                var clip = new XElement("clipitem", new XAttribute("id", $"clipitem-{++clipId}"),
                    new XElement("name", name),
                    new XElement("duration", outF),
                    Rate(),
                    new XElement("start", start),
                    new XElement("end", end),
                    new XElement("in", inF),
                    new XElement("out", outF),
                    FileRef(path, video, outF));
                if (!video)
                    clip.Add(new XElement("sourcetrack", new XElement("mediatype", "audio"), new XElement("trackindex", 1)));
                return clip;
            }

            var videoTrack = new XElement("track");
            foreach (var s in plan.Segments)
            {
                bool own = s.Kind == SegmentKind.Video;
                var path = own ? s.SourcePath : Path.Combine(cutFolder, CutCommandBuilder.SegmentFile(s));
                videoTrack.Add(Clip(s.Label, path, true, s.FilmStart, s.Duration, own ? s.SourceIn : 0f));
            }

            XElement AudioTrack(IEnumerable<XElement> clips)
            {
                var t = new XElement("track");
                foreach (var c in clips) t.Add(c);
                return t;
            }

            var voice = plan.Voice.Select(v => Clip(Path.GetFileNameWithoutExtension(v.Path), v.Path, false, v.FilmAt, v.Length, v.TrimIn));
            var music = new List<XElement>();
            float at = plan.Music.Count > 0 ? plan.Music[0].FilmAt : 0f;
            foreach (var m in plan.Music)
            {
                music.Add(Clip(Path.GetFileNameWithoutExtension(m.Path), m.Path, false, at, m.Length, m.TrimIn));
                at += m.Length - plan.MusicCrossfade; // the next cue starts under this one's tail
            }
            var sfx = plan.Sfx.Select(x => Clip(Path.GetFileNameWithoutExtension(x.Path), x.Path, false, x.FilmAt, x.Length > 0f ? x.Length : 1f, 0f));

            int total = Frames(plan.TotalSeconds, fps);
            var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null),
                new XDocumentType("xmeml", null, null, null),
                new XElement("xmeml", new XAttribute("version", "5"),
                    new XElement("sequence", new XAttribute("id", "sequence-1"),
                        new XElement("name", sequenceName),
                        new XElement("duration", total),
                        Rate(),
                        new XElement("media",
                            new XElement("video",
                                new XElement("format", new XElement("samplecharacteristics",
                                    new XElement("width", plan.Width), new XElement("height", plan.Height),
                                    new XElement("pixelaspectratio", "square"), Rate())),
                                videoTrack),
                            new XElement("audio", AudioTrack(voice), AudioTrack(music), AudioTrack(sfx))))));
            var sb = new StringBuilder();
            using (var w = new Utf8StringWriter(sb)) doc.Save(w);
            return sb.ToString();
        }

        /// <summary>file://localhost/D:/BERNARD/UNITY%20PROJECTS%20(GAMES)/x.mp4</summary>
        public static string PathUrl(string path)
        {
            var p = path.Replace('\\', '/');
            var sb = new StringBuilder("file://localhost/");
            foreach (var b in Encoding.UTF8.GetBytes(p.TrimStart('/')))
            {
                char c = (char)b;
                bool safe = b < 128 && (char.IsLetterOrDigit(c) || "-._~/:()!$&'*+,;=@".IndexOf(c) >= 0);
                if (safe) sb.Append(c);
                else sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        sealed class Utf8StringWriter : StringWriter
        {
            public Utf8StringWriter(StringBuilder sb) : base(sb, CultureInfo.InvariantCulture) { }
            public override Encoding Encoding => Encoding.UTF8;
        }
    }
}
