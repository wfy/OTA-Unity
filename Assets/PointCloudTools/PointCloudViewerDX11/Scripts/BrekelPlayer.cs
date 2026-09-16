// Brekel Animated Point Cloud Viewer (.bin format, exported from Brekel) https://brekel.com/brekel-pointcloud-v3/
// http://unitycoder.com

#if !UNITY_WEBPLAYER && !UNITY_SAMSUNGTV

using UnityEngine;
using System.IO;
using PointCloudHelpers;
using UnityEngine.Rendering;
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading;

namespace Unitycoder.Brekel
{
    public class BrekelPlayer : MonoBehaviour
    {
        [Header("Source File")]
        public string fileName = "StreamingAssets/PointCloudViewerSampleData/sample.bin";

        [Header("Settings")]
        [Tooltip("Load cloud at Start()")]
        public bool loadAtStart = true;
        public Material cloudMaterial;
        [Tooltip("Create copy of the material. Must enable if viewing multiple clouds with same materials")]
        public bool instantiateMaterial = false; // set True if using multiple viewers
        public bool showDebug = false;

        [Header("Visibility")]
        public bool displayPoints = true;
        [Tooltip("Enable this if you have multiple cameras and only want to draw in MainCamera")]
        public bool renderOnlyMainCam = false;
        [HideInInspector]
        public bool isPlaying = true;

        [Header("Controls")]
        [Tooltip("Toggle Play/Pause")]
        public KeyCode togglePlay = KeyCode.Space;
        [Tooltip("Rewind is not supported in LargeFileTreaming mode")]
        public KeyCode playPrevFrame = KeyCode.Comma;
        public KeyCode playNextFrame = KeyCode.Period;
        public float playbackDelay = 0f;

        // Brekel animated frames variables
        int[] numberOfPointsPerFrame;
        Int64[] frameBinaryPositionForEachFrame;
        Vector3[] animatedPointArray;
        Vector3[] animatedColorArray;
        int totalNumberOfPoints; // total point count, with padding if enabled
        int currentFrame = 0;
        int[] animatedOffset;
        float nextFrameTimer = 0.0F;

        private byte binaryVersion = 0;
        private int numberOfFrames = 0;
        [HideInInspector]
        public bool containsRGB = false;
        [HideInInspector]
        public int maxPointsPerFrame = 0;
        [HideInInspector]
        public int totalMaxPoints = 0; // new variable to keep total maximum point count
        ComputeBuffer bufferPoints;
        ComputeBuffer bufferColors;
        internal Vector3[] points; // actual point cloud points array
        internal Vector3[] pointColors;
        internal Vector3[] clearArray; // temp clearing array

        float frameRate = 0; // not used

        bool isLoading = false;
        bool haveError = false;
        bool isReady = false;

        // events
        public delegate void OnLoadComplete(string filename);
        public event OnLoadComplete OnLoadingComplete;

        Camera cam;

        [Header("Rendering")]
        [Tooltip("Draw using CommandBuffer instead of OnRenderObject")]
        public bool useCommandBuffer = false;
        [Tooltip("Default value: AfterForwardOpaque")]
        public CameraEvent camDrawPass = CameraEvent.AfterForwardOpaque;
        internal CommandBuffer commandBuffer;
        public bool forceDepthBufferPass = false;
        Material depthMaterial;
        [Tooltip("Changing CameraEvent takes effect only at Start(). Default value: AfterDepthTexture")]
        public CameraEvent camDepthPass = CameraEvent.AfterDepthTexture;
#if UNITY_EDITOR
        [Tooltip("Forces CommandBuffer to be rendered in Scene window also")]
        public bool commandBufferToSceneCamera = false;
#endif
        internal CommandBuffer commandBufferDepth;

        [Header("Experimental")]
        //public bool useMeshRendering = false;
        [Tooltip("Stream data from disk *Requires new .bin format to be exported from Brekel PointCloud V3")]
        public bool useLargeFileStreaming = false;

