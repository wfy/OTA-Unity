// Point Cloud Binary Viewer DX11 for runtime parsing
// http://unitycoder.com

using UnityEngine;
using System.Collections;
using unitycodercom_PointCloudHelpers;
using Debug = UnityEngine.Debug;
using PointCloudHelpers;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;
using System;
using UnityLibrary;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.InteropServices;
#if !UNITY_SAMSUNGTV && !UNITY_WEBGL
using System.Threading;
using System.IO;


namespace PointCloudRuntimeViewer
{
    // supported formats
    public enum PointCloudFormat
    {
        USE_FILE_EXTENSION,
        XYZ,
        XYZRGB,
        CGO,
        ASC,
        CATIA_ASC,
        PLY_ASCII,
        LAS,
        PTS,
        PCD_ASCII,
    }

    public class RuntimeViewerDX11 : MonoBehaviour
    {
#if !UNITY_SAMSUNGTV && !UNITY_WEBGL

        [Header("Settings")]
        public string fullPath = "raw.xyz";
        public bool loadAtStart = false;
        public Material cloudMaterial;
        [Tooltip("If disabled, uses mesh rendering")]
        public bool useDX11 = true;
        [Tooltip("Mesh rendering requires different material, those in Material/Mesh folder")]
        public Material meshMaterial;

        public enum RenderMode { Point, Mesh }
        [Header("Dual Mode Rendering")]
        public RenderMode currentRenderMode = RenderMode.Point;
        private MeshFilter _mf;
        private MeshRenderer _mr;

        [Tooltip("Create copy of the material. Must enable if viewing multiple clouds with same materials")]
        public bool instantiateMaterial = false; // set True if using multiple viewers
        private int totalPoints = 0;
        public int TotalPointCount => totalPoints;
        [HideInInspector]
        public int totalMaxPoints = 0; // new variable to keep total maximum point count
        private ComputeBuffer bufferPoints;
        private ComputeBuffer bufferColors;
        public Vector3[] points;
        [HideInInspector]
        public Vector4[] pointColors;
        IntPtr pointsBuffer;
        IntPtr pointColorsBuffer;
        private Vector3 dataColor;
        private float r, g, b;

        private bool isLoading = true;
        private bool drawFirstFrameForced = false; // used with showFirstFrameWhenAvailable
        private bool haveError = false;

        // runtime reader
        [Tooltip("0=XYZ, 1=XYZRGB, 2=CGO, 3=ASC, 4=CATIA ASC, 5=PLY (ASCII), 6=LAS, 7=PTS, 8=PCD (ASCII)")]
        public PointCloudFormat pointCloudFormat = PointCloudFormat.USE_FILE_EXTENSION;
        public bool readRGB = false;
        public bool readIntensity = false; // only for PTS currently

        [Header("Visibility")]
        public bool displayPoints = true;
        [Tooltip("Enable this if you have multiple cameras and only want to draw in MainCamera")]
        public bool renderOnlyMainCam = false;

        public Bounds cloudBounds;

        [Header("Rendering")]
        [Tooltip("Draw using CommandBuffer instead of OnRenderObject")]
        public bool useCommandBuffer = false;

        [Tooltip("Default value: AfterForwardOpaque")]
        public CameraEvent camDrawPass = CameraEvent.AfterForwardOpaque;
        CommandBuffer commandBuffer;
        public bool forceDepthBufferPass = true;
        public Material depthMaterial;
        [Tooltip("Changing CameraEvent takes effect only at Start(). Default value: AfterDepthTexture")]
        public CameraEvent camDepthPass = CameraEvent.AfterDepthTexture;
        CommandBuffer commandBufferDepth;

        [Header("Optional")]
        [Tooltip("Old brute-force method (to be decprecated)")]
        public bool enablePicking = false;

        public delegate void PointSelected(Vector3 pointPos);
        public event PointSelected PointWasSelected;
        // how many points are checked for measurement per frame (larger values will hang mainthread longer, too low values cause measuring to take very long time)
        int maxIterationsPerFrame = 100000;//256000;
        bool isSearchingPoint = false;
        public delegate void PointUnSelected();
        public event PointUnSelected PointWasUnSelected;

        public delegate void OnLoadComplete(string filename);
        public event OnLoadComplete OnLoadingComplete;

        //		private bool readNormals = false;
        public bool useUnitScale = false;
        public float unitScale = 0.001f;
        public bool flipYZ = true;
        public bool autoOffsetNearZero = true; // takes first point value as offset
        public bool useManualOffset = false;
        public Vector3 manualOffset = Vector3.zero;

        internal Vector4 Color(int i)
        {
            if (i > pointColors.Length)
                return Vector4.zero;

            return pointColors[i];
        }

        public bool plyHasNormals = false;
        private bool plyHasDensity = false;

        bool hasLoadedPointCloud = false;
        private long masterPointCount = 0;

        public bool showDebug = false;

        float[] LUT255 = new float[] { 0f, 0.00392156862745098f, 0.00784313725490196f, 0.011764705882352941f, 0.01568627450980392f, 0.0196078431372549f, 0.023529411764705882f, 0.027450980392156862f, 0.03137254901960784f, 0.03529411764705882f, 0.0392156862745098f, 0.043137254901960784f, 0.047058823529411764f, 0.050980392156862744f, 0.054901960784313725f, 0.058823529411764705f, 0.06274509803921569f, 0.06666666666666667f, 0.07058823529411765f, 0.07450980392156863f, 0.0784313725490196f, 0.08235294117647059f, 0.08627450980392157f, 0.09019607843137255f, 0.09411764705882353f, 0.09803921568627451f, 0.10196078431372549f, 0.10588235294117647f, 0.10980392156862745f, 0.11372549019607843f, 0.11764705882352941f, 0.12156862745098039f, 0.12549019607843137f, 0.12941176470588237f, 0.13333333333333333f, 0.13725490196078433f, 0.1411764705882353f, 0.1450980392156863f, 0.14901960784313725f, 0.15294117647058825f, 0.1568627450980392f, 0.1607843137254902f, 0.16470588235294117f, 0.16862745098039217f, 0.17254901960784313f, 0.17647058823529413f, 0.1803921568627451f, 0.1843137254901961f, 0.18823529411764706f, 0.19215686274509805f, 0.19607843137254902f, 0.2f, 0.20392156862745098f, 0.20784313725490197f, 0.21176470588235294f, 0.21568627450980393f, 0.2196078431372549f, 0.2235294117647059f, 0.22745098039215686f, 0.23137254901960785f, 0.23529411764705882f, 0.23921568627450981f, 0.24313725490196078f, 0.24705882352941178f, 0.25098039215686274f, 0.2549019607843137f, 0.25882352941176473f, 0.2627450980392157f, 0.26666666666666666f, 0.27058823529411763f, 0.27450980392156865f, 0.2784313725490196f, 0.2823529411764706f, 0.28627450980392155f, 0.2901960784313726f, 0.29411764705882354f, 0.2980392156862745f, 0.30196078431372547f, 0.3058823529411765f, 0.30980392156862746f, 0.3137254901960784f, 0.3176470588235294f, 0.3215686274509804f, 0.3254901960784314f, 0.32941176470588235f, 0.3333333333333333f, 0.33725490196078434f, 0.3411764705882353f, 0.34509803921568627f, 0.34901960784313724f, 0.35294117647058826f, 0.3568627450980392f, 0.3607843137254902f, 0.36470588235294116f, 0.3686274509803922f, 0.37254901960784315f, 0.3764705882352941f, 0.3803921568627451f, 0.3843137254901961f, 0.38823529411764707f, 0.39215686274509803f, 0.396078431372549f, 0.4f, 0.403921568627451f, 0.40784313725490196f, 0.4117647058823529f, 0.41568627450980394f, 0.4196078431372549f, 0.4235294117647059f, 0.42745098039215684f, 0.43137254901960786f, 0.43529411764705883f, 0.4392156862745098f, 0.44313725490196076f, 0.4470588235294118f, 0.45098039215686275f, 0.4549019607843137f, 0.4588235294117647f, 0.4627450980392157f, 0.4666666666666667f, 0.47058823529411764f, 0.4745098039215686f, 0.47843137254901963f, 0.4823529411764706f, 0.48627450980392156f, 0.49019607843137253f, 0.49411764705882355f, 0.4980392156862745f, 0.5019607843137255f, 0.5058823529411764f, 0.5098039215686274f, 0.5137254901960784f, 0.5176470588235295f, 0.5215686274509804f, 0.5254901960784314f, 0.5294117647058824f, 0.5333333333333333f, 0.5372549019607843f, 0.5411764705882353f, 0.5450980392156862f, 0.5490196078431373f, 0.5529411764705883f, 0.5568627450980392f, 0.5607843137254902f, 0.5647058823529412f, 0.5686274509803921f, 0.5725490196078431f, 0.5764705882352941f, 0.5803921568627451f, 0.5843137254901961f, 0.5882352941176471f, 0.592156862745098f, 0.596078431372549f, 0.6f, 0.6039215686274509f, 0.6078431372549019f, 0.611764705882353f, 0.615686274509804f, 0.6196078431372549f, 0.6235294117647059f, 0.6274509803921569f, 0.6313725490196078f, 0.6352941176470588f, 0.6392156862745098f, 0.6431372549019608f, 0.6470588235294118f, 0.6509803921568628f, 0.6549019607843137f, 0.6588235294117647f, 0.6627450980392157f, 0.6666666666666666f, 0.6705882352941176f, 0.6745098039215687f, 0.6784313725490196f, 0.6823529411764706f, 0.6862745098039216f, 0.6901960784313725f, 0.6941176470588235f, 0.6980392156862745f, 0.7019607843137254f, 0.7058823529411765f, 0.7098039215686275f, 0.7137254901960784f, 0.7176470588235294f, 0.7215686274509804f, 0.7254901960784313f, 0.7294117647058823f, 0.7333333333333333f, 0.7372549019607844f, 0.7411764705882353f, 0.7450980392156863f, 0.7490196078431373f, 0.7529411764705882f, 0.7568627450980392f, 0.7607843137254902f, 0.7647058823529411f, 0.7686274509803922f, 0.7725490196078432f, 0.7764705882352941f, 0.7803921568627451f, 0.7843137254901961f, 0.788235294117647f, 0.792156862745098f, 0.796078431372549f, 0.8f, 0.803921568627451f, 0.807843137254902f, 0.8117647058823529f, 0.8156862745098039f, 0.8196078431372549f, 0.8235294117647058f, 0.8274509803921568f, 0.8313725490196079f, 0.8352941176470589f, 0.8392156862745098f, 0.8431372549019608f, 0.8470588235294118f, 0.8509803921568627f, 0.8549019607843137f, 0.8588235294117647f, 0.8627450980392157f, 0.8666666666666667f, 0.8705882352941177f, 0.8745098039215686f, 0.8784313725490196f, 0.8823529411764706f, 0.8862745098039215f, 0.8901960784313725f, 0.8941176470588236f, 0.8980392156862745f, 0.9019607843137255f, 0.9058823529411765f, 0.9098039215686274f, 0.9137254901960784f, 0.9176470588235294f, 0.9215686274509803f, 0.9254901960784314f, 0.9294117647058824f, 0.9333333333333333f, 0.9372549019607843f, 0.9411764705882353f, 0.9450980392156862f, 0.9490196078431372f, 0.9529411764705882f, 0.9568627450980393f, 0.9607843137254902f, 0.9647058823529412f, 0.9686274509803922f, 0.9725490196078431f, 0.9764705882352941f, 0.9803921568627451f, 0.984313725490196f, 0.9882352941176471f, 0.9921568627450981f, 0.996078431372549f, 1f };

