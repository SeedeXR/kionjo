using UnityEditor;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Raises quality only while rendering (a quality level, LOD bias, texture mip limit, anisotropic
    /// filtering, skin weights) and puts everything back afterwards. The snapshot lives in
    /// SessionState so a domain reload or a crash mid render cannot strand the project on render
    /// settings: the next editor start restores them.
    /// </summary>
    public static class QualityOverride
    {
        const string Key = "SeedeXR.Kionjo.Quality.";

        public static bool IsApplied => SessionState.GetBool(Key + "applied", false);

        public static void Apply(KionjoQualityOverride q)
        {
            if (q == null || !q.enabled) return;
            if (!IsApplied)
            {
                SessionState.SetInt(Key + "level", QualitySettings.GetQualityLevel());
                SessionState.SetFloat(Key + "lodBias", QualitySettings.lodBias);
                SessionState.SetInt(Key + "mip", QualitySettings.globalTextureMipmapLimit);
                SessionState.SetInt(Key + "aniso", (int)QualitySettings.anisotropicFiltering);
                SessionState.SetInt(Key + "skin", (int)QualitySettings.skinWeights);
                SessionState.SetBool(Key + "applied", true);
            }
            if (!string.IsNullOrEmpty(q.qualityLevelName))
            {
                int index = System.Array.IndexOf(QualitySettings.names, q.qualityLevelName);
                if (index >= 0) QualitySettings.SetQualityLevel(index, true);
                else Debug.LogWarning($"Kionjo: no quality level named \"{q.qualityLevelName}\".");
            }
            if (q.lodBias > 0f) QualitySettings.lodBias = q.lodBias;
            if (q.textureMipLimit >= 0) QualitySettings.globalTextureMipmapLimit = q.textureMipLimit;
            if (q.anisotropicForce) QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            if (q.unlimitedSkinWeights) QualitySettings.skinWeights = SkinWeights.Unlimited;
        }

        /// <summary>Puts the snapshot back. Safe to call any number of times.</summary>
        public static void Restore()
        {
            if (!IsApplied) return;
            QualitySettings.SetQualityLevel(SessionState.GetInt(Key + "level", QualitySettings.GetQualityLevel()), true);
            QualitySettings.lodBias = SessionState.GetFloat(Key + "lodBias", QualitySettings.lodBias);
            QualitySettings.globalTextureMipmapLimit = SessionState.GetInt(Key + "mip", QualitySettings.globalTextureMipmapLimit);
            QualitySettings.anisotropicFiltering = (AnisotropicFiltering)SessionState.GetInt(Key + "aniso", (int)QualitySettings.anisotropicFiltering);
            QualitySettings.skinWeights = (SkinWeights)SessionState.GetInt(Key + "skin", (int)QualitySettings.skinWeights);
            SessionState.SetBool(Key + "applied", false);
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.ExitingPlayMode) Restore();
            };
            // A snapshot left over from a reload outside a render: restore it.
            EditorApplication.delayCall += () =>
            {
                if (IsApplied && !KionjoJobs.IsBusy) Restore();
            };
        }
    }
}
