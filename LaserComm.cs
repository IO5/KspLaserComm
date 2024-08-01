using LaserComm.Network;
using System.Collections.Generic;
using UnityEngine;

namespace LaserComm
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class LaserComm : MonoBehaviour
    {
        public static LaserComm Instance;

        public Dictionary<string, OpticalOccluder> OpticalOccluders = new Dictionary<string, OpticalOccluder>();

        public void Awake()
        {
            Instance = this;

            foreach (var body in PSystemManager.Instance.localBodies)
                OpticalOccluders[body.name] = new OpticalOccluder(body);

            foreach (var config in GameDatabase.Instance.GetConfigNodes("OPAQUENESS_CURVE"))
            {
                string bodyName = "";
                if (!config.TryGetValue("body", ref bodyName))
                {
                    Debug.LogError("[LaserComm] missing attribute \"body\" for OPAQUENESS_CURVE");
                    continue;
                }

                if (OpticalOccluders.ContainsKey(bodyName))
                {
                    Debug.LogError($"[LaserComm] duplicate OPAQUENESS_CURVE for \"{bodyName}\"");
                    continue;
                }

                CelestialBody body = PSystemManager.Instance.localBodies.Find(b => b.name == bodyName);
                if (body == null)
                    Debug.LogWarning($"[LaserComm] unknown body \"{bodyName}\"");

                ConfigNode curveNode = config.GetNode("CURVE");
                if (curveNode == null)
                {
                    Debug.LogError($"[LaserComm] missing node \"CURVE\" for \"{bodyName}\" OPAQUENESS_CURVE");
                    continue;
                }

                var curve = ParseCurve(curveNode, bodyName);
                if (curve == null)
                    continue;

                if (OpticalOccluders.TryGetValue(bodyName, out var occluder))
                    occluder.SetOpaquenessCurve(curve);
            }
        }

        protected FloatCurve ParseCurve(ConfigNode curveNode, string body)
        {
            FloatCurve curve = new FloatCurve();

            foreach (var keyStr in curveNode.GetValues("key"))
            {
                Vector4 key;
                if (!ParseExtensions.TryParseVector4(keyStr, out key))
                {
                    Debug.LogError($"[LaserComm] invalid \"key\" for \"{body}\" OPAQUENESS_CURVE");
                    return null;
                }
                curve.Add(key[0], key[1], key[2], key[3]);
            }

            return curve;
        }
    }
}
