using CommNet;
using KSP;
using KSP.Localization;
using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using static ModuleCommand;
using static SoftMasking.SoftMask;

namespace LaserComm
{
    public class ModuleOpticalComm : ModuleDataTransmitter, ICommAntenna//, IOverheatDisplay
    {
        [KSPField(isPersistant = true)]
        public bool isOn;

        [KSPField(isPersistant = true)]
        public bool relayMode = false;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "#autoLOC_6001894")]
        public string modeText;

        [KSPField]
        public double laserRange = 0.0;

        protected Module2AxisTracking trackingModule;

        [KSPField(isPersistant = true)]
        protected bool hasPower = true;

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
        }
        public override void OnStart(StartState startState)
        {
            base.OnStart(startState);

            trackingModule = part.Modules.GetModule<Module2AxisTracking>();

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

            return isOn && hasPower;
        }

        new public bool CanCommUnloaded(ProtoPartModuleSnapshot mSnap)
        {
            bool _isOn = false;
            if (!mSnap.moduleValues.TryGetValue(nameof(isOn), ref _isOn))
                return false;

            bool _hasPower = false;
            if (!mSnap.moduleValues.TryGetValue(nameof(hasPower), ref _hasPower))
                return false;

            return _isOn && _hasPower;
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
            Events["ToggleComms"].guiName = isOn ? "Deactivate comms" : "Activate comms";
            Events["StartTransmission"].active = isOn;
        }

        [KSPEvent(guiActiveEditor = true, guiActiveUnfocused = false, guiActive = true, guiName = "#autoLOC_6001300")]
        public void SwitchMode()
        {
            SetRelayMode(!relayMode);
        }

        [KSPAction("Toggle relay mode")]
        public void ToggleRelayModeAction(KSPActionParam param)
        {
            switch (param.type)
            {
                case KSPActionType.Toggle:
                    SetRelayMode(!relayMode);
                    break;
                case KSPActionType.Activate:
                    SetRelayMode(true);
                    break;
                case KSPActionType.Deactivate:
                    SetRelayMode(false);
                    break;
            }
        }

        [KSPAction("Set to relay mode")]
        public void RelayModeOnAction(KSPActionParam param)
        {
            SetRelayMode(true);
        }

        [KSPAction("Set to transmit mode")]
        public void RelayModeOffAction(KSPActionParam param)
        {
            SetRelayMode(false);
        }

        public void SetRelayMode(bool relayModeOn)
        {
            relayMode = relayModeOn;
            modeText = Localizer.Format(relayMode ? "#autoLOC_6004051" : "#autoLOC_6004050");
            modeText = char.ToUpper(modeText[0]) + modeText.Substring(1);
            Events["SwitchMode"].guiName = relayMode ? "Switch to transmit mode" : "Switch to relay mode";
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
                if (!relayMode)
                {
                    var link = vessel.connection.ControlPath?.First;
                    var laserLink = LaserCommNetwork.GetLaserLink(link);
                    if (laserLink != null)
                    {
                        var nextHop = link.OtherEnd(vessel.connection.Comm);
                        double radioStrength = (link.a == nextHop ? link.strengthAR : link.strengthBR);
                        if (NodeUtilities.ConvertSignalStrength(radioStrength) < NodeUtilities.ConvertSignalStrength(laserLink.strengthLaser))
                        {
                            SetTarget(nextHop);
                            return;
                        }
                    }
                }
                else
                {
                    CommNode furthestNode = null;
                    double furthestSqrDistance = 0;
                    CommNode thisNode = vessel.connection.Comm;
           
                    foreach (var entry in thisNode)
                    {
                        (CommNode node, CommLink link) = (entry.Key, entry.Value);

                        var laserLink = LaserCommNetwork.GetLaserLink(link);
                        if (laserLink == null)
                            continue;

                        double sqrDistance = (node.position - thisNode.position).sqrMagnitude;
                        if (sqrDistance > furthestSqrDistance)
                            furthestNode = node;
                    }

                    if (furthestNode != null)
                    {
                        SetTarget(furthestNode);
                        return;
                    }
                }
            }
            SetTarget(null);
        }

        private void SetTarget(CommNode node)
        {
            if (node == null)
            {
                trackingModule.UnSetTarget();
                return;
            }

            double sqrDistance = (vessel.connection.Comm.precisePosition - node.precisePosition).sqrMagnitude;
            if (sqrDistance < 1000 * 1000)
            {
                // point at the terminal rather than the vessel itself
                var targetVessel = FlightGlobals.VesselsLoaded.Find(v => v?.connection.Comm == node);

                foreach (var part in targetVessel.parts)
                {
                    var module = part.Modules.GetModules<ModuleOpticalComm>().Find(m => m.relayMode && m.CanComm());
                    var transform = module?.trackingModule?.pitchRotationTransform;
                    if (transform != null)
                    {
                        trackingModule.SetTarget(transform, node.displayName);
                        return;
                    }
                }
                var otherTerminal = targetVessel.parts.Find(p => p.Modules.GetModules<ModuleOpticalComm>().Any(m => m.relayMode && m.CanComm()));

                if (otherTerminal != null)
                {
                    return;
                }
            }

            trackingModule.SetTarget(node);
        }

        public override bool CanTransmit()
        {
            if (!CanComm())
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
            Events["SwitchMode"].active = !IsBusy();

            var rateMultiplier = 1.0; // TODO scalable

            if (!isOn)
                return;

            hasPower = true;
            if (!resHandler.UpdateModuleResourceInputs(ref statusText, rateMultiplier, 0.9, returnOnFirstLack: true))
            {
                hasPower = false;
            }
        }

        // TODO 
        // adjustable power
        // heat production
        // make resource consumption constant? needed?
        // editor info
        // terrain occlusion
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
