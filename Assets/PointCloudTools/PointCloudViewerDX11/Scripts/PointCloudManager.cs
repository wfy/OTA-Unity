// point cloud manager (currently used for point picking)
// unitycoder.com

using GK;
using PointCloudHelpers;
using System.Collections.Generic;
using System.Threading;
using unitycodercom_PointCloudBinaryViewer;
using UnityEngine;
using UnityLibrary;

namespace PointCloudViewer
{
    public class PointCloudManager : MonoBehaviour
    {
        public List<PointCloudViewerDX11> viewers = new List<PointCloudViewerDX11>();
        public int slices = 16;

        [Header("Advanced Options")]
        // TODO move to measurement tool?
        [Tooltip("Vector3.Dot magnitude threshold, try to keep in very small values")]
        public float pointSearchThreshold = 0.0001f;
        [Tooltip("1 = Full precision, every point is indexed. Lower values decrease collect & picking speeds")]
        public PointIndexPrecision pointIndexPrecision = PointIndexPrecision.Full;

        // TODO could add some faded sphere effect, so that other points get grayed out (too far for picking)
        public float maxPickDistance = 100;

        internal List<Cloud> clouds; // all clouds
        Thread pointPickingThread;

        public delegate void PointSelected(Vector3 pointPos);
        public static event PointSelected PointWasSelected;

        public static PointCloudManager instance;

        private void Awake()
        {
            instance = this;

            if (MainThread.instanceCount == 0)
            {
                if (viewers != null)
                {
                    for (int i = 0; i < viewers.Count; i++)
                    {
                        if (viewers[i] != null) viewers[i].FixMainThreadHelper();
                    }
                }
            }

            clouds = new List<Cloud>();

            // wait for loading complete event (for automatic registration)
            if (viewers != null)
            {
                for (int i = 0; i < viewers.Count; i++)
                {
                    if (viewers[i] != null) viewers[i].OnLoadingComplete -= CloudIsReady;
                    if (viewers[i] != null) viewers[i].OnLoadingComplete += CloudIsReady;
                }
            }
            else
            {
                Debug.Log("PointCloudManager: No viewers..");
            }
        }

        public void RegisterCloudManually(PointCloudViewerDX11 newViewer)
        {
            for (int i = 0; i < viewers.Count; i++)
            {
                // remove previous same instance cloud, if already in the list  
                for (int vv = 0, viewerLen = viewers.Count; vv < viewerLen; vv++)
                {
                    if (viewers[vv].fileName == newViewer.fileName)
                    {
                        Debug.Log("Removed duplicate cloud from viewers: " + newViewer.fileName);
                        clouds.RemoveAt(vv);
                        break;
                    }
                }
            }

            // add new cloud
            viewers.Add(newViewer);

            // manually call cloud to be processed
            CloudIsReady(newViewer.fileName);
        }

        public void CloudIsReady(object cloudFilePath)
        {
            ProcessCloud((string)cloudFilePath);
            Debug.Log("Cloud is ready for picking: " + (string)cloudFilePath);
        }

        void ProcessCloud(string cloudPath)
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            int viewerIndex = -1;
            // find index
            for (int vv = 0, viewerLen = viewers.Count; vv < viewerLen; vv++)
            {
                if (viewers[vv].fileName == cloudPath)
                {
                    viewerIndex = vv;
                    break;
                }
            }

            if (viewerIndex == -1)
            {
                Debug.LogError("Failed to find matching cloud for indexing..");
            }
            //Debug.Log("Adding: " + viewerIndex);

            var cloudBounds = viewers[viewerIndex].GetBounds();

            float minX = cloudBounds.min.x - 0.5f;// add sóme buffer for float imprecisions
            float minY = cloudBounds.min.y - 0.5f;
            float minZ = cloudBounds.min.z - 0.5f;

            float maxX = cloudBounds.max.x + 0.5f;
            float maxY = cloudBounds.max.y + 0.5f;
            float maxZ = cloudBounds.max.z + 0.5f;

            // cloud total size
            float width = (maxX - minX);
            float height = (maxY - minY);
            float depth = (maxZ - minZ);

