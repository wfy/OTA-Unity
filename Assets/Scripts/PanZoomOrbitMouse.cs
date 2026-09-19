using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Metervara.Interaction
{
    public enum CameraViewPreset
    {
        Reset,       // 复位 (默认鸟瞰透视)
        Top,         // 上 (俯视)
        Bottom,      // 下 (仰视)
        Left,        // 左 (左侧视)
        Right,       // 右 (右侧视)
        Front,       // 前 (正视)
        Back,        // 后 (后视)
        Isometric    // 透视 (45° 空间轴测)
    }

    /// <summary>
    /// Mouse interaction for cloud browsing:
    ///  - Left drag: smooth pan
    ///  - Right drag: orbit around picked point
    ///  - Wheel: zoom-to-cursor
    ///  - Annotation isolation: blocks camera moves when Shift is held in annotation mode
    ///  - Camera flyto helpers: FlyToTopDownView & FlyToTowerView
    /// </summary>
    public class PanZoomOrbitMouse : PanZoomOrbit
    {
        public static PanZoomOrbitMouse Instance { get; private set; }

        public float zoomSpeed = 1;
        public float orbitSpeed = 10;

        public RectTransform aimIndicator;
        public Canvas aimCanvas;

        private bool leftDragging = false;
        private bool rightDragging = false;
        private Vector3 previousMousePosition;
        private float panFocalDistance = 100f;

        private Vector3 orbitPivot;
        public Vector3 Pivot { get { return orbitPivot; } }
        private Vector3 orbitCameraVector;
        private Vector3 orbitCameraRight;
        private float orbitYawInitial;
        private float orbitPitchInitial;
        private float orbitYaw;
        private float orbitPitch;
        private Quaternion orbitInitialRotation;
        private bool orbitInitialized = false;

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
            EnsureAimIndicatorVisual();
        }

        private void EnsureAimIndicatorVisual()
        {
            if (aimIndicator == null) return;
            var img = aimIndicator.GetComponent<UnityEngine.UI.Image>();
            var txt = aimIndicator.GetComponent<UnityEngine.UI.Text>();
            if (txt != null && img == null)
            {
                Destroy(txt);
                img = aimIndicator.gameObject.AddComponent<UnityEngine.UI.Image>();
            }
            if (img != null)
            {
                if (img.sprite == null)
                {
                    img.sprite = Resources.Load<Sprite>("UI/aim_indicator_marker");
                }
                img.color = new Color(1f, 0f, 0f, 1f);
                img.raycastTarget = false;
                img.preserveAspect = true;
            }
            aimIndicator.sizeDelta = new Vector2(46f, 46f);
        }

                public void FocusOnBounds(Bounds b)
        {
            Vector3 target = b.center;
            float maxDim = Mathf.Max(b.size.x, b.size.z, b.size.y);
            float dist = Mathf.Clamp(maxDim * 1.2f, 30f, 1500f);
            transform.position = target + new Vector3(0, dist * 0.45f, -dist * 0.8f);
            transform.LookAt(target);
            panFocalDistance = dist;
            orbitPivot = target;
        }

        public void SetPresetView(CameraViewPreset preset)
        {
            Vector3 target = orbitPivot;
            if (target == Vector3.zero)
            {
                if (boundsReady && cloudBounds.size.sqrMagnitude > 0.1f)
                    target = cloudBounds.center;
                else
                    target = transform.position + transform.forward * Mathf.Max(30f, panFocalDistance);
            }

            float dist = Mathf.Clamp(panFocalDistance, 25f, 1500f);
            if (boundsReady && (dist <= 25f || dist > 1500f))
            {
                float maxDim = Mathf.Max(cloudBounds.size.x, cloudBounds.size.y, cloudBounds.size.z);
                dist = Mathf.Clamp(maxDim * 1.2f, 30f, 1500f);
            }

            switch (preset)
            {
                case CameraViewPreset.Reset:
                    if (boundsReady)
                    {
                        FocusOnBounds(cloudBounds);
                    }
                    else
                    {
                        transform.position = target + new Vector3(0, dist * 0.45f, -dist * 0.8f);
                        transform.LookAt(target);
                        panFocalDistance = dist;
                        orbitPivot = target;
                    }
                    break;

                case CameraViewPreset.Top:
                    transform.position = target + Vector3.up * dist;
                    transform.LookAt(target, Vector3.forward);
                    panFocalDistance = dist;
                    orbitPivot = target;
                    break;

                case CameraViewPreset.Bottom:
                    transform.position = target - Vector3.up * dist;
                    transform.LookAt(target, -Vector3.forward);
                    panFocalDistance = dist;
                    orbitPivot = target;
                    break;

                case CameraViewPreset.Front:
                    transform.position = target - Vector3.forward * dist;
                    transform.LookAt(target, Vector3.up);
                    panFocalDistance = dist;
                    orbitPivot = target;
                    break;

                case CameraViewPreset.Back:
                    transform.position = target + Vector3.forward * dist;
                    transform.LookAt(target, Vector3.up);
                    panFocalDistance = dist;
                    orbitPivot = target;
                    break;

                case CameraViewPreset.Left:
                    transform.position = target - Vector3.right * dist;
                    transform.LookAt(target, Vector3.up);
                    panFocalDistance = dist;
                    orbitPivot = target;
                    break;

                case CameraViewPreset.Right:
                    transform.position = target + Vector3.right * dist;
                    transform.LookAt(target, Vector3.up);
                    panFocalDistance = dist;
                    orbitPivot = target;
                    break;

                case CameraViewPreset.Isometric:
                    transform.position = target + new Vector3(dist * 0.707f, dist * 0.5f, -dist * 0.707f);
                    transform.LookAt(target, Vector3.up);
                    panFocalDistance = dist;
                    orbitPivot = target;
                    break;
            }
        }

        public void FlyToTopDownView(Vector3 targetCenter, float height = 80f)
        {
            Vector3 topDownPos = new Vector3(targetCenter.x, targetCenter.y + height, targetCenter.z - height * 0.1f);
            transform.position = topDownPos;
            transform.rotation = Quaternion.Euler(85f, 0f, 0f);
        }

        public void FocusOnTower(OTA.Corridor.Annotation.TowerAnnotation tower)
        {
            if (tower == null) return;
            Vector3 target = new Vector3(tower.topPosition.x, tower.basePosition.y + tower.totalHeight * 0.5f, tower.topPosition.z);
            float dist = Mathf.Max(30f, tower.totalHeight * 1.5f);
            transform.position = target + new Vector3(0, tower.totalHeight * 0.35f, -dist);
            transform.LookAt(target);
            panFocalDistance = dist;
            orbitPivot = target;
        }

        public void FocusOnConductor(OTA.Corridor.Annotation.ConductorAnnotation cond)
        {
            if (cond == null) return;
            Vector3 target = cond.midLowestPoint;
            float dist = Mathf.Max(35f, cond.horizontalSpan * 0.45f);
            transform.position = target + new Vector3(0, dist * 0.4f, -dist * 0.8f);
            transform.LookAt(target);
            panFocalDistance = dist;
            orbitPivot = target;
        }
        public void FlyToTowerView(OTA.Corridor.Annotation.TowerAnnotation tower)
        {
            if (tower == null) return;
            float rad = (tower.yawAngle + 90f) * Mathf.Deg2Rad;
            Vector3 fwdDir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            float dist = Mathf.Max(25f, tower.totalHeight * 1.35f);
            Vector3 midH = new Vector3(tower.topPosition.x, tower.basePosition.y + tower.totalHeight * 0.5f, tower.topPosition.z);
            Vector3 camPos = midH + fwdDir * dist + Vector3.up * (tower.totalHeight * 0.2f);
            transform.position = camPos;
            transform.LookAt(midH);
        }

        void Update()
        {
            if (BlockCameraInput)
            {
                leftDragging = false;
                rightDragging = false;
                if (aimIndicator != null && aimIndicator.gameObject.activeSelf)
                {
                    aimIndicator.gameObject.SetActive(false);
                }
                return;
            }

            bool isAnnotating = OTA.Corridor.Annotation.AnnotationController.IsAnnotating;

            // When user holds Shift in annotation mode, bypass all camera manipulations
            if (isAnnotating)
            {
                leftDragging = false;
                rightDragging = false;
                if (aimIndicator != null && aimIndicator.gameObject.activeSelf)
                {
                    aimIndicator.gameObject.SetActive(false);
                }
                return;
            }

            Vector3 mouse = Input.mousePosition;
            bool overUI = OTA.Corridor.UI.CorridorHUD.Instance != null && OTA.Corridor.UI.CorridorHUD.Instance.IsPointerOverUI(mouse);
            if (!overUI && UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                overUI = true;
            }

            if (overUI)
            {
                if (aimIndicator != null && aimIndicator.gameObject.activeSelf)
                {
                    aimIndicator.gameObject.SetActive(false);
                }
                // If not already in an active 3D drag, completely block starting drag or zoom on UI
                if (!leftDragging && !rightDragging)
                {
                    return;
                }
            }

            // Left drag: Pan on adapted focal plane
            if (Input.GetMouseButtonDown(0))
            {
                if (!overUI)
                {
                    leftDragging = true;
                    previousMousePosition = mouse;

                    Ray ray = Camera.ScreenPointToRay(mouse);
                    if (HitCloud(ray, out Vector3 hit, out float d))
                    {
                        panFocalDistance = Vector3.Dot(hit - transform.position, transform.forward);
                    }
                    else if (boundsReady)
                    {
                        panFocalDistance = Vector3.Dot(sphereCenter - transform.position, transform.forward);
                    }
                    if (panFocalDistance < 1f || float.IsNaN(panFocalDistance) || float.IsInfinity(panFocalDistance))
                    {
                        panFocalDistance = 100f;
                    }
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                leftDragging = false;
            }

            // Right drag: Orbit around picked cloud point
            if (Input.GetMouseButtonDown(1))
            {
                if (!overUI)
                {
                    rightDragging = true;
                    orbitInitialized = false;
                    previousMousePosition = mouse;
                    Ray ray = Camera.ScreenPointToRay(Input.mousePosition);
                    float d;
                    if (HitCloud(ray, out orbitPivot, out d))
                    {
                        orbitInitialized = true;
                    }
                    else
                    {
                        orbitPivot = transform.position + transform.forward * panFocalDistance;
                        orbitInitialized = true;
                    }

                    orbitCameraVector = transform.position - orbitPivot;
                    orbitCameraRight = transform.right;
                    orbitYawInitial = transform.eulerAngles.y;
                    orbitPitchInitial = transform.eulerAngles.x;
                    orbitYaw = orbitYawInitial;
                    orbitPitch = orbitPitchInitial;
                    orbitInitialRotation = transform.rotation;
                }
            }
            else if (Input.GetMouseButtonUp(1))
            {
                rightDragging = false;
                if (aimIndicator != null && aimIndicator.gameObject.activeSelf)
                {
                    aimIndicator.gameObject.SetActive(false);
                }
            }

            float scrollDelta = Input.GetAxis("Mouse ScrollWheel");

            if (leftDragging)
            {
                Pan(previousMousePosition, mouse, panFocalDistance);
                previousMousePosition = mouse;
            }

            if (rightDragging && orbitInitialized)
            {
                float strafe = Input.GetAxis("Mouse X");
                float fwd = Input.GetAxis("Mouse Y");

                orbitYaw = (orbitYaw + orbitSpeed * 0.5f * strafe) % 360f;
                orbitPitch = Mathf.Clamp(orbitPitch - orbitSpeed * 0.5f * fwd, -89f, 89f);
                var between = Quaternion.AngleAxis(orbitYaw - orbitYawInitial, Vector3.up) *
                              Quaternion.AngleAxis(orbitPitch - orbitPitchInitial, orbitCameraRight);
                transform.position = orbitPivot + between * orbitCameraVector;
                transform.rotation = between * orbitInitialRotation;

                if (aimIndicator != null)
                {
                    Vector3 sp = Camera.WorldToScreenPoint(orbitPivot);
                    if (sp.z > 0f)
                    {
                        if (!aimIndicator.gameObject.activeSelf) aimIndicator.gameObject.SetActive(true);
                        aimIndicator.position = sp;
                    }
                    else
                    {
                        if (aimIndicator.gameObject.activeSelf) aimIndicator.gameObject.SetActive(false);
                    }
                }
            }

            // Wheel: Zoom-to-cursor on the exact same plane as pan
            if (Mathf.Abs(scrollDelta) > 0.0001f && !overUI)
            {
                float factor = 1f - scrollDelta * zoomSpeed;
                ZoomMultiplyDistance(mouse, factor, panFocalDistance);
                panFocalDistance = Mathf.Clamp(panFocalDistance * factor, 1f, 10000f);
            }
        }
    }
}
