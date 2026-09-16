using System;
using UnityEngine;

namespace OTA.Corridor.Annotation
{
    /// <summary>
    /// 3D Space Measurement Controller:
    /// Supports Shift+Click Point A and Point B to measure 3D spatial distance,
    /// horizontal 2D distance, and vertical elevation difference.
    /// Draws spatial connection lines and endpoints in 3D viewport.
    /// </summary>
    public class MeasurementController : MonoBehaviour
    {
        public static MeasurementController Instance { get; private set; }

        public Vector3? pointA = null;
        public Vector3? pointB = null;
        public float dist3D = 0f;
        public float dist2D = 0f;
        public float deltaY = 0f;
        public bool hasResult = false;

        private Material lineMat;
        private bool isSubscribed = false;

        void Awake()
        {
            Instance = this;
            EnsureMaterial();
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
            if (AnnotationController.Instance == null) return;
            if (AnnotationController.Instance.currentMode != AnnotationTargetMode.Measure) return;

            if (pointA == null)
            {
                pointA = hitPos;
                pointB = null;
                hasResult = false;
            }
            else
            {
                pointB = hitPos;
                dist3D = Vector3.Distance(pointA.Value, pointB.Value);
                dist2D = Vector2.Distance(new Vector2(pointA.Value.x, pointA.Value.z), new Vector2(pointB.Value.x, pointB.Value.z));
                deltaY = Mathf.Abs(pointA.Value.y - pointB.Value.y);
                hasResult = true;
            }
        }

        public void ClearMeasurement()
        {
            pointA = null;
            pointB = null;
            hasResult = false;
            dist3D = 0f;
            dist2D = 0f;
            deltaY = 0f;
        }

        void OnRenderObject()
        {
            if (pointA == null) return;
            EnsureMaterial();
            lineMat.SetPass(0);

            GL.PushMatrix();
            GL.Begin(GL.LINES);

            // Draw point A marker (crosshair cube)
            DrawMarker(pointA.Value, new Color(0.9f, 0.3f, 1f, 0.95f));

            if (pointB != null)
            {
                // Draw point B marker
                DrawMarker(pointB.Value, new Color(0.9f, 0.3f, 1f, 0.95f));

                // 3D Spatial Distance Line (Purple Neon)
                GL.Color(new Color(0.85f, 0.4f, 1f, 0.95f));
                GL.Vertex(pointA.Value);
                GL.Vertex(pointB.Value);

                // Horizontal component line (Cyan)
                Vector3 midPoint = new Vector3(pointB.Value.x, pointA.Value.y, pointB.Value.z);
                GL.Color(new Color(0.2f, 0.8f, 1f, 0.6f));
                GL.Vertex(pointA.Value);
                GL.Vertex(midPoint);

                // Vertical component line (Amber)
                GL.Color(new Color(1f, 0.75f, 0.2f, 0.8f));
                GL.Vertex(midPoint);
                GL.Vertex(pointB.Value);
            }
            else if (AnnotationController.Instance != null && AnnotationController.Instance.currentMode == AnnotationTargetMode.Measure && AnnotationController.Instance.HasValidHit)
            {
                // Rubber band line to current hit position
                GL.Color(new Color(0.7f, 0.3f, 1f, 0.6f));
                GL.Vertex(pointA.Value);
                GL.Vertex(AnnotationController.Instance.CurrentHitPoint);
            }

            GL.End();
            GL.PopMatrix();
        }

        private void DrawMarker(Vector3 pos, Color col)
        {
            GL.Color(col);
            float s = 0.8f;
            GL.Vertex(pos + new Vector3(-s, 0, 0)); GL.Vertex(pos + new Vector3(s, 0, 0));
            GL.Vertex(pos + new Vector3(0, -s, 0)); GL.Vertex(pos + new Vector3(0, s, 0));
            GL.Vertex(pos + new Vector3(0, 0, -s)); GL.Vertex(pos + new Vector3(0, 0, s));
        }
    }
}
