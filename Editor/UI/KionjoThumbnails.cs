using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>Thumbnail PNGs loaded from disk (never imported as assets), cached by path and write time.</summary>
    public static class KionjoThumbnails
    {
        static readonly Dictionary<string, (DateTime written, Texture2D tex)> Cache = new();

        public static Texture2D Get(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var written = File.GetLastWriteTimeUtc(path);
            if (Cache.TryGetValue(path, out var hit) && hit.written == written && hit.tex != null) return hit.tex;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, name = Path.GetFileName(path) };
            if (!tex.LoadImage(File.ReadAllBytes(path)))
            {
                Object.DestroyImmediate(tex);
                return null;
            }
            if (hit.tex != null) Object.DestroyImmediate(hit.tex);
            Cache[path] = (written, tex);
            return tex;
        }

        public static void Forget(string path)
        {
            if (path != null && Cache.TryGetValue(path, out var hit))
            {
                if (hit.tex != null) Object.DestroyImmediate(hit.tex);
                Cache.Remove(path);
            }
        }
    }
}
