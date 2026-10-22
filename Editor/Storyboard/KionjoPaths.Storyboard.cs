using System.IO;

namespace SeedeXR.Kionjo.EditorTools
{
    public static partial class KionjoPaths
    {
        public static string Root(KionjoStoryboard sb) => Root(sb.outputFolder);
        public static string ShotTakes(KionjoStoryboard sb, KionjoShot shot) => ShotTakes(Root(sb), shot.code);
        public static string ShotRenders(KionjoStoryboard sb, KionjoShot shot) => ShotRenders(Root(sb), shot.code);
        public static string ShotCaptures(KionjoStoryboard sb, KionjoShot shot) => ShotCaptures(Root(sb), shot.code);
        public static string Thumbs(KionjoStoryboard sb) => ThumbsFolder(Root(sb));
        public static string Cuts(KionjoStoryboard sb) => CutsFolder(Root(sb));
        public static string Animatics(KionjoStoryboard sb) => AnimaticsFolder(Root(sb));
        public static string ThumbnailPath(KionjoStoryboard sb, KionjoShot shot) => Path.Combine(Thumbs(sb), shot.id + ".png");

        /// <summary>The selected take's folder, or the newest when none is selected; null when there is no take.</summary>
        public static string SelectedTakeFolder(KionjoStoryboard sb, KionjoShot shot)
        {
            var takes = ShotTakes(sb, shot);
            if (!string.IsNullOrEmpty(shot.selectedTake))
            {
                var chosen = Path.Combine(takes, shot.selectedTake);
                if (Directory.Exists(chosen)) return chosen;
            }
            return KionjoTakePaths.NewestTakeFolder(takes);
        }

        /// <summary>Render file for a take at a preset: renders/S01/S01_take03_Preview720p30.mp4.</summary>
        public static string RenderPath(KionjoStoryboard sb, KionjoShot shot, string takeFolderName, KionjoRenderPreset preset) =>
            Path.Combine(ShotRenders(sb, shot), $"{takeFolderName}_{preset}.mp4");

        /// <summary>The best existing render for the shot's selected take: the highest preset that exists, else null.</summary>
        public static string ExistingRender(KionjoStoryboard sb, KionjoShot shot)
        {
            var take = SelectedTakeFolder(sb, shot);
            var name = take != null ? Path.GetFileName(take) : null;
            if (shot.type == KionjoShotType.LiveCamera || shot.type == KionjoShotType.Still) name = shot.code;
            if (name == null) return null;
            foreach (var preset in new[] { KionjoRenderPreset.UHD2160p60, KionjoRenderPreset.HD1080p60, KionjoRenderPreset.Preview720p30 })
            {
                var path = RenderPath(sb, shot, name, preset);
                if (File.Exists(path)) return path;
            }
            return null;
        }
    }
}
