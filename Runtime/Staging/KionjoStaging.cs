using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SeedeXR.Kionjo
{
    /// <summary>
    /// Scene side of staging a shot: hierarchy paths resolved against the live scenes (inactive
    /// objects included), hide and show lists applied, stage handlers run in order.
    /// </summary>
    public static class KionjoStaging
    {
        /// <summary>"/Root/Child/Leaf" in any loaded scene, inactive objects included; null when missing.</summary>
        public static GameObject Resolve(string hierarchyPath)
        {
            if (string.IsNullOrWhiteSpace(hierarchyPath)) return null;
            var parts = hierarchyPath.Trim().Trim('/').Split('/');
            if (parts.Length == 0) return null;
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name != parts[0]) continue;
                    var t = root.transform;
                    for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
                    if (t != null) return t.gameObject;
                }
            }
            return null;
        }

        public static string PathOf(Transform t) => KionjoRecorder.PathOf(t);

        /// <summary>Deactivates the hide paths, activates the show paths, returns the paths it could not find.</summary>
        public static List<string> Apply(IEnumerable<string> hidePaths, IEnumerable<string> showPaths)
        {
            var missing = new List<string>();
            if (hidePaths != null)
                foreach (var p in hidePaths)
                {
                    var go = Resolve(p);
                    if (go == null) missing.Add(p);
                    else go.SetActive(false);
                }
            if (showPaths != null)
                foreach (var p in showPaths)
                {
                    var go = Resolve(p);
                    if (go == null) missing.Add(p);
                    else go.SetActive(true);
                }
            return missing;
        }

        /// <summary>Runs every IKionjoStageHandler in the loaded scenes, waiting for each in turn.</summary>
        public static IEnumerator RunHandlers(string shotId, string stageCommand)
        {
            var handlers = new List<IKionjoStageHandler>();
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID))
                if (mb is IKionjoStageHandler h) handlers.Add(h);
            foreach (var h in handlers)
            {
                var routine = h.Stage(shotId, stageCommand);
                if (routine != null) yield return routine;
            }
        }
    }
}
