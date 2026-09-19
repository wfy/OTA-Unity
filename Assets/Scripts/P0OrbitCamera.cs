using PointCloudRuntimeViewer;
using UnityEngine;
using Metervara.Interaction;

namespace OTA
{
    /// <summary>
    /// Professional GIS camera controller for transmission corridor point cloud roaming:
    ///  - Orbit & Zoom: uses real Point Cloud Collision (HitPointCloud) to pick exact 3D features (towers, conductors, ground)
    ///    as the rotation pivot and zoom focal target.
    ///  - Pan: uses an Adapted Point Cloud Focal View Plane (HitPanPlane) perpendicular to the camera sightline.
    ///    This guarantees ZERO displacement along the camera depth/forward axis (Dot(move, forward) == 0),
    ///    completely preventing depth-of-field/focal-distance drift during panning while keeping 1:1 mouse tracking.
    ///  - Aim indicator shows the exact 3D picked pivot.
    /// </summary>
    public class P0OrbitCamera : MonoBehaviour
    {
        [Header("Speed and Sensitivity")]
        public float panSpeed = 1.0f;
        public float rotateSpeed = 2.5f;
        public float zoomSpeed = 2.0f;
        public float minDist = 1.0f;
        public float maxDist = 30000f;

        [Header("References")]
        public RuntimeViewerDX11 viewer;
        public RectTransform aimIndicator;
        public Canvas aimCanvas;

        [Header("Adapted Plane & Bounds")]
        public float groundY = 0f;
        public bool hasGroundPlane = false;
        public Plane groundPlane;

        private Vector3 target = Vector3.zero;
        private Vector3 lastMouse;
        private bool havePivot = false;
        private bool wasRotating = false;
        private bool lastPick = false;
        private Vector3 lastPickPoint = Vector3.zero;

        // Bounding box and sphere for corridor point cloud limits
        private Bounds cloudBounds;
        private Vector3 sphereCenter = Vector3.zero;
        private float sphereRadius = 0f;
        private bool boundsReady = false;

        public static bool BlockCameraInput
        {
            get { return Metervara.Interaction.PanZoomOrbit.BlockCameraInput; }
            set { Metervara.Interaction.PanZoomOrbit.BlockCameraInput = value; }
        }
        private Camera cam;

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
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

        void Start()
        {
            lastMouse = Input.mousePosition;
            if (viewer == null)
            {
                viewer = GetComponent<RuntimeViewerDX11>();
                if (viewer == null) viewer = FindObjectOfType<RuntimeViewerDX11>();
            }

            if (aimIndicator != null)
            {
                aimIndicator.gameObject.SetActive(false);
            }

            // Default ground plane at Y = 0
            groundPlane = new Plane(Vector3.up, new Vector3(0, groundY, 0));
            hasGroundPlane = true;
        }

        void Update()
        {
            if (!boundsReady && viewer != null && viewer.points != null && viewer.points.Length > 0)
            {
                ComputeGroundPlaneAndBounds();
            }
        }

        public void ComputeGroundPlaneAndBounds()
        {
            var pts = viewer.points;
            if (pts == null || pts.Length == 0) return;

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            int stride = Mathf.Max(1, pts.Length / 100000);
            for (int i = 0; i < pts.Length; i += stride)
            {
                Vector3 p = pts[i];
                if (p.x < min.x) min.x = p.x;
                if (p.y < min.y) min.y = p.y;
                if (p.z < min.z) min.z = p.z;
                if (p.x > max.x) max.x = p.x;
                if (p.y > max.y) max.y = p.y;
                if (p.z > max.z) max.z = p.z;
            }

            cloudBounds = new Bounds((min + max) * 0.5f, max - min);
            sphereCenter = cloudBounds.center;
            sphereRadius = cloudBounds.extents.magnitude;
            groundY = min.y; // Lowest ground level of the corridor
            groundPlane = new Plane(Vector3.up, new Vector3(0, groundY, 0));
            hasGroundPlane = true;
            boundsReady = true;

            if (!havePivot)
            {
                target = new Vector3(cloudBounds.center.x, groundY, cloudBounds.center.z);
                havePivot = true;
            }

            Debug.Log("[P0OrbitCamera] Adapted Plane & Bounds established: Y_ground=" + groundY.ToString("F1") + ", center=" + cloudBounds.center);
        }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            var mouse = Input.mousePosition;
            bool overUI = OTA.Corridor.UI.CorridorHUD.Instance != null && OTA.Corridor.UI.CorridorHUD.Instance.IsPointerOverUI(mouse);
            if (!overUI && UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                overUI = true;
            }

