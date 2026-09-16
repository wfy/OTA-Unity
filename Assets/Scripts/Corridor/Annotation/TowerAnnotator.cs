using System;
using System.Collections.Generic;
using Metervara.Interaction;
using PointCloudRuntimeViewer;
using UnityEngine;

namespace OTA.Corridor.Annotation
{
    /// <summary>
    /// Dual Bounding Box Tower Annotator:
    /// 1. Shift+Click on Tower Top center creates tower with beacon & dual boxes.
    /// 2. Shift+Wheel: rotates yaw angle; when wheel stops, camera jumps to front elevation view.
    /// 3. Shift+RightDrag: horizontal drags head width, vertical drags nominal height.
    /// 4. Integrated with AnnotationHistory (Ctrl+Z / Ctrl+Y).
    /// </summary>
    public class TowerAnnotator : MonoBehaviour
    {
        public static TowerAnnotator Instance { get; private set; }

        public List<TowerAnnotation> towers = new List<TowerAnnotation>();
        public TowerAnnotation activeTower = null;

        public Color headBoxColor = new Color(1f, 0.6f, 0.1f, 0.95f);
        public Color bodyBoxColor = new Color(0.2f, 0.8f, 1f, 0.95f);
        public Color activeAccentColor = new Color(1f, 0.9f, 0.2f, 1f);

        private RuntimeViewerDX11 viewer;
        private bool isSubscribed = false;

        // Shift + Wheel tracking
        private bool isWheelActive = false;
        private float wheelStopTime = 0f;

        // Shift + RightDrag tracking
        private bool isRightDragging = false;
        private Vector3 prevRightMousePos;
        private float initialYaw;
        private float initialHeadW;
        private float initialNominalH;

        void Awake()
        {
            Instance = this;
            viewer = FindObjectOfType<RuntimeViewerDX11>();
        }

        void OnEnable()
        {
            EnsureSubscribed();
        }

        void Start()
        {
            EnsureSubscribed();
        }

        void Update()
        {
            if (!isSubscribed) EnsureSubscribed();

            bool isAnnotating = AnnotationController.IsAnnotating &&
                                AnnotationController.Instance.currentMode == AnnotationTargetMode.Tower;

            if (isAnnotating && activeTower != null && !AnnotationController.IsPointerOverUI(Input.mousePosition))
            {
                // 1. Shift + Wheel: Adjust Yaw
                float wheel = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(wheel) > 0.001f)
                {
                    AdjustActiveYaw(wheel * 25f);
                    AnnotationHistory.ShowToast($"杆塔朝向偏角: {activeTower.yawAngle:F1}°");
                }

                // 2. Shift + RightDrag: Adjust Head Width (horizontal) & Nominal Height (vertical)
                if (Input.GetMouseButtonDown(1))
                {
                    isRightDragging = true;
                    prevRightMousePos = Input.mousePosition;
                    initialYaw = activeTower.yawAngle;
                    initialHeadW = activeTower.topBox.size.x;
                    initialNominalH = activeTower.nominalHeight;
                }
                else if (Input.GetMouseButton(1) && isRightDragging)
                {
                    Vector3 delta = Input.mousePosition - prevRightMousePos;
                    prevRightMousePos = Input.mousePosition;

                    float deltaHeadW = delta.x * 0.12f;
                    float deltaNomH = delta.y * 0.12f;

                    AdjustActiveDimensions(deltaHeadW, deltaNomH);
                    AnnotationHistory.ShowToast($"横担宽度: {activeTower.topBox.size.x:F1}m | 最下方横担呼高: {activeTower.nominalHeight:F1}m");
                }
                else if (Input.GetMouseButtonUp(1) && isRightDragging)
                {
                    isRightDragging = false;
                    if (Mathf.Abs(activeTower.topBox.size.x - initialHeadW) > 0.01f ||
                        Mathf.Abs(activeTower.nominalHeight - initialNominalH) > 0.01f)
                    {
                        AnnotationHistory.Instance?.RecordAction(new TowerModifyAction(
                            activeTower, initialYaw, activeTower.yawAngle,
                            initialHeadW, activeTower.topBox.size.x,
                            initialNominalH, activeTower.nominalHeight));
                    }
                }
            }
            else
            {
                isRightDragging = false;
            }
        }

