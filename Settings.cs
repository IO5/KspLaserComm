using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace LaserComm
{
    public class Settings : GameParameters.CustomParameterNode
    {
        public override string Title { get { return "LaserComm"; } }

        public override string DisplaySection { get { return "LaserComm"; } }

        public override string Section { get { return "LaserComm"; } }

        public override int SectionOrder { get { return 0; } }

        public override bool HasPresets { get { return false; } }

        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }

        public static Settings Instance
        {
            get { return HighLogic.CurrentGame.Parameters.CustomParams<Settings>(); }
        }

        [GameParameters.CustomParameterUI("All ground stations have laser terminals", toolTip = "Set to enable all ground stations to communicate via laser link once the max level tracking station is built")]
        public bool allGroundStationsHaveLasers = false;
    }
}
