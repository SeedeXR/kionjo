using System;
using Unity.Collections;

namespace SeedeXR.Kionjo
{
    /// <summary>Receives raw RGBA frames from KionjoFrameCapture (an encoder, a PNG writer, a test).</summary>
    public interface IKionjoFrameSink : IDisposable
    {
        void Begin(int width, int height, float fps, bool flipVertically);
        /// <summary>Copies the frame before returning; may block when the sink is behind.</summary>
        void Push(NativeArray<byte> rgba);
        /// <summary>Flushes and waits until the output is complete.</summary>
        void End();
        /// <summary>Set when the sink failed; capture stops on the next frame.</summary>
        string Error { get; }
    }
}