        void OnDisable()
        {
            if (isSubscribed && AnnotationController.Instance != null)
            {
                AnnotationController.Instance.OnPointClicked -= HandlePointClicked;
            }
            isSubscribed = false;
        }

        private void EnsureSubscribed()
        {
            if (!isSubscribed && AnnotationController.Instance != null)
            {
                AnnotationController.Instance.OnPointClicked += HandlePointClicked;
                isSubscribed = true;
            }
        }

        private void HandlePointClicked(Vector3 hitPos)
        {
            if (AnnotationController.Instance == null ||
                AnnotationController.Instance.currentMode != AnnotationTargetMode.Tower)
            {
                return;
            }

            CreateTowerAt(hitPos);
        }

        public TowerAnnotation CreateTowerAt(Vector3 topPos)
        {
            float baseZ = EstimateGroundElevation(topPos);
            Vector3 basePos = new Vector3(topPos.x, baseZ, topPos.z);

            string nextNo = "#" + (towers.Count + 17);
            var tower = new TowerAnnotation(nextNo, topPos, basePos, 0f);
            towers.Add(tower);
            activeTower = tower;

            AnnotationHistory.Instance?.RecordAction(new TowerAddAction(this, tower));
            AnnotationHistory.ShowToast($"已创建杆塔 {tower.towerNo}，塔高={tower.totalHeight:F1}m，呼高={tower.nominalHeight:F1}m");

            Debug.Log($"[TowerAnnotator] Created Tower {tower.towerNo} at {topPos}, TotalHeight={tower.totalHeight:F1}m, NominalHeight={tower.nominalHeight:F1}m");
            return tower;
        }

        private float EstimateGroundElevation(Vector3 topPos)
        {
            if (viewer != null && viewer.points != null && viewer.points.Length > 0)
            {
                var pts = viewer.points;
                float radius = 2.0f; // 塔基物理半径 2m
                float minDist2 = radius * radius;

                // 收集 2m 物理半径内的候选高程点
                var nearbyY = new List<float>();
                var groundClassY = new List<float>();
                var colorMgr = CorridorColorManager.Instance;
                byte[] classes = colorMgr != null ? colorMgr.PointClasses : null;

                // 2m 区域面积精细，提高采样密度
                int stride = Mathf.Max(1, pts.Length / 500000);
                for (int i = 0; i < pts.Length; i += stride)
                {
                    float dx = pts[i].x - topPos.x;
                    float dz = pts[i].z - topPos.z;
                    if (dx * dx + dz * dz <= minDist2)
                    {
                        float py = pts[i].y;
                        if (py < topPos.y - 3f) // 排除塔顶自身结构点
                        {
                            nearbyY.Add(py);
                            if (classes != null && i < classes.Length && classes[i] == 2) // Class 2: 地面 (Ground)
                            {
                                groundClassY.Add(py);
                            }
                        }
                    }
                }

                // 1. 若 2m 内存在已分类的地面点 (Class 2)，以地面点中位数作为基准
                if (groundClassY.Count > 0)
                {
                    groundClassY.Sort();
                    return groundClassY[groundClassY.Count / 2];
                }

                // 2. 若无分类点但存在候选点，取第 10% 分位点剔除潜在下穿噪点
                if (nearbyY.Count >= 3)
                {
                    nearbyY.Sort();
                    int qIdx = Mathf.Clamp(nearbyY.Count / 10, 0, nearbyY.Count - 1);
                    return nearbyY[qIdx];
                }
                else if (nearbyY.Count > 0)
                {
                    nearbyY.Sort();
                    return nearbyY[0];
                }

                // 3. 若 2m 内点密度极低未命中，平滑回退至 4m 局部范围
                float fallbackDist2 = 4.0f * 4.0f;
                float lowestFallback = float.MaxValue;
                for (int i = 0; i < pts.Length; i += stride)
                {
                    float dx = pts[i].x - topPos.x;
                    float dz = pts[i].z - topPos.z;
                    if (dx * dx + dz * dz <= fallbackDist2)
                    {
                        if (pts[i].y < lowestFallback && pts[i].y < topPos.y - 3f)
                        {
                            lowestFallback = pts[i].y;
                        }
                    }
                }
                if (lowestFallback < topPos.y - 3f && lowestFallback != float.MaxValue)
                {
                    return lowestFallback;
                }
            }
            return topPos.y - 35f;
        }