        string applicationStreamingAssetsPath;

        Vector3 tempPoint;
        Vector3 tempColor;
        public Camera cam;

        [Header("Caching")]
        [Tooltip("Output .bin file, so can load it with PointCloudViewerDX11, instead of parsing raw pointcloud again")]
        public bool cacheBinFile = false;
        [Tooltip("If cache file exists, don't save again")]
        public bool overrideExistingCacheFile = false;

        public bool needCollsion = false;
        public bool needRecalculateCollision = true;
        private PointsCloundDivider pcDivider;

        public Vector3 Point(int i) { return points[i]; }

        private void Awake()
        {
            applicationStreamingAssetsPath = Application.streamingAssetsPath;
            pcDivider = GetComponent<PointsCloundDivider>();
            if (pcDivider == null) pcDivider = gameObject.AddComponent<PointsCloundDivider>();

            _mf = GetComponent<MeshFilter>();
            if (_mf == null) _mf = gameObject.AddComponent<MeshFilter>();

            _mr = GetComponent<MeshRenderer>();
            if (_mr == null) _mr = gameObject.AddComponent<MeshRenderer>();

            if (GetComponent<OTA.PerfDiag>() == null) gameObject.AddComponent<OTA.PerfDiag>();
        }

        public void SetCamera(Camera c)
        {
            cam = c;
        }

        // init
        private IEnumerator Start()
        {
            UpdateOutlineState(false);

            if (instantiateMaterial == true)
            {
                cloudMaterial = new Material(cloudMaterial);
            }

            // check if MainThread script exists in scene, its required only for threading though
            if (GameObject.Find("#MainThreadHelper") == null)
            {
                var go = new GameObject("#MainThreadHelper");
                go.AddComponent<UnityLibrary.MainThread>();
            }


            if (useCommandBuffer == true)
            {
                commandBuffer = new CommandBuffer();
                cam.AddCommandBuffer(camDrawPass, commandBuffer);
            }

            //if (forceDepthBufferPass == true)
            //{
            //    //depthMaterial = cloudMaterial;
            //    commandBufferDepth = new CommandBuffer();
            //    cam.AddCommandBuffer(camDepthPass, commandBufferDepth);
            //}

            if (loadAtStart == true)
            {
                // allow app to start first
                yield return new WaitForSecondsRealtime(1);

                try
                {
                    CallImporterThreaded(fullPath);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

            }

            yield return null;
        }

        void Update()
        {
            if (isLoading == true || haveError == true) return;
            // experimentel point picking, to be removed
            //if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (enablePicking) SelectClosestPoint();
        }

        bool abortReaderThread = false;
        Thread importerThread;

        public void CallImporterThreaded(string fullPath)
        {
            // if not full path, try streaming assets
            if (Path.IsPathRooted(fullPath) == false)
            {
                fullPath = Path.Combine(Application.streamingAssetsPath, fullPath);
            }
            if (File.Exists(fullPath) == false)
            {
                Debug.LogError("File not found: " + fullPath);
                return;
            }

            // Debug.Log("Reading threaded pointcloud file: " + fullPath, gameObject);

            if (Path.GetExtension(fullPath) == ".bin")
            {
                ParameterizedThreadStart start = new ParameterizedThreadStart(ReadBinaryPointCloudThreaded);
                importerThread = new Thread(start);
                importerThread.IsBackground = true;
                importerThread.Start(fullPath);
            }
            else
            {
                ParameterizedThreadStart start = new ParameterizedThreadStart(LoadRawPointCloud);
                importerThread = new Thread(start);
                importerThread.IsBackground = true;
                importerThread.Start(fullPath); // TODO use normal thread, not params
            }
        }
        
        public delegate void InitCollision(Vector3[] points);
        public InitCollision OnInitCollision;


        // binary point cloud reader (using separate thread)
        public unsafe void ReadBinaryPointCloudThreaded(System.Object a)
        {
            string fileName = (string)a;
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            // for testing loading times
            if (showDebug)
            {
                stopwatch.Start();
            }

            isLoading = true;


            byte[] data;
            //IntPtr buffer;

            try
            {
                data = File.ReadAllBytes(fileName);
            }
            catch
            {
                Debug.LogError(fileName + " cannot be opened with ReadAllBytes(), it might be too large >2gb. Try splitting your data into smaller parts (using external point cloud editing tools)");

                /*
                // try reading in smaller parts
                long fileSize = new System.IO.FileInfo(fileName).Length;

                try
                {
                    data = new byte[fileSize];
                }
                catch (System.Exception)
                {

                }
                finally
                {
                    Debug.LogError("File is too large, cannot create array: " + fileSize);
                }

                FileStream sourceFile = new FileStream(fileName, FileMode.Open);
                BinaryReader reader = new BinaryReader(sourceFile);

                reader.Close();
                sourceFile.Close();
               */

                return;
            }

            int sizeofInt32 = sizeof(int);

            System.Int32 byteIndex = 0;

            int binaryVersion = data[byteIndex];
            byteIndex += sizeof(System.Byte);

            // check format
            if (binaryVersion > 1)
            {
                Debug.LogError("File binaryVersion should have value (0-1). Was " + binaryVersion + " - Loading cancelled. " + ((binaryVersion == 2) ? "(2 is Animated Point Cloud, use BrekelViewer for that)" : ""));
                return;
            }

            totalPoints = (int)System.BitConverter.ToInt32(data, byteIndex);
            byteIndex += sizeofInt32;
            // Debug.Log(totalPoints);

            readRGB = System.BitConverter.ToBoolean(data, byteIndex);
            byteIndex += sizeof(System.Boolean);
            
            points = new Vector3[totalPoints];

            // Debug.Log("Loading old format: " + totalPoints + " points..");

            float x, y, z;
            float minX = Mathf.Infinity;
            float minY = Mathf.Infinity;
            float minZ = Mathf.Infinity;
            float maxX = Mathf.NegativeInfinity;
            float maxY = Mathf.NegativeInfinity;
            float maxZ = Mathf.NegativeInfinity;

            if (readRGB == true) pointColors = new Vector4[totalPoints];


            IntPtr byteArrayToFloats = Marshal.AllocHGlobal(sizeof(float) * (data.Length - byteIndex) / 4);
            Marshal.Copy(data, byteIndex, byteArrayToFloats, data.Length - byteIndex);
            //byteArrayToFloats = new float[(data.Length - byteIndex) / 4];
            //System.Buffer.BlockCopy(data, byteIndex, byteArrayToFloats, 0, data.Length - byteIndex);

            int dataIndex = 0;
            for (int i = 0; i < totalPoints; i++)
            {
                x = Marshal.PtrToStructure<float>(byteArrayToFloats + dataIndex * sizeof(float));
                dataIndex++;
                y = Marshal.PtrToStructure<float>(byteArrayToFloats + dataIndex * sizeof(float));
                dataIndex++;
                z = Marshal.PtrToStructure<float>(byteArrayToFloats + dataIndex * sizeof(float));
                dataIndex++;

                //x = byteArrayToFloats[dataIndex];
                //dataIndex++;
                //y = byteArrayToFloats[dataIndex];
                //dataIndex++;
                //z = byteArrayToFloats[dataIndex];
                //dataIndex++;

                if (flipYZ)
                {
                    var t = y;
                    y = z;
                    z = t;
                }

                points[i].x = x;
                points[i].y = y;
                points[i].z = z;
                // get bounds
                if (x < minX) minX = x;
                else if (x > maxX) maxX = x;
                //((x < minX) ? ref minX : ref maxX) = x; // c#7
                if (y < minY) minY = y;
                else if (y > maxY) maxY = y;
                if (z < minZ) minZ = z;
                else if (z > maxZ) maxZ = z;

                if (readRGB == true)
                {
                    r = Marshal.PtrToStructure<float>(byteArrayToFloats + dataIndex * sizeof(float));
                    dataIndex++;
                    g = Marshal.PtrToStructure<float>(byteArrayToFloats + dataIndex * sizeof(float));
                    dataIndex++;
                    b = Marshal.PtrToStructure<float>(byteArrayToFloats + dataIndex * sizeof(float));
                    dataIndex++;

                    //r = byteArrayToFloats[dataIndex];
                    //dataIndex++;
                    //g = byteArrayToFloats[dataIndex];
                    //dataIndex++;
                    //b = byteArrayToFloats[dataIndex];
                    //dataIndex++;

                    if (r == 0 && g == 0 && b == 0)
                        pointColors[i] = Vector4.one;
                    else
                    {
                        pointColors[i].x = r;
                        pointColors[i].y = g;
                        pointColors[i].z = b;
                        pointColors[i].w = 1;
                    }
                }

                if (abortReaderThread == true)
                {
                    return;
                }
            } // for all points

            //pointsBuffer = Marshal.AllocHGlobal(sizeof(float) * 3 * points.Length);
            //Marshal.Copy(points, 0, buffer, sizeof(float) * 3 * points.Length);
            //pointColorsBuffer = Marshal.AllocHGlobal(sizeof(float) * 4 * pointColors.Length);
            //Marshal.Copy(pointColors, 0, buffer, sizeof(float) * 4 * points.Length);

            Marshal.FreeHGlobal(byteArrayToFloats);
            //Marshal.FreeHGlobal(buffer);
            data = null;
            GC.Collect();
            // for testing load timer
            //            stopwatch.Stop();
            //            Debug.Log("Timer: " + stopwatch.ElapsedMilliseconds + "ms");
            //            stopwatch.Reset();

            cloudBounds = new Bounds(new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f), new Vector3((maxX - minX), (maxY - minY), (maxZ - minZ)));

            totalMaxPoints = totalPoints;


            // refresh buffers
            UnityLibrary.MainThread.Call(InitDX11Buffers);
            if (showDebug == true)
            {
                stopwatch.Stop();
                // Debug.Log("Loading finish: " + stopwatch.ElapsedMilliseconds + " ms");
                stopwatch.Reset();
                stopwatch.Start();
            }

            isLoading = false;
            UnityLibrary.MainThread.Call(OnLoadingCompleteCallBack, fileName);

            hasLoadedPointCloud = true;

            if (needRecalculateCollision)
            {
                needRecalculateCollision = false;
                OnInitCollision?.Invoke(points);
            }

            if (showDebug == true)
            {
                stopwatch.Stop();
                Debug.Log("Init Collision finish: " + stopwatch.ElapsedMilliseconds + " ms");
                stopwatch.Reset();
            }
        } // ReadPointCloudThreaded

