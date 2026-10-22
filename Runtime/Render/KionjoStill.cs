using System.IO;
using UnityEngine;

namespace SeedeXR.Kionjo
{
    /// <summary>One frame from a camera to a PNG. Works in edit mode and in Play mode.</summary>
    public static class KionjoStill
    {
        public static void CapturePng(Camera camera, string path, int width, int height)
        {
            var rt = KionjoRender.NewTarget(width, height);
            var previous = RenderTexture.active;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                rt.Create();
                KionjoRender.RenderTo(camera, rt);
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
            }
        }
    }
}
