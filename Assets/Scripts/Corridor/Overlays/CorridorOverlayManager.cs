﻿using System.Collections.Generic;
using PointCloudRuntimeViewer;
using UnityEngine;

namespace OTA.Corridor.Overlays
{
    public class CorridorOverlayManager : MonoBehaviour
    {
        public bool showTowers = true;
        public bool showWires = true;
        public bool showLabels = true;
        public bool showSagInfo = true;

        public List<TowerOverlay> towers = new List<TowerOverlay>();
        public List<WireOverlay> wires = new List<WireOverlay>();

        private GUIStyle labelStyle;

        void Awake()
        {
            InitStyle();
        }

        private void InitStyle()
        {
            labelStyle = new GUIStyle();
            labelStyle.fontSize = 12;
            labelStyle.normal.textColor = Color.yellow;
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.fontStyle = FontStyle.Bold;
        }

        public void ClearOverlays()
        {
            foreach (var t in towers) if (t != null) Destroy(t.gameObject);
            foreach (var w in wires) if (w != null) Destroy(w.gameObject);
            towers.Clear();
            wires.Clear();
        }

        public void SetTowersVisible(bool visible)
        {
            showTowers = visible;
            foreach (var t in towers) if (t != null) t.gameObject.SetActive(visible);
        }

        public void SetWiresVisible(bool visible)
        {
            showWires = visible;
            foreach (var w in wires) if (w != null) w.gameObject.SetActive(visible);
        }

        [System.Obsolete("Use ExtractFromRealPointCloud instead")]
        public void CreateSample220kvSpan(Vector3 center)
        {
            // Deprecated mock generation; no-op or fallback
        }

        public bool ExtractFromRealPointCloud(RuntimeViewerDX11 viewer, byte[] classes)
        {
            ClearOverlays();

            if (!PointCloudFeatureExtractor.ExtractCorridorElements(viewer, classes, out var extractedTowers, out var extractedWires))
            {
                Debug.LogWarning("[CorridorOverlayManager] 点云中未识别到 Class 14(导线) 或 Class 15(杆塔) 真实点，未创建任何覆盖层。");
                return false;
            }

            foreach (var tInfo in extractedTowers)
            {
                var tGo = new GameObject("RealTower_" + tInfo.id);
                tGo.transform.SetParent(transform);
                tGo.transform.position = tInfo.basePosition;

                var tower = tGo.AddComponent<TowerOverlay>();
                tower.towerId = tInfo.id;
                tower.towerName = tInfo.name + " (" + tInfo.height.ToString("F1") + "m)";
                tower.towerHeight = tInfo.height;
                tower.crossarmSpan = 14f;
                tower.RebuildGeometry();
                towers.Add(tower);
            }

            foreach (var wInfo in extractedWires)
            {
                var wGo = new GameObject("RealWire_" + wInfo.name);
                wGo.transform.SetParent(transform);

                var wire = wGo.AddComponent<WireOverlay>();
                wire.spanName = wInfo.name;
                wire.wireColor = new Color(0f, 0.9f, 1.0f, 0.9f);
                wire.SetPoints(wInfo.startAttachment, wInfo.endAttachment, wInfo.measuredSag);
                wires.Add(wire);
            }

            Debug.Log("[CorridorOverlayManager] 成功从真实点云中提取生成: " + towers.Count + " 基杆塔, " + wires.Count + " 条拟合导线。");
            return true;
        }

        void OnGUI()
        {
            var cam = CameraHelper.MainCamera;
            if (!showLabels || cam == null) return;
            if (labelStyle == null) InitStyle();

            if (showTowers)
            {
                foreach (var t in towers)
                {
                    if (t == null || !t.gameObject.activeInHierarchy) continue;
                    Vector3 sp = cam.WorldToScreenPoint(t.TopPosition + Vector3.up * 3f);
                    if (sp.z > 0f && sp.z < 2000f)
                    {
                        float y = Screen.height - sp.y;
                        GUI.Box(new Rect(sp.x - 75, y - 14, 150, 26), t.towerName, GUI.skin.box);
                    }
                }
            }

            if (showSagInfo)
            {
                foreach (var w in wires)
                {
                    if (w == null || !w.gameObject.activeInHierarchy) continue;
                    Vector3 mid = w.MaxSagPoint;
                    Vector3 sp = cam.WorldToScreenPoint(mid);
                    if (sp.z > 0f && sp.z < 1500f)
                    {
                        float y = Screen.height - sp.y;
                        string text = string.Format("{0}\n档距:{1:F1}m 实测弧垂:{2:F2}m", w.spanName, w.SpanLength, w.sag);
                        GUI.Label(new Rect(sp.x - 90, y - 22, 180, 44), text, labelStyle);
                    }
                }
            }
        }
    }
}
