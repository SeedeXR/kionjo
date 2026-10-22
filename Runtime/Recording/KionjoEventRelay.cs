using UnityEngine;

namespace SeedeXR.Kionjo
{
    /// <summary>
    /// A UnityEvent target that marks a named Kionjo event, so a Button's OnClick (or any UnityEvent)
    /// can be recorded without code.
    /// </summary>
    [AddComponentMenu("SeedeXR/Kionjo Event Relay")]
    public class KionjoEventRelay : MonoBehaviour
    {
        [Tooltip("Name written to the take when this relay fires.")]
        public string eventName = "event";

        public void Mark() => KionjoEvents.Mark(eventName);

        public void Mark(string payload) => KionjoEvents.Mark(eventName, payload);
    }
}
