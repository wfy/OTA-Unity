using System.IO;
using OTA;
using OTA.Corridor;
using OTA.Corridor.Overlays;
using OTA.Corridor.UI;
using PointCloudRuntimeViewer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OTA.EditorTools
{
    public static class P0SceneUpdater
    {
        [MenuItem("OTA/1. Upgrade P0 Scene to Full Corridor Visualization")]
        public static void UpgradeP0Scene()
        {
            string scenePath = "Assets/Scenes/P0-LasView.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 1. Ensure PointCloudViewer has Corridor components
            var viewerGo = GameObject.Find("PointCloudViewer");
            if (viewerGo == null)
            {
                Debug.LogError("[P0SceneUpdater] PointCloudViewer not found in scene!");
                return;
            }

            var viewer = viewerGo.GetComponent<RuntimeViewerDX11>();
            var colorMgr = viewerGo.GetComponent<CorridorColorManager>();
            if (colorMgr == null) colorMgr = viewerGo.AddComponent<CorridorColorManager>();

            var importer = viewerGo.GetComponent<LasImporter>();
            if (importer == null) importer = viewerGo.AddComponent<LasImporter>();

            if (viewerGo.GetComponent<MeshFilter>() == null) viewerGo.AddComponent<MeshFilter>();
            if (viewerGo.GetComponent<MeshRenderer>() == null) viewerGo.AddComponent<MeshRenderer>();

            // 确保原生点云模式使用高帧率 1px 像素材质，Mesh 模式使用自适应材质
            var pixelMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PointCloudTools/Materials/DX11/PointCloudColorDx11-Pixel.mat");
            var autoSizeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PointCloudTools/Materials/DX11/WillshareCesiumUnlitTilesetShader_PointCloudAutoSize_new.mat");
            if (pixelMat != null)
            {
                viewer.cloudMaterial = pixelMat;
            }
            if (autoSizeMat != null)
            {
                viewer.meshMaterial = autoSizeMat;
            }

            // Point to best sample Las
            importer.lasPath = LasDatasetRegistry.ResolveBestInitialLas();

            // 2. Ensure Camera has PostProcessLayer & SobelOutlineController
            var cam = Camera.main;
            if (cam == null) cam = Object.FindObjectOfType<Camera>();
            if (cam != null)
            {
                var ppLayer = cam.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>();
                if (ppLayer == null) ppLayer = cam.gameObject.AddComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>();
                ppLayer.volumeTrigger = cam.transform;
                ppLayer.volumeLayer = LayerMask.GetMask("Default") | 1;
                ppLayer.enabled = false; // 默认关闭，杜绝深度法线预通道三倍顶点开销

                var sobelCtrl = cam.GetComponent<SobelOutlineController>();
                if (sobelCtrl == null) sobelCtrl = cam.gameObject.AddComponent<SobelOutlineController>();
                sobelCtrl.outlineEnabled = false;
            }

            // 3. Ensure GlobalPostProcessVolume exists in scene with SobelOutline effect
            var ppGo = GameObject.Find("GlobalPostProcessVolume");
            if (ppGo == null) ppGo = new GameObject("GlobalPostProcessVolume");
            var volume = ppGo.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessVolume>();
            if (volume == null) volume = ppGo.AddComponent<UnityEngine.Rendering.PostProcessing.PostProcessVolume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f; // 默认权重 0

            string profilePath = "Assets/SobelOutline/SobelProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.PostProcessing.PostProcessProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.PostProcessing.PostProcessProfile>();
                var outline = profile.AddSettings<VertexFragment.SobelOutline>();
                outline.enabled.Override(true);
                outline.thickness.Override(1.2f);
                outline.color.Override(Color.black);
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            volume.profile = profile;

            // 4. Ensure CorridorManager exists with Section & Overlay managers
            var corridorGo = GameObject.Find("CorridorManager");
            if (corridorGo == null) corridorGo = new GameObject("CorridorManager");

            var sectionMgr = corridorGo.GetComponent<CorridorSectionManager>();
            if (sectionMgr == null) sectionMgr = corridorGo.AddComponent<CorridorSectionManager>();

            var overlayMgr = corridorGo.GetComponent<CorridorOverlayManager>();
            if (overlayMgr == null) overlayMgr = corridorGo.AddComponent<CorridorOverlayManager>();

            sectionMgr.overlayManager = overlayMgr;

            // 3. Ensure CorridorHUD is attached
            var hudGo = GameObject.Find("CorridorHUD");
            if (hudGo == null) hudGo = new GameObject("CorridorHUD");
            var hud = hudGo.GetComponent<CorridorHUD>();
            if (hud == null) hud = hudGo.AddComponent<CorridorHUD>();

            hud.colorManager = colorMgr;
            hud.sectionManager = sectionMgr;
            hud.overlayManager = overlayMgr;
            hud.importer = importer;

            // 5. Ensure AimIndicator is red bold circular marker
            var aimGo = GameObject.Find("AimIndicator");
            if (aimGo != null)
            {
                var txt = aimGo.GetComponent<UnityEngine.UI.Text>();
                if (txt != null) Object.DestroyImmediate(txt);

                var img = aimGo.GetComponent<UnityEngine.UI.Image>();
                if (img == null) img = aimGo.AddComponent<UnityEngine.UI.Image>();

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PointCloudTools/Textures/UI/aim_indicator_marker.png");
                if (sprite != null) img.sprite = sprite;
                img.color = new Color(1f, 0f, 0f, 1f);
                img.raycastTarget = false;
                img.preserveAspect = true;

                var rt = aimGo.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(46f, 46f);
            }

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();
            Debug.Log("[P0SceneUpdater] Successfully upgraded P0-LasView.unity to Full P1 Corridor Visualization!");
        }
    }

    [InitializeOnLoad]
    public static class P0SceneAutoUpgrader
    {
        static P0SceneAutoUpgrader()
        {
            EditorApplication.delayCall += ExecuteOnce;
        }

        private static void ExecuteOnce()
        {
            if (!SessionState.GetBool("P0SceneAutoUpgraded_SobelV1", false))
            {
                SessionState.SetBool("P0SceneAutoUpgraded_SobelV1", true);
                P0SceneUpdater.UpgradeP0Scene();
            }
        }
    }
}