        // raw point cloud reader
        public void LoadRawPointCloud(System.Object a)
        {
            // cleanup old buffers
            if (useDX11 == true) ReleaseDX11Buffers();

            fullPath = (string)a;

            // if not full path, try streaming assets
            if (Path.IsPathRooted(fullPath) == false)
            {
                fullPath = Path.Combine(applicationStreamingAssetsPath, fullPath);
            }

            if (PointCloudTools.CheckIfFileExists(fullPath) == false)
            {
                Debug.LogError("File not found:" + fullPath);
                return;
            }

            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            // check if automatic fileformat, get extension
            if (pointCloudFormat == PointCloudFormat.USE_FILE_EXTENSION)
            {
                var extension = Path.GetExtension(fullPath).ToUpper();
                switch (extension)
                {
                    case ".ASC": pointCloudFormat = PointCloudFormat.ASC; break;
                    case ".CATIA_ASC": pointCloudFormat = PointCloudFormat.CATIA_ASC; break;
                    case ".CGO": pointCloudFormat = PointCloudFormat.CGO; break;
                    case ".LAS": pointCloudFormat = PointCloudFormat.LAS; break;
                    case ".XYZ": pointCloudFormat = PointCloudFormat.XYZ; break;
                    case ".PCD": pointCloudFormat = PointCloudFormat.PCD_ASCII; break;
                    case ".PLY": pointCloudFormat = PointCloudFormat.PLY_ASCII; break;
                    case ".PTS": pointCloudFormat = PointCloudFormat.PTS; break;
                    case ".XYZRGB": pointCloudFormat = PointCloudFormat.XYZRGB; break;
                    default:
                        LogMessage("Unknown file extension: " + extension + ", trying to import as XYZRGB..");
                        pointCloudFormat = PointCloudFormat.XYZRGB;
                        break;
                }
            }

            // Custom reader for LAS binary
            if (pointCloudFormat == PointCloudFormat.LAS)
            {
                //LASDataConvert();
                LogMessage("LAS format is not yet supported in runtime importer:" + fullPath);
                return;
            }

            isLoading = true;
            hasLoadedPointCloud = false;

            LogMessage("Loading " + pointCloudFormat + " file: " + fullPath);

            long lines = 0;

            // get initial data (so can check if data is ok)
            using (StreamReader streamReader = new StreamReader(File.OpenRead(fullPath)))
            {
                double x = 0, y = 0, z = 0;
                float r = 0, g = 0, b = 0; //,nx=0,ny=0,nz=0;; // init vals
                string line = null;
                string[] row = null;

                PeekHeaderData headerCheck;
                headerCheck.x = 0; headerCheck.y = 0; headerCheck.z = 0;
                headerCheck.linesRead = 0;

                switch (pointCloudFormat)
                {
                    case PointCloudFormat.ASC: // ASC (space at front)
                        {
                            headerCheck = PeekHeader.PeekHeaderASC(streamReader, readRGB);
                            if (!headerCheck.readSuccess) { streamReader.Close(); return; }
                            lines = headerCheck.linesRead;
                        }
                        break;

                    case PointCloudFormat.CGO: // CGO	(counter at first line and uses comma)
                        {
                            headerCheck = PeekHeader.PeekHeaderCGO(streamReader, readRGB);
                            if (!headerCheck.readSuccess) { streamReader.Close(); return; }
                            lines = headerCheck.linesRead;
                        }
                        break;

                    case PointCloudFormat.CATIA_ASC: // CATIA ASC (with header and Point Format           = 'X %f Y %f Z %f')
                        {
                            headerCheck = PeekHeader.PeekHeaderCATIA_ASC(streamReader, ref readRGB);
                            if (!headerCheck.readSuccess) { streamReader.Close(); return; }
                            lines = headerCheck.linesRead;
                        }
                        break;

                    case PointCloudFormat.XYZRGB:
                    case PointCloudFormat.XYZ: // XYZ RGB(INT)
                        {
                            headerCheck = PeekHeader.PeekHeaderXYZ(streamReader, ref readRGB);
                            if (!headerCheck.readSuccess) { streamReader.Close(); return; }
                            lines = headerCheck.linesRead;
                        }
                        break;

                    case PointCloudFormat.PTS: // PTS (INT) (RGB)
                        {
                            headerCheck = PeekHeader.PeekHeaderPTS(streamReader, readRGB, readIntensity, ref masterPointCount);
                            if (!headerCheck.readSuccess) { streamReader.Close(); return; }
                            lines = headerCheck.linesRead;
                        }
                        break;

                    case PointCloudFormat.PLY_ASCII: // PLY (ASCII)
                        {
                            headerCheck = PeekHeader.PeekHeaderPLY(streamReader, readRGB, ref masterPointCount, ref plyHasNormals, ref plyHasDensity);
                            if (!headerCheck.readSuccess) { streamReader.Close(); return; }
                        }
                        break;

                    case PointCloudFormat.PCD_ASCII: // PCD (ASCII)
                        {
                            headerCheck = PeekHeader.PeekHeaderPCD(streamReader, ref readRGB, ref masterPointCount);
                            if (headerCheck.readSuccess == false) { streamReader.Close(); return; }
                        }
                        break;
                    default:
                        Debug.LogError("> Unknown fileformat error (1) " + pointCloudFormat);
                        break;

                } // switch format


                if (autoOffsetNearZero == true)
                {
                    manualOffset = new Vector3((float)headerCheck.x, (float)headerCheck.y, (float)headerCheck.z);
                }

                // scaling enabled, scale offset too
                if (useUnitScale == true) manualOffset *= unitScale;

                // progressbar
                long progressCounter = 0;

                // get total amount of points
                if (pointCloudFormat == PointCloudFormat.PLY_ASCII || pointCloudFormat == PointCloudFormat.PTS || pointCloudFormat == PointCloudFormat.CGO || pointCloudFormat == PointCloudFormat.PCD_ASCII)
                {
                    lines = masterPointCount;

                    // reset back to start of file
                    streamReader.DiscardBufferedData();
                    streamReader.BaseStream.Seek(0, SeekOrigin.Begin);
                    streamReader.BaseStream.Position = 0;

                    // get back to before first actual data line
                    for (int i = 0; i < headerCheck.linesRead - 1; i++)
                    {
                        streamReader.ReadLine();
                    }

                }
                else
                { // other formats need to be read completely

                    // reset back to start of file
                    streamReader.DiscardBufferedData();
                    streamReader.BaseStream.Seek(0, SeekOrigin.Begin);
                    streamReader.BaseStream.Position = 0;

                    // get back to first actual data line
                    for (int i = 0; i < headerCheck.linesRead; i++)
                    {
                        streamReader.ReadLine();
                    }
                    lines = 0;

                    // calculate actual point data lines
                    int splitCount = 0;
                    while (streamReader.EndOfStream == false && abortReaderThread == false)
                    {
                        line = streamReader.ReadLine();

                        if (progressCounter > 256000)
                        {
                            progressCounter = 0;
                        }

                        progressCounter++;

                        if (line.Length > 9)
                        {
                            splitCount = CharCount(line, ' ');
                            if (splitCount > 2 && splitCount < 16)
                            {
                                lines++;
                            }
                        }
                    }


                    // reset back to start of data
                    streamReader.DiscardBufferedData();
                    streamReader.BaseStream.Seek(0, SeekOrigin.Begin);
                    streamReader.BaseStream.Position = 0;

                    // now skip header lines
                    for (int i = 0; i < headerCheck.linesRead; i++)
                    {
                        streamReader.ReadLine();
                    }

                    masterPointCount = lines;
                }

                // create buffers
                points = new Vector3[masterPointCount];

                if (readRGB == true || readIntensity == true)
                {
                    pointColors = new Vector4[masterPointCount];
                }

                totalPoints = (int)masterPointCount;

                progressCounter = 0;

                int skippedRows = 0;
                long rowCount = 0;
                bool haveMoreToRead = true;

                // process all points
                while (haveMoreToRead == true && abortReaderThread == false)
                {
                    if (progressCounter > 256000)
                    {
                        // TODO: add runtime progressbar
                        //EditorUtility.DisplayProgressBar(appName, "Converting point cloud to binary file", rowCount / (float)lines);
                        progressCounter = 0;
                    }

                    progressCounter++;

                    line = streamReader.ReadLine();

                    if (line != null)// && line.Length > 9)
                    {
                        // trim duplicate spaces
                        line = line.Replace("   ", " ").Replace("  ", " ").Trim();
                        row = line.Split(' ');

                        if (row.Length > 2)
                        {
                            switch (pointCloudFormat)
                            {
                                case PointCloudFormat.ASC: // ASC
                                    if (line.IndexOf('!') == 0 || line.IndexOf('*') == 0)
                                    {
                                        skippedRows++;
                                        continue;
                                    }
                                    x = double.Parse(row[0], CultureInfo.InvariantCulture);
                                    y = double.Parse(row[1], CultureInfo.InvariantCulture);
                                    z = double.Parse(row[2], CultureInfo.InvariantCulture);
                                    break;

                                case PointCloudFormat.CGO: // CGO	(counter at first line and uses comma)
                                    if (line.IndexOf('!') == 0 || line.IndexOf('*') == 0)
                                    {
                                        skippedRows++;
                                        continue;
                                    }
                                    x = double.Parse(row[0].Replace(",", "."), CultureInfo.InvariantCulture);
                                    y = double.Parse(row[1].Replace(",", "."), CultureInfo.InvariantCulture);
                                    z = double.Parse(row[2].Replace(",", "."), CultureInfo.InvariantCulture);
                                    break;

                                case PointCloudFormat.CATIA_ASC: // CATIA ASC (with header and Point Format           = 'X %f Y %f Z %f')
                                    if (line.IndexOf('!') == 0 || line.IndexOf('*') == 0)
                                    {
                                        skippedRows++;
                                        continue;
                                    }
                                    x = double.Parse(row[1], CultureInfo.InvariantCulture);
                                    y = double.Parse(row[3], CultureInfo.InvariantCulture);
                                    z = double.Parse(row[5], CultureInfo.InvariantCulture);
                                    break;

                                case PointCloudFormat.XYZRGB:
                                case PointCloudFormat.XYZ: // XYZ RGB(INT)
                                    x = double.Parse(row[0], CultureInfo.InvariantCulture);
                                    y = double.Parse(row[1], CultureInfo.InvariantCulture);
                                    z = double.Parse(row[2], CultureInfo.InvariantCulture);

                                    if (readRGB == true)
                                    {
                                        r = LUT255[int.Parse(row[3], CultureInfo.InvariantCulture)];
                                        g = LUT255[int.Parse(row[4], CultureInfo.InvariantCulture)];
                                        b = LUT255[int.Parse(row[5], CultureInfo.InvariantCulture)];
                                    }
                                    break;

                                case PointCloudFormat.PTS: // PTS (INT) (RGB)
                                    x = double.Parse(row[0], CultureInfo.InvariantCulture);
                                    y = double.Parse(row[1], CultureInfo.InvariantCulture);
                                    z = double.Parse(row[2], CultureInfo.InvariantCulture);

                                    if (readRGB == true)
                                    {
                                        if (row.Length == 7) // XYZIRGB
                                        {
                                            r = LUT255[int.Parse(row[4], CultureInfo.InvariantCulture)];
                                            g = LUT255[int.Parse(row[5], CultureInfo.InvariantCulture)];
                                            b = LUT255[int.Parse(row[6], CultureInfo.InvariantCulture)];
                                        }
                                        else if (row.Length == 6) // XYZRGB
                                        {
                                            r = LUT255[int.Parse(row[3], CultureInfo.InvariantCulture)];
                                            g = LUT255[int.Parse(row[4], CultureInfo.InvariantCulture)];
                                            b = LUT255[int.Parse(row[5], CultureInfo.InvariantCulture)];
                                        }
                                    }
                                    else if (readIntensity == true)
                                    {
                                        if (row.Length == 4 || row.Length == 7) // XYZI or XYZIRGB
                                        {
                                            r = Remap(float.Parse(row[3], CultureInfo.InvariantCulture), -2048, 2047, 0, 1);
                                            g = r;
                                            b = r;
                                        }
                                    }
                                    break;

                                case PointCloudFormat.PLY_ASCII: // PLY (ASCII)
                                    x = double.Parse(row[0], CultureInfo.InvariantCulture);
                                    y = double.Parse(row[1], CultureInfo.InvariantCulture);
                                    z = double.Parse(row[2], CultureInfo.InvariantCulture);

                                    /*
									// normals
									if (readNormals)
									{
										// Vertex normals are the normalized average of the normals of the faces that contain that vertex
										// TODO: need to fix normal values?
										nx = float.Parse(row[3], CultureInfo.InvariantCulture);
										ny = float.Parse(row[4], CultureInfo.InvariantCulture);
										nz = float.Parse(row[5], CultureInfo.InvariantCulture);

										// and rgb
										if (readRGB)
										{
											r = float.Parse(row[6, CultureInfo.InvariantCulture])/255;
											g = float.Parse(row[7, CultureInfo.InvariantCulture])/255;
											b = float.Parse(row[8, CultureInfo.InvariantCulture])/255;
											//a = float.Parse(row[6], CultureInfo.InvariantCulture)/255; // TODO: alpha not supported yet
										}

									}else{ // no normals, but maybe rgb
										*/
                                    if (readRGB == true)
                                    {
                                        // TODO: need to fix PLY CloudCompare normals, they are before RGB
                                        if (plyHasNormals == true)
                                        {
                                            r = LUT255[int.Parse(row[6], CultureInfo.InvariantCulture)];
                                            g = LUT255[int.Parse(row[7], CultureInfo.InvariantCulture)];
                                            b = LUT255[int.Parse(row[8], CultureInfo.InvariantCulture)];
                                        }
                                        else if (plyHasDensity == false) // no normals or density
                                        {
                                            r = LUT255[int.Parse(row[3], CultureInfo.InvariantCulture)];
                                            g = LUT255[int.Parse(row[4], CultureInfo.InvariantCulture)];
                                            b = LUT255[int.Parse(row[5], CultureInfo.InvariantCulture)];
                                        }
                                        else // no normals, but have density
                                        {
                                            r = LUT255[int.Parse(row[4], CultureInfo.InvariantCulture)];
                                            g = LUT255[int.Parse(row[5], CultureInfo.InvariantCulture)];
                                            b = LUT255[int.Parse(row[6], CultureInfo.InvariantCulture)];
                                        }
                                        //a = float.Parse(row[6], CultureInfo.InvariantCulture)/255; // TODO: alpha not supported yet
                                    }
                                    /*
									}*/
                                    break;

                                case PointCloudFormat.PCD_ASCII: // pcd ascii
                                    x = double.Parse(row[0], CultureInfo.InvariantCulture);
                                    y = double.Parse(row[1], CultureInfo.InvariantCulture);
                                    z = double.Parse(row[2], CultureInfo.InvariantCulture);

                                    if (readRGB == true)
                                    {
                                        // TODO: need to check both rgb formats
                                        if (row.Length == 4)
                                        {
                                            var rgb = (int)decimal.Parse(row[3], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture);
                                            r = (rgb >> 16) & 0x0000ff;
                                            g = (rgb >> 8) & 0x0000ff;
                                            b = (rgb) & 0x0000ff;
                                            r = LUT255[(int)r];
                                            g = LUT255[(int)g];
                                            b = LUT255[(int)b];
                                        }
                                        else if (row.Length == 6)
                                        {
                                            r = LUT255[int.Parse(row[3], CultureInfo.InvariantCulture)];
                                            g = LUT255[int.Parse(row[4], CultureInfo.InvariantCulture)];
                                            b = LUT255[int.Parse(row[5], CultureInfo.InvariantCulture)];
                                        }
                                    }
                                    break;


                                default:
                                    Debug.LogError("> Error Unknown format:" + pointCloudFormat);
                                    break;

                            } // switch

                            // scaling enabled
                            if (useUnitScale == true)
                            {
                                x *= unitScale;
                                y *= unitScale;
                                z *= unitScale;
                            }

                            // manual offset enabled
                            if (autoOffsetNearZero == true || useManualOffset == true) // NOTE: can use only one at a time
                            {
                                x -= manualOffset.x;
                                y -= manualOffset.y;
                                z -= manualOffset.z;
                            }

                            // if flip
                            if (flipYZ == true)
                            {
                                points[rowCount].Set((float)x, (float)z, (float)y);
                            }
                            else
                            {
                                points[rowCount].Set((float)x, (float)y, (float)z);
                            }

                            // if have color data
                            if (readRGB == true || readIntensity == true)
                            {
                                if (r == 0 && g == 0 && b == 0)
                                    pointColors[rowCount] = Vector4.one;
                                else
                                    pointColors[rowCount].Set(r, g, b, 1);
                            }
                            /*
							// if have normals data, TODO: not possible yet
							if (readNormals)
							{
								writer.Write(nx);
								writer.Write(ny);
								writer.Write(nz);
							}
							*/

                            rowCount++;

                        }
                        else
                        { // if row length
                            skippedRows++;
                        }

                    }
                    else
                    { // if linelen
                        skippedRows++;
                    }


                    // reached end or enough points
                    if (streamReader.EndOfStream == true || rowCount >= masterPointCount)
                    {

                        if (skippedRows > 0) Debug.LogWarning("Parser skipped " + skippedRows + " rows (wrong length or bad data)");
                        //Debug.Log(masterVertexCount);

                        if (rowCount < masterPointCount) // error, file ended too early, not enough points
                        {
                            Debug.LogWarning("File does not contain enough points, fixing point count to " + rowCount + " (expected : " + masterPointCount + ")");
                            // fix header point count
                            //                            writer.BaseStream.Seek(0, SeekOrigin.Begin);
                            //                            writer.Write(binaryVersion);
                            //                            writer.Write((System.Int32)rowCount);
                        }
                        haveMoreToRead = false;
                    }
                } // while loop reading file

                stopwatch.Stop();
                // Debug.Log(stopwatch.ElapsedMilliseconds);
                // done reading, display it now
                isLoading = false;
                if (useDX11 == true)
                {
                    MainThread.Call(InitDX11Buffers);
                    Thread.Sleep(10); // wait for buffers to be ready_
                }
                OnLoadingCompleteCallBack(fullPath);

                hasLoadedPointCloud = true;
            } // using reader

            Debug.Log("Finished loading.");

            // if mesh version, build meshes
            if (useDX11 == false)
            {
                // build mesh assets
                int indexCount = 0;

#if UNITY_2017_3_OR_NEWER
                int MaxVertexCountPerMesh = 1000000;
#else
                int MaxVertexCountPerMesh = 65000;
#endif

                Vector3[] verts = new Vector3[MaxVertexCountPerMesh];
                Vector2[] uvs2 = new Vector2[MaxVertexCountPerMesh];
                int[] tris = new int[MaxVertexCountPerMesh];
                Color[] cols = new Color[MaxVertexCountPerMesh];
                Vector3[] norms = new Vector3[MaxVertexCountPerMesh];

                // process all point data into meshes
                for (int i = 0, len = points.Length; i < len; i++)
                {
                    verts[indexCount] = points[i];
                    uvs2[indexCount].Set(points[i].x, points[i].y);
                    tris[indexCount] = i % MaxVertexCountPerMesh;

                    if (readRGB || readIntensity)
                    {
                        cols[indexCount] = new Color(pointColors[i].x, pointColors[i].y, pointColors[i].z, 1);
                    }
                    //if (readNormals) normals2[indexCount] = normalArray[i];

                    indexCount++;

                    if (indexCount >= MaxVertexCountPerMesh || i == MaxVertexCountPerMesh - 1)
                    {
                        var m = new TempMesh();
                        m.verts = verts;
                        m.tris = tris;
                        m.cols = cols;
                        m.norms = norms;
                        //Debug.Log(m.verts.Length);
                        isBuildingMesh = true;
                        MainThread.Call(BuildMesh, m);
                        while (isBuildingMesh == true)
                        {
                            Thread.Sleep(50);
                        }
                        //if (addMeshesToScene && go != null) if (createLODS) BuildLODS(go, vertices2, triangles2, colors2, normals2);

                        indexCount = 0;

                        // need to clear arrays, should use lists otherwise last mesh has too many verts (or slice last array)
                        System.Array.Clear(verts, 0, MaxVertexCountPerMesh);
                        System.Array.Clear(uvs2, 0, MaxVertexCountPerMesh);
                        System.Array.Clear(tris, 0, MaxVertexCountPerMesh);
                        if (readRGB || readIntensity) System.Array.Clear(cols, 0, MaxVertexCountPerMesh);
                        //if (readNormals) System.Array.Clear(norms, 0, MaxVertexCountPerMesh);
                    }
                } // all points
            } // use dx11

            // if caching, save as bin
            if (cacheBinFile == true)
            {
                var outputFile = fullPath + ".bin";

                if (File.Exists(outputFile) == true && overrideExistingCacheFile == false)
                {
                    Debug.Log("Cache file already exists, not saving new cached file.." + outputFile);
                    return;
                }

                var writer = new BinaryWriter(File.Open(outputFile, FileMode.Create));
                if (writer == null)
                {
                    Debug.LogError("Cannot output file: " + outputFile);
                    return;
                }

                byte binaryVersion = 1;
                writer.Write(binaryVersion);
                writer.Write((System.Int32)masterPointCount);
                writer.Write(readRGB | readIntensity);

                for (int i = 0, length = points.Length; i < length; i++)
                {
                    writer.Write(points[i].x);
                    writer.Write(points[i].y);
                    writer.Write(points[i].z);
                    if (readRGB == true || readIntensity == true)
                    {
                        writer.Write(pointColors[i].x);
                        writer.Write(pointColors[i].y);
                        writer.Write(pointColors[i].z);
                        writer.Write(pointColors[i].w);
                    }
                }
                writer.Close();
                Debug.Log("Finished saving cached file: " + outputFile);
            } // cache
        } // LoadRawPointCloud()


