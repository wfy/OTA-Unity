using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OTA.Corridor
{
    public static class LasDatasetRegistry
    {
        public const string DefaultDatasetRoot = "E:/unity/点云";
        public const string DefaultSampleLas = "E:/unity/点云/17-18(17_18)_sign.las";
        public const string FallbackSampleLas = "E:/unity/点云/17-18(17_18).las";

        public struct LasFileEntry
        {
            public string displayName;
            public string fullPath;
            public float sizeMb;
        }

        public static List<LasFileEntry> ScanLocalDatasets(string root = DefaultDatasetRoot)
        {
            var list = new List<LasFileEntry>();
            if (!Directory.Exists(root)) return list;

            try
            {
                var files = Directory.GetFiles(root, "*.las", SearchOption.AllDirectories);
                foreach (var f in files)
                {
                    var fi = new FileInfo(f);
                    list.Add(new LasFileEntry
                    {
                        displayName = Path.GetFileName(f),
                        fullPath = f.Replace("\\", "/"),
                        sizeMb = (float)(fi.Length / (1024.0 * 1024.0))
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LasDatasetRegistry] Scan error: " + e.Message);
            }
            return list;
        }

        public static string ResolveBestInitialLas()
        {
            if (File.Exists(DefaultSampleLas)) return DefaultSampleLas;
            if (File.Exists(FallbackSampleLas)) return FallbackSampleLas;
            var list = ScanLocalDatasets();
            if (list.Count > 0) return list[0].fullPath;
            return null;
        }
    }
}