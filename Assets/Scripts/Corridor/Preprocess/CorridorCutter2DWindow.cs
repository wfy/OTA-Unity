using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using OTA.Corridor.Hierarchy;
using OTA.Corridor.UI;
using UnityEngine;

namespace OTA.Corridor.Preprocess
{
    /// <summary>
    /// Interactive 2D Tianditu Map Window for Corridor Marking, Tower Layout,
    /// and Streaming Multi-Segment Point Cloud Slicing.
    /// Seamlessly mounts sliced segments into the Province/City/Line hierarchy tree.
    /// </summary>
    public class CorridorCutter2DWindow : MonoBehaviour
    {
        public static CorridorCutter2DWindow Instance { get; private set; }

        public bool IsOpen { get; private set; } = false;

        // Current Input Context
        private LasImportMetadata currentMeta;
        private List<LasImportMetadata> batchMetas = new List<LasImportMetadata>();
        private LasImportMetadata masterCRS;
        private List<GeoRect> batchGeoBounds = new List<GeoRect>();
        private string province;
        private string city;
        private string lineName;
        private GeoRect lasGeoBounds;

        // Map View State
        private double centerLat = 30.2741;
        private double centerLon = 120.1551;
        private int zoom = 17; // Level 10 - 19 (Max 0.3m/px High-Res Satellite)
        private Vector2 dragStartMouse;
        private double dragStartLat, dragStartLon;
        private bool isDraggingMap = false;

        // Corridor Parameters
        private float halfWidth = 30f;
        private string halfWidthInputStr = "30";

        private struct WidthPreset
        {
            public float width;
            public string label;
            public WidthPreset(float w, string l) { width = w; label = l; }
        }

        private static readonly WidthPreset[] widthPresets = new WidthPreset[]
        {
            new WidthPreset(15f, "15m 110k"),
            new WidthPreset(25f, "25m 220k"),
            new WidthPreset(30f, "30m 标准"),
            new WidthPreset(40f, "40m 500k"),
            new WidthPreset(50f, "50m 特高")
        };

        private void SetHalfWidth(float val)
        {
            halfWidth = Mathf.Clamp(val, 5f, 150f);
            halfWidthInputStr = halfWidth.ToString("F0");
            PlayerPrefs.SetFloat("CorridorHalfWidth", halfWidth);
            statusMessage = string.Format("廊道半宽已设为: {0:F0}m (总宽度: {1:F0}m)", halfWidth, halfWidth * 2);
        }

        private List<TowerMarker> towers = new List<TowerMarker>();
        private int selectedTowerIndex = -1;
        private bool isDraggingTower = false;

        // Slicing State
        private bool isCutting = false;
        private float cutProgress = 0f;
        private string statusMessage = "准备就绪：请在地图上右键标记杆塔，或导入 KML 航线";
        private CancellationTokenSource cts;
        private List<CutSegmentTask> activeTasks = new List<CutSegmentTask>();
        private MapSourceType mapSource = MapSourceType.Bing_Aerial;

        // GUI Styles
        private GUIStyle windowStyle;
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle buttonStyle;
        private GUIStyle greenButtonStyle;
        private GUIStyle labelStyle;
        private GUIStyle boxStyle;
        private bool stylesInitialized = false;

        private Texture2D greenTex;
        private Texture2D yellowTex;
        private Texture2D yellowBorderTex;
        private Texture2D redTex;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);

            halfWidth = PlayerPrefs.GetFloat("CorridorHalfWidth", 30f);
            halfWidthInputStr = halfWidth.ToString("F0");