        public struct TempMesh
        {
            public Vector3[] verts;
            public int[] tris;
            public Color[] cols;
            public Vector3[] norms;
        }

        public void InitDX11Buffers()
        {
            EnsureCloudMaterial();
            if (totalPoints == 0)
            {
                totalPoints = 1;
                points = new Vector3[1];
                pointColors = new Vector4[1];
            }

            if (useDX11 == true) ReleaseDX11Buffers();

            if (bufferPoints != null) bufferPoints.Dispose();
            bufferPoints = new ComputeBuffer(totalPoints, 12);
            bufferPoints.SetData(points);
            if (cloudMaterial != null)
            {
                cloudMaterial.SetBuffer("buf_Points", bufferPoints);
                cloudMaterial.SetMatrix("_modelMatrix", transform.localToWorldMatrix);
            }

            // Ensure bufferColors is ALWAYS populated and bound to buf_Colors
            if (pointColors == null || pointColors.Length < totalPoints)
            {
                pointColors = new Vector4[totalPoints];
                for (int i = 0; i < totalPoints; i++) pointColors[i] = Vector4.one;
            }
            if (bufferColors != null) bufferColors.Dispose();
            bufferColors = new ComputeBuffer(totalPoints, 16);
            bufferColors.SetData(pointColors);
            if (cloudMaterial != null)
            {
                cloudMaterial.SetBuffer("buf_Colors", bufferColors);
            }

            if (forceDepthBufferPass == true && depthMaterial != null)
            {
                depthMaterial.SetBuffer("buf_Points", bufferPoints);
                depthMaterial.SetMatrix("_modelMatrix", transform.localToWorldMatrix);
            }
        }

