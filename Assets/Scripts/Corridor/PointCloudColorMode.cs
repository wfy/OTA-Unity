using System;
using System.Collections.Generic;
using UnityEngine;

namespace OTA.Corridor
{
    public enum PointCloudColorMode
    {
        RGB = 0,            // 真实色彩
        Classification = 1, // ASPRS 电力行业标准分类
        Elevation = 2,      // 高程渐变 (Turbo/Jet)
        Intensity = 3       // 反射强度
    }

    public enum ElevationRampType
    {
        Turbo = 0,   // 连续高感知彩虹
        Jet = 1,     // 经典蓝-绿-黄-红
        Rainbow = 2, // 七彩渐变
        Viridis = 3, // 蓝绿黄科学色阶
        Terrain = 4  // 绿-褐-白地形色
    }

    [Serializable]
    public struct ClassMeta
    {
        public byte id;
        public string name;
        public Color color;
        public int count;
        public bool isVisible;

        public ClassMeta(byte id, string name, Color color)
        {
            this.id = id;
            this.name = name;
            this.color = color;
            this.count = 0;
            this.isVisible = true;
        }
    }

    public static class ClassificationPalette
    {
        private static readonly Dictionary<byte, ClassMeta> Defaults = new Dictionary<byte, ClassMeta>
        {
            { 0,  new ClassMeta(0,  "未分类 (Created)",       new Color(0.70f, 0.70f, 0.70f, 1f)) },
            { 1,  new ClassMeta(1,  "未分类 (Unclassified)",  new Color(0.60f, 0.60f, 0.60f, 1f)) },
            { 2,  new ClassMeta(2,  "地面 (Ground)",          new Color(0.63f, 0.45f, 0.28f, 1f)) },
            { 3,  new ClassMeta(3,  "低植被 (Low Veg)",       new Color(0.47f, 0.78f, 0.31f, 1f)) },
            { 4,  new ClassMeta(4,  "中植被 (Med Veg)",       new Color(0.20f, 0.63f, 0.20f, 1f)) },
            { 5,  new ClassMeta(5,  "高植被/树木 (High Veg)", new Color(0.00f, 0.85f, 0.00f, 1f)) },
            { 6,  new ClassMeta(6,  "建筑物 (Building)",      new Color(0.24f, 0.39f, 0.86f, 1f)) },
            { 7,  new ClassMeta(7,  "低噪点 (Low Noise)",     new Color(1.00f, 0.39f, 0.39f, 1f)) },
            { 8,  new ClassMeta(8,  "特征点 (Key-point)",     new Color(0.85f, 0.85f, 0.20f, 1f)) },
            { 9,  new ClassMeta(9,  "水体 (Water)",           new Color(0.20f, 0.71f, 0.94f, 1f)) },
            { 10, new ClassMeta(10, "铁路 (Rail)",            new Color(0.55f, 0.31f, 0.55f, 1f)) },
            { 11, new ClassMeta(11, "道路 (Road)",            new Color(0.35f, 0.35f, 0.35f, 1f)) },
            { 13, new ClassMeta(13, "地线 (Guard Wire)",      new Color(0.95f, 0.88f, 0.30f, 1f)) },
            { 14, new ClassMeta(14, "导线 (Conductor)",       new Color(1.00f, 0.48f, 0.00f, 1f)) },
            { 15, new ClassMeta(15, "杆塔 (Tower)",           new Color(1.00f, 0.78f, 0.00f, 1f)) },
            { 16, new ClassMeta(16, "绝缘子 (Insulator)",     new Color(0.85f, 0.25f, 0.95f, 1f)) },
            { 17, new ClassMeta(17, "桥梁 (Bridge)",          new Color(0.31f, 0.63f, 0.71f, 1f)) },
            { 18, new ClassMeta(18, "高处噪点 (High Noise)",  new Color(1.00f, 0.00f, 0.50f, 1f)) },
            { 20, new ClassMeta(20, "边坡树 (Slope Tree)",    new Color(0.00f, 0.90f, 0.90f, 1f)) },
            { 21, new ClassMeta(21, "通道缺陷 (Defect)",      new Color(1.00f, 0.00f, 0.00f, 1f)) }
        };

        public static ClassMeta GetMeta(byte classId)
        {
            if (Defaults.TryGetValue(classId, out var meta)) return meta;
            return new ClassMeta(classId, "类别 " + classId, new Color(0.5f, 0.5f, 0.5f, 1f));
        }

        public static Color GetColor(byte classId)
        {
            if (Defaults.TryGetValue(classId, out var meta)) return meta.color;
            return new Color(0.5f, 0.5f, 0.5f, 1f);
        }

        public static Dictionary<byte, ClassMeta> CreatePaletteCopy()
        {
            var dict = new Dictionary<byte, ClassMeta>();
            foreach (var kv in Defaults) dict[kv.Key] = kv.Value;
            return dict;
        }
    }

