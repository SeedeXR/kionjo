namespace SeedeXR.Kionjo
{
    /// <summary>
    /// Put this on any MonoBehaviour to start and stop your own capture (a device recorder, a
    /// replay buffer, a Cinemachine setup) together with a Kionjo take.
    /// </summary>
    public interface IKionjoRecordingHook
    {
        void OnKionjoRecordingStarted(string takeFolder);
        void OnKionjoRecordingStopped(string takeFolder);
    }
}
