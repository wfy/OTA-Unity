using System;
using System.Diagnostics;
using PointCloudRuntimeViewer;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace OTA
{
    public class PerfDiag : MonoBehaviour
    {
        private float timer = 0f;
        private int frames = 0;
        private static int renderCount = 0;

        public static void RecordRender()
        {
            renderCount++;
        }

        void Start()
        {
            UnityEngine.Debug.Log("[PerfDiag] Started diagnostics tracker.");
        }

        void Update()
        {
            frames++;
            timer += Time.unscaledDeltaTime;
            if (timer >= 1.0f)
            {
                float fps = frames / timer;
                var viewer = FindObjectOfType<RuntimeViewerDX11>();
                var mr = viewer != null ? viewer.GetComponent<MeshRenderer>() : null;
                var cam = Camera.main ?? FindObjectOfType<Camera>();
                var ppl = cam != null ? cam.GetComponent<PostProcessLayer>() : null;

                UnityEngine.Debug.Log($"[PERF] FPS: {fps:F1} | DrawProcedural/sec: {renderCount} | " +
                                      $"ViewerMode: {(viewer != null ? viewer.currentRenderMode.ToString() : "null")} | " +
                                      $"MeshRenderer.enabled: {(mr != null ? mr.enabled.ToString() : "null")} | " +
                                      $"PostProcessLayer.enabled: {(ppl != null ? ppl.enabled.ToString() : "null")} | " +
                                      $"TotalPoints: {(viewer != null ? viewer.TotalPointCount : 0)} | " +
                                      $"AllCameras: {Camera.allCamerasCount}");

                frames = 0;
                timer = 0f;
                renderCount = 0;
            }
        }
    }
}
