using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>PlayMode 테스트가 실제로 프레임을 진행하는지 확인한다.</summary>
    public class SetupSmokePlayTests
    {
        [UnityTest]
        public IEnumerator PlayMode_AdvancesFrames()
        {
            int startFrame = Time.frameCount;
            yield return null;
            yield return null;
            Assert.Greater(Time.frameCount, startFrame);
        }
    }
}
