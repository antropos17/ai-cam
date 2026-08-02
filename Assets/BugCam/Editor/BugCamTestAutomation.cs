#if UNITY_EDITOR
using System;
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
        // LatestResultPath holds only the most recently completed suite (EditMode or
        // PlayMode). It is not a merged combined report of both suites.
        private static readonly string LatestResultPath = Path.Combine(
            ProjectRoot,
            "Library/BugCamTestResults.xml");

        private static bool isRunning;
        private static double suiteStartedAt = -1d;

        static BugCamTestAutomation()
        {
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            ClearStalePendingMarkerIfNeeded();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode &&
                isRunning &&
                !File.Exists(PlayModePendingPath) &&
                suiteStartedAt > 0d &&
                EditorApplication.timeSinceStartup - suiteStartedAt > 120d)
            {
                // PlayMode run ended without RunFinished (domain reload / abort).
                isRunning = false;
                suiteStartedAt = -1d;
            }
        }

        private static void ClearStalePendingMarkerIfNeeded()
        {
            if (!File.Exists(PlayModePendingPath))
            {
                return;
            }

            try
            {
                var pendingAge = DateTime.UtcNow - File.GetLastWriteTimeUtc(PlayModePendingPath);
                // A pending marker older than 30 minutes is treated as leftover from a
                // crashed or abandoned Editor session, not an in-flight chain.
                if (pendingAge.TotalMinutes > 30d)
                {
                    File.Delete(PlayModePendingPath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void Poll()
        {
            if (File.Exists(RecoveryRequestPath))
            {
                try
                {
                    File.Delete(RecoveryRequestPath);
                }
                catch (IOException)
                {
                }

                isRunning = false;
                suiteStartedAt = -1d;
                if (File.Exists(PlayModePendingPath))
                {
                    try
                    {
                        File.Delete(PlayModePendingPath);
                    }
                    catch (IOException)
                    {
                    }
                }

                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.ExitPlaymode();
                }

                return;
            }

            if (!EditorApplication.isCompiling && File.Exists(PreviewRequestPath))
            {
                try
                {
                    File.Delete(PreviewRequestPath);
                }
                catch (IOException)
                {
                    return;
                }

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
                try
                {
                    File.Delete(PlayModePendingPath);
                }
                catch (IOException)
                {
                    return;
                }

                isRunning = true;
                suiteStartedAt = EditorApplication.timeSinceStartup;
                ExecuteSuite(TestMode.PlayMode, PlayModeResultPath, alsoCopyToLatest: true);
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
            try
            {
                File.Delete(RequestPath);
            }
            catch (IOException)
            {
                return;
            }

            isRunning = true;
            suiteStartedAt = EditorApplication.timeSinceStartup;

            if (requestKind == "playmode")
            {
                ExecuteSuite(TestMode.PlayMode, PlayModeResultPath, alsoCopyToLatest: true);
                return;
            }

            var chainPlayMode = requestKind == "all";
            ExecuteSuite(
                TestMode.EditMode,
                EditModeResultPath,
                alsoCopyToLatest: !chainPlayMode,
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
            bool alsoCopyToLatest,
            bool chainPlayModeAfter = false)
        {
            try
            {
                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                var callbacks = new ResultCallbacks(
                    api,
                    resultPath,
                    alsoCopyToLatest,
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
            catch (Exception)
            {
                isRunning = false;
                suiteStartedAt = -1d;
                if (File.Exists(PlayModePendingPath))
                {
                    try
                    {
                        File.Delete(PlayModePendingPath);
                    }
                    catch (IOException)
                    {
                    }
                }

                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.ExitPlaymode();
                }

                throw;
            }
        }

        private sealed class ResultCallbacks : ICallbacks
        {
            private readonly TestRunnerApi api;
            private readonly string resultPath;
            private readonly bool alsoCopyToLatest;
            private readonly bool chainPlayModeAfter;

            public ResultCallbacks(
                TestRunnerApi api,
                string resultPath,
                bool alsoCopyToLatest,
                bool chainPlayModeAfter)
            {
                this.api = api;
                this.resultPath = resultPath;
                this.alsoCopyToLatest = alsoCopyToLatest;
                this.chainPlayModeAfter = chainPlayModeAfter;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                try
                {
                    TestRunnerApi.SaveResultToFile(result, resultPath);
                    if (alsoCopyToLatest || !chainPlayModeAfter)
                    {
                        File.Copy(resultPath, LatestResultPath, overwrite: true);
                    }

                    if (chainPlayModeAfter)
                    {
                        File.WriteAllText(PlayModePendingPath, "playmode");
                    }
                }
                finally
                {
                    api.UnregisterCallbacks(this);
                    UnityEngine.Object.DestroyImmediate(api);
                    isRunning = false;
                    suiteStartedAt = -1d;

                    if (!chainPlayModeAfter &&
                        EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        EditorApplication.ExitPlaymode();
                    }
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
