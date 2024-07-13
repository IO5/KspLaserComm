using CommNet;
using UnityEngine;
using System;
using System.Collections.Generic;
using CommNet.Occluders;
using HarmonyLib;

namespace LaserComm
{
    public class LaserCommNetwork : CommNetwork
    {
        // TODO
        // optical occluders
        // laserhome
        // test science with commnet disabled
        public Dictionary<CommNode, LaserCommNode> laserNodes = new Dictionary<CommNode, LaserCommNode>();

        public static LaserCommNetwork Instance;

        public override CommNode Add(CommNode conn)
        {
            if (conn == null)
                return null;

            base.Add(conn);

            var laserNode = new LaserCommNode(conn);
            // TODO only for some
            if (conn.isHome)
            {
                laserNode.laserRange = double.PositiveInfinity; // TODO delet
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

        protected override bool SetNodeConnection(CommNode a, CommNode b)
        {
            if (a.isHome && b.isHome)
            {
                Disconnect(a, b);
                return false;
            }

            base.SetNodeConnection(a, b);

            var la = laserNodes[a];
            var lb = laserNodes[b];

            double distanceSqr = (a.precisePosition - b.precisePosition).sqrMagnitude;

            bool aCanRelay = (distanceSqr <= Math.Pow(la.laserRelayRange, 2) && distanceSqr <= Math.Pow(lb.laserRange, 2));
            bool bCanRelay = (distanceSqr <= Math.Pow(lb.laserRelayRange, 2) && distanceSqr <= Math.Pow(la.laserRange, 2));

            if (aCanRelay || bCanRelay)
            {
                double distance = Math.Sqrt(distanceSqr);
                // TODO optical occluders
                if (TestOpticalOcclusion(a.precisePosition, b.precisePosition, distance))
                {
                    // TODO signal strength multipler
                    // opaque atmo
                    LaserConnect(la, lb, distance, aCanRelay, bCanRelay);
                    return true;
                }
            }

            Disconnect(a, b);
            return false;
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
            link.aCanRelay = a.antennaRelay.power > 0.0 || aCanRelay;
            link.bCanRelay = b.antennaRelay.power > 0.0 || bCanRelay;
            link.bothRelay = link.aCanRelay && link.bCanRelay;
        }

        protected bool TestOpticalOcclusion(Vector3d aPos, Vector3d bPos, double distance)
        {
            //TODO
            //foreach (var occluder in occluders)
            //{
            //    if (!occluder.InRange(aPos, distance))
            //        continue;

            //    if (!occluder.Raycast(aPos, bPos))
            //        continue;

            //    return false;
            //}
            return true;
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
