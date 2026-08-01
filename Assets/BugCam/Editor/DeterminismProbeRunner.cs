#if UNITY_EDITOR
using BugCam.Core;

namespace BugCam.Editor
{
    public static class DeterminismProbeRunner
    {
        public static DeterminismProbeResult RunCurrentMode(
            SimulationRequest baselineRequest,
            SimulationRequest perturbedRequest)
        {
            return new DeterminismProbe().Run(
                baselineRequest,
                perturbedRequest,
                PhysicsSettingsProbe.ReadThreadingMode());
        }
    }
}
#endif