        int currentFramePointCount = 0;
        Vector3 transformPos;
        Matrix4x4 Matrix4x4identity = Matrix4x4.identity;

        string applicationStreamingAssetsPath;
        const int sizeofInt32 = sizeof(System.Int32);
        const int sizeofInt64 = sizeof(System.Int64);
        const int sizeOfSingle = sizeof(System.Single);
        const int sizeOfBool = sizeof(System.Boolean);
        const int sizeOfByte = sizeof(System.Byte);

        // filestreaming
        int pointQueueCount = 0;
        Queue<Vector3[]> pointQueue = new Queue<Vector3[]>();
        Queue<Vector3[]> colorQueue = new Queue<Vector3[]>();
        Thread fileStreamerThread;
        bool abortStreamerThread = false;
        [Tooltip("How many frames are allowed in the buffer, if its full, skips current new frame (used for Large File Streaming, 100 seems like good value usually)")]
        public int maxFrameBufferCount = 100;
        int MAXPOINTCOUNT = 0;

        bool isInitializingBuffers = false;

        void Awake()
        {
            applicationStreamingAssetsPath = Application.streamingAssetsPath;
        }

        // init
        void Start()
        {
            FixMainThreadHelper();

            transformPos = transform.position;
            cam = Camera.main;

            if (useCommandBuffer == true)
            {
                commandBuffer = new CommandBuffer();
                cam.AddCommandBuffer(camDrawPass, commandBuffer);

#if UNITY_EDITOR
                if (commandBufferToSceneCamera == true) UnityEditor.SceneView.GetAllSceneCameras()[0].AddCommandBuffer(camDrawPass, commandBuffer);
#endif

            }

            if (forceDepthBufferPass == true)
            {
                depthMaterial = cloudMaterial;
                commandBufferDepth = new CommandBuffer();
                cam.AddCommandBuffer(camDepthPass, commandBufferDepth);
            }

            if (cam == null) { Debug.LogError("Camera main is missing..", gameObject); }

            // create material clone, so can view multiple clouds
            if (instantiateMaterial == true)
            {
                cloudMaterial = new Material(cloudMaterial);
            }

            if (loadAtStart == true)
            {
                //ReadAnimatedPointCloud(); // add option for non-threading?

                ParameterizedThreadStart start = new ParameterizedThreadStart(ReadAnimatedPointCloud);
                fileStreamerThread = new Thread(start);
                fileStreamerThread.IsBackground = true;
                fileStreamerThread.Start(fileName);
            }
        }

        // ====================================== mainloop ======================================
        void Update()
        {
            if (isLoading == true || haveError == true && useLargeFileStreaming == false) return;

            if (Input.GetKeyDown(togglePlay))
            {
                isPlaying = !isPlaying;
            }


            if (isPlaying == true && Time.time > nextFrameTimer)
            {
                nextFrameTimer = Time.time + playbackDelay;
                UpdateFrame();
                currentFrame = (++currentFrame) % numberOfFrames;
            }
            else // paused or waiting for framedelay
            {
                if (useLargeFileStreaming == false)
                {
                    if (Input.GetKeyDown(playPrevFrame))
                    {
                        currentFrame--;
                        if (currentFrame < 0) currentFrame = numberOfFrames - 1;
                        UpdateFrame();
                    }

                    if (Input.GetKeyDown(playNextFrame))
                    {
                        currentFrame = (++currentFrame) % numberOfFrames;
                        UpdateFrame();
                    }
                }
            }
        }

        void UpdateFrame()
        {
            if (useLargeFileStreaming == true)
            {
                if (pointQueueCount > 0)
                {
                    UpdatePointDataFromStream();
                }
            }
            else // full file
            {
                currentFramePointCount = numberOfPointsPerFrame[currentFrame];
                Array.Copy(animatedPointArray, animatedOffset[currentFrame], points, 0, currentFramePointCount);
                if (containsRGB == true) Array.Copy(animatedColorArray, animatedOffset[currentFrame], pointColors, 0, currentFramePointCount);
                bufferPoints.SetData(points);
                if (containsRGB) bufferColors.SetData(pointColors);
            }
            //Debug.Log("current frame = " + currentFrame);
        }

