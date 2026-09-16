using System;
using UnityEngine;

namespace OTA.Corridor.Annotation
{
    /// <summary>
    /// Utility to render 3D wireframe boxes, solid transparent faces, and beacons in scene.
    /// Uses ZTest Always so indicators are never hidden under point clouds.
    /// </summary>
    public static class WireframeBoxDrawer
    {
        private static Material lineMat;

        private static void EnsureMaterial()
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

        public static void DrawRotatedBox(Bounds b, float yawDeg, Vector3 pivot, Color col)
        {
            EnsureMaterial();
            lineMat.SetPass(0);

            Vector3[] v = GetBoxVertices(b, yawDeg, pivot);

            GL.PushMatrix();
            GL.Begin(GL.LINES);
            GL.Color(col);

            // Bottom square
            DrawLine(v[0], v[1]); DrawLine(v[1], v[2]); DrawLine(v[2], v[3]); DrawLine(v[3], v[0]);
            // Top square
            DrawLine(v[4], v[5]); DrawLine(v[5], v[6]); DrawLine(v[6], v[7]); DrawLine(v[7], v[4]);
            // Pillars
            DrawLine(v[0], v[4]); DrawLine(v[1], v[5]); DrawLine(v[2], v[6]); DrawLine(v[3], v[7]);


            GL.End();
            GL.PopMatrix();
        }

        public static void DrawSolidRotatedBox(Bounds b, float yawDeg, Vector3 pivot, Color faceColor)
        {
            EnsureMaterial();
            lineMat.SetPass(0);

            Vector3[] v = GetBoxVertices(b, yawDeg, pivot);

            GL.PushMatrix();
            GL.Begin(GL.TRIANGLES);
            GL.Color(faceColor);

            // 6 faces * 2 triangles each
            DrawQuad(v[0], v[1], v[2], v[3]);
            DrawQuad(v[4], v[5], v[6], v[7]);
            DrawQuad(v[0], v[1], v[5], v[4]);
            DrawQuad(v[2], v[3], v[7], v[6]);
            DrawQuad(v[3], v[0], v[4], v[7]);
            DrawQuad(v[1], v[2], v[6], v[5]);

            GL.End();
            GL.PopMatrix();
        }

        public static void DrawBeacon(Vector3 apex, float totalHeight, Color col)
        {
            EnsureMaterial();
            lineMat.SetPass(0);

            GL.PushMatrix();
            GL.Begin(GL.LINES);
            GL.Color(col);

            // Vertical spine
            Vector3 bBase = apex - Vector3.up * totalHeight;
            DrawLine(bBase, apex);

            // 3D Diamond at apex
            float r = 1.2f;
            DrawLine(apex - Vector3.right * r, apex + Vector3.right * r);
            DrawLine(apex - Vector3.forward * r, apex + Vector3.forward * r);
            DrawLine(apex - Vector3.up * r, apex + Vector3.up * (r * 1.5f));

            // Apex diamond frame
            Vector3 tUp = apex + Vector3.up * (r * 1.5f);
            Vector3 tDn = apex - Vector3.up * r;
            Vector3 pR = apex + Vector3.right * r;
            Vector3 pL = apex - Vector3.right * r;
            Vector3 pF = apex + Vector3.forward * r;
            Vector3 pB = apex - Vector3.forward * r;

            DrawLine(tUp, pR); DrawLine(tUp, pL); DrawLine(tUp, pF); DrawLine(tUp, pB);
            DrawLine(tDn, pR); DrawLine(tDn, pL); DrawLine(tDn, pF); DrawLine(tDn, pB);

            // Ground footprint circle (16 segs)
            float footR = 4f;
            int segs = 16;
            Vector3 prevP = bBase + Vector3.right * footR;
            for (int i = 1; i <= segs; i++)
            {
                float ang = (float)i / segs * Mathf.PI * 2f;
                Vector3 currP = bBase + new Vector3(Mathf.Cos(ang) * footR, 0f, Mathf.Sin(ang) * footR);
                DrawLine(prevP, currP);
                prevP = currP;
            }

            GL.End();
            GL.PopMatrix();
        }

        private static Vector3[] GetBoxVertices(Bounds b, float yawDeg, Vector3 pivot)
        {
            Vector3 min = b.min;
            Vector3 max = b.max;

            Vector3[] v = new Vector3[8]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
                new Vector3(min.x, max.y, max.z),
            };

            Quaternion rot = Quaternion.Euler(0, yawDeg, 0);
            for (int i = 0; i < 8; i++)
            {
                v[i] = pivot + rot * (v[i] - pivot);
            }
            return v;
        }

        private static void DrawQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            GL.Vertex(p0); GL.Vertex(p1); GL.Vertex(p2);
            GL.Vertex(p0); GL.Vertex(p2); GL.Vertex(p3);
        }

        private static void DrawLine(Vector3 p1, Vector3 p2)
        {
            GL.Vertex(p1);
            GL.Vertex(p2);
        }
    }
}
