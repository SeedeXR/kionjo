using UnityEngine;
using UnityEngine.Rendering;

namespace SeedeXR.Kionjo
{
    /// <summary>
    /// Renders a camera into a RenderTexture on demand. On a scriptable pipeline (URP, HDRP) it
    /// submits a StandardRequest, which renders the whole camera stack with post processing and
    /// returns when the texture is filled; on the built in pipeline it uses Camera.Render.
    /// Never waits for the end of the frame, which does not happen in batchmode.
    /// </summary>
    public static class KionjoRender
    {
        public static void RenderTo(Camera camera, RenderTexture target)
        {
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                {
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    return;
                }
            }
            var previous = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = previous;
        }

        public static RenderTexture NewTarget(int width, int height) =>
            new(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Kionjo Capture", antiAliasing = 1 };
    }
}
