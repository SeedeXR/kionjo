using System.IO;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;

namespace SeedeXR.Kionjo.Tests
{
    public class ResourcesTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "KionjoTests", TestContext.CurrentContext.Test.Name, "lock (x)");
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [TearDown]
        public void TearDown()
        {
            var top = Path.GetDirectoryName(_dir);
            if (Directory.Exists(top)) Directory.Delete(top, true);
        }

        [Test]
        public void CanRunNeedsTwiceAndFloor()
        {
            Assert.IsFalse(KionjoResources.CanRun(6, 8, (13.3, 20), out var reason));
            StringAssert.Contains("13.3", reason);
            StringAssert.Contains("6.0", reason);
            Assert.IsFalse(KionjoResources.CanRun(6, 1, (11.0, 20), out _), "needs twice the estimate");
            Assert.IsTrue(KionjoResources.CanRun(6, 8, (14.5, 20), out _));
        }

        [Test]
        public void UnknownMemoryRuns()
        {
            Assert.IsTrue(KionjoResources.CanRun(6, 8, null, out var reason));
            StringAssert.Contains("unknown", reason);
        }

        [Test]
        public void FreeIsReadOnWindows()
        {
            var free = KionjoResources.Free();
            if (UnityEngine.Application.platform != UnityEngine.RuntimePlatform.WindowsEditor)
            {
                Assert.IsNull(free);
                return;
            }
            Assert.IsNotNull(free);
            Assert.Greater(free.Value.freeCommitGB, 0);
            Assert.Greater(free.Value.freePhysicalGB, 0);
        }

        [Test]
        public void LockIsExclusive()
        {
            using var first = HeavyLock.TryAcquire(_dir, "capture hero", 3, out _);
            Assert.IsNotNull(first);
            Assert.IsNull(HeavyLock.TryAcquire(_dir, "render", 2, out var holder));
            StringAssert.Contains("capture hero", holder);
            StringAssert.Contains("capture hero", HeavyLock.Describe(_dir));
        }

        [Test]
        public void StaleLockIsReplaced()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "heavy.lock"), "{\"owner\":\"gone\",\"pid\":999999,\"gb\":4,\"since\":\"2026-11-17T10:00:00\"}");
            using var mine = HeavyLock.TryAcquire(_dir, "capture", 3, out _);
            Assert.IsNotNull(mine);
            StringAssert.Contains("capture", HeavyLock.Describe(_dir));
        }

        /// <summary>After a restart the old pid belongs to a newer process: the lock is stale, not held forever.</summary>
        [Test]
        public void ReusedPidIsStale()
        {
            Directory.CreateDirectory(_dir);
            int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            File.WriteAllText(Path.Combine(_dir, "heavy.lock"), "{\"owner\":\"before the restart\",\"pid\":" + pid + ",\"gb\":4,\"since\":\"2000-01-01T00:00:00\"}");
            using var mine = HeavyLock.TryAcquire(_dir, "capture", 3, out var holder);
            Assert.IsNotNull(mine, holder);
        }

        [Test]
        public void DisposeReleases()
        {
            var a = HeavyLock.TryAcquire(_dir, "a", 1, out _);
            a.Dispose();
            Assert.IsFalse(File.Exists(Path.Combine(_dir, "heavy.lock")));
            using var b = HeavyLock.TryAcquire(_dir, "b", 1, out _);
            Assert.IsNotNull(b);
        }
    }
}
