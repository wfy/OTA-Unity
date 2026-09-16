using System;
using System.Collections.Generic;
using UnityEngine;

namespace OTA.Corridor.Mechanics
{
    [Serializable]
    public class ConductorSpec
    {
        public string modelName;      // e.g. "LGJ-240/30"
        public string codeName;       // 规格代号
        public float outerDiameter;   // d, mm
        public float totalArea;       // S, mm²
        public float unitMass;        // kg/km -> converted to kg/m
        public float ratedStrength;   // Tp, N
        public float elasticModulus;  // E, N/mm²
        public float thermalExpansion;// α, 1/°C
        public float dcResistance;    // Ω/km
        public int bundleNumber = 1;  // 分裂数
    }

    [Serializable]
    public class WorkingCondition
    {
        public string id;
        public string name;
        public float temp;            // °C
        public float windSpeed;       // m/s
        public float iceThickness;    // mm
        public bool isControlCandidate;

        public WorkingCondition(string id, string name, float temp, float windSpeed, float iceThickness, bool isCandidate)
        {
            this.id = id;
            this.name = name;
            this.temp = temp;
            this.windSpeed = windSpeed;
            this.iceThickness = iceThickness;
            this.isControlCandidate = isCandidate;
        }
    }

    [Serializable]
    public class ConditionResult
    {
        public string conditionId;
        public string conditionName;
        public float temp;
        public float windSpeed;
        public float iceThickness;
        public float specificLoad;    // γ, N/(m·mm²)
        public float stress;          // σ, N/mm²
        public float tension;         // T, N
        public float safetyFactor;    // Kc
        public float sag;             // f, m
        public float windAngle;       // φ, deg
        public float verticalSag;     // f_v, m
        public float horizontalSwing; // f_h, m
        public bool isGoverning;      // 是否控制工况
    }

    public static class ConductorMechanics
    {
        public const float G_ACCEL = 9.80665f; // m/s²

        public static readonly ConductorSpec[] Presets = new ConductorSpec[]
        {
            new ConductorSpec
            {
                modelName = "LGJ-240/30",
                codeName = "220kV 常用导线",
                outerDiameter = 21.6f,
                totalArea = 275.96f,
                unitMass = 0.9222f, // kg/m
                ratedStrength = 75340f, // 75.34 kN
                elasticModulus = 73000f,
                thermalExpansion = 19.3e-6f,
                bundleNumber = 2
            },
            new ConductorSpec
            {
                modelName = "LGJ-300/40",
                codeName = "220kV 重载导线",
                outerDiameter = 23.94f,
                totalArea = 338.99f,
                unitMass = 1.133f,
                ratedStrength = 92220f,
                elasticModulus = 73000f,
                thermalExpansion = 19.3e-6f,
                bundleNumber = 2
            },
            new ConductorSpec
            {
                modelName = "LGJ-400/35",
                codeName = "500kV 典型导线",
                outerDiameter = 26.82f,
                totalArea = 425.24f,
                unitMass = 1.349f,
                ratedStrength = 103900f,
                elasticModulus = 65000f,
                thermalExpansion = 20.5e-6f,
                bundleNumber = 4
            },
            new ConductorSpec
            {
                modelName = "LGJ-185/25",
                codeName = "110kV 典型导线",
                outerDiameter = 18.88f,
                totalArea = 210.95f,
                unitMass = 0.706f,
                ratedStrength = 57520f,
                elasticModulus = 73000f,
                thermalExpansion = 19.3e-6f,
                bundleNumber = 1
            }
        };

        public static List<WorkingCondition> GetDefaultConditions()
        {
            return new List<WorkingCondition>
            {
                new WorkingCondition("min-temp", "最低气温工况", -10f, 0f, 0f, true),
                new WorkingCondition("max-wind", "最大风速工况", 10f, 25f, 0f, true),
                new WorkingCondition("max-ice",  "设计覆冰工况", -5f, 10f, 5f, true),
                new WorkingCondition("avg-temp", "年均气温工况", 15f, 0f, 0f, true),
                new WorkingCondition("max-temp", "最高气温工况", 40f, 0f, 0f, false),
                new WorkingCondition("install",  "施工安装工况", 15f, 0f, 0f, false)
            };
        }

        public static float CalculateSpecificLoad(ConductorSpec conductor, float windSpeed, float iceThickness, float spanLength, out float windLoadGamma, out float vertLoadGamma)
        {
            float S = conductor.totalArea;
            float d = conductor.outerDiameter;
            float m = conductor.unitMass;
            float b = iceThickness;

            // 1. 自重比载 g1
            float g1 = (m * G_ACCEL) / S;

            // 2. 覆冰比载 g2 (冰密度 900 kg/m³)
            float g2 = 0f;
            if (b > 0)
            {
                float iceMassPerMeter = 900f * Mathf.PI * (b / 1000f) * (d / 1000f + b / 1000f);
                g2 = (iceMassPerMeter * G_ACCEL) / S;
            }

            // 3. 风荷载比载 g3 (GB 50545 简捷有效算法)
            float g_wind = 0f;
            if (windSpeed > 0)
            {
                float W0 = (windSpeed * windSpeed) / 1600f; // kN/m²
                float mu_sc = 1.1f; // 体型系数
                float mu_z = 1.25f; // 30m 高度变化系数
                float alpha_L = 0.85f; // 档距折减系数
                float d_effective_m = (d + 2f * b) / 1000f;
                float P_w = W0 * mu_z * mu_sc * alpha_L * d_effective_m * 1000f; // N/m
                g_wind = P_w / S;
            }

            windLoadGamma = g_wind;
            vertLoadGamma = g1 + g2;

            return Mathf.Sqrt(vertLoadGamma * vertLoadGamma + windLoadGamma * windLoadGamma);
        }

