using CommNet;
using KSP;
using KSP.Localization;
using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace LaserComm
{
    public class ModuleOpticalComm : ModuleDataTransmitter, ICommAntenna//, IOverheatDisplay
    {
        [KSPField(isPersistant = true)]
        public bool isOn;

        [KSPField(isPersistant = true)]
        public bool relayMode = false; // TODO
        // TODO
        // relay follows the furtherst laser connected target

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "#autoLOC_6001894")]
        public string modeText;

        [KSPField]
        public float resourceAmount = 5.0f;

        //[KSPField]
        //public string resourceName = "ElectricCharge";

        [KSPField]
        public double laserRange = 0.0;

        protected Module2AxisTracking trackingModule;

        ModuleOpticalComm()
        {
            antennaType = AntennaType.RELAY;
            packetInterval = 0.5f;
            packetSize = 100f;
            packetResourceCost = 0.0;
            antennaPower = 0.0;
            antennaCombinable = false;
        }

        public override void OnAwake()
        {
            base.OnAwake();

            Fields["statusText"].guiName = "#autoLOC_6001352";
            Fields["statusText"].guiActiveUnfocused = true;
            Fields["statusText"].guiUnfocusedRange = 10.0f;
            Fields["powerText"].guiName = "#autoLOC_6001723";

            trackingModule = part.Modules.GetModule<Module2AxisTracking>();
        }
        public override void OnStart(StartState startState)
        {
            base.OnStart(startState);

            SetCommsState(isOn);
            SetRelayMode(relayMode);

            if (startState != StartState.Editor)
            {
                if (vessel?.connection != null)
                    vessel.connection.OnNetworkUpdate = OnNetworkUpdate;
            }
        }

        new public bool CanComm()
        {
            if (!moduleIsEnabled)
                return false;

            return isOn;
        }

        new public bool CanCommUnloaded(ProtoPartModuleSnapshot mSnap)
        {
            bool isOn = false;
            if (!mSnap.moduleValues.TryGetValue(nameof(isOn), ref isOn))
                return false;

            return isOn;
        }
        public bool RelayModeUnloaded(ProtoPartModuleSnapshot mSnap)
        {
            bool relayMode = false;
            if (!mSnap.moduleValues.TryGetValue(nameof(relayMode), ref relayMode))
                return false;

            return relayMode;
        }

        [KSPEvent(guiActiveEditor = true, guiActiveUnfocused = true, guiActive = true, unfocusedRange = 10f, guiName = "Toggle comms")]
        public void ToggleComms()
        {
            SetCommsState(!isOn);
        }

        [KSPAction("Toggle comms")]
        public void ToggleCommsAction(KSPActionParam param)
        {
            switch (param.type)
            {
                case KSPActionType.Toggle:
                    SetCommsState(!isOn);
                    break;
                case KSPActionType.Activate:
                    SetCommsState(true);
                    break;
                case KSPActionType.Deactivate:
                    SetCommsState(false);
                    break;
            }
        }

        [KSPAction("Activate comms")]
        public void CommsOnAction(KSPActionParam param)
        {
            SetCommsState(true);
        }

        [KSPAction("Deactivate comms")]
        public void CommsOffAction(KSPActionParam param)
        {
            SetCommsState(false);
        }

        public void SetCommsState(bool state)
        {
            isOn = state;
            statusText = isOn ? Localizer.Format("#autoLOC_236147") : Localizer.Format("#autoLOC_237024");
            Events["ToggleComms"].active = !isOn;
            Events["ToggleComms"].guiName = isOn ? "Deactivate comms" : "Activate comms";
            Events["StartTransmission"].active = isOn;
        }

        protected virtual void OnNetworkUpdate()
        {
            UpdateTracking();
        }

        public void UpdateTracking()
        {
            if (trackingModule == null || vessel?.connection == null)
                return;

            var ctrlState = vessel.connection.ControlState;

            if (CanComm() && (ctrlState & (VesselControlState.Partial | VesselControlState.Full)) != 0)
            {
                var link = vessel.connection.ControlPath?.First;
                var laserLink = LaserCommNetwork.GetLaserLink(link);
                if (laserLink != null)
                {
                    var nextHop = link.OtherEnd(vessel.connection.Comm);
                    double radioStrength = (link.a == nextHop ? link.strengthAR : link.strengthBR);
                    if (NodeUtilities.ConvertSignalStrength(radioStrength) < NodeUtilities.ConvertSignalStrength(laserLink.strengthLaser)) // TODO relay
                    {
                        trackingModule.SetTarget(nextHop.transform, nextHop.name);
                        return;
                    }
                }
            }
            trackingModule.SetTarget(null, null);
        }

        public override bool CanTransmit()
        {
            if (!isOn || !moduleIsEnabled)
                return false;

            if (!CommNetScenario.CommNetEnabled)
                return true;

            if (vessel?.connection != null)
                return vessel.connection.ControlPath.IsLastHopHome();

            return false;
        }

        public void FixedUpdate()
        {
            Events["ToggleComms"].active = !IsBusy();
        }

        public void SetRelayMode(bool state)
        {
            relayMode = state;
            modeText = Localizer.Format(relayMode ? "#autoLOC_6004051" : "#autoLOC_6004050");
            modeText = char.ToUpper(modeText[0]) + modeText.Substring(1);
        }

        // TODO 
        // adjustable power
        // heat production
        // make resource consumption constant? needed?
    }

    // y u no virtual
    [HarmonyPatch(typeof(ModuleDataTransmitter))]
    class ModuleOpticalCommPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(ModuleDataTransmitter.CanScienceTo))]
        static void CanScienceTo_Postfix(ModuleDataTransmitter __instance, ref bool __result, bool combined, double bPower, double sqrDistance)
        {
            if (__instance is ModuleOpticalComm module)
                __result = Math.Pow(Math.Max(bPower, module.laserRange), 2) > sqrDistance;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(ModuleDataTransmitter.UpdatePowerText))]
        static void UpdatePowerText_Postfix(ModuleDataTransmitter __instance)
        {
            if (__instance is ModuleOpticalComm module)
                __instance.powerText = KSPUtil.PrintSI(module.laserRange, "m");
        }
    }

}
