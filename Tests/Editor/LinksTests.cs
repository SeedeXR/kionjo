using System;
using System.IO;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;
using UnityEngine;

namespace SeedeXR.Kionjo.Tests
{
    public class LinksTests
    {
        string _dir, _src;
        Func<string, string, bool> _saved;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name, "a (b)");
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(Path.Combine(_dir, "materials"));
            _src = Path.Combine(_dir, "a b [c].mp4");
            File.WriteAllBytes(_src, new byte[] { 1, 2, 3 });
            _saved = KionjoLinks.TryLink;
        }

        [TearDown]
        public void TearDown()
        {
            KionjoLinks.TryLink = _saved;
            var top = Path.GetDirectoryName(_dir);
            if (Directory.Exists(top)) Directory.Delete(top, true);
        }

        string Dest => Path.Combine(_dir, "materials", "a b [c].mp4");

        [Test]
        public void LinksOnSameVolume()
        {
            Assert.IsTrue(KionjoLinks.LinkOrCopy(_src, Dest, out var note), note);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Dest));
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                using (var f = new FileStream(_src, FileMode.Open, FileAccess.Write)) f.WriteByte(9);
                Assert.AreEqual(9, File.ReadAllBytes(Dest)[0], "a hard link shares the bytes");
            }
        }

        [Test]
        public void DeletingLinkKeepsSource()
        {
            KionjoLinks.LinkOrCopy(_src, Dest, out _);
            File.Delete(Dest);
            Assert.IsTrue(File.Exists(_src));
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(_src));
        }

        [Test]
        public void FallsBackToCopy()
        {
            KionjoLinks.TryLink = (a, b) => false;
            Assert.IsFalse(KionjoLinks.LinkOrCopy(_src, Dest, out var note));
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Dest));
            Assert.IsFalse(string.IsNullOrEmpty(note));
        }

        /// <summary>A refresh must not re-copy an unchanged copy (gigabytes from another drive, on the main thread).</summary>
        [Test]
        public void UnchangedCopyIsNotCopiedAgain()
        {
            KionjoLinks.TryLink = (a, b) => false;
            KionjoLinks.LinkOrCopy(_src, Dest, out _);
            // Held open without delete sharing: a second copy would have to delete it and fail.
            using (new FileStream(Dest, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                KionjoLinks.LinkOrCopy(_src, Dest, out var note);
                Assert.IsFalse(note != null && note.StartsWith("Could not"), note);
            }
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Dest));
        }

        [Test]
        public void OverwritesExisting()
        {
            File.WriteAllBytes(Dest, new byte[] { 7, 7 });
            KionjoLinks.LinkOrCopy(_src, Dest, out _);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Dest));
        }
    }
}
