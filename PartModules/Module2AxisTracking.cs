using CommNet;
using KSP;
using KSP.UI.Screens;
using KSP.Localization;
using System;
using UnityEngine;
using KSP.UI.Dialogs;
using static GameEvents;

namespace LaserComm
{
    // NOTE: with start heading and pitch at 0 the part should be pointed towards positive Z
    // deploy animation (if any) should end at rest heading/pitch or 0,0
    public class Module2AxisTracking : PartModule, IMultipleDragCube
    {
        public enum DeployState
        {
            RETRACTED,
            DEPLOYING,
            DEPLOYED,
            RETRACTING,
        }

        [KSPField(guiActive = true, guiName = "#autoLOC_7001219")]
        public string targetName = Localizer.Format("#autoLOC_6003083");

        [KSPField]
        public float trackingMaxSpeed = 40.0f;

        [KSPField]
        public float trackingSmoothTime = 0.3f;

        [KSPField]
        public string headingPivotName = "headingPivot";

        [KSPField]
        public string pitchPivotName = "pitchPivot";

        [KSPField]
        public string deployAnimationName;

        [KSPField]
        public float deployAnimationSpeed = 1;

        [KSPField]
        public float restHeading = float.NaN; // NaN => no rest position, just stop on target loss

        [KSPField]
        public float restPitch = float.NaN; // NaN => no rest position, just stop on target loss

        // NOTE it's possible to use a range where min > max e.g. (300, 60)
        [KSPField]
        public float minHeading = 0.0f; // [0, 360)

        [KSPField]
        public float maxHeading = 360.0f; // [0, 360) 

        [KSPField]
        public float minPitch = 0.0f; // [-90, 90]

        [KSPField]
        public float maxPitch = 90.0f; // [-90, 90]

        [KSPField(isPersistant = true)]
        public float currentHeading = 0.0f;

        [KSPField(isPersistant = true)]
        public float currentPitch = 0.0f;

        [KSPField(isPersistant = true)]
        public DeployState deployState = DeployState.RETRACTED;

        public Transform targetTransform { get; protected set; }

        internal Transform headingRotationTransform;
        internal Transform pitchRotationTransform;
        protected float currentHeadingVelocity = 0.0f;
        protected float currentPitchVelocity = 0.0f;
        public Animation deployAnimation { get; protected set; }

        [KSPField(isPersistant = true)]
        public float storedAnimationTime = 0;

        public EventData<DeployState> onDeployStateChange { get; protected set; }

        public bool isDeploying { get { return deployState == DeployState.DEPLOYING || deployState == DeployState.RETRACTING; } }

        public override void OnAwake()
        {
            base.OnAwake();

            headingRotationTransform = part.FindModelTransform(headingPivotName);
            pitchRotationTransform = part.FindModelTransform(pitchPivotName);

            if (!FindAnimation())
            {
                Events["ToggleDeploy"].active = false;
                Actions["ToggleDeployAction"].active = false;
                Actions["DeployAction"].active = false;
                Actions["RetractAction"].active = false;
            }

            if (HighLogic.LoadedSceneIsEditor)
            {
                currentHeading  = float.IsNaN(restHeading) ? 0.0f : restHeading;
                currentPitch  = float.IsNaN(restPitch) ? 0.0f : restPitch;
            }

            onDeployStateChange = new EventData<DeployState>(part.partName + "_" + part.flightID + "_" + part.Modules.IndexOf(this) + "_onDeployStateChange");
        }

        public override void OnStart(StartState state)
        {
            if (deployAnimation)
            {
                deployAnimation[deployAnimationName].wrapMode = WrapMode.ClampForever;

                deployAnimation[deployAnimationName].enabled = true;
                deployAnimation[deployAnimationName].weight = 1f;
                deployAnimation[deployAnimationName].normalizedTime = storedAnimationTime;

                switch (deployState)
                {
                    case DeployState.DEPLOYING:
                        deployAnimation[deployAnimationName].speed = deployAnimationSpeed;
                        Events["ToggleDeploy"].active = false;
                        break;
                    case DeployState.RETRACTING:
                        deployAnimation[deployAnimationName].speed = -deployAnimationSpeed;
                        Events["ToggleDeploy"].active = false;
                        break;
                    default:
                        deployAnimation[deployAnimationName].speed = 0;
                        Events["ToggleDeploy"].active = true;
                        Events["ToggleDeploy"].guiName = Localizer.Format(deployState == DeployState.RETRACTED ? "#autoLOC_6001333" : "#autoLOC_6001339");
                        break;
                }
            }

            if (state != StartState.Editor)
            {
                if (deployState == DeployState.DEPLOYED)
                    RotateTo(currentHeading, currentPitch);
                UpdateDragCubes();
            }
        }

        [KSPEvent(guiActiveEditor = true, guiActiveUnfocused = true, guiActive = true, guiName = "#autoLOC_6001337")]
        public void ToggleDeploy()
        {
            DoDeployOrRetract(deployState == DeployState.RETRACTED);
        }

