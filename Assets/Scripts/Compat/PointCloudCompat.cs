using System.Collections.Generic;
using UnityEngine;

// Global-namespace compatibility shim so the stock RuntimeViewerDX11.cs
// (copied from the 3dTrack fork) compiles without the Willshare/business
// libraries. RuntimeViewerDX11 references these types by bare name.
// The OTA P0 direct-load path (SetPoints) does not use them.

public class PointsCloudCutter : MonoBehaviour
{
    public class SinglePoint
    {
        Vector3 position;
        Vector4 color;

        public SinglePoint(Vector3 pos, Vector4 col) { Position = pos; Color = col; }
        public SinglePoint(SinglePoint sp) { position = sp.position; color = sp.color; }

        public Vector3 Position { get => position; set => position = value; }
        public Vector4 Color { get => color; set => color = value; }
    }
}

public class PointsCloundDivider : MonoBehaviour
{
    public class PointTile
    {
        public class OBB
        {
            public Vector3 center;
            public Vector3 size;
        }

        public List<int> indices = new List<int>();
        public PointTile[] children;
        public Bounds bounds = new Bounds();
        public Bounds tmpBounds = new Bounds();
        public Bounds boundsForLine = new Bounds();
        public PointsCloundDivider _pcd = null;
        public OBB _obb = null;

        public void Clear()
        {
            indices.Clear();
            if (children == null) return;
            for (int i = 0; i < children.Length; i++) children[i].Clear();
        }

        public PointTile(PointsCloundDivider pcd) { _pcd = pcd; }

        public Vector3 GetPoint(int i)
        {
            // Compat stub: business pick path is unused in OTA P0 (SetPoints).
            return Vector3.zero;
        }
    }

    public List<PointTile> IntersectsRay(Ray ray) { return null; }
}

public class GISCameraController : MonoBehaviour
{
    public Ray GetMouseRay(Vector3 offset)
    {
        var cam = GetComponent<Camera>();
        var pos = Input.mousePosition + offset;
        return cam ? cam.ScreenPointToRay(pos) : new Ray();
    }

    public Vector2 GetMousePosition() { return Input.mousePosition; }
}

public class SectionManager : MonoBehaviour
{
    public static SectionManager instance;

    public Vector4 RemoveCaches(int index) { return Vector4.zero; }
}

public class ColorRulerManager : MonoBehaviour
{
    public static ColorRulerManager instance;

    public Vector4 GetRulerValue(string name) { return Vector4.zero; }
    public bool ContainsValue(Vector4 v) { return false; }
}

public class ObstacleTreeManager : MonoBehaviour
{
    public static ObstacleTreeManager instance;

    public Vector4 RemoveCaches(int index, bool isTower) { return Vector4.zero; }
}

public static class AnalysisBase
{
    public static void CombineTable(Dictionary<int, Vector4> dst, Dictionary<int, Vector4> src)
    {
        foreach (var kv in src) dst[kv.Key] = kv.Value;
    }
}
