#if UNITY_EDITOR
using BugCam.Core;

namespace BugCam.Editor
{
    /// <summary>
    /// Editor facade over Core.TowerProbeRequestFactory so existing EditMode contracts
    /// keep resolving the Editor type name.
    /// </summary>
    public static class TowerProbeRequestFactory
    {
        public static SimulationRequest CreateBaseline(int stepCount)
        {
            return BugCam.Core.TowerProbeRequestFactory.CreateBaseline(stepCount);
        }

        public static SimulationRequest CreatePerturbed(int stepCount, float magnitudeMetres)
        {
            return BugCam.Core.TowerProbeRequestFactory.CreatePerturbed(stepCount, magnitudeMetres);
        }
    }
}
#endif