        [KSPAction("#autoLOC_6001337")]
        public void ToggleDeployAction(KSPActionParam param)
        {
            switch (param.type)
            {
                case KSPActionType.Toggle:
                    DoDeployOrRetract(deployState == DeployState.RETRACTED);
                    break;
                case KSPActionType.Activate:
                    DoDeployOrRetract(true);
                    break;
                case KSPActionType.Deactivate:
                    DoDeployOrRetract(false);
                    break;
            }
        }

        [KSPAction("#autoLOC_6001333")]
        public void DeployAction(KSPActionParam param)
        {
            DoDeployOrRetract(true);
        }

        [KSPAction("#autoLOC_6001339")]
        public void RetractAction(KSPActionParam param)
        {
            DoDeployOrRetract(false);
        }

        protected void DoDeployOrRetract(bool deploy)
        {
            if (isDeploying)
                return;

            if (deployAnimation != null)
            {
                if (deploy)
                    PlayDeployAnimation(!deploy);
                else
                    deployAnimation[deployAnimationName].normalizedTime = 1;

                deployState = deploy ? DeployState.DEPLOYING : DeployState.RETRACTING;
                Events["ToggleDeploy"].active = false;
            }
            else
            {
                deployState = deploy ? DeployState.DEPLOYED : DeployState.RETRACTED;
            }
            onDeployStateChange.Fire(deployState);
        }

        protected void PlayDeployAnimation(bool reverse)
        {
            var anim = deployAnimation[deployAnimationName];
            anim.speed = deployAnimationSpeed * (HighLogic.LoadedSceneIsEditor ? 3 : 1);
            if (reverse)
                anim.speed *= -1;
            anim.normalizedTime = reverse ? 1 : 0;
            anim.enabled = true;
            deployAnimation.Play(deployAnimationName);
        }

        public override void OnIconCreate()
        {
            if (deployAnimation != null)
            {
                deployAnimation[deployAnimationName].normalizedTime = 1f;
                deployAnimation[deployAnimationName].normalizedSpeed = 0f;
                deployAnimation[deployAnimationName].enabled = true;
                deployAnimation[deployAnimationName].speed = 0f;
                deployAnimation[deployAnimationName].weight = 1f;
                deployAnimation.Play(deployAnimationName);
            }

            RotateTo(restHeading, restPitch);
        }

        public bool IsMultipleCubesActive { get { return true; } }

        public bool UsesProceduralDragCubes() { return false; }

        public void AssumeDragCubePosition(string name)
        {
            // Used by the DragCubeSystem when it is generating drag cube entries on startup if none are found for this module.
            // When called the module should set it's model position/setup to match the expected position/orientation when 'name' drag cube is active. 
            if (name == "RETRACTED")
                return;

            if (deployAnimation != null)
            {
                deployAnimation[deployAnimationName].normalizedTime = 1f;
                deployAnimation[deployAnimationName].normalizedSpeed = 0f;
                deployAnimation[deployAnimationName].enabled = true;
                deployAnimation[deployAnimationName].speed = 0f;
                deployAnimation[deployAnimationName].weight = 1f;
                deployAnimation.Play(deployAnimationName);
            }

            switch (name)
            {
                case "UP_FORWARD":
                    RotateTo(0, 90);
                    break;
                case "UP_SIDE":
                    RotateTo(90, 90);
                    break;
                case "FORWARD":
                    RotateTo(0, 0);
                    break;
                case "SIDE":
                    RotateTo(90, 0);
                    break;
            }
        }

        public string[] GetDragCubeNames()
        {
            return new string[] { "RETRACTED", "UP_FORWARD", "UP_SIDE", "FORWARD", "SIDE" };
        }

        protected void UpdateDragCubes()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            float extension = deployAnimation ? deployAnimation[deployAnimationName].normalizedTime : 1;
            float vertical = Mathf.Max(0, Mathf.Sin(currentPitch * Mathf.Deg2Rad));
            float horizontal = Mathf.Abs(Mathf.Cos(currentHeading * Mathf.Deg2Rad));
            part.DragCubes.SetCubeWeight("RETRACTED", 1 - extension);
            part.DragCubes.SetCubeWeight("UP_FORWARD", extension * vertical * horizontal);
            part.DragCubes.SetCubeWeight("UP_SIDE", extension * vertical * (1 - horizontal));
            part.DragCubes.SetCubeWeight("FORWARD", extension * (1 - vertical) * horizontal);
            part.DragCubes.SetCubeWeight("SIDE", extension * (1 - vertical) * (1 - horizontal));
        }

        public void RotateTo(float heading, float pitch)
        {
            headingRotationTransform.localRotation = Quaternion.Euler(0, 0, heading);
            pitchRotationTransform.localRotation = Quaternion.Euler(pitch, 0, 0);
            currentHeading = heading;
            currentPitch = pitch;
        }

        protected (float, float) CalculateTracking()
        {
            if (targetTransform == null)
            {
                var heading = float.IsNaN(restHeading) ? currentHeading : restHeading;
                var pitch = float.IsNaN(restPitch) ? currentPitch : restPitch;
                return (heading, pitch);
            }
            else
            {
                Vector3 vector = part.transform.InverseTransformPoint(targetTransform.position);
                var rotation = Quaternion.LookRotation(vector, Vector3.up).eulerAngles;
                var heading = rotation.y;
                var pitch = -rotation.x;
                return (heading, pitch);
            }
        }

