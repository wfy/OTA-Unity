using System;
using System.Collections.Generic;
using OTA.Framework;
using PointCloudRuntimeViewer;
using UnityEngine;

namespace OTA.Corridor
{
    /// <summary>
    /// Manages multi-mode point cloud coloring (RGB, Classification, Elevation, Intensity)
    /// and dynamically synchronizes with RuntimeViewerDX11 ComputeBuffers.
    /// </summary>
    [RequireComponent(typeof(RuntimeViewerDX11))]
    public class CorridorColorManager : MonoBehaviour
    {
        public static CorridorColorManager Instance { get; private set; }

        [Header("Color Mode")]
        public PointCloudColorMode colorMode = PointCloudColorMode.RGB;
        public ElevationRampType elevationRamp = ElevationRampType.Turbo;
        public bool intensityHeatMap = false;
        public bool dimHiddenClasses = true;

        [Header("Range")]
        public float minElevation = 0f;
        public float maxElevation = 100f;
        public float minIntensity = 0f;
        public float maxIntensity = 1f;

        private RuntimeViewerDX11 viewer;
        private Dictionary<byte, ClassMeta> palette;
        private List<byte> presentClasses = new List<byte>();

        private int pointCount = 0;
        private Vector4[] rawRgbColors;
        private Vector4[] baseRgbColors;
        private byte[] pointClasses;
        private float[] pointIntensities;
        private float[] pointZ;
        private Vector4[] currentActiveColors;

        private bool isDataReady = false;

        public PointCloudColorMode CurrentMode => colorMode;
        public int TotalPoints => pointCount;
        public bool IsDataReady => isDataReady;
        public List<byte> PresentClasses => presentClasses;
        public byte[] PointClasses => pointClasses;

        public void ClearData()
        {
            pointCount = 0;
            rawRgbColors = null;
            baseRgbColors = null;
            pointClasses = null;
            pointIntensities = null;
            pointZ = null;
            currentActiveColors = null;
            if (presentClasses != null) presentClasses.Clear();
            isDataReady = false;
        }

        void Awake()
        {
            if (Instance == null) Instance = this;
            viewer = GetComponent<RuntimeViewerDX11>();
            palette = ClassificationPalette.CreatePaletteCopy();
        }

        void OnEnable()
        {
            if (OTA.Corridor.Annotation.AnnotationHistory.Instance != null)
            {
                OTA.Corridor.Annotation.AnnotationHistory.Instance.OnRevert += HandleRevert;
                OTA.Corridor.Annotation.AnnotationHistory.Instance.OnReapply += HandleReapply;
            }
        }

        void OnDisable()
        {
            if (OTA.Corridor.Annotation.AnnotationHistory.Instance != null)
            {
                OTA.Corridor.Annotation.AnnotationHistory.Instance.OnRevert -= HandleRevert;
                OTA.Corridor.Annotation.AnnotationHistory.Instance.OnReapply -= HandleReapply;
            }
        }

        private void HandleRevert(int[] indices, byte[] oldClasses)
        {
            if (indices == null || oldClasses == null || pointClasses == null) return;
            for (int i = 0; i < indices.Length; i++)
            {
                int idx = indices[i];
                if (idx >= 0 && idx < pointCount)
                {
                    pointClasses[idx] = oldClasses[i];
                    if (baseRgbColors != null && idx < baseRgbColors.Length)
                    {
                        currentActiveColors[idx] = baseRgbColors[idx];
                        rawRgbColors[idx] = baseRgbColors[idx];
                    }
                }
            }
            UploadColorsToViewer();
        }

        private void HandleReapply(int[] indices, byte newClass)
        {
            UpdatePointsClass(indices, newClass);
        }

        public void InitFromLasData(ref LasParser.LasPointData data, Vector3 centerOffset)
        {
            pointCount = data.count;
            minElevation = (float)data.minZ;
            maxElevation = (float)data.maxZ;
            if (Mathf.Approximately(minElevation, maxElevation))
            {
                maxElevation = minElevation + 1f;
            }

            rawRgbColors = new Vector4[pointCount];
            baseRgbColors = new Vector4[pointCount];
            pointClasses = new byte[pointCount];
            pointIntensities = new float[pointCount];
            pointZ = new float[pointCount];
            currentActiveColors = new Vector4[pointCount];

            var keys = new List<byte>(palette.Keys);
            foreach (var k in keys)
            {
                var m = palette[k];
                m.count = 0;
                palette[k] = m;
            }
            presentClasses.Clear();
            var classFound = new HashSet<byte>();

            bool hasRgb = data.hasColor && data.colors != null;
            for (int i = 0; i < pointCount; i++)
            {
                int p = i * 3;
                if (hasRgb)
                {
                    Vector4 c = new Vector4(data.colors[p], data.colors[p + 1], data.colors[p + 2], 1f);
                    rawRgbColors[i] = c;
                    baseRgbColors[i] = c;
                }
                else
                {
                    Vector4 c = new Vector4(1f, 1f, 1f, 1f);
                    rawRgbColors[i] = c;
                    baseRgbColors[i] = c;
                }

                byte cls = data.classes != null ? data.classes[i] : (byte)0;
                pointClasses[i] = cls;
                if (classFound.Add(cls))
                {
                    presentClasses.Add(cls);
                }

                if (palette.TryGetValue(cls, out var meta))
                {
                    meta.count++;
                    palette[cls] = meta;
                }

                pointIntensities[i] = data.intensities != null ? data.intensities[i] : 0.5f;
                pointZ[i] = data.positions[p + 2];
            }
            presentClasses.Sort();

            isDataReady = true;
            ApplyColorMode(colorMode, false);
        }

