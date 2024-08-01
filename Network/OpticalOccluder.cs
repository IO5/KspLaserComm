using CommNet;
using HarmonyLib;
using Smooth.Slinq.Test;
using System;
using UnityEngine;

namespace LaserComm.Network
{
    public class OpticalOccluder
    {
        public string body;
        public float partialOcclusionExtraRadius { get; private set; }
        public FloatCurve atmoOpaquenessCurve { get; private set; }

        protected Transform transform;
        protected double radius;

        public OpticalOccluder(CelestialBody body)
        {
            this.body = body.name;
            this.transform = body.transform;
            this.radius = body.Radius;
        }

        public void SetOpaquenessCurve(FloatCurve curve)
        {
            atmoOpaquenessCurve = curve;
            partialOcclusionExtraRadius = curve.maxTime;
        }

        public bool InRange(Vector3d source, double distance)
        {
            distance += radius + partialOcclusionExtraRadius;
            return (source - transform.position).sqrMagnitude <= distance * distance;
        }

        public float Raycast(Vector3d source, Vector3d dest)
        {
            source -= transform.position;
            dest -= transform.position;

            if (source.sqrMagnitude > dest.sqrMagnitude)
                (source, dest) = (dest, source);

            var ray = (dest - source).normalized;
            double halfB = Vector3d.Dot(source, ray);

            double innerRSqr = radius * radius;
            var outerRSqr = radius + partialOcclusionExtraRadius;
            outerRSqr *= outerRSqr;

            double innerDiscriminant = halfB * halfB - source.sqrMagnitude + innerRSqr;
            if (innerDiscriminant <= 0)
            {
                if (partialOcclusionExtraRadius == 0)
                    return 1.0f; // no atmosphere, full miss
            }
            else
            {
                var intersectDist1 = (float)(-halfB - Math.Sqrt(innerDiscriminant));
                var intersectDist2 = (float)(-halfB + Math.Sqrt(innerDiscriminant));

                if (intersectDist1 > 0 && intersectDist2 > 0)
                    return 0.0f; // full occlusion

                // facing away

                if (source.sqrMagnitude >= outerRSqr)
                    return 1.0f; // facing away outside atmo, full miss
            }

            double outerDiscriminant = halfB * halfB - source.sqrMagnitude + outerRSqr;
            if (outerDiscriminant <= 0)
                return 1.0f; // full miss

            var atmoIntersectDist1 = (float)(-halfB - Math.Sqrt(outerDiscriminant));
            var atmoIntersectDist2 = (float)(-halfB + Math.Sqrt(outerDiscriminant));

            var start = source + (atmoIntersectDist1 < 0 ? Vector3d.zero : ray * atmoIntersectDist1);
            var end = (ray.sqrMagnitude < atmoIntersectDist2 * atmoIntersectDist2 ? dest : source + ray * atmoIntersectDist2);

            return RayMarchAtmosphere(start, end);
        }

        internal static int NumSteps = 10;
        protected float RayMarchAtmosphere(Vector3d start, Vector3d end)
        {
            var light = 1.0f;
            var step = (end - start) / (NumSteps - 1);
            var stepSize = (float)step.magnitude;

            for (int i = 0; i < NumSteps; ++i)
            {
                var altitude = (start + i * step).magnitude - radius;

                var lightScatteredPer1km = atmoOpaquenessCurve.Evaluate(Mathf.Max(0, (float)altitude));
                light *= Mathf.Exp(stepSize / 1000f * Mathf.Log(1 - lightScatteredPer1km));
            }

            return light;
        }
    }

    [HarmonyPatch(typeof(CommNetBody), "OnNetworkInitialized")]
    class CommNetBodyPatch
    {
        static void Postfix(ref CelestialBody ___body)
        {
            if (___body != null && LaserCommNetwork.Instance != null)
            {
                if (LaserComm.Instance.OpticalOccluders.TryGetValue(___body.name, out var occluder))
                    LaserCommNetwork.Instance.Add(occluder);
            }
        }
    }
}
