using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    public enum KionjoShotType
    {
        [Tooltip("Performed by a person (headset or camera), recorded, replayed and rendered.")] Performance,
        [Tooltip("A camera that already moves on its own (Cinemachine, animation): rendered frame locked for its target length.")] LiveCamera,
        [Tooltip("A single frame held for its target length.")] Still,
        [Tooltip("Black, a text card or an image card (opening line, closing lines, cover).")] Card,
    }

    public enum KionjoOutputMode
    {
        [Tooltip("Replay the take in the editor and render it offline at the render preset.")] OfflineRender,
        [Tooltip("Use the headset's own video capture and only trim it.")] DeviceCapture,
        [Tooltip("Keep files organised per shot; this shot is left out of the cut.")] OrganiseOnly,
    }

    public enum KionjoCardKind { Black, Text, Image }

    public enum KionjoTrimMode
    {
        [Tooltip("Use trim in and trim out exactly as typed.")] Manual,
        [Tooltip("Trim leading and trailing silence at -55 dB.")] Silence,
        [Tooltip("Trim to the first and last spoken word (needs faster-whisper).")] Words,
    }

    public enum KionjoRenderPreset { Preview720p30, HD1080p60, UHD2160p60 }
    public enum KionjoCutPreset { Preview720p30, HD1080p30, UHD2160p60 }

    [Serializable]
    public class KionjoShot
    {
        [Tooltip("Stable id, assigned on creation. Thumbnails and the state file use it.")]
        public string id;
        [Tooltip("Short label used in folder names, for example S07.")]
        public string code = "S01";
        [Tooltip("What the shot shows, for example \"Crane down on the dome\".")]
        public string name;
        [Tooltip("Story beat this shot carries (AND, BUT, THEREFORE, or free text).")]
        public string beat;
        [Tooltip("Where it happens (outside, inside, question 5). Shown as a tag on the card.")]
        public string location;
        public KionjoShotType type;
        public KionjoOutputMode outputMode;
        [Tooltip("Planned length in seconds. Used by the animatic, cards, stills and live camera shots.")]
        public float targetSeconds = 4f;
        [Tooltip("Take folder to use, for example S10_take03. Empty uses the newest take.")]
        public string selectedTake;
        [Tooltip("Seconds into the render or device capture where the shot starts in the cut.")]
        public float inPoint;
        [Tooltip("Seconds into the render or device capture where the shot ends. 0 = the end.")]
        public float outPoint;
        [Tooltip("Fade from black at the start, in seconds.")]
        public float fadeIn;
        [Tooltip("Fade to black at the end, in seconds.")]
        public float fadeOut;
        [Tooltip("Horizontal field of view for the render. 0 keeps the camera's own lens.")]
        public float horizontalFov;
        [Tooltip("Normalised crop (x, y, width, height). All zero = no crop.")]
        public Rect crop;
        [Tooltip("Passed to every IKionjoStageHandler in the scene when the shot is staged.")]
        public string stageCommand;
        [Tooltip("Scene objects deactivated when the shot is staged (hierarchy paths).")]
        public List<string> hidePaths = new();
        [Tooltip("Scene objects activated when the shot is staged (hierarchy paths).")]
        public List<string> showPaths = new();
        public List<KionjoVoiceLine> voice = new();
        public List<KionjoSfxCue> sfx = new();
        [TextArea(2, 6)] public string notes;
        [Tooltip("Device capture mode: the headset video file for this shot.")]
        [KionjoPath("mp4,mov,mkv")] public string deviceCapturePath;
        public KionjoCardKind cardKind;
        [Tooltip("Text card lines, shown one after another.")]
        public List<string> cardLines = new();
        [Tooltip("Image card: the picture to show (for example the store cover).")]
        [KionjoPath("png,jpg,jpeg")] public string cardImagePath;
        [Tooltip("Image card: slow push in, end scale. 1 = none.")]
        public float cardPushIn = 1.04f;
        [Tooltip("Thumbnail PNG shown on the card and in the animatic.")]
        public string thumbnailPath;

        public bool IsCard => type == KionjoShotType.Card;

        /// <summary>The in and out range when one is set, else the target length.</summary>
        public float PlannedSeconds() => outPoint > inPoint ? outPoint - inPoint : targetSeconds;
    }

    [Serializable]
    public class KionjoVoiceLine
    {
        [Tooltip("Audio file of the line.")]
        [KionjoPath("wav,mp3,ogg,m4a,flac")] public string audioPath;
        [Tooltip("Subtitle text. Empty = no subtitle.")]
        public string subtitle;
        [Tooltip("Seconds after the shot starts in the cut.")]
        public float at;
        public float trimIn;
        [Tooltip("0 = the end of the file.")]
        public float trimOut;
        public KionjoTrimMode trimMode = KionjoTrimMode.Silence;
        [Tooltip("ASS override tags put in front of the subtitle, each in its own braces, for example {\\an5}{\\fad(500,1100)}.")]
        public string overrideTags;
        public float fadeIn = 0.03f, fadeOut = 0.25f;
        [Tooltip("Words mode: first word index kept.")]
        public int firstWord;
        [Tooltip("Words mode: last word index kept. -1 = the last word.")]
        public int lastWord = -1;
    }

    [Serializable]
    public class KionjoSfxCue
    {
        [KionjoPath("wav,mp3,ogg,m4a,flac")] public string audioPath;
        [Tooltip("Seconds after the shot starts in the cut. Used when Event Name is empty.")]
        public float at;
        [Tooltip("Plays at the moment this recorded event happened in the take (for example a button press).")]
        public string eventName;
        [Range(0f, 2f)] public float volume = 0.9f;
    }

    [Serializable]
    public class KionjoMusicCue
    {
        [KionjoPath("mp3,wav,ogg,m4a,flac")] public string audioPath;
        public float trimIn;
        [Tooltip("0 = the end of the file.")]
        public float trimOut;
        [Tooltip("Absolute time in the cut. Consecutive cues are joined with a crossfade.")]
        public float at;
        public float fadeIn = 1.5f, fadeOut = 2f;
        [Range(0f, 2f)] public float volume = 0.5f;
    }

    [Serializable]
    public class KionjoDucking
    {
        public bool enabled = true;
        public float threshold = 0.05f;
        public float ratio = 3f;
        [Tooltip("Milliseconds.")] public float attack = 40f;
        [Tooltip("Milliseconds.")] public float release = 700f;
    }

    [Serializable]
    public class KionjoLoudness
    {
        [Tooltip("Integrated loudness target in LUFS. -14 suits YouTube and stores.")]
        public float integrated = -14f;
        public float truePeak = -1.5f;
        public float range = 11f;
    }

    [Serializable]
    public class KionjoSubtitleStyle
    {
        [Tooltip("Font file for subtitles. Empty uses Arial.")]
        [KionjoPath("ttf,otf")] public string fontPath;
        [Tooltip("Font family name inside that file.")]
        public string fontName = "Arial";
        [Tooltip("Font file for text cards. Empty uses a sans serif default.")]
        [KionjoPath("ttf,otf")] public string titleFontPath;
        public int size = 15;
        [Tooltip("ASS colour, &HAABBGGRR.")]
        public string colour = "&H00EDEDED";
        public int outline;
        public int marginV = 12;
    }

    [Serializable]
    public class KionjoQualityOverride
    {
        [Tooltip("Apply these settings only while rendering.")]
        public bool enabled;
        [Tooltip("Quality level to switch to while rendering. Empty keeps the current one.")]
        public string qualityLevelName;
        [Tooltip("0 keeps the current LOD bias.")]
        public float lodBias;
        [Tooltip("-1 keeps the current texture mip limit.")]
        public int textureMipLimit = -1;
        public bool anisotropicForce;
        public bool unlimitedSkinWeights;
    }
}
