using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OTA.Corridor.Mechanics
{
    [Serializable]
    public class StringingCell
    {
        public float temp;      // °C
        public float stress;    // N/mm²
        public float tension;   // N
        public float sag;       // m
    }

    [Serializable]
    public class StringingRow
    {
        public float span;      // m
        public List<StringingCell> cells = new List<StringingCell>();
    }

    /// <summary>
    /// 施工架线安装张力与弧垂百米百度放样换算器
    /// </summary>
    public static class StringingCalculator
    {
        public static readonly float[] DefaultTemps = new float[] { -10f, 0f, 10f, 15f, 20f, 30f, 40f };
        public static readonly float[] DefaultSpans = new float[] { 100f, 200f, 300f, 400f, 500f, 600f, 700f, 800f };

        public static List<StringingRow> CalculateTable(
            ConductorSpec conductor,
            float representativeSpan,
            float[] temps = null,
            float[] spans = null)
        {
            if (temps == null) temps = DefaultTemps;
            if (spans == null) spans = DefaultSpans;

            var rows = new List<StringingRow>();

            // 1. 获取代表档距下的控制工况基准
            ConductorMechanics.CalculateAllConditions(conductor, representativeSpan, out string govId);
            var conditions = ConductorMechanics.GetDefaultConditions();
            var govCond = conditions.Find(c => c.id == govId) ?? conditions[0];

            float S = conductor.totalArea * conductor.bundleNumber;
            float E = conductor.elasticModulus;
            float alpha = conductor.thermalExpansion;
            float Tp = conductor.ratedStrength * conductor.bundleNumber;
            float allowableStress = (Tp / 2.5f) / S;
            float govGamma = ConductorMechanics.CalculateSpecificLoad(conductor, govCond.windSpeed, govCond.iceThickness, representativeSpan, out _, out _);

            // 2. 遍历各档距与温度网格
            foreach (float span in spans)
            {
                var row = new StringingRow { span = span };

                // 当前档距自重比载 (无风无冰安装状态)
                float g1 = ConductorMechanics.CalculateSpecificLoad(conductor, 0f, 0f, span, out _, out _);

                foreach (float t in temps)
                {
                    // 状态方程从控制工况向安装状态解算
                    float solvedStress = ConductorMechanics.SolveStateEquation(
                        allowableStress, govGamma, govCond.temp,
                        g1, t, span, E, alpha);

                    float tension = solvedStress * S;
                    float sag = (g1 * span * span) / (8f * solvedStress);

                    row.cells.Add(new StringingCell
                    {
                        temp = t,
                        stress = solvedStress,
                        tension = tension,
                        sag = sag
                    });
                }

                rows.Add(row);
            }

            return rows;
        }

        public static string ExportToCsv(ConductorSpec conductor, List<StringingRow> table, float[] temps = null)
        {
            if (temps == null) temps = DefaultTemps;
            var sb = new StringBuilder();

            sb.Append("档距 L (m)");
            foreach (float t in temps)
            {
                sb.Append($",{t}°C 张力(kN),{t}°C 弧垂(m)");
            }
            sb.AppendLine();

            foreach (var row in table)
            {
                sb.Append(row.span.ToString("F0"));
                foreach (var cell in row.cells)
                {
                    sb.Append($",{(cell.tension / 1000f):F2},{cell.sag:F2}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
