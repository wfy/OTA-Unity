using System;
using System.Collections.Generic;
using System.IO;
using OTA.Corridor.Overlays;
using PointCloudRuntimeViewer;
using UnityEngine;

namespace OTA.Corridor
{
    [Serializable]
    public class CorridorSegment
    {
        public int id;
        public string name;
        public string lasPath;
        public GameObject root;
        public RuntimeViewerDX11 viewer;
        public CorridorColorManager colorManager;
        public LasImporter importer;
        public Bounds bounds;
        public bool isVisible = true;
        public int pointCount = 0;
    }

    public class CorridorSectionManager : MonoBehaviour
    {
        public static CorridorSectionManager Instance { get; private set; }

        public Material pointCloudMaterial;
        public CorridorOverlayManager overlayManager;
        public List<CorridorSegment> segments = new List<CorridorSegment>();
        public int activeSegmentIndex = 0;

        private Vector3 sharedCorridorCenter = Vector3.zero;
        private bool hasSharedOffset = false;

        public Vector3 SharedOffset => sharedCorridorCenter;
        public int TotalSegments => segments.Count;
        public CorridorSegment ActiveSegment => (segments != null && activeSegmentIndex >= 0 && activeSegmentIndex < segments.Count) ? segments[activeSegmentIndex] : null;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void UpdateCurrentActiveSegment(string segName, string path, GameObject rootGo, RuntimeViewerDX11 rvd, CorridorColorManager ccm, LasImporter imp, int count)
        {
            if (segments.Count == 0)
            {
                var seg = new CorridorSegment
                {
                    id = 1,
                    name = segName,
                    lasPath = path,
                    root = rootGo,
                    viewer = rvd,
                    colorManager = ccm,
                    importer = imp,
                    bounds = rvd != null ? rvd.cloudBounds : new Bounds(),
                    isVisible = true,
                    pointCount = count
                };
                segments.Add(seg);
                activeSegmentIndex = 0;
            }
            else
            {
                var seg = segments[activeSegmentIndex];
                seg.name = segName;
                seg.lasPath = path;
                seg.root = rootGo;
                seg.viewer = rvd;
                seg.colorManager = ccm;
                seg.importer = imp;
                seg.bounds = rvd != null ? rvd.cloudBounds : new Bounds();
                seg.pointCount = count;
                seg.isVisible = true;
            }
        }

        public void SetSegmentVisible(int index, bool visible)
        {
            if (index < 0 || index >= segments.Count) return;
            segments[index].isVisible = visible;
            if (segments[index].root != null)
            {
                segments[index].root.SetActive(visible);
            }
        }

        public void FocusSegment(int index)
        {
            if (index < 0 || index >= segments.Count) return;
            var seg = segments[index];
            var cam = CameraHelper.MainCamera;
            if (cam == null) return;

            Vector3 target = (seg.viewer != null && seg.viewer.cloudBounds.size.sqrMagnitude > 0)
                ? seg.viewer.cloudBounds.center
                : Vector3.zero;

            cam.transform.position = target + new Vector3(0, 45, -120);
            cam.transform.LookAt(target);
        }

        public int GetTotalPoints()
        {
            int total = 0;
            foreach (var s in segments)
            {
                if (s != null && s.isVisible && s.colorManager != null)
                {
                    total += s.colorManager.TotalPoints;
                }
            }
            return total;
        }
    }
}