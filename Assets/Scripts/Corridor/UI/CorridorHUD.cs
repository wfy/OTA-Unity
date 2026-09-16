using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OTA.Corridor.Hierarchy;
using OTA.Corridor.Mechanics;
using OTA.Corridor.Overlays;
using OTA.Corridor.Annotation;
using ConductorSpec = OTA.Corridor.Mechanics.ConductorSpec;
using PointCloudRuntimeViewer;
using UnityEngine;
using Metervara.Interaction;

namespace OTA.Corridor.UI
{
    public partial class CorridorHUD : MonoBehaviour
    {
        public static CorridorHUD Instance { get; private set; }
        [Header("Dependencies")]
        public CorridorColorManager colorManager;
        public CorridorSectionManager sectionManager;
        public CorridorOverlayManager overlayManager;
        public LasImporter importer;
        public P0OrbitCamera orbitCam;

        [Header("UI State")]
        public bool showSidebar = true;
        public int sidebarTab = 0;
        public string activeSegmentName = "";
        public string activeLineName = "";

        [Header("Reclassification Dropdown State")]
        private bool showReclassDropdown = false;
        private Vector2 reclassDropdownPos = new Vector2(400, 42);
        private int selectedReclassClassIdx = 3; // default: [15] 杆塔
        private readonly byte[] reclassClassValues = new byte[] { 2, 3, 14, 15, 1 };
        private readonly string[] reclassClassNames = new string[] { "[2] 地面", "[3] 植被", "[14] 导线", "[15] 杆塔", "[1] 未分类" };

        [Header("Roam View Dropdown State")]
        private bool showRoamDropdown = false;
        private Vector2 roamDropdownPos = new Vector2(500, 42);
        private bool isClassLegendVisible = true;

        [Header("Import Configuration Modal State")]
        public bool showImportModal = false;
        public LasImportMetadata pendingImportMeta = null;
        public List<LasImportMetadata> pendingBatchMetas = new List<LasImportMetadata>();
        public LasImportMetadata masterProjectCRS = null;
        private int activeMetaEditIndex = -1;
        private Vector2 batchTableScroll;
        private int selectedCrsIndex = 0;
        private int selectedUtmZoneIndex = 0;
        private int selectedMeridianIndex = 0;
        private string editProvince = "浙江省";
        private string editCity = "杭州市";
        private string editLineName = "220kV 钱塘线";
        private string editSegmentName = "#17 - #18 档距";
        private Vector2 modalScroll;
        private int provincePage = 0;

        [Header("P2 Annotation State")]
        private List<int> selectedPointIndices = new List<int>();
        private bool isMarqueeDragging = false;
        private Vector2 marqueeStart;
        private Vector2 marqueeCurrent;
        private string annotationToast = "";
        private float toastTimer = 0f;
        private Vector2 annotationScroll;

        // Double-click trackers
        private string lastClickedSegment = "";
        private float lastSegmentClickTime = 0f;
        private TowerAnnotation lastClickedTower = null;
        private float lastTowerClickTime = 0f;
        private ConductorAnnotation lastClickedConductor = null;
        private float lastConductorClickTime = 0f;


        private Texture2D modalBg;
        private Texture2D overlayDarkBg;
        private GUIStyle modalStyle;
        private GUIStyle modalHeaderStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle noteStyle;

        private int selectedConductorIdx = 4; // default LGJ-240/30
        private float spanLength = 350f;
        private List<ConditionResult> mechanicsResults = null;
        private string governingCondId = "";

        [Header("P3 Mechanics & Engineering Modals")]
        public bool showWindSwingModal = false;
        public bool showStringingModal = false;
        public bool showComplianceModal = false;
        public bool showFormulaModal = false;
        private int formulaModalTab = 0; // 0: Conductor, 1: Insulator
        private Vector2 formulaModalScroll;

        // 4 Collapsible Panels in Mechanics Tab
        private bool openSecWeather = true;
        private bool openSecConductor = true;
        private bool openSecTower = true;
        private bool openSecInsulator = false;

        // Working conditions management
        private List<WorkingCondition> conditions = new List<WorkingCondition>();
        private string selectedConditionId = "max-wind";

        // Conductor customizable physical spec
        private ConductorSpec activeSpec = null;
        private float condWindShapeFactor = 1.0f;
        private float condIceWindCoeff = 1.0f;

        // Tower & Span fine-grained parameters
        private int leftTowerStructureType = 0;  // 0: angle_steel, 1: steel_pipe
        private int rightTowerStructureType = 0; // 0: angle_steel, 1: steel_pipe
        private float viewSpanLength = 350f;
        private float leftHorizontalSpan = 350f;
        private float rightHorizontalSpan = 350f;
        private float leftVerticalSpan = 350f;
        private float rightVerticalSpan = 350f;
        private float leftAttachmentHeight = 35f;
        private float rightAttachmentHeight = 35f;
        private int terrainCategory = 1; // 0: A, 1: B, 2: C, 3: D
        private float averageConductorHeight = 20f;
        private float windAngleDeg = 90f;
        private int lineType = 0; // 0: general, 1: large_span

        // Insulator fine-grained parameters
        private int activeInsulatorTab = 0; // 0: left, 1: right
        private bool syncRightInsulator = true;
        private int leftInsSpecIdx = 0;
        private InsulatorStringType leftInsStringType = InsulatorStringType.Single_I;
        private float leftVAngle = 90f;
        private float leftStructureHeight = 146f;
        private float leftUnitMass = 6.0f;
        private float leftCounterWeight = 0f;
        private float leftCreepageRatio = 25f;
        private float leftCustomWeight = 0f;
        private float leftCustomWindArea = 0f;

        private int rightInsSpecIdx = 0;
        private InsulatorStringType rightInsStringType = InsulatorStringType.Single_I;
        private float rightVAngle = 90f;
        private float rightStructureHeight = 146f;
        private float rightUnitMass = 6.0f;
        private float rightCounterWeight = 0f;
        private float rightCreepageRatio = 25f;
        private float rightCustomWeight = 0f;
        private float rightCustomWindArea = 0f;

        [Header("Floating Viewport Widgets State")]
        private bool isTelemetryExpanded = true;
        private Vector2 legendClassScroll;

        private readonly byte[] asprsClassIds = new byte[] { 15, 14, 16, 2, 3, 4, 5, 6, 1 };
        private readonly string[] asprsClassNames = new string[] {
            "杆塔 (Towers)", "导线 (Conductors)", "绝缘子 (Insulators)", "地面 (Ground)",
            "低矮植被 (Low Veg)", "中层植被 (Med Veg)", "高大树木 (High Veg)", "建筑物 (Buildings)", "未分类 (Unclass)"
        };
        private readonly Color[] asprsClassColors = new Color[] {
            new Color(1f, 0.88f, 0.2f),
            new Color(1f, 0.42f, 0.15f),
            new Color(0.82f, 0.26f, 1f),
            new Color(0.6f, 0.4f, 0.2f),
            new Color(0.2f, 0.8f, 0.2f),
            new Color(0.15f, 0.65f, 0.15f),
            new Color(0.1f, 0.5f, 0.1f),
            new Color(0.95f, 0.3f, 0.3f),
            new Color(0.65f, 0.65f, 0.65f)
        };

        private int selectedZoneIdx = 1; // default Zone II (典型气象区 II 区 无冰常规)
        private int selectedVoltageIdx = 2; // 220kV
        private readonly int[] voltageLevels = new int[] { 35, 110, 220, 330, 500, 750, 1000 };
        private readonly string[] voltageNames = new string[] { "35kV", "110kV", "220kV", "330kV", "500kV", "750kV", "1000kV" };
        private float kvValue = 1.0f; // 垂直档距与水平档距比值 Kv = l_v / l_h
        private float heightDifference = 0f; // 左右塔高差 h (m)

        // Wind swing modal state
        private float windSwingTestSpeed = 25f; // m/s
        private float windSwingCounterWeight = 0f; // kg
        private InsulatorStringType windSwingStringType = InsulatorStringType.Single_I;
        private int selectedInsulatorSpecIdx = 0; // XP-160
        private Vector2 windSwingModalScroll;

        // Stringing modal state
        private Vector2 stringingModalScroll;
        private List<StringingRow> stringingTableData = null;

        // Compliance modal state
        private Vector2 complianceModalScroll;
        private List<ComplianceAuditItem> complianceAuditItems = null;

        private Vector2 treeScroll;
        private Vector2 mechanicsScroll;
        private Vector2 drawerScroll;
        private float fps = 60f;
        private float fpsTimer = 0f;

        private Texture2D glassBg;
        private Texture2D topBarBg;
        private GUIStyle glassStyle;
        private GUIStyle headerStyle;
        private GUIStyle boldStyle;
        private GUIStyle badgeStyle;
        private GUIStyle dimStyle;

        void Awake()
        {
            Instance = this;
            InitTextures();
        }


        public bool IsPointerOverUI(Vector3 mousePos)
        {
            if (GUIUtility.hotControl != 0) return true;
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return true;

            bool is2DMapOpen = (OTA.Corridor.Preprocess.CorridorCutter2DWindow.Instance != null && OTA.Corridor.Preprocess.CorridorCutter2DWindow.Instance.IsOpen);
            if (is2DMapOpen)
            {
                return showSidebar && mousePos.x <= 355f;
            }

            float invY = Screen.height - mousePos.y;
            // 1. Top bar
            if (invY <= 45f) return true;

            // 2. Operation guide ticker
            float tickerLeft = showSidebar ? 358f : 8f;
            if (invY >= 45f && invY <= 72f && mousePos.x >= tickerLeft && mousePos.x <= Screen.width - 12f) return true;

            // 3. Full-width bottom status bar
            if (invY >= Screen.height - 30f) return true;

            // 4. Left sidebar
            if (showSidebar && mousePos.x <= 355f && invY >= 45f && invY <= Screen.height - 30f) return true;

            // 5. Roam View Compass Dropdown
            if (showRoamDropdown)
            {
                float px = Mathf.Clamp(roamDropdownPos.x, 10f, Screen.width - 160f);
                float py = Mathf.Clamp(roamDropdownPos.y, 44f, Screen.height - 185f);
                if (mousePos.x >= px && mousePos.x <= px + 155f && invY >= py && invY <= py + 185f) return true;
            }

            // 6. Classification legend & filter HUD (bottom-right above status bar)
            if (isClassLegendVisible)
            {
                if (mousePos.x >= Screen.width - 285f && mousePos.x <= Screen.width - 8f && invY >= Screen.height - 285f && invY <= Screen.height - 30f) return true;
            }
            else
            {
                if (mousePos.x >= Screen.width - 115f && mousePos.x <= Screen.width - 8f && invY >= Screen.height - 65f && invY <= Screen.height - 30f) return true;
            }

            // 7. Reclass dropdown popup
            if (showReclassDropdown)
            {
                float px = Mathf.Clamp(reclassDropdownPos.x, 10f, Screen.width - 120f);
                float py = Mathf.Clamp(reclassDropdownPos.y, 40f, Screen.height - 160f);
                if (mousePos.x >= px && mousePos.x <= px + 110f && invY >= py && invY <= py + 140f) return true;
            }

            // 8. Modals
            if (showImportModal || showWindSwingModal || showStringingModal || showComplianceModal || showFormulaModal) return true;

            return false;
        }

        void Start()
        {
            if (colorManager == null) colorManager = FindObjectOfType<CorridorColorManager>();
            if (sectionManager == null) sectionManager = FindObjectOfType<CorridorSectionManager>();
            if (overlayManager == null) overlayManager = FindObjectOfType<CorridorOverlayManager>();
            if (importer == null) importer = FindObjectOfType<LasImporter>();
            if (orbitCam == null) orbitCam = FindObjectOfType<P0OrbitCamera>();

            RecalculateMechanics();

            EnsureAnnotationSystem();
        }

        void Update()
        {
            if (AnnotationController.Instance == null)
            {
                EnsureAnnotationSystem();
            }
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.25f)
            {
                fps = 1.0f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
                fpsTimer = 0f;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchMode(PointCloudColorMode.RGB);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchMode(PointCloudColorMode.Classification);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchMode(PointCloudColorMode.Elevation);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchMode(PointCloudColorMode.Intensity);
            if (Input.GetKeyDown(KeyCode.H)) showSidebar = !showSidebar;
            if (Input.GetKeyDown(KeyCode.T) && overlayManager != null) overlayManager.SetTowersVisible(!overlayManager.showTowers);
            
            if (toastTimer > 0f)
            {
                toastTimer -= Time.unscaledDeltaTime;
                if (toastTimer <= 0f) annotationToast = "";
            }

            // Keyboard shortcut for reclassifying selected points
            if (selectedPointIndices.Count > 0 && colorManager != null)
            {
                if (Input.GetKeyDown(KeyCode.Keypad2) || (Input.GetKeyDown(KeyCode.Alpha2) && Input.GetKey(KeyCode.LeftAlt))) ApplyClassToSelected(2);
                if (Input.GetKeyDown(KeyCode.Keypad3) || (Input.GetKeyDown(KeyCode.Alpha3) && Input.GetKey(KeyCode.LeftAlt))) ApplyClassToSelected(3);
                if (Input.GetKeyDown(KeyCode.Keypad5) || (Input.GetKeyDown(KeyCode.Alpha5) && Input.GetKey(KeyCode.LeftAlt))) ApplyClassToSelected(5);
                if (Input.GetKeyDown(KeyCode.Keypad4) || (Input.GetKeyDown(KeyCode.Alpha4) && Input.GetKey(KeyCode.LeftAlt))) ApplyClassToSelected(14); // Conductor
                if (Input.GetKeyDown(KeyCode.Keypad1) || (Input.GetKeyDown(KeyCode.Alpha1) && Input.GetKey(KeyCode.LeftAlt))) ApplyClassToSelected(15); // Tower
            }
            if (Input.GetKeyDown(KeyCode.W) && overlayManager != null) overlayManager.SetWiresVisible(!overlayManager.showWires);
        }

        private void SwitchMode(PointCloudColorMode mode)
        {
            if (colorManager != null) colorManager.SetColorMode(mode);
        }

        private void ResetConditionsFromZone(int zoneIdx)
        {
            var zone = MeteorologyRegistry.Zones[Mathf.Clamp(zoneIdx, 0, MeteorologyRegistry.Zones.Length - 1)];
            conditions = zone.GenerateConditions();
            if (conditions.Count > 0) selectedConditionId = conditions[0].id;
        }

        private void ResetSpecFromRegistry(int condIdx)
        {
            var baseSpec = ConductorRegistry.AllConductors[Mathf.Clamp(condIdx, 0, ConductorRegistry.AllConductors.Length - 1)];
            activeSpec = new ConductorSpec
            {
                modelName = baseSpec.modelName,
                codeName = baseSpec.codeName,
                outerDiameter = baseSpec.outerDiameter,
                totalArea = baseSpec.totalArea,
                unitMass = baseSpec.unitMass,
                ratedStrength = baseSpec.ratedStrength,
                elasticModulus = baseSpec.elasticModulus,
                thermalExpansion = baseSpec.thermalExpansion,
                dcResistance = baseSpec.dcResistance,
                bundleNumber = baseSpec.bundleNumber
            };
        }

        private void RecalculateMechanics()
        {
            if (activeSpec == null) ResetSpecFromRegistry(selectedConductorIdx);
            if (conditions == null || conditions.Count == 0) ResetConditionsFromZone(selectedZoneIdx);

            var spec = activeSpec;
            var zone = MeteorologyRegistry.Zones[Mathf.Clamp(selectedZoneIdx, 0, MeteorologyRegistry.Zones.Length - 1)];

            float L = Mathf.Max(10f, spanLength);
            float E = spec.elasticModulus;
            float alpha = spec.thermalExpansion;
            float Tp = spec.ratedStrength * spec.bundleNumber;
            float S = spec.totalArea * spec.bundleNumber;
            float allowableMaxStress = (Tp / 2.5f) / S;
            float allowableAvgStress = (Tp * 0.25f) / S;

            // 1. 控制工况判别
            string bestCondId = "max-wind";
            float maxStressRatio = -1f;

            foreach (var candidate in conditions)
            {
                if (!candidate.isControlCandidate) continue;
                float gammaCand = ConductorMechanics.CalculateSpecificLoad(spec, candidate.windSpeed, candidate.iceThickness, L, out _, out _);
                float candLimit = candidate.name.Contains("年均") ? allowableAvgStress : allowableMaxStress;

                float maxProjectionRatio = 0f;
                foreach (var other in conditions)
                {
                    if (other.id == candidate.id) continue;
                    float gammaOther = ConductorMechanics.CalculateSpecificLoad(spec, other.windSpeed, other.iceThickness, L, out _, out _);
                    float otherLimit = other.name.Contains("年均") ? allowableAvgStress : allowableMaxStress;

                    float solved = ConductorMechanics.SolveStateEquation(candLimit, gammaCand, candidate.temp, gammaOther, other.temp, L, E, alpha);
                    float ratio = solved / otherLimit;
                    if (ratio > maxProjectionRatio) maxProjectionRatio = ratio;
                }

                if (maxProjectionRatio > maxStressRatio)
                {
                    maxStressRatio = maxProjectionRatio;
                    bestCondId = candidate.id;
                }
            }

            governingCondId = bestCondId;
            var govCond = conditions.Find(c => c.id == bestCondId) ?? conditions[0];
            float govLimit = govCond.name.Contains("年均") ? allowableAvgStress : allowableMaxStress;
            float govGamma = ConductorMechanics.CalculateSpecificLoad(spec, govCond.windSpeed, govCond.iceThickness, L, out _, out _);

            // 2. 结算全部工况力学参数
            mechanicsResults = new List<ConditionResult>();
            foreach (var cond in conditions)
            {
                float gamma = ConductorMechanics.CalculateSpecificLoad(spec, cond.windSpeed, cond.iceThickness, L, out float wGamma, out float vGamma);
                float stress = (cond.id == bestCondId)
                    ? govLimit
                    : ConductorMechanics.SolveStateEquation(govLimit, govGamma, govCond.temp, gamma, cond.temp, L, E, alpha);

                float tension = stress * S;
                float safetyFactor = Tp / Mathf.Max(1f, tension);
                float sag = (gamma * L * L) / (8f * stress);

                float windAngle = (Mathf.Atan2(wGamma, Mathf.Max(0.001f, vGamma)) * Mathf.Rad2Deg);
                float vertSag = sag * Mathf.Cos(windAngle * Mathf.Deg2Rad);
                float horizSwing = sag * Mathf.Sin(windAngle * Mathf.Deg2Rad);

                mechanicsResults.Add(new ConditionResult
                {
                    conditionId = cond.id,
                    conditionName = cond.name,
                    temp = cond.temp,
                    windSpeed = cond.windSpeed,
                    iceThickness = cond.iceThickness,
                    specificLoad = gamma,
                    stress = stress,
                    tension = tension,
                    safetyFactor = safetyFactor,
                    sag = sag,
                    windAngle = windAngle,
                    verticalSag = vertSag,
                    horizontalSwing = horizSwing,
                    isGoverning = (cond.id == bestCondId)
                });
            }

            // 3. 联动刷新架线放样表数据缓存
            stringingTableData = StringingCalculator.CalculateTable(spec, spanLength);
        }

        private void InitTextures()
        {
            IndustrialUITheme.EnsureTextures();

            glassBg = IndustrialUITheme.GetSolidTexture(IndustrialUITheme.BgSurface);
            topBarBg = IndustrialUITheme.GetSolidTexture(IndustrialUITheme.BgBase);
            modalBg = IndustrialUITheme.GetSolidTexture(IndustrialUITheme.BgCard);
            overlayDarkBg = IndustrialUITheme.GetSolidTexture(new Color(0.02f, 0.05f, 0.04f, 0.85f));
        }

