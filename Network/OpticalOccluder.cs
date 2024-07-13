using CommNet;
using HarmonyLib;
using System;
using UnityEngine;

namespace LaserComm.Network
{
    public class OpticalOccluder
    {
        public CelestialBody body;
        public float partialOcclusionExtraRadius; // TODO read from config
        public DoubleCurve atmoOpaquenessCurve; // TODO read from config

        protected Transform transform;
        protected double radius;

        public OpticalOccluder(CelestialBody body)
        {
            this.body = body;
            this.transform = body.transform;
            this.radius = body.Radius;
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
            if (innerDiscriminant > 0)
            {
                var intersectDist1 = (float)(-halfB - Math.Sqrt(innerDiscriminant));
                var intersectDist2 = (float)(-halfB + Math.Sqrt(innerDiscriminant));

                if (intersectDist1 > 0 && intersectDist2 > 0)
                    return 1.0f; // full occlusion

                if (source.sqrMagnitude >= outerRSqr)
                    return 0.0f; // facing away outside atmo, full miss
            }
            else if (partialOcclusionExtraRadius == 0)
            {
                return 0.0f; // no atmosphere, full miss
            }

            double outerDiscriminant = halfB * halfB - source.sqrMagnitude + outerRSqr;
            if (outerDiscriminant <= 0)
                return 0.0f; // full miss

            var atmoIntersectDist1 = (float)(-halfB - Math.Sqrt(outerDiscriminant));
            var atmoIntersectDist2 = (float)(-halfB + Math.Sqrt(outerDiscriminant));

            var start = source + (atmoIntersectDist1 < 0 ? Vector3d.zero : ray * atmoIntersectDist1);
            var end = source + ray * atmoIntersectDist2;
            if (end.sqrMagnitude > dest.sqrMagnitude)
                end = dest;

            return RayMarchAtmosphere(ray, start, end);
        }

        protected float RayMarchAtmosphere(Vector3d direction, Vector3d start, Vector3d end)
        {
            // TODO
            return 0.0f;
        }
    }

    [HarmonyPatch(typeof(CommNetBody), "OnNetworkInitialized")]
    class CommNetBodyPatch
    {
        static void Postfix(ref CelestialBody ___body)
        {
            if (___body != null)
                LaserCommNetwork.Instance.Add(new OpticalOccluder(___body));
        }
    }
}
