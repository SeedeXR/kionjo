using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// The look and words of one owner (a person or a studio): logos, fonts, colours, caption lines,
    /// story types. Ships with neutral defaults; a studio's own kit lives in its own project.
    /// </summary>
    [CreateAssetMenu(menuName = "Kionjo/Brand Kit", fileName = "BrandKit", order = 1)]
    public class KionjoBrandKit : ScriptableObject
    {
        public string kitName = "My Studio";
        [Tooltip("Instagram handle of a studio to invite as collaborator, for example @studio. Empty for personal posts.")]
        public string collaboratorHandle = "";
        [Tooltip("PNG logo for dark backgrounds (ffmpeg here cannot read SVG).")]
        public string logoLightPath = "";
        [Tooltip("PNG logo for light backgrounds.")]
        public string logoDarkPath = "";
        public string titleFontPath = "";
        public string titleFontName = "Arial";
        public string bodyFontPath = "";
        public string bodyFontName = "Arial";
        public string accentColour = "#4FD1E8";
        public string textColour = "#FFFFFF";
        [Tooltip("One fixed sentence in every caption, for example \"<Project> is <what it is>, made by <studio>.\"")]
        public string identityLine = "";
        public string callToAction = "Send this to someone who has never tried VR.";
        public List<string> hashtags = new();
        public string seriesPrefix = "";
        public bool coverDesaturate = true;
        public List<string> storyTypes = new() { "Origin", "Villain", "Magic", "Prophecy", "Alchemy" };
        [TextArea(3, 8)] public string captionRules = "";
        public List<string> jargon = new() { "shader", "LOD", "URP", "FPS", "build", "mesh", "prefab", "render pipeline", "draw call", "normal map" };

        /// <summary>"#RRGGBB" in ASS order "&amp;H00BBGGRR"; anything else is white.</summary>
        public static string AssColour(string hex)
        {
            if (!TryRgb(hex, out var rgb)) return "&H00FFFFFF";
            return $"&H00{rgb.Substring(4, 2)}{rgb.Substring(2, 2)}{rgb.Substring(0, 2)}";
        }

        /// <summary>"#RRGGBB" as ffmpeg's "0xRRGGBB"; anything else is white.</summary>
        public static string FfmpegColour(string hex) => TryRgb(hex, out var rgb) ? "0x" + rgb : "0xFFFFFF";

        static bool TryRgb(string hex, out string rgb)
        {
            rgb = (hex ?? "").Trim().TrimStart('#').ToUpperInvariant();
            return rgb.Length == 6 && int.TryParse(rgb, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
        }
    }
}