        /// <summary>
        /// 牛顿拉夫逊迭代求解状态方程: σ_m³ + P·σ_m² = Q
        /// </summary>
        public static float SolveStateEquation(float sigma1, float gamma1, float t1, float gammaM, float tm, float L, float E, float alpha)
        {
            if (sigma1 <= 0.01f) sigma1 = 10f;
            float K1 = (E * Mathf.Pow(gamma1 * L, 2f)) / (24f * Mathf.Pow(sigma1, 2f));
            float P = K1 + alpha * E * (tm - t1) - sigma1;
            float Q = (E * Mathf.Pow(gammaM * L, 2f)) / 24f;

            float sigma = Mathf.Max(sigma1, 10f);
            for (int i = 0; i < 50; i++)
            {
                float f = Mathf.Pow(sigma, 3f) + P * Mathf.Pow(sigma, 2f) - Q;
                float fPrime = 3f * Mathf.Pow(sigma, 2f) + 2f * P * sigma;
                if (Mathf.Abs(fPrime) < 1e-9f) break;
                float delta = f / fPrime;
                sigma -= delta;
                if (Mathf.Abs(delta) < 1e-4f) break;
            }

            return Mathf.Max(sigma, 0.1f);
        }

        public static List<ConditionResult> CalculateAllConditions(ConductorSpec conductor, float spanLength, out string governingConditionId, float targetSafetyFactor = 2.5f)
        {
            var conditions = GetDefaultConditions();
            float L = Mathf.Max(10f, spanLength);
            float E = conductor.elasticModulus;
            float alpha = conductor.thermalExpansion;
            float Tp = conductor.ratedStrength;
            float S = conductor.totalArea;

            float allowableMaxStress = (Tp / targetSafetyFactor) / S;
            float allowableAvgStress = (Tp * 0.25f) / S;

            // 1. 控制工况判别
            string bestCondId = "max-wind";
            float maxStressRatio = -1f;

            foreach (var candidate in conditions)
            {
                if (!candidate.isControlCandidate) continue;
                float gammaCand = CalculateSpecificLoad(conductor, candidate.windSpeed, candidate.iceThickness, L, out _, out _);
                float candLimit = candidate.name.Contains("年均") ? allowableAvgStress : allowableMaxStress;

                float maxProjectionRatio = 0f;
                foreach (var other in conditions)
                {
                    if (other.id == candidate.id) continue;
                    float gammaOther = CalculateSpecificLoad(conductor, other.windSpeed, other.iceThickness, L, out _, out _);
                    float otherLimit = other.name.Contains("年均") ? allowableAvgStress : allowableMaxStress;

                    float solved = SolveStateEquation(candLimit, gammaCand, candidate.temp, gammaOther, other.temp, L, E, alpha);
                    float ratio = solved / otherLimit;
                    if (ratio > maxProjectionRatio) maxProjectionRatio = ratio;
                }

                if (maxProjectionRatio > maxStressRatio)
                {
                    maxStressRatio = maxProjectionRatio;
                    bestCondId = candidate.id;
                }
            }

            governingConditionId = bestCondId;
            var govCond = conditions.Find(c => c.id == bestCondId) ?? conditions[0];
            float govLimit = govCond.name.Contains("年均") ? allowableAvgStress : allowableMaxStress;
            float govGamma = CalculateSpecificLoad(conductor, govCond.windSpeed, govCond.iceThickness, L, out _, out _);

            // 2. 结算全部工况力学参数
            var results = new List<ConditionResult>();
            foreach (var cond in conditions)
            {
                float gamma = CalculateSpecificLoad(conductor, cond.windSpeed, cond.iceThickness, L, out float wGamma, out float vGamma);
                float stress = (cond.id == bestCondId) 
                    ? govLimit 
                    : SolveStateEquation(govLimit, govGamma, govCond.temp, gamma, cond.temp, L, E, alpha);

                float tension = stress * S;
                float safetyFactor = Tp / Mathf.Max(1f, tension);
                float sag = (gamma * L * L) / (8f * stress);

                float windAngle = (Mathf.Atan2(wGamma, vGamma) * Mathf.Rad2Deg);
                float vertSag = sag * Mathf.Cos(windAngle * Mathf.Deg2Rad);
                float horizSwing = sag * Mathf.Sin(windAngle * Mathf.Deg2Rad);

                results.Add(new ConditionResult
                {
                    conditionId = cond.id,
                    conditionName = cond.name,
                    temp = cond.temp,
                    windSpeed = cond.windSpeed,
                    iceThickness = cond.iceThickness,
                    specificLoad = gamma,
                    stress = stress,
                    tension = tension,
                    safetyFactor = safetyFactor,
                    sag = sag,
                    windAngle = windAngle,
                    verticalSag = vertSag,
                    horizontalSwing = horizSwing,
                    isGoverning = (cond.id == bestCondId)
                });
            }

            return results;
        }
    }
}
