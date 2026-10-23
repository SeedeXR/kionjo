using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Links into the documentation site, and the section header with its "?" button. The anchors
    /// here are a contract with the site's reference page: rename one on both sides.
    /// </summary>
    public static class KionjoHelp
    {
        public const string Site = "https://seedexr.github.io/kionjo-docs/";
        public const string Guide = Site + "guide.html";
        public const string Troubleshooting = Site + "troubleshooting.html";

        public static string Reference(string anchor) => Site + "reference.html" + (string.IsNullOrEmpty(anchor) ? "" : "#" + anchor);

        public static string TabUrl(KionjoWindow.Tab tab) => Reference(tab switch
        {
            KionjoWindow.Tab.Shoot => "shoot-tab",
            KionjoWindow.Tab.Cut => "cut-tab",
            _ => "storyboard-tab",
        });

        /// <summary>Inspector section titles to reference page anchors.</summary>
        public static readonly IReadOnlyDictionary<string, string> SectionAnchors = new Dictionary<string, string>
        {
            ["Shot"] = "shot",
            ["Card"] = "card",
            ["Source"] = "source",
            ["Timing"] = "timing",
            ["Staging"] = "staging",
            ["Voice and subtitles"] = "voice",
            ["Sound effects"] = "sfx",
            ["Notes"] = "notes",
            ["Picture"] = "picture",
            ["Storyboard"] = "storyboard-settings",
        };

        /// <summary>A section title with a small "?" that opens its part of the reference page.</summary>
        public static VisualElement Section(string title)
        {
            var row = new VisualElement();
            row.AddToClassList("kionjo-section");
            row.AddToClassList("kionjo-section--help");
            row.Add(new Label(title));
            if (SectionAnchors.TryGetValue(title, out var anchor))
            {
                var url = Reference(anchor);
                var help = new Button(() => Application.OpenURL(url)) { name = $"help-{anchor}", text = "?", tooltip = $"What \"{title}\" does, in the Kionjo docs." };
                help.AddToClassList("kionjo-help-button");
                row.Add(help);
            }
            return row;
        }
    }
}