            float stepX = width / (float)slices;
            float stepY = height / (float)slices;
            float stepZ = depth / (float)slices;

            // NOTE need to clamp to minimum 1?
            //if (stepY < 1) stepY += 32;

            float stepXInverted = 1f / stepX;
            float stepYInverted = 1f / stepY;
            float stepZInverted = 1f / stepZ;

            //Debug.Log("minX=" + minX + " minY=" + minY + " minZ=" + minZ + "  maxX=" + maxX + " maxY=" + maxY + " maxZ=" + maxZ);
            //Debug.Log("stepXInverted=" + stepXInverted + " stepYInverted=" + stepYInverted + " stepZInverted=" + stepZInverted + "  stepX=" + stepX + " stepY=" + stepY + " stepZ=" + stepZ);

            int totalBoxes = slices * slices * slices;

            // create new cloud object
            var newCloud = new Cloud();
            // add to total clouds
            // init child node boxes
            newCloud.nodes = new NodeBox[totalBoxes];
            newCloud.viewerIndex = viewerIndex;
            newCloud.bounds = cloudBounds;

            float xs = minX;
            float ys = minY;
            float zs = minZ;

            float halfStepX = stepX * 0.5f;
            float halfStepY = stepY * 0.5f;
            float halfStepZ = stepZ * 0.5f;

            Vector3 p;
            Vector3 tempCenter = Vector3.zero;
            Vector3 tempSize = Vector3.zero;
            Bounds boxBoundes = new Bounds();

            // build node boxes
            for (int y = 0; y < slices; y++)
            {
                tempSize.y = stepY;
                tempCenter.y = ys + halfStepY;
                for (int z = 0; z < slices; z++)
                {
                    tempSize.z = stepZ;
                    tempCenter.z = zs + halfStepZ;
                    int slicesMulYZ = slices * (y + slices * z);
                    for (int x = 0; x < slices; x++)
                    {
                        tempSize.x = stepX;
                        tempCenter.x = xs + halfStepX;

                        var np = new NodeBox();
                        boxBoundes.center = tempCenter;
                        boxBoundes.size = tempSize;
                        np.bounds = boxBoundes;
                        np.points = new List<int>(); // for struct
                        //PointCloudHelpers.PointCloudTools.DrawBounds(np.bounds, 20);

                        newCloud.nodes[x + slicesMulYZ] = np;
                        xs += stepX;
                    }
                    xs = minX;
                    zs += stepZ;
                }
                zs = minZ;
                ys += stepY;
            }

            stopwatch.Stop();
            // Debug.Log("Split: " + stopwatch.ElapsedTicks + " ticks");
            stopwatch.Reset();


            stopwatch.Start();

            /*
            Debug.Log("minx:" + minX + " maxx:" + maxX + " width:" + width);
            Debug.Log("miny:" + minY + " maxy:" + maxY + " height:" + height);
            Debug.Log("minz:" + minZ + " maxz:" + maxZ + " depth:" + depth);
            Debug.Log("stepX:" + stepX + " stepY:" + stepY + " stepZ:" + stepZ);
            Debug.Log("boxes:" + slices + " total=" + totalBoxes + " arrayboxes:" + nodeBoxes.Length);
            */

            // pick step resolution
            int pointStep = 1;
            switch (pointIndexPrecision)
            {
                case PointIndexPrecision.Full:
                    pointStep = 1;
                    break;
                case PointIndexPrecision.Half:
                    pointStep = 2;
                    break;
                case PointIndexPrecision.Quarter:
                    pointStep = 4;
                    break;
                case PointIndexPrecision.Eighth:
                    pointStep = 8;
                    break;
                case PointIndexPrecision.Sixteenth:
                    pointStep = 16;
                    break;
                case PointIndexPrecision.TwoHundredFiftySixth:
                    pointStep = 256;
                    break;
                default:
                    break;
            }

