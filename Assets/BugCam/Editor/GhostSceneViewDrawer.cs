#if UNITY_EDITOR
using BugCam.Evidence;
using UnityEditor;
using UnityEngine;

namespace BugCam.Editor
{
    /// <summary>
    /// Scene View ghost drawing via <see cref="SceneView.duringSceneGui"/> and
    /// <see cref="Handles.DrawAAPolyLine"/>. No permanent GameObjects; does not dirty the scene.
    /// Non-color encodings: labels + legend.
    /// </summary>
    public static class GhostSceneViewDrawer
    {
        private const float LineWidth = 2.5f;
        private const float MarkerRadius = 0.12f;
        private const float LegendWidth = 260f;
        private const float LegendHeight = 88f;
        // Anchor margin. The legend sits at the BOTTOM-RIGHT of the viewport: the top-left
        // hosts the Scene View toolbar and the Tools overlay column (the original Block 1.5
        // backlog defect), and the left flank is routinely covered by the floating Ghost
        // Visualization window in real layouts (verified on the 2026-08-03 A4 screenshots);
        // top-right belongs to the orientation gizmo. Bottom-right is the only corner free
        // in both the default and the observed layouts. Fixed in Block 2.2.1 A4.
        private const float LegendMargin = 12f;
        private const float LabelEdgePadding = 4f;

        private static GUIStyle _legendStyle;
        private static GUIStyle _markerLabelStyle;

        public static void Draw(GhostDrawSet drawSet, bool showBaseline, bool showFans, SceneView sceneView)
        {
            if (drawSet == null || drawSet.Polylines == null)
            {
                return;
            }

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

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

                Handles.color = line.Color;
                Handles.DrawAAPolyLine(LineWidth, line.Points);
            }

            if (drawSet.Markers != null)
            {
                for (var m = 0; m < drawSet.Markers.Length; m++)
                {
                    var marker = drawSet.Markers[m];
                    if (!marker.Available)
                    {
                        continue;
                    }

                    Handles.color = marker.Color;
                    Handles.SphereHandleCap(
                        0,
                        marker.Position,
                        Quaternion.identity,
                        MarkerRadius,
                        EventType.Repaint);
                }
            }

            var viewSize = GetViewSizePoints(sceneView);

            Handles.BeginGUI();
            DrawMarkerLabels(drawSet, viewSize);
            DrawLegend(drawSet, showBaseline, showFans, viewSize);
            Handles.EndGUI();
        }

        /// <summary>
        /// Scene View camera viewport in GUI points (the coordinate space inside
        /// <see cref="Handles.BeginGUI"/>). (0, 0) when unavailable — callers fall back
        /// to unclamped/top-left placement rather than guessing a size.
        /// </summary>
        private static Vector2 GetViewSizePoints(SceneView sceneView)
        {
            if (sceneView == null || sceneView.camera == null)
            {
                return Vector2.zero;
            }

            var pixelRect = sceneView.camera.pixelRect;
            var pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            if (pixelsPerPoint <= 0f)
            {
                return Vector2.zero;
            }

            return new Vector2(pixelRect.width / pixelsPerPoint, pixelRect.height / pixelsPerPoint);
        }

        private static void DrawMarkerLabels(GhostDrawSet drawSet, Vector2 viewSize)
        {
            if (drawSet.Markers == null)
            {
                return;
            }

            var style = MarkerLabelStyle();

            for (var m = 0; m < drawSet.Markers.Length; m++)
            {
                var marker = drawSet.Markers[m];
                if (!marker.Available)
                {
                    continue;
                }

                // Skip markers behind the camera: the old world-space Handles.Label drew
                // them at a meaningless projected point.
                var guiPoint = HandleUtility.WorldToGUIPointWithDepth(marker.Position);
                if (guiPoint.z < 0f)
                {
                    continue;
                }

                var label = marker.Kind == "firstDivergence"
                    ? "First divergence"
                    : marker.Kind == "maxSpread"
                        ? "Max spread"
                        : marker.Kind;

                var content = new GUIContent(label);
                var size = style.CalcSize(content);
                var rect = new Rect(
                    guiPoint.x - size.x * 0.5f,
                    guiPoint.y - size.y - 6f,
                    size.x,
                    size.y);

                // Clamp into the viewport so the label stays readable when the marker sits
                // near (or beyond) a screen edge (Block 1.5 backlog defect).
                if (viewSize.x > 0f && viewSize.y > 0f)
                {
                    rect.x = Mathf.Clamp(rect.x, LabelEdgePadding, Mathf.Max(LabelEdgePadding, viewSize.x - size.x - LabelEdgePadding));
                    rect.y = Mathf.Clamp(rect.y, LabelEdgePadding, Mathf.Max(LabelEdgePadding, viewSize.y - size.y - LabelEdgePadding));
                }

                GUI.Label(rect, content, style);
            }
        }

        private static void DrawLegend(GhostDrawSet drawSet, bool showBaseline, bool showFans, Vector2 viewSize)
        {
            // Bottom-right anchor; top-left fallback only when the viewport size is unknown
            // or too small to hold the legend above the margin.
            var hasSize = viewSize.x > LegendWidth + 2f * LegendMargin &&
                          viewSize.y > LegendHeight + 2f * LegendMargin;
            var x = hasSize ? viewSize.x - LegendWidth - LegendMargin : LegendMargin;
            var y = hasSize ? viewSize.y - LegendHeight - LegendMargin : LegendMargin;
            var rect = new Rect(x, y, LegendWidth, LegendHeight);

            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.55f));
            var style = LegendStyle();
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, 240f, 16f), "BugCam Ghost Visualization", style);
            GUI.Label(
                new Rect(rect.x + 8f, rect.y + 24f, 240f, 16f),
                showBaseline ? "Baseline: white polyline" : "Baseline: hidden",
                style);
            GUI.Label(
                new Rect(rect.x + 8f, rect.y + 40f, 240f, 16f),
                showFans ? "Fans: colored polylines (index palette)" : "Fans: hidden",
                style);
            GUI.Label(
                new Rect(rect.x + 8f, rect.y + 56f, 240f, 16f),
                "Markers: red=first-div, yellow=max-spread | lines=" +
                drawSet.Polylines.Length,
                style);
        }

        private static GUIStyle LegendStyle()
        {
            if (_legendStyle == null)
            {
                _legendStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } };
            }

            return _legendStyle;
        }

        private static GUIStyle MarkerLabelStyle()
        {
            if (_markerLabelStyle == null)
            {
                _markerLabelStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.white } };
            }

            return _markerLabelStyle;
        }
    }
}
#endif