            greenTex = MakeColorTex(new Color(0.2f, 0.85f, 0.4f, 0.5f));
            yellowTex = MakeColorTex(new Color(1f, 0.85f, 0.2f, 0.14f));
            yellowBorderTex = MakeColorTex(new Color(1f, 0.85f, 0.15f, 0.95f));
            redTex = MakeColorTex(new Color(0.9f, 0.25f, 0.25f, 0.8f));
        }

        private Texture2D MakeColorTex(Color col)
        {
            Texture2D tex = new Texture2D(2, 2);
            tex.SetPixels(new Color[] { col, col, col, col });
            tex.Apply();
            return tex;
        }

        public void Open(LasImportMetadata meta, string prov, string cty, string line)
        {
            var list = (meta != null) ? new List<LasImportMetadata> { meta } : new List<LasImportMetadata>();
            Open(list, meta, prov, cty, line);
        }

        public void Open(List<LasImportMetadata> metas, LasImportMetadata masterCrs, string prov, string cty, string line)
        {
            batchMetas = metas ?? new List<LasImportMetadata>();
            masterCRS = masterCrs ?? (batchMetas.Count > 0 ? batchMetas[0] : null);
            if (masterCRS != null && masterCRS.selectedCRS == CRSType.Auto)
            {
                masterCRS.selectedCRS = masterCRS.detectedCRS != CRSType.Auto ? masterCRS.detectedCRS : CRSType.UTM_North;
            }
            currentMeta = masterCRS;
            province = prov;
            city = cty;
            lineName = line;
            IsOpen = true;
            mapSource = (MapSourceType)PlayerPrefs.GetInt("PreferredMapSource", (int)MapSourceType.Bing_Aerial);
            halfWidth = PlayerPrefs.GetFloat("CorridorHalfWidth", 30f);
            halfWidthInputStr = halfWidth.ToString("F0");

            // Ensure CorridorHUD left sidebar is visible and shows hierarchy tree tab
            if (CorridorHUD.Instance != null)
            {
                CorridorHUD.Instance.showSidebar = true;
                CorridorHUD.Instance.sidebarTab = 0;
            }

            // Ensure TileProvider exists
            if (FindObjectOfType<TiandituTileProvider>() == null)
            {
                gameObject.AddComponent<TiandituTileProvider>();
            }

            // Calculate Geographic Extents of all LAS files and construct Union Bounds
            batchGeoBounds.Clear();
            double minLa = 90.0, maxLa = -90.0, minLo = 180.0, maxLo = -180.0;
            foreach (var m in batchMetas)
            {
                var gr = GISCoordinateConverter.ConvertBoundsToGeoRect(
                    m.header.minX, m.header.maxX,
                    m.header.minY, m.header.maxY,
                    m
                );
                batchGeoBounds.Add(gr);
                if (gr.minLat < minLa) minLa = gr.minLat;
                if (gr.maxLat > maxLa) maxLa = gr.maxLat;
                if (gr.minLon < minLo) minLo = gr.minLon;
                if (gr.maxLon > maxLo) maxLo = gr.maxLon;
            }

            if (batchGeoBounds.Count > 0 && minLa <= maxLa && minLo <= maxLo)
            {
                lasGeoBounds = new GeoRect(minLa, maxLa, minLo, maxLo);
            }
            else if (currentMeta != null)
            {
                lasGeoBounds = GISCoordinateConverter.ConvertBoundsToGeoRect(
                    currentMeta.header.minX, currentMeta.header.maxX,
                    currentMeta.header.minY, currentMeta.header.maxY,
                    currentMeta
                );
            }
            else
            {
                lasGeoBounds = new GeoRect(30.2, 30.3, 120.1, 120.2);
            }

            centerLat = lasGeoBounds.CenterLat;
            centerLon = lasGeoBounds.CenterLon;

            // Smart auto-zoom based on bounding box geographic span (Level 10 - 19)
            double span = Math.Max(lasGeoBounds.maxLat - lasGeoBounds.minLat,
                (lasGeoBounds.maxLon - lasGeoBounds.minLon) * Math.Cos(centerLat * Math.PI / 180.0));
            if (span > 0.15) zoom = 11;
            else if (span > 0.08) zoom = 12;
            else if (span > 0.03) zoom = 13;
            else if (span > 0.012) zoom = 14;
            else if (span > 0.005) zoom = 15;
            else if (span > 0.002) zoom = 16;
            else if (span > 0.0008) zoom = 17;
            else zoom = 18;

            towers.Clear();
            selectedTowerIndex = -1;
            isCutting = false;
            cutProgress = 0f;
            statusMessage = string.Format("已定位至测区 ({0:F4}°N, {1:F4}°E · 共载入 {2} 个点云标段)，请在地图上右键标定杆塔", centerLat, centerLon, batchMetas.Count);

            SyncTowersToHierarchy();

            // Block camera inputs while 2D map is open
            P0OrbitCamera.BlockCameraInput = true;
        }

        public void FocusOnSpan(string startTowerName, string endTowerName)
        {
            var t1 = towers.Find(t => t.name == startTowerName);
            var t2 = towers.Find(t => t.name == endTowerName);
            if (t1 != null && t2 != null)
            {
                centerLat = (t1.lat + t2.lat) * 0.5;
                centerLon = (t1.lon + t2.lon) * 0.5;
                zoom = Mathf.Max(zoom, 17);
                statusMessage = string.Format("🔍 地图已定位至档距: {0} ➔ {1}", startTowerName, endTowerName);
            }
            else if (t1 != null)
            {
                centerLat = t1.lat;
                centerLon = t1.lon;
                zoom = Mathf.Max(zoom, 17);
                statusMessage = string.Format("🔍 地图已定位至杆塔: {0}", startTowerName);
            }
        }

        public void Close()
        {
            if (isCutting && cts != null)
            {
                cts.Cancel();
            }
            IsOpen = false;
            P0OrbitCamera.BlockCameraInput = false;
        }

        void OnGUI()
        {
            if (!IsOpen) return;

            InitStyles();

            float leftMargin = (CorridorHUD.Instance != null && CorridorHUD.Instance.showSidebar) ? 366f : 0f;
            float topBarHeight = 44f;

            // Render background box only across the 2D map region so CorridorHUD left sidebar remains visible and interactive
            Rect mapAreaRect = new Rect(leftMargin, 0, Screen.width - leftMargin, Screen.height);
            GUI.Box(mapAreaRect, "", windowStyle);

            Rect topBarRect = new Rect(leftMargin, 0, Screen.width - leftMargin, topBarHeight);
            Rect mapRect = new Rect(leftMargin, topBarHeight, Screen.width - leftMargin, Screen.height - topBarHeight);

            DrawTopBar(topBarRect);
            DrawMapViewport(mapRect);
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;

            windowStyle = new GUIStyle(GUI.skin.box);
            windowStyle.normal.background = MakeColorTex(new Color(0.08f, 0.10f, 0.14f, 0.96f));

            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 16;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = Color.white;

            subHeaderStyle = new GUIStyle(GUI.skin.label);
            subHeaderStyle.fontSize = 12;
            subHeaderStyle.normal.textColor = new Color(0.7f, 0.8f, 0.9f);

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 12;

            greenButtonStyle = new GUIStyle(GUI.skin.button);
            greenButtonStyle.fontSize = 13;
            greenButtonStyle.fontStyle = FontStyle.Bold;
            greenButtonStyle.normal.textColor = Color.white;

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 12;
            labelStyle.normal.textColor = Color.white;

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeColorTex(new Color(0.12f, 0.16f, 0.22f, 0.8f));
        }

        private void DrawTopBar(Rect rect)
        {
            GUI.Box(rect, "", boxStyle);
            GUILayout.BeginArea(new Rect(rect.x + 12, rect.y + 6, rect.width - 24, rect.height - 12));
            GUILayout.BeginHorizontal();

            string srcName = (mapSource == MapSourceType.Bing_Aerial) ? "微软Bing 0.3m高清遥感" :
                             (mapSource == MapSourceType.ArcGIS_Imagery) ? "ESRI ArcGIS 遥感影像" : "天地图 0.5m 卫星";
            GUILayout.Label("🛰️ " + srcName, headerStyle, GUILayout.Width(215));

            GUILayout.Space(6);
            int spanCount = Mathf.Max(0, towers.Count - 1);
            string info = string.Format("【{0} · {1}】 {2}基({3}档)",
                city, lineName, towers.Count, spanCount);
            GUILayout.Label(info, subHeaderStyle);

            GUILayout.Space(8);

            // Map Source Switcher (Bing vs ArcGIS)
            GUI.backgroundColor = (mapSource == MapSourceType.Bing_Aerial) ? new Color(0.2f, 0.85f, 0.4f) : new Color(0.22f, 0.28f, 0.36f);
            if (GUILayout.Button("🌐 微软Bing(高分)", buttonStyle, GUILayout.Height(26), GUILayout.Width(105)))
            {
                mapSource = MapSourceType.Bing_Aerial;
                PlayerPrefs.SetInt("PreferredMapSource", (int)mapSource);
                statusMessage = "已切换至微软 Bing 0.3m~0.5m 高分卫星源 (WGS84无偏)";
            }

            GUI.backgroundColor = (mapSource == MapSourceType.ArcGIS_Imagery) ? new Color(0.2f, 0.85f, 0.4f) : new Color(0.22f, 0.28f, 0.36f);
            if (GUILayout.Button("🌍 ArcGIS", buttonStyle, GUILayout.Height(26), GUILayout.Width(68)))
            {
                mapSource = MapSourceType.ArcGIS_Imagery;
                PlayerPrefs.SetInt("PreferredMapSource", (int)mapSource);
                statusMessage = "已切换至 ESRI ArcGIS 遥感底图";
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Label(string.Format("🔍 L{0}", zoom), subHeaderStyle, GUILayout.Width(42));

            GUILayout.Space(6);

            // Corridor Half-Width Quick Controls
            GUILayout.Label("📐 半宽:", subHeaderStyle, GUILayout.Width(50));

            for (int p = 0; p < widthPresets.Length; p++)
            {
                bool isSelected = Mathf.Approximately(halfWidth, widthPresets[p].width);
                GUI.backgroundColor = isSelected ? new Color(0.2f, 0.85f, 0.4f) : new Color(0.22f, 0.28f, 0.36f);
                if (GUILayout.Button(widthPresets[p].label, buttonStyle, GUILayout.Height(26), GUILayout.Width(58)))
                {
                    SetHalfWidth(widthPresets[p].width);
                }
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(4);

            if (GUILayout.Button("－", buttonStyle, GUILayout.Width(24), GUILayout.Height(26)))
            {
                SetHalfWidth(halfWidth - 5f);
            }

            string newStr = GUILayout.TextField(halfWidthInputStr, GUILayout.Width(34), GUILayout.Height(24));
            if (newStr != halfWidthInputStr)
            {
                halfWidthInputStr = newStr;
                float parsed;
                if (float.TryParse(newStr, out parsed))
                {
                    halfWidth = Mathf.Clamp(parsed, 5f, 150f);
                    PlayerPrefs.SetFloat("CorridorHalfWidth", halfWidth);
                }
            }

            GUILayout.Label("m", subHeaderStyle, GUILayout.Width(14));

            if (GUILayout.Button("＋", buttonStyle, GUILayout.Width(24), GUILayout.Height(26)))
            {
                SetHalfWidth(halfWidth + 5f);
            }

            GUILayout.FlexibleSpace();

            if (isCutting)
            {
                GUILayout.Label(string.Format("⏳ 流式分段裁切中: {0:P0}", cutProgress), subHeaderStyle);
                GUILayout.Space(8);
                Rect pRect = GUILayoutUtility.GetRect(160, 22);
                GUI.Box(pRect, "");
                GUI.DrawTexture(new Rect(pRect.x, pRect.y, pRect.width * cutProgress, pRect.height), greenTex);
                GUI.Label(pRect, string.Format(" {0:P0}", cutProgress), labelStyle);
            }
            else
            {
                GUI.backgroundColor = (towers.Count >= 2) ? new Color(0.2f, 0.85f, 0.4f) : new Color(0.35f, 0.4f, 0.45f);
                GUI.enabled = (towers.Count >= 2);
                if (GUILayout.Button("🚀 完成标定并流式切割", greenButtonStyle, GUILayout.Height(28), GUILayout.Width(170)))
                {
                    StartStreamingCutPipeline();
                }
                GUI.enabled = true;
                GUI.backgroundColor = Color.white;

                GUILayout.Space(8);
                if (towers.Count > 0)
                {
                    if (GUILayout.Button("🗑️ 清空标定", GUILayout.Width(85), GUILayout.Height(28)))
                    {
                        towers.Clear();
                        selectedTowerIndex = -1;
                        SyncTowersToHierarchy();
                        statusMessage = "已清空当前杆塔标定";
                    }
                    GUILayout.Space(8);
                }

                if (GUILayout.Button("🔄 刷新底图", GUILayout.Width(85), GUILayout.Height(28)))
                {
                    if (TiandituTileProvider.Instance != null)
                    {
                        TiandituTileProvider.Instance.ClearAllDiskCache();
                    }
                }
                GUILayout.Space(8);
            }

            if (GUILayout.Button("✕ 返回三维", GUILayout.Width(88), GUILayout.Height(28)))
            {
                Close();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawMapViewport(Rect mapRect)
        {
            GUI.BeginGroup(mapRect);
            Rect localRect = new Rect(0, 0, mapRect.width, mapRect.height);

            // 1. Draw ArcGIS Satellite Tiles
            RenderTiles(localRect);

            // 2. Draw LAS Bounding Box Footprint
            DrawLasFootprint(localRect);

            // 3. Draw Corridor Envelopes and Tower Nodes
            DrawCorridorAndTowers(localRect);

            // 4. Draw Operation Tooltip Overlay (bottom left)
            Rect tipRect = new Rect(12, localRect.height - 34, 620, 24);
            GUI.Box(tipRect, "", boxStyle);
            GUI.Label(new Rect(tipRect.x + 8, tipRect.y + 2, tipRect.width - 16, tipRect.height),
                "💡 交互: [右键]加/删塔 | [左键拖]调位/移图 | [滚轮]缩放(最高19级高清) | [ [ / ] ] 或 Shift+滚轮 调半宽", subHeaderStyle);

            // 5. Handle Mouse Events (Pan, Zoom, Add Tower, Drag Tower, Delete Tower)
            HandleMapInput(localRect);

            GUI.EndGroup();
        }

        private void RenderTiles(Rect viewRect)
        {
            double centerTileFracX, centerTileFracY;
            int centerTileX, centerTileY;
            GISCoordinateConverter.LatLonToTile(centerLat, centerLon, zoom, out centerTileX, out centerTileY, out centerTileFracX, out centerTileFracY);

            float tileSize = 256f;
            float screenCenterX = viewRect.width * 0.5f;
            float screenCenterY = viewRect.height * 0.5f;

            int tilesXCount = Mathf.CeilToInt(viewRect.width / tileSize) + 2;
            int tilesYCount = Mathf.CeilToInt(viewRect.height / tileSize) + 2;

            int minTX = centerTileX - tilesXCount / 2;
            int maxTX = centerTileX + tilesXCount / 2;
            int minTY = centerTileY - tilesYCount / 2;
            int maxTY = centerTileY + tilesYCount / 2;

            var provider = TiandituTileProvider.Instance;

            for (int ty = minTY; ty <= maxTY; ty++)
            {
                for (int tx = minTX; tx <= maxTX; tx++)
                {
                    float px = screenCenterX + (float)((tx - centerTileX - centerTileFracX) * tileSize);
                    float py = screenCenterY + (float)((ty - centerTileY - centerTileFracY) * tileSize);
                    Rect tileRect = new Rect(px, py, tileSize, tileSize);

                    Texture2D tileTex = provider != null ? provider.GetTile(zoom, tx, ty, mapSource) : null;
                    if (tileTex != null)
                    {
                        GUI.DrawTexture(tileRect, tileTex);
                    }
                }
            }
        }

        private Vector2 GeoToScreen(double lat, double lon, Rect viewRect)
        {
            double centerTileFracX, centerTileFracY;
            int centerTileX, centerTileY;
            GISCoordinateConverter.LatLonToTile(centerLat, centerLon, zoom, out centerTileX, out centerTileY, out centerTileFracX, out centerTileFracY);

            double ptFracX, ptFracY;
            int ptTileX, ptTileY;
            GISCoordinateConverter.LatLonToTile(lat, lon, zoom, out ptTileX, out ptTileY, out ptFracX, out ptFracY);

            float tileSize = 256f;
            float screenCenterX = viewRect.width * 0.5f;
            float screenCenterY = viewRect.height * 0.5f;

            float x = screenCenterX + (float)(((ptTileX + ptFracX) - (centerTileX + centerTileFracX)) * tileSize);
            float y = screenCenterY + (float)(((ptTileY + ptFracY) - (centerTileY + centerTileFracY)) * tileSize);

            return new Vector2(x, y);
        }

        private void ScreenToGeo(Vector2 screenPos, Rect viewRect, out double lat, out double lon)
        {
            double centerTileFracX, centerTileFracY;
            int centerTileX, centerTileY;
            GISCoordinateConverter.LatLonToTile(centerLat, centerLon, zoom, out centerTileX, out centerTileY, out centerTileFracX, out centerTileFracY);

            float tileSize = 256f;
            float screenCenterX = viewRect.width * 0.5f;
            float screenCenterY = viewRect.height * 0.5f;

            double targetTileX = (centerTileX + centerTileFracX) + (screenPos.x - screenCenterX) / tileSize;
            double targetTileY = (centerTileY + centerTileFracY) + (screenPos.y - screenCenterY) / tileSize;

            double n = Math.Pow(2.0, zoom);
            lon = (targetTileX / n) * 360.0 - 180.0;
            double latRad = Math.Atan(Math.Sinh(Math.PI * (1.0 - 2.0 * targetTileY / n)));
            lat = latRad * (180.0 / Math.PI);
        }

        private void DrawLasFootprint(Rect viewRect)
        {
            if (yellowTex == null)
            {
                yellowTex = MakeColorTex(new Color(1f, 0.85f, 0.2f, 0.14f));
            }
            if (yellowBorderTex == null)
            {
                yellowBorderTex = MakeColorTex(new Color(1f, 0.85f, 0.15f, 0.95f));
            }

            int count = (batchGeoBounds != null && batchGeoBounds.Count > 0) ? batchGeoBounds.Count : 1;
            for (int b = 0; b < count; b++)
            {
                GeoRect gr = (batchGeoBounds != null && b < batchGeoBounds.Count) ? batchGeoBounds[b] : lasGeoBounds;
                var meta = (batchMetas != null && b < batchMetas.Count) ? batchMetas[b] : currentMeta;

                Vector2 pMin = GeoToScreen(gr.minLat, gr.minLon, viewRect);
                Vector2 pMax = GeoToScreen(gr.maxLat, gr.maxLon, viewRect);

                float x = Mathf.Min(pMin.x, pMax.x);
                float y = Mathf.Min(pMin.y, pMax.y);
                float w = Mathf.Max(12f, Mathf.Abs(pMax.x - pMin.x));
                float h = Mathf.Max(12f, Mathf.Abs(pMax.y - pMin.y));

                // 1. 内部黄色半透明填充：对 viewRect 进行安全裁剪
                float fillX0 = Mathf.Max(0f, x);
                float fillY0 = Mathf.Max(0f, y);
                float fillX1 = Mathf.Min(viewRect.width, x + w);
                float fillY1 = Mathf.Min(viewRect.height, y + h);

                if (fillX1 > fillX0 && fillY1 > fillY0)
                {
                    Rect clippedFillRect = new Rect(fillX0, fillY0, fillX1 - fillX0, fillY1 - fillY0);
                    GUI.DrawTexture(clippedFillRect, yellowTex);
                }

                // 2. 边框线绘制
                float lineWidth = 2.5f;
                float halfLw = lineWidth * 0.5f;

                // 上边框
                if (y >= -halfLw && y <= viewRect.height + halfLw)
                {
                    float x0 = Mathf.Max(0f, x);
                    float x1 = Mathf.Min(viewRect.width, x + w);
                    if (x1 > x0) GUI.DrawTexture(new Rect(x0, y - halfLw, x1 - x0, lineWidth), yellowBorderTex);
                }

                // 下边框
                float bottomY = y + h;
                if (bottomY >= -halfLw && bottomY <= viewRect.height + halfLw)
                {
                    float x0 = Mathf.Max(0f, x);
                    float x1 = Mathf.Min(viewRect.width, x + w);
                    if (x1 > x0) GUI.DrawTexture(new Rect(x0, bottomY - halfLw, x1 - x0, lineWidth), yellowBorderTex);
                }

                // 左边框
                if (x >= -halfLw && x <= viewRect.width + halfLw)
                {
                    float y0 = Mathf.Max(0f, y);
                    float y1 = Mathf.Min(viewRect.height, y + h);
                    if (y1 > y0) GUI.DrawTexture(new Rect(x - halfLw, y0, lineWidth, y1 - y0), yellowBorderTex);
                }

                // 右边框
                float rightX = x + w;
                if (rightX >= -halfLw && rightX <= viewRect.width + halfLw)
                {
                    float y0 = Mathf.Max(0f, y);
                    float y1 = Mathf.Min(viewRect.height, y + h);
                    if (y1 > y0) GUI.DrawTexture(new Rect(rightX - halfLw, y0, lineWidth, y1 - y0), yellowBorderTex);
                }

                // 3. 标识标签：当包围盒位于视口内或部分可见时，自适应停靠显示
                if (fillX1 > fillX0 && fillY1 > fillY0)
                {
                    float labelX = Mathf.Clamp(x + 4f, 8f, viewRect.width - 250f);
                    float labelY = Mathf.Clamp(y - 20f, 6f, viewRect.height - 24f);
                    string fn = (meta != null) ? Path.GetFileName(meta.filePath) : "原始点云";
                    string ptsStr = (meta != null && meta.header.pointCount > 0) ? string.Format("{0:F1}万点", meta.header.pointCount / 10000.0) : "";
                    string crsDesc = "";
                    if (meta != null)
                    {
                        crsDesc = (meta.selectedCRS == CRSType.CGCS2000_3Deg_NoZone)
                            ? string.Format("{0}°E", meta.centralMeridian)
                            : (meta.selectedCRS == CRSType.UTM_North ? string.Format("{0}N", meta.utmZone) : meta.selectedCRS.ToString());
                    }
                    string tag = (count > 1)
                        ? string.Format("📦 #{0:D2} {1} [{2} · {3}]", b + 1, fn, ptsStr, crsDesc)
                        : string.Format("📊 {0} [{1} · {2}]", fn, ptsStr, crsDesc);
                    GUI.Label(new Rect(labelX, labelY, 250, 20), tag, subHeaderStyle);
                }
            }
        }

        private void DrawCorridorAndTowers(Rect viewRect)
        {
            // Draw corridor spans between towers
            for (int i = 0; i < towers.Count - 1; i++)
            {
                var tA = towers[i];
                var tB = towers[i + 1];

                Vector2 pA = GeoToScreen(tA.lat, tA.lon, viewRect);
                Vector2 pB = GeoToScreen(tB.lat, tB.lon, viewRect);

                // Draw center line
                DrawLine(pA, pB, new Color(0.2f, 0.9f, 0.4f, 0.9f), 3f);

                // Draw Corridor Polygon
                Vector2 dir = (pB - pA).normalized;
                Vector2 norm = new Vector2(-dir.y, dir.x);
                float pixelWidth = (float)(halfWidth / (156543.03392 * Math.Cos(centerLat * Math.PI / 180.0) / Math.Pow(2, zoom)));

                Vector2 c1 = pA + norm * pixelWidth;
                Vector2 c2 = pB + norm * pixelWidth;
                Vector2 c3 = pB - norm * pixelWidth;
                Vector2 c4 = pA - norm * pixelWidth;

                DrawLine(c1, c2, new Color(0.2f, 0.85f, 0.4f, 0.6f), 1.5f);
                DrawLine(c2, c3, new Color(0.2f, 0.85f, 0.4f, 0.6f), 1.5f);
                DrawLine(c3, c4, new Color(0.2f, 0.85f, 0.4f, 0.6f), 1.5f);
                DrawLine(c4, c1, new Color(0.2f, 0.85f, 0.4f, 0.6f), 1.5f);

                // Midpoint distance and corridor width label
                Vector2 mid = (pA + pB) * 0.5f;
                double dist = Math.Sqrt(Math.Pow(tB.easting - tA.easting, 2) + Math.Pow(tB.northing - tA.northing, 2));
                string spanInfo = string.Format("{0:F0}m (走廊±{1:F0}m)", dist, halfWidth);
                GUI.Label(new Rect(mid.x - 65, mid.y - 10, 130, 20), spanInfo, subHeaderStyle);
            }

            // Draw Tower Markers
            for (int i = 0; i < towers.Count; i++)
            {
                var t = towers[i];
                Vector2 p = GeoToScreen(t.lat, t.lon, viewRect);

                float r = (i == selectedTowerIndex) ? 14f : 10f;
                Rect markerRect = new Rect(p.x - r, p.y - r, r * 2, r * 2);

                GUI.DrawTexture(markerRect, (i == selectedTowerIndex) ? redTex : greenTex);
                GUI.Label(new Rect(p.x + 12, p.y - 10, 100, 20), t.name, labelStyle);
            }
        }

        private void DrawLine(Vector2 pointA, Vector2 pointB, Color color, float width)
        {
            Vector2 p0 = pointA;
            Vector2 p1 = pointB;
            Rect clipRect = new Rect(-50f, -50f, Screen.width + 100f, Screen.height + 100f);
            if (!ClipLine(ref p0, ref p1, clipRect)) return;

            Color old = GUI.color;
            GUI.color = color;
            float angle = Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg;
            float length = Vector2.Distance(p0, p1);
            GUIUtility.RotateAroundPivot(angle, p0);
            GUI.DrawTexture(new Rect(p0.x, p0.y - width * 0.5f, length, width), Texture2D.whiteTexture);
            GUIUtility.RotateAroundPivot(-angle, p0);
            GUI.color = old;
        }

        private static int ComputeOutCode(Vector2 p, Rect r)
        {
            int code = 0;
            if (p.x < r.xMin) code |= 1;      // Left
            else if (p.x > r.xMax) code |= 2; // Right
            if (p.y < r.yMin) code |= 8;      // Top
            else if (p.y > r.yMax) code |= 4; // Bottom
            return code;
        }

        private static bool ClipLine(ref Vector2 p0, ref Vector2 p1, Rect clipRect)
        {
            int code0 = ComputeOutCode(p0, clipRect);
            int code1 = ComputeOutCode(p1, clipRect);
            bool accept = false;

            for (int i = 0; i < 6; i++)
            {
                if ((code0 | code1) == 0)
                {
                    accept = true;
                    break;
                }
                if ((code0 & code1) != 0)
                {
                    break;
                }

                float x = 0f, y = 0f;
                int outcodeOut = (code0 != 0) ? code0 : code1;

                float dx = p1.x - p0.x;
                float dy = p1.y - p0.y;

                if ((outcodeOut & 8) != 0) // Top
                {
                    if (Mathf.Abs(dy) < 1e-5f) break;
                    x = p0.x + dx * (clipRect.yMin - p0.y) / dy;
                    y = clipRect.yMin;
                }
                else if ((outcodeOut & 4) != 0) // Bottom
                {
                    if (Mathf.Abs(dy) < 1e-5f) break;
                    x = p0.x + dx * (clipRect.yMax - p0.y) / dy;
                    y = clipRect.yMax;
                }
                else if ((outcodeOut & 2) != 0) // Right
                {
                    if (Mathf.Abs(dx) < 1e-5f) break;
                    y = p0.y + dy * (clipRect.xMax - p0.x) / dx;
                    x = clipRect.xMax;
                }
                else if ((outcodeOut & 1) != 0) // Left
                {
                    if (Mathf.Abs(dx) < 1e-5f) break;
                    y = p0.y + dy * (clipRect.xMin - p0.x) / dx;
                    x = clipRect.xMin;
                }

                if (outcodeOut == code0)
                {
                    p0.x = x;
                    p0.y = y;
                    code0 = ComputeOutCode(p0, clipRect);
                }
                else
                {
                    p1.x = x;
                    p1.y = y;
                    code1 = ComputeOutCode(p1, clipRect);
                }
            }

            return accept;
        }

        private void HandleMapInput(Rect viewRect)
        {
            Event e = Event.current;
            if (!viewRect.Contains(e.mousePosition)) return;

            // 0. Keyboard shortcuts for Half-Width
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.LeftBracket || e.keyCode == KeyCode.Minus || e.keyCode == KeyCode.KeypadMinus)
                {
                    SetHalfWidth(halfWidth - 5f);
                    e.Use();
                }
                else if (e.keyCode == KeyCode.RightBracket || e.keyCode == KeyCode.Equals || e.keyCode == KeyCode.Plus || e.keyCode == KeyCode.KeypadPlus)
                {
                    SetHalfWidth(halfWidth + 5f);
                    e.Use();
                }
            }

            // 1. Zoom (Scroll Wheel) or Shift+Wheel fine-tuning half-width
            if (e.type == EventType.ScrollWheel)
            {
                if (e.shift)
                {
                    if (e.delta.y < 0) SetHalfWidth(halfWidth + 1f);
                    else if (e.delta.y > 0) SetHalfWidth(halfWidth - 1f);
                    e.Use();
                }
                else
                {
                    if (e.delta.y < 0 && zoom < 19) zoom++;
                    else if (e.delta.y > 0 && zoom > 10) zoom--;
                    e.Use();
                }
            }

            // 2. Pan (Middle click or Left click drag on empty space)
            if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 2))
            {
                // Check if clicking existing tower
                selectedTowerIndex = -1;
                for (int i = 0; i < towers.Count; i++)
                {
                    Vector2 sp = GeoToScreen(towers[i].lat, towers[i].lon, viewRect);
                    if (Vector2.Distance(sp, e.mousePosition) < 16f)
                    {
                        selectedTowerIndex = i;
                        isDraggingTower = true;
                        break;
                    }
                }

                if (selectedTowerIndex < 0)
                {
                    isDraggingMap = true;
                    dragStartMouse = e.mousePosition;
                    dragStartLat = centerLat;
                    dragStartLon = centerLon;
                }
                e.Use();
            }

            if (e.type == EventType.MouseDrag && isDraggingMap)
            {
                double latPerPx = (lasGeoBounds.maxLat - lasGeoBounds.minLat) / viewRect.height * (17.0 / zoom);
                double lonPerPx = (lasGeoBounds.maxLon - lasGeoBounds.minLon) / viewRect.width * (17.0 / zoom);

                centerLat = dragStartLat + (e.mousePosition.y - dragStartMouse.y) * latPerPx;
                centerLon = dragStartLon - (e.mousePosition.x - dragStartMouse.x) * lonPerPx;
                e.Use();
            }

            if (e.type == EventType.MouseDrag && isDraggingTower && selectedTowerIndex >= 0 && selectedTowerIndex < towers.Count)
            {
                double newLat, newLon;
                ScreenToGeo(e.mousePosition, viewRect, out newLat, out newLon);
                var t = towers[selectedTowerIndex];
                t.lat = newLat;
                t.lon = newLon;

                // Update Projected Easting/Northing using unified CRS projection
                double eNew, nNew;
                GISCoordinateConverter.LatLonToProjected(newLat, newLon, currentMeta, out eNew, out nNew);
                t.easting = eNew;
                t.northing = nNew;
                SyncTowersToHierarchy();
                e.Use();
            }

            if (e.type == EventType.MouseUp)
            {
                isDraggingMap = false;
                isDraggingTower = false;
            }

            // 3. Right Click: Place New Tower or Delete Existing Tower
            if (e.type == EventType.MouseDown && e.button == 1)
            {
                // Check if clicking existing tower within 20px radius to delete
                int hitTower = -1;
                for (int i = 0; i < towers.Count; i++)
                {
                    Vector2 sp = GeoToScreen(towers[i].lat, towers[i].lon, viewRect);
                    if (Vector2.Distance(sp, e.mousePosition) < 20f)
                    {
                        hitTower = i;
                        break;
                    }
                }

                if (hitTower >= 0)
                {
                    string removedName = towers[hitTower].name;
                    towers.RemoveAt(hitTower);
                    // Re-sequence remaining towers in order
                    for (int k = 0; k < towers.Count; k++)
                    {
                        towers[k].id = "t-" + (k + 1);
                        towers[k].name = string.Format("#{0:D2} 杆塔", k + 1);
                    }
                    selectedTowerIndex = -1;
                    SyncTowersToHierarchy();
                    statusMessage = string.Format("已删除杆塔 {0}，当前剩余 {1} 基", removedName, towers.Count);
                    e.Use();
                }
                else
                {
                    // Add new tower
                    double clickLat, clickLon;
                    ScreenToGeo(e.mousePosition, viewRect, out clickLat, out clickLon);

                    double easting, northing;
                    GISCoordinateConverter.LatLonToProjected(clickLat, clickLon, currentMeta, out easting, out northing);

                    int nextIdx = towers.Count + 1;
                    string tName = string.Format("#{0:D2} 杆塔", nextIdx);
                    var newMarker = new TowerMarker("t-" + nextIdx, tName, easting, northing, currentMeta != null ? currentMeta.alt : 0.0, clickLat, clickLon);
                    towers.Add(newMarker);
                    selectedTowerIndex = towers.Count - 1;
                    SyncTowersToHierarchy();

                    statusMessage = string.Format("已添加杆塔: {0} ({1:F4}°N, {2:F4}°E)", tName, clickLat, clickLon);
                    e.Use();
                }
            }
        }

        private void SyncTowersToHierarchy()
        {
            CorridorHierarchyManager.SyncMarkedCorridorSegments(province, city, lineName, towers);
        }

        private void ImportKmlTowers(string path)
        {
            try
            {
                string text = File.ReadAllText(path);
                // Look for <coordinates>lon,lat,alt ...</coordinates>
                var matches = System.Text.RegularExpressions.Regex.Matches(text, @"<coordinates>([\s\S]*?)<\/coordinates>");
                int count = 0;
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    string rawCoords = m.Groups[1].Value.Trim();
                    string[] lines = rawCoords.Split(new char[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        string[] parts = line.Split(',');
                        if (parts.Length >= 2)
                        {
                            double lo, la, el = currentMeta.alt;
                            if (double.TryParse(parts[0], out lo) && double.TryParse(parts[1], out la))
                            {
                                if (parts.Length >= 3) double.TryParse(parts[2], out el);
                                double e, n;
                                GISCoordinateConverter.LatLonToProjected(la, lo, currentMeta, out e, out n);
                                count++;
                                towers.Add(new TowerMarker("t-" + count, string.Format("#{0:D2} 杆塔", count), e, n, el, la, lo));
                            }
                        }
                    }
                }
                statusMessage = string.Format("已从 KML 成功导入 {0} 基杆塔航点", count);
                SyncTowersToHierarchy();
            }
            catch (Exception ex)
            {
                statusMessage = "KML 解析失败: " + ex.Message;
            }
        }

        private string ResolveSourceDirectory()
        {
            // 1. Try currentMeta
            if (currentMeta != null && !string.IsNullOrEmpty(currentMeta.filePath))
            {
                try
                {
                    string dir = Path.GetDirectoryName(currentMeta.filePath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return dir;
                }
                catch { }
            }

            // 2. Try batchMetas
            if (batchMetas != null)
            {
                foreach (var bm in batchMetas)
                {
                    if (bm != null && !string.IsNullOrEmpty(bm.filePath))
                    {
                        try
                        {
                            string dir = Path.GetDirectoryName(bm.filePath);
                            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return dir;
                        }
                        catch { }
                    }
                }
            }

            // 3. Try masterCRS
            if (masterCRS != null && !string.IsNullOrEmpty(masterCRS.filePath))
            {
                try
                {
                    string dir = Path.GetDirectoryName(masterCRS.filePath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return dir;
                }
                catch { }
            }

            // 4. Fallback to common directory
            if (Directory.Exists("E:/unity/点云")) return "E:/unity/点云";
            return Application.persistentDataPath;
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unnamed";
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in invalid)
            {
                name = name.Replace(c, '_');
            }
            return name.Replace(" ", "_");
        }

        private async void StartStreamingCutPipeline()
        {
            if (towers.Count < 2)
            {
                statusMessage = "⚠️ 至少需要标定 2 基杆塔才能进行分段切割！";
                if (CorridorHUD.Instance != null) CorridorHUD.Instance.ShowToast("至少需要标定 2 基杆塔");
                return;
            }

            if (currentMeta == null)
            {
                currentMeta = masterCRS ?? (batchMetas != null && batchMetas.Count > 0 ? batchMetas[0] : null);
                if (currentMeta == null)
                {
                    statusMessage = "❌ 缺失点云坐标系基准";
                    return;
                }
            }

            isCutting = true;
            cutProgress = 0.05f;
            statusMessage = "正在初始化分段几何包络...";
            cts = new CancellationTokenSource();

            string baseDir = ResolveSourceDirectory();
            string outputDir = Path.Combine(baseDir, "Corridors_" + DateTime.Now.ToString("yyyyMMdd_HHmm"));
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            activeTasks.Clear();
            for (int i = 0; i < towers.Count - 1; i++)
            {
                var ta = towers[i];
                var tb = towers[i + 1];
                string sName = string.Format("{0} - {1} 档距", ta.name, tb.name);
                string cleanLineName = SanitizeFileName(string.IsNullOrEmpty(lineName) ? "Corridor" : lineName);
                string cleanSegName = SanitizeFileName(sName);
                string outPath = Path.Combine(outputDir, string.Format("{0}_{1}.las", cleanLineName, cleanSegName));

                var task = new CutSegmentTask
                {
                    index = i,
                    segmentName = sName,
                    towerA = ta,
                    towerB = tb,
                    halfWidth = this.halfWidth,
                    longitudinalExt = 15.0,
                    outputFilePath = outPath
                };
                activeTasks.Add(task);
            }

            try
            {
                var inputList = (batchMetas != null && batchMetas.Count > 0) ? batchMetas : new List<LasImportMetadata> { currentMeta };
                var master = masterCRS ?? currentMeta;

                await LasStreamCutter.ExecuteStreamingCutBatchAsync(
                    inputList,
                    master,
                    activeTasks,
                    province,
                    city,
                    lineName,
                    (pct, msg) =>
                    {
                        cutProgress = pct;
                        statusMessage = msg;
                    },
                    (completedTask) =>
                    {
                        // 核心：切完一个档距，立即主线程上树！首个切出的档距自动载入 3D 场景
                        bool loadFirst = (completedTask.index == 0);
                        UnityLibrary.MainThread.Call(() =>
                        {
                            OnSingleSegmentCompleted(completedTask, loadFirst);
                        });
                    },
                    cts.Token
                );

                statusMessage = string.Format("🎉 全部 {0} 个档距走廊切割完成并已成功挂载！", activeTasks.Count);
                if (CorridorHUD.Instance != null)
                {
                    CorridorHUD.Instance.ShowToast(string.Format("🎉 全部 {0} 个廊道切分完成并已成功上树！", activeTasks.Count));
                }
                Close();
            }
            catch (Exception ex)
            {
                statusMessage = "❌ 切割过程异常: " + ex.Message;
                Debug.LogError("[CorridorCutter2DWindow] Cut failed: " + ex);
            }
            finally
            {
                isCutting = false;
            }
        }

        private void OnSingleSegmentCompleted(CutSegmentTask task, bool loadIntoViewer)
        {
            if (!task.isCompleted || task.pointCount <= 0) return;

            // 1. 动态挂载到对应的 省 -> 市 -> 线路 -> 档距节点
            float length = (float)Math.Round(Math.Sqrt(
                Math.Pow(task.towerB.easting - task.towerA.easting, 2) +
                Math.Pow(task.towerB.northing - task.towerA.northing, 2)
            ));

            CorridorHierarchyManager.AddOrUpdateSegment(
                province,
                city,
                lineName,
                task.segmentName,
                task.outputFilePath,
                task.bounds,
                task.pointCount,
                task.towerA.lat,
                task.towerA.lon,
                task.towerA.name,
                task.towerB.name,
                length
            );

            // 2. 如果是首个切出的档距，自动通过现有 LasImporter 载入 3D 视口，即刻可见！
            if (loadIntoViewer)
            {
                var importer = FindObjectOfType<LasImporter>();
                if (importer != null)
                {
                    importer.Load(task.outputFilePath);
                }
            }

            Debug.Log(string.Format("[CorridorCutter2DWindow] 档距节点已上树: {0} ({1:N0} 点)", task.segmentName, task.pointCount));
        }
    }
}