            // collect points to boxes
            for (int j = 0, pointLen = viewers[newCloud.viewerIndex].points.Length; j < pointLen; j += pointStep)
            {
                p = viewers[newCloud.viewerIndex].points[j];
                // http://www.reactiongifs.com/r/mgc.gif
                int sx = (int)((p.x - minX) * stepXInverted);
                int sy = (int)((p.y - minY) * stepYInverted);
                int sz = (int)((p.z - minZ) * stepZInverted);

                // could use this also for overflow checking
                //sx = sx >= slices ? slices - 1 : sx;
                //sy = sy >= slices ? slices - 1 : sy;
                //sz = sz >= slices ? slices - 1 : sz;

                var boxIndex = sx + slices * (sy + slices * sz);
                //try
                //{
                    newCloud.nodes[boxIndex].points.Add(j);
                //}
                //catch (System.Exception)
                //{
                //    Debug.LogError("ERROR; slices = " + slices + "  sx,sy,sz=" + sx + "," + sy + "," + sz + " boxIndex=" + boxIndex + " p.x=" + p.x + " minX=" + minX + " p.y=" + p.y + "  minY=" + minY + " p.z=" + p.z + "  minZ=" + minZ + " newCloud.nodes=" + newCloud.nodes.Length);
                //    throw;
                //}
            }
            // add to clouds list
            clouds.Add(newCloud);

            stopwatch.Stop();
            //Debug.Log("Collect: " + stopwatch.ElapsedMilliseconds + " ms");
            stopwatch.Reset();
        }

        public void RunPointPickingThread(Ray ray)
        {
            ParameterizedThreadStart start = new ParameterizedThreadStart(FindClosestPoint);
            pointPickingThread = new Thread(start);
            pointPickingThread.IsBackground = true;
            pointPickingThread.Start(ray);
        }

        public void FindClosestPoint(object rawRay)
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            int nearestIndex = -1;
            float nearestPointRayDist = Mathf.Infinity;
            int viewerIndex = -1; // which viewer has this point data

            Ray ray = (Ray)rawRay;
            Vector3 rayDirection = ray.direction;
            Vector3 rayOrigin = ray.origin;
            Vector3 rayEnd = rayOrigin + rayDirection * maxPickDistance;
            Vector3 point;

            // check our selection ray
            // MainThread.Call(PointCloudMath.DrawRay, (object)ray);

            // check all clouds
            for (int cloudIndex = 0, cloudsLen = clouds.Count; cloudIndex < cloudsLen; cloudIndex++)
            {
                // check all nodes from each cloud
                for (int nodeIndex = 0, nodesLen = clouds[cloudIndex].nodes.Length; nodeIndex < nodesLen; nodeIndex++)
                {
                    // check if this box intersects ray, or that camera is inside node
                    if (clouds[cloudIndex].nodes[nodeIndex].bounds.IntersectRay(ray))
                    {
                        // show what node box we are checking
                        //MainThread.Call(PointCloudTools.DrawBounds, (object)clouds[cloudIndex].nodes[nodeIndex].bounds);
                        //Debug.Log("Hit cloud: " + System.IO.Path.GetFileName(viewers[clouds[cloudIndex].viewerIndex].fileName) + " nodebox:" + nodeIndex + " with x points=" + clouds[cloudIndex].nodes[nodeIndex].points.Count);

                        // then check all points from that node
                        for (int nodePointIndex = 0, nodePointLen = clouds[cloudIndex].nodes[nodeIndex].points.Count; nodePointIndex < nodePointLen; nodePointIndex++)
                        {
                            int pointIndex = clouds[cloudIndex].nodes[nodeIndex].points[nodePointIndex];
                            point = viewers[clouds[cloudIndex].viewerIndex].points[pointIndex];

                            // limit picking angle
                            float dotAngle = Vector3.Dot(rayDirection, (point - rayOrigin).normalized);
                            if (dotAngle > 0.99f)
                            {
                                float camDist = PointCloudMath.Distance(rayOrigin, point);
                                var pointRayDist = PointCloudMath.SqDistPointSegment(rayOrigin, rayEnd, point);
                                // magic formula to prefer nearby points
                                //var normCamDist = (camDist / maxPickDistance) * pointRayDist; // ok for nearby points, but too often picks nearby
                                var normCamDist = (camDist / maxPickDistance) * pointRayDist * pointRayDist; // best so far, but still too eager to pick nearby points
                                //var normCamDist = maxPickDistance-(camDist / (pointRayDist* pointRayDist)); // better for far away picking

                                if (normCamDist < nearestPointRayDist)
                                {
                                    // skip point if too far
                                    if (pointRayDist > maxPickDistance) continue;
                                    nearestPointRayDist = normCamDist;
                                    nearestIndex = pointIndex;
                                    viewerIndex = clouds[cloudIndex].viewerIndex;

                                    // debug color show checked points
                                    //MainThread.Call(PointCloudMath.DebugHighLightPointYellow, point);
                                    //Debug.Log("normCamDist=" + normCamDist + " : pointRayDist=" + pointRayDist);
                                }
                            }
                            // to debug which points where checked
                            //viewers[clouds[cloudIndex].viewerIndex].pointColors[pointIndex] = new Vector3(1, 0, 0);
                        } // each point inside box
                    } // if ray hits box
                } // all boxes
                // for debug update test colors
                //MainThread.Call(viewers[clouds[cloudIndex].viewerIndex].UpdateColorData);
            } // all clouds

