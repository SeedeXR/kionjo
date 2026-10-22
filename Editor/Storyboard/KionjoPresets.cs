namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>Frame size, rate and x264 settings per preset.</summary>
    public static class KionjoPresets
    {
        public readonly struct Video
        {
            public readonly int Width, Height, Fps;
            public readonly string[] EncoderArgs;
            public readonly string Name;

            public Video(string name, int width, int height, int fps, params string[] encoderArgs)
            {
                Name = name;
                Width = width;
                Height = height;
                Fps = fps;
                EncoderArgs = encoderArgs;
            }
        }

        static readonly Video Preview = new("Preview720p30", 1280, 720, 30, "-c:v", "libx264", "-crf", "23", "-preset", "veryfast");
        static readonly Video Hd60 = new("HD1080p60", 1920, 1080, 60, "-c:v", "libx264", "-crf", "18", "-preset", "medium");
        static readonly Video Hd30 = new("HD1080p30", 1920, 1080, 30, "-c:v", "libx264", "-crf", "18", "-preset", "medium");
        static readonly Video Uhd60 = new("UHD2160p60", 3840, 2160, 60, "-c:v", "libx264", "-crf", "16", "-preset", "medium");

        public static Video Get(KionjoRenderPreset p) => p switch
        {
            KionjoRenderPreset.HD1080p60 => Hd60,
            KionjoRenderPreset.UHD2160p60 => Uhd60,
            _ => Preview,
        };

        public static Video Get(KionjoCutPreset p) => p switch
        {
            KionjoCutPreset.HD1080p30 => Hd30,
            KionjoCutPreset.UHD2160p60 => Uhd60,
            _ => Preview,
        };
    }
}
