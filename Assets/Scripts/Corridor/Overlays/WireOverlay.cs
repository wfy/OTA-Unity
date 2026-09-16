using System.Collections.Generic;
using UnityEngine;

namespace OTA.Corridor.Overlays
{
    [RequireComponent(typeof(LineRenderer))]
    public class WireOverlay : MonoBehaviour
    {
        public string spanName = "导线 #17-#18 A相";
        public Vector3 startPoint;
        public Vector3 endPoint;
        public float sag = 6.5f;
        public int segments = 40;
        public Color wireColor = new Color(0.0f, 0.85f, 1.0f, 0.95f);
        public float wireWidth = 0.25f;

        private LineRenderer lineRenderer;

        public float SpanLength => Vector3.Distance(startPoint, endPoint);
        public Vector3 MaxSagPoint
        {
            get
            {
                Vector3 mid = (startPoint + endPoint) * 0.5f;
                return mid - Vector3.up * sag;
            }
        }

        void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            SetupRenderer();
        }

        void Start()
        {
            RebuildCatenary();
        }

        private void SetupRenderer()
        {
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = wireWidth;
            lineRenderer.endWidth = wireWidth;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = wireColor;
            lineRenderer.endColor = wireColor;
        }

        public void SetPoints(Vector3 p1, Vector3 p2, float maxSag)
        {
            startPoint = p1;
            endPoint = p2;
            sag = maxSag;
            RebuildCatenary();
        }

        public void RebuildCatenary()
        {
            if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();
            if (segments < 4) segments = 4;

            lineRenderer.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 p = Vector3.Lerp(startPoint, endPoint, t);
                p.y -= 4f * sag * t * (1f - t);
                lineRenderer.SetPosition(i, p);
            }
        }
    }
}