        private void EnsureStyles()
        {
            IndustrialUITheme.EnsureInitialized();
            SystemFontHelper.EnsureSkinFont(GUI.skin, 12);

            if (glassStyle == null)
            {
                glassStyle = new GUIStyle(GUI.skin.box);
                glassStyle.normal.background = glassBg;
                glassStyle.border = new RectOffset(2, 2, 2, 2);

                headerStyle = SystemFontHelper.CreateChineseLabel(13, true, IndustrialUITheme.SgccEmerald);
                boldStyle = SystemFontHelper.CreateChineseLabel(12, true, IndustrialUITheme.TextPrimary);
                badgeStyle = SystemFontHelper.CreateChineseLabel(11, false, IndustrialUITheme.TextSecondary);
                badgeStyle.alignment = TextAnchor.MiddleRight;
                dimStyle = SystemFontHelper.CreateChineseLabel(11, false, IndustrialUITheme.TextDim);

                modalStyle = new GUIStyle(GUI.skin.box);
                modalStyle.normal.background = modalBg;
                modalStyle.border = new RectOffset(2, 2, 2, 2);

                modalHeaderStyle = SystemFontHelper.CreateChineseLabel(14, true, IndustrialUITheme.VoltageGold);
                subHeaderStyle = SystemFontHelper.CreateChineseLabel(12, true, IndustrialUITheme.ConductorCyan);
                noteStyle = SystemFontHelper.CreateChineseLabel(11, false, IndustrialUITheme.SgccEmerald);
            }
        }

        void OnGUI()
        {
            EnsureStyles();

            bool is2DMapOpen = (OTA.Corridor.Preprocess.CorridorCutter2DWindow.Instance != null && OTA.Corridor.Preprocess.CorridorCutter2DWindow.Instance.IsOpen);

            if (!is2DMapOpen)
            {
                DrawTopBar();
                DrawOperationGuideTicker();
            }

            if (showSidebar) DrawLeftSidebar();

            if (!is2DMapOpen)
            {
                DrawStatusHUD();
                DrawRoamCompassDropdown();
                DrawClassificationLegendHUD();
                Draw3DMeasurementInWorld();
                DrawMarqueeSelection();
                DrawReclassDropdownPopup();
                if (showWindSwingModal) DrawWindSwingModal();
                if (showStringingModal) DrawStringingModal();
                if (showComplianceModal) DrawComplianceModal();
                if (showFormulaModal) DrawFormulaModal();
            }

            if (showImportModal) DrawImportModal();
        }

