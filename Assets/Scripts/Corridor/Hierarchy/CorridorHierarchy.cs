using System;
using System.Collections.Generic;
using System.IO;
using OTA.Corridor.Preprocess;
using UnityEngine;

namespace OTA.Corridor.Hierarchy
{
    public enum HierarchyNodeType
    {
        Province = 0,
        City = 1,
        Line = 2,
        Segment = 3
    }

    [Serializable]
    public class CorridorSegmentData
    {
        public string id;
        public string name;
        public string province;
        public string city;
        public string lineName;
        public string startTower;
        public string endTower;
        public float startMileage;
        public float endMileage;
        public float length;
        public int pointCount;
        public string lasPath;
        public Bounds bounds;
        public bool isLoaded;
        public bool isVisible = true;
    }

    [Serializable]
    public class HierarchyNode
    {
        public string id;
        public string name;
        public HierarchyNodeType type;
        public double lat;
        public double lon;
        public float alt;
        public bool isExpanded = true;
        public bool isVisible = true;
        public List<HierarchyNode> children = new List<HierarchyNode>();
        public CorridorSegmentData segmentData = null;

        public HierarchyNode(string id, string name, HierarchyNodeType type, float alt = 1000f)
        {
            this.id = id;
            this.name = name;
            this.type = type;
            this.alt = alt;
        }
    }

    public static class CorridorHierarchyManager
    {
        private static List<HierarchyNode> rootNodes = null;

        public static List<HierarchyNode> GetOrBuildDefaultHierarchy(string pointCloudRoot = null)
        {
            if (rootNodes == null)
            {
                rootNodes = new List<HierarchyNode>();
            }
            return rootNodes;
        }

        public static void ClearHierarchy()
        {
            if (rootNodes == null) rootNodes = new List<HierarchyNode>();
            else rootNodes.Clear();
        }

        public static CorridorSegmentData FindSegmentById(string id)
        {
            if (rootNodes == null) GetOrBuildDefaultHierarchy();
            return TraverseFindSegment(rootNodes, id);
        }

        private static CorridorSegmentData TraverseFindSegment(List<HierarchyNode> nodes, string id)
        {
            if (nodes == null) return null;
            foreach (var n in nodes)
            {
                if (n.type == HierarchyNodeType.Segment && n.segmentData != null && n.segmentData.id == id)
                {
                    return n.segmentData;
                }
                var found = TraverseFindSegment(n.children, id);
                if (found != null) return found;
            }
            return null;
        }

