using HarmonyLib;
using CommNet;
using KSP;
using UnityEngine;
using System;

namespace LaserComm
{
    [HarmonyPatch(typeof(CommNetVessel))]
    class CommNetVesselPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch("UpdateComm")]
        static void UpdateComm_Postfix(ref Vessel ___vessel, ref CommNode ___comm)
        {
            if (___vessel == null)
                return;

            if (!LaserCommNetwork.Instance.laserNodes.TryGetValue(___comm, out var laserComm))
                return;

            laserComm.laserRange = 0;
            laserComm.laserRelayRange = 0;

            if (___vessel.loaded)
            {
                foreach (var part in ___vessel.Parts)
                {
                    foreach (var partModule in part.Modules)
                    {
                        if (partModule is ModuleOpticalComm oc && oc.CanComm())
                        {
                            if (!oc.relayMode)
                                laserComm.laserRange = Math.Max(oc.laserRange, laserComm.laserRange);
                            else
                                laserComm.laserRelayRange = Math.Max(oc.laserRange, laserComm.laserRange);
                        }
                    }
                }
            }
            else
            {
                foreach (var snapshot in ___vessel.protoVessel.protoPartSnapshots)
                {
                    var part = snapshot.partInfo.partPrefab;

                    int idx = 0;
                    foreach (var partModule in part.Modules)
                    {
                        if (partModule is ModuleOpticalComm oc)
                        {
                            var protoModule = snapshot.FindModule(partModule, idx);
                            if (!oc.CanCommUnloaded(protoModule))
                                continue;

                            if(!oc.RelayModeUnloaded(protoModule))
                                laserComm.laserRange = Math.Max(oc.laserRange, laserComm.laserRange);
                            else
                                laserComm.laserRelayRange = Math.Max(oc.laserRange, laserComm.laserRange);
                        }
                        ++idx;
                    }
                }
            }
        }
    }
}
