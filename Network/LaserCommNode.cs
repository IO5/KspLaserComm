using CommNet;
using UnityEngine;

namespace LaserComm
{
    public class LaserCommNode
    {
        public CommNode node;
        public double laserRange = 0.0;
        public double laserRelayRange = 0.0;

        public LaserCommNode(CommNode node)
        {
            this.node = node;
        }
    }
}
