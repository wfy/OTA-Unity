using System.IO;
using OTA.Corridor;
using OTA.Framework;
using UnityEditor;
using UnityEngine;

namespace OTA.EditorTools
{
    public static class OtaMenu
    {
        [MenuItem("OTA/1. Upgrade P0 Scene to Full Corridor Visualization")]
        public static void UpgradeScene()
        {
            P0SceneUpdater.UpgradeP0Scene();
        }

        [MenuItem("OTA/2. Quick Load 220kV Sample (17-18_sign)")]
        public static void LoadSampleCorridor()
        {
            var importer = Object.FindObjectOfType<LasImporter>();
            if (importer != null)
            {
                importer.Load(LasDatasetRegistry.ResolveBestInitialLas());
            }
            else
            {
                Debug.LogWarning("[OTA] No LasImporter found in active scene. Upgrade scene first.");
            }
        }

        [MenuItem("OTA/3. LAS -> V1 .bin (Convert)")]
        public static void ConvertOne()
        {
            var path = EditorUtility.OpenFilePanel("Select LAS file", "", "las");
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            ConvertPaletteFile(path);
        }

        private static bool ConvertPaletteFile(string lasPath)
        {
            var data = LasParser.Parse(lasPath);
            if (data.error != null)
            {
                Debug.LogError("[OTA] parse failed " + lasPath + ": " + data.error);
                return false;
            }
            var bin = LasParser.ConvertToV1Bin(ref data);
            if (bin == null) { Debug.LogError("[OTA] convert failed: " + lasPath); return false; }
            var outPath = lasPath + ".bin";
            File.WriteAllBytes(outPath, bin);
            Debug.Log("[OTA] wrote " + outPath + " (" + (bin.Length / 1024f / 1024f).ToString("F1") + " MB)");
            AssetDatabase.Refresh();
            return true;
        }
    }
}