        void ReleaseDX11Buffers()
        {
            if (bufferPoints != null)
            {
                bufferPoints.Release();
                bufferPoints.Dispose();
            }
            bufferPoints = null;
            if (bufferColors != null)
            {
                bufferColors.Release();
                bufferColors.Dispose();
            }
            bufferColors = null;
        }

        void OnDestroy()
        {
            abortReaderThread = true;

            if (importerThread != null) importerThread.Abort();

            if (useDX11 == true)
            {
                ReleaseDX11Buffers();

                // cleanup
                points = null;
                pointColors = null;
                GC.Collect();
            }
            ReleaseMesh();
            OnInitCollision = null;
        }

        // mainloop, for displaying the points
        void OnRenderObject()
        {
            if (currentRenderMode == RenderMode.Mesh) return;
            if (displayPoints == false || useCommandBuffer == true) return;
            if (drawFirstFrameForced == false && isLoading == true) return;
            if (cloudMaterial == null || bufferPoints == null) return;
            if (Camera.current == null) return;

            // Restrict point rendering strictly to primary view cameras (Game View RightPointCloud / MainCamera)
            // Filtering out SceneView, internal DepthNormals, and secondary passes avoids 4x~6x overdraw and GPU stalls
            if (!Camera.current.CompareTag("RightPointCloud") &&
                !Camera.current.CompareTag("MainCamera") &&
                !Camera.current.CompareTag("RightPntDepthCamera"))
                return;

            if (Camera.current.CompareTag("RightPntDepthCamera"))
            {
                if (depthMaterial != null) depthMaterial.SetPass(0);
                else cloudMaterial.SetPass(0);
            }
            else
            {
                cloudMaterial.SetPass(0);
            }

            if (cam == null) cam = Camera.current;

            OTA.PerfDiag.RecordRender();

#if UNITY_2019_1_OR_NEWER
            Graphics.DrawProceduralNow(MeshTopology.Points, totalPoints);
#else
            Graphics.DrawProcedural(MeshTopology.Points, totalPoints);
#endif
        }


        // called after some file load operation has finished
        void OnLoadingCompleteCallBack(System.Object a)
        {
            if (OnLoadingComplete != null) OnLoadingComplete((string)a);

            if (useCommandBuffer == true)
            {
                commandBuffer.DrawProcedural(Matrix4x4.identity, cloudMaterial, 0, MeshTopology.Points, totalPoints, 1);
            }

            //if (forceDepthBufferPass == true)
            //{
            //    commandBufferDepth.DrawProcedural(Matrix4x4.identity, cloudMaterial, 0, MeshTopology.Points, totalPoints, 1);
            //}
        }

        float timerCount = 0;
        // bruteforce point picker
        void SelectClosestPoint()
        {
            timerCount += Time.deltaTime;
            // left click for measuring
            if (/*Input.GetMouseButtonDown(0) ||*/ needCollsion)
            {
                if (hasLoadedPointCloud)
                {
                    FindClosestPointBrute();
                }
            }
        }

