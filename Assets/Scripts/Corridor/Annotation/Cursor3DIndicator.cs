using System;
using UnityEngine;

namespace OTA.Corridor.Annotation
{
    /// <summary>
    /// Visual 3D target indicator rendered directly via GL.LINES in OnRenderObject().
    /// High visibility, ZTest Always (never occluded by point cloud), zero GC allocation.
    /// Provides WYSIWYG feedback when holding Shift for annotation.
    /// </summary>
    public class Cursor3DIndicator : MonoBehaviour
    {
        [Header("Indicator Settings")]
        public float baseRadius = 0.5f;
        public Color activeColor = new Color(0.2f, 1f, 0.4f, 0.95f); // Neon fluorescent green
        public Color clickColor = new Color(1f, 0.85f, 0.1f, 1f);   // Amber click flash

        private Material lineMaterial;
        private Camera targetCamera;

        private bool isVisible = false;
        private Vector3 currentPosition = Vector3.zero;
        private float clickFlashTimer = 0f;

        void Awake()
        {
            EnsureCamera();
            EnsureMaterial();
        }

        private void EnsureCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
                if (targetCamera == null) targetCamera = CameraHelper.MainCamera;
            }
        }

        private void EnsureMaterial()
        {
            if (lineMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (!shader) shader = Shader.Find("Sprites/Default");
                lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                lineMaterial.SetInt("_ZWrite", 0);
                lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            }
        }

        public void SetVisible(bool visible)
        {
            isVisible = visible;
        }

        public void FlashClick()
        {
            clickFlashTimer = 0.25f;
        }

        public void UpdatePosition(Vector3 worldPos, Vector3 hitNormal)
        {
            currentPosition = worldPos;
        }

        void Update()
        {
            if (clickFlashTimer > 0f)
            {
                clickFlashTimer -= Time.unscaledDeltaTime;
            }
        }

        void OnRenderObject()
        {
            if (!isVisible) return;
            EnsureCamera();
            if (targetCamera == null) return;
            if (Camera.current != null && Camera.current != targetCamera) return;

            EnsureMaterial();
            lineMaterial.SetPass(0);

            GL.PushMatrix();
            GL.Begin(GL.LINES);

            Color col = clickFlashTimer > 0f ? clickColor : activeColor;
            GL.Color(col);

            float dist = Vector3.Distance(targetCamera.transform.position, currentPosition);
            float scale = Mathf.Clamp(dist * 0.025f, 0.15f, 25f) * baseRadius;

            Vector3 right = targetCamera.transform.right;
            Vector3 up = targetCamera.transform.up;

            // 1. Draw Reticle Ring (32 segments)
            const int segs = 32;
            Vector3 prevPt = currentPosition + right * scale;
            for (int i = 1; i <= segs; i++)
            {
                float angle = (float)i / segs * Mathf.PI * 2f;
                Vector3 currPt = currentPosition + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * scale;
                GL.Vertex(prevPt);
                GL.Vertex(currPt);
                prevPt = currPt;
            }

            // 2. Draw Crosshairs (4 arms)
            float inner = scale * 0.25f;
            float outer = scale * 1.35f;

            // Left
            GL.Vertex(currentPosition - right * inner);
            GL.Vertex(currentPosition - right * outer);
            // Right
            GL.Vertex(currentPosition + right * inner);
            GL.Vertex(currentPosition + right * outer);
            // Up
            GL.Vertex(currentPosition + up * inner);
            GL.Vertex(currentPosition + up * outer);
            // Down
            GL.Vertex(currentPosition - up * inner);
            GL.Vertex(currentPosition - up * outer);

            // 3. Center Target Dot (Diamond)
            float dotR = scale * 0.1f;
            GL.Vertex(currentPosition + right * dotR);
            GL.Vertex(currentPosition + up * dotR);

            GL.Vertex(currentPosition + up * dotR);
            GL.Vertex(currentPosition - right * dotR);

            GL.Vertex(currentPosition - right * dotR);
            GL.Vertex(currentPosition - up * dotR);

            GL.Vertex(currentPosition - up * dotR);
            GL.Vertex(currentPosition + right * dotR);

            GL.End();
            GL.PopMatrix();
        }

        void OnDestroy()
        {
            if (lineMaterial != null)
            {
                Destroy(lineMaterial);
            }
        }
    }
}
