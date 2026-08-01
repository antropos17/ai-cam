#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using TestMode = UnityEditor.TestTools.TestRunner.Api.TestMode;

namespace BugCam.Editor
{
    [InitializeOnLoad]
    internal static class BugCamTestAutomation
    {
        private static readonly string ProjectRoot = Path.GetFullPath(
            Path.Combine(Application.dataPath, ".."));
        private static readonly string RequestPath = Path.Combine(ProjectRoot, "Library/BugCamTest.request");
        private static readonly string PreviewRequestPath = Path.Combine(
            ProjectRoot,
            "Library/BugCamPreview.request");
        private static readonly string RecoveryRequestPath = Path.Combine(
            ProjectRoot,
            "Library/BugCamTestRecovery.request");
        private static readonly string PlayModePendingPath = Path.Combine(
            ProjectRoot,
            "Library/BugCamTest.playmode.pending");
        private static readonly string EditModeResultPath = Path.Combine(
            ProjectRoot,
            "Library/BugCamTestResults.EditMode.xml");
        private static readonly string PlayModeResultPath = Path.Combine(
            ProjectRoot,
            "Library/BugCamTestResults.PlayMode.xml");
        private static readonly string CombinedResultPath = Path.Combine(
            ProjectRoot,
            "Library/BugCamTestResults.xml");

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
                if (File.Exists(PlayModePendingPath))
                {
                    File.Delete(PlayModePendingPath);
                }

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

            if (isRunning || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            // PlayMode is started from a pending marker so domain reload during the
            // PlayMode run cannot drop an in-memory "chain next suite" flag.
            if (File.Exists(PlayModePendingPath))
            {
                File.Delete(PlayModePendingPath);
                isRunning = true;
                ExecuteSuite(TestMode.PlayMode, PlayModeResultPath, alsoCopyToCombined: true);
                return;
            }

            if (!File.Exists(RequestPath))
            {
                return;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            var requestKind = ReadRequestKind(RequestPath);
            File.Delete(RequestPath);
            isRunning = true;

            if (requestKind == "playmode")
            {
                ExecuteSuite(TestMode.PlayMode, PlayModeResultPath, alsoCopyToCombined: true);
                return;
            }

            var chainPlayMode = requestKind == "all";
            ExecuteSuite(
                TestMode.EditMode,
                EditModeResultPath,
                alsoCopyToCombined: !chainPlayMode,
                chainPlayModeAfter: chainPlayMode);
        }

        private static string ReadRequestKind(string path)
        {
            try
            {
                var text = File.ReadAllText(path).Trim().ToLowerInvariant();
                if (text == "editmode" || text == "playmode" || text == "all")
                {
                    return text;
                }
            }
            catch (IOException)
            {
            }

            return "all";
        }

        private static void ExecuteSuite(
            TestMode mode,
            string resultPath,
            bool alsoCopyToCombined,
            bool chainPlayModeAfter = false)
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var callbacks = new ResultCallbacks(
                api,
                resultPath,
                alsoCopyToCombined,
                chainPlayModeAfter);
            api.RegisterCallbacks(callbacks);

            var assemblyNames = mode == TestMode.EditMode
                ? new[] { "BugCam.Tests" }
                : new[] { "BugCam.Tests.PlayMode" };

            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = mode,
                assemblyNames = assemblyNames
            }));
        }

        private sealed class ResultCallbacks : ICallbacks
        {
            private readonly TestRunnerApi api;
            private readonly string resultPath;
            private readonly bool alsoCopyToCombined;
            private readonly bool chainPlayModeAfter;

            public ResultCallbacks(
                TestRunnerApi api,
                string resultPath,
                bool alsoCopyToCombined,
                bool chainPlayModeAfter)
            {
                this.api = api;
                this.resultPath = resultPath;
                this.alsoCopyToCombined = alsoCopyToCombined;
                this.chainPlayModeAfter = chainPlayModeAfter;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result, resultPath);
                if (alsoCopyToCombined || !chainPlayModeAfter)
                {
                    File.Copy(resultPath, CombinedResultPath, overwrite: true);
                }

                api.UnregisterCallbacks(this);
                UnityEngine.Object.DestroyImmediate(api);
                isRunning = false;

                if (chainPlayModeAfter)
                {
                    File.WriteAllText(PlayModePendingPath, "playmode");
                }
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