            if (nearestIndex > -1)
            {
                // UnityLibrary.MainThread.Call(HighLightPoint);
                MainThread.Call(PointCallBack, viewers[viewerIndex].points[nearestIndex]);
                Debug.Log("Selected Point #:" + (nearestIndex) + " Position:" + viewers[viewerIndex].points[nearestIndex] + " from " + (System.IO.Path.GetFileName(viewers[viewerIndex].fileName)));

                // DEBUG change color for that point
                //viewers[viewerIndex].pointColors[nearestIndex] = new Vector3(0, 0, 0);
                //MainThread.Call(viewers[viewerIndex].UpdateColorData);
            }
            else
            {
                Debug.Log("No points found..");
            }

            stopwatch.Stop();
            Debug.Log("PickTimer: " + stopwatch.ElapsedMilliseconds + "ms");
            stopwatch.Reset();

            if (pointPickingThread != null && pointPickingThread.IsAlive == true) pointPickingThread.Abort();
        } // FindClosestPoint

        private void OnDestroy()
        {
            if (pointPickingThread != null && pointPickingThread.IsAlive == true) pointPickingThread.Abort();
        }

        // this gets called after thread finds closest point
        void PointCallBack(System.Object a)
        {
            if (PointWasSelected != null) PointWasSelected((Vector3)a);
        }

        public static float DistanceToRay(Ray ray, Vector3 point)
        {
            return Vector3.Cross(ray.direction, point - ray.origin).sqrMagnitude;
            //return Vector3.Cross(ray.direction, point - ray.origin).magnitude;
        }

        public static float Distance(Vector3 a, Vector3 b)
        {
            float vecx = a.x - b.x;
            float vecy = a.y - b.y;
            float vecz = a.z - b.z;
            return vecx * vecx + vecy * vecy + vecz * vecz;
        }

        // checks if give AABB box collides with any point (point is inside the given box)
        public bool BoundsIntersectsCloud(Bounds box)
        {
            //PointCloudHelpers.PointCloudTools.DrawBounds(box);

            // check all clouds
            for (int cloudIndex = 0, length2 = clouds.Count; cloudIndex < length2; cloudIndex++)
            {
                // exit if outside whole cloud bounds
                if (clouds[cloudIndex].bounds.Contains(box.center) == false) return false;

                // get full cloud bounds
                float minX = clouds[cloudIndex].bounds.min.x;
                float minY = clouds[cloudIndex].bounds.min.y;
                float minZ = clouds[cloudIndex].bounds.min.z;
                float maxX = clouds[cloudIndex].bounds.max.x;
                float maxY = clouds[cloudIndex].bounds.max.y;
                float maxZ = clouds[cloudIndex].bounds.max.z;

                // helpers
                float width = Mathf.Ceil(maxX - minX);
                float height = Mathf.Ceil(maxY - minY);
                float depth = Mathf.Ceil(maxZ - minZ);
                float stepX = width / slices;
                float stepY = height / slices;
                float stepZ = depth / slices;
                float stepXInverted = 1f / stepX;
                float stepYInverted = 1f / stepY;
                float stepZInverted = 1f / stepZ;

                // get collider box min node index
                int colliderX = (int)((box.center.x - minX) * stepXInverted);
                int colliderY = (int)((box.center.y - minY) * stepYInverted);
                int colliderZ = (int)((box.center.z - minZ) * stepZInverted);
                var BoxIndex = colliderX + slices * (colliderY + slices * colliderZ);
                //PointCloudHelpers.PointCloudTools.DrawBounds(clouds[cloudIndex].nodes[BoxIndex].bounds);

                // check if we hit within that area
                for (int j = 0, l = clouds[cloudIndex].nodes[BoxIndex].points.Count; j < l; j++)
                {
                    // each point
                    int pointIndex = clouds[cloudIndex].nodes[BoxIndex].points[j];
                    Vector3 p = viewers[clouds[cloudIndex].viewerIndex].points[pointIndex];

                    // check if within bounds distance
                    if (box.Contains(p) == true)
                    {
                        return true;
                    }
                }
            }
            return false;
        } // BoundsIntersectsCloud

        // area selection from multiple clouds, returns list of collectedpoints struct (which contains indexes to cloud and the actual point)
        public List<CollectedPoint> ConvexHullSelectPoints(GameObject go, List<Vector3> area)
        {
            // build area bounds
            var areaBounds = new Bounds();
            for (int i = 0, len = area.Count; i < len; i++)
            {
                areaBounds.Encapsulate(area[i]);
            }

            // check bounds
            //PointCloudTools.DrawBounds(areaBounds, 100);

            // build area hull
            var calc = new ConvexHullCalculator();

            var verts = new List<Vector3>();
            var tris = new List<int>();
            var normals = new List<Vector3>();
            var mf = go.GetComponent<MeshFilter>();
            if (go == null) Debug.LogError("Missing MeshFilter from " + go.name, go);
            var mesh = new Mesh();
            mf.sharedMesh = mesh;

            calc.GenerateHull(area, false, ref verts, ref tris, ref normals);

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetNormals(normals);

            var results = new List<CollectedPoint>();
            // all clouds
            for (int cloudIndex = 0, length2 = clouds.Count; cloudIndex < length2; cloudIndex++)
            {
                // exit if outside whole cloud bounds
                if (clouds[cloudIndex].bounds.Intersects(areaBounds) == false) return null;

                // check all nodes from this cloud
                for (int nodeIndex = 0; nodeIndex < clouds[cloudIndex].nodes.Length; nodeIndex++)
                {
                    // early exit if bounds doesnt hit this node?
                    if (clouds[cloudIndex].nodes[nodeIndex].bounds.Intersects(areaBounds) == false) continue;

                    // loop points
                    for (int j = 0, l = clouds[cloudIndex].nodes[nodeIndex].points.Count; j < l; j++)
                    {
                        // check all points from that node
                        int pointIndex = clouds[cloudIndex].nodes[nodeIndex].points[j];
                        // get actual point
                        Vector3 p = viewers[clouds[cloudIndex].viewerIndex].points[pointIndex];

                        // check if inside hull
                        if (IsPointInsideMesh(mesh, p))
                        {
                            var temp = new CollectedPoint();
                            temp.cloudIndex = cloudIndex;
                            temp.pointIndex = pointIndex;
                            results.Add(temp);
                        }
                    }

                }

            } // for clouds
            return results;
        }

        // source http://answers.unity.com/answers/612014/view.html
        public static bool IsPointInsideMesh(Mesh aMesh, Vector3 point)
        {
            var verts = aMesh.vertices;
            var tris = aMesh.triangles;
            int triangleCount = tris.Length / 3;
            for (int i = 0; i < triangleCount; i++)
            {
                var V1 = verts[tris[i * 3]];
                var V2 = verts[tris[i * 3 + 1]];
                var V3 = verts[tris[i * 3 + 2]];
                var P = new Plane(V1, V2, V3);
                if (P.GetSide(point)) return false;
            }
            return true;
        }
    } // class
} // namespace