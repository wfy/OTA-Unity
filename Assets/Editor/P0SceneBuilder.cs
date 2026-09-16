using System.IO;
using OTA;
using PointCloudRuntimeViewer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OTA.EditorTools
{
    /// <summary>
    /// P0 scene bootstrapper (run once or via menu):
    /// Main camera + RuntimeViewerDX11 + LasImporter bound to a LAS path.
    /// </summary>
    public static class P0SceneBuilder
    {
        [MenuItem("OTA/P0 Scene (build once)")]
        public static void BuildScene()
        {
            Build("Assets/Scenes/P0-LasView.unity");
        }

        public static void Run()
        {
            Build("Assets/Scenes/P0-LasView.unity");
        }

        private static void Build(string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // camera
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0, 30, -80);
            cam.transform.LookAt(Vector3.zero);
            cam.farClipPlane = 1500;
            camGo.AddComponent<OTA.P0OrbitCamera>();

            // lights
            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;

            // runtime viewer + importer
            var viewerGo = new GameObject("PointCloudViewer");
            var viewer = viewerGo.AddComponent<RuntimeViewerDX11>();
            viewer.cloudMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/PointCloudTools/Materials/DX11/PointCloudColorSizeDX11v2.mat");
            viewer.useDX11 = true;
            viewer.readRGB = true;
            viewer.flipYZ = true;
            viewer.autoOffsetNearZero = true;
            var importer = viewerGo.AddComponent<LasImporter>();
            importer.lasPath = "E:/unity/Plans/OTA/backend/data/uploads/result/3b295ca08ba0472fa41f6fbb1b00df76_17-18(17_18)_sign.las";
            viewerGo.AddComponent<MeshFilter>();
            viewerGo.AddComponent<MeshRenderer>();

            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.Refresh();
            Debug.Log("[OTA] P0 scene saved: " + path);
        }
    }
}
