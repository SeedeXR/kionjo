using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// A storyboard as JSON, for agents and scripts: export, edit or generate, import. Field names
    /// are the C# field names of KionjoStoryboard and KionjoShot (see Documentation~/AgentGuide.md).
    /// </summary>
    public static class KionjoStoryboardJson
    {
        public static string Export(KionjoStoryboard sb) => EditorJsonUtility.ToJson(sb, true);

        /// <summary>
        /// replace: the JSON's shots (and settings) replace the storyboard's. Otherwise its shots are
        /// appended. Shots without an id get one. Undoable.
        /// </summary>
        public static int Import(KionjoStoryboard sb, string json, bool replace)
        {
            var incoming = ScriptableObject.CreateInstance<KionjoStoryboard>();
            try
            {
                incoming.shots.Clear();
                EditorJsonUtility.FromJsonOverwrite(json, incoming);
                Undo.RecordObject(sb, "Import storyboard");
                foreach (var s in incoming.shots)
                    if (string.IsNullOrEmpty(s.id) || (!replace && sb.shots.Exists(e => e.id == s.id)))
                        s.id = Guid.NewGuid().ToString("N"); // appended copies get their own thumbnails and takes
                if (replace)
                {
                    var keepOutput = sb.outputFolder;
                    EditorJsonUtility.FromJsonOverwrite(json, sb);
                    sb.shots = incoming.shots;
                    if (string.IsNullOrEmpty(sb.outputFolder)) sb.outputFolder = keepOutput;
                }
                else sb.shots.AddRange(incoming.shots);
                EditorUtility.SetDirty(sb);
                return incoming.shots.Count;
            }
            finally
            {
                Object.DestroyImmediate(incoming);
            }
        }
    }
}
