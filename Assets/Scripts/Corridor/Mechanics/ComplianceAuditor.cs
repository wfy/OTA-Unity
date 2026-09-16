using System;
using System.Collections.Generic;
using UnityEngine;

namespace OTA.Corridor.Mechanics
{
    [Serializable]
    public class ComplianceAuditItem
    {
        public string item;
        public string codeReference;
        public string standardRequirement;
        public string calculatedValue;
        public bool passed;
        public string notes;
    }

    /// <summary>
    /// DL/T 5582-2020 架空输电线路电气设计规程合规性审计引擎
    /// </summary>
    public static class ComplianceAuditor
    {
        public static List<ComplianceAuditItem> AuditCompliance(
            ConductorSpec conductor,
            int voltageLevel,
            float spanLength,
            float maxTension,
            float avgTension,
            float maxSag,
            InsulatorCalcResult insulatorResult,
            string terrainType = "residential")
        {
            var items = new List<ComplianceAuditItem>();

            float Tp = conductor.ratedStrength * conductor.bundleNumber;

            // 1. 最大运行张力安全系数
            float safetyFactor = Tp / Mathf.Max(1f, maxTension);
            bool passSf = safetyFactor >= 2.5f;
            items.Add(new ComplianceAuditItem
            {
                item = "导线最大综合张力安全系数校验",
                codeReference = "DL/T 5582-2020 5.1.15",
                standardRequirement = "设计安全系数 Kc ≥ 2.50",
                calculatedValue = string.Format("实测 Kc = {0:F2} (张力 {1:F1} kN)", safetyFactor, maxTension / 1000f),
                passed = passSf,
                notes = passSf ? "满足规程机械强度裕度要求" : "安全系数不足，需降低设计张力或增大导线截面"
            });

            // 2. 年均气温平均运行张力防振限制
            float avgRatio = (avgTension / Tp) * 100f;
            bool passAvg = avgRatio <= 25.0f;
            items.Add(new ComplianceAuditItem
            {
                item = "年平均运行张力防振上限校验",
                codeReference = "DL/T 5582-2020 5.1.17",
                standardRequirement = "平均运行张力不得超过额定拉断力的 25.0%",
                calculatedValue = string.Format("实测占比 = {0:F1}% (张力 {1:F1} kN)", avgRatio, avgTension / 1000f),
                passed = passAvg,
                notes = passAvg ? "满足微风振动控制要求" : "年均张力过高，存在导线微风振动疲劳断股隐患"
            });

            // 3. 最大弧垂对地净空校验
            float reqGround = MeteorologyRegistry.GetMinGroundClearance(voltageLevel, terrainType);
            // 假设挂点净高为 35m (典型双回转角/直线塔挂高)
            float nominalHangingHeight = voltageLevel <= 110 ? 25f : (voltageLevel <= 220 ? 32f : 45f);
            float actualGroundClearance = Mathf.Max(0f, nominalHangingHeight - maxSag);
            bool passGround = actualGroundClearance >= reqGround;
            items.Add(new ComplianceAuditItem
            {
                item = "最高温/大弧垂工况对地安全净距校验",
                codeReference = "DL/T 5582-2020 表 13.0.1",
                standardRequirement = string.Format("{0} 地形最小对地净距 ≥ {1:F1} m", terrainType == "residential" ? "居民区" : "常规区", reqGround),
                calculatedValue = string.Format("计算净距 = {0:F2} m (最大弧垂 {1:F2} m)", actualGroundClearance, maxSag),
                passed = passGround,
                notes = passGround ? "满足规程对地及交越安全距离" : "对地净空不足，需调高呼高或缩小档距"
            });

            // 4. 绝缘子风偏塔头最小电气间隙
            if (insulatorResult != null)
            {
                items.Add(new ComplianceAuditItem
                {
                    item = "大风工况绝缘子风偏对塔身电气间隙",
                    codeReference = "DL/T 5582-2020 表 9.4.2-1",
                    standardRequirement = string.Format("工频过电压最小空气间隙 ≥ {0:F2} m", insulatorResult.minAirClearanceRequired),
                    calculatedValue = string.Format("风偏后净距 = {0:F2} m (风偏角 {1:F1}°)", insulatorResult.actualClearanceToTower, insulatorResult.insulatorWindSwingAngle),
                    passed = insulatorResult.clearancePassed,
                    notes = insulatorResult.clearancePassed ? "电气间隙满足规程防闪络要求" : "风偏角过大产生塔身空气间隙侵限，建议加装防风偏重锤或改用V串"
                });

                // 5. V串上拔校验
                if (insulatorResult.stringType == InsulatorStringType.V_String)
                {
                    items.Add(new ComplianceAuditItem
                    {
                        item = "V型悬垂绝缘子串防上拔稳定性校验",
                        codeReference = "DL/T 5582-2020 9.4.6",
                        standardRequirement = "大风工况下下风侧单臂受力严禁上拔 (No Lift-off)",
                        calculatedValue = insulatorResult.vStringLiftOff ? "发生上拔受拉失稳" : "双臂受压稳固",
                        passed = !insulatorResult.vStringLiftOff,
                        notes = !insulatorResult.vStringLiftOff ? "V串抗风偏稳定性合格" : "强风导致上拔，需调整V串夹角或加装垂直配重"
                    });
                }
            }

            return items;
        }
    }
}