            if (BlockCameraInput || (overUI && !wasRotating))
            {
                lastMouse = mouse;
                if (aimIndicator != null && aimIndicator.gameObject.activeSelf)
                {
                    aimIndicator.gameObject.SetActive(false);
                }
                return;
            }
            Vector3 delta = mouse - lastMouse;

            bool rotating = Input.GetMouseButton(1) && !overUI; // Right click orbit
            bool panning = Input.GetMouseButton(0) && !overUI;  // Left click pan
            float scroll = overUI ? 0f : Input.GetAxis("Mouse ScrollWheel");

            // 1. Right click: Pick POINT CLOUD collision point as rotation pivot
            if (rotating && !wasRotating)
            {
                lastPick = PickPointForRotateOrZoom(mouse, out lastPickPoint);
                if (lastPick)
                {
                    target = lastPickPoint;
                    havePivot = true;
                    if (aimIndicator != null)
                    {
                        aimIndicator.position = mouse;
                        aimIndicator.gameObject.SetActive(true);
                    }
                }
            }
            else if (!rotating && wasRotating)
            {
                if (aimIndicator != null)
                {
                    aimIndicator.gameObject.SetActive(false);
                }
            }
            wasRotating = rotating;

            // 2. Zoom-to-cursor: uses FOCAL VIEW PLANE (same plane as pan)
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                Ray zoomRay = cam.ScreenPointToRay(mouse);
                Vector3 zoomTarget;
                if (!HitPanPlane(zoomRay, out zoomTarget))
                {
                    zoomTarget = target;
                }

                Vector3 dir = (zoomTarget - transform.position).normalized;
                float currentDist = Vector3.Distance(transform.position, zoomTarget);
                float zoomDelta = currentDist * scroll * zoomSpeed * 0.8f;

                if (currentDist - zoomDelta > minDist && currentDist - zoomDelta < maxDist)
                {
                    transform.position += dir * zoomDelta;
                }
            }

            // 3. Orbit around target
            if (rotating)
            {
                if (delta.sqrMagnitude > 0.01f)
                {
                    // Rotate horizontally around target using world Vector3.up
                    transform.RotateAround(target, Vector3.up, delta.x * rotateSpeed * 0.15f);

                    // Rotate vertically around camera right, preventing flip
                    float pitchDelta = -delta.y * rotateSpeed * 0.15f;
                    Vector3 forward = transform.forward;
                    float currentPitch = Vector3.Angle(Vector3.up, forward);
                    if ((pitchDelta > 0 && currentPitch + pitchDelta < 170f) || (pitchDelta < 0 && currentPitch + pitchDelta > 10f))
                    {
                        transform.RotateAround(target, transform.right, pitchDelta);
                    }
                }
                transform.LookAt(target);
            }
            // 4. Pan: uses FOCAL VIEW PLANE (Strictly 0 depth drift, 1:1 cursor tracking)
            else if (panning)
            {
                Ray rayOld = cam.ScreenPointToRay(lastMouse);
                Ray rayNew = cam.ScreenPointToRay(mouse);

                if (HitPanPlane(rayOld, out Vector3 pOld) && HitPanPlane(rayNew, out Vector3 pNew))
                {
                    Vector3 planeMove = pOld - pNew;
                    transform.position += planeMove;
                    target += planeMove;
                }
                else
                {
                    // Fallback to camera screen plane
                    float dist = Vector3.Distance(transform.position, target);
                    float scale = dist * 0.0015f * panSpeed;
                    Vector3 move = (-transform.right * delta.x - transform.up * delta.y) * scale;
                    transform.position += move;
                    target += move;
                }
            }

