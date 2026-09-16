using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OTA.Corridor.Hierarchy
{
    public enum CRSType
    {
        Auto = 0,
        UTM_North = 1,
        CGCS2000_3Deg_Zone = 2,
        CGCS2000_3Deg_NoZone = 3,
        WGS84_Geo = 4,
        Local_Project = 5
    }

    [Serializable]
    public struct LasHeaderInfo
    {
        public double minX, maxX;
        public double minY, maxY;
        public double minZ, maxZ;
        public long pointCount;
        public byte versionMajor;
        public byte versionMinor;
        public string versionStr;
        public float parseTimeMs;
        public double scaleX, scaleY, scaleZ;
        public double offsetX, offsetY, offsetZ;
        public string error;

        public Vector3 Center => new Vector3(
            (float)((minX + maxX) * 0.5),
            (float)((minY + maxY) * 0.5),
            (float)((minZ + maxZ) * 0.5)
        );

        public Vector3 Size => new Vector3(
            (float)Math.Abs(maxX - minX),
            (float)Math.Abs(maxY - minY),
            (float)Math.Abs(maxZ - minZ)
        );

        public Bounds Bounds => new Bounds(Center, Size);
    }

    [Serializable]
    public class LasImportMetadata
    {
        public string filePath;
        public string fileName;
        public LasHeaderInfo header;
        public CRSType detectedCRS;
        public CRSType selectedCRS;
        public int utmZone = 50;
        public int centralMeridian = 120;
        public double lat = 30.2741;
        public double lon = 120.1551;
        public float alt = 250f;
        public string province = "浙江省";
        public string city = "杭州市";
        public string lineName = "220kV 钱塘线";
        public string segmentName = "#17 - #18 档距";
        public string startTower = "#17 耐张塔";
        public string endTower = "#18 直线塔";
        public float length = 350f;
        public string statusNote = "";
        public string matchedBy = "";
    }

    public struct CityBound
    {
        public string province;
        public string city;
        public double minLat, maxLat, minLon, maxLon;

        public CityBound(string prov, string c, double minLa, double maxLa, double minLo, double maxLo)
        {
            province = prov;
            city = c;
            minLat = minLa;
            maxLat = maxLa;
            minLon = minLo;
            maxLon = maxLo;
        }
    }

    public struct CityCenter
    {
        public string city;
        public double lat;
        public double lon;

        public CityCenter(string c, double la, double lo)
        {
            city = c;
            lat = la;
            lon = lo;
        }
    }

    /// <summary>
    /// High-performance LAS header metadata extractor, coordinate projection solver,
    /// and geographic/power grid administrative hierarchy detector.
    /// Aligned with the Web PointCloudCorridorViewer.tsx specifications.
    /// </summary>
    public static class LasMetadataDetector
    {
        public static readonly Dictionary<string, string[]> AdministrativeDivisions = new Dictionary<string, string[]>
        {
            { "浙江省", new string[] { "杭州市", "宁波市", "温州市", "嘉兴市", "湖州市", "绍兴市", "金华市", "衢州市", "台州市", "丽水市", "舟山市" } },
            { "江苏省", new string[] { "南京市", "苏州市", "无锡市", "常州市", "南通市", "徐州市", "连云港市", "淮安市", "盐城市", "扬州市", "镇江市", "泰州市", "宿迁市" } },
            { "上海市", new string[] { "上海市" } },
            { "广东省", new string[] { "广州市", "深圳市", "珠海市", "佛山市", "东莞市", "惠州市", "中山市", "湛江市", "汕头市", "江门市", "茂名市", "肇庆市", "梅州市", "清远市", "潮州市", "揭阳市" } },
            { "北京市", new string[] { "北京市" } },
            { "天津市", new string[] { "天津市" } },
            { "重庆市", new string[] { "重庆市" } },
            { "安徽省", new string[] { "合肥市", "芜湖市", "蚌埠市", "淮南市", "马鞍山市", "淮北市", "铜陵市", "安庆市", "黄山市", "滁州市", "阜阳市", "宿州市", "六安市", "亳州市", "池州市", "宣城市" } },
            { "福建省", new string[] { "福州市", "厦门市", "莆田市", "三明市", "泉州市", "漳州市", "南平市", "龙岩市", "宁德市" } },
            { "山东省", new string[] { "济南市", "青岛市", "淄博市", "枣庄市", "东营市", "烟台市", "潍坊市", "济宁市", "泰安市", "威海市", "日照市", "临沂市", "德州市", "聊城市", "滨州市", "菏泽市" } },
            { "湖北省", new string[] { "武汉市", "黄石市", "十堰市", "宜昌市", "襄阳市", "鄂州市", "荆门市", "孝感市", "荆州市", "黄冈市", "咸宁市", "随州市", "恩施州" } },
            { "湖南省", new string[] { "长沙市", "株洲市", "湘潭市", "衡阳市", "邵阳市", "岳阳市", "常德市", "张家界市", "益阳市", "郴州市", "永州市", "怀化市", "娄底市", "湘西州" } },
            { "四川省", new string[] { "成都市", "自贡市", "攀枝花市", "泸州市", "德阳市", "绵阳市", "广元市", "遂宁市", "内江市", "乐山市", "南充市", "眉山市", "宜宾市", "广安市", "达州市", "雅安市", "巴中市", "资阳市" } },
            { "河南省", new string[] { "郑州市", "开封市", "洛阳市", "平顶山市", "安阳市", "鹤壁市", "新乡市", "焦作市", "濮阳市", "许昌市", "漯河市", "三门峡市", "南阳市", "商丘市", "信阳市", "周口市", "驻马店市" } },
            { "陕西省", new string[] { "西安市", "铜川市", "宝鸡市", "咸阳市", "渭南市", "延安市", "汉中市", "榆林市", "安康市", "商洛市" } },
            { "河北省", new string[] { "石家庄市", "唐山市", "秦皇岛市", "邯郸市", "邢台市", "保定市", "张家口市", "承德市", "沧州市", "廊坊市", "衡水市" } },
            { "江西省", new string[] { "南昌市", "景德镇市", "萍乡市", "九江市", "新余市", "鹰潭市", "赣州市", "吉安市", "宜春市", "抚州市", "上饶市" } },
            { "贵州省", new string[] { "贵阳市", "遵义市", "安顺市", "毕节市", "铜仁市", "六盘水市" } },
            { "云南省", new string[] { "昆明市", "曲靖市", "玉溪市", "保山市", "昭通市", "丽江市", "普洱市", "大理州" } },
            { "广西壮族自治区", new string[] { "南宁市", "桂林市", "柳州市", "北海市", "梧州市", "玉林市" } },
            { "内蒙古自治区", new string[] { "呼和浩特市", "包头市", "鄂尔多斯市", "赤峰市", "通辽市" } },
            { "辽宁省", new string[] { "沈阳市", "大连市", "鞍山市", "抚顺市", "本溪市", "锦州市" } },
            { "吉林省", new string[] { "长春市", "吉林市", "四平市", "延边州" } },
            { "黑龙江省", new string[] { "哈尔滨市", "齐齐哈尔市", "大庆市", "牡丹江市", "佳木斯市" } },
            { "新疆维吾尔自治区", new string[] { "乌鲁木齐市", "克拉玛依市", "昌吉州", "伊犁州" } },
            { "甘肃省", new string[] { "兰州市", "酒泉市", "天水市", "嘉峪关市" } },
            { "青海省", new string[] { "西宁市", "海东市" } },
            { "宁夏回族自治区", new string[] { "银川市", "石嘴山市", "吴忠市" } },
            { "海南省", new string[] { "海口市", "三亚市" } }
        };

        public static readonly CityBound[] CityBounds = new CityBound[]
        {
            // 浙江省
            new CityBound("浙江省", "杭州市", 29.18, 30.56, 118.35, 120.72),
            new CityBound("浙江省", "宁波市", 28.85, 30.33, 120.92, 122.28),
            new CityBound("浙江省", "温州市", 27.05, 28.53, 119.62, 121.25),
            new CityBound("浙江省", "嘉兴市", 30.25, 31.03, 120.30, 121.27),
            new CityBound("浙江省", "湖州市", 30.38, 31.18, 119.23, 120.48),
            new CityBound("浙江省", "绍兴市", 29.23, 30.28, 119.88, 121.10),
            new CityBound("浙江省", "金华市", 28.53, 29.70, 119.23, 120.78),
            new CityBound("浙江省", "衢州市", 28.25, 29.50, 118.02, 119.33),
            new CityBound("浙江省", "台州市", 28.02, 29.13, 120.02, 121.93),
            new CityBound("浙江省", "丽水市", 27.42, 28.95, 118.68, 120.15),
            new CityBound("浙江省", "舟山市", 29.53, 30.85, 121.52, 123.25),

            // 江苏省
            new CityBound("江苏省", "南京市", 31.23, 32.62, 118.35, 119.23),
            new CityBound("江苏省", "苏州市", 30.75, 32.03, 119.92, 121.33),
            new CityBound("江苏省", "无锡市", 31.12, 32.08, 119.52, 120.60),
            new CityBound("江苏省", "常州市", 31.15, 32.07, 119.13, 120.20),
            new CityBound("江苏省", "南通市", 31.68, 32.72, 120.20, 121.92),
            new CityBound("江苏省", "徐州市", 33.72, 34.93, 116.37, 118.67),

            // 上海市
            new CityBound("上海市", "上海市", 30.68, 31.88, 120.85, 122.20),

            // 广东省
            new CityBound("广东省", "广州市", 22.43, 23.93, 112.95, 114.05),
            new CityBound("广东省", "深圳市", 22.40, 22.88, 113.75, 114.62),

            // 北京市、天津市、重庆市
            new CityBound("北京市", "北京市", 39.43, 41.05, 115.42, 117.50),
            new CityBound("天津市", "天津市", 38.57, 40.25, 116.72, 118.07),
            new CityBound("重庆市", "重庆市", 28.17, 32.20, 105.28, 110.20),

            // 四川省、湖北省、湖南省、山东省、安徽省
            new CityBound("四川省", "成都市", 30.08, 31.43, 102.98, 104.90),
            new CityBound("湖北省", "武汉市", 29.97, 31.37, 113.70, 115.08),
            new CityBound("湖南省", "长沙市", 27.85, 28.67, 111.88, 114.25),
            new CityBound("山东省", "济南市", 36.03, 37.53, 116.18, 117.88),
            new CityBound("山东省", "青岛市", 35.58, 37.15, 119.50, 121.15),
            new CityBound("安徽省", "合肥市", 30.95, 32.63, 116.68, 117.97),
            new CityBound("福建省", "福州市", 25.25, 26.65, 118.37, 119.98),
            new CityBound("河南省", "郑州市", 34.27, 34.97, 112.72, 114.23),
            new CityBound("陕西省", "西安市", 33.70, 34.75, 107.67, 109.82)
        };

        public static readonly CityBound[] ProvinceFallbackBounds = new CityBound[]
        {
            new CityBound("浙江省", "杭州市", 27.0, 31.2, 118.0, 123.0),
            new CityBound("江苏省", "南京市", 30.7, 35.1, 116.3, 122.0),
            new CityBound("上海市", "上海市", 30.6, 31.9, 120.8, 122.3),
            new CityBound("安徽省", "合肥市", 29.4, 34.6, 114.9, 119.6),
            new CityBound("福建省", "福州市", 23.5, 28.3, 115.8, 120.8),
            new CityBound("江西省", "南昌市", 24.5, 30.1, 113.6, 118.5),
            new CityBound("山东省", "济南市", 34.4, 38.4, 114.8, 122.7),
            new CityBound("广东省", "广州市", 20.2, 25.5, 109.6, 117.3),
            new CityBound("广西壮族自治区", "南宁市", 20.9, 26.4, 104.4, 112.1),
            new CityBound("北京市", "北京市", 39.4, 41.1, 115.4, 117.5),
            new CityBound("天津市", "天津市", 38.5, 40.3, 116.7, 118.1),
            new CityBound("河北省", "石家庄市", 36.0, 42.6, 113.4, 119.8),
            new CityBound("山西省", "太原市", 34.6, 40.7, 110.2, 114.6),
            new CityBound("河南省", "郑州市", 31.4, 36.4, 110.4, 116.6),
            new CityBound("湖北省", "武汉市", 29.0, 33.3, 108.4, 116.1),
            new CityBound("湖南省", "长沙市", 24.6, 30.1, 108.8, 114.2),
            new CityBound("四川省", "成都市", 26.0, 34.3, 97.4, 108.5),
            new CityBound("重庆市", "重庆市", 28.2, 32.2, 105.3, 110.2),
            new CityBound("贵州省", "贵阳市", 24.6, 29.2, 103.6, 109.6),
            new CityBound("云南省", "昆明市", 21.1, 29.3, 97.5, 106.2),
            new CityBound("陕西省", "西安市", 31.7, 39.6, 105.5, 111.2)
        };

        public static readonly Dictionary<string, CityCenter[]> ProvinceCityCenters = new Dictionary<string, CityCenter[]>
        {
            {
                "浙江省", new CityCenter[]
                {
                    new CityCenter("杭州市", 30.2741, 120.1551),
                    new CityCenter("宁波市", 29.8683, 121.5440),
                    new CityCenter("温州市", 28.0006, 120.6994),
                    new CityCenter("嘉兴市", 30.7627, 120.7555),
                    new CityCenter("湖州市", 30.8943, 120.0868),
                    new CityCenter("绍兴市", 30.0024, 120.5821),
                    new CityCenter("金华市", 29.0791, 119.6474),
                    new CityCenter("衢州市", 28.9358, 118.8729),
                    new CityCenter("舟山市", 29.9852, 122.2072),
                    new CityCenter("台州市", 28.6564, 121.4206),
                    new CityCenter("丽水市", 28.4676, 119.9230)
                }
            }
        };

        /// <summary>
        /// Fast sub-millisecond LAS Header parsing without loading entire point buffers.
        /// </summary>
        public static LasHeaderInfo FastParseHeader(string lasPath)
        {
            var header = new LasHeaderInfo { error = null };
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (!File.Exists(lasPath))
                {
                    header.error = "File does not exist: " + lasPath;
                    return header;
                }

                using (var fs = new FileStream(lasPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(fs))
                {
                    if (fs.Length < 227)
                    {
                        header.error = "File too small for LAS header: " + fs.Length;
                        return header;
                    }

                    var sig = new string(reader.ReadChars(4));
                    if (sig != "LASF")
                    {
                        header.error = "Invalid signature: " + sig;
                        return header;
                    }

                    fs.Seek(24, SeekOrigin.Begin);
                    header.versionMajor = reader.ReadByte();
                    header.versionMinor = reader.ReadByte();
                    header.versionStr = "LAS " + header.versionMajor + "." + header.versionMinor;

                    fs.Seek(107, SeekOrigin.Begin);
                    header.pointCount = reader.ReadUInt32();

                    // Check for 64-bit point count in LAS 1.4 if legacy is 0
                    if (header.pointCount == 0 && header.versionMinor >= 4 && fs.Length >= 255)
                    {
                        fs.Seek(247, SeekOrigin.Begin);
                        header.pointCount = (long)reader.ReadUInt64();
                    }

                    // Scale & Offset (bytes 131..178)
                    fs.Seek(131, SeekOrigin.Begin);
                    header.scaleX = reader.ReadDouble();
                    header.scaleY = reader.ReadDouble();
                    header.scaleZ = reader.ReadDouble();
                    header.offsetX = reader.ReadDouble();
                    header.offsetY = reader.ReadDouble();
                    header.offsetZ = reader.ReadDouble();

                    // Bounding Box (bytes 179..226)
                    header.maxX = reader.ReadDouble();
                    header.minX = reader.ReadDouble();
                    header.maxY = reader.ReadDouble();
                    header.minY = reader.ReadDouble();
                    header.maxZ = reader.ReadDouble();
                    header.minZ = reader.ReadDouble();
                }
            }
            catch (Exception ex)
            {
                header.error = ex.Message;
            }

            sw.Stop();
            header.parseTimeMs = (float)sw.Elapsed.TotalMilliseconds;
            return header;
        }

        /// <summary>
        /// Detect full import metadata including coordinates, CRS, province, city, line, and segment.
        /// </summary>
        public static LasImportMetadata DetectMetadata(string lasPath)
        {
            var meta = new LasImportMetadata
            {
                filePath = lasPath.Replace('\\', '/'),
                fileName = Path.GetFileName(lasPath)
            };

            meta.header = FastParseHeader(lasPath);
            if (!string.IsNullOrEmpty(meta.header.error))
            {
                meta.statusNote = "⚠️ 解析错误: " + meta.header.error;
                return meta;
            }

            double cx = (meta.header.minX + meta.header.maxX) * 0.5;
            double cy = (meta.header.minY + meta.header.maxY) * 0.5;
            meta.alt = (float)((meta.header.minZ + meta.header.maxZ) * 0.5);

            // 1. CRS Auto-Detection
            int recommendedZone, recommendedMeridian;
            meta.detectedCRS = DetectCRS(cx, cy, out recommendedZone, out recommendedMeridian);
            meta.selectedCRS = meta.detectedCRS;
            meta.utmZone = recommendedZone;
            meta.centralMeridian = recommendedMeridian;

            // 2. Resolve (lat, lon)
            CalculateGeoLocation(meta);

            // 3. Resolve Province, City, Line, Segment
            ResolveHierarchy(meta);

            return meta;
        }

        public static CRSType DetectCRS(double cx, double cy, out int utmZone, out int centralMeridian)
        {
            utmZone = 50;
            centralMeridian = 120;

            // WGS84 Geodetic Longitude/Latitude (China bounds: Lon 70~140, Lat 15~55)
            if (cx >= 70.0 && cx <= 140.0 && cy >= 15.0 && cy <= 55.0)
            {
                return CRSType.WGS84_Geo;
            }
            // Inverted Lat/Lon
            if (cy >= 70.0 && cy <= 140.0 && cx >= 15.0 && cx <= 55.0)
            {
                return CRSType.WGS84_Geo;
            }

            // CGCS2000 3-degree with Belt/Zone Number (e.g. 40523100 -> Zone 40, CM 120E)
            if (cx > 10000000.0 && cy > 1000000.0)
            {
                int zone = (int)(cx / 1000000.0);
                centralMeridian = zone * 3;
                utmZone = (int)Math.Floor(((centralMeridian + 180) / 6.0)) + 1;
                return CRSType.CGCS2000_3Deg_Zone;
            }

            // Projected 6-digit Easting (100,000 ~ 900,000) and 7-digit Northing (>1,000,000)
            if (cx >= 100000.0 && cx <= 900000.0 && cy > 1000000.0)
            {
                // East China / Zhejiang default Zone 50N (117E), CGCS2000 120E
                utmZone = 50;
                centralMeridian = 120;
                return CRSType.UTM_North;
            }

            // Small engineering coordinates
            return CRSType.Local_Project;
        }

        public static void CalculateGeoLocation(LasImportMetadata meta)
        {
            double cx = (meta.header.minX + meta.header.maxX) * 0.5;
            double cy = (meta.header.minY + meta.header.maxY) * 0.5;

            switch (meta.selectedCRS)
            {
                case CRSType.Auto:
                case CRSType.UTM_North:
                    UtmNorthToLatLon(cx, cy, meta.utmZone, out meta.lat, out meta.lon);
                    meta.statusNote = string.Format("🤖 WGS84 UTM Zone {0}N: ({1:F4}°N, {2:F4}°E)", meta.utmZone, meta.lat, meta.lon);
                    break;

                case CRSType.CGCS2000_3Deg_Zone:
                    int zone = (int)(cx / 1000000.0);
                    double pureEasting = cx % 1000000.0;
                    int cm = zone > 0 ? zone * 3 : meta.centralMeridian;
                    GaussKrugerToLatLon(pureEasting, cy, cm, out meta.lat, out meta.lon);
                    meta.statusNote = string.Format("📐 CGCS2000 3度带 (第{0}带, {1}°E): ({2:F4}°N, {3:F4}°E)", zone, cm, meta.lat, meta.lon);
                    break;

                case CRSType.CGCS2000_3Deg_NoZone:
                    GaussKrugerToLatLon(cx, cy, meta.centralMeridian, out meta.lat, out meta.lon);
                    meta.statusNote = string.Format("📐 CGCS2000 3度带 (中央子午线{0}°E): ({1:F4}°N, {2:F4}°E)", meta.centralMeridian, meta.lat, meta.lon);
                    break;

                case CRSType.WGS84_Geo:
                    if (cx >= 70 && cx <= 140) { meta.lon = cx; meta.lat = cy; }
                    else { meta.lon = cy; meta.lat = cx; }
                    meta.statusNote = string.Format("🗺️ 经纬度直读: ({0:F4}°N, {1:F4}°E)", meta.lat, meta.lon);
                    break;

                case CRSType.Local_Project:
                default:
                    meta.lat = 30.2741;
                    meta.lon = 120.1551;
                    meta.statusNote = string.Format("📏 独立局部工程坐标 (X={0:F0}m, Y={1:F0}m)，无绝对地理坐标", cx, cy);
                    break;
            }
        }

        public static void ResolveHierarchy(LasImportMetadata meta)
        {
            string matchedProv = "浙江省";
            string matchedCity = "杭州市";
            string matchedBy = "默认推断";

            // 1. Spatial Match by (lat, lon)
            if (meta.selectedCRS != CRSType.Local_Project && meta.lat >= 10 && meta.lat <= 55 && meta.lon >= 70 && meta.lon <= 140)
            {
                if (LookupProvinceAndCity(meta.lat, meta.lon, meta.fileName, out matchedProv, out matchedCity, out matchedBy))
                {
                    meta.matchedBy = matchedBy;
                }
            }

            // 2. Filename extraction for Province/City if spatial was generic
            string fileProv, fileCity, fileLine, fileSeg;
            ParseFilename(meta.fileName, out fileProv, out fileCity, out fileLine, out fileSeg);
            if (!string.IsNullOrEmpty(fileProv)) matchedProv = fileProv;
            if (!string.IsNullOrEmpty(fileCity)) matchedCity = fileCity;

            meta.province = matchedProv;
            meta.city = matchedCity;
            meta.lineName = !string.IsNullOrEmpty(fileLine) ? fileLine : ("220kV " + matchedCity.Replace("市", "") + "线");
            meta.segmentName = !string.IsNullOrEmpty(fileSeg) ? fileSeg : ("#01 - #02 档距");

            // Extract towers if segment matches #XX - #YY
            ParseTowers(meta.segmentName, out meta.startTower, out meta.endTower);
            meta.length = Mathf.Max(100f, meta.header.Size.magnitude > 50f ? (float)Math.Round(meta.header.Size.magnitude) : 350f);

            meta.statusNote += string.Format(" ➔ 空间挂接: {0} · {1} · {2} · {3}", meta.province, meta.city, meta.lineName, meta.segmentName);
        }

        public static bool LookupProvinceAndCity(double lat, double lon, string filename, out string province, out string city, out string matchedBy)
        {
            province = "浙江省";
            city = "杭州市";
            matchedBy = "";

            // A. Precise City Bounding Box Match
            foreach (var b in CityBounds)
            {
                if (lat >= b.minLat && lat <= b.maxLat && lon >= b.minLon && lon <= b.maxLon)
                {
                    province = b.province;
                    city = b.city;
                    matchedBy = "精确匹配城市地理界线";
                    return true;
                }
            }

            // B. Province Fallback Match + Nearest City Center
            foreach (var p in ProvinceFallbackBounds)
            {
                if (lat >= p.minLat && lat <= p.maxLat && lon >= p.minLon && lon <= p.maxLon)
                {
                    province = p.province;
                    city = p.city;

                    CityCenter[] centers;
                    if (ProvinceCityCenters.TryGetValue(p.province, out centers) && centers != null && centers.Length > 0)
                    {
                        double minSq = double.MaxValue;
                        foreach (var c in centers)
                        {
                            double dLat = lat - c.lat;
                            double dLon = lon - c.lon;
                            double sq = dLat * dLat + dLon * dLon;
                            if (sq < minSq)
                            {
                                minSq = sq;
                                city = c.city;
                            }
                        }
                    }

                    matchedBy = "省份地理界线匹配";
                    return true;
                }
            }

            return false;
        }

        public static void ParseFilename(string filename, out string province, out string city, out string lineName, out string segmentName)
        {
            province = null;
            city = null;
            lineName = null;
            segmentName = null;

            if (string.IsNullOrEmpty(filename)) return;

            string clean = Path.GetFileNameWithoutExtension(filename);

            // Match Province
            foreach (var p in AdministrativeDivisions.Keys)
            {
                if (clean.Contains(p) || clean.Contains(p.Replace("省", "").Replace("市", "")))
                {
                    province = p;
                    break;
                }
            }

            // Match City
            foreach (var b in CityBounds)
            {
                string shortCity = b.city.Replace("市", "");
                if (clean.Contains(b.city) || clean.Contains(shortCity))
                {
                    city = b.city;
                    if (string.IsNullOrEmpty(province)) province = b.province;
                    break;
                }
            }

            // Match Line Name
            if (clean.Contains("钱塘")) lineName = "220kV 钱塘线";
            else if (clean.Contains("凤城") || clean.Contains("125-126")) lineName = "500kV 凤城线";
            else if (clean.Contains("紫金")) lineName = "220kV 紫金线";
            else if (clean.Contains("500kV") || clean.Contains("220kV") || clean.Contains("110kV") || clean.Contains("1000kV"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(clean, @"(\d+kV[\u4e00-\u9fa5A-Za-z0-9_]+线)");
                if (match.Success) lineName = match.Groups[1].Value;
            }

            // Match Segment Name: e.g. "17-18", "17_18", "125-126", "#01-#02"
            var segMatch = System.Text.RegularExpressions.Regex.Match(clean, @"(#?\d+)[-_~—](#?\d+)");
            if (segMatch.Success)
            {
                string t1 = segMatch.Groups[1].Value.Replace("#", "");
                string t2 = segMatch.Groups[2].Value.Replace("#", "");
                segmentName = string.Format("#{0} - #{1} 档距", t1, t2);
            }
            else
            {
                segmentName = clean.Replace("_sign", "");
            }
        }

        private static void ParseTowers(string segmentName, out string startTower, out string endTower)
        {
            startTower = "#1 杆塔";
            endTower = "#2 杆塔";
            if (string.IsNullOrEmpty(segmentName)) return;

            var match = System.Text.RegularExpressions.Regex.Match(segmentName, @"#?(\d+)\s*[-_~—]\s*#?(\d+)");
            if (match.Success)
            {
                startTower = string.Format("#{0} 铁塔", match.Groups[1].Value);
                endTower = string.Format("#{0} 铁塔", match.Groups[2].Value);
            }
        }

        /// <summary>
        /// Gauss-Kruger (CGCS2000) Inverse Projection from (Easting, Northing) to (Lat, Lon)
        /// </summary>
        public static void GaussKrugerToLatLon(double easting, double northing, double centralMeridian, out double lat, out double lon)
        {
            double pureEasting = easting - 500000.0;
            double a = 6378137.0;
            double f = 1.0 / 298.257222101;
            double e2 = 2 * f - f * f;

            double latRad = northing / a;
            double cosLat = Math.Cos(latRad);
            double N = a / Math.Sqrt(1.0 - e2 * Math.Sin(latRad) * Math.Sin(latRad));

            double dLonRad = pureEasting / (N * Math.Max(0.00001, cosLat));
            lat = latRad * 180.0 / Math.PI;
            lon = centralMeridian + (dLonRad * 180.0 / Math.PI);
        }

        /// <summary>
        /// UTM Inverse Projection from (Easting, Northing) to (Lat, Lon) on WGS84
        /// </summary>
        public static void UtmNorthToLatLon(double easting, double northing, int zone, out double lat, out double lon)
        {
            double cm = (zone * 6) - 183.0;
            double a = 6378137.0;
            double f = 1.0 / 298.257223563;
            double k0 = 0.9996;
            double e2 = 2 * f - f * f;
            double ePrime2 = e2 / (1.0 - e2);

            double x = easting - 500000.0;
            double y = northing;

            double M = y / k0;
            double mu = M / (a * (1.0 - e2 / 4.0 - 3.0 * e2 * e2 / 64.0 - 5.0 * e2 * e2 * e2 / 256.0));

            double e1 = (1.0 - Math.Sqrt(1.0 - e2)) / (1.0 + Math.Sqrt(1.0 - e2));
            double J1 = (3.0 * e1 / 2.0 - 27.0 * Math.Pow(e1, 3) / 32.0);
            double J2 = (21.0 * Math.Pow(e1, 2) / 16.0 - 55.0 * Math.Pow(e1, 4) / 32.0);
            double J3 = (151.0 * Math.Pow(e1, 3) / 96.0);
            double J4 = (1097.0 * Math.Pow(e1, 4) / 512.0);

            double fp = mu + J1 * Math.Sin(2.0 * mu) + J2 * Math.Sin(4.0 * mu) + J3 * Math.Sin(6.0 * mu) + J4 * Math.Sin(8.0 * mu);

            double sinFp = Math.Sin(fp);
            double cosFp = Math.Cos(fp);
            double tanFp = Math.Tan(fp);

            double C1 = ePrime2 * cosFp * cosFp;
            double T1 = tanFp * tanFp;
            double N1 = a / Math.Sqrt(1.0 - e2 * sinFp * sinFp);
            double R1 = a * (1.0 - e2) / Math.Pow(1.0 - e2 * sinFp * sinFp, 1.5);
            double D = x / (N1 * k0);

            double latRad = fp - (N1 * tanFp / R1) * (D * D / 2.0 - (5.0 + 3.0 * T1 + 10.0 * C1 - 4.0 * C1 * C1 - 9.0 * ePrime2) * Math.Pow(D, 4) / 24.0
                + (61.0 + 90.0 * T1 + 298.0 * C1 + 45.0 * T1 * T1 - 252.0 * ePrime2 - 3.0 * C1 * C1) * Math.Pow(D, 6) / 720.0);
            double lonRad = (D - (1.0 + 2.0 * T1 + C1) * Math.Pow(D, 3) / 6.0
                + (5.0 - 2.0 * C1 + 28.0 * T1 - 3.0 * C1 * C1 + 8.0 * ePrime2 + 24.0 * T1 * T1) * Math.Pow(D, 5) / 120.0) / cosFp;

            lat = latRad * 180.0 / Math.PI;
            lon = cm + (lonRad * 180.0 / Math.PI);
        }
    }
}
