#if UNITY_EDITOR
using System;
using System.IO;
using BugCam.Evidence;
using UnityEditor;
using UnityEngine;

namespace BugCam.Editor
{
    /// <summary>
    /// Temporary camera + RenderTexture screenshots for ghost evidence visuals.
    /// Unavailable when metrics/markers are missing. Always cleans up in finally.
    /// </summary>
    public static class GhostScreenshotCapture
    {
        public readonly struct CaptureResult
        {
            public CaptureResult(
                bool overviewWritten,
                bool firstDivergenceWritten,
                bool maxSpreadWritten,
                bool finalWritten,
                string visualsDirectory)
            {
                OverviewWritten = overviewWritten;
                FirstDivergenceWritten = firstDivergenceWritten;
                MaxSpreadWritten = maxSpreadWritten;
                FinalWritten = finalWritten;
                VisualsDirectory = visualsDirectory ?? string.Empty;
            }

            public bool OverviewWritten { get; }

            public bool FirstDivergenceWritten { get; }

            public bool MaxSpreadWritten { get; }

            public bool FinalWritten { get; }

            public string VisualsDirectory { get; }
        }

        public static CaptureResult Capture(GhostEvidenceDocument document, string runDirectory)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (string.IsNullOrEmpty(runDirectory))
            {
                throw new ArgumentException("Run directory is required.", nameof(runDirectory));
            }

            var visuals = Path.Combine(runDirectory, GhostEvidenceSchema.VisualsDirectoryName);
            Directory.CreateDirectory(visuals);

            var drawSet = document.DrawSet;
            var overview = false;
            var first = false;
            var max = false;
            var final = false;

            GameObject cameraObject = null;
            Camera camera = null;
            RenderTexture rt = null;

            try
            {
                cameraObject = new GameObject("BugCamGhostCaptureCamera_TEMP");
                cameraObject.hideFlags = HideFlags.HideAndDontSave;
                camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 500f;

                rt = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = rt;

                if (drawSet.HasBounds)
                {
                    PositionCamera(camera, drawSet.WorldBounds.center, drawSet.WorldBounds.size.magnitude);
                    overview = RenderAndSave(
                        camera,
                        rt,
                        Path.Combine(visuals, GhostEvidenceSchema.OverviewPngFileName),
                        drawSet,
                        true,
                        true);
                    final = RenderAndSave(
                        camera,
                        rt,
                        Path.Combine(visuals, GhostEvidenceSchema.FinalPngFileName),
                        drawSet,
                        true,
                        true);
                }

                if (drawSet.HasFirstDivergence)
                {
                    PositionCamera(camera, drawSet.FirstDivergenceWorld, 3f);
                    first = RenderAndSave(
                        camera,
                        rt,
                        Path.Combine(visuals, GhostEvidenceSchema.FirstDivergencePngFileName),
                        drawSet,
                        true,
                        true);
                }

                if (drawSet.HasMaxSpread)
                {
                    PositionCamera(camera, drawSet.MaxSpreadWorld, 3f);
                    max = RenderAndSave(
                        camera,
                        rt,
                        Path.Combine(visuals, GhostEvidenceSchema.MaxSpreadPngFileName),
                        drawSet,
                        true,
                        true);
                }
            }
            finally
            {
                if (camera != null)
                {
                    camera.targetTexture = null;
                }

                if (rt != null)
                {
                    RenderTexture.ReleaseTemporary(rt);
                }

                if (cameraObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                }
            }

            return new CaptureResult(overview, first, max, final, visuals);
        }

        private static void PositionCamera(Camera camera, Vector3 focus, float size)
        {
            var distance = Mathf.Max(2f, size * 1.4f);
            var direction = (Vector3.back + Vector3.up * 0.45f + Vector3.right * 0.35f).normalized;
            camera.transform.position = focus + direction * distance;
            camera.transform.LookAt(focus);
            camera.orthographic = false;
            camera.fieldOfView = 50f;
        }

        private static bool RenderAndSave(
            Camera camera,
            RenderTexture rt,
            string path,
            GhostDrawSet drawSet,
            bool showBaseline,
            bool showFans)
        {
            // Render scene, then composite Handles-equivalent polylines via GL onto the RT.
            camera.Render();

            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = rt;
                GL.PushMatrix();
                GL.LoadProjectionMatrix(camera.projectionMatrix);
                GL.modelview = camera.worldToCameraMatrix;
                DrawPolylinesGl(drawSet, showBaseline, showFans);
                DrawMarkersGl(drawSet);
                GL.PopMatrix();

                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                try
                {
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tex.Apply();
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            }
            finally
            {
                RenderTexture.active = previous;
            }

            return File.Exists(path);
        }

        private static void DrawPolylinesGl(GhostDrawSet drawSet, bool showBaseline, bool showFans)
        {
            GL.Begin(GL.LINES);
            for (var i = 0; i < drawSet.Polylines.Length; i++)
            {
                var line = drawSet.Polylines[i];
                if (line.Points == null || line.Points.Length < 2)
                {
                    continue;
                }

                if (line.IsBaseline && !showBaseline)
                {
                    continue;
                }

                if (!line.IsBaseline && !showFans)
                {
                    continue;
                }

                GL.Color(line.Color);
                for (var p = 0; p < line.Points.Length - 1; p++)
                {
                    GL.Vertex(line.Points[p]);
                    GL.Vertex(line.Points[p + 1]);
                }
            }

            GL.End();
        }

        private static void DrawMarkersGl(GhostDrawSet drawSet)
        {
            if (drawSet.Markers == null)
            {
                return;
            }

            GL.Begin(GL.LINES);
            for (var i = 0; i < drawSet.Markers.Length; i++)
            {
                var marker = drawSet.Markers[i];
                if (!marker.Available)
                {
                    continue;
                }

                GL.Color(marker.Color);
                const float r = 0.15f;
                var p = marker.Position;
                GL.Vertex(p + Vector3.left * r);
                GL.Vertex(p + Vector3.right * r);
                GL.Vertex(p + Vector3.down * r);
                GL.Vertex(p + Vector3.up * r);
                GL.Vertex(p + Vector3.back * r);
                GL.Vertex(p + Vector3.forward * r);
            }

            GL.End();
        }
    }
}
#endif
