using CommNet;
using UnityEngine;
using System;
using System.Collections.Generic;
using CommNet.Occluders;
using HarmonyLib;
using LaserComm.Network;

namespace LaserComm
{
    public class LaserCommNetwork : CommNetwork
    {
        // TODO
        // optical occluders
        // laserhome
        // test science with commnet disabled
        public static LaserCommNetwork Instance;

        public Dictionary<CommNode, LaserCommNode> laserNodes = new Dictionary<CommNode, LaserCommNode>();
        public List<OpticalOccluder> opticalOccluders = new List<OpticalOccluder>();

        public static bool GroundStationsUnlocked
        {
            get
            {
                if (ScenarioUpgradeableFacilities.Instance == null)
                    return true;

                var maxLevel = ScenarioUpgradeableFacilities.GetFacilityLevelCount(SpaceCenterFacility.TrackingStation);
                return ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation) == maxLevel;
            }
        }

        public override CommNode Add(CommNode conn)
        {
            if (conn == null)
                return null;

            base.Add(conn);

            var laserNode = new LaserCommNode(conn);

            if (conn.isHome && GroundStationsUnlocked)
            {
                if (Settings.Instance.allGroundStationsHaveLasers || conn.name.EndsWith(": KSC"))
                    laserNode.laserRelayRange = double.PositiveInfinity;
            }

            laserNodes.Add(conn, laserNode);

            return conn;
        }

        public override bool Remove(CommNode conn)
        {
            if (conn != null)
                laserNodes.Remove(conn);
            return base.Remove(conn);
        }

        public void Add(OpticalOccluder occluder)
        {
            if (occluder != null)
                opticalOccluders.Add(occluder);
        }

        protected override bool SetNodeConnection(CommNode a, CommNode b)
        {
            if (a.isHome && b.isHome)
            {
                Disconnect(a, b);
                return false;
            }

            bool radioConnected = base.SetNodeConnection(a, b);

            var la = laserNodes[a];
            var lb = laserNodes[b];

            double distanceSqr = (a.precisePosition - b.precisePosition).sqrMagnitude;

            bool aCanRelay = (distanceSqr <= Math.Pow(la.laserRelayRange, 2) && distanceSqr <= Math.Pow(lb.laserRange, 2));
            bool bCanRelay = (distanceSqr <= Math.Pow(lb.laserRelayRange, 2) && distanceSqr <= Math.Pow(la.laserRange, 2));

            if (aCanRelay || bCanRelay)
            {
                double distance = Math.Sqrt(distanceSqr);
                float multipler = ApplyOpticalOcclusion(a.precisePosition, b.precisePosition, distance);
                if (multipler != 0)
                {
                    LaserConnect(la, lb, distance, aCanRelay, bCanRelay);
                    return true;
                }
            }

            return radioConnected;
        }

        protected virtual void LaserConnect(LaserCommNode la, LaserCommNode lb, double distance, bool aCanRelay, bool bCanRelay)
        {
            var a = la.node;
            var b = lb.node;

            // if there's an existing link replace it with a new, shiny one with laser metadata
            CommLink oldLink = null;
            if (a.TryGetValue(b, out oldLink))
                Disconnect(a, b);

            var link = new LaserCommLink();
            link.Set(a, b, distance);
            a.Add(b, link);
            b.Add(a, link);
            links.Add(link);

            double aRange = aCanRelay ? Math.Min(la.laserRelayRange, lb.laserRange) : 0;
            double bRange = bCanRelay ? Math.Min(lb.laserRelayRange, la.laserRange) : 0;

            link.strengthLaser = 1.0 - distance / Math.Max(aRange, bRange);
            link.strengthRelayALaser = 1.0 - distance / aRange;
            link.strengthRelayBLaser = 1.0 - distance / bRange;

            link.strengthRR = oldLink?.strengthRR ?? 0.0;
            link.strengthAR = oldLink?.strengthAR ?? 0.0;
            link.strengthBR = oldLink?.strengthBR ?? 0.0;
            link.aCanRelay = oldLink?.aCanRelay == true || aCanRelay;
            link.bCanRelay = oldLink?.bCanRelay == true || bCanRelay;
            link.bothRelay = oldLink?.bothRelay == true || (aCanRelay && bCanRelay);
        }

        protected float ApplyOpticalOcclusion(Vector3d aPos, Vector3d bPos, double distance)
        {
            float multipler = 1.0f;
            foreach (var occluder in opticalOccluders)
            {
                if (!occluder.InRange(aPos, distance))
                    continue;

                multipler *= occluder.Raycast(aPos, bPos);
                if (multipler == 0.0f)
                    break;
            }

            return multipler;
        }

        static public LaserCommLink GetLaserLink(CommLink link)
        {
            if (link == null)
                return null;

            if (link is LaserCommLink laserLink)
                return laserLink;

            return Instance.Links.Find(l => l.a == link.a || l.b == link.a) as LaserCommLink;
        }
    }

    [HarmonyPatch(typeof(CommNetNetwork), "ResetNetwork")]
    class CommNetNetworkPatch
    {
        static bool Prefix(ref CommNetwork ___commNet)
        {
            ___commNet = LaserCommNetwork.Instance = new LaserCommNetwork();
            GameEvents.CommNet.OnNetworkInitialized.Fire();
            return false;
        }
    }
}