        public void UpdatePointDataFromStream()
        {
            var tempdata = pointQueue.Dequeue();
            currentFramePointCount = tempdata.Length; // TODO take len from ready array
            bufferPoints.SetData(tempdata);

            // add clear padding
            if (useCommandBuffer || forceDepthBufferPass)
            {
                if (tempdata.Length < clearArray.Length)
                {
                    // TEST if better than array resize/copy
                    bufferPoints.SetData(clearArray, 0, tempdata.Length, clearArray.Length - tempdata.Length);
                }
            }

            cloudMaterial.SetBuffer("buf_Points", bufferPoints);

            if (containsRGB == true)
            {
                tempdata = colorQueue.Dequeue();
                bufferColors.SetData(tempdata);
                cloudMaterial.SetBuffer("buf_Colors", bufferColors);
            }

            pointQueueCount--;
            currentFrame++; // total played frame count
        }

        // For Brekel animated binary data only
        void ReadAnimatedPointCloud(System.Object objfileName)
        {
            isLoading = true;

            var fileName = (string)objfileName;
            // if not full path, use streaming assets
            if (Path.IsPathRooted(fileName) == false)
            {
                fileName = Path.Combine(applicationStreamingAssetsPath, fileName);
            }

            if (PointCloudTools.CheckIfFileExists(fileName) == false)
            {
                Debug.LogError("File not found:" + fileName);
                haveError = true;
                isLoading = false;
                return;
            }

            Debug.Log("Reading pointcloud from: " + fileName);

            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            isReady = false;
            haveError = false;
            int totalCounter = 0;

            long fileSize = new FileInfo(fileName).Length;
            var tempPoint = Vector3.zero;
            var tempColor = Vector3.zero;

            totalNumberOfPoints = 0;
            maxPointsPerFrame = 0;

            if (fileSize >= 2147483647 || useLargeFileStreaming == true)
            {
                Debug.Log("Starting large file streaming: " + PointCloudTools.HumanReadableFileSize(fileSize));
                isLoading = false;

                useLargeFileStreaming = true;

                using (FileStream fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.None))
                using (BinaryReader binReader = new BinaryReader(fs))
                {
                    // parse header data
                    Int32 byteIndex = 0;
                    binaryVersion = binReader.ReadByte();
                    byteIndex += sizeOfByte;
                    if (binaryVersion != 3) { Debug.LogError("For large Animated point cloud, header binaryVersion should have value (3), received=" + binaryVersion); return; }

                    numberOfFrames = binReader.ReadInt32();
                    byteIndex += sizeofInt32;
                    frameRate = binReader.ReadSingle();
                    byteIndex += sizeOfSingle;
                    containsRGB = binReader.ReadBoolean();
                    byteIndex += sizeOfBool;

                    numberOfPointsPerFrame = new int[numberOfFrames];

                    if (showDebug) Debug.Log("(ReadAll) Animated file header: binaryVersion= " + binaryVersion + " numberOfFrames = " + numberOfFrames + " hasRGB=" + containsRGB);

                    // get each frame point count info
                    for (int i = 0; i < numberOfFrames; i++)
                    {
                        numberOfPointsPerFrame[i] = binReader.ReadInt32();
                        byteIndex += sizeofInt32;
                        if (numberOfPointsPerFrame[i] > maxPointsPerFrame) maxPointsPerFrame = numberOfPointsPerFrame[i]; // largest value will be used as a fixed size for point array
                        totalNumberOfPoints += numberOfPointsPerFrame[i];
                    }

                    MAXPOINTCOUNT = maxPointsPerFrame;

                    if (useCommandBuffer || forceDepthBufferPass) clearArray = new Vector3[MAXPOINTCOUNT];

                    isInitializingBuffers = true;
                    UnityLibrary.MainThread.Call(InitBuffersForStreaming);
                    while (isInitializingBuffers == true && abortStreamerThread == false)
                    {
                        Thread.Sleep(100);
                    }

                    int headerSize = byteIndex;
                    int currentReadFrame = 0;
                    long currentPosition = byteIndex;

                    // read data loop
                    while (abortStreamerThread == false)
                    {
                        // if queue too full, wait
                        if (abortStreamerThread == false && pointQueueCount >= maxFrameBufferCount)
                        {
                            Thread.Sleep(100);
                            continue;
                        }

                        int numberOfPointsThisFrame = numberOfPointsPerFrame[currentReadFrame];
                        int dataBytesSize = numberOfPointsThisFrame * 4 * 3;

                        // convert single frame, TODO init outside loop, for max amount, then slice? (can use nativearray later)
                        Vector3[] convertedPoints = new Vector3[numberOfPointsThisFrame];
                        GCHandle vectorPointer = GCHandle.Alloc(convertedPoints, GCHandleType.Pinned);
                        IntPtr pV = vectorPointer.AddrOfPinnedObject();
                        Marshal.Copy(binReader.ReadBytes(dataBytesSize), 0, pV, dataBytesSize);
                        byteIndex += dataBytesSize;
                        vectorPointer.Free();

                        // post to queue
                        pointQueue.Enqueue(convertedPoints);
                        if (containsRGB == true)
                        {
                            Vector3[] convertedColors = new Vector3[numberOfPointsThisFrame];
                            GCHandle colorPointer = GCHandle.Alloc(convertedColors, GCHandleType.Pinned);
                            IntPtr pV2 = colorPointer.AddrOfPinnedObject();
                            Marshal.Copy(binReader.ReadBytes(dataBytesSize), 0, pV2, dataBytesSize);
                            byteIndex += dataBytesSize;
                            colorPointer.Free();
                            colorQueue.Enqueue(convertedColors);
                        }
                        pointQueueCount++;
                        currentReadFrame++;

                        // TODO add user rewind/pause

                        // loop from end-of-file
                        if (currentPosition >= fileSize || currentReadFrame >= numberOfFrames)
                        {
                            binReader.BaseStream.Seek(headerSize, 0);
                            currentPosition = headerSize;
                            currentReadFrame = 0;
                        }
                    } // loop data
                } // read file

                Debug.Log("Closing streamer..");
                return;
            }
            else // can read with allbytes
            {
                var data = File.ReadAllBytes(fileName);

                // parse header data
                Int32 byteIndex = 0;
                binaryVersion = data[byteIndex];
                if (binaryVersion != 2 && binaryVersion != 3) { Debug.LogError("For Animated point cloud, header binaryVersion should have value (2 or 3), received=" + binaryVersion); return; }
                byteIndex += sizeof(Byte);
                numberOfFrames = (int)BitConverter.ToInt32(data, byteIndex);
                byteIndex += sizeofInt32;

                frameRate = System.BitConverter.ToSingle(data, byteIndex); // not used
                byteIndex += sizeOfSingle;
                containsRGB = BitConverter.ToBoolean(data, byteIndex);
                byteIndex += sizeOfBool;

                numberOfPointsPerFrame = new int[numberOfFrames];

                if (showDebug) Debug.Log("(ReadAll) Animated file header: numberOfFrames=" + numberOfFrames + " hasRGB=" + containsRGB);

                // get each frame point count info
                for (int i = 0; i < numberOfFrames; i++)
                {
                    numberOfPointsPerFrame[i] = (int)BitConverter.ToInt32(data, byteIndex);
                    byteIndex += sizeofInt32;
                    if (numberOfPointsPerFrame[i] > maxPointsPerFrame) maxPointsPerFrame = numberOfPointsPerFrame[i]; // largest value will be used as a fixed size for point array
                    totalNumberOfPoints += numberOfPointsPerFrame[i];
                }

                if (binaryVersion == 2)
                {
                    // get frame positions
                    frameBinaryPositionForEachFrame = new Int64[numberOfFrames];
                    for (int i = 0; i < numberOfFrames; i++)
                    {
                        frameBinaryPositionForEachFrame[i] = (Int64)BitConverter.ToInt64(data, byteIndex);
                        byteIndex += sizeofInt64;
                    }
                }

                if (showDebug) Debug.Log("totalNumberOfPoints = " + totalNumberOfPoints);
                if (showDebug) Debug.Log("Maximum frame point count: " + maxPointsPerFrame);

                // init playback arrays
                animatedPointArray = new Vector3[totalNumberOfPoints];
                if (containsRGB == true) animatedColorArray = new Vector3[totalNumberOfPoints];

                animatedOffset = new int[numberOfFrames];
                if (containsRGB == true) pointColors = new Vector3[maxPointsPerFrame];

                if (binaryVersion == 2)
                {
                    // parse points from data
                    for (int frame = 0; frame < numberOfFrames; frame++)
                    {
                        animatedOffset[frame] = totalCounter;
                        for (int i = 0; i < numberOfPointsPerFrame[frame]; i++)
                        {
                            tempPoint.x = PointCloudMath.BytesToFloat(data[byteIndex], data[byteIndex + 1], data[byteIndex + 2], data[byteIndex + 3]) + transformPos.x;
                            byteIndex += sizeOfSingle;
                            tempPoint.y = PointCloudMath.BytesToFloat(data[byteIndex], data[byteIndex + 1], data[byteIndex + 2], data[byteIndex + 3]) + transformPos.y;
                            byteIndex += sizeOfSingle;
                            tempPoint.z = PointCloudMath.BytesToFloat(data[byteIndex], data[byteIndex + 1], data[byteIndex + 2], data[byteIndex + 3]) + transformPos.z;
                            byteIndex += sizeOfSingle;
                            animatedPointArray[totalCounter] = tempPoint;
                            if (containsRGB == true)
                            {
                                tempColor.x = PointCloudMath.BytesToFloat(data[byteIndex], data[byteIndex + 1], data[byteIndex + 2], data[byteIndex + 3]);
                                byteIndex += sizeOfSingle;
                                tempColor.y = PointCloudMath.BytesToFloat(data[byteIndex], data[byteIndex + 1], data[byteIndex + 2], data[byteIndex + 3]);
                                byteIndex += sizeOfSingle;
                                tempColor.z = PointCloudMath.BytesToFloat(data[byteIndex], data[byteIndex + 1], data[byteIndex + 2], data[byteIndex + 3]);
                                byteIndex += sizeOfSingle;
                                animatedColorArray[totalCounter] = tempColor;
                            }
                            totalCounter++;
                        }
                    }
                }
                else // animated format 3
                {
                    int pointIndex = 0;
                    int colorIndex = 0;

                    GCHandle vectorPointer = GCHandle.Alloc(animatedPointArray, GCHandleType.Pinned);
                    IntPtr pV = vectorPointer.AddrOfPinnedObject();

                    GCHandle colorPointer = GCHandle.Alloc(animatedColorArray, GCHandleType.Pinned);
                    IntPtr pV2 = colorPointer.AddrOfPinnedObject();

                    for (int frame = 0; frame < numberOfFrames; frame++)
                    {
                        int dataBytesSize = numberOfPointsPerFrame[frame] * 3 * 4;

                        // xyz
                        Marshal.Copy(data, byteIndex, pV + pointIndex, dataBytesSize);
                        byteIndex += dataBytesSize;
                        pointIndex += dataBytesSize;

                        // rgb
                        if (containsRGB == true)
                        {
                            Marshal.Copy(data, byteIndex, pV2 + colorIndex, dataBytesSize);
                            byteIndex += dataBytesSize;
                            colorIndex += dataBytesSize;
                        }

                        animatedOffset[frame] = totalCounter;
                        totalCounter += numberOfPointsPerFrame[frame];
                    }
                    vectorPointer.Free();
                    colorPointer.Free();
                } // v3
            } // allbytes

