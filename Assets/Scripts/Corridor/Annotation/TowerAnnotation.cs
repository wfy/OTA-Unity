using System;
using UnityEngine;

namespace OTA.Corridor.Annotation
{
    [Serializable]
    public class TowerAnnotation
    {
        public string towerNo = "#1";
        public string towerType = "220kV标准塔";
        public Vector3 topPosition;
        public Vector3 basePosition;
        public float totalHeight = 35f;
        public float nominalHeight = 26f;
        public Bounds topBox;
        public Bounds bodyBox;
        public float yawAngle = 0f;
        public bool isConfirmed = false;
        public bool isVisible = true;

        public TowerAnnotation() { }

        public TowerAnnotation(string no, Vector3 top, Vector3 bBase, float yaw = 0f)
        {
            towerNo = no;
            topPosition = top;
            basePosition = bBase;
            totalHeight = Mathf.Max(5f, top.y - bBase.y);
            nominalHeight = totalHeight * 0.75f;
            yawAngle = yaw;
            RecalculateBoxes();
        }

        public void RecalculateBoxes(float headWidth = 20f, float headDepth = 6f, float bodyWidth = 8f, float bodyDepth = 8f, float topMargin = 1.2f, float bottomMargin = 1.2f)
        {
            // 上方包围盒顶面略高于杆塔顶点 topMargin (1.2m)，覆盖避雷针与顶尖金具
            // 下方包围盒底面略低于地面高度 bottomMargin (1.2m)，覆盖塔脚、基础短柱与接地坡度
            float headHeight = totalHeight - nominalHeight + topMargin;
            float bodyHeight = nominalHeight + bottomMargin;

            Vector3 bodyCenter = new Vector3(topPosition.x, basePosition.y - bottomMargin + bodyHeight * 0.5f, topPosition.z);
            Vector3 headCenter = new Vector3(topPosition.x, basePosition.y + nominalHeight + headHeight * 0.5f, topPosition.z);

            bodyBox = new Bounds(bodyCenter, new Vector3(bodyWidth, bodyHeight, bodyDepth));
            topBox = new Bounds(headCenter, new Vector3(headWidth, headHeight, headDepth));
        }

        public bool ContainsPoint(Vector3 p)
        {
            float rad = yawAngle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            float dx = p.x - topPosition.x;
            float dz = p.z - topPosition.z;

            float rx = dx * cos - dz * sin;
            float rz = dx * sin + dz * cos;

            Vector3 localP = new Vector3(topPosition.x + rx, p.y, topPosition.z + rz);
            return BoxContains(topBox, localP) || BoxContains(bodyBox, localP);
        }

        public bool Raycast(Ray ray, out float hitDist)
        {
            hitDist = float.MaxValue;
            if (!isVisible) return false;

            // Pure trigonometric inverse rotation around topPosition (matches Unity Euler(0, yaw, 0))
            float rad = yawAngle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            float dox = ray.origin.x - topPosition.x;
            float doz = ray.origin.z - topPosition.z;
            float rox = dox * cos - doz * sin + topPosition.x;
            float roz = dox * sin + doz * cos + topPosition.z;
            Vector3 localOrigin = new Vector3(rox, ray.origin.y, roz);

            float rdx = ray.direction.x * cos - ray.direction.z * sin;
            float rdz = ray.direction.x * sin + ray.direction.z * cos;
            Vector3 localDir = new Vector3(rdx, ray.direction.y, rdz);

            Ray localRay = new Ray(localOrigin, localDir);

            bool hit = false;
            if (IntersectAABB(topBox, localRay, out float distTop))
            {
                if (distTop < hitDist) { hitDist = distTop; hit = true; }
            }
            if (IntersectAABB(bodyBox, localRay, out float distBody))
            {
                if (distBody < hitDist) { hitDist = distBody; hit = true; }
            }
            return hit;
        }

        public static bool IntersectAABB(Bounds b, Ray ray, out float dist)
        {
            dist = 0f;
            Vector3 min = b.min;
            Vector3 max = b.max;

            float tmin = float.MinValue;
            float tmax = float.MaxValue;

            // X slab
            if (Mathf.Abs(ray.direction.x) > 1e-7f)
            {
                float invD = 1f / ray.direction.x;
                float t0 = (min.x - ray.origin.x) * invD;
                float t1 = (max.x - ray.origin.x) * invD;
                if (t0 > t1) { float tmp = t0; t0 = t1; t1 = tmp; }
                tmin = Mathf.Max(tmin, t0);
                tmax = Mathf.Min(tmax, t1);
                if (tmin > tmax) return false;
            }
            else if (ray.origin.x < min.x || ray.origin.x > max.x) return false;

            // Y slab
            if (Mathf.Abs(ray.direction.y) > 1e-7f)
            {
                float invD = 1f / ray.direction.y;
                float t0 = (min.y - ray.origin.y) * invD;
                float t1 = (max.y - ray.origin.y) * invD;
                if (t0 > t1) { float tmp = t0; t0 = t1; t1 = tmp; }
                tmin = Mathf.Max(tmin, t0);
                tmax = Mathf.Min(tmax, t1);
                if (tmin > tmax) return false;
            }
            else if (ray.origin.y < min.y || ray.origin.y > max.y) return false;

            // Z slab
            if (Mathf.Abs(ray.direction.z) > 1e-7f)
            {
                float invD = 1f / ray.direction.z;
                float t0 = (min.z - ray.origin.z) * invD;
                float t1 = (max.z - ray.origin.z) * invD;
                if (t0 > t1) { float tmp = t0; t0 = t1; t1 = tmp; }
                tmin = Mathf.Max(tmin, t0);
                tmax = Mathf.Min(tmax, t1);
                if (tmin > tmax) return false;
            }
            else if (ray.origin.z < min.z || ray.origin.z > max.z) return false;

            dist = tmin > 0f ? tmin : tmax;
            return dist >= 0f;
        }

        public static bool BoxContains(Bounds b, Vector3 p)
        {
            Vector3 min = b.min;
            Vector3 max = b.max;
            return p.x >= min.x && p.x <= max.x &&
                   p.y >= min.y && p.y <= max.y &&
                   p.z >= min.z && p.z <= max.z;
        }
    }
}
