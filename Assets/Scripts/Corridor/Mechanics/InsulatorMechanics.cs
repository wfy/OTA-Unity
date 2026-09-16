using System;
using System.Collections.Generic;
using UnityEngine;

namespace OTA.Corridor.Mechanics
{
    public enum InsulatorStringType
    {
        Single_I,    // 单I型悬垂串
        Double_I,    // 双I型悬垂串
        V_String,    // V型悬垂串
        Tension,     // 耐张串
        Post         // 柱式防风偏绝缘子
    }

    [Serializable]
    public class InsulatorSpec
    {
        public string id;
        public string name;
        public string material; // "porcelain", "glass", "composite"
        public InsulatorStringType stringType = InsulatorStringType.Single_I;
        public float vAngle = 90f;              // V串夹角 (度)
        public float structureHeight = 146f;    // 结构高度 mm (单片)
        public float creepageDistance = 400f;   // 爬电距离 mm (单片)
        public float unitMass = 6.0f;           // 单片质量 kg
        public float ratedLoad = 160f;          // 额定机械载荷 kN
        public float characteristicIndexM1 = 0.5f; // 高原海拔特征指数 m1
        public float counterWeightKg = 0f;      // 防风偏加重锤 kg
        public float discDiameter = 255f;       // 盘径 mm

        public static readonly InsulatorSpec[] Presets = new InsulatorSpec[]
        {
            new InsulatorSpec { id = "xp-160", name = "XP-160 盘形瓷绝缘子", material = "porcelain", structureHeight = 146f, creepageDistance = 400f, unitMass = 6.2f, ratedLoad = 160f, discDiameter = 255f },
            new InsulatorSpec { id = "xp-210", name = "XP-210 耐张盘形瓷绝缘子", material = "porcelain", structureHeight = 155f, creepageDistance = 450f, unitMass = 7.5f, ratedLoad = 210f, discDiameter = 280f },
            new InsulatorSpec { id = "lxy-160", name = "LXY-160 钢化玻璃绝缘子", material = "glass", structureHeight = 146f, creepageDistance = 420f, unitMass = 5.8f, ratedLoad = 160f, discDiameter = 255f },
            new InsulatorSpec { id = "fxbw-110", name = "FXBW-110/100 复合绝缘子", material = "composite", structureHeight = 1200f, creepageDistance = 3500f, unitMass = 4.5f, ratedLoad = 100f, discDiameter = 120f },
            new InsulatorSpec { id = "fxbw-220", name = "FXBW-220/160 复合绝缘子", material = "composite", structureHeight = 2200f, creepageDistance = 6800f, unitMass = 8.2f, ratedLoad = 160f, discDiameter = 140f },
            new InsulatorSpec { id = "fxbw-500", name = "FXBW-500/210 复合绝缘子", material = "composite", structureHeight = 4500f, creepageDistance = 15000f, unitMass = 19.5f, ratedLoad = 210f, discDiameter = 180f }
        };
    }

    [Serializable]
    public class InsulatorCalcResult
    {
        public int creepageRequiredCount;
        public int altitudeCorrectedCount;
        public int finalCount;
        public float stringLength;              // m
        public InsulatorStringType stringType;
        public float counterWeightMass;         // kg
        public float stringTotalWeightKg;       // kg
        public float stringWindAreaM2;          // m²
        public float windLoadOnString;          // kN
        public float conductorWindLoadOnString; // kN
        public float conductorWeightOnString;   // kN
        public float insulatorWindSwingAngle;   // deg (φ_ins)
        public float conductorWindAngle;        // deg (φ_cond)
        public float horizontalDisplacement;    // m (Δx)
        public float verticalDropDisplacement;  // m (Δy)
        public float minAirClearanceRequired;   // m
        public float actualClearanceToTower;    // m
        public bool clearancePassed;
        public bool vStringLiftOff;
        public float horizontalSpan;            // l_h, m
        public float verticalSpan;              // l_v, m
        public float kvValue;                   // K_v
    }