            // framebuffer is always max point count
            points = new Vector3[maxPointsPerFrame];

            Debug.Log("Finished loading animated point cloud. total points = " + PointCloudTools.HumanReadableCount(totalCounter));

            totalMaxPoints = maxPointsPerFrame;

            isInitializingBuffers = true;
            UnityLibrary.MainThread.Call(InitDX11Buffers);
            while (isInitializingBuffers == true && abortStreamerThread == false)
            {
                Thread.Sleep(100);
            }

            isLoading = false;
            UnityLibrary.MainThread.Call(OnLoadingCompleteCallBack, fileName);

            stopwatch.Stop();
            if (showDebug) Debug.Log("Loading time: " + stopwatch.ElapsedMilliseconds + "ms");
            stopwatch.Reset();
        } // ReadAnimatedPointcloud


        void InitBuffersForStreaming()
        {
            //if (currentFramePointCount < 1) return;
            //if (useMeshRendering == true) return;

            if (bufferPoints != null) bufferPoints.Dispose();
            bufferPoints = new ComputeBuffer(MAXPOINTCOUNT, 12);
            if (bufferColors != null) bufferColors.Dispose();
            bufferColors = new ComputeBuffer(MAXPOINTCOUNT, 12);

            if (useCommandBuffer == true)
            {
                commandBuffer.DrawProcedural(Matrix4x4.identity, cloudMaterial, 0, MeshTopology.Points, MAXPOINTCOUNT, 1);
            }

            if (forceDepthBufferPass == true)
            {
                commandBufferDepth.DrawProcedural(Matrix4x4.identity, cloudMaterial, 0, MeshTopology.Points, MAXPOINTCOUNT, 1);
            }
            isInitializingBuffers = false;
        }