    public static class ColorRamp
    {
        public static Color EvaluateElevation(float t, ElevationRampType rampType)
        {
            t = Mathf.Clamp01(t);
            switch (rampType)
            {
                case ElevationRampType.Turbo: return Turbo(t);
                case ElevationRampType.Jet: return Jet(t);
                case ElevationRampType.Rainbow: return Rainbow(t);
                case ElevationRampType.Viridis: return Viridis(t);
                case ElevationRampType.Terrain: return Terrain(t);
                default: return Turbo(t);
            }
        }

        public static Color EvaluateIntensity(float intensity, bool heatMap = false)
        {
            intensity = Mathf.Clamp01(intensity);
            if (!heatMap)
            {
                return new Color(intensity, intensity, intensity, 1f);
            }
            return Heat(intensity);
        }

        public static Color Turbo(float x)
        {
            const float kRedVec4_0 = 0.13572138f;
            const float kRedVec4_1 = 4.61539260f;
            const float kRedVec4_2 = -42.66032258f;
            const float kRedVec4_3 = 132.13108234f;
            const float kRedVec4_4 = -152.94239396f;
            const float kRedVec4_5 = 59.28637943f;

            const float kGreenVec4_0 = 0.09140261f;
            const float kGreenVec4_1 = 2.19418839f;
            const float kGreenVec4_2 = 4.84296658f;
            const float kGreenVec4_3 = -14.18503333f;
            const float kGreenVec4_4 = 4.27729857f;
            const float kGreenVec4_5 = 2.82956604f;

            const float kBlueVec4_0 = 0.10798322f;
            const float kBlueVec4_1 = 8.33825085f;
            const float kBlueVec4_2 = -79.64654921f;
            const float kBlueVec4_3 = 251.78201202f;
            const float kBlueVec4_4 = -324.96096538f;
            const float kBlueVec4_5 = 143.51865463f;

            float r = kRedVec4_0 + x * (kRedVec4_1 + x * (kRedVec4_2 + x * (kRedVec4_3 + x * (kRedVec4_4 + x * kRedVec4_5))));
            float g = kGreenVec4_0 + x * (kGreenVec4_1 + x * (kGreenVec4_2 + x * (kGreenVec4_3 + x * (kGreenVec4_4 + x * kGreenVec4_5))));
            float b = kBlueVec4_0 + x * (kBlueVec4_1 + x * (kBlueVec4_2 + x * (kBlueVec4_3 + x * (kBlueVec4_4 + x * kBlueVec4_5))));
            return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f);
        }

        public static Color Jet(float t)
        {
            float r = Mathf.Clamp01(1.5f - Mathf.Abs(4f * t - 3f));
            float g = Mathf.Clamp01(1.5f - Mathf.Abs(4f * t - 2f));
            float b = Mathf.Clamp01(1.5f - Mathf.Abs(4f * t - 1f));
            return new Color(r, g, b, 1f);
        }

        public static Color Rainbow(float t)
        {
            float h = (1f - t) * 0.75f;
            return Color.HSVToRGB(h, 0.95f, 0.95f);
        }

        public static Color Viridis(float t)
        {
            float r = Mathf.Clamp01(-0.017f + t * (1.35f + t * (-0.72f + t * 0.35f)));
            float g = Mathf.Clamp01(0.024f + t * (0.42f + t * (1.85f + t * -1.33f)));
            float b = Mathf.Clamp01(0.28f + t * (1.30f + t * (-3.22f + t * 1.95f)));
            return new Color(r, g, b, 1f);
        }

        public static Color Terrain(float t)
        {
            if (t < 0.25f)
            {
                float k = t / 0.25f;
                return Color.Lerp(new Color(0.1f, 0.4f, 0.1f), new Color(0.3f, 0.7f, 0.2f), k);
            }
            if (t < 0.65f)
            {
                float k = (t - 0.25f) / 0.4f;
                return Color.Lerp(new Color(0.3f, 0.7f, 0.2f), new Color(0.7f, 0.5f, 0.2f), k);
            }
            if (t < 0.85f)
            {
                float k = (t - 0.65f) / 0.2f;
                return Color.Lerp(new Color(0.7f, 0.5f, 0.2f), new Color(0.6f, 0.6f, 0.6f), k);
            }
            float kTop = (t - 0.85f) / 0.15f;
            return Color.Lerp(new Color(0.6f, 0.6f, 0.6f), Color.white, kTop);
        }

        public static Color Heat(float t)
        {
            if (t < 0.33f)
            {
                return Color.Lerp(Color.black, Color.red, t / 0.33f);
            }
            if (t < 0.66f)
            {
                return Color.Lerp(Color.red, Color.yellow, (t - 0.33f) / 0.33f);
            }
            return Color.Lerp(Color.yellow, Color.white, (t - 0.66f) / 0.34f);
        }
    }
}
