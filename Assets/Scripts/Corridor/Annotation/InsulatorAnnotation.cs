using System;
using UnityEngine;

namespace OTA.Corridor.Annotation
{
    [Serializable]
    public class InsulatorAnnotation
    {
        public string towerNo = "#17";
        public string phaseName = "A相绝缘子";
        public Vector3 crossarmEnd;
        public Vector3 conductorEnd;
        public float stringLength;
        public float swingAngle;
        public bool isConfirmed = false;

        public InsulatorAnnotation() { }

        public InsulatorAnnotation(string tower, string phase, Vector3 pCrossarm, Vector3 pConductor)
        {
            towerNo = tower;
            phaseName = phase;
            crossarmEnd = pCrossarm;
            conductorEnd = pConductor;

            stringLength = Vector3.Distance(pCrossarm, pConductor);
            Vector3 dir = (pConductor - pCrossarm).normalized;
            // Angle with vertical downward direction (0, -1, 0)
            swingAngle = Vector3.Angle(dir, Vector3.down);
        }
    }
}
