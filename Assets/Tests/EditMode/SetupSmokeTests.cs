using NUnit.Framework;
using ProjectNeo.Core;

namespace ProjectNeo.Tests
{
    /// <summary>테스트 어셈블리가 런타임 어셈블리를 참조해 실행되는지 확인한다.</summary>
    public class SetupSmokeTests
    {
        [Test]
        public void RuntimeAssembly_IsReferencedFromEditModeTests()
        {
            Assert.AreEqual("ProjectNeo", ProjectInfo.Name);
        }
    }
}
