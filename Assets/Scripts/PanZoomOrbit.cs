using System.Collections;
using System.Collections.Generic;
using PointCloudRuntimeViewer;
using UnityEngine;

namespace Metervara.Interaction
{
    [RequireComponent(typeof(Camera))]
    public class PanZoomOrbit : MonoBehaviour
    {
        public bool pan = true;
        public bool zoom = true;
        public bool orbit = true;

        // Cloud reference for ray hits
        public RuntimeViewerDX11 viewer;

        protected Camera _camera;
        protected Camera Camera
        {
            get
            {
                if (!_camera) _camera = GetComponent<Camera>();
                return _camera;
            }
        }

        public static bool BlockCameraInput = false;

        protected virtual void Awake()
        {
            if (viewer == null)
            {
                viewer = FindObjectOfType<RuntimeViewerDX11>();
            }
        }

        protected Vector3 sphereCenter = Vector3.zero;
        protected float sphereRadius = 0f;
        protected Bounds cloudBounds;
        protected bool boundsReady = false;
        protected int lastPointCount = -1;

        public void InvalidateBounds()
        {
            boundsReady = false;
        }

        public void UpdateBounds()
        {
            if (viewer == null || viewer.points == null || viewer.points.Length == 0) return;
            var pts = viewer.points;
            lastPointCount = pts.Length;

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            int stride = Mathf.Max(1, pts.Length / 100000);
            for (int i = 0; i < pts.Length; i += stride)
            {
                Vector3 p = pts[i];
                if (p.x < min.x) min.x = p.x;
                if (p.y < min.y) min.y = p.y;
                if (p.z < min.z) min.z = p.z;
                if (p.x > max.x) max.x = p.x;
                if (p.y > max.y) max.y = p.y;
                if (p.z > max.z) max.z = p.z;
            }
            cloudBounds = new Bounds((min + max) * 0.5f, max - min);
            sphereCenter = cloudBounds.center;
            sphereRadius = cloudBounds.extents.magnitude;
            boundsReady = true;
        }

        /// <summary>
        /// Precision raycast into point cloud:
        /// Uses coarse bounding-sphere check then searches points minimizing along-ray depth + axis perpendicular distance.
        /// </summary>
        public bool HitCloud(Ray ray, out Vector3 hit, out float distance)
        {
            hit = Vector3.zero;
            distance = 0f;

            int curCount = (viewer != null && viewer.points != null) ? viewer.points.Length : 0;
            if (!boundsReady || curCount != lastPointCount)
            {
                if (viewer != null && curCount > 0) UpdateBounds();
            }

            // 1. Precise Point Cloud Raycast (with dynamic bounding sphere coarse cull)
            if (boundsReady && sphereRadius > 0.01f)
            {
                Vector3 oc = ray.origin - sphereCenter;
                float b = Vector3.Dot(oc, ray.direction);
                float c = Vector3.Dot(oc, oc) - sphereRadius * sphereRadius;
                float disc = b * b - c;
                if (disc >= 0f)
                {
                    float t = -b - Mathf.Sqrt(disc);
                    if (t < 0f) t = -b + Mathf.Sqrt(disc);
                    if (t >= 0f)
                    {
                        Vector3 entry = ray.origin + ray.direction * t;
                        var pts = viewer.points;
                        float bestScore = float.MaxValue;
                        bool found = false;

                        if (pts != null && pts.Length > 0)
                        {
                            float camDist = Vector3.Distance(ray.origin, entry);
                            float maxPerp = Mathf.Max(0.4f, camDist * 0.003f);
                            float maxPerp2 = maxPerp * maxPerp;
                            int stride = Mathf.Max(1, pts.Length / 200000);
                            for (int i = 0; i < pts.Length; i += stride)
                            {
                                Vector3 toP = pts[i] - ray.origin;
                                float along = Vector3.Dot(toP, ray.direction);
                                if (along < 0f) continue;
                                float perp2 = toP.sqrMagnitude - along * along;
                                if (perp2 > maxPerp2) continue;

                                float perp = Mathf.Sqrt(perp2);
                                float score = along + perp * 8.0f;
                                if (score < bestScore)
                                {
                                    bestScore = score;
                                    hit = pts[i];
                                    distance = along;
                                    found = true;
                                }
                            }
                        }
                        if (found) return true;
                    }
                }
            }

            // 2. Coarse Bounding Box Raycast Fallback
            if (boundsReady && cloudBounds.size.sqrMagnitude > 0.01f)
            {
                if (cloudBounds.IntersectRay(ray, out float boxDist))
                {
                    hit = ray.origin + ray.direction * boxDist;
                    distance = boxDist;
                    return true;
                }
            }

            // 3. Fallback: Horizon plane (Y = 0)
            if (Mathf.Abs(ray.direction.y) > 0.001f)
            {
                float planeDist = -ray.origin.y / ray.direction.y;
                if (planeDist > 0f && planeDist < 5000f)
                {
                    hit = ray.origin + ray.direction * planeDist;
                    distance = planeDist;
                    return true;
                }
            }

            return false;
        }

