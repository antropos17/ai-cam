#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BugCam.Editor
{
    public static class TowerSceneGenerator
    {
        private const string TowerScenePath = "Assets/BugCam/Tests/TowerScene.unity";
        private const int TowerLevels = 12;
        private const int CubesPerLevel = 4;

        [MenuItem("BugCam/Generate Tower Scene")]
        public static void GenerateTowerScene()
        {
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            CreateGround(scene);
            CreateTower(scene);
            CreateProjectile(scene);
            EditorSceneManager.SaveScene(scene, TowerScenePath);
            AssetDatabase.Refresh();
        }

        private static void CreateGround(Scene scene)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetPositionAndRotation(new Vector3(0f, -0.5f, 0f), Quaternion.identity);
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            SceneManager.MoveGameObjectToScene(ground, scene);
        }

        private static void CreateTower(Scene scene)
        {
            var towerRoot = new GameObject("Tower");
            SceneManager.MoveGameObjectToScene(towerRoot, scene);
            for (var level = 0; level < TowerLevels; level++)
            {
                for (var cubeIndex = 0; cubeIndex < CubesPerLevel; cubeIndex++)
                {
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = "Tower Cube";
                    cube.transform.SetParent(towerRoot.transform);
                    cube.transform.SetPositionAndRotation(
                        new Vector3(
                            (cubeIndex % 2 == 0 ? -0.5f : 0.5f),
                            0.5f + level,
                            (cubeIndex / 2 == 0 ? -0.5f : 0.5f)),
                        Quaternion.identity);
                    cube.AddComponent<Rigidbody>().mass = 1f;
                }
            }
        }

        private static void CreateProjectile(Scene scene)
        {
            var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "Projectile";
            projectile.transform.SetPositionAndRotation(new Vector3(-8f, 5.5f, 0f), Quaternion.identity);
            SceneManager.MoveGameObjectToScene(projectile, scene);
            var rigidbody = projectile.AddComponent<Rigidbody>();
            rigidbody.mass = 2f;
            rigidbody.linearVelocity = Vector3.right * 12f;
        }
    }
}
#endif
