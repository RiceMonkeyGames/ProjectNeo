using System;
using UnityEngine.InputSystem;

namespace ProjectNeo.Tests
{
    /// <summary>합성 입력을 Game View 포커스와 무관하게 처리하고 종료 시 원래 설정을 복원한다.</summary>
    internal sealed class FocusIndependentInputScope : IDisposable
    {
        readonly InputSettings settings;
        readonly InputSettings.BackgroundBehavior background;
#if UNITY_EDITOR
        readonly InputSettings.EditorInputBehaviorInPlayMode editorInput;
#endif
        bool disposed;

        public FocusIndependentInputScope()
        {
            settings = InputSystem.settings;
            background = settings.backgroundBehavior;
#if UNITY_EDITOR
            editorInput = settings.editorInputBehaviorInPlayMode;
#endif
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            settings.backgroundBehavior = background;
#if UNITY_EDITOR
            settings.editorInputBehaviorInPlayMode = editorInput;
#endif
        }
    }
}
