using CommNet;
using HarmonyLib;
using System;

namespace LaserComm
{
    public class LaserCommLink : CommLink
    {
        public double strengthLaser;
        public double strengthRelayALaser;
        public double strengthRelayBLaser;

        public override void Update(double cost)
        {
            signalStrength = cost;
            signal = NodeUtilities.ConvertSignalStrength(signalStrength);
        }
    }

    // y u no virtual
    [HarmonyPatch(typeof(CommLink))]
    class CommLinkPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(CommLink.GetSignalStrength), new[] { typeof(bool), typeof(bool) })]
        static void GetSignalStrength_Postfix(CommLink __instance, ref double __result, bool aMustRelay, bool bMustRelay)
        {
            if (__instance is LaserCommLink link)
                __result = Math.Max(link.strengthLaser, __result);
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(CommLink.GetBestSignal))]
        static void GetBestSignal_Postfix(CommLink __instance, ref double __result)
        {
            if (__instance is LaserCommLink link)
                __result = Math.Max(link.strengthLaser, __result);
        }
    }
}
