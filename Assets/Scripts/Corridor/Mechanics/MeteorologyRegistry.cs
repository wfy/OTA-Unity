using System;
using System.Collections.Generic;
using UnityEngine;

namespace OTA.Corridor.Mechanics
{
    [Serializable]
    public class MeteorologicalZone
    {
        public string id;
        public string name;
        public float maxTemp;            // °C
        public float minTemp;            // °C
        public float avgTemp;            // °C
        public float maxWindSpeed;       // m/s
        public float designIceThickness; // mm
        public float iceDensity = 0.9f;  // g/cm³
        public float windWithIceSpeed = 10f; // m/s
        public float iceTemp = -5f;      // °C
        public float windTemp = 10f;     // °C
        public float installationTemp = 15f; // °C

        public MeteorologicalZone(string id, string name, float maxT, float minT, float avgT, float maxW, float iceThick, float windT = 10f, float iceT = -5f, float iceW = 10f, float installT = 15f)
        {
            this.id = id;
            this.name = name;
            this.maxTemp = maxT;
            this.minTemp = minT;
            this.avgTemp = avgT;
            this.maxWindSpeed = maxW;
            this.designIceThickness = iceThick;
            this.windTemp = windT;
            this.iceTemp = iceT;
            this.windWithIceSpeed = iceW;
            this.installationTemp = installT;
        }

        public List<WorkingCondition> GenerateConditions()
        {
            return new List<WorkingCondition>
            {
                new WorkingCondition("min-temp", "最低气温工况", minTemp, 0f, 0f, true),
                new WorkingCondition("max-wind", "最大风速工况", windTemp, maxWindSpeed, 0f, true),
                new WorkingCondition("max-ice",  "设计覆冰工况", iceTemp, windWithIceSpeed, designIceThickness, true),
                new WorkingCondition("avg-temp", "年均气温工况", avgTemp, 0f, 0f, true),
                new WorkingCondition("max-temp", "最高气温工况", maxTemp, 0f, 0f, false),
                new WorkingCondition("install",  "施工安装工况", installationTemp, 0f, 0f, false)
            };
        }
    }

    /// <summary>
    /// DL/T 5582-2020 附录 A 典型气象区预设库与表 9.4.2-1 / 表 13.0.1 规程标准限值检索库
    /// </summary>
    public static class MeteorologyRegistry
    {
        public static readonly MeteorologicalZone[] Zones = new MeteorologicalZone[]
        {
            new MeteorologicalZone("zone-1", "典型气象区 I 区 (轻冰低风)", 40f, -5f, 20f, 31f, 0f, 10f, -5f, 10f, 15f),
            new MeteorologicalZone("zone-2", "典型气象区 II 区 (无冰常规)", 40f, -10f, 15f, 25f, 5f, 10f, -5f, 10f, 15f),
            new MeteorologicalZone("zone-3", "典型气象区 III 区 (轻冰温和)", 40f, -10f, 15f, 22f, 5f, -5f, -5f, 10f, 15f),
            new MeteorologicalZone("zone-4", "典型气象区 IV 区 (中冰寒冷)", 40f, -20f, 10f, 22f, 5f, -5f, -5f, 10f, 15f),
            new MeteorologicalZone("zone-5", "典型气象区 V 区 (中冰严寒)", 40f, -20f, 15f, 27f, 10f, -5f, -5f, 10f, 15f),
            new MeteorologicalZone("zone-6", "典型气象区 VI 区 (重冰极寒)", 40f, -30f, 5f, 27f, 10f, -5f, -5f, 10f, 15f),
            new MeteorologicalZone("zone-7", "典型气象区 VII 区 (大风区)", 40f, -20f, 10f, 35f, 5f, -5f, -5f, 15f, 15f),
            new MeteorologicalZone("zone-8", "典型气象区 VIII 区 (极寒重冰)", 40f, -40f, 0f, 27f, 15f, -5f, -5f, 10f, 15f),
            new MeteorologicalZone("zone-9", "典型气象区 IX 区 (东南沿海台风)", 40f, -5f, 20f, 40f, 0f, 10f, -5f, 10f, 15f),
            new MeteorologicalZone("zone-10", "典型气象区 X 区 (高山微地形重冰)", 35f, -25f, 5f, 30f, 20f, -5f, -5f, 15f, 10f)
        };

