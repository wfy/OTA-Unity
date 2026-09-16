﻿using System;
using System.Collections.Generic;
using PointCloudRuntimeViewer;
using UnityEngine;

namespace OTA.Corridor.Annotation
{
    /// <summary>
    /// Manages conductor marking:
    /// 1. Shift+Click Point 1: Hanging point on Tower A (renders 3D anchor sphere + rubber band).
    /// 2. Shift+Click Point 2: Hanging point on Tower B.
    /// 3. SagFinder automatically searches catenary and renders strictly vertical sag drop line.
    /// 4. Integrated with AnnotationHistory (Ctrl+Z / Ctrl+Y).
    /// </summary>
    public class ConductorAnnotator : MonoBehaviour
    {
        public static ConductorAnnotator Instance { get; private set; }

        public List<ConductorAnnotation> conductors = new List<ConductorAnnotation>();
        public ConductorAnnotation activeConductor = null;

        public Color wireGlowColor = new Color(0.9f, 0.4f, 1f, 0.95f);
        public Color activeWireColor = new Color(1f, 0.95f, 0.2f, 1f);
        public Color anchorColor = new Color(0.2f, 1f, 0.4f, 0.95f);

        private Vector3? pendingStartPoint = null;
        public bool HasPendingPoint => pendingStartPoint.HasValue;

        private RuntimeViewerDX11 viewer;
        private Material lineMat;
        private bool isSubscribed = false;

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

        private void EnsureMaterial()
        {
            if (lineMat == null)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (!shader) shader = Shader.Find("Sprites/Default");
                lineMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                lineMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                lineMat.SetInt("_ZWrite", 0);
                lineMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            }
        }

        private void HandlePointClicked(Vector3 hitPos)
        {
            if (AnnotationController.Instance == null ||
                AnnotationController.Instance.currentMode != AnnotationTargetMode.Conductor)
            {
                return;
            }

            if (pendingStartPoint == null)
            {
                pendingStartPoint = hitPos;
                AnnotationHistory.ShowToast("挂点 1 已设置，按住 Shift 点击对侧挂点 2");
                Debug.Log($"[ConductorAnnotator] Set Start Hanging Point: {hitPos}. Now click End Hanging Point...");
            }
            else
            {
                Vector3 startPos = pendingStartPoint.Value;
                pendingStartPoint = null;

                Vector3[] pts = viewer != null ? viewer.points : null;
                var colorMgr = FindObjectOfType<CorridorColorManager>();
                byte[] classes = colorMgr != null ? colorMgr.PointClasses : null;

                string span = "#17-#18";
                string phase = "导线-" + (conductors.Count + 1);

                var cond = SagFinder.AnalyzeConductor(span, phase, startPos, hitPos, pts, classes);
                conductors.Add(cond);
                activeConductor = cond;

                AnnotationHistory.Instance?.RecordAction(new ConductorAddAction(this, cond));
                AnnotationHistory.ShowToast($"导线 {cond.phaseName} 拟合完成: 弧垂={cond.measuredSag:F2}m, 外径={cond.conductorModel} ({cond.inlierPointCount}点)");

                Debug.Log($"[ConductorAnnotator] Built Conductor {cond.phaseName} for {cond.spanId}: Span={cond.horizontalSpan:F1}m, Sag={cond.measuredSag:F2}m, LowestPoint={cond.midLowestPoint}");
            }
        }