        public static CorridorSegmentData AddOrUpdateSegment(
            string province,
            string city,
            string lineName,
            string segmentName,
            string lasPath,
            Bounds bounds,
            int pointCount,
            double lat = 0,
            double lon = 0,
            string startTower = "",
            string endTower = "",
            float length = 350f)
        {
            if (rootNodes == null) GetOrBuildDefaultHierarchy();

            if (string.IsNullOrEmpty(province)) province = "浙江省";
            if (string.IsNullOrEmpty(city)) city = "杭州市";
            if (string.IsNullOrEmpty(lineName)) lineName = "220kV 示范线";
            if (string.IsNullOrEmpty(segmentName)) segmentName = "#01 - #02 档距";

            // 1. Province Node
            HierarchyNode provNode = rootNodes.Find(p => p.name == province || p.name.Contains(province) || province.Contains(p.name));
            if (provNode == null)
            {
                provNode = new HierarchyNode("prov-" + Guid.NewGuid().ToString().Substring(0, 8), province, HierarchyNodeType.Province, 12000f);
                provNode.lat = lat;
                provNode.lon = lon;
                provNode.isExpanded = true;
                rootNodes.Add(provNode);
            }
            else
            {
                provNode.isExpanded = true;
            }

            // 2. City Node
            HierarchyNode cityNode = provNode.children.Find(c => c.name == city || c.name.Contains(city) || city.Contains(c.name));
            if (cityNode == null)
            {
                cityNode = new HierarchyNode("city-" + Guid.NewGuid().ToString().Substring(0, 8), city, HierarchyNodeType.City, 4000f);
                cityNode.lat = lat;
                cityNode.lon = lon;
                cityNode.isExpanded = true;
                provNode.children.Add(cityNode);
            }
            else
            {
                cityNode.isExpanded = true;
            }

            // 3. Line Node
            HierarchyNode lineNode = cityNode.children.Find(l => l.name == lineName || l.name.Contains(lineName) || lineName.Contains(l.name));
            if (lineNode == null)
            {
                lineNode = new HierarchyNode("line-" + Guid.NewGuid().ToString().Substring(0, 8), lineName, HierarchyNodeType.Line, 1200f);
                lineNode.isExpanded = true;
                cityNode.children.Add(lineNode);
            }
            else
            {
                lineNode.isExpanded = true;
            }

            // 4. Segment Node
            HierarchyNode segNode = lineNode.children.Find(s => s.name == segmentName || (s.segmentData != null && s.segmentData.lasPath == lasPath));
            if (segNode == null)
            {
                string segId = "seg-" + Guid.NewGuid().ToString().Substring(0, 8);
                var segData = new CorridorSegmentData
                {
                    id = segId,
                    name = segmentName,
                    province = province,
                    city = city,
                    lineName = lineName,
                    startTower = !string.IsNullOrEmpty(startTower) ? startTower : "#1 杆塔",
                    endTower = !string.IsNullOrEmpty(endTower) ? endTower : "#2 杆塔",
                    startMileage = 0f,
                    endMileage = length,
                    length = length,
                    pointCount = pointCount,
                    lasPath = lasPath.Replace('\\', '/'),
                    bounds = bounds,
                    isLoaded = true,
                    isVisible = true
                };

                segNode = new HierarchyNode(segId, segmentName, HierarchyNodeType.Segment, 250f);
                segNode.segmentData = segData;
                lineNode.children.Add(segNode);
                return segData;
            }
            else
            {
                if (segNode.segmentData == null)
                {
                    segNode.segmentData = new CorridorSegmentData();
                }
                segNode.name = segmentName;
                segNode.segmentData.name = segmentName;
                segNode.segmentData.province = province;
                segNode.segmentData.city = city;
                segNode.segmentData.lineName = lineName;
                segNode.segmentData.lasPath = lasPath.Replace('\\', '/');
                segNode.segmentData.bounds = bounds;
                segNode.segmentData.pointCount = pointCount;
                if (!string.IsNullOrEmpty(startTower)) segNode.segmentData.startTower = startTower;
                if (!string.IsNullOrEmpty(endTower)) segNode.segmentData.endTower = endTower;
                segNode.segmentData.length = length;
                segNode.segmentData.isLoaded = true;
                return segNode.segmentData;
            }
        }

