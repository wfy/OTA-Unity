# Task 2: 重构 Point 模式 Procedural 着色器 CorridorPointCloudDX11.shader

**Files:**
- Modify: `e:/unity/OTA-Unity/Assets/PointCloudTools/Shaders/DX11/CorridorPointCloudDX11.shader`

**Requirements & Background:**
`CorridorPointCloudDX11.shader` 是专为输电通道 Procedural ComputeBuffer 设计的高性能点云着色器。
原实现使用世界空间叉乘 `cross(camUpWorld, cameraForward)`，在垂直俯视视角下存在视线与相机正交向量退化风险；同时开启了 `Blend SrcAlpha OneMinusSrcAlpha`，破坏了 Early-Z。

**Detailed Tasks:**
1. **重构 ForwardBase Pass 的 GS_Main 为 View Space 绝对正交展开**：
   - 顶点由模型空间变换至世界空间后，乘 `UNITY_MATRIX_V` 变换至相机视图空间：
     ```hlsl
     float3 localPos = buf_Points[id];
     float3 worldPos = mul(_modelMatrix, float4(localPos, 1.0)).xyz;
     float4 viewPos = mul(UNITY_MATRIX_V, float4(worldPos, 1.0));
     float dist = -viewPos.z;
     if (dist < 0.001) dist = 0.001;
     ```
   - 自适应像素尺寸计算保持：
     ```hlsl
     float farDist = _MaxDistance > 1.0 ? _MaxDistance : 1500.0;
     float maxPx = _MaxPixel > 1.0 ? _MaxPixel : 18.0;
     float minPx = _MinPixel > 0.5 ? _MinPixel : 3.5;
     float pixelSize = lerp(maxPx, minPx, pow(saturate(max(dist - NEAR, 0.0) / farDist), 0.3));
     float _Size = dist * tan(HALFFOV * DEG2RAD) * (1.0 / HALFSCREEN) * pixelSize;
     float halfSize = _Size * 0.5;
     bool isOrthographic = (unity_OrthoParams.w == 1.0);
     if (isOrthographic) halfSize *= 0.2;
     ```
   - View Space 展开 4 个正交顶点并乘 `UNITY_MATRIX_P`：
     ```hlsl
     float4 offsets[4] = {
         float4(-halfSize, -halfSize, 0, 0),
         float4(-halfSize,  halfSize, 0, 0),
         float4( halfSize, -halfSize, 0, 0),
         float4( halfSize,  halfSize, 0, 0)
     };
     float2 uvs[4] = { float2(0.0, 0.0), float2(0.0, 1.0), float2(1.0, 0.0), float2(1.0, 1.0) };
     for (int k = 0; k < 4; k++)
     {
         FS_INPUT v;
         v.pos = mul(UNITY_MATRIX_P, viewPos + offsets[k]);
         v.uv = uvs[k];
         v.color = pointColor;
         triStream.Append(v);
     }
     ```
2. **移除 Alpha 混合开启 Early-Z**：
   - 移除 `Blend SrcAlpha OneMinusSrcAlpha`；
   - 保持 `Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "PerformanceChecks" = "False" }`；
   - 确保 `ZWrite On` 和 `ZTest LEqual`。
