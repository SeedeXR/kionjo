using System.IO;
using NUnit.Framework;

namespace SeedeXR.Kionjo.Tests
{
    public class TakePathsTests
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
        public void TakeFolderName_PadsToTwoDigits() =>
            Assert.AreEqual("S1_take03", KionjoTakePaths.TakeFolderName("S1", 3));

        [Test]
        public void NextTakeNumber_MissingFolder_IsOne() =>
            Assert.AreEqual(1, KionjoTakePaths.NextTakeNumber(Path.Combine(_dir, "nope")));

        [Test]
        public void NextTakeNumber_SkipsGaps()
        {
            Directory.CreateDirectory(Path.Combine(_dir, "S1_take01"));
            Directory.CreateDirectory(Path.Combine(_dir, "S1_take05"));
            Directory.CreateDirectory(Path.Combine(_dir, "notes"));
            Assert.AreEqual(6, KionjoTakePaths.NextTakeNumber(_dir));
        }

        [Test]
        public void NewestTakeFolder_PicksHighestNumber()
        {
            Directory.CreateDirectory(Path.Combine(_dir, "S1_take02"));
            Directory.CreateDirectory(Path.Combine(_dir, "S1_take10"));
            Directory.CreateDirectory(Path.Combine(_dir, "S1_take09"));
            Assert.AreEqual("S1_take10", Path.GetFileName(KionjoTakePaths.NewestTakeFolder(_dir)));
        }

        [Test]
        public void Sanitize_ReplacesSpacesAndSlashes() =>
            Assert.AreEqual("S_07_a", KionjoTakePaths.Sanitize("S 07/a"));

        [Test]
        public void Sanitize_Empty_IsShot() => Assert.AreEqual("shot", KionjoTakePaths.Sanitize("  "));
    }
}
