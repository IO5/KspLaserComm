using UnityEngine;

namespace LaserComm
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class Harmony : MonoBehaviour
    {
        void Start()
        {
            var harmony = new HarmonyLib.Harmony("LaserComm.Harmony");
            harmony.PatchAll();
        }
    }
}
