# Task 1: 重构 Mesh 模式着色器 WillshareCesiumUnlitTilesetShader_PointCloudAutoSize_new.shader

**Files:**
- Modify: `e:/unity/OTA-Unity/Assets/PointCloudTools/Shaders/DX11/WillshareCesiumUnlitTilesetShader_PointCloudAutoSize_new.shader`

**Requirements & Background:**
当前 `WillshareCesiumUnlitTilesetShader_PointCloudAutoSize_new.shader` 在垂直俯视（Pitch ≈ 90°）时，输电导线点云会严重退化为一截一截的斜线切片 / 条带状。
其根因是 GS 展开代码（第 97-115 行及第 214-233 行）使用了跨空间错误叉乘：
`float3 cameraUp = UNITY_MATRIX_IT_MV[1].xyz;`
`float3 cameraForward = _WorldSpaceCameraPos - pos;`
`float3 rightSize = normalize(cross(cameraUp, cameraForward)) * _Size * 0.5;`
并且着色器开启了 `Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha`，破坏了 GPU 硬件 Early-Z。

**Detailed Tasks:**
1. **重构 ForwardBase Pass 的 GS_Main**：
   - 模型顶点变换到 View Space（相机视图空间）：
     `float4 viewPos = mul(UNITY_MATRIX_MV, p[0].vertex);`
     `float dist = -viewPos.z;`
   - 自适应像素尺寸计算：
     `float farDist = _MaxDistance > 1.0 ? _MaxDistance : 1500.0;`
     `float pixelSize = lerp((float)_MaxPixel, 6.0, pow(saturate(max(dist - NEAR, 0.0) / farDist), 0.3));`
     `float _Size = dist * tan(HALFFOV * DEG2RAD) * (1.0 / HALFSCREEN) * pixelSize;`
     `float halfSize = _Size * 0.5;`
     处理正交相机兼容（`if (unity_OrthoParams.w == 1.0) halfSize *= 0.2;`）。
   - 在 View Space 严格按相机视口正交轴展开 4 个顶点：
     ```hlsl
     float4 offsets[4] = {
         float4( halfSize, -halfSize, 0, 0),
         float4(-halfSize, -halfSize, 0, 0),
         float4( halfSize,  halfSize, 0, 0),
         float4(-halfSize,  halfSize, 0, 0)
     };
     float2 uvs[4] = { float2(1,0), float2(0,0), float2(1,1), float2(0,1) };
     for (int i = 0; i < 4; i++) {
         v2f o;
         o.vertex = mul(UNITY_MATRIX_P, viewPos + offsets[i]);
         o.uv = uvs[i];
         o.color = p[0].color;
         triStream.Append(o);
     }
     ```
2. **重构 ShadowCaster Pass 的 GS_Main**：
   - 同样采用上述 View Space 正交展开算法，顶点乘 `UNITY_MATRIX_P` 输出。
3. **启用 Early-Z 并移除 Alpha 混合**：
   - 在 ForwardBase 与 ShadowCaster 两个 Pass 中，移除 `Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha`；
   - 确保 `Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }`；
   - 确保 `ZWrite On` 和 `ZTest LEqual`；
   - 片段着色器保留 `clip(0.5 - length(i.uv - 0.5))` 实现圆形切片与快速深度拒绝。