    /// <summary>
    /// DL/T 5582-2020 绝缘子配置与风偏力学计算引擎
    /// </summary>
    public static class InsulatorMechanics
    {
        public const float G_ACCEL = 9.80665f;

        /// <summary>
        /// 绝缘子片数及串长计算 (Clause 6.1)
        /// </summary>
        public static void CalculateInsulatorUnits(
            InsulatorSpec insulator,
            int voltageLevel,
            float elevation,
            float creepageRatio, // 爬电比距 mm/kV (默认 31.5)
            out int creepageCount,
            out int altitudeCorrectedCount,
            out int finalCount,
            out float totalLength,
            out float totalWeight,
            out float calculatedWindArea,
            float efficiencyFactorKe = 0.95f)
        {
            float discDia = insulator.discDiameter > 0 ? insulator.discDiameter : (insulator.material == "composite" ? 120f : 255f);

            if (insulator.material == "composite")
            {
                creepageCount = 1;
                altitudeCorrectedCount = 1;
                finalCount = 1;
                totalLength = insulator.structureHeight / 1000f;
                totalWeight = insulator.unitMass;
                calculatedWindArea = totalLength * (discDia / 1000f);
                return;
            }

            float maxOperatingVoltage = voltageLevel * 1.05f;
            float U_ph_e = maxOperatingVoltage / Mathf.Sqrt(3f);
            float L01 = Mathf.Max(insulator.creepageDistance, 100f);

            // 式 6.1.3-2: n >= (λ * U_ph-e) / (Ke * L01)
            creepageCount = Mathf.CeilToInt((creepageRatio * U_ph_e) / (efficiencyFactorKe * L01));

            // 式 6.1.5: 海拔 > 1000m 气压修正
            altitudeCorrectedCount = creepageCount;
            if (elevation > 1000f)
            {
                float m1 = insulator.characteristicIndexM1 > 0 ? insulator.characteristicIndexM1 : 0.5f;
                float factor = Mathf.Exp((m1 * (elevation - 1000f)) / 8150f);
                altitudeCorrectedCount = Mathf.CeilToInt(creepageCount * factor);
            }

            // 式 6.2.2 规程保底片数
            int codeMin = 7;
            if (voltageLevel <= 35) codeMin = 3;
            else if (voltageLevel <= 110) codeMin = 7;
            else if (voltageLevel <= 220) codeMin = 13;
            else if (voltageLevel <= 330) codeMin = 17;
            else if (voltageLevel <= 500) codeMin = 25;
            else if (voltageLevel <= 750) codeMin = 32;
            else codeMin = 43;

            finalCount = Mathf.Max(altitudeCorrectedCount, codeMin);
            totalLength = (finalCount * insulator.structureHeight) / 1000f;
            totalWeight = finalCount * insulator.unitMass;

            float discArea = discDia >= 300f ? 0.03f : 0.02f;
            calculatedWindArea = finalCount * discArea + 0.03f;
        }