        public ConductorAnnotation PickConductor(Ray ray, float pickRadius = 1.5f)
        {
            ConductorAnnotation best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < conductors.Count; i++)
            {
                if (conductors[i].isVisible && conductors[i].Raycast(ray, pickRadius, out float d) && d < bestDist)
                {
                    bestDist = d;
                    best = conductors[i];
                }
            }
            return best;
        }

        public int ColorizeConductorPoints(ConductorAnnotation cond, byte targetClass = 14)
        {
            if (cond == null || cond.curvePoints == null || cond.curvePoints.Length < 2) return 0;
            if (viewer == null) viewer = FindObjectOfType<RuntimeViewerDX11>();
            if (viewer == null || viewer.points == null) return 0;

            var colorMgr = FindObjectOfType<CorridorColorManager>();
            if (colorMgr == null) return 0;

            var pts = viewer.points;
            byte[] currentClasses = colorMgr.PointClasses;
            List<int> affectedIndices = new List<int>();
            List<byte> oldClasses = new List<byte>();

            float maxRadius = 1.5f;
            float maxR2 = maxRadius * maxRadius;

            // Bounding box pre-cull for performance
            Bounds curveBounds = new Bounds(cond.curvePoints[0], Vector3.zero);
            for (int j = 1; j < cond.curvePoints.Length; j++) curveBounds.Encapsulate(cond.curvePoints[j]);
            curveBounds.Expand(maxRadius * 2f);

            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 p = pts[i];
                if (!curveBounds.Contains(p)) continue;

                // Test distance to catenary segments
                bool nearCurve = false;
                for (int j = 0; j < cond.curvePoints.Length - 1; j++)
                {
                    Vector3 a = cond.curvePoints[j];
                    Vector3 b = cond.curvePoints[j + 1];
                    Vector3 ab = b - a;
                    float abLen2 = ab.sqrMagnitude;
                    if (abLen2 < 0.0001f) continue;

                    float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / abLen2);
                    Vector3 proj = a + ab * t;
                    if ((p - proj).sqrMagnitude <= maxR2)
                    {
                        nearCurve = true;
                        break;
                    }
                }

                if (nearCurve)
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
                    $"导线 {cond.phaseName} 赋类为 Class {targetClass} ({affectedIndices.Count}点)"
                );
                AnnotationHistory.ShowToast($"已将导线 {cond.phaseName} 沿线 {affectedIndices.Count} 点云赋类为 Class {targetClass}");
            }
            else
            {
                AnnotationHistory.ShowToast($"导线 {cond.phaseName} 缓冲区内未检索到点云数据");
            }

            return affectedIndices.Count;
        }

        public void CancelPending()
        {
            pendingStartPoint = null;
        }

        void OnRenderObject()
        {
            EnsureMaterial();
            lineMat.SetPass(0);

            GL.PushMatrix();
            GL.Begin(GL.LINES);

            // 1. Pending Hanging Point 1: 3D Anchor Marker + Dynamic Rubber Band Line
            if (pendingStartPoint.HasValue)
            {
                Vector3 p1 = pendingStartPoint.Value;
                DrawAnchorSphere(p1, 0.6f, anchorColor);

                if (AnnotationController.Instance != null && AnnotationController.Instance.HasValidHit)
                {
                    GL.Color(new Color(1f, 0.95f, 0.2f, 0.95f));
                    GL.Vertex(p1);
                    GL.Vertex(AnnotationController.Instance.CurrentHitPoint);
                }
            }

            // 2. Completed Catenaries and Endpoints
            for (int i = 0; i < conductors.Count; i++)
            {
                var cond = conductors[i];
                if (!cond.isVisible) continue;
                if (cond.curvePoints == null || cond.curvePoints.Length < 2) continue;

                Color col = (cond == activeConductor) ? activeWireColor : wireGlowColor;
                GL.Color(col);

                // Hanging terminal markers
                DrawAnchorSphere(cond.startHangingPoint, 0.45f, col);
                DrawAnchorSphere(cond.endHangingPoint, 0.45f, col);

                // Catenary curve
                for (int j = 0; j < cond.curvePoints.Length - 1; j++)
                {
                    GL.Vertex(cond.curvePoints[j]);
                    GL.Vertex(cond.curvePoints[j + 1]);
                }

                // 3. Strictly Vertical Red Sag Drop Line (Delta X = 0, Delta Z = 0)
                Vector3 midP = cond.midLowestPoint;
                float chordY = SagFinder.GetChordYAt(cond.startHangingPoint, cond.endHangingPoint, midP.x, midP.z);
                Vector3 chordPt = new Vector3(midP.x, chordY, midP.z);

                GL.Color(new Color(1f, 0.2f, 0.2f, 0.95f));
                GL.Vertex(chordPt);
                GL.Vertex(midP);

                // Bottom tick mark at lowest point
                float tick = 0.5f;
                GL.Vertex(midP - Vector3.right * tick);
                GL.Vertex(midP + Vector3.right * tick);
                GL.Vertex(midP - Vector3.forward * tick);
                GL.Vertex(midP + Vector3.forward * tick);
            }

            GL.End();
            GL.PopMatrix();
        }

        private void DrawAnchorSphere(Vector3 center, float radius, Color col)
        {
            GL.Color(col);
            int segs = 16;
            // Circle XY
            Vector3 prev = center + Vector3.right * radius;
            for (int i = 1; i <= segs; i++)
            {
                float a = (float)i / segs * Mathf.PI * 2f;
                Vector3 cur = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                GL.Vertex(prev); GL.Vertex(cur);
                prev = cur;
            }
            // Circle XZ
            prev = center + Vector3.right * radius;
            for (int i = 1; i <= segs; i++)
            {
                float a = (float)i / segs * Mathf.PI * 2f;
                Vector3 cur = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                GL.Vertex(prev); GL.Vertex(cur);
                prev = cur;
            }
            // Axis ticks
            float crossR = radius * 1.4f;
            GL.Vertex(center - Vector3.right * crossR); GL.Vertex(center + Vector3.right * crossR);
            GL.Vertex(center - Vector3.up * crossR); GL.Vertex(center + Vector3.up * crossR);
            GL.Vertex(center - Vector3.forward * crossR); GL.Vertex(center + Vector3.forward * crossR);
        }
    }
}