        private void DrawTopBar()
        {
            float h = 44f;
            Rect barRect = new Rect(0, 0, Screen.width, h);
            IndustrialUITheme.DrawFrame(barRect, IndustrialUITheme.BgBase, IndustrialUITheme.BorderHairline, 1f);
            GUI.DrawTexture(new Rect(0, h - 1.5f, Screen.width, 1.5f), IndustrialUITheme.GetSolidTexture(IndustrialUITheme.BorderBright));

            GUILayout.BeginArea(new Rect(10, 6, Screen.width - 20, 32));
            GUILayout.BeginHorizontal();

            // 1. 系统标识与走廊项目面包屑
            string lineTag = (!string.IsNullOrEmpty(activeLineName))
                ? $" · {activeLineName} ({activeSegmentName})"
                : " · 默认状态: 未载入点云数据";
            GUILayout.Label("⚡ LiDAR 数字孪生" + lineTag, IndustrialUITheme.HeaderTitleStyle, GUILayout.Width(360));
            GUILayout.FlexibleSpace();

            // 2. 渲染模式：真实 RGB 与 高程渐变 单按钮互切
            bool isRgb = (colorManager == null || colorManager.CurrentMode == PointCloudColorMode.RGB);
            GUIStyle modeBtnStyle = isRgb ? IndustrialUITheme.ActiveButtonStyle : IndustrialUITheme.TechButtonStyle;
            string modeBtnText = isRgb ? "📷 真实RGB ⇄" : "🏔️ 高程渐变 ⇄";
            if (GUILayout.Button(modeBtnText, modeBtnStyle, GUILayout.Width(100), GUILayout.Height(26)))
            {
                if (colorManager != null)
                {
                    var nextMode = isRgb ? PointCloudColorMode.Elevation : PointCloudColorMode.RGB;
                    colorManager.SetColorMode(nextMode);
                }
            }

            GUILayout.Space(8);

            // 2.1 线框渲染模式 Toggle (Point 原生点云 vs Mesh 拓扑网格)
            bool isMeshMode = (colorManager != null && colorManager.CurrentRenderMode == RuntimeViewerDX11.RenderMode.Mesh);
            GUIStyle wireframeBtnStyle = isMeshMode ? IndustrialUITheme.ActiveButtonStyle : IndustrialUITheme.TechButtonStyle;
            string wireframeBtnText = isMeshMode ? "🕸️ 线框渲染 [开]" : "🕸️ 线框渲染 [关]";
            if (GUILayout.Button(wireframeBtnText, wireframeBtnStyle, GUILayout.Width(108), GUILayout.Height(26)))
            {
                if (colorManager != null)
                {
                    var targetMode = isMeshMode ? RuntimeViewerDX11.RenderMode.Point : RuntimeViewerDX11.RenderMode.Mesh;
                    colorManager.SwitchRenderMode(targetMode);
                }
            }

            GUILayout.Space(8);

            // 3. 空间标定交互工具栏 (漫游 / 标杆塔 / 标导线 / 选区修点 / 3D测量)
            AnnotationTargetMode currentAnnMode = AnnotationController.Instance != null ? AnnotationController.Instance.currentMode : AnnotationTargetMode.None;

            bool isRoam = (currentAnnMode == AnnotationTargetMode.None);
            GUIStyle roamStyle = isRoam ? IndustrialUITheme.ActiveButtonStyle : IndustrialUITheme.TechButtonStyle;
            if (GUILayout.Button("🖱️ 漫游 ▾", roamStyle, GUILayout.Width(76), GUILayout.Height(26)))
            {
                EnsureAnnotationSystem();
                if (AnnotationController.Instance != null) AnnotationController.Instance.SetMode(AnnotationTargetMode.None);
                showRoamDropdown = !showRoamDropdown;
                roamDropdownPos = new Vector2(Event.current.mousePosition.x - 30f, 44f);
            }

            bool isTower = (currentAnnMode == AnnotationTargetMode.Tower);
            GUIStyle towerStyle = isTower ? IndustrialUITheme.GoldButtonStyle : IndustrialUITheme.TechButtonStyle;
            if (GUILayout.Button("🗼 标杆塔", towerStyle, GUILayout.Width(74), GUILayout.Height(26)))
            {
                EnsureAnnotationSystem();
                if (AnnotationController.Instance != null) AnnotationController.Instance.SetMode(AnnotationTargetMode.Tower);
                showRoamDropdown = false;
                sidebarTab = 0;
                showSidebar = true;
                ShowToast("已开启杆塔标定模式，按住 Shift 移动鼠标定位靶点，左键点击塔顶");
            }

            bool isCond = (currentAnnMode == AnnotationTargetMode.Conductor);
            GUIStyle condStyle = isCond ? IndustrialUITheme.ActiveButtonStyle : IndustrialUITheme.TechButtonStyle;
            if (GUILayout.Button("⚡ 标导线", condStyle, GUILayout.Width(74), GUILayout.Height(26)))
            {
                EnsureAnnotationSystem();
                if (AnnotationController.Instance != null) AnnotationController.Instance.SetMode(AnnotationTargetMode.Conductor);
                showRoamDropdown = false;
                sidebarTab = 0;
                showSidebar = true;
                ShowToast("已开启导线标定模式，按住 Shift 依次点击两端挂点自动拟合");
            }

            bool isReclass = (currentAnnMode == AnnotationTargetMode.Reclassify);
            GUIStyle reclassStyle = isReclass ? IndustrialUITheme.ActiveButtonStyle : IndustrialUITheme.TechButtonStyle;
            if (GUILayout.Button("🔲 选区修点", reclassStyle, GUILayout.Width(82), GUILayout.Height(26)))
            {
                EnsureAnnotationSystem();
                if (AnnotationController.Instance != null) AnnotationController.Instance.SetMode(AnnotationTargetMode.Reclassify);
                showRoamDropdown = false;
                sidebarTab = 0;
                showSidebar = true;
                ShowToast("已开启选区修点模式，按住 Shift 拖拽矩形框选点云");
            }

            bool isMeasure = (currentAnnMode == AnnotationTargetMode.Measure);
            GUIStyle measureStyle = isMeasure ? IndustrialUITheme.ActiveButtonStyle : IndustrialUITheme.TechButtonStyle;
            if (GUILayout.Button("📏 3D测量", measureStyle, GUILayout.Width(78), GUILayout.Height(26)))
            {
                EnsureAnnotationSystem();
                if (AnnotationController.Instance != null) AnnotationController.Instance.SetMode(AnnotationTargetMode.Measure);
                showRoamDropdown = false;
                ShowToast("已开启 3D 测量模式，按住 Shift 依次点击测点 A 与测点 B，在三维视口中查看原位尺寸");
            }

            GUILayout.Space(8);

            // 4. 快速动作与历史撤销
            if (GUILayout.Button("🏷️ 特征提取", IndustrialUITheme.TechButtonStyle, GUILayout.Width(84), GUILayout.Height(26)))
            {
                if (overlayManager != null && importer != null && colorManager != null)
                {
                    var v = importer.GetComponent<RuntimeViewerDX11>();
                    overlayManager.ExtractFromRealPointCloud(v, colorManager.PointClasses);
                }
            }

            if (GUILayout.Button("↩ 撤销", IndustrialUITheme.TechButtonStyle, GUILayout.Width(54), GUILayout.Height(26)))
            {
                if (AnnotationHistory.Instance != null) AnnotationHistory.Instance.Undo();
            }

            // 选区修点快捷下拉与赋类按钮
            if (currentAnnMode == AnnotationTargetMode.Reclassify || selectedPointIndices.Count > 0)
            {
                if (GUILayout.Button(reclassClassNames[selectedReclassClassIdx] + " ▾", IndustrialUITheme.TechButtonStyle, GUILayout.Width(82), GUILayout.Height(26)))
                {
                    reclassDropdownPos = new Vector2(Event.current.mousePosition.x - 30f, 44f);
                    showReclassDropdown = !showReclassDropdown;
                }
                if (selectedPointIndices.Count > 0)
                {
                    if (GUILayout.Button($"✓ 赋类({selectedPointIndices.Count})", IndustrialUITheme.ActiveButtonStyle, GUILayout.Width(84), GUILayout.Height(26)))
                    {
                        ApplyClassToSelected(reclassClassValues[selectedReclassClassIdx]);
                        showReclassDropdown = false;
                    }
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawLeftSidebar()
        {
            bool is2DMapOpen = (OTA.Corridor.Preprocess.CorridorCutter2DWindow.Instance != null && OTA.Corridor.Preprocess.CorridorCutter2DWindow.Instance.IsOpen);
            float w = 350f;
            float top = is2DMapOpen ? 8f : 45f;
            float h = is2DMapOpen ? (Screen.height - 16f) : (Screen.height - top - 32f);
            Rect sideRect = new Rect(6, top, w, h);
            IndustrialUITheme.DrawFrame(sideRect, IndustrialUITheme.BgSurface, IndustrialUITheme.BorderHairline, 1f);

            GUILayout.BeginArea(new Rect(sideRect.x + 8, sideRect.y + 8, sideRect.width - 16, sideRect.height - 16));

            GUILayout.BeginHorizontal();
            GUILayout.Label("🎛️ 走廊主控台", IndustrialUITheme.HeaderTitleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", IndustrialUITheme.TechButtonStyle, GUILayout.Width(26), GUILayout.Height(22))) showSidebar = false;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("📂 导入点云(多选)", IndustrialUITheme.ActiveButtonStyle, GUILayout.Height(28)))
            {
                string[] picked = NativeFileBrowserHelper.OpenMultipleLasFilesDialog();
                if (picked != null && picked.Length > 0)
                {
                    OpenBatchImportModal(picked);
                }
            }
            if (GUILayout.Button("📁 目录批量", IndustrialUITheme.TechButtonStyle, GUILayout.Width(76), GUILayout.Height(28)))
            {
                string folder = NativeFileBrowserHelper.OpenFolderPanel();
                if (!string.IsNullOrEmpty(folder))
                {
                    string[] scanned = NativeFileBrowserHelper.ScanFolderForLas(folder);
                    if (scanned != null && scanned.Length > 0)
                    {
                        OpenBatchImportModal(scanned);
                    }
                    else
                    {
                        ShowToast("所选目录中未发现 .las 或 .laz 点云文件");
                    }
                }
            }
            if (importer != null && importer.IsLoaded)
            {
                if (GUILayout.Button("✕ 清空", IndustrialUITheme.DangerButtonStyle, GUILayout.Width(50), GUILayout.Height(28)))
                {
                    importer.Unload();
                    activeSegmentName = "";
                    activeLineName = "";
                    ShowToast("已清空中央区域点云及电力覆盖层");
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUIStyle tab0Style = (sidebarTab == 0) ? IndustrialUITheme.ActiveButtonStyle : IndustrialUITheme.TechButtonStyle;
            if (GUILayout.Button("走廊层级树", tab0Style, GUILayout.Height(26))) sidebarTab = 0;

            GUIStyle tab1Style = (sidebarTab == 1) ? IndustrialUITheme.ActiveButtonStyle : IndustrialUITheme.TechButtonStyle;
            if (GUILayout.Button("工况力学", tab1Style, GUILayout.Height(26))) sidebarTab = 1;
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            if (sidebarTab == 0)
            {
                DrawHierarchyTab();
            }
            else
            {
                DrawMechanicsTab();
            }

            GUILayout.EndArea();
        }

        private void DrawHierarchyTab()
        {
            var roots = CorridorHierarchyManager.GetOrBuildDefaultHierarchy();

            GUILayout.BeginHorizontal();
            GUILayout.Label("走廊资产四级拓扑 (省/市/线路/档距):", boldStyle);
            GUILayout.FlexibleSpace();
            if (roots != null && roots.Count > 0)
            {
                if (GUILayout.Button("清空树", GUILayout.Width(52), GUILayout.Height(20)))
                {
                    CorridorHierarchyManager.ClearHierarchy();
                    activeSegmentName = "";
                    activeLineName = "";
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            treeScroll = GUILayout.BeginScrollView(treeScroll);

            if (roots == null || roots.Count == 0)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Space(12);
                GUILayout.Label("🌱 层级树暂无点云走廊数据", boldStyle);
                GUILayout.Space(6);
                GUILayout.Label("请点击下方【📂 导入本地点云数据 (LAS/LAZ)】：", subHeaderStyle);
                GUILayout.Label("• 可直接挂载为单档距走廊节点；", noteStyle);
                GUILayout.Label("• 或进入【天地图标定】进行流式分段切分并动态上树。", noteStyle);
                GUILayout.Space(12);
                GUILayout.EndVertical();
            }
            else
            {
                foreach (var prov in roots)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.BeginHorizontal();
                    string arrow = prov.isExpanded ? "▾ " : "▸ ";
                    if (GUILayout.Button(arrow + prov.name, GUI.skin.label, GUILayout.ExpandWidth(true)))
                    {
                        prov.isExpanded = !prov.isExpanded;
                    }
                    GUILayout.EndHorizontal();

                    if (prov.isExpanded)
                {
                    foreach (var city in prov.children)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(14);
                        string cityArrow = city.isExpanded ? "▾ " : "▸ ";
                        if (GUILayout.Button(cityArrow + city.name, GUI.skin.label, GUILayout.ExpandWidth(true)))
                        {
                            city.isExpanded = !city.isExpanded;
                        }
                        GUILayout.EndHorizontal();

                        if (city.isExpanded)
                        {
                            foreach (var line in city.children)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(28);
                                string lineArrow = line.isExpanded ? "▾ " : "▸ ";
                                if (GUILayout.Button(lineArrow + line.name, GUI.skin.label, GUILayout.ExpandWidth(true)))
                                {
                                    var tm = TowerAnnotator.Instance; if (tm != null) tm.activeTower = null;
                                    var cm = ConductorAnnotator.Instance; if (cm != null) cm.activeConductor = null;
                                    line.isExpanded = !line.isExpanded;
                                }
                                GUILayout.EndHorizontal();

                                if (line.isExpanded)
                                {
                                    foreach (var segNode in line.children)
                                    {
                                        var seg = segNode.segmentData;
                                        if (seg == null) continue;

                                        GUILayout.BeginVertical(GUI.skin.box);
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Space(32);
                                        string segArrow = segNode.isExpanded ? "▾ " : "▸ ";
                                        bool isCur = (seg.name == activeSegmentName);
                                        if (isCur) GUI.color = new Color(0.3f, 1f, 0.5f);

                                         if (GUILayout.Button(segArrow + seg.name, GUI.skin.label, GUILayout.ExpandWidth(true)))
                                         {
                                             var tm = TowerAnnotator.Instance; if (tm != null) tm.activeTower = null;
                                             var cm = ConductorAnnotator.Instance; if (cm != null) cm.activeConductor = null;

                                             // When 2D map is open, clicking segment auto-focuses the map to this span
                                             if (OTA.Corridor.Preprocess.CorridorCutter2DWindow.Instance != null && OTA.Corridor.Preprocess.CorridorCutter2DWindow.Instance.IsOpen)
                                             {
                                                 OTA.Corridor.Preprocess.CorridorCutter2DWindow.Instance.FocusOnSpan(seg.startTower, seg.endTower);
                                             }

                                             float now = Time.realtimeSinceStartup;
                                             if (lastClickedSegment == seg.name && (now - lastSegmentClickTime < 0.4f))
                                             {
                                                 // Double Click: Load Point Cloud
                                                 if (importer != null && !string.IsNullOrEmpty(seg.lasPath) && File.Exists(seg.lasPath))
                                                 {
                                                     importer.Load(seg.lasPath);
                                                     activeSegmentName = seg.name;
                                                     activeLineName = seg.lineName;
                                                     ShowToast($"★ 双击载入点云: {seg.name}");
                                                 }
                                                 else if (string.IsNullOrEmpty(seg.lasPath))
                                                 {
                                                     ShowToast($"⚠️ 该档距为标绘规划通道，尚未完成流式切片 (请在地图上点击【🚀 完成标定并流式切割】)");
                                                 }
                                                 else
                                                 {
                                                     ShowToast($"⚠️ 点云切片文件不存在: {Path.GetFileName(seg.lasPath)}");
                                                 }
                                                 lastSegmentClickTime = 0f;
                                             }
                                             else
                                             {
                                                 // Single Click: Toggle expansion
                                                 segNode.isExpanded = !segNode.isExpanded;
                                                 lastClickedSegment = seg.name;
                                                 lastSegmentClickTime = now;
                                             }
                                         }
                                         GUI.color = Color.white;

                                        if (isCur)
                                        {
                                            GUI.color = new Color(0.3f, 1f, 0.5f);
                                            GUILayout.Label("●当前", boldStyle, GUILayout.Width(38));
                                            GUI.color = Color.white;
                                        }
                                        else if (!seg.isLoaded || !File.Exists(seg.lasPath))
                                        {
                                            GUI.color = new Color(1f, 0.75f, 0.2f);
                                            GUILayout.Label(string.Format("○待切({0:F0}m)", seg.length), dimStyle, GUILayout.Width(66));
                                            GUI.color = Color.white;
                                        }
                                        else
                                        {
                                            GUI.color = new Color(0.4f, 0.8f, 1f);
                                            GUILayout.Label(string.Format("{0:F1}万点", seg.pointCount / 10000f), dimStyle, GUILayout.Width(46));
                                            GUI.color = Color.white;
                                        }

                                        if (GUILayout.Button("🎯", GUILayout.Width(26), GUILayout.Height(19)))
                                        {
                                            var tm = TowerAnnotator.Instance; if (tm != null) tm.activeTower = null;
                                            var cm = ConductorAnnotator.Instance; if (cm != null) cm.activeConductor = null;
                                            if (importer != null)
                                            {
                                                var v = importer.GetComponent<RuntimeViewerDX11>();
                                                if (v != null)
                                                {
                                                    var panCam = FindObjectOfType<Metervara.Interaction.PanZoomOrbitMouse>();
                                                    if (panCam != null) panCam.FocusOnBounds(v.cloudBounds);
                                                    if (orbitCam != null) orbitCam.FocusOnBounds(v.cloudBounds);
                                                }
                                            }
                                        }
                                        GUILayout.EndHorizontal();

                                        // Render sub-assets under this segment
                                        if (segNode.isExpanded)
                                        {
                                            DrawSegmentAssets();
                                        }
                                        GUILayout.EndVertical();
                                    }
                                }
                            }
                        }
                    }
                }
                GUILayout.EndVertical();
            }
            }
            GUILayout.EndScrollView();

            // ==========================================
            // 廊道统一成果持久化与要素批量着色统一控制条
            // ==========================================
            GUILayout.Space(6);
            GUILayout.BeginVertical(GUI.skin.box);

            var towerMgr = TowerAnnotator.Instance;
            var condMgr = ConductorAnnotator.Instance;
            int totalTowers = towerMgr != null ? towerMgr.towers.Count : 0;
            int totalConds = condMgr != null ? condMgr.conductors.Count : 0;

            // 1. Unified Batch Colorization Button
            GUI.backgroundColor = new Color(0.1f, 0.75f, 0.85f);
            string batchColorBtnText = $"🎨 廊道要素一键赋类着色 ({totalTowers}杆塔 + {totalConds}导线)";
            if (GUILayout.Button(batchColorBtnText, GUILayout.Height(28)))
            {
                BatchColorizeCorridorElements();
            }
            GUI.backgroundColor = Color.white;

            // 2. Point Cloud Box Selection Reclassification (if points selected)
            if (selectedPointIndices.Count > 0)
            {
                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"🔲 选区修点 ({selectedPointIndices.Count}点):", boldStyle);
                if (GUILayout.Button(reclassClassNames[selectedReclassClassIdx] + " ▾", GUILayout.Width(88), GUILayout.Height(22)))
                {
                    reclassDropdownPos = new Vector2(Event.current.mousePosition.x - 30f, Event.current.mousePosition.y + 24f);
                    showReclassDropdown = !showReclassDropdown;
                }
                GUI.backgroundColor = new Color(0.2f, 0.85f, 0.45f);
                if (GUILayout.Button("✓ 赋类", GUILayout.Width(50), GUILayout.Height(22)))
                {
                    ApplyClassToSelected(reclassClassValues[selectedReclassClassIdx]);
                    showReclassDropdown = false;
                }
                GUI.backgroundColor = Color.white;
                if (GUILayout.Button("清空", GUILayout.Width(38), GUILayout.Height(22)))
                {
                    selectedPointIndices.Clear();
                    showReclassDropdown = false;
                }
                GUILayout.EndHorizontal();
            }

            // 3. Unified Export & Persistence Actions
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.15f, 0.8f, 0.45f);
            if (GUILayout.Button("💾 写回LAS", GUILayout.Height(24))) SaveLasFile();
            GUI.backgroundColor = new Color(0.2f, 0.65f, 1f);
            if (GUILayout.Button("📦 导出训练集", GUILayout.Height(24))) ExportTrainingDataset();

            var hist = AnnotationHistory.Instance;
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("↩", GUILayout.Width(26), GUILayout.Height(24))) { if (hist != null) hist.Undo(); }
            if (GUILayout.Button("↪", GUILayout.Width(26), GUILayout.Height(24))) { if (hist != null) hist.Redo(); }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }


        private void DrawSegmentAssets()
        {
            var towerMgr = TowerAnnotator.Instance;
            var condMgr = ConductorAnnotator.Instance;

            // Toast feedback
            if (!string.IsNullOrEmpty(annotationToast))
            {
                GUI.color = new Color(0.3f, 1f, 0.4f);
                GUILayout.Label(" " + annotationToast, noteStyle);
                GUI.color = Color.white;
            }

            // 1. Towers Section (Clean Tree Rows)
            int tCount = (towerMgr != null) ? towerMgr.towers.Count : 0;
            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            GUILayout.Label($"标定杆塔 ({tCount} 基):", boldStyle);
            GUILayout.EndHorizontal();

            if (towerMgr != null && towerMgr.towers.Count > 0)
            {
                for (int i = 0; i < towerMgr.towers.Count; i++)
                {
                    var t = towerMgr.towers[i];
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(28);

                    bool vis = GUILayout.Toggle(t.isVisible, "", GUILayout.Width(16));
                    if (vis != t.isVisible) t.isVisible = vis;

                    bool isAct = (towerMgr.activeTower == t);
                    GUI.color = isAct ? new Color(1f, 0.9f, 0.2f) : Color.white;
                    string towerTitle = $"🗼 {t.towerNo} 杆塔 (H={t.totalHeight:F0}m)";
                    if (GUILayout.Button(towerTitle, GUI.skin.label, GUILayout.ExpandWidth(true)))
                    {
                        float now = Time.realtimeSinceStartup;
                        var pz = Metervara.Interaction.PanZoomOrbitMouse.Instance;
                        if (lastClickedTower == t && (now - lastTowerClickTime < 0.4f))
                        {
                            // Double Click: Focus & Locate & SHOW bounding box
                            towerMgr.activeTower = t;
                            if (condMgr != null) condMgr.activeConductor = null;
                            if (pz != null) pz.FocusOnTower(t);
                            ShowToast($"★ 双击定位杆塔: {t.towerNo}");
                            lastTowerClickTime = 0f;
                        }
                        else
                        {
                            // Single Click: Focus & Locate, but HIDE bounding boxes
                            towerMgr.activeTower = null;
                            if (condMgr != null) condMgr.activeConductor = null;
                            if (pz != null) pz.FocusOnTower(t);
                            ShowToast($"定位杆塔: {t.towerNo}");
                            lastClickedTower = t;
                            lastTowerClickTime = now;
                        }
                    }
                    GUI.color = Color.white;

                    // Delete button
                    if (GUILayout.Button("✕", IndustrialUITheme.DangerButtonStyle, GUILayout.Width(22), GUILayout.Height(19)))
                    {
                        towerMgr.towers.RemoveAt(i);
                        if (towerMgr.activeTower == t) towerMgr.activeTower = null;
                        break;
                    }
                    GUILayout.EndHorizontal();
                }
            }

            // 2. Conductors Section (Clean Tree Rows)
            int cCount = (condMgr != null) ? condMgr.conductors.Count : 0;
            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            GUILayout.Label($"标定导线 ({cCount} 根):", boldStyle);
            GUILayout.EndHorizontal();

            if (condMgr != null && condMgr.conductors.Count > 0)
            {
                for (int i = 0; i < condMgr.conductors.Count; i++)
                {
                    var c = condMgr.conductors[i];
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(28);

                    bool vis = GUILayout.Toggle(c.isVisible, "", GUILayout.Width(16));
                    if (vis != c.isVisible) c.isVisible = vis;

                    bool isAct = (condMgr.activeConductor == c);
                    GUI.color = isAct ? new Color(1f, 0.95f, 0.2f) : Color.white;
                    string condTitle = $"⚡ {c.phaseName} (弧垂:{c.measuredSag:F1}m | Φ{c.estimatedDiameterMm:F0}mm)";
                    if (GUILayout.Button(condTitle, GUI.skin.label, GUILayout.ExpandWidth(true)))
                    {
                        float now = Time.realtimeSinceStartup;
                        var pz = Metervara.Interaction.PanZoomOrbitMouse.Instance;
                        if (lastClickedConductor == c && (now - lastConductorClickTime < 0.4f))
                        {
                            // Double Click: Focus & Locate & Select
                            condMgr.activeConductor = c;
                            if (towerMgr != null) towerMgr.activeTower = null;
                            if (pz != null) pz.FocusOnConductor(c);
                            ShowToast($"★ 双击定位导线: {c.phaseName}");
                            lastConductorClickTime = 0f;
                        }
                        else
                        {
                            // Single Click: Focus & Locate, but HIDE bounding boxes
                            if (towerMgr != null) towerMgr.activeTower = null;
                            condMgr.activeConductor = null;
                            if (pz != null) pz.FocusOnConductor(c);
                            ShowToast($"定位导线: {c.phaseName}");
                            lastClickedConductor = c;
                            lastConductorClickTime = now;
                        }
                    }
                    GUI.color = Color.white;

                    // Delete button
                    if (GUILayout.Button("✕", IndustrialUITheme.DangerButtonStyle, GUILayout.Width(22), GUILayout.Height(19)))
                    {
                        condMgr.conductors.RemoveAt(i);
                        if (condMgr.activeConductor == c) condMgr.activeConductor = null;
                        break;
                    }
                    GUILayout.EndHorizontal();
                }
            }

            // 3. Dedicated Inspector Card for Active Tower or Active Conductor
            DrawActiveAssetInspector();
        }

        private void DrawActiveAssetInspector()
        {
            var towerMgr = TowerAnnotator.Instance;
            var condMgr = ConductorAnnotator.Instance;

            var t = (towerMgr != null) ? towerMgr.activeTower : null;
            var c = (condMgr != null) ? condMgr.activeConductor : null;

            if (t == null && c == null) return;

            GUILayout.Space(6);
            GUILayout.BeginVertical(IndustrialUITheme.CardStyle);

            if (t != null)
            {
                // Tower Inspector Panel
                GUILayout.BeginHorizontal();
                GUILayout.Label($"🗼 选中杆塔: {t.towerNo}", IndustrialUITheme.SubHeaderStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("取消选中", IndustrialUITheme.TechButtonStyle, GUILayout.Width(68), GUILayout.Height(20)))
                {
                    towerMgr.activeTower = null;
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(2);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"全高: {t.totalHeight:F1}m", IndustrialUITheme.DimLabelStyle, GUILayout.Width(90));
                GUILayout.Label($"呼高: {t.nominalHeight:F1}m", IndustrialUITheme.DimLabelStyle, GUILayout.Width(90));
                GUILayout.Label($"偏角: {t.yawAngle:F0}°", IndustrialUITheme.DimLabelStyle);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"横担宽: {t.topBox.size.x:F1}m", IndustrialUITheme.DimLabelStyle, GUILayout.Width(90));
                GUILayout.Label($"塔身宽: {t.bodyBox.size.x:F1}m", IndustrialUITheme.DimLabelStyle);
                GUILayout.EndHorizontal();
            }
            else if (c != null)
            {
                // Conductor Inspector Panel
                GUILayout.BeginHorizontal();
                GUILayout.Label($"⚡ 选中导线: {c.phaseName}", IndustrialUITheme.SubHeaderStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("取消选中", IndustrialUITheme.TechButtonStyle, GUILayout.Width(68), GUILayout.Height(20)))
                {
                    condMgr.activeConductor = null;
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(2);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"档距: {c.horizontalSpan:F1}m", IndustrialUITheme.DimLabelStyle, GUILayout.Width(110));
                GUILayout.Label($"挂点高差: {c.heightDifference:F1}m", IndustrialUITheme.DimLabelStyle);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUI.color = new Color(1f, 0.4f, 0.4f);
                GUILayout.Label($"实测跨中弧垂: {c.measuredSag:F2} m", IndustrialUITheme.BodyLabelStyle, GUILayout.Width(160));
                GUI.color = Color.white;
                GUILayout.Label($"悬链常数 C: {c.catenaryC:F0}m", IndustrialUITheme.DimLabelStyle);
                GUILayout.EndHorizontal();

                GUILayout.Space(2);
                GUI.color = IndustrialUITheme.ConductorCyan;
                GUILayout.Label($"★ 反演导线外径: Φ {c.estimatedDiameterMm:F1} mm", IndustrialUITheme.BodyLabelStyle);
                GUILayout.Label($"★ 匹配规程型号: {c.conductorModel} (标称外径 {c.estimatedDiameterMm:F1}mm)", IndustrialUITheme.BodyLabelStyle);
                GUI.color = Color.white;

                string sampleInfo = (c.inlierPointCount > 0)
                    ? $"有效采样内点: {c.inlierPointCount} 点 (激光雷达点云物理拟合)"
                    : "有效采样内点: 采用 DL/T 5582 标准电力规程推荐值";
                GUILayout.Label(sampleInfo, IndustrialUITheme.DimLabelStyle);
            }

            GUILayout.EndVertical();
        }
        private void DrawMechanicsTab()
        {
            mechanicsScroll = GUILayout.BeginScrollView(mechanicsScroll);

            GUILayout.Label("DL/T 5582 导线力学与规程工程计算:", boldStyle);
            GUILayout.Space(2);

            // 1. 三大业务功能快捷入口
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.2f, 0.75f, 0.9f);
            if (GUILayout.Button("🌬️ 绝缘子风偏", GUILayout.Height(24)))
            {
                showWindSwingModal = true;
                P0OrbitCamera.BlockCameraInput = true;
            }
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.6f);
            if (GUILayout.Button("📋 架线放样表", GUILayout.Height(24)))
            {
                showStringingModal = true;
                P0OrbitCamera.BlockCameraInput = true;
            }
            GUI.backgroundColor = new Color(1f, 0.7f, 0.2f);
            if (GUILayout.Button("🛡️ 合规核查", GUILayout.Height(24)))
            {
                showComplianceModal = true;
                P0OrbitCamera.BlockCameraInput = true;
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // 电压等级快速切换
            GUILayout.BeginHorizontal();
            GUILayout.Label("电压等级:", dimStyle, GUILayout.Width(60));
            int newVIdx = GUILayout.SelectionGrid(selectedVoltageIdx, voltageNames, 4);
            if (newVIdx != selectedVoltageIdx)
            {
                selectedVoltageIdx = newVIdx;
                var defSpec = ConductorRegistry.GetDefaultByVoltage(voltageLevels[selectedVoltageIdx]);
                for (int ci = 0; ci < ConductorRegistry.AllConductors.Length; ci++)
                {
                    if (ConductorRegistry.AllConductors[ci].modelName == defSpec.modelName)
                    {
                        selectedConductorIdx = ci;
                        ResetSpecFromRegistry(selectedConductorIdx);
                        break;
                    }
                }
                RecalculateMechanics();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // -------------------------------------------------------------
            // SECTION 1: 工况预设与气象参数调节
            // -------------------------------------------------------------
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUI.color = new Color(0.3f, 0.9f, 1f);
            if (GUILayout.Button((openSecWeather ? "▼" : "▶") + " ☁️ 工况预设与气象参数调节", GUI.skin.label))
            {
                openSecWeather = !openSecWeather;
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            if (openSecWeather)
            {
                GUILayout.Space(2);
                // 典型气象区一键预设
                GUILayout.BeginHorizontal();
                GUILayout.Label("气象区预设:", dimStyle, GUILayout.Width(72));
                if (GUILayout.Button("◀", GUILayout.Width(22)))
                {
                    selectedZoneIdx = (selectedZoneIdx - 1 + MeteorologyRegistry.Zones.Length) % MeteorologyRegistry.Zones.Length;
                    ResetConditionsFromZone(selectedZoneIdx);
                    RecalculateMechanics();
                }
                GUILayout.Label(MeteorologyRegistry.Zones[selectedZoneIdx].name, boldStyle);
                if (GUILayout.Button("▶", GUILayout.Width(22)))
                {
                    selectedZoneIdx = (selectedZoneIdx + 1) % MeteorologyRegistry.Zones.Length;
                    ResetConditionsFromZone(selectedZoneIdx);
                    RecalculateMechanics();
                }
                GUILayout.EndHorizontal();

                var activeZone = MeteorologyRegistry.Zones[selectedZoneIdx];
                GUILayout.Label($"  最大风速: {activeZone.maxWindSpeed:F0}m/s | 冰厚: {activeZone.designIceThickness:F0}mm | 最低: {activeZone.minTemp:F0}°C", dimStyle);

                GUILayout.Space(4);

                // 当前工况选择与新增
                if (conditions == null || conditions.Count == 0) ResetConditionsFromZone(selectedZoneIdx);
                int curCondIdx = Mathf.Max(0, conditions.FindIndex(c => c.id == selectedConditionId));
                if (curCondIdx >= conditions.Count) curCondIdx = 0;
                var curCond = conditions[curCondIdx];

                GUILayout.BeginHorizontal();
                GUILayout.Label("当前工况:", dimStyle, GUILayout.Width(60));
                if (GUILayout.Button("◀", GUILayout.Width(22)))
                {
                    curCondIdx = (curCondIdx - 1 + conditions.Count) % conditions.Count;
                    selectedConditionId = conditions[curCondIdx].id;
                }
                string condDisp = curCond.name + (curCond.id == governingCondId ? " ★" : "");
                GUILayout.Label(condDisp, boldStyle);
                if (GUILayout.Button("▶", GUILayout.Width(22)))
                {
                    curCondIdx = (curCondIdx + 1) % conditions.Count;
                    selectedConditionId = conditions[curCondIdx].id;
                }
                GUI.backgroundColor = new Color(0.2f, 0.7f, 0.9f);
                if (GUILayout.Button("+ 新增", GUILayout.Width(50), GUILayout.Height(20)))
                {
                    conditions.Add(new WorkingCondition("custom-" + System.DateTime.UtcNow.Ticks, "自定义工况-" + (conditions.Count + 1), 20f, 10f, 0f, true));
                    selectedConditionId = conditions[conditions.Count - 1].id;
                    RecalculateMechanics();
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();

                // 单工况详情与气象输入卡片
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                GUILayout.Label("工况名称:", dimStyle, GUILayout.Width(55));
                string newName = GUILayout.TextField(curCond.name, GUILayout.Width(100));
                if (newName != curCond.name) curCond.name = newName;

                bool newCandidate = GUILayout.Toggle(curCond.isControlCandidate, " 控制候选");
                if (newCandidate != curCond.isControlCandidate)
                {
                    curCond.isControlCandidate = newCandidate;
                    RecalculateMechanics();
                }

                if (curCond.id == governingCondId)
                {
                    GUI.color = new Color(0.2f, 0.85f, 1f);
                    GUILayout.Label("★ 控制工况", badgeStyle);
                    GUI.color = Color.white;
                }
                GUILayout.FlexibleSpace();
                if (conditions.Count > 1)
                {
                    GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                    if (GUILayout.Button("✕", GUILayout.Width(20), GUILayout.Height(18)))
                    {
                        conditions.RemoveAt(curCondIdx);
                        if (conditions.Count > 0) selectedConditionId = conditions[0].id;
                        RecalculateMechanics();
                    }
                    GUI.backgroundColor = Color.white;
                }
                GUILayout.EndHorizontal();

                // 气象输入条
                GUILayout.BeginHorizontal();
                GUILayout.Label($"气温 t: {curCond.temp:F0}°C", dimStyle, GUILayout.Width(90));
                float nT = GUILayout.HorizontalSlider(curCond.temp, -40f, 50f);
                if (Mathf.Abs(nT - curCond.temp) > 0.5f) { curCond.temp = Mathf.Round(nT); RecalculateMechanics(); }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"风速 v: {curCond.windSpeed:F0}m/s", dimStyle, GUILayout.Width(90));
                float nV = GUILayout.HorizontalSlider(curCond.windSpeed, 0f, 45f);
                if (Mathf.Abs(nV - curCond.windSpeed) > 0.5f) { curCond.windSpeed = Mathf.Round(nV); RecalculateMechanics(); }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"覆冰 b: {curCond.iceThickness:F0}mm", dimStyle, GUILayout.Width(90));
                float nIce = GUILayout.HorizontalSlider(curCond.iceThickness, 0f, 40f);
                if (Mathf.Abs(nIce - curCond.iceThickness) > 0.5f) { curCond.iceThickness = Mathf.Round(nIce); RecalculateMechanics(); }
                GUILayout.EndHorizontal();

                // 当前工况物理计算实时预览
                var curRes = mechanicsResults?.Find(r => r.conditionId == curCond.id);
                if (curRes != null)
                {
                    GUILayout.Space(2);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"弧垂: {curRes.sag:F2}m", dimStyle);
                    GUILayout.Label($"张力: {(curRes.tension / 1000f):F1}kN", dimStyle);
                    GUILayout.Label($"应力: {curRes.stress:F1}N/mm²", dimStyle);
                    GUI.color = curRes.safetyFactor >= 2.5f ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.35f, 0.35f);
                    GUILayout.Label($"K: {curRes.safetyFactor:F2}", boldStyle);
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // -------------------------------------------------------------
            // SECTION 2: 导线规格与物理力学参数
            // -------------------------------------------------------------
            if (activeSpec == null) ResetSpecFromRegistry(selectedConductorIdx);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUI.color = new Color(0.3f, 0.9f, 1f);
            if (GUILayout.Button((openSecConductor ? "▼" : "▶") + " ⚡ 导线规格与物理力学参数", GUI.skin.label))
            {
                openSecConductor = !openSecConductor;
            }
            GUI.color = Color.white;
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = new Color(0.15f, 0.45f, 0.75f);
            if (GUILayout.Button("📖 计算公式", GUILayout.Width(76), GUILayout.Height(20)))
            {
                formulaModalTab = 0;
                showFormulaModal = true;
                P0OrbitCamera.BlockCameraInput = true;
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            if (openSecConductor)
            {
                GUILayout.Space(2);
                // 国标导线切换
                GUILayout.BeginHorizontal();
                GUILayout.Label("常用规格:", dimStyle, GUILayout.Width(60));
                if (GUILayout.Button("◀", GUILayout.Width(22)))
                {
                    selectedConductorIdx = (selectedConductorIdx - 1 + ConductorRegistry.AllConductors.Length) % ConductorRegistry.AllConductors.Length;
                    ResetSpecFromRegistry(selectedConductorIdx);
                    RecalculateMechanics();
                }
                GUILayout.Label(activeSpec.modelName + " (" + activeSpec.codeName + ")", boldStyle);
                if (GUILayout.Button("▶", GUILayout.Width(22)))
                {
                    selectedConductorIdx = (selectedConductorIdx + 1) % ConductorRegistry.AllConductors.Length;
                    ResetSpecFromRegistry(selectedConductorIdx);
                    RecalculateMechanics();
                }
                GUILayout.EndHorizontal();

                // 物理参数微调表 (6参数)
                GUILayout.BeginHorizontal();
                GUILayout.Label($"截面 S: {activeSpec.totalArea:F1}mm²", dimStyle, GUILayout.Width(130));
                float nArea = GUILayout.HorizontalSlider(activeSpec.totalArea, 50f, 1000f);
                if (Mathf.Abs(nArea - activeSpec.totalArea) > 1f) { activeSpec.totalArea = nArea; RecalculateMechanics(); }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"外径 d: {activeSpec.outerDiameter:F1}mm", dimStyle, GUILayout.Width(130));
                float nDia = GUILayout.HorizontalSlider(activeSpec.outerDiameter, 8f, 45f);
                if (Mathf.Abs(nDia - activeSpec.outerDiameter) > 0.2f) { activeSpec.outerDiameter = nDia; RecalculateMechanics(); }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"单重 m: {activeSpec.unitMass:F3}kg/m", dimStyle, GUILayout.Width(130));
                float nMass = GUILayout.HorizontalSlider(activeSpec.unitMass, 0.2f, 3.5f);
                if (Mathf.Abs(nMass - activeSpec.unitMass) > 0.02f) { activeSpec.unitMass = nMass; RecalculateMechanics(); }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"拉断力 Tp: {(activeSpec.ratedStrength / 1000f):F1}kN", dimStyle, GUILayout.Width(130));
                float nTp = GUILayout.HorizontalSlider(activeSpec.ratedStrength / 1000f, 20f, 300f);
                if (Mathf.Abs(nTp - activeSpec.ratedStrength / 1000f) > 1f) { activeSpec.ratedStrength = nTp * 1000f; RecalculateMechanics(); }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"模量 E: {(activeSpec.elasticModulus / 1000f):F0}kN/mm²", dimStyle, GUILayout.Width(130));
                float nE = GUILayout.HorizontalSlider(activeSpec.elasticModulus, 50000f, 100000f);
                if (Mathf.Abs(nE - activeSpec.elasticModulus) > 500f) { activeSpec.elasticModulus = nE; RecalculateMechanics(); }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"膨胀 α: {(activeSpec.thermalExpansion * 1e6f):F1}×10⁻⁶", dimStyle, GUILayout.Width(130));
                float nAlpha = GUILayout.HorizontalSlider(activeSpec.thermalExpansion * 1e6f, 12f, 25f);
                if (Mathf.Abs(nAlpha - activeSpec.thermalExpansion * 1e6f) > 0.2f) { activeSpec.thermalExpansion = nAlpha * 1e-6f; RecalculateMechanics(); }
                GUILayout.EndHorizontal();

                // 精细风荷载参数 (GB 50545 9.3)
                GUILayout.Space(2);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                GUILayout.Label("🌀 风荷载比载参数 (GB 50545 9.3):", boldStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("推导公式", GUI.skin.label))
                {
                    formulaModalTab = 0;
                    showFormulaModal = true;
                    P0OrbitCamera.BlockCameraInput = true;
                }
                GUILayout.EndHorizontal();

                // 分裂根数 n
                GUILayout.BeginHorizontal();
                GUILayout.Label("分裂数 n:", dimStyle, GUILayout.Width(60));
                string[] bundleOpts = new string[] { "1", "2", "3", "4", "6", "8" };
                int[] bundleVals = new int[] { 1, 2, 3, 4, 6, 8 };
                int curBIdx = 0;
                for (int bi = 0; bi < bundleVals.Length; bi++) if (bundleVals[bi] == activeSpec.bundleNumber) curBIdx = bi;
                int newBIdx = GUILayout.SelectionGrid(curBIdx, bundleOpts, 6);
                if (newBIdx != curBIdx)
                {
                    activeSpec.bundleNumber = bundleVals[newBIdx];
                    RecalculateMechanics();
                }
                GUILayout.EndHorizontal();

                // 体型系数
                GUILayout.BeginHorizontal();
                string shapeTip = activeSpec.outerDiameter >= 17f ? "d≥17mm(1.0)" : "d<17mm(1.1)";
                GUILayout.Label($"体型系数 μ_sc ({shapeTip}): {condWindShapeFactor:F2}", dimStyle, GUILayout.Width(185));
                float nShape = GUILayout.HorizontalSlider(condWindShapeFactor, 0.8f, 1.4f);
                if (Mathf.Abs(nShape - condWindShapeFactor) > 0.02f) { condWindShapeFactor = nShape; RecalculateMechanics(); }
                GUILayout.EndHorizontal();

                // 覆冰增大系数
                GUILayout.BeginHorizontal();
                GUILayout.Label($"覆冰增大系数 B₁: {condIceWindCoeff:F2}", dimStyle, GUILayout.Width(185));
                float nB1 = GUILayout.HorizontalSlider(condIceWindCoeff, 1.0f, 1.3f);
                if (Mathf.Abs(nB1 - condIceWindCoeff) > 0.02f) { condIceWindCoeff = nB1; RecalculateMechanics(); }
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // -------------------------------------------------------------
            // SECTION 3: 杆塔结构型式与档距参数
            // -------------------------------------------------------------
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUI.color = new Color(0.3f, 0.9f, 1f);
            if (GUILayout.Button((openSecTower ? "▼" : "▶") + " 🗼 杆塔结构型式与档距参数", GUI.skin.label))
            {
                openSecTower = !openSecTower;
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            if (openSecTower)
            {
                GUILayout.Space(2);
                // 杆塔材质型式
                GUILayout.Label("杆塔结构材质型式:", boldStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label("左塔(A):", dimStyle, GUILayout.Width(55));
                leftTowerStructureType = GUILayout.SelectionGrid(leftTowerStructureType, new string[] { "角钢塔", "钢管塔" }, 2);
                GUILayout.Label("右塔(B):", dimStyle, GUILayout.Width(55));
                rightTowerStructureType = GUILayout.SelectionGrid(rightTowerStructureType, new string[] { "角钢塔", "钢管塔" }, 2);
                GUILayout.EndHorizontal();

                // 一键统设按钮
                GUILayout.BeginHorizontal();
                GUILayout.Label("一键统设:", dimStyle, GUILayout.Width(55));
                if (GUILayout.Button("两端角钢塔", GUILayout.Height(20)))
                {
                    leftTowerStructureType = 0;
                    rightTowerStructureType = 0;
                }
                if (GUILayout.Button("两端钢管塔", GUILayout.Height(20)))
                {
                    leftTowerStructureType = 1;
                    rightTowerStructureType = 1;
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(4);

                // 档距与代表档距
                GUILayout.BeginHorizontal();
                GUILayout.Label($"代表档距 L_r: {spanLength:F0}m", dimStyle, GUILayout.Width(130));
                float nSpan = GUILayout.HorizontalSlider(spanLength, 50f, 1200f);
                if (Mathf.Abs(nSpan - spanLength) > 1f) { spanLength = Mathf.Round(nSpan); RecalculateMechanics(); }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"视图档距 L: {viewSpanLength:F0}m", dimStyle, GUILayout.Width(130));
                float nView = GUILayout.HorizontalSlider(viewSpanLength, 50f, 1200f);
                if (Mathf.Abs(nView - viewSpanLength) > 1f) { viewSpanLength = Mathf.Round(nView); }
                GUILayout.EndHorizontal();

                // 前后杆塔水平/垂直档距 (l_h & l_v)
                GUILayout.Space(2);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                GUILayout.Label("前后杆塔水平/垂直档距 (l_h & l_v):", boldStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("一键按视图同步", GUI.skin.label))
                {
                    leftHorizontalSpan = viewSpanLength;
                    rightHorizontalSpan = viewSpanLength;
                    leftVerticalSpan = viewSpanLength;
                    rightVerticalSpan = viewSpanLength;
                    kvValue = 1.0f;
                    RecalculateMechanics();
                }
                GUILayout.EndHorizontal();

                // 左塔水平与垂直档距
                GUILayout.BeginHorizontal();
                GUILayout.Label($"左塔水平 l_hA: {leftHorizontalSpan:F0}m", dimStyle, GUILayout.Width(135));
                float nLhA = GUILayout.HorizontalSlider(leftHorizontalSpan, 50f, 1000f);
                if (Mathf.Abs(nLhA - leftHorizontalSpan) > 1f) { leftHorizontalSpan = Mathf.Round(nLhA); RecalculateMechanics(); }
                GUILayout.EndHorizontal();

                float kvA = leftHorizontalSpan > 0 ? leftVerticalSpan / leftHorizontalSpan : 1.0f;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"左塔垂直 l_vA: {leftVerticalSpan:F0}m (Kv={kvA:F2})", dimStyle, GUILayout.Width(185));
                float nLvA = GUILayout.HorizontalSlider(leftVerticalSpan, 50f, 1000f);
                if (Mathf.Abs(nLvA - leftVerticalSpan) > 1f) { leftVerticalSpan = Mathf.Round(nLvA); kvValue = kvA; RecalculateMechanics(); }
                GUILayout.EndHorizontal();

                // 右塔水平与垂直档距
                GUILayout.BeginHorizontal();
                GUILayout.Label($"右塔水平 l_hB: {rightHorizontalSpan:F0}m", dimStyle, GUILayout.Width(135));
                float nLhB = GUILayout.HorizontalSlider(rightHorizontalSpan, 50f, 1000f);
                if (Mathf.Abs(nLhB - rightHorizontalSpan) > 1f) { rightHorizontalSpan = Mathf.Round(nLhB); RecalculateMechanics(); }
                GUILayout.EndHorizontal();

                float kvB = rightHorizontalSpan > 0 ? rightVerticalSpan / rightHorizontalSpan : 1.0f;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"右塔垂直 l_vB: {rightVerticalSpan:F0}m (Kv={kvB:F2})", dimStyle, GUILayout.Width(185));
                float nLvB = GUILayout.HorizontalSlider(rightVerticalSpan, 50f, 1000f);
                if (Mathf.Abs(nLvB - rightVerticalSpan) > 1f) { rightVerticalSpan = Mathf.Round(nLvB); RecalculateMechanics(); }
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();

                // 挂点高差与挂线高
                GUILayout.Space(2);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"高差 h: {heightDifference:F1}m", dimStyle, GUILayout.Width(100));
                float nH = GUILayout.HorizontalSlider(heightDifference, -100f, 100f);
                if (Mathf.Abs(nH - heightDifference) > 0.5f)
                {
                    heightDifference = Mathf.Round(nH);
                    rightAttachmentHeight = leftAttachmentHeight + heightDifference;
                    RecalculateMechanics();
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"左挂高 HA: {leftAttachmentHeight:F0}m", dimStyle, GUILayout.Width(100));
                float nHa = GUILayout.HorizontalSlider(leftAttachmentHeight, 10f, 120f);
                if (Mathf.Abs(nHa - leftAttachmentHeight) > 0.5f)
                {
                    leftAttachmentHeight = Mathf.Round(nHa);
                    rightAttachmentHeight = leftAttachmentHeight + heightDifference;
                    RecalculateMechanics();
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"右挂高 HB: {rightAttachmentHeight:F0}m", dimStyle, GUILayout.Width(100));
                float nHb = GUILayout.HorizontalSlider(rightAttachmentHeight, 10f, 120f);
                if (Mathf.Abs(nHb - rightAttachmentHeight) > 0.5f)
                {
                    rightAttachmentHeight = Mathf.Round(nHb);
                    heightDifference = rightAttachmentHeight - leftAttachmentHeight;
                    RecalculateMechanics();
                }
                GUILayout.EndHorizontal();

                // 地貌粗糙度与风向
                GUILayout.Space(2);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("风偏气象与地形环境参量 (GB 50545 9.3):", boldStyle);

                GUILayout.Label("地貌粗糙度类别:", dimStyle);
                string[] terrainOpts = new string[] { "A类 (海面)", "B类 (田野乡村)", "C类 (城镇密集)", "D类 (高层建筑)" };
                terrainCategory = GUILayout.SelectionGrid(terrainCategory, terrainOpts, 2);

                GUILayout.BeginHorizontal();
                GUILayout.Label($"导线平均高度 z: {averageConductorHeight:F0}m", dimStyle, GUILayout.Width(140));
                float nAvgH = GUILayout.HorizontalSlider(averageConductorHeight, 5f, 120f);
                if (Mathf.Abs(nAvgH - averageConductorHeight) > 0.5f) averageConductorHeight = Mathf.Round(nAvgH);
                if (GUILayout.Button("自动估算", GUILayout.Width(65), GUILayout.Height(18)))
                {
                    averageConductorHeight = Mathf.Max(10f, Mathf.Round(((leftAttachmentHeight + rightAttachmentHeight) * 0.5f) * 0.75f));
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("风向夹角 θ:", dimStyle, GUILayout.Width(75));
                string[] angleOpts = new string[] { "90°", "75°", "60°", "45°", "30°" };
                float[] angleVals = new float[] { 90f, 75f, 60f, 45f, 30f };
                int curAIdx = 0;
                for (int ai = 0; ai < angleVals.Length; ai++) if (Mathf.Approximately(angleVals[ai], windAngleDeg)) curAIdx = ai;
                int newAIdx = GUILayout.SelectionGrid(curAIdx, angleOpts, 5);
                if (newAIdx != curAIdx) windAngleDeg = angleVals[newAIdx];
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("工程类型:", dimStyle, GUILayout.Width(60));
                lineType = GUILayout.SelectionGrid(lineType, new string[] { "一般输电线路", "大跨越工程" }, 2);
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // -------------------------------------------------------------
            // SECTION 4: 两端杆塔绝缘子串选型与风偏
            // -------------------------------------------------------------
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUI.color = new Color(0.3f, 0.9f, 1f);
            if (GUILayout.Button((openSecInsulator ? "▼" : "▶") + " 🌬️ 两端杆塔绝缘子串选型与风偏", GUI.skin.label))
            {
                openSecInsulator = !openSecInsulator;
            }
            GUI.color = Color.white;
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = new Color(0.15f, 0.45f, 0.75f);
            if (GUILayout.Button("📖 计算公式", GUILayout.Width(76), GUILayout.Height(20)))
            {
                formulaModalTab = 1;
                showFormulaModal = true;
                P0OrbitCamera.BlockCameraInput = true;
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            if (openSecInsulator)
            {
                GUILayout.Space(2);
                // 左右Tab与对称镜像
                GUILayout.BeginHorizontal();
                string[] insTabs = new string[] { "左塔 (A端) 绝缘子", "右塔 (B端) 绝缘子" };
                activeInsulatorTab = GUILayout.SelectionGrid(activeInsulatorTab, insTabs, 2);
                bool newSync = GUILayout.Toggle(syncRightInsulator, "左右对称");
                if (newSync != syncRightInsulator)
                {
                    syncRightInsulator = newSync;
                    if (syncRightInsulator)
                    {
                        // 复制左侧配置到右侧
                        rightInsSpecIdx = leftInsSpecIdx;
                        rightInsStringType = leftInsStringType;
                        rightVAngle = leftVAngle;
                        rightStructureHeight = leftStructureHeight;
                        rightUnitMass = leftUnitMass;
                        rightCounterWeight = leftCounterWeight;
                        rightCreepageRatio = leftCreepageRatio;
                        rightCustomWeight = leftCustomWeight;
                        rightCustomWindArea = leftCustomWindArea;
                    }
                }
                GUILayout.EndHorizontal();

                // 绝缘子型号列表名字
                string[] insSpecNames = new string[InsulatorSpec.Presets.Length];
                for (int pi = 0; pi < InsulatorSpec.Presets.Length; pi++) insSpecNames[pi] = InsulatorSpec.Presets[pi].name;
                string[] stringTypeNames = new string[] { "单I型", "双I型", "V型串", "耐张串" };

                int dummy1, dummy2;

                if (activeInsulatorTab == 0)
                {
                    // 左侧绝缘子
                    GUILayout.Label("左侧杆塔 (A) 绝缘子型式与参量:", boldStyle);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("绝缘子选型:", dimStyle, GUILayout.Width(70));
                    int newLeftSpec = GUILayout.SelectionGrid(leftInsSpecIdx, insSpecNames, 2);
                    if (newLeftSpec != leftInsSpecIdx)
                    {
                        leftInsSpecIdx = newLeftSpec;
                        var p = InsulatorSpec.Presets[leftInsSpecIdx];
                        leftStructureHeight = p.structureHeight;
                        leftUnitMass = p.unitMass;
                        if (syncRightInsulator) { rightInsSpecIdx = leftInsSpecIdx; rightStructureHeight = leftStructureHeight; rightUnitMass = leftUnitMass; }
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("串型结构:", dimStyle, GUILayout.Width(70));
                    int curStIdx = (int)leftInsStringType;
                    if (curStIdx >= stringTypeNames.Length) curStIdx = 0;
                    int newSt = GUILayout.SelectionGrid(curStIdx, stringTypeNames, 4);
                    if (newSt != curStIdx)
                    {
                        leftInsStringType = (InsulatorStringType)newSt;
                        if (syncRightInsulator) rightInsStringType = leftInsStringType;
                    }
                    GUILayout.EndHorizontal();

                    if (leftInsStringType == InsulatorStringType.V_String)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"V型串夹角 θ_v: {leftVAngle:F0}°", dimStyle, GUILayout.Width(130));
                        leftVAngle = GUILayout.HorizontalSlider(leftVAngle, 60f, 130f);
                        if (syncRightInsulator) rightVAngle = leftVAngle;
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"单片结构高度: {leftStructureHeight:F0}mm", dimStyle, GUILayout.Width(140));
                    leftStructureHeight = GUILayout.HorizontalSlider(leftStructureHeight, 100f, 5000f);
                    if (syncRightInsulator) rightStructureHeight = leftStructureHeight;
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"单片质量: {leftUnitMass:F1}kg", dimStyle, GUILayout.Width(140));
                    leftUnitMass = GUILayout.HorizontalSlider(leftUnitMass, 1f, 30f);
                    if (syncRightInsulator) rightUnitMass = leftUnitMass;
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"加挂重锤: {leftCounterWeight:F0}kg", dimStyle, GUILayout.Width(140));
                    leftCounterWeight = GUILayout.HorizontalSlider(leftCounterWeight, 0f, 200f);
                    if (syncRightInsulator) rightCounterWeight = leftCounterWeight;
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"统一爬比距: {leftCreepageRatio:F1}mm/kV", dimStyle, GUILayout.Width(160));
                    leftCreepageRatio = GUILayout.HorizontalSlider(leftCreepageRatio, 15f, 40f);
                    if (syncRightInsulator) rightCreepageRatio = leftCreepageRatio;
                    GUILayout.EndHorizontal();

                    // 自动计算片数与重力/受风面积预览
                    var pLeft = InsulatorSpec.Presets[Mathf.Clamp(leftInsSpecIdx, 0, InsulatorSpec.Presets.Length - 1)];
                    int vLvl = voltageLevels[selectedVoltageIdx];
                    InsulatorMechanics.CalculateInsulatorUnits(pLeft, vLvl, 200f, leftCreepageRatio,
                        out dummy1, out dummy2, out int lCount, out float lLen, out float lTotMass, out float lWindArea);
                    float typeMult = (leftInsStringType == InsulatorStringType.Double_I || leftInsStringType == InsulatorStringType.V_String) ? 2f : 1f;
                    float areaMult = (leftInsStringType == InsulatorStringType.Double_I || leftInsStringType == InsulatorStringType.V_String) ? 1.8f : 1.0f;
                    float autoMass = lTotMass * typeMult;
                    float autoArea = lWindArea * areaMult;

                    GUILayout.BeginHorizontal(GUI.skin.box);
                    GUILayout.Label($"基准配置: {lCount}片/支 ({lLen:F2}m)", dimStyle);
                    GUILayout.Label($"整串质量: {autoMass:F1}kg", dimStyle);
                    GUILayout.Label($"受风面积: {autoArea:F2}m²", dimStyle);
                    GUILayout.EndHorizontal();
                }
                else
                {
                    // 右侧绝缘子
                    if (syncRightInsulator)
                    {
                        GUILayout.BeginHorizontal(GUI.skin.box);
                        GUI.color = new Color(0.3f, 0.9f, 1f);
                        GUILayout.Label("当前已开启【左右对称】镜像，修改将自动解除联动。", dimStyle);
                        GUI.color = Color.white;
                        if (GUILayout.Button("转为独立配置", GUILayout.Width(90), GUILayout.Height(20)))
                        {
                            syncRightInsulator = false;
                        }
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.Label("右侧杆塔 (B) 绝缘子型式与参量:", boldStyle);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("绝缘子选型:", dimStyle, GUILayout.Width(70));
                    int newRightSpec = GUILayout.SelectionGrid(rightInsSpecIdx, insSpecNames, 2);
                    if (newRightSpec != rightInsSpecIdx)
                    {
                        syncRightInsulator = false;
                        rightInsSpecIdx = newRightSpec;
                        var p = InsulatorSpec.Presets[rightInsSpecIdx];
                        rightStructureHeight = p.structureHeight;
                        rightUnitMass = p.unitMass;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("串型结构:", dimStyle, GUILayout.Width(70));
                    int curStIdx = (int)rightInsStringType;
                    if (curStIdx >= stringTypeNames.Length) curStIdx = 0;
                    int newSt = GUILayout.SelectionGrid(curStIdx, stringTypeNames, 4);
                    if (newSt != curStIdx)
                    {
                        syncRightInsulator = false;
                        rightInsStringType = (InsulatorStringType)newSt;
                    }
                    GUILayout.EndHorizontal();

                    if (rightInsStringType == InsulatorStringType.V_String)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"V型串夹角 θ_v: {rightVAngle:F0}°", dimStyle, GUILayout.Width(130));
                        float nV = GUILayout.HorizontalSlider(rightVAngle, 60f, 130f);
                        if (Mathf.Abs(nV - rightVAngle) > 0.5f) { syncRightInsulator = false; rightVAngle = nV; }
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"单片结构高度: {rightStructureHeight:F0}mm", dimStyle, GUILayout.Width(140));
                    float nSh = GUILayout.HorizontalSlider(rightStructureHeight, 100f, 5000f);
                    if (Mathf.Abs(nSh - rightStructureHeight) > 5f) { syncRightInsulator = false; rightStructureHeight = nSh; }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"单片质量: {rightUnitMass:F1}kg", dimStyle, GUILayout.Width(140));
                    float nUm = GUILayout.HorizontalSlider(rightUnitMass, 1f, 30f);
                    if (Mathf.Abs(nUm - rightUnitMass) > 0.1f) { syncRightInsulator = false; rightUnitMass = nUm; }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"加挂重锤: {rightCounterWeight:F0}kg", dimStyle, GUILayout.Width(140));
                    float nCw = GUILayout.HorizontalSlider(rightCounterWeight, 0f, 200f);
                    if (Mathf.Abs(nCw - rightCounterWeight) > 1f) { syncRightInsulator = false; rightCounterWeight = nCw; }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"统一爬比距: {rightCreepageRatio:F1}mm/kV", dimStyle, GUILayout.Width(160));
                    float nCr = GUILayout.HorizontalSlider(rightCreepageRatio, 15f, 40f);
                    if (Mathf.Abs(nCr - rightCreepageRatio) > 0.2f) { syncRightInsulator = false; rightCreepageRatio = nCr; }
                    GUILayout.EndHorizontal();

                    var pRight = InsulatorSpec.Presets[Mathf.Clamp(rightInsSpecIdx, 0, InsulatorSpec.Presets.Length - 1)];
                    int vLvl = voltageLevels[selectedVoltageIdx];
                    InsulatorMechanics.CalculateInsulatorUnits(pRight, vLvl, 200f, rightCreepageRatio,
                        out dummy1, out dummy2, out int rCount, out float rLen, out float rTotMass, out float rWindArea);
                    float typeMult = (rightInsStringType == InsulatorStringType.Double_I || rightInsStringType == InsulatorStringType.V_String) ? 2f : 1f;
                    float areaMult = (rightInsStringType == InsulatorStringType.Double_I || rightInsStringType == InsulatorStringType.V_String) ? 1.8f : 1.0f;
                    float autoMass = rTotMass * typeMult;
                    float autoArea = rWindArea * areaMult;

                    GUILayout.BeginHorizontal(GUI.skin.box);
                    GUILayout.Label($"基准配置: {rCount}片/支 ({rLen:F2}m)", dimStyle);
                    GUILayout.Label($"整串质量: {autoMass:F1}kg", dimStyle);
                    GUILayout.Label($"受风面积: {autoArea:F2}m²", dimStyle);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndVertical();

            GUILayout.Space(8);

            // -------------------------------------------------------------
            // BOTTOM: 全工况力学结算卡片列表
            // -------------------------------------------------------------
            GUILayout.Label("📊 全部工况力学核算成果汇总:", boldStyle);
            if (mechanicsResults != null)
            {
                foreach (var r in mechanicsResults)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.BeginHorizontal();
                    GUI.color = r.isGoverning ? new Color(1f, 0.85f, 0.2f) : Color.white;
                    GUILayout.Label(r.conditionName + (r.isGoverning ? " [控制]" : ""), boldStyle);
                    GUI.color = Color.white;
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Kc=" + r.safetyFactor.ToString("F2"), badgeStyle);
                    GUILayout.EndHorizontal();

                    GUILayout.Label(string.Format("应力: {0:F1} N/mm² | 张力: {1:F1} kN", r.stress, r.tension / 1000f), dimStyle);
                    GUILayout.Label(string.Format("跨中弧垂: {0:F2} m | 风偏角: {1:F1}°", r.sag, r.windAngle), dimStyle);
                    GUILayout.EndVertical();
                }
            }

            GUILayout.EndScrollView();
        }

        private void DrawWindSwingModal()
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), overlayDarkBg);

            float w = Mathf.Min(740f, Screen.width - 24f);
            float h = Mathf.Min(560f, Screen.height - 24f);
            Rect modalRect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(modalRect, "", modalStyle);

            GUILayout.BeginArea(new Rect(modalRect.x + 16, modalRect.y + 12, modalRect.width - 32, modalRect.height - 24));

            // Title Bar
            GUILayout.BeginHorizontal();
            GUILayout.Label("🌬️ 绝缘子风偏力学分析与塔头电气净空核验 (DL/T 5582)", modalHeaderStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", GUILayout.Width(28), GUILayout.Height(24)))
            {
                showWindSwingModal = false;
                P0OrbitCamera.BlockCameraInput = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            windSwingModalScroll = GUILayout.BeginScrollView(windSwingModalScroll);

            var conductor = ConductorRegistry.AllConductors[Mathf.Clamp(selectedConductorIdx, 0, ConductorRegistry.AllConductors.Length - 1)];
            var insulator = InsulatorSpec.Presets[Mathf.Clamp(selectedInsulatorSpecIdx, 0, InsulatorSpec.Presets.Length - 1)];
            int volt = voltageLevels[selectedVoltageIdx];

            // 1. Controls
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("📐 绝缘子串型与参数调谐:", boldStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("绝缘子选型:", dimStyle, GUILayout.Width(80));
            string[] insNames = new string[InsulatorSpec.Presets.Length];
            for (int i = 0; i < insNames.Length; i++) insNames[i] = InsulatorSpec.Presets[i].name;
            selectedInsulatorSpecIdx = GUILayout.SelectionGrid(selectedInsulatorSpecIdx, insNames, 3);
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label("串型配置:", dimStyle, GUILayout.Width(80));
            string[] stringTypeNames = new string[] { "单I型", "双I型", "V型串", "耐张串", "柱式" };
            int stIdx = (int)windSwingStringType;
            int newStIdx = GUILayout.SelectionGrid(stIdx, stringTypeNames, 5);
            if (newStIdx != stIdx) windSwingStringType = (InsulatorStringType)newStIdx;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("试验风速: {0:F1} m/s", windSwingTestSpeed), GUILayout.Width(130));
            windSwingTestSpeed = GUILayout.HorizontalSlider(windSwingTestSpeed, 0f, 45f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("防风偏加重锤: {0:F0} kg", windSwingCounterWeight), GUILayout.Width(130));
            windSwingCounterWeight = GUILayout.HorizontalSlider(windSwingCounterWeight, 0f, 200f);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // 2. Real-time Calculation
            var result = InsulatorMechanics.CalculateWindSwing(
                insulator, conductor, volt, spanLength, kvValue,
                windSwingTestSpeed, 0f, 200f, windSwingStringType, windSwingCounterWeight);

            GUILayout.Space(8);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("📊 实时风偏态势与安全判决:", boldStyle);
            GUILayout.FlexibleSpace();
            if (result.clearancePassed)
            {
                GUI.color = new Color(0.2f, 1f, 0.4f);
                GUILayout.Label("✅ 电气间隙合格 PASS", boldStyle);
            }
            else
            {
                GUI.color = new Color(1f, 0.3f, 0.3f);
                GUILayout.Label("⚠️ 间隙不足侵限 WARNING", boldStyle);
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            if (result.vStringLiftOff)
            {
                GUI.color = new Color(1f, 0.2f, 0.2f);
                GUILayout.Label("🚨 警告: 当前大风荷载已导致 V 串下风侧臂受拉上拔失稳 (Liftoff)！建议调大 V 夹角或加配重！", boldStyle);
                GUI.color = Color.white;
            }

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("绝缘子风偏角 φ_ins: <b>{0:F1}°</b>", result.insulatorWindSwingAngle), boldStyle);
            GUILayout.Label(string.Format("导线风偏角 φ_cond: {0:F1}°", result.conductorWindAngle), dimStyle);
            GUILayout.Label(string.Format("挂点水平位移 Δx: {0:F2} m", result.horizontalDisplacement), dimStyle);
            GUILayout.Label(string.Format("挂点垂直下落 Δy: {0:F2} m", result.verticalDropDisplacement), dimStyle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("片数配置: <b>{0} 片</b> (串长 {1:F2}m)", result.finalCount, result.stringLength), boldStyle);
            GUILayout.Label(string.Format("整串质量: {0:F1} kg", result.stringTotalWeightKg), dimStyle);
            GUILayout.Label(string.Format("受风荷载: 绝缘子 {0:F2}kN / 导线 {1:F2}kN", result.windLoadOnString, result.conductorWindLoadOnString), dimStyle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("塔头实际净距: <b>{0:F2} m</b>", result.actualClearanceToTower), boldStyle);
            GUILayout.Label(string.Format("规程最小空气间隙 (表 9.4.2-1): <b>{0:F2} m</b>", result.minAirClearanceRequired), noteStyle);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            // 3. 5类串型抗风偏横向对比
            GUILayout.Space(8);
            GUILayout.Label("⚖️ 5 类串型抗风偏性能横向对照 (同等气象与档距):", boldStyle);
            GUILayout.BeginHorizontal(GUI.skin.box);
            var compTypes = new InsulatorStringType[] { InsulatorStringType.Single_I, InsulatorStringType.Double_I, InsulatorStringType.V_String, InsulatorStringType.Tension, InsulatorStringType.Post };
            var compLabels = new string[] { "单I型悬垂", "双I型悬垂", "V型悬垂", "耐张串", "柱式防风偏" };
            for (int ci = 0; ci < compTypes.Length; ci++)
            {
                var compRes = InsulatorMechanics.CalculateWindSwing(insulator, conductor, volt, spanLength, kvValue, windSwingTestSpeed, 0f, 200f, compTypes[ci], windSwingCounterWeight);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(compLabels[ci], boldStyle);
                GUILayout.Label(string.Format("风偏角: {0:F1}°", compRes.insulatorWindSwingAngle), dimStyle);
                GUILayout.Label(string.Format("净距: {0:F2}m", compRes.actualClearanceToTower), dimStyle);
                GUI.color = compRes.clearancePassed ? new Color(0.2f, 1f, 0.4f) : new Color(1f, 0.3f, 0.3f);
                GUILayout.Label(compRes.clearancePassed ? "合格" : "超限", boldStyle);
                GUI.color = Color.white;
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawStringingModal()
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), overlayDarkBg);

            float w = Mathf.Min(820f, Screen.width - 24f);
            float h = Mathf.Min(580f, Screen.height - 24f);
            Rect modalRect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(modalRect, "", modalStyle);

            GUILayout.BeginArea(new Rect(modalRect.x + 16, modalRect.y + 12, modalRect.width - 32, modalRect.height - 24));

            // Title Bar
            GUILayout.BeginHorizontal();
            GUILayout.Label("📋 施工架线安装张力与弧垂百米百度换算表 (DL/T 5582)", modalHeaderStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", GUILayout.Width(28), GUILayout.Height(24)))
            {
                showStringingModal = false;
                P0OrbitCamera.BlockCameraInput = false;
            }
            GUILayout.EndHorizontal();

            var conductor = ConductorRegistry.AllConductors[Mathf.Clamp(selectedConductorIdx, 0, ConductorRegistry.AllConductors.Length - 1)];

            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("导线型号: {0} ({1}) | 代表档距: {2:F0} m", conductor.modelName, conductor.codeName, spanLength), dimStyle);
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.6f);
            if (GUILayout.Button("📥 导出 CSV 放样大表", GUILayout.Height(24)))
            {
                if (stringingTableData == null || stringingTableData.Count == 0)
                {
                    stringingTableData = StringingCalculator.CalculateTable(conductor, spanLength);
                }
                string csv = StringingCalculator.ExportToCsv(conductor, stringingTableData);
                string outPath = Path.Combine(Application.dataPath, "../StringingTable_" + conductor.modelName.Replace('/', '_') + ".csv");
                try
                {
                    File.WriteAllText(outPath, csv, System.Text.Encoding.UTF8);
                    annotationToast = "已成功导出 CSV: " + Path.GetFileName(outPath);
                    toastTimer = 3.5f;
                }
                catch (Exception ex)
                {
                    annotationToast = "导出失败: " + ex.Message;
                    toastTimer = 3.5f;
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            if (stringingTableData == null || stringingTableData.Count == 0)
            {
                stringingTableData = StringingCalculator.CalculateTable(conductor, spanLength);
            }

            stringingModalScroll = GUILayout.BeginScrollView(stringingModalScroll);

            // Table Header
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("代表档距 L", boldStyle, GUILayout.Width(80));
            foreach (float t in StringingCalculator.DefaultTemps)
            {
                GUILayout.Label(string.Format("{0:F0}°C\nT(kN) / f(m)", t), boldStyle, GUILayout.Width(92));
            }
            GUILayout.EndHorizontal();

            // Table Rows
            foreach (var row in stringingTableData)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUI.color = (Mathf.Abs(row.span - spanLength) < 10f) ? new Color(1f, 0.85f, 0.2f) : Color.white;
                GUILayout.Label(row.span.ToString("F0") + " m", boldStyle, GUILayout.Width(80));
                GUI.color = Color.white;

                foreach (var cell in row.cells)
                {
                    string cellStr = string.Format("{0:F2} kN\n{1:F2} m", cell.tension / 1000f, cell.sag);
                    GUILayout.Label(cellStr, dimStyle, GUILayout.Width(92));
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawComplianceModal()
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), overlayDarkBg);

            float w = Mathf.Min(760f, Screen.width - 24f);
            float h = Mathf.Min(580f, Screen.height - 24f);
            Rect modalRect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(modalRect, "", modalStyle);

            GUILayout.BeginArea(new Rect(modalRect.x + 16, modalRect.y + 12, modalRect.width - 32, modalRect.height - 24));

            // Title Bar
            GUILayout.BeginHorizontal();
            GUILayout.Label("🛡️ DL/T 5582-2020 架空输电线路电气设计规程合规核查报告", modalHeaderStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", GUILayout.Width(28), GUILayout.Height(24)))
            {
                showComplianceModal = false;
                P0OrbitCamera.BlockCameraInput = false;
            }
            GUILayout.EndHorizontal();

            var conductor = ConductorRegistry.AllConductors[Mathf.Clamp(selectedConductorIdx, 0, ConductorRegistry.AllConductors.Length - 1)];
            var insulator = InsulatorSpec.Presets[Mathf.Clamp(selectedInsulatorSpecIdx, 0, InsulatorSpec.Presets.Length - 1)];
            int volt = voltageLevels[selectedVoltageIdx];

            // Run Audit
            float maxTension = 0f;
            float avgTension = 0f;
            float maxSag = 0f;
            if (mechanicsResults != null)
            {
                foreach (var r in mechanicsResults)
                {
                    if (r.tension > maxTension) maxTension = r.tension;
                    if (r.conditionId == "avg-temp") avgTension = r.tension;
                    if (r.sag > maxSag) maxSag = r.sag;
                }
            }

            var insRes = InsulatorMechanics.CalculateWindSwing(
                insulator, conductor, volt, spanLength, kvValue,
                windSwingTestSpeed, 0f, 200f, windSwingStringType, windSwingCounterWeight);

            complianceAuditItems = ComplianceAuditor.AuditCompliance(
                conductor, volt, spanLength, maxTension, avgTension, maxSag, insRes);

            bool allPassed = true;
            foreach (var it in complianceAuditItems)
            {
                if (!it.passed) { allPassed = false; break; }
            }

            GUILayout.Space(6);
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("核查结论总括:", boldStyle, GUILayout.Width(100));
            if (allPassed)
            {
                GUI.color = new Color(0.2f, 1f, 0.4f);
                GUILayout.Label("✅ 全项校验合格 (PASS) · 满足国家电网基建规程全部强制性条文", boldStyle);
            }
            else
            {
                GUI.color = new Color(1f, 0.3f, 0.3f);
                GUILayout.Label("⚠️ 存在不合规项 (WARNING) · 请根据下列条款明细优化力学参数", boldStyle);
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            complianceModalScroll = GUILayout.BeginScrollView(complianceModalScroll);

            foreach (var item in complianceAuditItems)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                GUILayout.Label(item.item, boldStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label("[" + item.codeReference + "]", noteStyle);
                GUILayout.Space(8);
                if (item.passed)
                {
                    GUI.color = new Color(0.2f, 1f, 0.4f);
                    GUILayout.Label("合格 PASS", boldStyle);
                }
                else
                {
                    GUI.color = new Color(1f, 0.3f, 0.3f);
                    GUILayout.Label("不合格 FAIL", boldStyle);
                }
                GUI.color = Color.white;
                GUILayout.EndHorizontal();

                GUILayout.Label("规程限值要求: " + item.standardRequirement, dimStyle);
                GUILayout.Label("实测工程计算: " + item.calculatedValue, boldStyle);
                GUILayout.Label("专家诊断建议: " + item.notes, dimStyle);
                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void TriggerPresetView(CameraViewPreset preset)
        {
            if (orbitCam == null) orbitCam = FindObjectOfType<P0OrbitCamera>();
            if (orbitCam != null)
            {
                orbitCam.SetPresetView(preset);
            }
            if (PanZoomOrbitMouse.Instance != null)
            {
                PanZoomOrbitMouse.Instance.SetPresetView(preset);
            }
        }

        private void DrawReclassDropdownPopup()
        {
            if (!showReclassDropdown) return;
            float px = Mathf.Clamp(reclassDropdownPos.x, 10f, Screen.width - 120f);
            float py = Mathf.Clamp(reclassDropdownPos.y, 40f, Screen.height - 160f);
            Rect popRect = new Rect(px, py, 110f, 140f);
            GUI.Box(popRect, "", glassStyle);

            GUILayout.BeginArea(new Rect(popRect.x + 4, popRect.y + 4, popRect.width - 8, popRect.height - 8));
            for (int ci = 0; ci < reclassClassNames.Length; ci++)
            {
                if (GUILayout.Button(reclassClassNames[ci], GUILayout.Height(22)))
                {
                    selectedReclassClassIdx = ci;
                    showReclassDropdown = false;
                }
            }
            GUILayout.EndArea();

            if (Event.current.type == EventType.MouseDown && !popRect.Contains(Event.current.mousePosition))
            {
                showReclassDropdown = false;
            }
        }

        private void DrawOperationGuideTicker()
        {
            float bannerX = showSidebar ? 358f : 8f;
            float bannerW = Screen.width - bannerX - 10f;
            float bannerH = 24f;
            Rect bannerRect = new Rect(bannerX, 44f, bannerW, bannerH);
            IndustrialUITheme.DrawFrame(bannerRect, IndustrialUITheme.BgBase, IndustrialUITheme.BorderHairline, 1f);

            var annCtrl = AnnotationController.Instance;
            AnnotationTargetMode curMode = annCtrl != null ? annCtrl.currentMode : AnnotationTargetMode.None;
            string modeTag = "🌐 漫游观察";
            Color tagColor = IndustrialUITheme.SgccEmerald;
            string guideText = "";

            switch (curMode)
            {
                case AnnotationTargetMode.None:
                    modeTag = "🌐 漫游观察";
                    tagColor = IndustrialUITheme.SgccEmerald;
                    guideText = "【鼠标右键】按住旋转视角  |  【鼠标中键/滚轮按住】平移视口  |  【滚轮】推拉缩放  |  点击顶部【漫游▾】展开俯/仰/正/侧/轴测预设视角  |  按【F】聚焦选中资产";
                    break;
                case AnnotationTargetMode.Tower:
                    modeTag = "🗼 杆塔标定";
                    tagColor = IndustrialUITheme.VoltageGold;
                    guideText = "【Shift + 鼠标左键点击点云】放置杆塔中轴底部靶点  |  【Shift+滚轮】旋转朝向偏角  |  【Shift+右键拖拽】调整呼高与横担  |  双击资产树节点快速相机聚焦";
                    break;
                case AnnotationTargetMode.Conductor:
                    modeTag = "⚡ 导线标定";
                    tagColor = IndustrialUITheme.ConductorCyan;
                    guideText = "【Shift + 鼠标左键依次点击点云】标记导线悬链特征点（至少3点）  |  系统自动拟合悬链线并反演导线外径  |  双击资产树节点快速聚焦";
                    break;
                case AnnotationTargetMode.Reclassify:
                    modeTag = "🔲 选区修点";
                    tagColor = IndustrialUITheme.ConductorCyan;
                    guideText = "【按住鼠标左键在视口中拉框】矩形框选误分类点云簇  |  点击顶部【选区修点▾】选择目标ASPRS类别  |  点击【应用分类变更】重构语义属性";
                    break;
                case AnnotationTargetMode.Measure:
                    modeTag = "📏 3D空间测距";
                    tagColor = IndustrialUITheme.ConductorCyan;
                    var mCtrl = MeasurementController.Instance;
                    if (mCtrl != null && mCtrl.pointA == null)
                        guideText = "👉 第一步：按住【Shift + 鼠标左键】在点云上拾取测距起点【A点】  |  支持三维空间原位测距与高差净空分析";
                    else if (mCtrl != null && !mCtrl.hasResult)
                        guideText = "👉 第二步：按住【Shift + 鼠标左键】在点云上拾取测距终点【B点】  |  系统将即时生成三维空间视差连线与净空高差标尺";
                    else
                        guideText = "✅ 3D测距已就绪：空间中已原位标注测点A/B与尺寸微章  |  按【Shift + 左键】重新选点，或点击尺寸牌【✕】清空";
                    break;
            }

            float badgeW = 92f;
            IndustrialUITheme.DrawBadge(new Rect(bannerX + 2f, 46f, badgeW, 20f), modeTag, tagColor, Color.black);

            float clipX = bannerX + badgeW + 8f;
            float clipW = bannerW - badgeW - 14f;
            if (clipW > 60f)
            {
                GUI.BeginGroup(new Rect(clipX, 44f, clipW, 24f));
                GUIContent content = new GUIContent(guideText);
                float textW = IndustrialUITheme.DimLabelStyle.CalcSize(content).x;
                float speed = 32f;
                float loopSpan = textW + 90f;
                float scrollOffset = (Time.time * speed) % loopSpan;

                GUI.Label(new Rect(-scrollOffset, 3f, textW, 20f), guideText, IndustrialUITheme.DimLabelStyle);
                if (textW > clipW)
                {
                    GUI.Label(new Rect(-scrollOffset + loopSpan, 3f, textW, 20f), guideText, IndustrialUITheme.DimLabelStyle);
                }
                GUI.EndGroup();
            }
        }

        private void DrawRoamCompassDropdown()
        {
            if (!showRoamDropdown) return;

            float w = 160f;
            float h = 188f;
            float px = Mathf.Clamp(roamDropdownPos.x, 10f, Screen.width - w - 10f);
            float py = Mathf.Clamp(roamDropdownPos.y, 44f, Screen.height - h - 35f);
            Rect r = new Rect(px, py, w, h);

            IndustrialUITheme.DrawFrame(r, IndustrialUITheme.BgBase, IndustrialUITheme.BorderHairline, 1f);

            GUILayout.BeginArea(new Rect(r.x + 6, r.y + 6, r.width - 12, r.height - 12));
            GUILayout.Label("🧭 漫游预设视角", IndustrialUITheme.TechHeaderStyle);
            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("俯视 (顶)", IndustrialUITheme.TechButtonStyle, GUILayout.Width(70), GUILayout.Height(22))) { TriggerPresetView(CameraViewPreset.Top); showRoamDropdown = false; }
            if (GUILayout.Button("仰视 (底)", IndustrialUITheme.TechButtonStyle, GUILayout.Width(70), GUILayout.Height(22))) { TriggerPresetView(CameraViewPreset.Bottom); showRoamDropdown = false; }
            GUILayout.EndHorizontal();

            GUILayout.Space(2);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("正视 (前)", IndustrialUITheme.TechButtonStyle, GUILayout.Width(70), GUILayout.Height(22))) { TriggerPresetView(CameraViewPreset.Front); showRoamDropdown = false; }
            if (GUILayout.Button("后视 (背)", IndustrialUITheme.TechButtonStyle, GUILayout.Width(70), GUILayout.Height(22))) { TriggerPresetView(CameraViewPreset.Back); showRoamDropdown = false; }
            GUILayout.EndHorizontal();

            GUILayout.Space(2);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("左视 (侧)", IndustrialUITheme.TechButtonStyle, GUILayout.Width(70), GUILayout.Height(22))) { TriggerPresetView(CameraViewPreset.Left); showRoamDropdown = false; }
            if (GUILayout.Button("右视 (侧)", IndustrialUITheme.TechButtonStyle, GUILayout.Width(70), GUILayout.Height(22))) { TriggerPresetView(CameraViewPreset.Right); showRoamDropdown = false; }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            if (GUILayout.Button("📐 轴测透视 (3D Iso)", IndustrialUITheme.ActiveButtonStyle, GUILayout.Height(22)))
            {
                TriggerPresetView(CameraViewPreset.Isometric);
                showRoamDropdown = false;
            }

            GUILayout.Space(2);
            if (GUILayout.Button("🔄 初始复位 (Reset)", IndustrialUITheme.GoldButtonStyle, GUILayout.Height(22)))
            {
                TriggerPresetView(CameraViewPreset.Reset);
                showRoamDropdown = false;
            }

            GUILayout.EndArea();

            if (Event.current.type == EventType.MouseDown && !r.Contains(Event.current.mousePosition))
            {
                showRoamDropdown = false;
            }
        }

        private void DrawClassificationLegendHUD()
        {
            if (!isClassLegendVisible)
            {
                float btnW = 100f;
                float btnH = 24f;
                Rect rMin = new Rect(Screen.width - btnW - 12f, Screen.height - 30f - btnH - 4f, btnW, btnH);
                if (GUI.Button(rMin, "🎨 类别过滤 ▴", IndustrialUITheme.TechButtonStyle))
                {
                    isClassLegendVisible = true;
                }
                return;
            }

            float w = 270f;
            float h = 245f;
            float rx = Screen.width - w - 12f;
            float ry = Screen.height - 30f - h - 4f; // directly above status bar
            Rect r = new Rect(rx, ry, w, h);
            IndustrialUITheme.DrawFrame(r, IndustrialUITheme.BgBase, IndustrialUITheme.BorderHairline, 1f);

            GUILayout.BeginArea(new Rect(r.x + 8f, r.y + 6f, r.width - 16f, r.height - 12f));

            GUILayout.BeginHorizontal();
            GUILayout.Label("🎨 ASPRS 类别图例与过滤", IndustrialUITheme.TechHeaderStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("—", IndustrialUITheme.TechButtonStyle, GUILayout.Width(22), GUILayout.Height(18)))
            {
                isClassLegendVisible = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(3);

            // Fast action filter buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("⚡ 仅电力", IndustrialUITheme.TechButtonStyle, GUILayout.Height(20)))
            {
                if (colorManager != null)
                {
                    foreach (var cid in asprsClassIds)
                    {
                        bool isPower = (cid == 14 || cid == 15 || cid == 16);
                        colorManager.SetClassVisible(cid, isPower);
                    }
                }
            }
            if (GUILayout.Button("全显", IndustrialUITheme.TechButtonStyle, GUILayout.Height(20)))
            {
                if (colorManager != null) colorManager.SetAllClassesVisible(true);
            }
            if (GUILayout.Button("全隐", IndustrialUITheme.TechButtonStyle, GUILayout.Height(20)))
            {
                if (colorManager != null) colorManager.SetAllClassesVisible(false);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            legendClassScroll = GUILayout.BeginScrollView(legendClassScroll);
            for (int i = 0; i < asprsClassIds.Length; i++)
            {
                byte cid = asprsClassIds[i];
                bool isVis = (colorManager != null) ? colorManager.IsClassVisible(cid) : true;

                GUILayout.BeginHorizontal(GUI.skin.box);
                GUI.color = asprsClassColors[i];
                GUILayout.Label("■", GUILayout.Width(14));
                GUI.color = Color.white;

                GUILayout.Label(asprsClassNames[i], boldStyle, GUILayout.Width(125));
                GUILayout.Label($"[{cid}]", dimStyle, GUILayout.Width(35));

                bool newVis = GUILayout.Toggle(isVis, "", GUILayout.Width(18));
                if (newVis != isVis && colorManager != null)
                {
                    colorManager.SetClassVisible(cid, newVis);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void DrawTelemetryHUD()
        {
            // Deprecated: telemetry HUD integrated into full-width bottom status bar (DrawStatusHUD)
        }

        private void Draw3DMeasurementInWorld()
        {
            var mCtrl = MeasurementController.Instance;
            if (mCtrl == null) return;
            var cam = CameraHelper.MainCamera;
            if (cam == null) return;

            // 1. Draw Point A badge in 3D world space
            if (mCtrl.pointA.HasValue)
            {
                Vector3 spA = cam.WorldToScreenPoint(mCtrl.pointA.Value);
                if (spA.z > 0f)
                {
                    Vector2 guiA = new Vector2(spA.x, Screen.height - spA.y);
                    IndustrialUITheme.DrawBadge(new Rect(guiA.x - 22f, guiA.y - 11f, 44f, 22f), "A点", IndustrialUITheme.ConductorCyan, Color.black);
                }
            }

            // 2. Draw Point B badge in 3D world space
            if (mCtrl.pointB.HasValue)
            {
                Vector3 spB = cam.WorldToScreenPoint(mCtrl.pointB.Value);
                if (spB.z > 0f)
                {
                    Vector2 guiB = new Vector2(spB.x, Screen.height - spB.y);
                    IndustrialUITheme.DrawBadge(new Rect(guiB.x - 22f, guiB.y - 11f, 44f, 22f), "B点", IndustrialUITheme.VoltageGold, Color.black);
                }
            }

            // 3. Draw World-space Dimension Billboard Card at the 3D Midpoint
            if (mCtrl.hasResult && mCtrl.pointA.HasValue && mCtrl.pointB.HasValue)
            {
                Vector3 midWorld = (mCtrl.pointA.Value + mCtrl.pointB.Value) * 0.5f;
                Vector3 spMid = cam.WorldToScreenPoint(midWorld);
                if (spMid.z > 0f)
                {
                    Vector2 guiMid = new Vector2(spMid.x, Screen.height - spMid.y);
                    float cardW = 210f;
                    float cardH = 74f;
                    Rect cardRect = new Rect(guiMid.x - cardW * 0.5f, guiMid.y - cardH - 12f, cardW, cardH);

                    IndustrialUITheme.DrawFrame(cardRect, IndustrialUITheme.BgBase, IndustrialUITheme.BorderHairline, 1f);

                    GUILayout.BeginArea(new Rect(cardRect.x + 8f, cardRect.y + 6f, cardRect.width - 16f, cardRect.height - 10f));
                    
                    GUILayout.BeginHorizontal();
                    GUI.color = IndustrialUITheme.ConductorCyan;
                    GUILayout.Label($"📏 空间距: {mCtrl.dist3D:F2} m", IndustrialUITheme.TechHeaderStyle);
                    GUI.color = Color.white;
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("✕", IndustrialUITheme.TechButtonStyle, GUILayout.Width(20), GUILayout.Height(18)))
                    {
                        mCtrl.ClearMeasurement();
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.Space(2);
                    GUILayout.Label($"水平距: {mCtrl.dist2D:F2} m  |  高差: {mCtrl.deltaY:F2} m", IndustrialUITheme.DimLabelStyle);
                    
                    GUILayout.Space(2);
                    GUI.color = IndustrialUITheme.SgccEmerald;
                    GUILayout.Label("原位真空间实测", IndustrialUITheme.DimLabelStyle);
                    GUI.color = Color.white;

                    GUILayout.EndArea();
                }
            }
            else if (mCtrl.pointA.HasValue && !mCtrl.pointB.HasValue)
            {
                // Prompt badge near Point A in world space
                Vector3 spA = cam.WorldToScreenPoint(mCtrl.pointA.Value);
                if (spA.z > 0f)
                {
                    Vector2 guiA = new Vector2(spA.x, Screen.height - spA.y);
                    Rect promptRect = new Rect(guiA.x - 105f, guiA.y + 14f, 210f, 22f);
                    IndustrialUITheme.DrawBadge(promptRect, "👉 Shift+左键拾取空间终点 B", IndustrialUITheme.VoltageGold, Color.black);
                }
            }
        }

        private void DrawFormulaModal()
        {
            DrawFormulaModalFull();
        }

        private void DrawLegendRow(Color swatchColor, string name, string code)
        {
            GUILayout.BeginHorizontal();
            GUI.color = swatchColor;
            GUILayout.Label(name, boldStyle, GUILayout.Width(85));
            GUI.color = Color.white;
            GUILayout.FlexibleSpace();
            GUILayout.Label(code, dimStyle, GUILayout.Width(45));
            GUILayout.EndHorizontal();
        }

        private void DrawStatusHUD()
        {
            float h = 28f;
            Rect barRect = new Rect(0, Screen.height - h, Screen.width, h);
            IndustrialUITheme.DrawFrame(barRect, IndustrialUITheme.BgBase, IndustrialUITheme.BorderHairline, 1f);
            GUI.DrawTexture(new Rect(0, Screen.height - h, Screen.width, 1.2f), IndustrialUITheme.GetSolidTexture(IndustrialUITheme.BorderBright));

            // Fetch camera & distance
            var cam = CameraHelper.MainCamera;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
            float altDist = 162.0f;
            var panCamMouse = Metervara.Interaction.PanZoomOrbitMouse.Instance;
            if (panCamMouse != null && cam != null)
            {
                altDist = Vector3.Distance(camPos, panCamMouse.Pivot);
            }

            int tCount = TowerAnnotator.Instance != null ? TowerAnnotator.Instance.towers.Count : 0;
            int wCount = ConductorAnnotator.Instance != null ? ConductorAnnotator.Instance.conductors.Count : 0;
            int pts = (colorManager != null) ? colorManager.TotalPoints : 0;
            bool isRgb = (colorManager == null || colorManager.CurrentMode == PointCloudColorMode.RGB);
            string modeStr = isRgb ? "真实RGB" : "高程渐变";

            GUILayout.BeginArea(new Rect(10, Screen.height - h + 3, Screen.width - 20, 22));
            GUILayout.BeginHorizontal();

            // 1. HUD 遥测标识
            GUILayout.Label("🧭 遥测姿态", IndustrialUITheme.TechHeaderStyle, GUILayout.Width(75));

            // 2. FPS
            GUI.color = IndustrialUITheme.SgccEmerald;
            GUILayout.Label($"{fps:F0} FPS", boldStyle, GUILayout.Width(55));
            GUI.color = Color.white;

            GUILayout.Label("|", dimStyle, GUILayout.Width(8));

            // 3. 空间坐标
            GUILayout.Label($"坐标: X:{camPos.x:F1} Y:{camPos.y:F1} Z:{camPos.z:F1} m", dimStyle, GUILayout.Width(230));

            GUILayout.Label("|", dimStyle, GUILayout.Width(8));

            // 4. 视距
            GUI.color = IndustrialUITheme.ConductorCyan;
            GUILayout.Label($"视距 Alt: {altDist:F1} m", boldStyle, GUILayout.Width(100));
            GUI.color = Color.white;

            GUILayout.Label("|", dimStyle, GUILayout.Width(8));

            // 5. 电力资产
            GUI.color = IndustrialUITheme.VoltageGold;
            GUILayout.Label($"杆塔: {tCount}座 | 导线: {wCount}根", boldStyle, GUILayout.Width(135));
            GUI.color = Color.white;

            GUILayout.Label("|", dimStyle, GUILayout.Width(8));

            // 6. 点云总数
            GUILayout.Label(pts > 0 ? $"点云: {pts:N0}点" : "点云: 未载入", dimStyle, GUILayout.Width(110));

            GUILayout.Label("|", dimStyle, GUILayout.Width(8));

            // 7. 着色模式
            GUI.color = isRgb ? IndustrialUITheme.ConductorCyan : IndustrialUITheme.SgccEmerald;
            GUILayout.Label($"着色: {modeStr}", boldStyle, GUILayout.Width(95));
            GUI.color = Color.white;

            GUILayout.FlexibleSpace();

            // 8. 线路名称
            if (!string.IsNullOrEmpty(activeLineName))
            {
                GUILayout.Label($"线路: {activeLineName}", dimStyle, GUILayout.Width(140));
            }
            else
            {
                GUILayout.Label("线路: 未选择", dimStyle, GUILayout.Width(95));
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void SyncCrsIndices(LasImportMetadata meta)
        {
            if (meta == null) return;
            selectedCrsIndex = (int)meta.selectedCRS;

            int[] utmValues = new int[] { 50, 49, 51, 48, 47, 52 };
            selectedUtmZoneIndex = 0;
            for (int i = 0; i < utmValues.Length; i++)
            {
                if (utmValues[i] == meta.utmZone)
                {
                    selectedUtmZoneIndex = i;
                    break;
                }
            }

            int[] meridianValues = new int[] { 120, 117, 114, 111, 108, 105 };
            selectedMeridianIndex = 0;
            for (int i = 0; i < meridianValues.Length; i++)
            {
                if (meridianValues[i] == meta.centralMeridian)
                {
                    selectedMeridianIndex = i;
                    break;
                }
            }
        }

        public void OpenImportModal(string path)
        {
            OpenBatchImportModal(new string[] { path });
        }

        public void OpenBatchImportModal(string[] paths)
        {
            if (paths == null || paths.Length == 0) return;
            if (pendingBatchMetas == null) pendingBatchMetas = new List<LasImportMetadata>();

            foreach (var p in paths)
            {
                if (File.Exists(p) && !pendingBatchMetas.Any(m => m.filePath == p))
                {
                    var meta = LasMetadataDetector.DetectMetadata(p);
                    pendingBatchMetas.Add(meta);
                }
            }

            if (pendingBatchMetas.Count == 0) return;

            if (pendingImportMeta == null || !pendingBatchMetas.Contains(pendingImportMeta))
            {
                pendingImportMeta = pendingBatchMetas[0];
            }

            if (masterProjectCRS == null)
            {
                masterProjectCRS = pendingBatchMetas[0];
            }
            if (masterProjectCRS.selectedCRS == CRSType.Auto)
            {
                masterProjectCRS.selectedCRS = masterProjectCRS.detectedCRS != CRSType.Auto ? masterProjectCRS.detectedCRS : CRSType.UTM_North;
            }

            SyncCrsIndices(masterProjectCRS);

            editProvince = masterProjectCRS.province;
            editCity = masterProjectCRS.city;
            editLineName = masterProjectCRS.lineName;
            editSegmentName = masterProjectCRS.segmentName;

            showImportModal = true;
            P0OrbitCamera.BlockCameraInput = true;
            ShowToast(string.Format("已载入 {0} 个点云标段，请核对坐标系", pendingBatchMetas.Count));
            Debug.Log(string.Format("[CorridorHUD] OpenBatchImportModal: successfully loaded {0} point clouds.", pendingBatchMetas.Count));
        }

        private void CancelImport()
        {
            showImportModal = false;
            pendingImportMeta = null;
            if (pendingBatchMetas != null) pendingBatchMetas.Clear();
            masterProjectCRS = null;
            activeMetaEditIndex = -1;
            P0OrbitCamera.BlockCameraInput = false;
        }

        private void ConfirmImport()
        {
            var target = (pendingBatchMetas != null && pendingBatchMetas.Count > 0) ? pendingBatchMetas[0] : pendingImportMeta;
            if (target == null) return;

            // 1. Mount into Hierarchy
            var segData = CorridorHierarchyManager.AddOrUpdateSegment(
                editProvince,
                editCity,
                editLineName,
                editSegmentName,
                target.filePath,
                target.header.Bounds,
                (int)target.header.pointCount,
                target.lat,
                target.lon,
                target.startTower,
                target.endTower,
                target.length
            );

            activeSegmentName = editSegmentName;
            activeLineName = editLineName;
            sidebarTab = 0; // Show mounted node in hierarchy tree tab

            // 2. Load LAS into viewer
            if (importer != null)
            {
                importer.Load(target.filePath);
            }

            // 3. Focus camera on point cloud bounds
            if (importer != null)
            {
                var v = importer.GetComponent<RuntimeViewerDX11>();
                if (v != null)
                {
                    var panCam = FindObjectOfType<Metervara.Interaction.PanZoomOrbitMouse>();
                    if (panCam != null) panCam.FocusOnBounds(v.cloudBounds);
                    if (orbitCam != null) orbitCam.FocusOnBounds(v.cloudBounds);
                }
            }

            CancelImport();
        }

        private void OpenCorridorCutter2D()
        {
            if (pendingBatchMetas == null || pendingBatchMetas.Count == 0)
            {
                if (pendingImportMeta != null)
                {
                    pendingBatchMetas = new List<LasImportMetadata> { pendingImportMeta };
                }
                else return;
            }

            var batchCopy = new List<LasImportMetadata>(pendingBatchMetas);
            var masterCopy = masterProjectCRS ?? (batchCopy.Count > 0 ? batchCopy[0] : null);
            if (masterCopy != null && string.IsNullOrEmpty(masterCopy.filePath) && batchCopy.Count > 0)
            {
                masterCopy.filePath = batchCopy[0].filePath;
            }
            var p = editProvince;
            var c = editCity;
            var l = editLineName;

            CancelImport();

            var cutter = FindObjectOfType<OTA.Corridor.Preprocess.CorridorCutter2DWindow>();
            if (cutter == null)
            {
                cutter = gameObject.AddComponent<OTA.Corridor.Preprocess.CorridorCutter2DWindow>();
            }
            cutter.Open(batchCopy, masterCopy, p, c, l);
        }

        private void DrawCrsSelectionControls(LasImportMetadata targetMeta)
        {
            if (targetMeta == null) return;
            string[] crsOptions = new string[]
            {
                "🤖 自动识别",
                "🌐 WGS84 UTM",
                "📐 CGCS2000含带号",
                "📐 CGCS2000无带号",
                "🗺️ 经纬度",
                "📏 局部工程"
            };

            int curCrs = (int)targetMeta.selectedCRS;
            int newCrs = GUILayout.SelectionGrid(curCrs, crsOptions, 3);
            if (newCrs != curCrs)
            {
                if (newCrs == 0)
                {
                    int rz, rm;
                    double cx = (targetMeta.header.minX + targetMeta.header.maxX) * 0.5;
                    double cy = (targetMeta.header.minY + targetMeta.header.maxY) * 0.5;
                    targetMeta.detectedCRS = LasMetadataDetector.DetectCRS(cx, cy, out rz, out rm);
                    targetMeta.selectedCRS = targetMeta.detectedCRS != CRSType.Auto ? targetMeta.detectedCRS : CRSType.UTM_North;
                    targetMeta.utmZone = rz;
                    targetMeta.centralMeridian = rm;
                }
                else
                {
                    targetMeta.selectedCRS = (CRSType)newCrs;
                }
                LasMetadataDetector.CalculateGeoLocation(targetMeta);
                LasMetadataDetector.ResolveHierarchy(targetMeta);
            }

            if (targetMeta.selectedCRS == CRSType.UTM_North)
            {
                GUILayout.Space(4);
                GUILayout.Label("UTM 投影带 (UTM Zone N):", boldStyle);
                string[] utmZones = new string[]
                {
                    "Zone 50N (117°E 华东/浙/沪)",
                    "Zone 49N (111°E 华中/京/粤/鄂)",
                    "Zone 51N (123°E 华东沿海/鲁/辽)",
                    "Zone 48N (105°E 西南/川/渝/贵)",
                    "Zone 47N (99°E 西北/滇/藏)",
                    "Zone 52N (129°E 东北/黑)"
                };
                int[] utmValues = new int[] { 50, 49, 51, 48, 47, 52 };
                int curU = 0;
                for (int u = 0; u < utmValues.Length; u++) if (utmValues[u] == targetMeta.utmZone) curU = u;
                int newUtm = GUILayout.SelectionGrid(curU, utmZones, 2);
                if (newUtm != curU)
                {
                    targetMeta.utmZone = utmValues[newUtm];
                    LasMetadataDetector.CalculateGeoLocation(targetMeta);
                    LasMetadataDetector.ResolveHierarchy(targetMeta);
                }
            }
            else if (targetMeta.selectedCRS == CRSType.CGCS2000_3Deg_NoZone)
            {
                GUILayout.Space(4);
                GUILayout.Label("中央子午线 (Central Meridian):", boldStyle);
                string[] meridians = new string[]
                {
                    "120°E (浙/沪/苏/闽)",
                    "117°E (鲁/皖/苏/闽)",
                    "114°E (京/冀/鄂/粤)",
                    "111°E (晋/豫/湘/桂)",
                    "108°E (陕/鄂/渝/贵)",
                    "105°E (内蒙/川/滇)"
                };
                int[] meridianValues = new int[] { 120, 117, 114, 111, 108, 105 };
                int curM = 0;
                for (int m = 0; m < meridianValues.Length; m++) if (meridianValues[m] == targetMeta.centralMeridian) curM = m;
                int newM = GUILayout.SelectionGrid(curM, meridians, 3);
                if (newM != curM)
                {
                    targetMeta.centralMeridian = meridianValues[newM];
                    LasMetadataDetector.CalculateGeoLocation(targetMeta);
                    LasMetadataDetector.ResolveHierarchy(targetMeta);
                }
            }
        }

        private void DrawImportModal()
        {
            if (pendingBatchMetas == null || pendingBatchMetas.Count == 0)
            {
                if (pendingImportMeta != null) pendingBatchMetas = new List<LasImportMetadata> { pendingImportMeta };
                else return;
            }

            // 1. Full-screen dark tint
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), overlayDarkBg);

            // 2. Centered modal dialog window
            float w = Mathf.Min(780f, Screen.width - 24f);
            float h = Mathf.Min(720f, Screen.height - 24f);
            Rect modalRect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            GUI.Box(modalRect, "", modalStyle);

            GUILayout.BeginArea(new Rect(modalRect.x + 16, modalRect.y + 12, modalRect.width - 32, modalRect.height - 24));

            // Title Bar
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("📥 批量导入点云 · 多源异构坐标系协同配置 (已选 {0} 个文件)", pendingBatchMetas.Count), modalHeaderStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", GUILayout.Width(28), GUILayout.Height(24)))
            {
                CancelImport();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Top action buttons: Append files, Append folder, Clear
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("➕ 追加文件", GUILayout.Width(95), GUILayout.Height(24)))
            {
                string[] moreFiles = NativeFileBrowserHelper.OpenMultipleLasFilesDialog();
                if (moreFiles != null && moreFiles.Length > 0)
                {
                    OpenBatchImportModal(moreFiles);
                }
            }
            if (GUILayout.Button("📁 追加目录", GUILayout.Width(95), GUILayout.Height(24)))
            {
                string folder = NativeFileBrowserHelper.OpenFolderPanel();
                if (!string.IsNullOrEmpty(folder))
                {
                    string[] scanned = NativeFileBrowserHelper.ScanFolderForLas(folder);
                    if (scanned != null && scanned.Length > 0)
                    {
                        OpenBatchImportModal(scanned);
                    }
                    else
                    {
                        ShowToast("所选目录未包含有效 LAS/LAZ 文件");
                    }
                }
            }
            if (GUILayout.Button("🧹 清空列表", GUILayout.Width(85), GUILayout.Height(24)))
            {
                pendingBatchMetas.Clear();
                pendingImportMeta = null;
                activeMetaEditIndex = -1;
                return;
            }
            GUILayout.FlexibleSpace();
            long totalBatchPoints = 0;
            foreach (var bm in pendingBatchMetas) if (bm.header.pointCount > 0) totalBatchPoints += (long)bm.header.pointCount;
            GUILayout.Label(string.Format("总计: {0} 个标段 | {1:N0} 点 ({2:F1}万点)", pendingBatchMetas.Count, totalBatchPoints, totalBatchPoints / 10000.0), badgeStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            modalScroll = GUILayout.BeginScrollView(modalScroll);

            // Point Cloud Batch Table
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("📋 点云标段文件列表 (支持各个点云坐标系异构与独立微调):", subHeaderStyle);
            GUILayout.Space(4);

            batchTableScroll = GUILayout.BeginScrollView(batchTableScroll, GUILayout.Height(Mathf.Min(160f, pendingBatchMetas.Count * 36f + 25f)));
            for (int i = 0; i < pendingBatchMetas.Count; i++)
            {
                var item = pendingBatchMetas[i];
                bool isEditingThis = (activeMetaEditIndex == i);

                GUILayout.BeginHorizontal(GUI.skin.box);
                GUI.color = isEditingThis ? new Color(0.3f, 1f, 0.5f) : Color.white;
                GUILayout.Label(string.Format("#{0:D2}", i + 1), boldStyle, GUILayout.Width(30));
                GUI.color = Color.white;

                string fn = Path.GetFileName(item.filePath);
                GUILayout.Label(fn, boldStyle, GUILayout.Width(210));
                GUILayout.Label(string.Format("{0:F1}万点", item.header.pointCount / 10000.0), dimStyle, GUILayout.Width(75));

                string crsName = item.selectedCRS == CRSType.CGCS2000_3Deg_NoZone
                    ? string.Format("CGCS2000 {0}°E", item.centralMeridian)
                    : (item.selectedCRS == CRSType.UTM_North ? string.Format("UTM {0}N", item.utmZone) : item.selectedCRS.ToString());
                GUILayout.Label(crsName, badgeStyle, GUILayout.Width(130));

                string geoDesc = string.Format("({0:F3}°N, {1:F3}°E)", item.lat, item.lon);
                GUILayout.Label(geoDesc, noteStyle, GUILayout.Width(130));

                GUILayout.FlexibleSpace();

                GUI.backgroundColor = isEditingThis ? new Color(0.2f, 0.85f, 0.4f) : Color.white;
                if (GUILayout.Button(isEditingThis ? "收起" : "⚙️微调", GUILayout.Width(50), GUILayout.Height(20)))
                {
                    if (activeMetaEditIndex == i)
                    {
                        activeMetaEditIndex = -1;
                    }
                    else
                    {
                        activeMetaEditIndex = i;
                        SyncCrsIndices(item);
                    }
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = new Color(0.85f, 0.3f, 0.3f);
                if (GUILayout.Button("✕", GUILayout.Width(25), GUILayout.Height(20)))
                {
                    pendingBatchMetas.RemoveAt(i);
                    if (activeMetaEditIndex == i) activeMetaEditIndex = -1;
                    else if (activeMetaEditIndex > i) activeMetaEditIndex--;
                    break;
                }
                GUI.backgroundColor = Color.white;

                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // Single File Fine-tuning Box (If activeMetaEditIndex >= 0)
            if (activeMetaEditIndex >= 0 && activeMetaEditIndex < pendingBatchMetas.Count)
            {
                var curEditMeta = pendingBatchMetas[activeMetaEditIndex];
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                GUI.color = new Color(0.3f, 1f, 0.5f);
                GUILayout.Label(string.Format("⚙️ 正在单独微调: #{0:D2} {1}", activeMetaEditIndex + 1, Path.GetFileName(curEditMeta.filePath)), boldStyle);
                GUI.color = Color.white;
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("⚡ 一键将当前坐标系应用至全部点云", GUILayout.Height(22)))
                {
                    foreach (var m in pendingBatchMetas)
                    {
                        m.selectedCRS = curEditMeta.selectedCRS;
                        m.centralMeridian = curEditMeta.centralMeridian;
                        m.utmZone = curEditMeta.utmZone;
                        LasMetadataDetector.CalculateGeoLocation(m);
                    }
                    ShowToast("已成功将该坐标系同步至所有点云！");
                }
                GUILayout.EndHorizontal();

                DrawCrsSelectionControls(curEditMeta);

                GUILayout.EndVertical();
                GUILayout.Space(6);
            }

            // Project Master CRS Settings Box
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("🌐 整条线路项目主基准坐标系 (用于统一标塔、档距度量及成果LAS输出)", subHeaderStyle);
            GUILayout.FlexibleSpace();
            string masterDesc = (masterProjectCRS != null)
                ? (masterProjectCRS.selectedCRS == CRSType.CGCS2000_3Deg_NoZone ? $"CGCS2000 {masterProjectCRS.centralMeridian}°E" : masterProjectCRS.selectedCRS.ToString())
                : "未设定";
            GUILayout.Label(masterDesc, badgeStyle);
            GUILayout.EndHorizontal();

            if (activeMetaEditIndex < 0 && masterProjectCRS != null)
            {
                DrawCrsSelectionControls(masterProjectCRS);
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // Power Grid Spatial Hierarchy Settings Box
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("⚡ 电网空间层级挂接设定 (省份 · 城市 · 线路)", subHeaderStyle);

            GUILayout.Space(4);
            // Province Selection
            GUILayout.BeginHorizontal();
            GUILayout.Label("省份 (Province):", GUILayout.Width(110));
            GUILayout.Label(editProvince, boldStyle, GUILayout.Width(90));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(provincePage == 0 ? "更多省份 ▸" : "◂ 常用省份", GUILayout.Width(90), GUILayout.Height(20)))
            {
                provincePage = 1 - provincePage;
            }
            GUILayout.EndHorizontal();

            string[] commonProvs = provincePage == 0
                ? new string[] { "浙江省", "江苏省", "上海市", "安徽省", "山东省", "广东省", "四川省", "湖北省" }
                : new string[] { "河南省", "陕西省", "河北省", "江西省", "福建省", "湖南省", "重庆市", "北京市" };

            int curProvIdx = -1;
            for (int i = 0; i < commonProvs.Length; i++)
            {
                if (commonProvs[i] == editProvince) curProvIdx = i;
            }
            int clickedProv = GUILayout.SelectionGrid(curProvIdx, commonProvs, 4);
            if (clickedProv >= 0 && clickedProv < commonProvs.Length && clickedProv != curProvIdx)
            {
                editProvince = commonProvs[clickedProv];
                string[] cities;
                if (LasMetadataDetector.AdministrativeDivisions.TryGetValue(editProvince, out cities) && cities != null && cities.Length > 0)
                {
                    editCity = cities[0];
                }
            }

            GUILayout.Space(4);
            // City Selection Cascading
            GUILayout.BeginHorizontal();
            GUILayout.Label("城市 (City):", GUILayout.Width(110));
            GUILayout.Label(editCity, boldStyle);
            GUILayout.EndHorizontal();

            string[] cityList;
            if (LasMetadataDetector.AdministrativeDivisions.TryGetValue(editProvince, out cityList) && cityList != null)
            {
                int curCityIdx = -1;
                for (int i = 0; i < cityList.Length; i++)
                {
                    if (cityList[i] == editCity) curCityIdx = i;
                }
                int clickedCity = GUILayout.SelectionGrid(curCityIdx, cityList, 4);
                if (clickedCity >= 0 && clickedCity < cityList.Length)
                {
                    editCity = cityList[clickedCity];
                }
            }

            GUILayout.Space(6);
            // Line Name Field
            GUILayout.BeginHorizontal();
            GUILayout.Label("线路名称 (Line):", GUILayout.Width(110));
            editLineName = GUILayout.TextField(editLineName);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUILayout.EndScrollView();

            GUILayout.Space(8);

            // Bottom Action Buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("取消", GUILayout.Width(80), GUILayout.Height(32)))
            {
                CancelImport();
            }

            GUILayout.FlexibleSpace();

            if (pendingBatchMetas.Count == 1)
            {
                GUI.color = new Color(0.7f, 0.8f, 0.9f);
                if (GUILayout.Button("直接导入为单档距", GUILayout.Width(130), GUILayout.Height(32)))
                {
                    ConfirmImport();
                }
                GUI.color = Color.white;
                GUILayout.Space(8);
            }

            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
            string goText = string.Format("🗺️ 进入天地图联合标定与切割 ▸ ({0}个点云)", pendingBatchMetas.Count);
            if (GUILayout.Button(goText, GUILayout.Width(280), GUILayout.Height(32)))
            {
                OpenCorridorCutter2D();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void DrawAnnotationTab()
        {
            var ctrl = AnnotationController.Instance;
            AnnotationTargetMode mode = ctrl != null ? ctrl.currentMode : AnnotationTargetMode.None;

            GUILayout.Label("P2 电力数字化交互标注闭环:", boldStyle);
            GUILayout.Space(2);

            // Toast feedback
            if (!string.IsNullOrEmpty(annotationToast))
            {
                GUI.color = new Color(0.3f, 1f, 0.4f);
                GUILayout.Label("✓ " + annotationToast, boldStyle);
                GUI.color = Color.white;
                GUILayout.Space(4);
            }

            // Current Mode Banner
            string modeName = "漫游浏览 (无标注)";
            string hint = "按住 Shift 移动鼠标即可实时显示三维空间靶点。";
            if (mode == AnnotationTargetMode.Tower)
            {
                modeName = "【杆塔标定模式】";
                hint = "按住 Shift + 鼠标左键点击铁塔塔顶中心，自动生成呼高双长方体包围盒。";
            }
            else if (mode == AnnotationTargetMode.Conductor)
            {
                modeName = "【导线弧垂标定】";
                hint = "按住 Shift + 鼠标左键依次点击两端塔挂点，系统自动在剖面搜索最低点并拟合悬链线。";
            }
            else if (mode == AnnotationTargetMode.Insulator)
            {
                modeName = "【绝缘子串标定】";
                hint = "按住 Shift 依次点击横担侧与金具侧挂点。";
            }
            else if (mode == AnnotationTargetMode.Reclassify)
            {
                modeName = "【选区语义修点】";
                hint = "在 3D 视图按住 Shift + 拖拽鼠标绘制矩形框选，选定后点击分类一键改写。";
            }

            GUI.color = new Color(0.2f, 0.8f, 1f);
            GUILayout.Label(modeName, headerStyle);
            GUI.color = Color.white;
            GUILayout.Label(hint, noteStyle);
            GUILayout.Space(6);

            annotationScroll = GUILayout.BeginScrollView(annotationScroll);

            // 1. Tower List
            var towerMgr = TowerAnnotator.Instance;
            int towerCount = towerMgr != null ? towerMgr.towers.Count : 0;
            GUILayout.Label($"🗼 已标定杆塔 ({towerCount} 基):", boldStyle);
            if (towerMgr != null)
            {
                for (int i = 0; i < towerMgr.towers.Count; i++)
                {
                    var t = towerMgr.towers[i];
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{t.towerNo} [H={t.totalHeight:F1}m, 呼高={t.nominalHeight:F1}m]", GUILayout.Width(180));
                    if (GUILayout.Button("微调", GUILayout.Width(45), GUILayout.Height(20)))
                    {
                        towerMgr.activeTower = t;
                    }
                    if (GUILayout.Button("删除", GUILayout.Width(45), GUILayout.Height(20)))
                    {
                        towerMgr.towers.RemoveAt(i);
                        break;
                    }
                    GUILayout.EndHorizontal();

                    if (towerMgr.activeTower == t)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"朝向: {t.yawAngle:F0}°", GUILayout.Width(80));
                        if (GUILayout.Button("↺-10°", GUILayout.Width(50), GUILayout.Height(18))) towerMgr.AdjustActiveYaw(-10f);
                        if (GUILayout.Button("↻+10°", GUILayout.Width(50), GUILayout.Height(18))) towerMgr.AdjustActiveYaw(10f);
                        if (GUILayout.Button("横担+2m", GUILayout.Width(65), GUILayout.Height(18))) towerMgr.AdjustActiveDimensions(2f, 0f);
                        GUILayout.EndHorizontal();
                    }
                }
            }
            GUILayout.Space(8);

            // 2. Conductor List
            var condMgr = ConductorAnnotator.Instance;
            int condCount = condMgr != null ? condMgr.conductors.Count : 0;
            GUILayout.Label($"⚡ 已标定导线 ({condCount} 根):", boldStyle);
            if (condMgr != null)
            {
                for (int i = 0; i < condMgr.conductors.Count; i++)
                {
                    var c = condMgr.conductors[i];
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{c.phaseName} [档距:{c.horizontalSpan:F0}m, 弧垂:{c.measuredSag:F2}m]", GUILayout.Width(220));
                    if (GUILayout.Button("删除", GUILayout.Width(45), GUILayout.Height(20)))
                    {
                        condMgr.conductors.RemoveAt(i);
                        break;
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.Space(8);

            // 3. Reclassification & Selection Panel
            GUILayout.Label($"🔲 选区修点 (已选中 {selectedPointIndices.Count} 点):", boldStyle);
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.8f, 0.4f, 0.2f);
            if (GUILayout.Button("[2]地面", GUILayout.Height(24))) ApplyClassToSelected(2);
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
            if (GUILayout.Button("[3]植被", GUILayout.Height(24))) ApplyClassToSelected(3);
            GUI.backgroundColor = new Color(0.8f, 0.3f, 1f);
            if (GUILayout.Button("[14]导线", GUILayout.Height(24))) ApplyClassToSelected(14);
            GUI.backgroundColor = new Color(1f, 0.8f, 0.1f);
            if (GUILayout.Button("[15]杆塔", GUILayout.Height(24))) ApplyClassToSelected(15);
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            var hist = AnnotationHistory.Instance;
            int uCount = hist != null ? hist.UndoCount : 0;
            int rCount = hist != null ? hist.RedoCount : 0;
            if (GUILayout.Button($"↩ 撤销 (Ctrl+Z) [{uCount}]", GUILayout.Height(22))) { if (hist != null) hist.Undo(); }
            if (GUILayout.Button($"↪ 重做 (Ctrl+Y) [{rCount}]", GUILayout.Height(22))) { if (hist != null) hist.Redo(); }
            if (GUILayout.Button("清空选区", GUILayout.Height(22))) selectedPointIndices.Clear();
            GUILayout.EndHorizontal();
            GUILayout.Space(12);

            // 4. Persistence & Export Actions
            GUILayout.Label("💾 数据持久化与成果闭环:", boldStyle);
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.1f, 0.8f, 0.4f);
            if (GUILayout.Button("💾 保存修改写回 LAS", GUILayout.Height(30)))
            {
                SaveLasFile();
            }
            GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
            if (GUILayout.Button("📦 导出算法训练集", GUILayout.Height(30)))
            {
                ExportTrainingDataset();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();
        }

        private void ApplyClassToSelected(byte newClass)
        {
            if (selectedPointIndices.Count == 0 || colorManager == null) return;

            byte[] oldClasses = new byte[selectedPointIndices.Count];
            byte[] ptsClasses = colorManager.PointClasses;
            for (int i = 0; i < selectedPointIndices.Count; i++)
            {
                int idx = selectedPointIndices[i];
                oldClasses[i] = (ptsClasses != null && idx >= 0 && idx < ptsClasses.Length) ? ptsClasses[idx] : (byte)1;
            }

            colorManager.UpdatePointsClass(selectedPointIndices.ToArray(), newClass);

            if (AnnotationHistory.Instance != null)
            {
                AnnotationHistory.Instance.RecordTransaction(selectedPointIndices.ToArray(), oldClasses, newClass, $"修改为类别 {newClass}");
            }

            ShowToast($"成功将 {selectedPointIndices.Count} 点重分类为 Class {newClass}");
        }

        private void SaveLasFile()
        {
            string srcLas = GetCurrentLasPath();
            if (string.IsNullOrEmpty(srcLas) || !File.Exists(srcLas))
            {
                ShowToast("未找到原始 LAS 文件路径！");
                return;
            }

            string outDir = Path.Combine(Application.dataPath, "../Exports");
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            string destLas = Path.Combine(outDir, Path.GetFileNameWithoutExtension(srcLas) + "_labeled.las");

            byte[] classes = colorManager != null ? colorManager.PointClasses : null;
            if (LasAnnotationWriter.SaveClassificationToLas(srcLas, destLas, classes, out string err))
            {
                ShowToast($"已成功流式回写 LAS: {Path.GetFileName(destLas)}");
            }
            else
            {
                ShowToast("写入失败: " + err);
            }
        }

        private void ExportTrainingDataset()
        {
            string srcLas = GetCurrentLasPath();
            if (string.IsNullOrEmpty(srcLas) || !File.Exists(srcLas))
            {
                ShowToast("未找到原始 LAS 文件路径！");
                return;
            }

            string outDir = Path.Combine(Application.dataPath, "../Exports");
            var towers = TowerAnnotator.Instance != null ? TowerAnnotator.Instance.towers : null;
            var conds = ConductorAnnotator.Instance != null ? ConductorAnnotator.Instance.conductors : null;
            byte[] classes = colorManager != null ? colorManager.PointClasses : null;

            if (LasAnnotationWriter.ExportTrainingDataset(srcLas, outDir, towers, conds, classes, out string outJson, out string outLas, out string err))
            {
                ShowToast($"已导出训练集: {Path.GetFileName(outJson)}");
            }
            else
            {
                ShowToast("导出失败: " + err);
            }
        }

        private void BatchColorizeCorridorElements()
        {
            var towerMgr = TowerAnnotator.Instance;
            var condMgr = ConductorAnnotator.Instance;
            if (towerMgr == null && condMgr == null) return;

            var viewer = FindObjectOfType<RuntimeViewerDX11>();
            if (viewer == null || viewer.points == null || viewer.points.Length == 0)
            {
                ShowToast("未检测到当前载入的点云数据");
                return;
            }

            if (colorManager == null) colorManager = FindObjectOfType<CorridorColorManager>();
            if (colorManager == null) return;

            var pts = viewer.points;
            byte[] currentClasses = colorManager.PointClasses;
            Dictionary<int, byte> targetChanges = new Dictionary<int, byte>();
            Dictionary<int, byte> oldValues = new Dictionary<int, byte>();

            // 1. Conductor Buffer Search (Class 14) - 1.5m cylinder buffer
            if (condMgr != null && condMgr.conductors != null)
            {
                float maxRadius = 1.5f;
                float maxR2 = maxRadius * maxRadius;

                for (int cIdx = 0; cIdx < condMgr.conductors.Count; cIdx++)
                {
                    var cond = condMgr.conductors[cIdx];
                    if (cond == null || cond.curvePoints == null || cond.curvePoints.Length < 2) continue;

                    Bounds curveBounds = new Bounds(cond.curvePoints[0], Vector3.zero);
                    for (int j = 1; j < cond.curvePoints.Length; j++) curveBounds.Encapsulate(cond.curvePoints[j]);
                    curveBounds.Expand(maxRadius * 2f);

                    for (int i = 0; i < pts.Length; i++)
                    {
                        Vector3 p = pts[i];
                        if (!curveBounds.Contains(p)) continue;

                        for (int j = 0; j < cond.curvePoints.Length - 1; j++)
                        {
                            Vector3 a = cond.curvePoints[j];
                            Vector3 b = cond.curvePoints[j + 1];
                            Vector3 ab = b - a;
                            float abLen2 = ab.sqrMagnitude;
                            if (abLen2 < 0.0001f) continue;

                            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / abLen2);
                            Vector3 proj = a + ab * t;
                            if ((p - proj).sqrMagnitude <= maxR2)
                            {
                                if (!targetChanges.ContainsKey(i))
                                {
                                    targetChanges[i] = 14;
                                    oldValues[i] = (currentClasses != null && i < currentClasses.Length) ? currentClasses[i] : (byte)1;
                                }
                                break;
                            }
                        }
                    }
                }
            }

            // 2. Tower Double-Box Search (Class 15) - Tower overrides conductor where they overlap
            if (towerMgr != null && towerMgr.towers != null)
            {
                for (int tIdx = 0; tIdx < towerMgr.towers.Count; tIdx++)
                {
                    var tower = towerMgr.towers[tIdx];
                    if (tower == null) continue;

                    // Coarse AABB pre-cull to avoid millions of trigonometric calculations
                    float maxExtent = Mathf.Max(tower.topBox.size.x, tower.bodyBox.size.x) * 0.75f;
                    float minY = tower.basePosition.y - 1f;
                    float maxY = tower.topPosition.y + 1f;

                    for (int i = 0; i < pts.Length; i++)
                    {
                        Vector3 p = pts[i];
                        if (p.y < minY || p.y > maxY ||
                            Mathf.Abs(p.x - tower.topPosition.x) > maxExtent ||
                            Mathf.Abs(p.z - tower.topPosition.z) > maxExtent)
                            continue;

                        if (tower.ContainsPoint(p))
                        {
                            targetChanges[i] = 15;
                            if (!oldValues.ContainsKey(i))
                            {
                                oldValues[i] = (currentClasses != null && i < currentClasses.Length) ? currentClasses[i] : (byte)1;
                            }
                        }
                    }
                }
            }

            if (targetChanges.Count == 0)
            {
                ShowToast("未在杆塔包围盒或导线缓冲区内检索到点云");
                return;
            }

            // Apply in batch to color manager
            colorManager.UpdatePointsClassBatch(targetChanges);

            // Record undo transactions separately for conductor (14) and tower (15) so Redo restores correct classes
            List<int> condIndices = new List<int>();
            List<byte> condOld = new List<byte>();
            List<int> towerIndices = new List<int>();
            List<byte> towerOld = new List<byte>();

            foreach (var kvp in targetChanges)
            {
                int idx = kvp.Key;
                byte newCls = kvp.Value;
                byte oldCls = oldValues[idx];
                if (newCls == 14)
                {
                    condIndices.Add(idx);
                    condOld.Add(oldCls);
                }
                else if (newCls == 15)
                {
                    towerIndices.Add(idx);
                    towerOld.Add(oldCls);
                }
            }

            if (condIndices.Count > 0)
            {
                AnnotationHistory.Instance?.RecordTransaction(
                    condIndices.ToArray(),
                    condOld.ToArray(),
                    14,
                    $"批量导线赋类 Class 14 ({condIndices.Count}点)"
                );
            }
            if (towerIndices.Count > 0)
            {
                AnnotationHistory.Instance?.RecordTransaction(
                    towerIndices.ToArray(),
                    towerOld.ToArray(),
                    15,
                    $"批量杆塔赋类 Class 15 ({towerIndices.Count}点)"
                );
            }

            int tCnt = towerMgr != null ? towerMgr.towers.Count : 0;
            int cCnt = condMgr != null ? condMgr.conductors.Count : 0;
            ShowToast($"★ 廊道一键赋类完成: {tCnt}基杆塔 + {cCnt}根导线，共赋类着色 {targetChanges.Count} 点！");
        }

        private string GetCurrentLasPath()
        {
            if (!string.IsNullOrEmpty(activeSegmentName))
            {
                var roots = CorridorHierarchyManager.GetOrBuildDefaultHierarchy();
                foreach (var prov in roots)
                {
                    foreach (var city in prov.children)
                    {
                        foreach (var line in city.children)
                        {
                            foreach (var seg in line.children)
                            {
                                if (seg.segmentData != null && seg.segmentData.name == activeSegmentName && File.Exists(seg.segmentData.lasPath))
                                {
                                    return seg.segmentData.lasPath;
                                }
                            }
                        }
                    }
                }
            }

            if (pendingImportMeta != null && !string.IsNullOrEmpty(pendingImportMeta.filePath) && File.Exists(pendingImportMeta.filePath))
            {
                return pendingImportMeta.filePath;
            }
            string defaultPath = "E:/unity/点云/17-18(17_18)_sign.las";
            if (File.Exists(defaultPath)) return defaultPath;
            return "E:/unity/点云/17-18(17_18).las";
        }

        public void ShowToast(string msg)
        {
            annotationToast = msg;
            toastTimer = 4.0f;
            Debug.Log("[CorridorHUD] " + msg);
        }

        private void DrawMarqueeSelection()
        {
            var ctrl = AnnotationController.Instance;
            if (ctrl == null || ctrl.currentMode != AnnotationTargetMode.Reclassify)
            {
                isMarqueeDragging = false;
                return;
            }

            Event e = Event.current;
            Vector3 mPos = new Vector3(e.mousePosition.x, Screen.height - e.mousePosition.y, 0);
            if (e.type == EventType.MouseDown && e.button == 0 && e.shift)
            {
                if (IsPointerOverUI(mPos)) return;
                isMarqueeDragging = true;
                marqueeStart = e.mousePosition;
                marqueeCurrent = e.mousePosition;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && isMarqueeDragging)
            {
                marqueeCurrent = e.mousePosition;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && isMarqueeDragging)
            {
                isMarqueeDragging = false;
                marqueeCurrent = e.mousePosition;

                float xMin = Mathf.Min(marqueeStart.x, marqueeCurrent.x);
                float xMax = Mathf.Max(marqueeStart.x, marqueeCurrent.x);
                float yMin = Mathf.Min(marqueeStart.y, marqueeCurrent.y);
                float yMax = Mathf.Max(marqueeStart.y, marqueeCurrent.y);

                if (xMax - xMin > 5f && yMax - yMin > 5f)
                {
                    Rect screenRect = new Rect(xMin, Screen.height - yMax, xMax - xMin, yMax - yMin);
                    var v = importer != null ? importer.GetComponent<RuntimeViewerDX11>() : FindObjectOfType<RuntimeViewerDX11>();
                    var actCam = CameraHelper.MainCamera;
                    if (v != null && actCam != null && v.points != null)
                    {
                        selectedPointIndices = PointSelector.SelectPointsInScreenRect(screenRect, actCam, v.points);
                        ShowToast($"已成功框选 {selectedPointIndices.Count} 个点，可按快捷键或点击按钮赋予分类");
                    }
                }
                e.Use();
            }

            if (isMarqueeDragging)
            {
                float x = Mathf.Min(marqueeStart.x, marqueeCurrent.x);
                float y = Mathf.Min(marqueeStart.y, marqueeCurrent.y);
                float w = Mathf.Abs(marqueeCurrent.x - marqueeStart.x);
                float h = Mathf.Abs(marqueeCurrent.y - marqueeStart.y);
                Rect boxRect = new Rect(x, y, w, h);

                GUI.color = new Color(0.2f, 0.8f, 1f, 0.25f);
                GUI.DrawTexture(boxRect, Texture2D.whiteTexture);
                GUI.color = new Color(0.3f, 1f, 0.5f, 0.95f);
                GUI.DrawTexture(new Rect(boxRect.x, boxRect.y, boxRect.width, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(boxRect.x, boxRect.y + boxRect.height - 2, boxRect.width, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(boxRect.x, boxRect.y, 2, boxRect.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(boxRect.x + boxRect.width - 2, boxRect.y, 2, boxRect.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }

        public static void ShowGlobalToast(string msg)
        {
            var hud = FindObjectOfType<CorridorHUD>();
            if (hud != null) hud.ShowToast(msg);
        }

        public static void EnsureAnnotationSystem()
        {
            AnnotationHistory.OnGlobalToast = (msg) => {
                var hud = FindObjectOfType<CorridorHUD>();
                if (hud != null) hud.ShowToast(msg);
            };
            Camera cam = CameraHelper.MainCamera;
            if (cam != null)
            {
                if (cam.GetComponent<Cursor3DIndicator>() == null) cam.gameObject.AddComponent<Cursor3DIndicator>();
                if (cam.GetComponent<AnnotationController>() == null) cam.gameObject.AddComponent<AnnotationController>();
                if (cam.GetComponent<TowerAnnotator>() == null) cam.gameObject.AddComponent<TowerAnnotator>();
                if (cam.GetComponent<ConductorAnnotator>() == null) cam.gameObject.AddComponent<ConductorAnnotator>();
                if (cam.GetComponent<InsulatorAnnotator>() == null) cam.gameObject.AddComponent<InsulatorAnnotator>();
                if (cam.GetComponent<AnnotationHistory>() == null) cam.gameObject.AddComponent<AnnotationHistory>();
                if (cam.GetComponent<MeasurementController>() == null) cam.gameObject.AddComponent<MeasurementController>();
            }
            else
            {
                var hudGo = FindObjectOfType<CorridorHUD>()?.gameObject;
                if (hudGo != null)
                {
                    if (hudGo.GetComponent<Cursor3DIndicator>() == null) hudGo.AddComponent<Cursor3DIndicator>();
                    if (hudGo.GetComponent<AnnotationController>() == null) hudGo.AddComponent<AnnotationController>();
                    if (hudGo.GetComponent<TowerAnnotator>() == null) hudGo.AddComponent<TowerAnnotator>();
                    if (hudGo.GetComponent<ConductorAnnotator>() == null) hudGo.AddComponent<ConductorAnnotator>();
                    if (hudGo.GetComponent<InsulatorAnnotator>() == null) hudGo.AddComponent<InsulatorAnnotator>();
                    if (hudGo.GetComponent<AnnotationHistory>() == null) hudGo.AddComponent<AnnotationHistory>();
                    if (hudGo.GetComponent<MeasurementController>() == null) hudGo.AddComponent<MeasurementController>();
                }
            }
        }
    }
}
