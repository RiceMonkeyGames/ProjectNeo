using System;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace ProjectNeo.Tests
{
    public class FocusIndependentInputScopeTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void Dispose_RestoresOriginalSettings_EvenWhenTestThrows(bool throwInsideScope)
        {
            InputSettings original = InputSystem.settings;
            var background = original.backgroundBehavior;
#if UNITY_EDITOR
            var editorInput = original.editorInputBehaviorInPlayMode;
#endif

            try
            {
                using var scope = new FocusIndependentInputScope();
                InputSettings temporary = InputSystem.settings;
                Assert.AreSame(original, temporary);
                Assert.AreEqual(InputSettings.BackgroundBehavior.IgnoreFocus, temporary.backgroundBehavior);
#if UNITY_EDITOR
                Assert.AreEqual(InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView,
                    temporary.editorInputBehaviorInPlayMode);
#endif

                if (throwInsideScope) throw new InvalidOperationException("Simulated test failure");
            }
            catch (InvalidOperationException) when (throwInsideScope) { }

            Assert.AreSame(original, InputSystem.settings);
            Assert.AreEqual(background, original.backgroundBehavior);
#if UNITY_EDITOR
            Assert.AreEqual(editorInput, original.editorInputBehaviorInPlayMode);
#endif

        }
    }
}
