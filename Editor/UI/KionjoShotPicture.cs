using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Where a shot's picture comes from: the Scene view (the default, taken when a shot is added,
    /// because planning happens with the place framed in the Scene view), the game camera, or an
    /// image file. A render later replaces it with its middle frame (RenderJob).
    /// </summary>
    public static class KionjoShotPicture
    {
        public const int Width = 480, Height = 270;

        /// <summary>The Scene view camera's view, through a temporary game camera so the pipeline renders it like any other.</summary>
        public static bool TryFromSceneView(KionjoStoryboard sb, KionjoShot shot, out string message)
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null || view.camera == null)
            {
                message = "No Scene view open: open one (Window > General > Scene) and frame the shot.";
                return false;
            }
            var go = new GameObject("Kionjo Scene View Still") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var src = view.camera;
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
                cam.orthographic = src.orthographic;
                cam.orthographicSize = src.orthographicSize;
                cam.fieldOfView = src.fieldOfView;
                cam.nearClipPlane = src.nearClipPlane;
                cam.farClipPlane = src.farClipPlane;
                cam.cullingMask = Tools.visibleLayers;
                return Save(sb, shot, path => KionjoStill.CapturePng(cam, path, Width, Height), "the Scene view", out message);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>The Kionjo Camera Source, else the main camera.</summary>
        public static bool TryFromCamera(KionjoStoryboard sb, KionjoShot shot, out string message)
        {
            var cam = KionjoCameraSource.Resolve();
            if (cam == null)
            {
                message = "No camera: add a Kionjo Camera Source or tag a camera MainCamera.";
                return false;
            }
            return Save(sb, shot, path => KionjoStill.CapturePng(cam, path, Width, Height), cam.name, out message);
        }

        /// <summary>A PNG or JPG file (or a texture asset's file), stored as the shot's PNG.</summary>
        public static bool TryFromFile(KionjoStoryboard sb, KionjoShot shot, string file, out string message)
        {
            if (!IsImage(file) || !File.Exists(file))
            {
                message = "Drop a PNG or JPG file.";
                return false;
            }
            return Save(sb, shot, path =>
            {
                var tex = new Texture2D(2, 2);
                try
                {
                    if (!tex.LoadImage(File.ReadAllBytes(file))) throw new InvalidDataException($"{Path.GetFileName(file)} is not a readable image.");
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                }
                finally
                {
                    Object.DestroyImmediate(tex);
                }
            }, Path.GetFileName(file), out message);
        }

        public static bool IsImage(string file)
        {
            var ext = Path.GetExtension(file ?? "").ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg";
        }

        static bool Save(KionjoStoryboard sb, KionjoShot shot, Action<string> write, string from, out string message)
        {
            var path = KionjoPaths.ThumbnailPath(sb, shot);
            try
            {
                write(path);
            }
            catch (Exception e)
            {
                message = $"Picture not saved: {e.Message}";
                return false;
            }
            KionjoThumbnails.Forget(path);
            Undo.RecordObject(sb, "Shot picture");
            shot.thumbnailPath = path;
            EditorUtility.SetDirty(sb);
            message = $"Picture taken from {from}.";
            return true;
        }
    }
}
