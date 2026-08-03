#if UNITY_EDITOR
using System;
using System.IO;
using BugCam.Evidence;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BugCam.Editor
{
    /// <summary>
    /// Temporary camera + RenderTexture screenshots for ghost evidence visuals.
    /// Named artifacts use distinct framing/content — never byte-identical copies.
    /// Unavailable when metrics/markers are missing. Always cleans up in finally.
    /// </summary>
    public static class GhostScreenshotCapture
    {
        private static Material s_LineMaterial;

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

            // batchmode -nographics (Null device) cannot composite GL lines; omit rather than
            // writing identical clear/garbage PNGs that look like visual success.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                return new CaptureResult(false, false, false, false, visuals);
            }

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
                    // Overview: wide framing, baseline + fans + markers.
                    PositionCamera(
                        camera,
                        drawSet.WorldBounds.center,
                        drawSet.WorldBounds.size.magnitude,
                        (Vector3.back + Vector3.up * 0.45f + Vector3.right * 0.35f).normalized,
                        50f);
                    overview = RenderAndSave(
                        camera,
                        rt,
                        Path.Combine(visuals, GhostEvidenceSchema.OverviewPngFileName),
                        drawSet,
                        true,
                        true,
                        true);

                    // Final: distinct opposite-side framing; markers only (no fan clutter).
                    PositionCamera(
                        camera,
                        drawSet.WorldBounds.center,
                        drawSet.WorldBounds.size.magnitude * 0.85f,
                        (Vector3.forward + Vector3.up * 0.65f + Vector3.left * 0.55f).normalized,
                        40f);
                    final = RenderAndSave(
                        camera,
                        rt,
                        Path.Combine(visuals, GhostEvidenceSchema.FinalPngFileName),
                        drawSet,
                        true,
                        false,
                        true);
                }

                if (drawSet.HasFirstDivergence)
                {
                    PositionCamera(
                        camera,
                        drawSet.FirstDivergenceWorld,
                        2.4f,
                        (Vector3.back + Vector3.up * 0.2f + Vector3.right * 0.15f).normalized,
                        45f);
                    first = RenderAndSave(
                        camera,
                        rt,
                        Path.Combine(visuals, GhostEvidenceSchema.FirstDivergencePngFileName),
                        drawSet,
                        true,
                        true,
                        true);
                }

                if (drawSet.HasMaxSpread)
                {
                    PositionCamera(
                        camera,
                        drawSet.MaxSpreadWorld,
                        2.1f,
                        (Vector3.forward + Vector3.up * 0.35f + Vector3.left * 0.25f).normalized,
                        35f);
                    max = RenderAndSave(
                        camera,
                        rt,
                        Path.Combine(visuals, GhostEvidenceSchema.MaxSpreadPngFileName),
                        drawSet,
                        false,
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

        private static void PositionCamera(
            Camera camera,
            Vector3 focus,
            float size,
            Vector3 direction,
            float fieldOfView)
        {
            var distance = Mathf.Max(2f, size * 1.4f);
            camera.transform.position = focus + direction.normalized * distance;
            camera.transform.LookAt(focus);
            camera.orthographic = false;
            camera.fieldOfView = fieldOfView;
        }

        private static bool RenderAndSave(
            Camera camera,
            RenderTexture rt,
            string path,
            GhostDrawSet drawSet,
            bool showBaseline,
            bool showFans,
            bool showMarkers)
        {
            camera.Render();

            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = rt;
                var material = GetLineMaterial();
                if (material == null || !material.SetPass(0))
                {
                    // Fail closed: do not write a blank clear-color PNG as a "visual".
                    return false;
                }

                GL.PushMatrix();
                GL.LoadProjectionMatrix(camera.projectionMatrix);
                GL.modelview = camera.worldToCameraMatrix;
                DrawPolylinesGl(drawSet, showBaseline, showFans);
                if (showMarkers)
                {
                    DrawMarkersGl(drawSet);
                }

                GL.PopMatrix();

                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                try
                {
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tex.Apply();
                    if (IsNearlySolidClear(tex, camera.backgroundColor))
                    {
                        // Fail closed: solid clear-color frames are not valid ghost visuals
                        // (common under batchmode -nographics when GL never composites).
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                        }

                        return false;
                    }

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

        private static bool IsNearlySolidClear(Texture2D tex, Color clear)
        {
            if (tex == null || tex.width <= 0 || tex.height <= 0)
            {
                return true;
            }

            const float tol = 0.03f;
            var stepX = Math.Max(1, tex.width / 32);
            var stepY = Math.Max(1, tex.height / 32);
            for (var y = 0; y < tex.height; y += stepY)
            {
                for (var x = 0; x < tex.width; x += stepX)
                {
                    var c = tex.GetPixel(x, y);
                    if (Mathf.Abs(c.r - clear.r) > tol ||
                        Mathf.Abs(c.g - clear.g) > tol ||
                        Mathf.Abs(c.b - clear.b) > tol)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static Material GetLineMaterial()
        {
            if (s_LineMaterial != null)
            {
                return s_LineMaterial;
            }

            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                return null;
            }

            s_LineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            s_LineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            s_LineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            s_LineMaterial.SetInt("_Cull", (int)CullMode.Off);
            s_LineMaterial.SetInt("_ZWrite", 0);
            s_LineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            return s_LineMaterial;
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
