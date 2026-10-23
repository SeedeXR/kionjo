using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// One trailer: an ordered list of shots (cards included), music, subtitle style and output
    /// settings. Lives in Assets; everything heavy it produces lives under its output folder.
    /// </summary>
    [CreateAssetMenu(menuName = "Kionjo/Storyboard", fileName = "Storyboard", order = 0)]
    [Icon("Packages/com.seedexr.kionjo/Editor/Icons/kionjo-asset.png")]
    public class KionjoStoryboard : ScriptableObject
    {
        [Tooltip("Used in output file names, for example Baraza_Trailer_draft03.mp4.")]
        public string title = "Trailer";
        [Tooltip("Where takes, renders and cuts go. Relative to the project folder (outside Assets), or absolute.")]
        public string outputFolder = "Kionjo";
        [Tooltip("Target running time in seconds. The timeline bar warns past it.")]
        public float targetSeconds = 60f;
        [Tooltip("The shots and cards, in trailer order.")]
        public List<KionjoShot> shots = new();
        [Tooltip("Music cues, placed at absolute times in the cut.")]
        public List<KionjoMusicCue> music = new();
        [Tooltip("Crossfade between consecutive music cues, in seconds. 0 joins them end to end.")]
        public float musicCrossfadeSeconds = 2f;
        [Tooltip("Lowers the music under the voice.")]
        public KionjoDucking ducking = new();
        [Tooltip("Final loudness of the cut.")]
        public KionjoLoudness loudness = new();
        [Tooltip("Font, size and colour of subtitles, and the font of text cards.")]
        public KionjoSubtitleStyle subtitles = new();
        [Tooltip("Frame size and rate of each shot's render. Preview while you work, 4K for the final.")]
        public KionjoRenderPreset renderPreset = KionjoRenderPreset.Preview720p30;
        [Tooltip("Frame size and rate of the assembled draft.")]
        public KionjoCutPreset cutPreset = KionjoCutPreset.Preview720p30;
        [Tooltip("Higher quality settings used only while rendering.")]
        public KionjoQualityOverride renderQuality = new();

        public KionjoShot AddShot(KionjoShotType type = KionjoShotType.Performance)
        {
            Undo.RecordObject(this, type == KionjoShotType.Card ? "Add card" : "Add shot");
            var shot = new KionjoShot
            {
                id = NewId(),
                type = type,
                code = NextCode(type == KionjoShotType.Card ? "C" : "S"),
                name = type == KionjoShotType.Card ? "Card" : "New shot",
            };
            if (type == KionjoShotType.Card) shot.targetSeconds = 3f;
            shots.Add(shot);
            EditorUtility.SetDirty(this);
            return shot;
        }

        /// <summary>Deep copy placed right after the original, with a new id and the code suffixed "b".</summary>
        public KionjoShot Duplicate(int index)
        {
            if (index < 0 || index >= shots.Count) return null;
            Undo.RecordObject(this, "Duplicate shot");
            var copy = JsonUtility.FromJson<KionjoShot>(JsonUtility.ToJson(shots[index]));
            copy.id = NewId();
            copy.code = shots[index].code + "b";
            copy.selectedTake = null;
            copy.thumbnailPath = null;
            shots.Insert(index + 1, copy);
            EditorUtility.SetDirty(this);
            return copy;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= shots.Count) return;
            Undo.RecordObject(this, "Remove shot");
            shots.RemoveAt(index);
            EditorUtility.SetDirty(this);
        }

        public void Move(int from, int to)
        {
            if (from < 0 || from >= shots.Count || to < 0 || to >= shots.Count || from == to) return;
            Undo.RecordObject(this, "Move shot");
            var shot = shots[from];
            shots.RemoveAt(from);
            shots.Insert(to, shot);
            EditorUtility.SetDirty(this);
        }

        public float PlannedSeconds()
        {
            float total = 0f;
            foreach (var s in shots) total += s.PlannedSeconds();
            return total;
        }

        public KionjoShot FindShot(string idOrCode)
        {
            foreach (var s in shots)
                if (s.id == idOrCode || s.code == idOrCode) return s;
            return null;
        }

        string NextCode(string prefix)
        {
            int highest = 0;
            foreach (var s in shots)
                if (s.code != null && s.code.StartsWith(prefix) && int.TryParse(s.code.Substring(prefix.Length), out int n) && n > highest)
                    highest = n;
            return $"{prefix}{highest + 1:00}";
        }

        static string NewId() => Guid.NewGuid().ToString("N");
    }
}
