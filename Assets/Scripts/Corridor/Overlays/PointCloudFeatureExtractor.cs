using System;
using System.Collections.Generic;
using PointCloudRuntimeViewer;
using UnityEngine;

namespace OTA.Corridor.Overlays
{
    public struct ExtractedTowerInfo
    {
        public string id;
        public string name;
        public Vector3 basePosition;
        public Vector3 topPosition;
        public float height;
        public int pointCount;
    }

    public struct ExtractedWireSpanInfo
    {
        public string name;
        public Vector3 startAttachment;
        public Vector3 endAttachment;
        public float spanLength;
        public float measuredSag;
        public int pointCount;
    }

    public static class PointCloudFeatureExtractor
    {
        public static bool ExtractCorridorElements(
            RuntimeViewerDX11 viewer,
            byte[] classes,
            out List<ExtractedTowerInfo> towers,
            out List<ExtractedWireSpanInfo> wires)
        {
            towers = new List<ExtractedTowerInfo>();
            wires = new List<ExtractedWireSpanInfo>();

            if (viewer == null || viewer.points == null || classes == null || viewer.points.Length != classes.Length)
            {
                return false;
            }

            var pts = viewer.points;
            int n = pts.Length;

            var towerPts = new List<Vector3>();
            var wirePts = new List<Vector3>();

            for (int i = 0; i < n; i++)
            {
                byte cls = classes[i];
                if (cls == 15) towerPts.Add(pts[i]);
                else if (cls == 14) wirePts.Add(pts[i]);
            }

            if (towerPts.Count > 50)
            {
                Vector3 minT = towerPts[0], maxT = towerPts[0];
                for (int i = 1; i < towerPts.Count; i++)
                {
                    minT = Vector3.Min(minT, towerPts[i]);
                    maxT = Vector3.Max(maxT, towerPts[i]);
                }

                Vector3 extent = maxT - minT;
                bool splitAlongX = extent.x >= extent.z;
                float midSplit = splitAlongX ? (minT.x + maxT.x) * 0.5f : (minT.z + maxT.z) * 0.5f;

                var t1Cluster = new List<Vector3>();
                var t2Cluster = new List<Vector3>();

                foreach (var p in towerPts)
                {
                    float coord = splitAlongX ? p.x : p.z;
                    if (coord < midSplit) t1Cluster.Add(p);
                    else t2Cluster.Add(p);
                }

                if (t1Cluster.Count > 10) towers.Add(BuildTowerInfo(t1Cluster, "#17"));
                if (t2Cluster.Count > 10) towers.Add(BuildTowerInfo(t2Cluster, "#18"));
            }

            if (wirePts.Count > 50 && towers.Count >= 2)
            {
                var t1 = towers[0];
                var t2 = towers[1];

                float minY = float.MaxValue;
                foreach (var wp in wirePts)
                {
                    if (wp.y < minY) minY = wp.y;
                }

                float chordY = (t1.topPosition.y + t2.topPosition.y) * 0.5f;
                float measuredSag = Mathf.Max(1.0f, chordY - minY);
                float spanLen = Vector3.Distance(t1.topPosition, t2.topPosition);

                wires.Add(new ExtractedWireSpanInfo
                {
                    name = "实测激光导线簇 (Catenary)",
                    startAttachment = t1.topPosition - Vector3.up * 2f,
                    endAttachment = t2.topPosition - Vector3.up * 2f,
                    spanLength = spanLen,
                    measuredSag = measuredSag,
                    pointCount = wirePts.Count
                });
            }

            return towers.Count > 0 || wires.Count > 0;
        }

        private static ExtractedTowerInfo BuildTowerInfo(List<Vector3> cluster, string defaultId)
        {
            Vector3 min = cluster[0], max = cluster[0];
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < cluster.Count; i++)
            {
                var p = cluster[i];
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
                sum += p;
            }

            Vector3 center = sum / cluster.Count;
            float height = max.y - min.y;

            return new ExtractedTowerInfo
            {
                id = defaultId,
                name = defaultId + " 真实激光铁塔",
                basePosition = new Vector3(center.x, min.y, center.z),
                topPosition = new Vector3(center.x, max.y, center.z),
                height = height,
                pointCount = cluster.Count
            };
        }
    }
}