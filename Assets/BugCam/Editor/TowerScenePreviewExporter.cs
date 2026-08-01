#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BugCam.Editor
{
    [InitializeOnLoad]
    public static class TowerScenePreviewExporter
    {
        private const string ScenePath = "Assets/BugCam/Tests/TowerScene.unity";
        private const string OutputPath = "Library/BugCamTowerPreview.png";
        private static readonly string RequestPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Library/BugCamPreview.request"));

        static TowerScenePreviewExporter()
        {
            EditorApplication.update += PollForRequest;
        }

        private static void PollForRequest()
        {
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling)
            {
                return;
            }

            File.Delete(RequestPath);
            EditorApplication.delayCall += Export;
        }

        public static void Export()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.34f, 0.42f);

            var cameraObject = new GameObject("Preview Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(15f, 10f, -18f);
            camera.transform.LookAt(new Vector3(0f, 5.3f, 0f));
            camera.fieldOfView = 40f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.075f);

            var lightObject = new GameObject("Preview Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.color = new Color(1f, 0.9f, 0.78f);
            light.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

            ColorScene(scene.GetRootGameObjects());

            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
            image.Apply();
            File.WriteAllBytes(OutputPath, image.EncodeToPNG());

            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(lightObject);
            AssetDatabase.Refresh();
            Debug.Log("BugCam tower preview exported: " + Path.GetFullPath(OutputPath));
        }

        private static void ColorScene(GameObject[] roots)
        {
            var shader = Shader.Find("Hidden/Internal-Colored");
            foreach (var root in roots)
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    var material = new Material(shader);
                    if (renderer.gameObject.name == "Projectile")
                    {
                        material.SetColor("_Color", new Color(1f, 0.24f, 0.08f));
                    }
                    else if (renderer.gameObject.name == "Ground")
                    {
                        material.SetColor("_Color", new Color(0.08f, 0.1f, 0.16f));
                    }
                    else
                    {
                        var height = renderer.transform.position.y / 12f;
                        material.SetColor("_Color", Color.Lerp(
                            new Color(0.05f, 0.7f, 0.85f),
                            new Color(0.5f, 0.2f, 1f),
                            height));
                    }

                    renderer.sharedMaterial = material;
                }
            }
        }
    }
}
#endif