        private List<Vector3> canSelectedPts = new List<Vector3>();
        // TODO replace with new measuring system
        private void FindClosestPointBrute() // in screen pixel coordinates
        {
            int? closestIndex = null;
            int closestTileIndex = 0;
            float closestDistance = Mathf.Infinity;
            //Camera cam = Camera.main;
            Ray ray = cam.GetComponent<GISCameraController>().GetMouseRay(Vector3.zero);
            Vector2 mousePos = cam.GetComponent<GISCameraController>().GetMousePosition();
            List<PointsCloundDivider.PointTile> tiles = pcDivider.IntersectsRay(ray);

            if(tiles != null)
            {
                canSelectedPts.Clear();
                for (int k = 0; k < tiles.Count; k++)
                {
                    List<int> indices = tiles[k].indices;
                    //var offsetPixels = new Vector2(0, 32); // search area in pixels
                    //var farPointUp = cam.ScreenPointToRay(mousePos + offsetPixels).GetPoint(999);
                    //var farPointDown = cam.ScreenPointToRay(mousePos - offsetPixels).GetPoint(999);

                    //var farPointUp = cam.GetComponent<GISCameraController>().GetMouseRay(offsetPixels).GetPoint(999);
                    //var farPointDown = cam.GetComponent<GISCameraController>().GetMouseRay(-offsetPixels).GetPoint(999);

                    //offsetPixels = new Vector2(32, 0);  // search area in pixels
                    //var farPointLeft = cam.ScreenPointToRay(mousePos - offsetPixels).GetPoint(999);
                    //var farPointRight = cam.ScreenPointToRay(mousePos + offsetPixels).GetPoint(999);

                    //var farPointLeft = cam.GetComponent<GISCameraController>().GetMouseRay(-offsetPixels).GetPoint(999);
                    //var farPointRight = cam.GetComponent<GISCameraController>().GetMouseRay(offsetPixels).GetPoint(999);

                    var screenPos = Vector2.zero;
                    float distance = Mathf.Infinity;

                    // build filtering planes
                    //Plane forwardPlane = new Plane(cam.transform.forward, cam.transform.position);
                    //Plane bottomLeft = new Plane(cam.transform.position, farPointDown, farPointLeft);
                    //Plane topLeft = new Plane(cam.transform.position, farPointLeft, farPointUp);
                    //Plane topRight = new Plane(cam.transform.position, farPointUp, farPointRight);
                    //Plane bottomRight = new Plane(cam.transform.position, farPointRight, farPointDown);


                    // display search area
                    //Debug.DrawLine(farPointDown, farPointLeft, Color.magenta, 20);
                    //Debug.DrawLine(farPointLeft, farPointUp, Color.magenta, 20);
                    //Debug.DrawLine(farPointUp, farPointRight, Color.magenta, 20);
                    //Debug.DrawLine(farPointRight, farPointDown, Color.magenta, 20);


                    // check all points, until find close enough hit
                    var pixelThreshold = 15; // if distance is this or less, just select it
                    var viewThreshold = 50;

                    for (int i = 0, len = indices.Count; i < len; i++)
                    {
                        //if (i % maxIterationsPerFrame == 0)
                        //{
                        //    // Pause our work here, and continue finding on the next frame
                        //    yield return null;
                        //}
                        if ((!cam.orthographic && (cam.transform.position - tiles[k].GetPoint(i)).magnitude > viewThreshold) || (cam.orthographic && cam.orthographicSize > 32))
                            continue;
                        if(cam.orthographic)
                        {
                            Plane forwardPlane = new Plane(cam.transform.forward, Vector3.zero);
                            if (forwardPlane.GetSide(tiles[k].GetPoint(i))) continue;
                        }

                        screenPos = cam.WorldToScreenPoint(tiles[k].GetPoint(i));
                        //if (!forwardPlane.GetSide(pts[i])) continue;
                        //if (topRight.GetSide(pts[i])) continue;
                        //if (bottomRight.GetSide(pts[i])) continue;
                        //if (bottomLeft.GetSide(pts[i])) continue;
                        //if (topLeft.GetSide(pts[i])) continue;


                        distance = Vector2.Distance(mousePos, screenPos);
                        //distance = DistanceApprox(mousePos, screenPos);

                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            if (distance <= pixelThreshold)
                            {
                                closestIndex = i;
                                closestTileIndex = k;
                                canSelectedPts.Add(tiles[k].GetPoint(i));
                                break;
                            }// early exit on close enough hit
                        }
                    }
                }
                canSelectedPts.Sort((T1, T2) => {
                    float dis1 = (cam.transform.position - T1).sqrMagnitude;
                    float dis2 = (cam.transform.position - T2).sqrMagnitude;
                    return dis1 > dis2 ? 1 : -1;
                });
            }

            if (closestIndex != null)
            {
                if (PointWasSelected != null) PointWasSelected(canSelectedPts[0]);
                    //PointWasSelected(tiles[closestTileIndex].points[(int)closestIndex]); // fire event if have listeners
                    //Debug.Log("PointIndex:" + ((int)closestIndex) + " pos:" + points[(int)closestIndex]);
            }
            else
            {
                if (PointWasUnSelected != null) PointWasUnSelected(); // fire event if have listeners
                //Debug.Log("No point selected..");
            }
        }

        public bool MouseCast(out Vector3 pt) // in screen pixel coordinates
        {
            pt = Vector3.zero;
            isSearchingPoint = true;

            int? closestIndex = null;
            int closestTileIndex = 0;
            float closestDistance = Mathf.Infinity;
            if (cam == null) cam = Camera.main;
            if (cam == null) return false;
            var gisCam = cam.GetComponent<GISCameraController>();
            Ray ray = gisCam != null ? gisCam.GetMouseRay(Vector3.zero) : cam.ScreenPointToRay(Input.mousePosition);
            var mousePos = gisCam != null ? (Vector2)gisCam.GetMousePosition() : (Vector2)Input.mousePosition;
            List<PointsCloundDivider.PointTile> tiles = pcDivider != null ? pcDivider.IntersectsRay(ray) : null;

            if (tiles != null)
            {
                canSelectedPts.Clear();
                for (int k = 0; k < tiles.Count; k++)
                {
                    List<int> indices = tiles[k].indices;
                    var screenPos = Vector2.zero;
                    float distance = Mathf.Infinity;

                    // check all points, until find close enough hit
                    var pixelThreshold = 15; // if distance is this or less, just select it
                    var viewThreshold = 500;

                    for (int i = 0, len = indices.Count; i < len; i++)
                    {
                        //if (i % maxIterationsPerFrame == 0)
                        //{
                        //    // Pause our work here, and continue finding on the next frame
                        //    yield return null;
                        //}
                        if ((!cam.orthographic && (cam.transform.position - tiles[k].GetPoint(i)).magnitude > viewThreshold) || (cam.orthographic && cam.orthographicSize > 32))
                            continue;
                        if (cam.orthographic)
                        {
                            Plane forwardPlane = new Plane(cam.transform.forward, Vector3.zero);
                            if (forwardPlane.GetSide(tiles[k].GetPoint(i))) continue;
                        }

                        screenPos = cam.WorldToScreenPoint(tiles[k].GetPoint(i));

                        distance = Vector2.Distance(mousePos, screenPos);

                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            if (distance <= pixelThreshold)
                            {
                                closestIndex = i;
                                closestTileIndex = k;
                                canSelectedPts.Add(tiles[k].GetPoint(i));
                                break;
                            }// early exit on close enough hit
                        }
                    }
                }
                canSelectedPts.Sort((T1, T2) => {
                    float dis1 = (cam.transform.position - T1).sqrMagnitude;
                    float dis2 = (cam.transform.position - T2).sqrMagnitude;
                    return dis1 > dis2 ? 1 : -1;
                });
            }