        public void AdjustActiveYaw(float deltaDeg)
        {
            if (activeTower == null) return;
            activeTower.yawAngle = (activeTower.yawAngle + deltaDeg) % 360f;
        }

        public void AdjustActiveDimensions(float deltaHeadWidth, float deltaNominalHeight)
        {
            if (activeTower == null) return;
            activeTower.nominalHeight = Mathf.Clamp(activeTower.nominalHeight + deltaNominalHeight, 5f, activeTower.totalHeight - 2f);
            float curHeadWidth = activeTower.topBox.size.x + deltaHeadWidth;
            activeTower.RecalculateBoxes(headWidth: Mathf.Clamp(curHeadWidth, 4f, 60f));
        }

        public TowerAnnotation PickTower(Ray ray)
        {
            TowerAnnotation best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < towers.Count; i++)
            {
                if (towers[i].isVisible && towers[i].Raycast(ray, out float d) && d < bestDist)
                {
                    bestDist = d;
                    best = towers[i];
                }
            }
            return best;
        }

        public int ColorizeTowerPoints(TowerAnnotation tower, byte targetClass = 15)
        {
            if (tower == null) return 0;
            if (viewer == null) viewer = FindObjectOfType<RuntimeViewerDX11>();
            if (viewer == null || viewer.points == null) return 0;

            var colorMgr = FindObjectOfType<CorridorColorManager>();
            if (colorMgr == null) return 0;

            var pts = viewer.points;
            byte[] currentClasses = colorMgr.PointClasses;
            List<int> affectedIndices = new List<int>();
            List<byte> oldClasses = new List<byte>();

            for (int i = 0; i < pts.Length; i++)
            {
                if (tower.ContainsPoint(pts[i]))
                {
                    affectedIndices.Add(i);
                    oldClasses.Add(currentClasses != null && i < currentClasses.Length ? currentClasses[i] : (byte)1);
                }
            }

            if (affectedIndices.Count > 0)
            {
                int[] indicesArr = affectedIndices.ToArray();
                colorMgr.UpdatePointsClass(indicesArr, targetClass);

                AnnotationHistory.Instance?.RecordTransaction(
                    indicesArr,
                    oldClasses.ToArray(),
                    targetClass,
                    $"杆塔 {tower.towerNo} 赋类为 Class {targetClass} ({affectedIndices.Count}点)"
                );
                AnnotationHistory.ShowToast($"已将杆塔 {tower.towerNo} 内部 {affectedIndices.Count} 点云赋类为 Class {targetClass}");
            }
            else
            {
                AnnotationHistory.ShowToast($"杆塔 {tower.towerNo} 包围盒内未检索到点云数据");
            }

            return affectedIndices.Count;
        }

        void OnRenderObject()
        {
            for (int i = 0; i < towers.Count; i++)
            {
                var t = towers[i];
                if (!t.isVisible) continue;

                // 杆塔着色后包围盒自动消失，仅在编辑状态（当前选中活动杆塔）才显示包围盒
                if (t != activeTower) continue;

                Color headCol = activeAccentColor;
                Color bodyCol = activeAccentColor;

                // 1. Beacon at apex
                WireframeBoxDrawer.DrawBeacon(t.topPosition, t.totalHeight, headCol);

                // 2. Solid semi-transparent face boxes
                Color headFace = new Color(headCol.r, headCol.g, headCol.b, 0.22f);
                Color bodyFace = new Color(bodyCol.r, bodyCol.g, bodyCol.b, 0.18f);
                WireframeBoxDrawer.DrawSolidRotatedBox(t.topBox, t.yawAngle, t.topPosition, headFace);
                WireframeBoxDrawer.DrawSolidRotatedBox(t.bodyBox, t.yawAngle, t.topPosition, bodyFace);

                // 3. Bold wireframes with diagonal braces
                WireframeBoxDrawer.DrawRotatedBox(t.topBox, t.yawAngle, t.topPosition, headCol);
                WireframeBoxDrawer.DrawRotatedBox(t.bodyBox, t.yawAngle, t.topPosition, bodyCol);
            }
        }
    }
}
