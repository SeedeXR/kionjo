using System.Collections.Generic;
using UnityEngine;

namespace SeedeXR.Kionjo
{
    /// <summary>
    /// Runs the game at a shot's speed curve while it renders: Time.timeScale follows the curve, and the physics step
    /// shrinks with it so slow motion stays smooth instead of stepping every few frames. 0 freezes the world (bullet
    /// time); a camera on render time (Kionjo Camera Move) keeps moving. Restores time when it ends.
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(32002)] // after the capture has rendered this frame: sets the speed of the next one
    public class KionjoTimeRemap : MonoBehaviour
    {
        public List<KionjoSpeedKey> keys = new();
        public float fps = 60f;

        public bool Running { get; private set; }
        public float Speed { get; private set; } = 1f;

        int _startFrame;
        float _savedScale, _savedFixed;

        public void Begin()
        {
            if (Running) return;
            _savedScale = Time.timeScale;
            _savedFixed = Time.fixedDeltaTime;
            _startFrame = Time.frameCount;
            Running = true;
            ApplyFor(0);
        }

        // From the frame count, not a counter: right even if this LateUpdate did not run in the frame Begin was called.
        void LateUpdate()
        {
            if (Running) ApplyFor(Time.frameCount - _startFrame);
        }

        void ApplyFor(int frame)
        {
            Speed = KionjoSpeed.Evaluate(keys, frame / fps);
            Time.timeScale = Speed;
            Time.fixedDeltaTime = Mathf.Max(0.0001f, _savedFixed * Mathf.Max(Speed, 0.001f));
        }

        public void End()
        {
            if (!Running) return;
            Running = false;
            Time.timeScale = _savedScale;
            Time.fixedDeltaTime = _savedFixed;
            Speed = 1f;
        }

        void OnDisable() => End();
        void OnDestroy() => End();
    }
}