        public void Pan(Vector3 fromScreenPosition, Vector3 toScreenPosition, float focalDistance)
        {
            if (!pan) return;
            if (focalDistance < 1f || float.IsNaN(focalDistance) || float.IsInfinity(focalDistance))
            {
                focalDistance = 100f;
            }

            Ray rayFrom = Camera.ScreenPointToRay(fromScreenPosition);
            Ray rayTo = Camera.ScreenPointToRay(toScreenPosition);

            Vector3 planePoint = Camera.transform.position + Camera.transform.forward * focalDistance;
            Plane panPlane = new Plane(-Camera.transform.forward, planePoint);

            if (panPlane.Raycast(rayFrom, out float dFrom) && panPlane.Raycast(rayTo, out float dTo))
            {
                Vector3 pFrom = rayFrom.GetPoint(dFrom);
                Vector3 pTo = rayTo.GetPoint(dTo);
                Vector3 move = pFrom - pTo;

                move -= Vector3.Project(move, Camera.transform.forward);
                transform.position += move;
            }
        }

        public void Pan(Vector3 fromScreenPosition, Vector3 toScreenPosition)
        {
            Pan(fromScreenPosition, toScreenPosition, 100f);
        }

        public void Zoom(float distance, float focalDistance = 100f)
        {
            if (!zoom) return;
            if (focalDistance < 1f || float.IsNaN(focalDistance) || float.IsInfinity(focalDistance))
            {
                focalDistance = 100f;
            }

            Ray ray = Camera.ScreenPointToRay(Input.mousePosition);
            Vector3 planePoint = Camera.transform.position + Camera.transform.forward * focalDistance;
            Plane panPlane = new Plane(-Camera.transform.forward, planePoint);

            Vector3 hit;
            if (panPlane.Raycast(ray, out float enter) && enter > 0.01f)
            {
                hit = ray.GetPoint(enter);
            }
            else
            {
                hit = planePoint;
            }

            Vector3 toHit = hit - transform.position;
            if (toHit.magnitude > distance || distance < 0)
            {
                transform.position += toHit.normalized * distance;
            }
        }

        public void ZoomMultiplyDistance(Vector3 mousePosition, float factor, float focalDistance = 100f)
        {
            if (!zoom) return;
            if (focalDistance < 1f || float.IsNaN(focalDistance) || float.IsInfinity(focalDistance))
            {
                focalDistance = 100f;
            }

            Ray ray = Camera.ScreenPointToRay(mousePosition);
            Vector3 planePoint = Camera.transform.position + Camera.transform.forward * focalDistance;
            Plane panPlane = new Plane(-Camera.transform.forward, planePoint);

            Vector3 hit;
            if (panPlane.Raycast(ray, out float enter) && enter > 0.01f)
            {
                hit = ray.GetPoint(enter);
            }
            else
            {
                hit = planePoint;
            }

            Vector3 targetPosition = hit + (transform.position - hit) * factor;
            float d = Vector3.Distance(targetPosition, hit);
            if (d > 0.5f && d < 30000f)
            {
                transform.position = targetPosition;
            }
        }
    }
}
