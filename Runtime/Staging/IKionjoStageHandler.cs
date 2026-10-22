using System.Collections;

namespace SeedeXR.Kionjo
{
    /// <summary>
    /// Put this on any MonoBehaviour to set the scene up for a shot: jump the story to a beat, open
    /// a panel, place a creature. Kionjo runs every handler in the loaded scenes, one after another,
    /// and waits for each coroutine before recording or rendering.
    /// </summary>
    public interface IKionjoStageHandler
    {
        IEnumerator Stage(string shotId, string stageCommand);
    }
}