        public void InitDX11Buffers()
        {
            // cannot init 0 size, so create dummy data if its 0, TODO is this needed?
            //if (maxPointsPerFrame == 0)
            //{
            //    maxPointsPerFrame = 1;
            //    points = new Vector3[1];
            //    if (containsRGB == true)
            //    {
            //        pointColors = new Vector3[1];
            //    }
            //}

            // clear old buffers
            ReleaseDX11Buffers();

            if (bufferPoints != null) bufferPoints.Dispose();

            bufferPoints = new ComputeBuffer(maxPointsPerFrame, 12);
            bufferPoints.SetData(points);
            cloudMaterial.SetBuffer("buf_Points", bufferPoints);
            if (containsRGB == true)
            {
                if (bufferColors != null) bufferColors.Dispose();
                bufferColors = new ComputeBuffer(maxPointsPerFrame, 12);
                bufferColors.SetData(pointColors);
                cloudMaterial.SetBuffer("buf_Colors", bufferColors);
            }

            if (forceDepthBufferPass == true)
            {
                depthMaterial.SetBuffer("buf_Points", bufferPoints);
            }
            isInitializingBuffers = false;
        }

        public void ReleaseDX11Buffers()
        {
            if (bufferPoints != null) bufferPoints.Release();
            bufferPoints = null;
            if (bufferColors != null) bufferColors.Release();
            bufferColors = null;
        }

