using System;

namespace SeedeXR.Kionjo
{
    /// <summary>
    /// Game code marks moments worth recording ("seal", "speak", "button:Confirm"). While a take is
    /// recording, every mark lands in take.json with its time; on replay the replayer raises it again.
    /// Marking costs nothing when nobody listens.
    /// </summary>
    public static class KionjoEvents
    {
        public static event Action<string, string> Marked;

        public static void Mark(string name, string payload = null) => Marked?.Invoke(name, payload);
    }
}
