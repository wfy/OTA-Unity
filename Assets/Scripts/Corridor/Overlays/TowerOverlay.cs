using System.Collections.Generic;
using UnityEngine;

namespace OTA.Corridor.Overlays
{
    public class TowerOverlay : MonoBehaviour
    {
        public string towerId = "#1";
        public string towerName = "220kV 铁塔";
        public float towerHeight = 32f;
        public float baseWidth = 8f;
        public float crossarmSpan = 14f;
        public Color towerColor = new Color(1.0f, 0.78f, 0.0f, 0.9f);
        public bool showBillboard = true;

        public Vector3 BasePosition => transform.position;
        public Vector3 TopPosition => transform.position + Vector3.up * towerHeight;
        public Vector3 LeftAttachment => TopPosition + transform.right * (-crossarmSpan * 0.5f) - Vector3.up * 2f;
        public Vector3 RightAttachment => TopPosition + transform.right * (crossarmSpan * 0.5f) - Vector3.up * 2f;
        public Vector3 MiddleAttachment => TopPosition - Vector3.up * 1f;

        private LineRenderer lineRenderer;

        void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }
            SetupRenderer();
        }

        void Start()
        {
            RebuildGeometry();
        }

        private void SetupRenderer()
        {
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = 0.35f;
            lineRenderer.endWidth = 0.35f;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = towerColor;
            lineRenderer.endColor = towerColor;
        }

        public void RebuildGeometry()
        {
            if (lineRenderer == null) return;

            Vector3 b = BasePosition;
            Vector3 top = TopPosition;
            Vector3 l = LeftAttachment;
            Vector3 r = RightAttachment;

            var pts = new Vector3[]
            {
                b,
                top,
                l,
                r,
                top
            };
            lineRenderer.positionCount = pts.Length;
            lineRenderer.SetPositions(pts);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = towerColor;
            Gizmos.DrawWireCube(BasePosition + Vector3.up * (towerHeight * 0.5f), new Vector3(baseWidth, towerHeight, baseWidth));
            Gizmos.DrawLine(BasePosition, TopPosition);
            Gizmos.DrawLine(LeftAttachment, RightAttachment);
        }
    }
}
