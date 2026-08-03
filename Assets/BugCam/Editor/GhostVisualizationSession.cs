#if UNITY_EDITOR
using System;
using BugCam.Core;
using BugCam.Evidence;
using UnityEditor;
using UnityEngine;

namespace BugCam.Editor
{
    /// <summary>
    /// Idempotent ghost visualization session: Scene View registration, materials/RT cleanup,
    /// domain reload and play-mode transition safety.
    /// </summary>
    [InitializeOnLoad]
    public sealed class GhostVisualizationSession : IDisposable
    {
        private static GhostVisualizationSession _active;

        private bool _registered;
        private bool _visible = true;
        private bool _showBaseline = true;
        private bool _showFans = true;
        private bool _disposed;
        private GhostEvidenceDocument _document;
        private string _evidenceDirectory = string.Empty;
        private string _metricsPath = string.Empty;

        static GhostVisualizationSession()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static GhostVisualizationSession Active => _active;

        public GhostEvidenceDocument Document => _document;

        public string EvidenceDirectory => _evidenceDirectory;

        public string MetricsPath => _metricsPath;

        public bool IsVisible
        {
            get => _visible;
            set => _visible = value;
        }

        public bool ShowBaseline
        {
            get => _showBaseline;
            set => _showBaseline = value;
        }

        public bool ShowFans
        {
            get => _showFans;
            set => _showFans = value;
        }

        public bool HasDocument => _document != null;

        public static GhostVisualizationSession Ensure()
        {
            if (_active == null || _active._disposed)
            {
                _active = new GhostVisualizationSession();
            }

            _active.Register();
            return _active;
        }

        public void SetDocument(
            GhostEvidenceDocument document,
            string evidenceDirectory = null,
            string metricsPath = null)
        {
            ThrowIfDisposed();
            _document = document;
            _evidenceDirectory = evidenceDirectory ?? string.Empty;
            _metricsPath = metricsPath ?? string.Empty;
            Register();
            SceneView.RepaintAll();
        }

        public void Clear()
        {
            ThrowIfDisposed();
            _document = null;
            _evidenceDirectory = string.Empty;
            _metricsPath = string.Empty;
            // No document ⇒ drop Scene View callback until SetDocument/Ensure re-registers.
            Unregister();
            SceneView.RepaintAll();
        }

        public void Register()
        {
            if (_disposed || _registered)
            {
                return;
            }

            SceneView.duringSceneGui += OnSceneGui;
            _registered = true;
        }

        public void Unregister()
        {
            if (!_registered)
            {
                return;
            }

            SceneView.duringSceneGui -= OnSceneGui;
            _registered = false;
        }

        public void FrameFirstDivergence()
        {
            if (_document == null || !_document.DrawSet.HasFirstDivergence)
            {
                return;
            }

            FramePoint(_document.DrawSet.FirstDivergenceWorld, 1.5f);
        }

        public void FrameMaxSpread()
        {
            if (_document == null || !_document.DrawSet.HasMaxSpread)
            {
                return;
            }

            FramePoint(_document.DrawSet.MaxSpreadWorld, 1.5f);
        }

        public void FrameOverview()
        {
            if (_document == null || !_document.DrawSet.HasBounds)
            {
                return;
            }

            var view = SceneView.lastActiveSceneView;
            if (view == null)
            {
                return;
            }

            view.Frame(_document.DrawSet.WorldBounds, false);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Unregister();
            _document = null;
            _disposed = true;
            if (ReferenceEquals(_active, this))
            {
                _active = null;
            }
        }

        private void OnSceneGui(SceneView sceneView)
        {
            if (!_visible || _document == null)
            {
                return;
            }

            GhostSceneViewDrawer.Draw(_document.DrawSet, _showBaseline, _showFans);
        }

        private static void FramePoint(Vector3 point, float size)
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null)
            {
                return;
            }

            view.Frame(new Bounds(point, Vector3.one * size), false);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GhostVisualizationSession));
            }
        }

        private static void OnBeforeAssemblyReload()
        {
            if (_active != null)
            {
                _active.Unregister();
                _active._disposed = true;
                _active = null;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.ExitingPlayMode)
            {
                // Keep document; only ensure drawer re-registers after transition.
                if (_active != null && !_active._disposed)
                {
                    _active.Unregister();
                }
            }
            else if (state == PlayModeStateChange.EnteredEditMode ||
                     state == PlayModeStateChange.EnteredPlayMode)
            {
                if (_active != null && !_active._disposed)
                {
                    _active.Register();
                }
            }
        }
    }
}
#endif
