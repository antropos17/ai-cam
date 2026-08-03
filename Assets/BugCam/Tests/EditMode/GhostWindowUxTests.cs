#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BugCam.Tests
{
    /// <summary>
    /// Block 2.2 window-UX regression pins.
    /// The manual-test blocker: after a window-started search completed, the editor stayed
    /// in Play Mode and the window blamed the user ("Play Mode started manually"). These
    /// tests pin the fix contracts: the host exits ONLY the Play Mode session it started
    /// itself, strictly after Busy is cleared, and the Core progress accessors the window
    /// depends on stay present and read-only.
    /// </summary>
    public sealed class GhostWindowUxTests
    {
        private static string HostSource()
        {
            return File.ReadAllText(Path.Combine(
                Application.dataPath, "BugCam", "Editor", "GhostEvidencePlayModeHost.cs"));
        }

        private static string WindowSource()
        {
            return File.ReadAllText(Path.Combine(
                Application.dataPath, "BugCam", "Editor", "GhostVisualizationWindow.cs"));
        }

        private static int CountOf(string haystack, string needle)
        {
            var count = 0;
            var index = 0;
            while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }

            return count;
        }

        [Test]
        public void HostExitsOnlyHostEnteredPlayModeAndOnlyAfterCleanup()
        {
            var hostCs = HostSource();

            // Marker is set exactly once — in the branch where the host itself flips
            // isPlaying. A search launched inside a user-started Play Mode session must
            // never set it.
            const string setMarker = "SessionState.SetBool(EnteredPlayModeKey, true);";
            Assert.That(
                CountOf(hostCs, setMarker),
                Is.EqualTo(1),
                "Exactly one host-initiated Play Mode entry may arm the exit marker.");
            var setIdx = hostCs.IndexOf(setMarker, StringComparison.Ordinal);
            var flipIdx = hostCs.IndexOf(
                "EditorApplication.isPlaying = true;",
                StringComparison.Ordinal);
            Assert.That(flipIdx, Is.GreaterThan(setIdx),
                "Marker must be armed before the host flips isPlaying, in the same branch.");

            // The exit helper refuses to touch Play Mode unless the marker is armed.
            var helperIdx = hostCs.IndexOf(
                "private static void ExitHostEnteredPlayMode()",
                StringComparison.Ordinal);
            Assert.That(helperIdx, Is.GreaterThanOrEqualTo(0),
                "Host must define ExitHostEnteredPlayMode.");
            var helperBody = hostCs.Substring(helperIdx, Math.Min(700, hostCs.Length - helperIdx));
            StringAssert.Contains(
                "if (!SessionState.GetBool(EnteredPlayModeKey, false))",
                helperBody,
                "Exit helper must gate on the marker — never exit a user-started session.");

            // Both completion paths call the exit helper, and each call comes strictly
            // after Cleanup() so ExitingPlayMode sees Busy=false and cannot emit a false
            // 'Interrupted' completion.
            const string exitCall = "ExitHostEnteredPlayMode();";
            var callCount = 0;
            var searchFrom = 0;
            while (true)
            {
                var idx = hostCs.IndexOf(exitCall, searchFrom, StringComparison.Ordinal);
                if (idx < 0)
                {
                    break;
                }

                callCount++;
                var windowBefore = hostCs.Substring(Math.Max(0, idx - 300), Math.Min(300, idx));
                StringAssert.Contains(
                    "Cleanup();",
                    windowBefore,
                    "Every ExitHostEnteredPlayMode call must directly follow Cleanup().");
                searchFrom = idx + exitCall.Length;
            }

            Assert.That(callCount, Is.EqualTo(2),
                "Both completion paths (write-failure and success) must exit host-entered Play Mode.");

            // Any Play Mode exit — manual stop, interrupt — disarms the marker so a stale
            // flag can never auto-exit a future user-started session.
            var exitingIdx = hostCs.IndexOf(
                "PlayModeStateChange.ExitingPlayMode",
                StringComparison.Ordinal);
            Assert.That(exitingIdx, Is.GreaterThanOrEqualTo(0));
            var exitingBlock = hostCs.Substring(exitingIdx, Math.Min(700, hostCs.Length - exitingIdx));
            StringAssert.Contains(
                "SessionState.SetBool(EnteredPlayModeKey, false);",
                exitingBlock,
                "ExitingPlayMode must disarm the marker.");
        }

        [Test]
        public void WindowNeverEntersPlayModeAndExitsOnlyViaInterrupt()
        {
            var windowCs = WindowSource();
            Assert.That(
                CountOf(windowCs, "EditorApplication.isPlaying = true"),
                Is.EqualTo(0),
                "The window must never enter Play Mode itself — the host owns entry.");
            Assert.That(
                CountOf(windowCs, "EditorApplication.isPlaying = false;"),
                Is.EqualTo(1),
                "Exactly one window-side Play Mode exit: the user-facing interrupt button.");
        }

        [Test]
        public void CoreExposesReadOnlySearchProgressAccessors()
        {
            var searchType = Type.GetType("BugCam.Core.EpsilonSearch, BugCam.Core");
            Assert.That(searchType, Is.Not.Null);
            foreach (var name in new[]
                     {
                         "CurrentPhaseStep",
                         "PhaseStepTotal",
                         "HasOutstandingProbe",
                         "CurrentEpsilonMetres"
                     })
            {
                var prop = searchType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                Assert.That(prop, Is.Not.Null, name + " must exist for window progress.");
                Assert.That(prop.CanWrite, Is.False, name + " must be read-only.");
            }
        }

        [Test]
        public void HostExposesSearchProgressEvent()
        {
            var hostType = Type.GetType("BugCam.Editor.GhostEvidencePlayModeHost, BugCam.Editor");
            Assert.That(hostType, Is.Not.Null);
            Assert.That(
                hostType.GetEvent("SearchProgress", BindingFlags.Public | BindingFlags.Static),
                Is.Not.Null,
                "Host must expose the SearchProgress event the window repaints on.");

            var progressType = Type.GetType("BugCam.Editor.GhostSearchProgress, BugCam.Editor");
            Assert.That(progressType, Is.Not.Null);
            foreach (var name in new[] { "Phase", "CurrentStep", "StepTotal", "EpsilonMetres", "HasEpsilon" })
            {
                var prop = progressType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                Assert.That(prop, Is.Not.Null, "GhostSearchProgress." + name + " must exist.");
                Assert.That(prop.CanWrite, Is.False, "GhostSearchProgress." + name + " must be read-only.");
            }
        }
    }
}
#endif