            if (closestIndex != null)
            {
                pt = canSelectedPts[0];
                return true;
            }
            return false;
        }

        public bool RayCast(ref Vector3 pt, Ray ray) // in screen pixel coordinates
        {
            isSearchingPoint = true;

            int? closestIndex = null;
            int closestTileIndex = 0;
            List<PointsCloundDivider.PointTile> tiles = pcDivider.IntersectsRay(ray);
            Vector3 op = pt;

            if (tiles != null)
            {
                canSelectedPts.Clear();
                for (int k = 0; k < tiles.Count; k++)
                {
                    List<int> indices = tiles[k].indices;
                    var screenPos = Vector2.zero;

                    // check all points, until find close enough hit
                    var viewThreshold = 1000;

                    for (int i = 0, len = indices.Count; i < len; i++)
                    {
                        if ((!cam.orthographic && (op - tiles[k].GetPoint(i)).magnitude > viewThreshold) || (cam.orthographic && cam.orthographicSize > 32))
                            continue;
                        Vector3 d1 = (tiles[k].GetPoint(i) - op).normalized;
                        if (Vector3.Dot(d1, ray.direction) > 0.996194698f)
                        {
                            closestIndex = i;
                            closestTileIndex = k;
                            canSelectedPts.Add(tiles[k].GetPoint(i));
                            break;
                            // early exit on close enough hit
                        }
                    }
                }
                canSelectedPts.Sort((T1, T2) =>
                {
                    float dis1 = Vector3.Dot(T1 - op, ray.direction);
                    float dis2 = Vector3.Dot(T2 - op, ray.direction);
                    return dis1 < dis2 ? -1 : 1;
                });
            }

            if (closestIndex != null)
            {
                pt = canSelectedPts[0];
                return true;
            }

            pt = Vector3.zero;
            return false;
        }


        void LogMessage(string msg)
        {
            Debug.Log(msg);
        }

        bool ValidateSaveAndRead(string path, string fileToRead)
        {
            if (path.Length < 1) { Debug.Log("> Save cancelled.."); return false; }
            if (fileToRead.Length < 1) { Debug.LogError("> Cannot find file (" + fileToRead + ")"); return false; }
            if (!File.Exists(fileToRead)) { Debug.LogError("> Cannot find file (" + fileToRead + ")"); return false; }
            if (Path.GetExtension(fileToRead).ToLower() == ".bin") { Debug.LogError("Source file extension is .bin, binary file conversion is not supported"); return false; }
            return true;
        }

        float Remap(float source, float sourceFrom, float sourceTo, float targetFrom, float targetTo)
        {
            return targetFrom + (source - sourceFrom) * (targetTo - targetFrom) / (sourceTo - sourceFrom);
        }

        int CharCount(string source, char separator)
        {
            int count = 0;
            for (int i = 0, length = source.Length; i < length; i++)
            {
                if (source[i] == separator) count++;
            }
            return count;
        }

        public void InitPoints(Dictionary<int, PointsCloudCutter.SinglePoint> pts)
        {
            points = new Vector3[pts.Count];
            pointColors = new Vector4[pts.Count];
            int index = 0;
            foreach(var pt in pts)
            {
                points[index] = pt.Value.Position;
                pointColors[index] = pt.Value.Color;
                index++;
            }
            isLoading = false;
            totalPoints = pts.Count;
            InitDX11Buffers();
        }

        // OTA: direct array upload (no PointsCloudCutter/Willshare dependency).
        // pts: xyz, cols: rgb *with w=1* (may be null when hasColor=false).
        public void SetPoints(Vector3[] pts, Vector4[] cols, bool hasColor)
        {
            points = pts;
            readRGB = hasColor && cols != null && cols.Length == pts.Length;
            if (readRGB) pointColors = cols;
            isLoading = false;
            totalPoints = pts != null ? pts.Length : 0;
            UpdateViewBuffers(currentRenderMode);

            if (pcDivider == null)
            {
                pcDivider = GetComponent<PointsCloundDivider>();
                if (pcDivider == null) pcDivider = gameObject.AddComponent<PointsCloundDivider>();
            }
            if (pcDivider != null && points != null && points.Length > 0)
            {
                pcDivider.InitPointClounds(points);
            }
        }

        public void EnsureCloudMaterial()
        {
            if (cloudMaterial == null || cloudMaterial.shader == null || (currentRenderMode == RenderMode.Point && cloudMaterial.shader.name != "UnityCoder/PointCloud/DX11/PointCloudColorDx11-Pixel"))
            {
                var mat = Resources.Load<Material>("PointCloudColorDx11-Pixel");
                if (mat != null)
                {
                    cloudMaterial = new Material(mat);
                }
                else
                {
                    var s = Shader.Find("UnityCoder/PointCloud/DX11/PointCloudColorDx11-Pixel");
                    if (s != null)
                    {
                        cloudMaterial = new Material(s) { name = "PointCloudColorDx11_Pixel" };
                    }
                }

                if (cloudMaterial != null)
                {
                    if (bufferPoints != null) cloudMaterial.SetBuffer("buf_Points", bufferPoints);
                    if (bufferColors != null) cloudMaterial.SetBuffer("buf_Colors", bufferColors);
                    cloudMaterial.SetMatrix("_modelMatrix", transform.localToWorldMatrix);
                }
            }
        }

        private void EnsureMeshAndRenderers()
        {
            if (_mf == null) _mf = GetComponent<MeshFilter>();
            if (_mf == null) _mf = gameObject.AddComponent<MeshFilter>();
            if (_mr == null) _mr = GetComponent<MeshRenderer>();
            if (_mr == null) _mr = gameObject.AddComponent<MeshRenderer>();
        }

        private void EnsureMeshGeometryAndColors()
        {
            if (points == null || points.Length == 0) return;
            EnsureMeshAndRenderers();

            int count = points.Length;
            bool needRebuildGeometry = (_mf.sharedMesh == null || _mf.sharedMesh.vertexCount != count);

            if (needRebuildGeometry)
            {
                Mesh mesh = new Mesh();
                mesh.name = "CorridorPointCloud_Mesh";
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.vertices = points;

                int[] indices = new int[count];
                for (int i = 0; i < count; i++) indices[i] = i;
                mesh.SetIndices(indices, MeshTopology.Points, 0);

                Color32[] cCols = new Color32[count];
                bool hasCols = (pointColors != null && pointColors.Length == count);
                for (int i = 0; i < count; i++)
                {
                    if (hasCols)
                    {
                        Vector4 c = pointColors[i];
                        cCols[i] = new Color32(
                            (byte)(Mathf.Clamp01(c.x) * 255f),
                            (byte)(Mathf.Clamp01(c.y) * 255f),
                            (byte)(Mathf.Clamp01(c.z) * 255f),
                            (byte)(Mathf.Clamp01(c.w) * 255f)
                        );
                    }
                    else
                    {
                        cCols[i] = new Color32(255, 255, 255, 255);
                    }
                }
                mesh.colors32 = cCols;

                if (_mf.sharedMesh != null)
                {
                    if (Application.isPlaying) Destroy(_mf.sharedMesh);
                    else DestroyImmediate(_mf.sharedMesh);
                }
                _mf.sharedMesh = mesh;
            }
            else
            {
                Color32[] cCols = new Color32[count];
                bool hasCols = (pointColors != null && pointColors.Length == count);
                for (int i = 0; i < count; i++)
                {
                    if (hasCols)
                    {
                        Vector4 c = pointColors[i];
                        cCols[i] = new Color32(
                            (byte)(Mathf.Clamp01(c.x) * 255f),
                            (byte)(Mathf.Clamp01(c.y) * 255f),
                            (byte)(Mathf.Clamp01(c.z) * 255f),
                            (byte)(Mathf.Clamp01(c.w) * 255f)
                        );
                    }
                    else
                    {
                        cCols[i] = new Color32(255, 255, 255, 255);
                    }
                }
                _mf.sharedMesh.colors32 = cCols;
            }
        }

        public void UpdateViewBuffers(RenderMode mode)
        {
            if (points == null || points.Length == 0) return;
            totalPoints = points.Length;
            currentRenderMode = mode;
            isLoading = false;

            EnsureMeshAndRenderers();

            if (mode == RenderMode.Mesh)
            {
                // 开启线框模式：使用 Unity Mesh 拓扑 + MeshRenderer + WillshareCesiumUnlitTilesetShader_PointCloudAutoSize_new
                EnsureMeshGeometryAndColors();

                if (meshMaterial == null)
                {
                    meshMaterial = Resources.Load<Material>("WillshareCesiumUnlitTilesetShader_PointCloudAutoSize_new");
                    if (meshMaterial == null)
                    {
                        var s = Shader.Find("Unlit/WillshareCesiumUnlitTilesetShader_PointCloudAutoSize_new");
                        if (s != null) meshMaterial = new Material(s);
                    }
                }

                if (meshMaterial != null)
                {
                    meshMaterial.SetInt("_MaxPixel", 25);
                    meshMaterial.SetInt("_OriginColor", 1);
                    _mr.sharedMaterial = meshMaterial;
                }

                _mr.enabled = true;
                _mr.receiveShadows = false;
                _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                _mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                UpdateOutlineState(true);
            }
            else
            {
                // 关闭线框模式（Point 原生点云）：100% 对齐 3dTrack 真实效果（图2样式）
                // 1px 硬件原生点基元，铁塔晶格精细纤巧、树木细腻自然、导线纤细真实
                if (_mr != null && _mr.enabled) _mr.enabled = false;
                UpdateOutlineState(false);

                var mat = Resources.Load<Material>("PointCloudColorDx11-Pixel");
                if (mat != null)
                {
                    cloudMaterial = new Material(mat);
                }
                else
                {
                    var s = Shader.Find("UnityCoder/PointCloud/DX11/PointCloudColorDx11-Pixel");
                    if (s != null)
                    {
                        cloudMaterial = new Material(s) { name = "PointCloudColorDx11_Pixel" };
                    }
                }

                InitDX11Buffers();
            }
        }

        private void UpdateOutlineState(bool enable)
        {
            Camera targetCam = cam != null ? cam : OTA.Corridor.CameraHelper.MainCamera;
            if (targetCam != null)
            {
                var layer = targetCam.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>();
                if (layer != null)
                {
                    layer.enabled = enable;
                }
                var controller = targetCam.GetComponent<OTA.Corridor.Overlays.SobelOutlineController>();
                if (controller == null) controller = FindObjectOfType<OTA.Corridor.Overlays.SobelOutlineController>();
                if (controller == null && enable) controller = targetCam.gameObject.AddComponent<OTA.Corridor.Overlays.SobelOutlineController>();
                if (controller != null)
                {
                    controller.outlineEnabled = enable;
                    controller.ApplyOutlineState();
                }
            }

            // If disabling outline, ensure all PostProcessLayers and Volumes across scene are strictly silenced to prevent Depth/Normals pre-passes
            if (!enable)
            {
                var allLayers = FindObjectsOfType<UnityEngine.Rendering.PostProcessing.PostProcessLayer>();
                for (int i = 0; i < allLayers.Length; i++)
                {
                    if (allLayers[i] != null) allLayers[i].enabled = false;
                }
                var allVolumes = FindObjectsOfType<UnityEngine.Rendering.PostProcessing.PostProcessVolume>();
                for (int i = 0; i < allVolumes.Length; i++)
                {
                    if (allVolumes[i] != null) allVolumes[i].weight = 0f;
                }
            }
        }

        private void UpdateMeshPipeline()
        {
            // Deprecated: Handled by UpdateViewBuffers and EnsureMeshGeometryAndColors
        }

        // OTA: Instant GPU color buffer update for interactive reclassification without reloading
        public void UpdateColors(Vector4[] cols)
        {
            if (cols == null || cols.Length == 0) return;
            pointColors = cols;
            readRGB = true;

            // 1. 更新 Point 模式下的 ComputeBuffer
            if (bufferColors != null && bufferColors.count == cols.Length)
            {
                bufferColors.SetData(cols);
            }
            else
            {
                totalPoints = cols.Length;
                InitDX11Buffers();
            }

            if (cloudMaterial != null && bufferColors != null)
            {
                cloudMaterial.SetBuffer("buf_Colors", bufferColors);
            }

            // 2. 更新 Mesh 模式下的 Mesh 顶点颜色
            if (_mf != null && _mf.sharedMesh != null && _mf.sharedMesh.vertexCount == cols.Length)
            {
                Color32[] cCols = new Color32[cols.Length];
                for (int i = 0; i < cols.Length; i++)
                {
                    Vector4 c = cols[i];
                    cCols[i] = new Color32(
                        (byte)(Mathf.Clamp01(c.x) * 255f),
                        (byte)(Mathf.Clamp01(c.y) * 255f),
                        (byte)(Mathf.Clamp01(c.z) * 255f),
                        (byte)(Mathf.Clamp01(c.w) * 255f)
                    );
                }
                _mf.sharedMesh.colors32 = cCols;
            }
        }

        public void ReleaseComputeBuffers()
        {
            ReleaseDX11Buffers();
        }

        private void ReleaseMesh()
        {
            if (_mf != null && _mf.sharedMesh != null)
            {
                if (Application.isPlaying) Destroy(_mf.sharedMesh);
                else DestroyImmediate(_mf.sharedMesh);
                _mf.sharedMesh = null;
            }
        }

        public void ClearPoints()
        {
            ReleaseDX11Buffers();
            ReleaseMesh();
            if (pcDivider != null) pcDivider.Clear();

            points = null;
            pointColors = null;
            GC.Collect();
        }

        public delegate void PointSelectedHandler(Vector3 pointPos, int pointIndex);
        public event PointSelectedHandler OnPointSelected;

        /// <summary>
        /// 15px screen-space picking via Octree raycast pruning + screen projection (<5ms)
        /// </summary>
        public bool MouseCast(Vector2 mouseScreenPos, Camera targetCam, out Vector3 hitPos, out int hitIndex, float pixelThreshold = 15f)
        {
            hitPos = Vector3.zero;
            hitIndex = -1;
            if (targetCam == null) targetCam = cam != null ? cam : Camera.main;
            if (targetCam == null || pcDivider == null || !pcDivider.Initialized || points == null || points.Length == 0)
            {
                return false;
            }

            Ray ray = targetCam.ScreenPointToRay(mouseScreenPos);
            var tiles = pcDivider.IntersectsRay(ray);
            if (tiles == null || tiles.Count == 0) return false;

            float closestPixelDist = pixelThreshold;
            float closestDepth = Mathf.Infinity;
            bool found = false;

            for (int k = 0; k < tiles.Count; k++)
            {
                var tile = tiles[k];
                var indices = tile.indices;
                for (int i = 0, len = indices.Count; i < len; i++)
                {
                    int ptIdx = indices[i];
                    Vector3 worldPt = points[ptIdx];

                    // View distance cull
                    float camDist = Vector3.Distance(targetCam.transform.position, worldPt);
                    if (camDist > 1000f) continue;

                    Vector3 screenPt3 = targetCam.WorldToScreenPoint(worldPt);
                    if (screenPt3.z <= 0) continue; // Behind camera

                    float pixelDist = Vector2.Distance(mouseScreenPos, new Vector2(screenPt3.x, screenPt3.y));
                    if (pixelDist <= closestPixelDist)
                    {
                        if (pixelDist < closestPixelDist * 0.5f || screenPt3.z < closestDepth)
                        {
                            closestPixelDist = pixelDist;
                            closestDepth = screenPt3.z;
                            hitPos = worldPt;
                            hitIndex = ptIdx;
                            found = true;
                        }
                    }
                }
            }

            if (found)
            {
                OnPointSelected?.Invoke(hitPos, hitIndex);
                PointWasSelected?.Invoke(hitPos);
            }
            return found;
        }


        public bool IsLoading()
        {
            return isLoading;
        }

        bool isBuildingMesh = false;
        int meshCounter = 0;
        void BuildMesh(System.Object a)
        {
            var m = (TempMesh)a;
            var verts = m.verts;
            var tris = m.tris;
            var colors = m.cols;
            //var normals = m.norms;

            GameObject target = new GameObject();

            var mf = target.AddComponent<MeshFilter>();
            var mr = target.AddComponent<MeshRenderer>();

            Mesh mesh = new Mesh();
#if UNITY_2017_3_OR_NEWER
            mesh.indexFormat = IndexFormat.UInt32;
#endif
            target.isStatic = false;
            mf.mesh = mesh;
            target.transform.name = "PC_" + meshCounter;
            mr.sharedMaterial = meshMaterial;
            mr.receiveShadows = false;
            mr.shadowCastingMode = ShadowCastingMode.Off;
#if UNITY_5_6_OR_NEWER
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
#else
            mr.lightProbeUsage = LightProbeUsage.Off;
#endif
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            // disable ligtmap static 
            //GameObjectUtility.SetStaticEditorFlags(target, ~StaticEditorFlags.LightmapStatic);

            //GameObject lodRoot = null;

            //target.transform.parent = folder.transform;

            mesh.vertices = verts;
            //mesh.uv = uvs;
            if (readRGB == true || readIntensity == true)
            {
                mesh.colors = colors;
            }
            //if (readNormals == true) mesh.normals = normals;

            // TODO: use scanner centerpoint and calculate direction from that..not really accurate
            //if (forceRecalculateNormals) ...

            mesh.SetIndices(tris, MeshTopology.Points, 0);
            //mesh.RecalculateBounds();

            //cloudList.Add(mesh);
            meshCounter++;

            // FIXME: temporary workaround to not add objects into scene..
            //if (addMeshesToScene == false) DestroyImmediate(target);

            //return target;
            isBuildingMesh = false;
        }

