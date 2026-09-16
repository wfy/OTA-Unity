using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace OTA.Corridor.Preprocess
{
    public enum MapSourceType
    {
        ArcGIS_Imagery = 0,     // ESRI ArcGIS 全球高分辨率卫星影像 (WGS84标准坐标，零偏移，免Key)
        AutoNavi_Satellite = 1, // 高德卫星影像 (受国内火星坐标GCJ-02偏移影响)
        Tianditu_Satellite = 2, // 天地图卫星 WMTS (需有效 Token)
        Bing_Aerial = 3         // 微软必应高分卫星影像 (0.3m~0.5m超高清，WGS84无偏，直连免Key)
    }

    /// <summary>
    /// Multi-source GIS satellite tile provider for Unity.
    /// Supports ArcGIS World Imagery (WGS84 unshifted), AutoNavi, and Tianditu WMTS.
    /// Provides local disk caching, memory LRU, automatic invalid-key fallback,
    /// and event notification upon tile updates.
    /// </summary>
    public class TiandituTileProvider : MonoBehaviour
    {
        public static TiandituTileProvider Instance { get; private set; }

        public static event Action OnTileUpdated;

        [Header("Tile Service Configuration")]
        public MapSourceType defaultSource = MapSourceType.Bing_Aerial;
        public string tiandituToken = "";
        public bool enableDiskCache = true;
        public int memoryCacheCapacity = 256;

        private string cacheDirectory;
        private readonly Dictionary<string, Texture2D> memoryCache = new Dictionary<string, Texture2D>();
        private readonly List<string> lruKeys = new List<string>();
        private readonly HashSet<string> pendingRequests = new HashSet<string>();
        private readonly Dictionary<string, float> failedCooldowns = new Dictionary<string, float>();

        private Texture2D fallbackTexture;
        public static bool HadTiandituTokenError { get; private set; } = false;

        public static string GetSourcePrefix(MapSourceType source)
        {
            switch (source)
            {
                case MapSourceType.ArcGIS_Imagery: return "arcgis";
                case MapSourceType.AutoNavi_Satellite: return "amap";
                case MapSourceType.Tianditu_Satellite: return "tianditu";
                case MapSourceType.Bing_Aerial: return "bing";
                default: return source.ToString().ToLower();
            }
        }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            defaultSource = MapSourceType.Bing_Aerial;

            // Load saved user token if available
            tiandituToken = PlayerPrefs.GetString("TiandituToken", "");

            cacheDirectory = Path.Combine(Application.persistentDataPath, "TiandituCache");
            if (!Directory.Exists(cacheDirectory))
            {
                try { Directory.CreateDirectory(cacheDirectory); }
                catch (Exception ex) { Debug.LogWarning("[TiandituTileProvider] Cache dir creation failed: " + ex.Message); }
            }
            else
            {
                // Purge legacy numeric cache files (e.g. 0_*, 1_*) from old versions to eliminate map source contamination
                CleanLegacyCache();
            }

            CreateFallbackTexture();
        }

        private void CleanLegacyCache()
        {
            try
            {
                if (!Directory.Exists(cacheDirectory)) return;
                string[] legacyFiles = Directory.GetFiles(cacheDirectory, "*_*.png");
                foreach (var file in legacyFiles)
                {
                    string fname = Path.GetFileName(file);
                    // If filename starts with a single digit followed by underscore (e.g. 0_16_...)
                    if (fname.Length >= 2 && char.IsDigit(fname[0]) && fname[1] == '_')
                    {
                        File.Delete(file);
                    }
                    // Purge Bing placeholder tiles (4489 bytes slashed-camera image)
                    else if (fname.StartsWith("bing_") && new FileInfo(file).Length == 4489)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch { }
        }

        public void ClearAllDiskCache()
        {
            try
            {
                if (Directory.Exists(cacheDirectory))
                {
                    Directory.Delete(cacheDirectory, true);
                    Directory.CreateDirectory(cacheDirectory);
                }
                memoryCache.Clear();
                lruKeys.Clear();
                failedCooldowns.Clear();
                Debug.Log("[TiandituTileProvider] 已完全清空本地瓦片缓存");
                OnTileUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[TiandituTileProvider] 清空缓存失败: " + ex.Message);
            }
        }

        private void CreateFallbackTexture()
        {
            fallbackTexture = new Texture2D(64, 64, TextureFormat.RGB24, false);
            Color dark = new Color(0.18f, 0.22f, 0.26f);
            Color grid = new Color(0.25f, 0.30f, 0.35f);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    bool isBorder = (x == 0 || y == 0 || x == 63 || y == 63);
                    fallbackTexture.SetPixel(x, y, isBorder ? grid : dark);
                }
            }
            fallbackTexture.Apply();
        }

        public Texture2D GetTile(int zoom, int tileX, int tileY, MapSourceType source = MapSourceType.Bing_Aerial, Action<Texture2D> onTileLoaded = null)
        {
            string key = string.Format("{0}_{1}_{2}_{3}", GetSourcePrefix(source), zoom, tileX, tileY);

            // 1. Memory Cache Check
            Texture2D tex;
            if (memoryCache.TryGetValue(key, out tex) && tex != null)
            {
                TouchLru(key);
                return tex;
            }

            // 2. Local Disk Cache Check
            string localFilePath = Path.Combine(cacheDirectory, key + ".png");
            if (enableDiskCache && File.Exists(localFilePath))
            {
                try
                {
                    byte[] fileBytes = File.ReadAllBytes(localFilePath);
                    if (fileBytes != null && fileBytes.Length > 200) // Ensure valid image file, not error text
                    {
                        // Guard against Bing placeholder image (4489 bytes slashed-camera placeholder)
                        if (key.StartsWith("bing_") && fileBytes.Length == 4489)
                        {
                            try { File.Delete(localFilePath); } catch { }
                            return fallbackTexture;
                        }

                        Texture2D diskTex = new Texture2D(256, 256, TextureFormat.RGB24, false);
                        if (diskTex.LoadImage(fileBytes))
                        {
                            diskTex.wrapMode = TextureWrapMode.Clamp;
                            diskTex.filterMode = FilterMode.Bilinear;
                            CacheTexture(key, diskTex);
                            return diskTex;
                        }
                    }
                    else
                    {
                        // Clean up invalid/corrupt cache file
                        try { File.Delete(localFilePath); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[TiandituTileProvider] Failed to read disk tile: " + ex.Message);
                }
            }

            // 3. Request from Network Asynchronously
            if (failedCooldowns.TryGetValue(key, out float retryTime))
            {
                if (Time.realtimeSinceStartup < retryTime)
                {
                    return fallbackTexture;
                }
                else
                {
                    failedCooldowns.Remove(key);
                }
            }

            if (!pendingRequests.Contains(key))
            {
                StartCoroutine(DownloadTileRoutine(source, zoom, tileX, tileY, key, localFilePath, onTileLoaded));
            }

            // Return fallback placeholder while loading
            return fallbackTexture;
        }

        private IEnumerator DownloadTileRoutine(MapSourceType source, int zoom, int tileX, int tileY, string key, string savePath, Action<Texture2D> onLoaded)
        {
            pendingRequests.Add(key);

            string url = BuildTileUrl(source, zoom, tileX, tileY);

            using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
            {
                req.certificateHandler = new BypassCertificate();
                req.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                req.timeout = 8;
                yield return req.SendWebRequest();

                pendingRequests.Remove(key);

                if (!req.isNetworkError && !req.isHttpError)
                {
                    byte[] rawData = req.downloadHandler.data;

                    // 1. Guard against Bing "No Imagery Available" placeholder (4489 bytes slashed-camera placeholder)
                    if (source == MapSourceType.Bing_Aerial && rawData != null)
                    {
                        string tileInfo = req.GetResponseHeader("X-VE-Tile-Info");
                        if (rawData.Length == 4489 || (tileInfo != null && tileInfo.IndexOf("no-tile", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            failedCooldowns[key] = Time.realtimeSinceStartup + 120f;
                            yield break; // Drop placeholder, do not cache or render
                        }
                    }

                    // 2. Check if content is actually image or an error string (e.g. Tianditu returns HTTP 200 with JSON error)
                    bool isErrorJson = false;
                    if (rawData != null && rawData.Length < 300)
                    {
                        string str = System.Text.Encoding.UTF8.GetString(rawData);
                        if (str.Contains("非法key") || str.Contains("code") || str.Contains("resolve"))
                        {
                            isErrorJson = true;
                        }
                    }

                    if (isErrorJson)
                    {
                        failedCooldowns[key] = Time.realtimeSinceStartup + 30f;
                        HadTiandituTokenError = true;
                        Debug.LogWarning("[TiandituTileProvider] 天地图 Token 无效或未授权，建议切换至 ArcGIS 卫星源！");

                        // Auto-fallback to ArcGIS for this tile immediately
                        if (source == MapSourceType.Tianditu_Satellite)
                        {
                            StartCoroutine(DownloadTileRoutine(MapSourceType.ArcGIS_Imagery, zoom, tileX, tileY,
                                string.Format("{0}_{1}_{2}_{3}", GetSourcePrefix(MapSourceType.ArcGIS_Imagery), zoom, tileX, tileY),
                                savePath.Replace(GetSourcePrefix(source), GetSourcePrefix(MapSourceType.ArcGIS_Imagery)),
                                onLoaded));
                        }
                    }
                    else
                    {
                        Texture2D downloadedTex = DownloadHandlerTexture.GetContent(req);
                        if (downloadedTex != null)
                        {
                            downloadedTex.wrapMode = TextureWrapMode.Clamp;
                            downloadedTex.filterMode = FilterMode.Bilinear;
                            CacheTexture(key, downloadedTex);

                            if (enableDiskCache)
                            {
                                try
                                {
                                    byte[] pngBytes = downloadedTex.EncodeToPNG();
                                    if (pngBytes != null && pngBytes.Length > 0)
                                    {
                                        File.WriteAllBytes(savePath, pngBytes);
                                    }
                                }
                                catch { }
                            }

                            if (onLoaded != null)
                            {
                                onLoaded(downloadedTex);
                            }

                            if (OnTileUpdated != null)
                            {
                                OnTileUpdated();
                            }
                        }
                    }
                }
                else
                {
                    // Network or HTTP error: back off for 15s to prevent hammering failed requests every frame
                    failedCooldowns[key] = Time.realtimeSinceStartup + 15f;
                    Debug.LogWarning(string.Format("[TiandituTileProvider] 瓦片下载失败 ({0}): {1}", req.error, url));
                }
            }
        }

        private string BuildTileUrl(MapSourceType source, int zoom, int tileX, int tileY)
        {
            switch (source)
            {
                case MapSourceType.ArcGIS_Imagery:
                    // ESRI ArcGIS World Imagery (Global Standard WGS84 Web Mercator, Zero Shift)
                    // Note: Always use server.arcgisonline.com; services.arcgisonline.com is often unreachable/blocked in CN.
                    return string.Format("https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{0}/{1}/{2}", zoom, tileY, tileX);

                case MapSourceType.AutoNavi_Satellite:
                    // AutoNavi subdomains: webst01 - webst04
                    int amapSub = Math.Abs((tileX + tileY) % 4) + 1;
                    return string.Format("https://webst0{0}.is.autonavi.com/appmaptile?style=6&x={1}&y={2}&z={3}", amapSub, tileX, tileY, zoom);

                case MapSourceType.Bing_Aerial:
                    // Microsoft Bing Maps Aerial (High-Res 0.3m~0.5m, WGS84 Web Mercator, Zero Shift)
                    string quadKey = TileToQuadKey(tileX, tileY, zoom);
                    int bingSub = Math.Abs((tileX + tileY) % 4);
                    return string.Format("https://ecn.t{0}.tiles.virtualearth.net/tiles/a{1}.jpeg?g=1", bingSub, quadKey);

                case MapSourceType.Tianditu_Satellite:
                default:
                    int subDomain = Math.Abs((tileX + tileY) % 8);
                    string tk = !string.IsNullOrEmpty(tiandituToken) ? tiandituToken : "7a53be247a3240e8e45ea98444a7732a";
                    return string.Format(
                        "https://t{0}.tianditu.gov.cn/img_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=img&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILEMATRIX={1}&TILEROW={2}&TILECOL={3}&tk={4}",
                        subDomain, zoom, tileY, tileX, tk
                    );
            }
        }

        public static string TileToQuadKey(int tileX, int tileY, int zoom)
        {
            var quadKey = new System.Text.StringBuilder();
            for (int i = zoom; i > 0; i--)
            {
                int digit = 0;
                int mask = 1 << (i - 1);
                if ((tileX & mask) != 0) digit += 1;
                if ((tileY & mask) != 0) digit += 2;
                quadKey.Append(digit);
            }
            return quadKey.ToString();
        }

        private void CacheTexture(string key, Texture2D tex)
        {
            if (memoryCache.ContainsKey(key))
            {
                memoryCache[key] = tex;
                TouchLru(key);
                return;
            }

            if (memoryCache.Count >= memoryCacheCapacity && lruKeys.Count > 0)
            {
                string oldest = lruKeys[0];
                lruKeys.RemoveAt(0);
                if (memoryCache.ContainsKey(oldest))
                {
                    Texture2D oldTex = memoryCache[oldest];
                    memoryCache.Remove(oldest);
                    if (oldTex != null && oldTex != fallbackTexture)
                    {
                        Destroy(oldTex);
                    }
                }
            }

            memoryCache[key] = tex;
            lruKeys.Add(key);
        }

        private void TouchLru(string key)
        {
            lruKeys.Remove(key);
            lruKeys.Add(key);
        }

        void OnDestroy()
        {
            foreach (var kvp in memoryCache)
            {
                if (kvp.Value != null && kvp.Value != fallbackTexture)
                {
                    Destroy(kvp.Value);
                }
            }
            memoryCache.Clear();
            lruKeys.Clear();
            if (fallbackTexture != null) Destroy(fallbackTexture);
        }
    }

    /// <summary>
    /// Bypass SSL certificate verification for tile server downloads in Unity Editor / standalone.
    /// </summary>
    internal class BypassCertificate : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true;
        }
    }
}
