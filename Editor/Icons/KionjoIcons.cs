using UnityEditor;
using UnityEngine;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>The Kionjo icons. Unity picks the d_ (dark skin) and @2x variants beside each file.</summary>
    public static class KionjoIcons
    {
        public const string Folder = "Packages/com.seedexr.kionjo/Editor/Icons/";
        public const string WindowPath = Folder + "kionjo.png";
        public const string AssetPath = Folder + "kionjo-asset.png";

        public static Texture2D Window => EditorGUIUtility.IconContent(WindowPath).image as Texture2D
                                          ?? AssetDatabase.LoadAssetAtPath<Texture2D>(WindowPath);

        public static Texture2D Asset => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath);
    }

    /// <summary>Imports Kionjo's icon PNGs as editor GUI textures: no mipmaps, no compression, alpha kept.</summary>
    class KionjoIconImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(KionjoIcons.Folder)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.GUI;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
        }
    }
}