#endif



        public void ChangePointColor(Dictionary<int, Vector4> pts, Vector4 color, bool autoupdate = false, bool bsection = false)
        {
            if ((points == null || pts.Count == 0) && color != Vector4.zero)
            {
                for (int i = 0; i < pointColors.Length; i++)
                {
                    if (IsColored(i))
                        continue;
                    pointColors[i] = color;
                }
            }
            else
            {
                Dictionary<int, Vector4> tmppts = null;
                if (color != Vector4.zero)
                    tmppts = new Dictionary<int, Vector4>();
                foreach (var i in pts.Keys)
                {
                    if (color != Vector4.zero && !bsection)
                    {
                        if (IsColored(i) && pointColors[i] != color)
                        {
                            var scolor = SectionManager.instance.RemoveCaches(i);
                            if (scolor != Vector4.zero)
                                pointColors[i] = scolor;
                            //else if(ObstacleTreeManager.instance.RemoveCaches(i, pointColors[i] == ColorRulerManager.instance.GetRulerValue("铁塔")) == Vector4.zero)
                            //    pointColors[i] = Vector3.zero;
                        }
                        tmppts.Add(i, pointColors[i]);
                    }
                    if(pts[i] != Vector4.zero || color != Vector4.zero)
                        pointColors[i] = color == Vector4.zero ? pts[i] : color;
                }
                if (color != Vector4.zero && tmppts.Count != 0)
                {
                    pts.Clear();
                    AnalysisBase.CombineTable(pts, tmppts);
                    tmppts.Clear();
                }
            }

            if (autoupdate)
                InitDX11Buffers();
        }

        public void ChangeColor(Dictionary<int, Vector4> pts)
        {
            foreach (var i in pts.Keys)
                pointColors[i] = pts[i];
        }
        
        public void ChangeColor(List<int> indices, Vector4 color)
        {
            foreach (var i in indices)
                pointColors[i] = color;
        }

        public void ChangePointToTree(Dictionary<int, Vector4> pts)
        {
            Dictionary<int, Vector4> tmppts = new Dictionary<int, Vector4>();
            foreach (var i in pts.Keys)
            {
                if (!IsColored(i))
                    continue;
                Vector4 v = SectionManager.instance.RemoveCaches(i);
                if (v == Vector4.zero)
                    v = ObstacleTreeManager.instance.RemoveCaches(i, pointColors[i] == ColorRulerManager.instance.GetRulerValue("铁塔"));
                if (v == Vector4.zero)
                    continue;

                tmppts.Add(i, pointColors[i]);
                pointColors[i] = v;
            }
            if (tmppts.Count != 0)
            {
                pts.Clear();
                AnalysisBase.CombineTable(pts, tmppts);
                tmppts.Clear();
            }

            InitDX11Buffers();
        }

        public void UpdateViewBuffers()
        {
            InitDX11Buffers();
        }

        internal bool IsColored(int index)
        {
            return ColorRulerManager.instance.ContainsValue(pointColors[index]) || (pointColors[index].w == 0 && pointColors[index] != Vector4.zero);
        }
        
        internal bool IsColored(Vector3 pt)
        {
            int index = Array.IndexOf(points, pt);
            
            return ColorRulerManager.instance.ContainsValue(pointColors[index]) || (pointColors[index].w == 0 && pointColors[index] != Vector4.zero);
        }

#endif

        internal bool ISPointExist(int key)
        {
            return pointColors.Length > key;
        }

        internal Dictionary<Vector3, int> GetGroudPoints()
        {
            Dictionary<Vector3, int> pts = new Dictionary<Vector3, int>();
            for (int i = 0; i < points.Length; i++)
            {
                if (IsColored(i))
                    continue;
                pts.Add(points[i], i);
            }

            return pts;
        }

        internal void ChangeColor(List<int> indices, Color col)
        {
            foreach(var index in indices)
                pointColors[index] = col;
        }
    } // class
} // namespace