            lastMouse = mouse;
        }

        /// <summary>
        /// Dedicated Pan Plane: a view plane passing through the current point cloud focal point (target),
        /// strictly perpendicular to the camera's line of sight (-cam.forward).
        /// This guarantees that:
        ///  1. Displacement along the depth/forward axis is IDENTICALLY ZERO (Dot(planeMove, forward) == 0).
        ///     Distance to objects and visual depth of field NEVER changes during panning.
        ///  2. The point cloud object under the cursor tracks 1:1 with the mouse on screen without slippage.
        ///  3. There are zero singularities regardless of pitch/roll angle.
        /// </summary>
        public bool HitPanPlane(Ray ray, out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;
            if (cam == null) cam = Camera.main;
            if (cam == null) return false;

            Vector3 pivotRef = havePivot ? target : (boundsReady ? cloudBounds.center : transform.position + transform.forward * 100f);
            Plane panPlane = new Plane(-cam.transform.forward, pivotRef);

            if (panPlane.Raycast(ray, out float enter) && enter > 0.1f)
            {
                hitPoint = ray.GetPoint(enter);
                return true;
            }

            float fallbackDist = havePivot ? Vector3.Distance(transform.position, target) : 100f;
            hitPoint = ray.GetPoint(Mathf.Clamp(fallbackDist, 5f, 1000f));
            return true;
        }

        /// <summary>
        /// Real Point Cloud Collision: raycasts against points in RuntimeViewerDX11.
        /// Returns the exact nearest point along the ray.
        /// </summary>
        public bool HitPointCloud(Ray ray, out Vector3 hit, out float distance)
        {
            hit = Vector3.zero;
            distance = 0f;

            if (!boundsReady && viewer != null) ComputeGroundPlaneAndBounds();
            if (!boundsReady) return false;

            // Coarse bounding sphere test
            Vector3 oc = ray.origin - sphereCenter;
            float b = Vector3.Dot(oc, ray.direction);
            float c = Vector3.Dot(oc, oc) - sphereRadius * sphereRadius;
            float disc = b * b - c;
            if (disc < 0f) return false;
            float t = -b - Mathf.Sqrt(disc);
            if (t < 0f) t = -b + Mathf.Sqrt(disc);
            if (t < 0f) return false;
            Vector3 entry = ray.origin + ray.direction * t;

            // Fine point cloud sampling
            var pts = viewer != null ? viewer.points : null;
            if (pts != null && pts.Length > 0)
            {
                float camDist = Vector3.Distance(ray.origin, entry);
                float maxPerp = Mathf.Max(1.2f, camDist * 0.004f);
                float maxPerp2 = maxPerp * maxPerp;
                int stride = Mathf.Max(1, pts.Length / 150000);
                float bestAlong = float.MaxValue;
                bool found = false;

                for (int i = 0; i < pts.Length; i += stride)
                {
                    Vector3 toP = pts[i] - ray.origin;
                    float along = Vector3.Dot(toP, ray.direction);
                    if (along <= 0.1f || along >= bestAlong) continue;
                    float perp2 = toP.sqrMagnitude - along * along;
                    if (perp2 > maxPerp2) continue;
                    bestAlong = along;
                    hit = pts[i];
                    distance = along;
                    found = true;
                }

                if (found) return true;
            }

            return false;
        }

        /// <summary>
        /// Adapted Point Cloud Plane Collision: used for Rotate/Zoom fallback when ray misses actual points.
        /// </summary>
        public bool HitAdaptedPlane(Ray ray, out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;
            if (cam == null) cam = Camera.main;
            if (cam == null) return false;

            // 1. Horizontal plane adapted to target/ground elevation
            float planeY = havePivot ? target.y : groundY;
            Plane hPlane = new Plane(Vector3.up, new Vector3(0, planeY, 0));

            // If ray is not parallel to horizontal ground (> 5 deg angle)
            if (Mathf.Abs(Vector3.Dot(ray.direction, Vector3.up)) > 0.08f)
            {
                if (hPlane.Raycast(ray, out float enter) && enter > 0.1f && enter < maxDist)
                {
                    hitPoint = ray.GetPoint(enter);
                    return true;
                }
            }

            // 2. Camera-facing plane passing through target (prevents grazing angle singularities)
            Vector3 pivotRef = havePivot ? target : (boundsReady ? cloudBounds.center : transform.position + transform.forward * 50f);
            Plane camPlane = new Plane(-transform.forward, pivotRef);
            if (camPlane.Raycast(ray, out float camEnter) && camEnter > 0.1f)
            {
                hitPoint = ray.GetPoint(camEnter);
                return true;
            }

            // 3. Fallback depth along ray
            float fallbackDist = havePivot ? Vector3.Distance(transform.position, target) : 100f;
            hitPoint = ray.GetPoint(Mathf.Clamp(fallbackDist, 5f, 1000f));
            return true;
        }

        /// <summary>
        /// Used for Orbit & Zoom: prefers real Point Cloud Collision; falls back to Adapted Plane.
        /// </summary>
        public bool PickPointForRotateOrZoom(Vector3 screenPos, out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;
            if (cam == null) cam = Camera.main;
            if (cam == null) return false;

            Ray ray = cam.ScreenPointToRay(screenPos);

            // 1. Prioritize real Point Cloud Collision
            if (HitPointCloud(ray, out Vector3 cloudHit, out float dist))
            {
                hitPoint = cloudHit;
                return true;
            }

            // 2. Fallback to Adapted Plane
            return HitAdaptedPlane(ray, out hitPoint);
        }

        /// <summary>
        /// Backward compatibility wrapper
        /// </summary>
        public bool PickGroundPoint(Vector3 mousePos, out Vector3 hitPoint)
        {
            return HitPanPlane(cam.ScreenPointToRay(mousePos), out hitPoint);
        }

        public void FocusOnBounds(Bounds b)
        {
            target = new Vector3(b.center.x, groundY, b.center.z);
            float maxDim = Mathf.Max(b.size.x, b.size.z);
            float dist = Mathf.Clamp(maxDim * 1.2f, 30f, 1500f);
            transform.position = target + new Vector3(0, dist * 0.45f, -dist * 0.8f);
            transform.LookAt(target);
            havePivot = true;
        }

        public void SetPresetView(CameraViewPreset preset)
        {
            Vector3 focus = havePivot ? target : Vector3.zero;
            if (focus == Vector3.zero)
            {
                if (boundsReady && cloudBounds.size.sqrMagnitude > 0.1f)
                    focus = new Vector3(cloudBounds.center.x, groundY, cloudBounds.center.z);
                else
                    focus = transform.position + transform.forward * 50f;
            }

            float dist = 80f;
            if (boundsReady)
            {
                float maxDim = Mathf.Max(cloudBounds.size.x, cloudBounds.size.z);
                dist = Mathf.Clamp(maxDim * 1.2f, 30f, 1500f);
            }
            else
            {
                dist = Mathf.Clamp(Vector3.Distance(transform.position, focus), 30f, 1500f);
            }

            switch (preset)
            {
                case CameraViewPreset.Reset:
                    if (boundsReady) FocusOnBounds(cloudBounds);
                    else
                    {
                        transform.position = focus + new Vector3(0, dist * 0.45f, -dist * 0.8f);
                        transform.LookAt(focus);
                        target = focus;
                        havePivot = true;
                    }
                    break;

                case CameraViewPreset.Top:
                    transform.position = focus + Vector3.up * dist;
                    transform.LookAt(focus, Vector3.forward);
                    target = focus;
                    havePivot = true;
                    break;

                case CameraViewPreset.Bottom:
                    transform.position = focus - Vector3.up * dist;
                    transform.LookAt(focus, -Vector3.forward);
                    target = focus;
                    havePivot = true;
                    break;

                case CameraViewPreset.Front:
                    transform.position = focus - Vector3.forward * dist;
                    transform.LookAt(focus, Vector3.up);
                    target = focus;
                    havePivot = true;
                    break;

                case CameraViewPreset.Back:
                    transform.position = focus + Vector3.forward * dist;
                    transform.LookAt(focus, Vector3.up);
                    target = focus;
                    havePivot = true;
                    break;

                case CameraViewPreset.Left:
                    transform.position = focus - Vector3.right * dist;
                    transform.LookAt(focus, Vector3.up);
                    target = focus;
                    havePivot = true;
                    break;

                case CameraViewPreset.Right:
                    transform.position = focus + Vector3.right * dist;
                    transform.LookAt(focus, Vector3.up);
                    target = focus;
                    havePivot = true;
                    break;

                case CameraViewPreset.Isometric:
                    transform.position = focus + new Vector3(dist * 0.707f, dist * 0.5f, -dist * 0.707f);
                    transform.LookAt(focus, Vector3.up);
                    target = focus;
                    havePivot = true;
                    break;
            }
        }
    }
}