        public void InitFromArrays(Vector3[] pts, Vector4[] cols, byte[] classes, float[] intensities, float minZ, float maxZ)
        {
            if (pts == null || pts.Length == 0) return;
            pointCount = pts.Length;
            float minE = float.MaxValue;
            float maxE = float.MinValue;
            for (int i = 0; i < pointCount; i++)
            {
                float y = pts[i].y;
                if (y < minE) minE = y;
                if (y > maxE) maxE = y;
            }

            if (minE <= maxE)
            {
                minElevation = minE;
                maxElevation = maxE;
            }
            else
            {
                minElevation = minZ;
                maxElevation = maxZ;
            }

            if (Mathf.Approximately(minElevation, maxElevation))
            {
                maxElevation = minElevation + 1f;
            }

            rawRgbColors = new Vector4[pointCount];
            baseRgbColors = new Vector4[pointCount];
            pointClasses = new byte[pointCount];
            pointIntensities = new float[pointCount];
            pointZ = new float[pointCount];
            currentActiveColors = new Vector4[pointCount];

            var keys = new List<byte>(palette.Keys);
            foreach (var k in keys)
            {
                var m = palette[k];
                m.count = 0;
                palette[k] = m;
            }
            presentClasses.Clear();
            var classFound = new HashSet<byte>();

            bool hasCols = cols != null && cols.Length == pointCount;
            for (int i = 0; i < pointCount; i++)
            {
                Vector4 c = hasCols ? cols[i] : Vector4.one;
                rawRgbColors[i] = c;
                baseRgbColors[i] = c;

                byte cls = classes != null && i < classes.Length ? classes[i] : (byte)0;
                pointClasses[i] = cls;
                if (classFound.Add(cls))
                {
                    presentClasses.Add(cls);
                }

                if (palette.TryGetValue(cls, out var meta))
                {
                    meta.count++;
                    palette[cls] = meta;
                }

                pointIntensities[i] = intensities != null && i < intensities.Length ? intensities[i] : 0.5f;
                pointZ[i] = pts[i].y; // in Unity left-hand coords, y is elevation
            }
            presentClasses.Sort();

            isDataReady = true;
            ApplyColorMode(colorMode, true);
        }

        public RuntimeViewerDX11.RenderMode CurrentRenderMode =>
            viewer != null ? viewer.currentRenderMode : (viewer = GetComponent<RuntimeViewerDX11>()) != null ? viewer.currentRenderMode : RuntimeViewerDX11.RenderMode.Point;

        public void SwitchRenderMode(RuntimeViewerDX11.RenderMode mode)
        {
            if (viewer == null) viewer = GetComponent<RuntimeViewerDX11>();
            if (viewer != null)
            {
                viewer.UpdateViewBuffers(mode);
                UploadColorsToViewer();
            }
        }

        public void SetColorMode(PointCloudColorMode mode)
        {
            if (colorMode == mode) return;
            colorMode = mode;
            ApplyColorMode(colorMode, true);
        }

        public void SetElevationRamp(ElevationRampType ramp)
        {
            elevationRamp = ramp;
            if (colorMode == PointCloudColorMode.Elevation)
            {
                ApplyColorMode(colorMode, true);
            }
        }

        public void SetIntensityHeatMap(bool heat)
        {
            intensityHeatMap = heat;
            if (colorMode == PointCloudColorMode.Intensity)
            {
                ApplyColorMode(colorMode, true);
            }
        }

        public void SetClassVisible(byte classId, bool visible)
        {
            if (palette.TryGetValue(classId, out var meta))
            {
                meta.isVisible = visible;
                palette[classId] = meta;
                if (colorMode == PointCloudColorMode.Classification)
                {
                    ApplyColorMode(colorMode, true);
                }
            }
        }

        public void SetAllClassesVisible(bool visible)
        {
            var keys = new List<byte>(palette.Keys);
            foreach (var k in keys)
            {
                var m = palette[k];
                m.isVisible = visible;
                palette[k] = m;
            }
            if (colorMode == PointCloudColorMode.Classification)
            {
                ApplyColorMode(colorMode, true);
            }
        }

        public bool IsClassVisible(byte classId)
        {
            if (palette.TryGetValue(classId, out var meta)) return meta.isVisible;
            return true;
        }

        public ClassMeta GetClassMeta(byte classId)
        {
            if (palette.TryGetValue(classId, out var meta)) return meta;
            return ClassificationPalette.GetMeta(classId);
        }