        protected (float, float) ApplyLimits((float, float) rotation)
        {
            (float heading, float pitch) = rotation;

            if (minHeading != 0.0 && maxHeading != 360)
            {
                heading += heading < 0 ? 360 : 0;

                if ((minHeading <= maxHeading) && (heading < minHeading || heading > maxHeading) ||
                    ((minHeading > maxHeading) && (heading < minHeading && heading > maxHeading)))
                {
                    if (Mathf.Abs(Mathf.DeltaAngle(heading, minHeading)) < Mathf.Abs(Mathf.DeltaAngle(heading, maxHeading)))
                        heading = minHeading;
                    else
                        heading = maxHeading;
                }
            }

            pitch = Mathf.DeltaAngle(0, pitch);
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            pitch += pitch < 0 ? 360 : 0;

            return (heading, pitch);
        }

        private static string noneStr = Localizer.Format("#autoLOC_6003083");

        public void SetTarget(Transform transform, string name)
        {
            targetTransform = transform;
            if (string.IsNullOrEmpty(name))
            {
                targetName = noneStr;
            }
            else
            {
                targetName = Localizer.Format(name);
            }
        }

        public void UnSetTarget() => SetTarget(null, null);

        public void SetTarget(CelestialBody body) => SetTarget(body?.transform, body?.displayName);

        public void SetTarget(Vessel vessel) => SetTarget(vessel?.transform, vessel?.vesselName);

        public void SetTarget(CommNode node) => SetTarget(node?.transform, node?.displayName);

        private bool FindAnimation()
        {
            if (string.IsNullOrEmpty(deployAnimationName))
                return false;

            foreach (var animation in GetComponentsInChildren<Animation>())
            {
                if (animation.GetClip(deployAnimationName) != null)
                {
                    deployAnimation = animation;
                    break;
                }
            }

            if (deployAnimation && !deployAnimation[deployAnimationName])
                deployAnimation = null;

            if (deployAnimation == null)
                deployState = DeployState.DEPLOYED;

            return (bool)deployAnimation;
        }

        public void FixedUpdate()
        {
            if (isDeploying)
            {
                var animTime = deployAnimation[deployAnimationName].normalizedTime;
                storedAnimationTime = animTime;

                if (deployState == DeployState.RETRACTING &&
                    animTime >= 1 &&
                    Mathf.Abs(Mathf.DeltaAngle(currentHeading, 0)) < 0.001 &&
                    Mathf.Abs(Mathf.DeltaAngle(currentPitch, 0)) < 000.1)
                {
                    PlayDeployAnimation(true);
                    UpdateDragCubes();
                    return;
                }
                else if ((deployState == DeployState.DEPLOYING) ? (animTime >= 1) : (animTime <= 0))
                {
                    deployState = (deployState == DeployState.DEPLOYING ? DeployState.DEPLOYED : DeployState.RETRACTED);
                    deployAnimation.Stop(deployAnimationName);
                    Events["ToggleDeploy"].active = true;
                    Events["ToggleDeploy"].guiName = Localizer.Format(deployState == DeployState.RETRACTED ? "#autoLOC_6001333" : "#autoLOC_6001339");
                    onDeployStateChange.Fire(deployState);
                    UpdateDragCubes();
                    storedAnimationTime = 0;
                    return;
                }
            }

            if (HighLogic.LoadedSceneIsFlight)
            {
                if (deployState == DeployState.RETRACTING)
                {
                    float heading = Mathf.SmoothDampAngle(currentHeading, 0, ref currentHeadingVelocity, trackingSmoothTime, trackingMaxSpeed, TimeWarp.deltaTime);
                    float pitch = Mathf.SmoothDampAngle(currentPitch, 0, ref currentPitchVelocity, trackingSmoothTime, trackingMaxSpeed, TimeWarp.deltaTime);

                    if (deployAnimation == null || !deployAnimation[deployAnimationName].enabled)
                        RotateTo(heading, pitch);
                }
                else if (deployState == DeployState.DEPLOYED)
                {
                    (float targetHeading, float targetPitch) = (deployState == DeployState.RETRACTING) ? (0, 0) : CalculateTracking();
                    targetPitch += targetPitch < 0 ? 360 : 0;

                    float heading = Mathf.SmoothDampAngle(currentHeading, targetHeading, ref currentHeadingVelocity, trackingSmoothTime, trackingMaxSpeed, TimeWarp.deltaTime);
                    float pitch = Mathf.SmoothDampAngle(currentPitch, targetPitch, ref currentPitchVelocity, trackingSmoothTime, trackingMaxSpeed, TimeWarp.deltaTime);

                    heading = (float.IsNaN(heading) ? 0 : heading);
                    pitch = (float.IsNaN(pitch) ? 0 : pitch);

                    (heading, pitch) = ApplyLimits((heading, pitch));

                    RotateTo(heading, pitch);
                }

                UpdateDragCubes();
            }
        }
    }
}
