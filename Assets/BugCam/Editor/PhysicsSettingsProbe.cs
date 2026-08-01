#if UNITY_EDITOR
using System;
using BugCam.Core;
using UnityEditor;
using UnityEngine;

namespace BugCam.Editor
{
    public static class PhysicsSettingsProbe
    {
        private const string DynamicsManagerPath = "ProjectSettings/DynamicsManager.asset";

        public static SimulationThreadingMode ReadThreadingMode()
        {
            var serializedSettings = LoadSerializedSettings();
            var threadingMode = serializedSettings.FindProperty("m_ThreadingMode");
            if (threadingMode == null)
            {
                throw new InvalidOperationException(
                    "Unity physics settings do not expose m_ThreadingMode.");
            }

            switch (threadingMode.intValue)
            {
                case 0:
                    return SimulationThreadingMode.MultiThreaded;
                case 1:
                    return SimulationThreadingMode.SingleThreaded;
                default:
                    throw new InvalidOperationException(
                        "Unsupported Unity physics threading mode value: " +
                        threadingMode.intValue + ".");
            }
        }

        public static void Print()
        {
            var serializedSettings = LoadSerializedSettings();
            var iterator = serializedSettings.GetIterator();
            if (!iterator.NextVisible(true))
            {
                throw new InvalidOperationException("Physics settings contain no serialized properties.");
            }

            do
            {
                if (IsEnvironmentProperty(iterator.propertyPath))
                {
                    Debug.Log(Describe(iterator));
                }
            }
            while (iterator.NextVisible(false));

            Debug.Log($"BUGCAM_TIME fixedDeltaTime={Time.fixedDeltaTime:R}");
        }

        private static SerializedObject LoadSerializedSettings()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(DynamicsManagerPath);
            if (assets.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Unity did not expose {DynamicsManagerPath} through AssetDatabase.");
            }

            var serializedSettings = new SerializedObject(assets[0]);
            serializedSettings.Update();
            return serializedSettings;
        }

        private static string Describe(SerializedProperty property)
        {
            string value;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    value = property.boolValue.ToString();
                    break;
                case SerializedPropertyType.Enum:
                    value = $"{property.enumValueIndex}:{property.enumDisplayNames[property.enumValueIndex]}" +
                            $" options=[{string.Join(",", property.enumDisplayNames)}]";
                    break;
                case SerializedPropertyType.Float:
                    value = property.floatValue.ToString("R");
                    break;
                case SerializedPropertyType.Integer:
                    value = property.intValue.ToString();
                    break;
                case SerializedPropertyType.Vector3:
                    value = property.vector3Value.ToString("R");
                    break;
                default:
                    value = property.propertyType.ToString();
                    break;
            }

            return
                $"BUGCAM_PHYSICS path={property.propertyPath} display={property.displayName} " +
                $"type={property.propertyType} value={value}";
        }

        private static bool IsEnvironmentProperty(string path)
        {
            switch (path)
            {
                case "m_Gravity":
                case "m_BounceThreshold":
                case "m_SleepThreshold":
                case "m_DefaultContactOffset":
                case "m_DefaultSolverIterations":
                case "m_DefaultSolverVelocityIterations":
                case "m_SimulationMode":
                case "m_ThreadingMode":
                case "m_AutoSyncTransforms":
                case "m_ReuseCollisionCallbacks":
                case "m_BroadphaseType":
                case "m_FrictionType":
                case "m_EnableEnhancedDeterminism":
                case "m_SolverType":
                case "m_DefaultMaxAngularSpeed":
                    return true;
                default:
                    return false;
            }
        }
    }
}
#endif
