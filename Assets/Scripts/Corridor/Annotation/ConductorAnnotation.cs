using System;
using UnityEngine;

namespace OTA.Corridor.Annotation
{
    [Serializable]
    public class ConductorAnnotation
    {
        public string spanId = "#17-#18";
        public string phaseName = "A相";
        public Vector3 startHangingPoint;
        public Vector3 endHangingPoint;
        public Vector3 midLowestPoint;
        public float horizontalSpan;
        public float heightDifference;
        public float measuredSag;
        public float catenaryC;
        public float estimatedDiameterMm = 26.8f;
        public string conductorModel = "LGJ-400/35";
        public int inlierPointCount = 0;
        public bool isVisible = true;
        public Vector3[] curvePoints;
        public bool isConfirmed = false;

        public ConductorAnnotation() { }

        public ConductorAnnotation(string span, string phase, Vector3 start, Vector3 end, Vector3 mid, float sag, Vector3[] curve)
        {
            spanId = span;
            phaseName = phase;
            startHangingPoint = start;
            endHangingPoint = end;
            midLowestPoint = mid;
            measuredSag = sag;
            curvePoints = curve;

            float dx = end.x - start.x;
            float dz = end.z - start.z;
            horizontalSpan = Mathf.Sqrt(dx * dx + dz * dz);
            heightDifference = end.y - start.y;

            if (measuredSag > 0.01f && horizontalSpan > 1f)
            {
                catenaryC = (horizontalSpan * horizontalSpan) / (8f * measuredSag);
            }
            else
            {
                catenaryC = 1000f;
            }
        }
        public bool Raycast(Ray ray, float pickRadius, out float hitDist)
        {
            hitDist = float.MaxValue;
            if (!isVisible || curvePoints == null || curvePoints.Length < 2) return false;

            bool hit = false;
            for (int i = 0; i < curvePoints.Length - 1; i++)
            {
                Vector3 pA = curvePoints[i];
                Vector3 pB = curvePoints[i + 1];
                Vector3 v = pB - pA;
                float vLen2 = v.sqrMagnitude;
                if (vLen2 < 0.0001f) continue;

                // Distance between ray and segment pA->pB
                Vector3 w0 = pA - ray.origin;
                float a = 1.0f; // ray.direction is normalized
                float b = Vector3.Dot(ray.direction, v);
                float c = vLen2;
                float d = Vector3.Dot(ray.direction, w0);
                float e = Vector3.Dot(v, w0);
                float denom = a * c - b * b;

                float s = 0f;
                float t = 0f;
                if (denom > 0.0001f)
                {
                    s = Mathf.Clamp((b * d - a * e) / denom, 0f, 1f);
                    t = (b * s + d) / a;
                }
                else
                {
                    s = 0f;
                    t = d / a;
                }

                if (t > 0.1f)
                {
                    Vector3 ptOnRay = ray.origin + ray.direction * t;
                    Vector3 ptOnSeg = pA + v * s;
                    float dist = Vector3.Distance(ptOnRay, ptOnSeg);
                    if (dist <= pickRadius && t < hitDist)
                    {
                        hitDist = t;
                        hit = true;
                    }
                }
            }
            return hit;
        }
    }
}
