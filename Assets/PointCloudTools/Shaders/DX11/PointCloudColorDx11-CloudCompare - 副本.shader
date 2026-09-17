Shader "UnityCoder/PointCloud/DX11/PointCloudColorDx11-CloudCompare"
{
    Properties{
        _Tint("Tint", Color) = (1,1,1,1)
        _Brightness("Brightness", Range(0.8, 1.5)) = 1.05
        _Contrast("Contrast", Range(0.8, 1.5)) = 1.1
        _Saturation("Saturation", Range(0.5, 1.5)) = 0.9
        _Gamma("Gamma", Range(0.8, 1.2)) = 1.0
        _EDLStrength("EDL Strength", Range(0, 1.0)) = 0.4
        _EDLRadius("EDL Radius", Range(1, 5)) = 2.0
    }

    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        ZWrite On
        Cull Off
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            StructuredBuffer<half3> buf_Points;
            StructuredBuffer<fixed4> buf_Colors;
            uniform float4x4 _modelMatrix;

            struct ps_input {
                half4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float3 worldPos : TEXCOORD0;
                float depth : TEXCOORD1;
            };

            fixed4 _Tint;
            float _UseTintColor;
            float _Brightness;
            float _Contrast;
            float _Saturation;
            float _Gamma;
            float _EDLStrength;
            float _EDLRadius;

            // ========== 自然风格的 CSB 调整 ==========
            float3 NaturalCSB(float3 color, float brt, float sat, float con)
            {
                // 使用 Rec.709 亮度系数（更自然）
                float3 luminanceCoeff = float3(0.2126, 0.7152, 0.0722);
                
                // 亮度（轻微）
                float3 brtColor = color * brt;
                
                // 饱和度（降低而非提高，避免荧光感）
                float intensity = dot(brtColor, luminanceCoeff);
                float3 intensityColor = float3(intensity, intensity, intensity);
                float3 satColor = lerp(intensityColor, brtColor, sat);
                
                // 对比度（轻微 S 曲线，而非线性拉伸）
                float3 conColor = pow(satColor, con);
                
                return conColor;
            }

            // ========== 自然的色带映射（可选，用于高程显示）==========
            // CloudCompare 风格：Viridis 或 Plasma 色带，更自然
            fixed4 NaturalColorRamp(float t)
            {
                t = saturate(t);
                
                // Viridis 风格色带（蓝->绿->黄，更自然）
                fixed4 colors[5];
                colors[0] = fixed4(0.267, 0.004, 0.329, 1.0);   // 深紫
                colors[1] = fixed4(0.230, 0.322, 0.545, 1.0);   // 蓝紫
                colors[2] = fixed4(0.173, 0.627, 0.498, 1.0);   // 青绿
                colors[3] = fixed4(0.612, 0.839, 0.231, 1.0);   // 黄绿
                colors[4] = fixed4(0.993, 0.906, 0.144, 1.0);   // 黄色
                
                float segment = t * 4.0;
                int index = (int)segment;
                float frac = segment - index;
                
                if (index >= 4) return colors[4];
                return lerp(colors[index], colors[index + 1], frac);
            }

            ps_input vert(uint id : SV_VertexID, uint inst : SV_InstanceID)
            {
                ps_input o;
                half3 worldPos = mul(_modelMatrix, float4(buf_Points[id], 1)).xyz;
                o.pos = mul(UNITY_MATRIX_VP, half4(worldPos, 1.0f));
                o.worldPos = worldPos;
                
                // 计算深度（用于 EDL）
                float4 viewPos = mul(UNITY_MATRIX_V, half4(worldPos, 1.0f));
                o.depth = -viewPos.z;

                // 基础颜色（保留原始 RGB，不做强色带映射）
                fixed4 baseColor = buf_Colors[id] * (1 - _UseTintColor) + _Tint * _UseTintColor;
                
#if !UNITY_COLORSPACE_GAMMA
                baseColor.rgb = baseColor.rgb * baseColor.rgb;
#endif
                
                o.color = baseColor;
                o.color.a = buf_Colors[id].a;
                
                return o;
            }

            float4 frag(ps_input i) : COLOR
            {
                float3 color = i.color.rgb;

                // 1. 先做 Gamma 校正（如果输入是 sRGB）
                color = pow(abs(color), 2.2); // sRGB to Linear

                // 2. 自然风格的 CSB（降低饱和度，轻微对比度）
                color = NaturalCSB(color, _Brightness, _Saturation, _Contrast);

                // 3. 可选：基于深度的轻微 EDL（简化版）
                // 注意：真正的 EDL 需要深度纹理，这里用距离模拟
                float dist = length(_WorldSpaceCameraPos - i.worldPos);
                float edlFactor = 1.0 - saturate(dist / 200.0) * _EDLStrength * 0.15;
                color *= edlFactor;

                // 4. 转回 sRGB
                color = pow(abs(color), 1.0 / 2.2);

                // 5. 轻微的环境光（避免死黑）
                color += UNITY_LIGHTMODEL_AMBIENT.rgb * 0.05;

                // 6. 最终裁剪
                color = saturate(color);

                return float4(color, i.color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
