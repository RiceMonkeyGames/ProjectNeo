using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// CLI 빌드 진입점.
    /// 사용: unity build --target StandaloneWindows64 --execute-method ProjectNeo.Editor.BuildScript.BuildWindows --output-path Builds/Windows/ProjectNeo.exe
    /// </summary>
    public static class BuildScript
    {
        const string DefaultWindowsOutput = "Builds/Windows/ProjectNeo.exe";

        [MenuItem("ProjectNeo/Build/Windows")]
        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, GetArgument("-buildOutput") ?? DefaultWindowsOutput);
        }

        static void Build(BuildTarget target, string outputPath)
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
                Fail("Build Settings에 활성화된 씬이 없습니다.");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None,
            };

            BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
            if (summary.result != BuildResult.Succeeded)
                Fail($"빌드 실패: {summary.result}, 에러 {summary.totalErrors}개");

            Debug.Log($"빌드 성공: {summary.outputPath} ({summary.totalSize / (1024 * 1024)} MB)");
        }

        // 배치 모드에서는 예외만 던지면 종료 코드가 0으로 끝날 수 있어서 직접 1로 종료한다.
        static void Fail(string message)
        {
            Debug.LogError(message);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            throw new InvalidOperationException(message);
        }

        static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
