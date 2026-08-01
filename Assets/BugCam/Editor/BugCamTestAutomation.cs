#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace BugCam.Editor
{
    [InitializeOnLoad]
    internal static class BugCamTestAutomation
    {
        private const string ResultPath = "Library/BugCamTestResults.xml";
        private static readonly string RequestPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Library/BugCamTest.request"));
        private static readonly string PreviewRequestPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Library/BugCamPreview.request"));
        private static readonly string RecoveryRequestPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Library/BugCamTestRecovery.request"));
        private static bool isRunning;

        static BugCamTestAutomation()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (File.Exists(RecoveryRequestPath))
            {
                File.Delete(RecoveryRequestPath);
                isRunning = false;
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.ExitPlaymode();
                }

                return;
            }

            if (!EditorApplication.isCompiling && File.Exists(PreviewRequestPath))
            {
                File.Delete(PreviewRequestPath);
                TowerScenePreviewExporter.Export();
                return;
            }

            if (isRunning || !File.Exists(RequestPath))
            {
                return;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            isRunning = true;
            File.Delete(RequestPath);

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var callbacks = new ResultCallbacks(api);
            api.RegisterCallbacks(callbacks);
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode,
                assemblyNames = new[] { "BugCam.Tests" }
            }));
        }

        private sealed class ResultCallbacks : ICallbacks
        {
            private readonly TestRunnerApi api;

            public ResultCallbacks(TestRunnerApi api)
            {
                this.api = api;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result, ResultPath);
                api.UnregisterCallbacks(this);
                UnityEngine.Object.DestroyImmediate(api);
                isRunning = false;
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }
        }
    }
}
#endif
