using System;
using Metervara.Interaction;
using PointCloudRuntimeViewer;
using UnityEngine;

namespace OTA.Corridor.Annotation
{
    /// <summary>
    /// Core coordinator for P2 Interactive Annotation Closed Loop.
    /// Manages modeless Shift-key interaction state machine, 3D target raycast, and point click dispatch.
    /// </summary>
    public class AnnotationController : MonoBehaviour
    {
        public static AnnotationController Instance { get; private set; }

        public PanZoomOrbit panCam;
        public AnnotationTargetMode currentMode = AnnotationTargetMode.None;
        public RuntimeViewerDX11 viewer;

        private Cursor3DIndicator cursorIndicator;
        private Camera targetCamera;

        public static bool IsShiftHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        public static bool IsAnnotating => Instance != null && IsShiftHeld && Instance.currentMode != AnnotationTargetMode.None;

        public Vector3 CurrentHitPoint { get; private set; }
        public bool HasValidHit { get; private set; }

        public event Action<Vector3> OnPointClicked;

        void Awake()
        {
            Instance = this;
            EnsureReferences();
        }

        public void EnsureReferences()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
                if (targetCamera == null) targetCamera = CameraHelper.MainCamera;
            }

            if (panCam == null)
            {
                panCam = GetComponent<PanZoomOrbit>();
                if (panCam == null && targetCamera != null) panCam = targetCamera.GetComponent<PanZoomOrbit>();
                if (panCam == null && PanZoomOrbitMouse.Instance != null) panCam = PanZoomOrbitMouse.Instance;
                if (panCam == null) panCam = FindObjectOfType<PanZoomOrbit>();
            }

            if (cursorIndicator == null)
            {
                cursorIndicator = GetComponent<Cursor3DIndicator>();
                if (cursorIndicator == null && targetCamera != null) cursorIndicator = targetCamera.GetComponent<Cursor3DIndicator>();
                if (cursorIndicator == null) cursorIndicator = gameObject.AddComponent<Cursor3DIndicator>();
            }

            if (viewer == null)
            {
                viewer = FindObjectOfType<RuntimeViewerDX11>();
            }

            if (panCam != null && panCam.viewer == null && viewer != null)
            {
                panCam.viewer = viewer;
            }
        }

        public void SetMode(AnnotationTargetMode mode)
        {
            currentMode = mode;
            EnsureReferences();
            Debug.Log($"[AnnotationController] Switched Annotation Mode to: {mode}");
        }

        void Update()
        {
            if (targetCamera == null || panCam == null || cursorIndicator == null || viewer == null)
            {
                EnsureReferences();
            }

            // Modeless activation: only active when Shift is held AND an annotation mode is selected AND not over UI
            bool overUI = IsPointerOverUI(Input.mousePosition);
            bool active = IsShiftHeld && currentMode != AnnotationTargetMode.None && !PanZoomOrbit.BlockCameraInput && !overUI;

            if (active && panCam != null && targetCamera != null)
            {
                Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
                if (panCam.HitCloud(ray, out Vector3 hitPos, out float hitDist))
                {
                    HasValidHit = true;
                    CurrentHitPoint = hitPos;

                    // Crucial: reticle origin placed on view ray at hit distance => exactly 0 pixel offset on screen!
                    Vector3 reticleOnRay = ray.origin + ray.direction * hitDist;

                    cursorIndicator.SetVisible(true);
                    cursorIndicator.UpdatePosition(reticleOnRay, -ray.direction);

                    // Capture Left Click for Annotation
                    if (Input.GetMouseButtonDown(0))
                    {
                        cursorIndicator.FlashClick();
                        OnPointClicked?.Invoke(hitPos);
                    }
                }
                else
                {
                    HasValidHit = false;
                    cursorIndicator.SetVisible(false);
                }
            }
            else
            {
                HasValidHit = false;
                if (cursorIndicator != null)
                {
                    cursorIndicator.SetVisible(false);
                }
            }

            // 2. 渲染界面已取消点击显示包围盒功能，避免漫游与平仪时场景误触弹盒
            // （仅通过左侧主控台双击杆塔才会显示包围盒与尺寸参考）
        }

        public static bool IsPointerOverUI(Vector3 mousePos)
        {
            var hud = OTA.Corridor.UI.CorridorHUD.Instance;
            if (hud == null) hud = FindObjectOfType<OTA.Corridor.UI.CorridorHUD>();
            if (hud == null) return false;
            return hud.IsPointerOverUI(mousePos);
        }
    }
}