        // can use this to set new points data
        public void UpdatePointData()
        {
            if (points.Length == bufferPoints.count)
            {
                // same length as earlier
                bufferPoints.SetData(points);
                cloudMaterial.SetBuffer("buf_Points", bufferPoints);
            }
            else
            {
                // new data is different sized array, need to redo it
                maxPointsPerFrame = points.Length;
                totalMaxPoints = maxPointsPerFrame;
                bufferPoints.Dispose();
                bufferPoints = new ComputeBuffer(maxPointsPerFrame, 12);
                bufferPoints.SetData(points);
                cloudMaterial.SetBuffer("buf_Points", bufferPoints);
            }
        }


        // can use this to set new point colors data
        public void UpdateColorData()
        {
            if (pointColors.Length == bufferColors.count)
            {
                // same length as earlier
                bufferColors.SetData(pointColors);
                cloudMaterial.SetBuffer("buf_Colors", bufferColors);
            }
            else
            {
                // new data is different sized array, need to redo it
                maxPointsPerFrame = pointColors.Length;
                totalMaxPoints = maxPointsPerFrame;
                bufferColors.Dispose();
                bufferColors = new ComputeBuffer(maxPointsPerFrame, 12);
                bufferColors.SetData(pointColors);
                cloudMaterial.SetBuffer("buf_Colors", bufferColors);
            }
        }

