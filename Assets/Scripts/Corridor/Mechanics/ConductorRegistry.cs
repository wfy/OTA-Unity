using System;
using System.Collections.Generic;

namespace OTA.Corridor.Mechanics
{
    /// <summary>
    /// 全系列架空输电线路导线规格物性库 (ACSR 钢芯铝绞线 / JL-G1A 系列)
    /// 符合 GB/T 1179-2017 与 DL/T 5582-2020 规程
    /// </summary>
    public static class ConductorRegistry
    {
        public static readonly ConductorSpec[] AllConductors = new ConductorSpec[]
        {
            new ConductorSpec
            {
                modelName = "LGJ-120/20",
                codeName = "35kV/110kV 轻载",
                outerDiameter = 15.07f,
                totalArea = 138.83f,
                unitMass = 0.489f,
                ratedStrength = 39420f,
                elasticModulus = 77000f,
                thermalExpansion = 18.9e-6f,
                bundleNumber = 1
            },
            new ConductorSpec
            {
                modelName = "LGJ-150/25",
                codeName = "110kV 常用导线",
                outerDiameter = 17.10f,
                totalArea = 173.08f,
                unitMass = 0.612f,
                ratedStrength = 49130f,
                elasticModulus = 77000f,
                thermalExpansion = 18.9e-6f,
                bundleNumber = 1
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
            },
            new ConductorSpec
            {
                modelName = "LGJ-185/30",
                codeName = "110kV 加强芯导线",
                outerDiameter = 18.88f,
                totalArea = 216.31f,
                unitMass = 0.762f,
                ratedStrength = 61400f,
                elasticModulus = 73000f,
                thermalExpansion = 19.2e-6f,
                bundleNumber = 1
            },
            new ConductorSpec
            {
                modelName = "LGJ-240/30",
                codeName = "220kV 常用双分裂",
                outerDiameter = 21.60f,
                totalArea = 275.96f,
                unitMass = 0.9222f,
                ratedStrength = 75340f,
                elasticModulus = 73000f,
                thermalExpansion = 19.3e-6f,
                bundleNumber = 2
            },
            new ConductorSpec
            {
                modelName = "LGJ-240/40",
                codeName = "220kV 加强型导线",
                outerDiameter = 21.66f,
                totalArea = 281.08f,
                unitMass = 0.978f,
                ratedStrength = 83200f,
                elasticModulus = 75000f,
                thermalExpansion = 19.0e-6f,
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
                modelName = "LGJ-300/50",
                codeName = "220kV 重冰区导线",
                outerDiameter = 24.20f,
                totalArea = 347.58f,
                unitMass = 1.225f,
                ratedStrength = 104500f,
                elasticModulus = 76000f,
                thermalExpansion = 18.9e-6f,
                bundleNumber = 2
            },
            new ConductorSpec
            {
                modelName = "LGJ-400/35",
                codeName = "500kV 典型四分裂",
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
                modelName = "LGJ-400/50",
                codeName = "500kV 常用导线",
                outerDiameter = 27.63f,
                totalArea = 451.55f,
                unitMass = 1.511f,
                ratedStrength = 124900f,
                elasticModulus = 70000f,
                thermalExpansion = 19.8e-6f,
                bundleNumber = 4
            },
            new ConductorSpec
            {
                modelName = "LGJ-500/45",
                codeName = "500kV 重载四分裂",
                outerDiameter = 30.00f,
                totalArea = 544.70f,
                unitMass = 1.765f,
                ratedStrength = 142200f,
                elasticModulus = 67000f,
                thermalExpansion = 20.2e-6f,
                bundleNumber = 4
            },
            new ConductorSpec
            {
                modelName = "LGJ-630/45",
                codeName = "750kV/1000kV 特高压",
                outerDiameter = 33.60f,
                totalArea = 666.51f,
                unitMass = 2.057f,
                ratedStrength = 159800f,
                elasticModulus = 63000f,
                thermalExpansion = 20.9e-6f,
                bundleNumber = 6
            },
            new ConductorSpec
            {
                modelName = "LGJ-720/50",
                codeName = "1000kV 特高压八分裂",
                outerDiameter = 36.20f,
                totalArea = 765.80f,
                unitMass = 2.378f,
                ratedStrength = 181200f,
                elasticModulus = 63000f,
                thermalExpansion = 20.9e-6f,
                bundleNumber = 8
            },
            new ConductorSpec
            {
                modelName = "JL/G1A-300/40",
                codeName = "高强度低弧垂导线",
                outerDiameter = 23.94f,
                totalArea = 338.99f,
                unitMass = 1.133f,
                ratedStrength = 95200f,
                elasticModulus = 76000f,
                thermalExpansion = 19.1e-6f,
                bundleNumber = 2
            },
            new ConductorSpec
            {
                modelName = "JL/G1A-400/35",
                codeName = "高强度四分裂导线",
                outerDiameter = 26.82f,
                totalArea = 425.24f,
                unitMass = 1.349f,
                ratedStrength = 108500f,
                elasticModulus = 68000f,
                thermalExpansion = 20.1e-6f,
                bundleNumber = 4
            }
        };

        public static ConductorSpec FindByName(string modelName)
        {
            if (string.IsNullOrEmpty(modelName)) return AllConductors[4]; // default LGJ-240/30
            for (int i = 0; i < AllConductors.Length; i++)
            {
                if (string.Equals(AllConductors[i].modelName, modelName, StringComparison.OrdinalIgnoreCase))
                {
                    return AllConductors[i];
                }
            }
            return AllConductors[4];
        }

        public static ConductorSpec GetDefaultByVoltage(int voltageLevel)
        {
            if (voltageLevel <= 110) return AllConductors[2]; // LGJ-185/25
            if (voltageLevel <= 220) return AllConductors[4]; // LGJ-240/30
            if (voltageLevel <= 500) return AllConductors[8]; // LGJ-400/35
            if (voltageLevel <= 750) return AllConductors[11]; // LGJ-630/45
            return AllConductors[12]; // LGJ-720/50 (1000kV)
        }
    }
}
