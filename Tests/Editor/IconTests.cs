using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;
using UnityEditor;

namespace SeedeXR.Kionjo.Tests
{
    public class IconTests
    {
        [Test]
        public void Icons_LoadFromPackage()
        {
            Assert.IsNotNull(KionjoIcons.Window, "window icon");
            Assert.IsNotNull(KionjoIcons.Asset, "asset icon");
            Assert.AreEqual(64, KionjoIcons.Asset.width);
        }

        [Test]
        public void Icons_ImportAsGuiWithoutMipmaps()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(KionjoIcons.WindowPath);
            Assert.AreEqual(TextureImporterType.GUI, importer.textureType);
            Assert.IsFalse(importer.mipmapEnabled);
        }
    }
}
