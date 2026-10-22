using NUnit.Framework;

namespace SeedeXR.Kionjo.Tests
{
    public class SmokeTests
    {
        [Test]
        public void Version_IsSemver() => StringAssert.IsMatch(@"^\d+\.\d+\.\d+$", KionjoVersion.Value);
    }
}
