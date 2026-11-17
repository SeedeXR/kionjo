using System.IO;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;
using UnityEngine;

namespace SeedeXR.Kionjo.Tests
{
    public class BrandKitTests
    {
        [Test]
        public void AssColour_Hex_ToBgr()
        {
            Assert.AreEqual("&H00E8D14F", KionjoBrandKit.AssColour("#4FD1E8"));
            Assert.AreEqual("&H00FFFFFF", KionjoBrandKit.AssColour("bad"));
            Assert.AreEqual("0x4FD1E8", KionjoBrandKit.FfmpegColour("#4fd1e8"));
        }

        [Test]
        public void NewKit_DefaultsNeutral()
        {
            var kit = ScriptableObject.CreateInstance<KionjoBrandKit>();
            try
            {
                CollectionAssert.AreEqual(new[] { "Origin", "Villain", "Magic", "Prophecy", "Alchemy" }, kit.storyTypes);
                Assert.AreEqual("", kit.collaboratorHandle);
                var json = JsonUtility.ToJson(kit).ToLowerInvariant();
                StringAssert.DoesNotContain("baraza", json);
                StringAssert.DoesNotContain("seedexr", json);
            }
            finally
            {
                Object.DestroyImmediate(kit);
            }
        }

        [Test]
        public void PostsRoot_UnderOutputFolder()
        {
            Assert.AreEqual(Path.Combine(KionjoPaths.Root("Kionjo"), "posts"), KionjoPaths.PostsRoot("Kionjo"));
            Assert.AreEqual(Path.Combine("D:/out", "posts"), KionjoPaths.PostsRoot("D:/out"));
        }
    }
}
