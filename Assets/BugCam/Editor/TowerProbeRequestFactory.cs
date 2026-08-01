#if UNITY_EDITOR
using BugCam.Core;
using UnityEngine;

namespace BugCam.Editor
{
    public static class TowerProbeRequestFactory
    {
        private const int TowerLevels = 12;
        private const int CubesPerLevel = 4;

        public static SimulationRequest CreateBaseline(int stepCount)
        {
            var bodies = new SimulationBodyDefinition[49];
            var stableId = 1;
            for (var level = 0; level < TowerLevels; level++)
            {
                for (var cubeIndex = 0; cubeIndex < CubesPerLevel; cubeIndex++)
                {
                    bodies[stableId - 1] = new SimulationBodyDefinition(
                        stableId,
                        new Vector3(
                            cubeIndex % 2 == 0 ? -0.5f : 0.5f,
                            0.5f + level,
                            cubeIndex / 2 == 0 ? -0.5f : 0.5f),
                        Quaternion.identity,
                        Vector3.one,
                        1f);
                    stableId++;
                }
            }

            bodies[48] = new SimulationBodyDefinition(
                49,
                new Vector3(-8f, 5.5f, 0f),
                Quaternion.identity,
                Vector3.one,
                2f,
                Vector3.right * 12f);
            return new SimulationRequest(bodies, stepCount, default);
        }

        public static SimulationRequest CreatePerturbed(int stepCount, float magnitudeMetres)
        {
            var baseline = CreateBaseline(stepCount);
            return new SimulationRequest(
                baseline.Bodies,
                baseline.StepCount,
                new SimulationPerturbation(49, Vector3.right, magnitudeMetres));
        }
    }
}
#endif