        /// <summary>
        /// DL/T 5582 表 9.4.2-1 塔头空气间隙最小安全净距 (m)
        /// </summary>
        public static float GetMinAirClearance(int voltageLevel, string clearanceType)
        {
            switch (voltageLevel)
            {
                case 35:
                    if (clearanceType == "internalOvervoltage") return 0.25f;
                    if (clearanceType == "lightningOvervoltage") return 0.35f;
                    return 0.10f; // operational
                case 110:
                    if (clearanceType == "internalOvervoltage") return 0.70f;
                    if (clearanceType == "lightningOvervoltage") return 1.00f;
                    return 0.25f;
                case 220:
                    if (clearanceType == "internalOvervoltage") return 1.45f;
                    if (clearanceType == "lightningOvervoltage") return 1.90f;
                    return 0.55f;
                case 330:
                    if (clearanceType == "internalOvervoltage") return 2.20f;
                    if (clearanceType == "lightningOvervoltage") return 2.80f;
                    return 0.90f;
                case 500:
                    if (clearanceType == "internalOvervoltage") return 3.30f;
                    if (clearanceType == "lightningOvervoltage") return 4.10f;
                    return 1.20f;
                case 750:
                    if (clearanceType == "internalOvervoltage") return 4.60f;
                    if (clearanceType == "lightningOvervoltage") return 5.30f;
                    return 2.00f;
                case 1000:
                    if (clearanceType == "internalOvervoltage") return 6.00f;
                    if (clearanceType == "lightningOvervoltage") return 7.20f;
                    return 2.80f;
                default:
                    return 1.45f;
            }
        }

        /// <summary>
        /// DL/T 5582 表 13.0.1 导线对地面及交叉跨越物最小安全净空距离 (m)
        /// </summary>
        public static float GetMinGroundClearance(int voltageLevel, string terrainType)
        {
            switch (voltageLevel)
            {
                case 35:
                    if (terrainType == "residential") return 7.0f;
                    if (terrainType == "non_residential") return 6.0f;
                    if (terrainType == "difficult_transport") return 5.0f;
                    if (terrainType == "railway") return 7.5f;
                    if (terrainType == "road") return 7.0f;
                    return 6.0f;
                case 110:
                    if (terrainType == "residential") return 7.5f;
                    if (terrainType == "non_residential") return 6.5f;
                    if (terrainType == "difficult_transport") return 5.5f;
                    if (terrainType == "railway") return 7.5f;
                    if (terrainType == "road") return 7.0f;
                    return 6.5f;
                case 220:
                    if (terrainType == "residential") return 8.5f;
                    if (terrainType == "non_residential") return 7.5f;
                    if (terrainType == "difficult_transport") return 6.5f;
                    if (terrainType == "railway") return 8.5f;
                    if (terrainType == "road") return 8.0f;
                    return 7.5f;
                case 330:
                    if (terrainType == "residential") return 9.5f;
                    if (terrainType == "non_residential") return 8.5f;
                    if (terrainType == "difficult_transport") return 7.5f;
                    if (terrainType == "railway") return 9.5f;
                    if (terrainType == "road") return 9.0f;
                    return 8.5f;
                case 500:
                    if (terrainType == "residential") return 14.0f;
                    if (terrainType == "non_residential") return 11.0f;
                    if (terrainType == "difficult_transport") return 8.5f;
                    if (terrainType == "railway") return 14.0f;
                    if (terrainType == "road") return 14.0f;
                    return 11.0f;
                case 750:
                    if (terrainType == "residential") return 19.5f;
                    if (terrainType == "non_residential") return 15.5f;
                    if (terrainType == "difficult_transport") return 12.0f;
                    if (terrainType == "railway") return 16.5f;
                    if (terrainType == "road") return 16.0f;
                    return 15.5f;
                case 1000:
                    if (terrainType == "residential") return 27.0f;
                    if (terrainType == "non_residential") return 21.0f;
                    if (terrainType == "difficult_transport") return 15.0f;
                    if (terrainType == "railway") return 20.0f;
                    if (terrainType == "road") return 20.0f;
                    return 21.0f;
                default:
                    return 7.5f;
            }
        }
    }
}
