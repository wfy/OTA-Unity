using System.Collections.Generic;
using System.IO;
using OTA.Corridor;
using OTA.Corridor.Overlays;
using OTA.Framework;
using PointCloudRuntimeViewer;
using UnityEngine;

namespace OTA
{
    /// <summary>
    /// LAS loader with dynamic dataset switching, coordinate normalization,
    /// and automatic overlay & color manager synchronization.
    /// </summary>
    [RequireComponent(typeof(RuntimeViewerDX11))]
    public class LasImporter : MonoBehaviour
    {
        [Tooltip("Path to a .las file. Absolute path or relative to StreamingAssets.")]
        public string lasPath = "";

        [Tooltip("Parse in a background thread and push buffers on the main thread.")]
        public bool threaded = true;

        [Tooltip("Auto load default las dataset at start")]
        public bool autoLoadOnStart = false;

        private RuntimeViewerDX11 viewer;
        private CorridorColorManager colorManager;
        private bool loaded = false;
        private string currentLoadedFile = "";

        public bool IsLoaded => loaded;
        public string CurrentLoadedFile => currentLoadedFile;

        void Awake()
        {
            viewer = GetComponent<RuntimeViewerDX11>();
            colorManager = GetComponent<CorridorColorManager>();
            if (colorManager == null) colorManager = gameObject.AddComponent<CorridorColorManager>();
        }

        void Start()
        {
            if (autoLoadOnStart)
            {
                if (string.IsNullOrEmpty(lasPath))
                {
                    lasPath = LasDatasetRegistry.ResolveBestInitialLas();
                }
                if (!string.IsNullOrEmpty(lasPath)) Load(lasPath);
            }
        }

        public void Unload()
        {
            if (viewer != null)
            {
                viewer.displayPoints = false;
            }
            if (colorManager != null)
            {
                colorManager.ClearData();
            }
            var overlayMgr = FindObjectOfType<CorridorOverlayManager>();
            if (overlayMgr != null)
            {
                overlayMgr.ClearOverlays();
            }
            lasPath = "";
            currentLoadedFile = "";
            loaded = false;
        }

        public void Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            if (!File.Exists(path))
            {
                path = Path.Combine(Application.streamingAssetsPath, path);
                if (!File.Exists(path))
                {
                    Debug.LogError("[LasImporter] file not found: " + path);
                    return;
                }
            }

            var full = Path.GetFullPath(path).Replace("\\", "/");
            lasPath = full;

            if (threaded)
            {
                new System.Threading.Thread(() => ParseAndPush(full)).Start();
            }
            else
            {
                ParseAndPush(full);
            }
        }

        private void ParseAndPush(string fullPath)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var data = LasParser.Parse(fullPath);
            sw.Stop();
            if (data.error != null)
            {
                Debug.LogError("[LasImporter] parse failed: " + data.error);
                return;
            }

            Debug.Log("[LasImporter] parsed " + data.count + " pts in " +
                      (sw.ElapsedMilliseconds / 1000f).ToString("F1") + "s" +
                      (data.hasColor ? " (RGB: yes)" : " (RGB: NO)") +
                      ", classes: " + (data.classes != null ? "yes" : "no"));

            Vector3 center = new Vector3(
                (float)data.centerX,
                (float)data.centerY,
                (float)data.centerZ);

            int count = data.count;
            Vector3[] pts = new Vector3[count];
            Vector4[] cols = new Vector4[count];
            bool hasColor = data.hasColor && data.colors != null;

            for (int i = 0; i < count; i++)
            {
                int p = i * 3;
                float lx = data.positions[p];
                float ly = data.positions[p + 1];
                float lz = data.positions[p + 2];
                pts[i] = new Vector3(lx, lz, ly); // flipYZ
                cols[i] = hasColor
                    ? new Vector4(data.colors[p], data.colors[p + 1], data.colors[p + 2], 1f)
                    : new Vector4(1f, 1f, 1f, 1f);
            }

            var center2 = center;
            var dataCopy = data;

            UnityLibrary.MainThread.Call(() =>
            {
                viewer.useManualOffset = true;
                viewer.manualOffset = center2;
                viewer.readRGB = true;
                viewer.SetPoints(pts, cols, dataCopy.hasColor);

                if (colorManager != null)
                {
                    float relMinZ = (float)(dataCopy.minZ - dataCopy.centerZ);
                    float relMaxZ = (float)(dataCopy.maxZ - dataCopy.centerZ);
                    colorManager.InitFromArrays(pts, cols, dataCopy.classes, dataCopy.intensities, relMinZ, relMaxZ);
                }

                currentLoadedFile = fullPath;
                loaded = true;

                // Register / update segment in CorridorSectionManager
                if (CorridorSectionManager.Instance != null)
                {
                    CorridorSectionManager.Instance.UpdateCurrentActiveSegment(
                        Path.GetFileNameWithoutExtension(fullPath),
                        fullPath,
                        gameObject,
                        viewer,
                        colorManager,
                        this,
                        dataCopy.count);
                }

                // Synchronize overlays around new bounds
                var overlayMgr = FindObjectOfType<CorridorOverlayManager>();
                if (overlayMgr != null)
                {
                    overlayMgr.ExtractFromRealPointCloud(viewer, colorManager != null ? colorManager.PointClasses : null);
                }

                // Synchronize and re-focus camera to newly loaded point cloud
                var panCam = FindObjectOfType<Metervara.Interaction.PanZoomOrbitMouse>();
                if (panCam != null)
                {
                    panCam.InvalidateBounds();
                    panCam.UpdateBounds();
                    panCam.FocusOnBounds(viewer.cloudBounds);
                }
                var p0Cam = FindObjectOfType<P0OrbitCamera>();
                if (p0Cam != null)
                {
                    p0Cam.FocusOnBounds(viewer.cloudBounds);
                }

                Debug.Log("[LasImporter] Loaded and synchronized: " + Path.GetFileName(fullPath) + " (" + dataCopy.count + " pts)");
            });
        }

        public void Reload()
        {
            if (!string.IsNullOrEmpty(lasPath)) Load(lasPath);
        }
    }
}