        void OnDestroy()
        {
            abortStreamerThread = true;
            if (fileStreamerThread != null)
            {
                fileStreamerThread.Join(); // to avoid exception
            }

            ReleaseDX11Buffers();
            points = new Vector3[0];
            pointColors = new Vector3[0];
        }


        // drawing mainloop, for drawing the points
        //void OnPostRender() // < works also, BUT must have this script attached to Camera
        public void OnRenderObject()
        {
            // optional: if you only want to render to specific camera, use next line
            if (renderOnlyMainCam == true && Camera.current.CompareTag("MainCamera") == false) return;

            // dont display while loading, it slows down with huge clouds
            if (isLoading == true || displayPoints == false || useCommandBuffer == true) return;

            cloudMaterial.SetPass(0);

#if UNITY_2019_1_OR_NEWER
            Graphics.DrawProceduralNow(MeshTopology.Points, currentFramePointCount);
#else
            Graphics.DrawProcedural(MeshTopology.Points, currentFramePointCount);
            //Debug.Log("render " + currentFrame + " currentFramePointCount=" + currentFramePointCount);
#endif
        }


        // called after some file load operation has finished
        void OnLoadingCompleteCallBack(System.Object a)
        {
            //PointCloudTools.DrawBounds(GetBounds(), 99);

            if (OnLoadingComplete != null) OnLoadingComplete((string)a);

            if (useLargeFileStreaming == false)
            {

                if (useCommandBuffer == true)
                {
                    commandBuffer.DrawProcedural(Matrix4x4identity, cloudMaterial, 0, MeshTopology.Points, maxPointsPerFrame, 0);
                }

                if (forceDepthBufferPass == true)
                {
                    commandBufferDepth.DrawProcedural(Matrix4x4identity, depthMaterial, 0, MeshTopology.Points, maxPointsPerFrame, 0);
                }
            }

            isReady = true;
        }

        // can try enabling this, if your cloud disappears on alt tab
        //void OnApplicationFocus(bool focused)
        //{
        //    Debug.Log("focus = "+focused);
        //    if (focused) InitDX11Buffers();
        //}

        // -------------------------- POINT CLOUD HELPER METHODS --------------------------------
        // returns current frame point count, or -1 if points array is null
        public int GetPointCount()
        {
            if (points == null) return -1;
            return points.Length;
        }

        // returns given point position from array
        public Vector3 GetPointPosition(int index)
        {
            if (points == null || index < 0 || index > points.Length - 1) return Vector3.zero;
            return points[index];
        }

        // set material/shader pointsize
        public void SetPointSize(float newSize)
        {
            cloudMaterial.SetFloat("_Size", newSize);
        }

        // enable/disable drawing
        public void ToggleCloud(bool state)
        {
            displayPoints = state;
        }

        // return current material shader _Size variable
        public float? GetPointSize()
        {
            if (cloudMaterial.HasProperty("_Size") == false) return null;
            return cloudMaterial.GetFloat("_Size");
        }

        public int GetTotalPointCount()
        {
            return totalNumberOfPoints;
        }

        public void FixMainThreadHelper()
        {
            if (GameObject.Find("#MainThreadHelper") == null || UnityLibrary.MainThread.instanceCount == 0)
            {
                var go = new GameObject();
                go.name = "#MainThreadHelper";
                go.AddComponent<UnityLibrary.MainThread>();
            }
        }

        public bool IsLoading()
        {
            return isLoading;
        }

        public bool IsReady()
        {
            return isReady;
        }

    } // class
} // namespace

#endif