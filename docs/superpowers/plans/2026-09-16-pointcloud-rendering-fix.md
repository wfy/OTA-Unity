# 点云俯视视角条带化退化修复与性能优化实施方案

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 彻底消除俯视（Top-down / 垂直下视）视角下点云（特别是输电导线）呈现斜线状、条带状切片的几何退化缺陷，并通过开启 GPU 硬件 Early-Z 深度拒绝大幅提升渲染帧率。

**Architecture:** 摒弃存在空间混杂和 90° 俯视奇异性的世界空间/模型空间叉乘展开算法，统一采用相机视图空间（View Space）绝对正交展开生成正对相机的 Quad Billboard，结合点图元自适应像素尺寸（Point Splatting）弥合机载激光雷达扫描空隙；同时移除 Shader 中的 Alpha 混合（`Blend`），开启硬件级 Early-Z。

**Tech Stack:** Unity ShaderLab, HLSL/CG (Target 5.0 / Geometry Shader), DX11 ComputeBuffer, Unity C# (RuntimeViewerDX11 / CorridorColorManager).

## Global Constraints
- 严格遵循 `3DTrackPlan-点云双模式渲染模块技术方案.md` 第 8 章与第 9 章规范与算法实现。
- 绝不破坏现有工程中的分类调色（`CorridorColorManager`）、高程映射、Sobel 轮廓描边（`SobelOutline`）及空间拾取功能。
- 所有着色器修改需严格兼容 Unity 2019.4 LTS 及 DX11 图形 API。

---

### Task 1: 重构 Mesh 模式着色器 `WillshareCesiumUnlitTilesetShader_PointCloudAutoSize_new.shader`

**Files:**
- Modify: `Assets/PointCloudTools/Shaders/DX11/WillshareCesiumUnlitTilesetShader_PointCloudAutoSize_new.shader`

**Interfaces:**
- Consumes: `appdata (vertex, color)`, `_MaxPixel`, `_MaxDistance`, `_CycleRange`, `_OriginColor`
- Produces: `v2f (vertex, uv, color)` via View Space orthonormal Billboard Quad

- [ ] **Step 1: 重构 Pass "ForwardBase" 的几何着色器（GS_Main）**
  将世界空间叉乘 `cross(cameraUp, cameraForward)` 替换为 View Space 严格正交展开：
  先执行 `float4 viewPos = mul(UNITY_MATRIX_MV, p[0].vertex);`，通过 `-viewPos.z` 提取线性深度计算 `_Size`，并在 View Space 以 `(±halfSize, ±halfSize, 0, 0)` 展开 4 个顶点，乘 `UNITY_MATRIX_P` 输出。

- [ ] **Step 2: 重构 Pass "ShadowCaster" 的几何着色器（GS_Main）**
  以相同的 View Space 正交算法重构 ShadowCaster Pass 的 Billboard 展开，确保阴影投影与主视口完全吻合。

- [ ] **Step 3: 优化 Early-Z 与移除 Alpha 混合**
  在 Pass "ForwardBase" 与 "ShadowCaster" 中移除 `Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha`，显式设置 `Tags { "Queue" = "Geometry" "RenderType"="Opaque" }`，确保 `ZWrite On` 和 `ZTest LEqual`，利用 `clip(0.5 - length(i.uv - 0.5))` 配合 Early-Z 剔除遮挡片元。

---

### Task 2: 重构 Point 模式 Procedural 着色器 `CorridorPointCloudDX11.shader`

**Files:**
- Modify: `Assets/PointCloudTools/Shaders/DX11/CorridorPointCloudDX11.shader`

**Interfaces:**
- Consumes: `buf_Points (StructuredBuffer<half3>)`, `buf_Colors (StructuredBuffer<fixed4>)`, `_modelMatrix`
- Produces: `FS_INPUT (pos, uv, color)` via View Space orthonormal expansion

- [ ] **Step 1: 消除世界空间叉乘奇异性**
  顶点经 `_modelMatrix` 变换后，乘以 `UNITY_MATRIX_V` 转换至视图空间：
  `float4 viewPos = mul(UNITY_MATRIX_V, float4(worldPos, 1.0));`
  在视图空间进行自适应点尺寸计算与正交展开，彻底解决垂直下看时与 `camUpWorld` 平行引发的退化。

- [ ] **Step 2: 开启 Early-Z 深度剔除**
  移除 `Blend SrcAlpha OneMinusSrcAlpha`，设置纯不透明渲染队列与 `ZWrite On`，片段着色器保留抗锯齿圆盘裁切。

---

### Task 3: 优化调度层点尺寸与材质联动 `RuntimeViewerDX11.cs`

**Files:**
- Modify: `Assets/PointCloudTools/PointCloudViewerDX11/Scripts/RuntimeViewerDX11.cs`

**Interfaces:**
- Consumes: `currentRenderMode`, `points`, `pointColors`
- Produces: 运行时正确的点尺寸配置与材质自适应调度

- [ ] **Step 1: 优化 Mesh 模式下的默认 `_MaxPixel`**
  在 `UpdateViewBuffers(RenderMode mode)` 中，将 `meshMaterial.SetInt("_MaxPixel", 25)` 调整为更加自然细致的 `12`（避免过大像素导致大范围点云模糊成片，同时完美弥合导线间隙）。

- [ ] **Step 2: 增强 Point 模式下的 Shader 匹配与尺寸配置**
  若材质支持 `_MaxPixel` 或使用 `CorridorPointCloudDX11`，同步设置自适应像素尺寸，确保双模式下俯视表现一致。

---

### Task 4: 更新场景构建器与材质默认配置 `P0SceneUpdater.cs`

**Files:**
- Modify: `Assets/Editor/P0SceneUpdater.cs`

**Interfaces:**
- Consumes: `P0-LasView.unity` 资产路径
- Produces: 升级后的场景材质配置与默认参数

- [ ] **Step 1: 校准 P0 场景更新器的材质属性**
  确保 `P0SceneUpdater` 中加载 `autoSizeMat` 时设置 `_MaxPixel` 为 `12`，验证材质引用的 Shader 与参数完整有效。

---

### Task 5: 验证与复核 (Verification & Review)

**Files:**
- Review: 所有修改后的 Shader 与 C# 脚本

- [ ] **Step 1: 代码语法与 HLSL 语义全面复核**
  检查矩阵相乘顺序、向量维度、齐次坐标、宏定义（`HALFFOV`、`HALFSCREEN` 等）的一致性。

- [ ] **Step 2: 对照方案规范验证**
  比对 `3DTrackPlan-点云双模式渲染模块技术方案.md` 第 8 章与第 9 章的源码规范，确认 View Space 展开算法与 Early-Z 优化完全达标。