        public void ApplyColorMode(PointCloudColorMode mode, bool uploadImmediately = true)
        {
            if (!isDataReady || pointCount == 0) return;

            float zRange = Mathf.Max(0.001f, maxElevation - minElevation);

            switch (mode)
            {
                case PointCloudColorMode.RGB:
                    Array.Copy(rawRgbColors, currentActiveColors, pointCount);
                    break;

                case PointCloudColorMode.Classification:
                    for (int i = 0; i < pointCount; i++)
                    {
                        byte cls = pointClasses[i];
                        if (palette.TryGetValue(cls, out var meta))
                        {
                            if (meta.isVisible)
                            {
                                currentActiveColors[i] = meta.color;
                            }
                            else
                            {
                                currentActiveColors[i] = dimHiddenClasses
                                    ? new Vector4(0.12f, 0.12f, 0.12f, 0.05f)
                                    : new Vector4(0f, 0f, 0f, 0f);
                            }
                        }
                        else
                        {
                            currentActiveColors[i] = new Vector4(0.5f, 0.5f, 0.5f, 1f);
                        }
                    }
                    break;

                case PointCloudColorMode.Elevation:
                    for (int i = 0; i < pointCount; i++)
                    {
                        float t = Mathf.Clamp01((pointZ[i] - minElevation) / zRange);
                        currentActiveColors[i] = ColorRamp.EvaluateElevation(t, elevationRamp);
                    }
                    break;

                case PointCloudColorMode.Intensity:
                    for (int i = 0; i < pointCount; i++)
                    {
                        currentActiveColors[i] = ColorRamp.EvaluateIntensity(pointIntensities[i], intensityHeatMap);
                    }
                    break;
            }

            if (uploadImmediately)
            {
                UploadColorsToViewer();
            }
        }

        public void UploadColorsToViewer()
        {
            if (viewer == null || currentActiveColors == null) return;
            viewer.UpdateColors(currentActiveColors);
        }

        public void UpdatePointsClass(int[] indices, byte newClass)
        {
            if (indices == null || indices.Length == 0 || pointClasses == null) return;

            if (!presentClasses.Contains(newClass))
            {
                presentClasses.Add(newClass);
                presentClasses.Sort();
            }
            if (palette.TryGetValue(newClass, out var m))
            {
                if (!m.isVisible)
                {
                    m.isVisible = true;
                    palette[newClass] = m;
                }
            }

            Color newC = ClassificationPalette.GetColor(newClass);
            Vector4 newVCol = new Vector4(newC.r, newC.g, newC.b, 1f);

            for (int i = 0; i < indices.Length; i++)
            {
                int idx = indices[i];
                if (idx >= 0 && idx < pointCount)
                {
                    byte oldClass = pointClasses[idx];
                    if (oldClass != newClass)
                    {
                        if (palette.TryGetValue(oldClass, out var oldMeta) && oldMeta.count > 0)
                        {
                            oldMeta.count--;
                            palette[oldClass] = oldMeta;
                        }
                        if (palette.TryGetValue(newClass, out var targetMeta))
                        {
                            targetMeta.count++;
                            palette[newClass] = targetMeta;
                        }
                        pointClasses[idx] = newClass;
                    }

                    // Selectively update active and raw RGB colors for targeted points ONLY,
                    // preserving base colors for all other points in the point cloud
                    currentActiveColors[idx] = newVCol;
                    if (rawRgbColors != null && idx < rawRgbColors.Length)
                    {
                        rawRgbColors[idx] = newVCol;
                    }
                }
            }

            UploadColorsToViewer();
        }

        public void UpdatePointsClassBatch(Dictionary<int, byte> changes)
        {
            if (changes == null || changes.Count == 0 || pointClasses == null) return;

            bool classAdded = false;
            foreach (var kvp in changes)
            {
                int idx = kvp.Key;
                byte newClass = kvp.Value;
                if (idx >= 0 && idx < pointCount)
                {
                    byte oldClass = pointClasses[idx];
                    if (oldClass != newClass)
                    {
                        if (palette.TryGetValue(oldClass, out var oldMeta) && oldMeta.count > 0)
                        {
                            oldMeta.count--;
                            palette[oldClass] = oldMeta;
                        }
                        if (palette.TryGetValue(newClass, out var targetMeta))
                        {
                            targetMeta.count++;
                            palette[newClass] = targetMeta;
                        }
                        pointClasses[idx] = newClass;
                    }

                    // Selectively update colors for targeted points ONLY
                    Color c = ClassificationPalette.GetColor(newClass);
                    Vector4 vCol = new Vector4(c.r, c.g, c.b, 1f);
                    currentActiveColors[idx] = vCol;
                    if (rawRgbColors != null && idx < rawRgbColors.Length)
                    {
                        rawRgbColors[idx] = vCol;
                    }

                    if (!presentClasses.Contains(newClass))
                    {
                        presentClasses.Add(newClass);
                        classAdded = true;
                    }
                    if (palette.TryGetValue(newClass, out var m))
                    {
                        if (!m.isVisible)
                        {
                            m.isVisible = true;
                            palette[newClass] = m;
                        }
                    }
                }
            }
            if (classAdded) presentClasses.Sort();

            UploadColorsToViewer();
        }
    }
}
