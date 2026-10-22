using System.Collections;
using UnityEngine;

namespace SeedeXR.Kionjo
{
    /// <summary>A hidden object that runs Kionjo's coroutines (staging) in Play mode.</summary>
    [AddComponentMenu("")]
    public class KionjoRunner : MonoBehaviour
    {
        static KionjoRunner _instance;

        public static Coroutine Run(IEnumerator routine)
        {
            if (_instance == null)
            {
                var go = new GameObject("Kionjo Runner") { hideFlags = HideFlags.HideAndDontSave };
                _instance = go.AddComponent<KionjoRunner>();
            }
            return _instance.StartCoroutine(routine);
        }
    }
}
