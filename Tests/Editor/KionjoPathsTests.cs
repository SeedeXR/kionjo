using System.IO;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;

namespace SeedeXR.Kionjo.Tests
{
    public class KionjoPathsTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void NextNumbered_StartsAt01AndIncrements()
        {
            Assert.AreEqual("draft01", Path.GetFileName(KionjoPaths.NextNumbered(_dir, "draft")));
            Directory.CreateDirectory(Path.Combine(_dir, "draft01"));
            Directory.CreateDirectory(Path.Combine(_dir, "draft07"));
            Assert.AreEqual("draft08", Path.GetFileName(KionjoPaths.NextNumbered(_dir, "draft")));
        }

        [Test]
        public void Root_AbsoluteFolder_UsedAsIs() => Assert.AreEqual(_dir, KionjoPaths.Root(_dir));

        [Test]
        public void Root_RelativeFolder_UnderProject() =>
            Assert.AreEqual(Path.Combine(KionjoPaths.ProjectRoot, "Kionjo"), KionjoPaths.Root("Kionjo"));
    }
}
