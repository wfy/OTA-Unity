using System;
using System.Collections.Generic;
using UnityEngine;

namespace OTA.Corridor.Annotation
{
    /// <summary>
    /// Optional / on-demand annotator for insulator strings:
    /// Click 1: Tower crossarm hanging point.
    /// Click 2: Conductor clamp connection point.
    /// </summary>
    public class InsulatorAnnotator : MonoBehaviour
    {
        public static InsulatorAnnotator Instance { get; private set; }

        public List<InsulatorAnnotation> insulators = new List<InsulatorAnnotation>();
        public InsulatorAnnotation activeInsulator = null;

        public Color insulatorColor = new Color(0.2f, 0.9f, 1f, 0.9f);
        public Color activeColor = new Color(1f, 0.85f, 0.2f, 1f);

        private Vector3? pendingPoint = null;
        private Material lineMat;
        private bool isSubscribed = false;

        void Awake()
        {
            Instance = this;
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
            }
        }

        private void HandlePointClicked(Vector3 hitPos)
        {
            if (AnnotationController.Instance == null ||
                AnnotationController.Instance.currentMode != AnnotationTargetMode.Insulator)
            {
                return;
            }

            if (pendingPoint == null)
            {
                pendingPoint = hitPos;
                Debug.Log($"[InsulatorAnnotator] Crossarm Point Set: {hitPos}. Now click Conductor Clamp Point...");
            }
            else
            {
                Vector3 p1 = pendingPoint.Value;
                pendingPoint = null;

                string tower = "#17";
                if (TowerAnnotator.Instance != null && TowerAnnotator.Instance.activeTower != null)
                {
                    tower = TowerAnnotator.Instance.activeTower.towerNo;
                }
                string phase = "绝缘子-" + (insulators.Count + 1);

                var ins = new InsulatorAnnotation(tower, phase, p1, hitPos);
                insulators.Add(ins);
                activeInsulator = ins;

                Debug.Log($"[InsulatorAnnotator] Created Insulator {ins.phaseName} on {ins.towerNo}: Length={ins.stringLength:F2}m, Angle={ins.swingAngle:F1}°");
            }
        }

        public void CancelPending()
        {
            pendingPoint = null;
        }

        void OnRenderObject()
        {
            EnsureMaterial();
            lineMat.SetPass(0);

            GL.PushMatrix();
            GL.Begin(GL.LINES);

            // 1. Pending line
            if (pendingPoint.HasValue && AnnotationController.Instance != null && AnnotationController.Instance.HasValidHit)
            {
                GL.Color(new Color(0.2f, 1f, 1f, 0.7f));
                GL.Vertex(pendingPoint.Value);
                GL.Vertex(AnnotationController.Instance.CurrentHitPoint);
            }

            // 2. Completed insulators
            for (int i = 0; i < insulators.Count; i++)
            {
                var ins = insulators[i];
                Color col = (ins == activeInsulator) ? activeColor : insulatorColor;
                GL.Color(col);

                // Draw central axis
                GL.Vertex(ins.crossarmEnd);
                GL.Vertex(ins.conductorEnd);

                // Cross tick marks
                Vector3 dir = (ins.conductorEnd - ins.crossarmEnd).normalized;
                Vector3 perp = Vector3.Cross(dir, Vector3.forward).normalized * 0.35f;
                int discs = Mathf.Clamp((int)(ins.stringLength / 0.15f), 3, 20);
                for (int d = 1; d < discs; d++)
                {
                    float t = (float)d / discs;
                    Vector3 center = Vector3.Lerp(ins.crossarmEnd, ins.conductorEnd, t);
                    GL.Vertex(center - perp);
                    GL.Vertex(center + perp);
                }
            }

            GL.End();
            GL.PopMatrix();
        }
    }
}