        /// <summary>
        /// Synchronizes marked tower spans directly into the line's hierarchy segment nodes in real time.
        /// </summary>
        public static void SyncMarkedCorridorSegments(
            string province,
            string city,
            string lineName,
            List<TowerMarker> towers,
            string outputDir = null)
        {
            if (rootNodes == null) GetOrBuildDefaultHierarchy();

            if (string.IsNullOrEmpty(province)) province = "浙江省";
            if (string.IsNullOrEmpty(city)) city = "杭州市";
            if (string.IsNullOrEmpty(lineName)) lineName = "220kV 示范线";

            // 1. Find or create Province Node
            HierarchyNode provNode = rootNodes.Find(p => p.name == province || p.name.Contains(province) || province.Contains(p.name));
            if (provNode == null)
            {
                provNode = new HierarchyNode("prov-" + Guid.NewGuid().ToString().Substring(0, 8), province, HierarchyNodeType.Province, 12000f);
                if (towers != null && towers.Count > 0) { provNode.lat = towers[0].lat; provNode.lon = towers[0].lon; }
                provNode.isExpanded = true;
                rootNodes.Add(provNode);
            }
            else { provNode.isExpanded = true; }

            // 2. Find or create City Node
            HierarchyNode cityNode = provNode.children.Find(c => c.name == city || c.name.Contains(city) || city.Contains(c.name));
            if (cityNode == null)
            {
                cityNode = new HierarchyNode("city-" + Guid.NewGuid().ToString().Substring(0, 8), city, HierarchyNodeType.City, 4000f);
                if (towers != null && towers.Count > 0) { cityNode.lat = towers[0].lat; cityNode.lon = towers[0].lon; }
                cityNode.isExpanded = true;
                provNode.children.Add(cityNode);
            }
            else { cityNode.isExpanded = true; }

            // 3. Find or create Line Node
            HierarchyNode lineNode = cityNode.children.Find(l => l.name == lineName || l.name.Contains(lineName) || lineName.Contains(l.name));
            if (lineNode == null)
            {
                lineNode = new HierarchyNode("line-" + Guid.NewGuid().ToString().Substring(0, 8), lineName, HierarchyNodeType.Line, 1200f);
                lineNode.isExpanded = true;
                cityNode.children.Add(lineNode);
            }
            else { lineNode.isExpanded = true; }

            if (towers == null || towers.Count < 2)
            {
                // Remove temporary uncompleted segments for this line
                lineNode.children.RemoveAll(s => s.segmentData != null && !s.segmentData.isLoaded && !File.Exists(s.segmentData.lasPath));
                return;
            }

            int numSpans = towers.Count - 1;
            var currentSpanNames = new HashSet<string>();

            for (int i = 0; i < numSpans; i++)
            {
                var ta = towers[i];
                var tb = towers[i + 1];
                string segName = string.Format("{0} - {1} 档距", ta.name, tb.name);
                currentSpanNames.Add(segName);

                float spanLen = (float)Math.Round(Math.Sqrt(
                    Math.Pow(tb.easting - ta.easting, 2) +
                    Math.Pow(tb.northing - ta.northing, 2)
                ));

                string outPath = !string.IsNullOrEmpty(outputDir)
                    ? Path.Combine(outputDir, string.Format("{0}_{1}.las", lineName.Replace(" ", "_"), segName.Replace(" ", "_")))
                    : "";

                HierarchyNode segNode = lineNode.children.Find(s => s.name == segName || (s.segmentData != null && s.segmentData.startTower == ta.name && s.segmentData.endTower == tb.name));
                if (segNode == null)
                {
                    string segId = "seg-" + Guid.NewGuid().ToString().Substring(0, 8);
                    var segData = new CorridorSegmentData
                    {
                        id = segId,
                        name = segName,
                        province = province,
                        city = city,
                        lineName = lineName,
                        startTower = ta.name,
                        endTower = tb.name,
                        startMileage = 0f,
                        endMileage = spanLen,
                        length = spanLen,
                        pointCount = 0,
                        lasPath = outPath,
                        isLoaded = false,
                        isVisible = true
                    };
                    segNode = new HierarchyNode(segId, segName, HierarchyNodeType.Segment, 250f);
                    segNode.lat = ta.lat;
                    segNode.lon = ta.lon;
                    segNode.segmentData = segData;
                    lineNode.children.Add(segNode);
                }
                else
                {
                    segNode.name = segName;
                    if (segNode.segmentData != null)
                    {
                        segNode.segmentData.name = segName;
                        segNode.segmentData.startTower = ta.name;
                        segNode.segmentData.endTower = tb.name;
                        segNode.segmentData.length = spanLen;
                        segNode.segmentData.endMileage = spanLen;
                        if (!string.IsNullOrEmpty(outPath)) segNode.segmentData.lasPath = outPath;
                    }
                }
            }

            // Remove uncompleted segments that are no longer part of current marking sequence
            lineNode.children.RemoveAll(s => s.segmentData != null && !s.segmentData.isLoaded && !File.Exists(s.segmentData.lasPath) && !currentSpanNames.Contains(s.name));
        }
    }
}
