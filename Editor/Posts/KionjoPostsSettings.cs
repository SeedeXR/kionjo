using System.IO;
using UnityEditor;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>Project settings for Posts, independent of any storyboard: output folder and active brand kit.</summary>
    [FilePath("ProjectSettings/KionjoPosts.asset", FilePathAttribute.Location.ProjectFolder)]
    public class KionjoPostsSettings : ScriptableSingleton<KionjoPostsSettings>
    {
        public string outputFolder = "Kionjo";
        public string activeKitGuid = "";

        public KionjoBrandKit ActiveKit
        {
            get => string.IsNullOrEmpty(activeKitGuid) ? null : AssetDatabase.LoadAssetAtPath<KionjoBrandKit>(AssetDatabase.GUIDToAssetPath(activeKitGuid));
            set => activeKitGuid = value == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
        }

        public void SaveNow() => Save(true);
    }

    public static partial class KionjoPaths
    {
        public static string PostsRoot(string outputFolder) => Path.Combine(Root(outputFolder), "posts");
    }
}
