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
    // NOTE: part should be pointed towards positive Z (negative Y in blender)
    public class Module2AxisTracking : PartModule, IMultipleDragCube
    {
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
        public Transform targetTransform { get; protected set; }

        protected Transform headingRotationTransform;
        protected Transform pitchRotationTransform;
        protected float currentHeadingVelocity = 0.0f;
        protected float currentPitchVelocity = 0.0f;

        public override void OnAwake()
        {
            base.OnAwake();

            headingRotationTransform = part.FindModelTransform(headingPivotName);
            pitchRotationTransform = part.FindModelTransform(pitchPivotName);

            if (HighLogic.LoadedSceneIsEditor)
            {
                var heading = float.IsNaN(restHeading) ? 0.0f : restHeading;
                var pitch = float.IsNaN(restPitch) ? 0.0f : restPitch;
                RotateTo(heading, pitch);
            }
        }

        public override void OnStart(StartState state)
        {
            if (state != StartState.Editor)
            {
                RotateTo(currentHeading, currentPitch);
                UpdateDragCubes();
            }
        }

        public override void OnIconCreate()
        {
            RotateTo(restHeading, restPitch);
        }

        public bool IsMultipleCubesActive { get { return true; } }

        public bool UsesProceduralDragCubes() { return false; } 

        public void AssumeDragCubePosition(string name)
        {
            // Used by the DragCubeSystem when it is generating drag cube entries on startup if none are found for this module.
            // When called the module should set it's model position/setup to match the expected position/orientation when 'name' drag cube is active. 
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
            return new string[] { "UP_FORWARD", "UP_SIDE", "FORWARD", "SIDE" };
        }

        protected void UpdateDragCubes()
        {
            float vertical = Mathf.Max(0, Mathf.Sin(currentPitch * Mathf.Deg2Rad));
            float horizontal = Mathf.Abs(Mathf.Cos(currentHeading * Mathf.Deg2Rad));
            part.DragCubes.SetCubeWeight("UP_FORWARD", vertical * horizontal);
            part.DragCubes.SetCubeWeight("UP_SIDE", vertical * (1 - horizontal));
            part.DragCubes.SetCubeWeight("FORWARD", (1 - vertical) * horizontal);
            part.DragCubes.SetCubeWeight("SIDE", (1 - vertical) * (1 - horizontal));
        }

        public void RotateTo(float heading, float pitch)
        {
            headingRotationTransform.localRotation = Quaternion.Euler(0, 0, heading);
            pitchRotationTransform.localRotation = Quaternion.Euler(-pitch, 0, 0);
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

        public void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                (float targetHeading, float targetPitch) = CalculateTracking();
                targetPitch += targetPitch < 0 ? 360 : 0;

                float heading = Mathf.SmoothDampAngle(currentHeading, targetHeading, ref currentHeadingVelocity, trackingSmoothTime, trackingMaxSpeed, TimeWarp.deltaTime);
                float pitch = Mathf.SmoothDampAngle(currentPitch, targetPitch, ref currentPitchVelocity, trackingSmoothTime, trackingMaxSpeed, TimeWarp.deltaTime);

                heading = (float.IsNaN(heading) ? 0 : heading);
                pitch = (float.IsNaN(pitch) ? 0 : pitch);

                (heading, pitch) = ApplyLimits((heading, pitch));

                RotateTo(heading, pitch);
                UpdateDragCubes();
            }
        }

        private static string noneStr = Localizer.Format("#autoLOC_6003083");

        public void SetTarget(Transform transform, string name)
        {
            targetTransform = transform;
            targetName = name ?? noneStr;
        }
    }
}