        /// <summary>
        /// 绝缘子串风偏角及塔身电气净隙校验 (DL/T 5582 Clause 9.4)
        /// </summary>
        public static InsulatorCalcResult CalculateWindSwing(
            InsulatorSpec insulator,
            ConductorSpec conductor,
            int voltageLevel,
            float spanLength,
            float kvValue,
            float windSpeed,
            float iceThickness,
            float elevation,
            InsulatorStringType stringType,
            float counterWeightKg,
            float vAngleDeg = 90f,
            float creepageRatio = 31.5f,
            string clearanceConditionType = "internalOvervoltage")
        {
            float l_h = Mathf.Max(10f, spanLength);
            float kv = Mathf.Max(0.2f, kvValue);
            float l_v = l_h * kv;

            CalculateInsulatorUnits(
                insulator, voltageLevel, elevation, creepageRatio,
                out int creepageCount, out int altitudeCount, out int finalCount,
                out float totalLength, out float totalWeight, out float windArea);

            if (stringType == InsulatorStringType.Double_I)
            {
                totalWeight *= 2f;
                windArea *= 1.8f;
            }

            // 1. 风压与绝缘子受风荷载
            float W0 = (windSpeed * windSpeed) / 1600f; // kN/m²
            float mu_z = 1.25f;
            float mu_s_ins = 1.2f;
            float P_ins = W0 * mu_z * windArea * mu_s_ins; // kN

            // 2. 导线风荷载与自重荷载
            ConductorMechanics.CalculateSpecificLoad(conductor, windSpeed, iceThickness, l_h, out float wGamma, out float vGamma);

            float S_total = conductor.totalArea * conductor.bundleNumber;
            // 垂直荷载与横向风荷载 (kN)
            float P_c = (wGamma * S_total * l_h) / 1000f;
            float G_c = (vGamma * S_total * l_v) / 1000f;

            float G_ins = (totalWeight * G_ACCEL) / 1000f;
            float G_weight = (counterWeightKg * G_ACCEL) / 1000f;

            float totalHoriz = P_c + 0.5f * P_ins;
            float totalVert = G_c + 0.5f * G_ins + G_weight;

            float phi_cond = Mathf.Atan2(wGamma, Mathf.Max(0.001f, vGamma)) * Mathf.Rad2Deg;
            float phi_ins = 0f;
            bool vLiftOff = false;

            switch (stringType)
            {
                case InsulatorStringType.Single_I:
                case InsulatorStringType.Double_I:
                    phi_ins = Mathf.Atan2(totalHoriz, Mathf.Max(0.001f, totalVert)) * Mathf.Rad2Deg;
                    break;
                case InsulatorStringType.V_String:
                    // V串双臂刚性抑制：两臂张力平衡判断
                    float halfV = (vAngleDeg * 0.5f) * Mathf.Deg2Rad;
                    float liftForce = totalHoriz * Mathf.Sin(halfV) - totalVert * Mathf.Cos(halfV);
                    if (liftForce > 0f)
                    {
                        vLiftOff = true;
                        phi_ins = Mathf.Min(35f, (liftForce / Mathf.Max(1f, totalVert)) * Mathf.Rad2Deg);
                    }
                    else
                    {
                        vLiftOff = false;
                        phi_ins = 0f;
                    }
                    break;
                case InsulatorStringType.Tension:
                case InsulatorStringType.Post:
                    phi_ins = 0f;
                    break;
            }

            float rad = phi_ins * Mathf.Deg2Rad;
            float deltaX = totalLength * Mathf.Sin(rad);
            float deltaY = totalLength * (1f - Mathf.Cos(rad));

            // 3. 塔头空气间隙校验 (表 9.4.2-1)
            float minRequiredClearance = MeteorologyRegistry.GetMinAirClearance(voltageLevel, clearanceConditionType);

            // 假定横担基础挂点距塔身净距：根据电压等级典型跨臂长度 (110kV: 2.8m, 220kV: 4.5m, 500kV: 8.0m)
            float nominalCrossarmReach = voltageLevel <= 110 ? 2.8f : (voltageLevel <= 220 ? 4.5f : (voltageLevel <= 500 ? 8.0f : 12.0f));
            float actualClearance = Mathf.Max(0f, nominalCrossarmReach - deltaX);
            bool passed = actualClearance >= minRequiredClearance && !vLiftOff;

            return new InsulatorCalcResult
            {
                creepageRequiredCount = creepageCount,
                altitudeCorrectedCount = altitudeCount,
                finalCount = finalCount,
                stringLength = totalLength,
                stringType = stringType,
                counterWeightMass = counterWeightKg,
                stringTotalWeightKg = totalWeight,
                stringWindAreaM2 = windArea,
                windLoadOnString = P_ins,
                conductorWindLoadOnString = P_c,
                conductorWeightOnString = G_c,
                insulatorWindSwingAngle = phi_ins,
                conductorWindAngle = phi_cond,
                horizontalDisplacement = deltaX,
                verticalDropDisplacement = deltaY,
                minAirClearanceRequired = minRequiredClearance,
                actualClearanceToTower = actualClearance,
                clearancePassed = passed,
                vStringLiftOff = vLiftOff,
                horizontalSpan = l_h,
                verticalSpan = l_v,
                kvValue = kv
            };
        }
    }
}
