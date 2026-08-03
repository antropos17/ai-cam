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

        public static void Draw(GhostDrawSet drawSet, bool showBaseline, bool showFans)
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

                    var label = marker.Kind == "firstDivergence"
                        ? "First divergence"
                        : marker.Kind == "maxSpread"
                            ? "Max spread"
                            : marker.Kind;
                    Handles.Label(marker.Position + Vector3.up * 0.2f, label);
                }
            }

            DrawLegend(drawSet, showBaseline, showFans);
        }

        private static void DrawLegend(GhostDrawSet drawSet, bool showBaseline, bool showFans)
        {
            Handles.BeginGUI();
            var rect = new Rect(12f, 12f, 260f, 88f);
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.55f));
            var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } };
            GUI.Label(new Rect(20f, 18f, 240f, 16f), "BugCam Ghost Visualization", style);
            GUI.Label(
                new Rect(20f, 36f, 240f, 16f),
                showBaseline ? "Baseline: white polyline" : "Baseline: hidden",
                style);
            GUI.Label(
                new Rect(20f, 52f, 240f, 16f),
                showFans ? "Fans: colored polylines (index palette)" : "Fans: hidden",
                style);
            GUI.Label(
                new Rect(20f, 68f, 240f, 16f),
                "Markers: red=first-div, yellow=max-spread | lines=" +
                drawSet.Polylines.Length,
                style);
            Handles.EndGUI();
        }
    }
}
#endif
