Shader "Unlit/WillshareCesiumUnlitTilesetShader_PointCloudAutoSize_new"
{
    Properties
    {
        _Visible("Visible", int) = 15
        _Color("Color", Color) = (1,1,1,1)
        _OverrideColor("OverrideColor", float) = 0
        _Contour("Contour", Vector) = (0,0,0,0)
        _HeightInterval("HeightInterval", float) = 0

        _GradientMap("Gradient Texture", 2D) = "white" {}
        _CycleRange("Cycle Range", float) = 50
        _MaxDistance("MaxDistance", float) = 1000
        
        _MaxPixel("MAXPIXEL", int) = 10
        _OriginColor("OriginColor", int) = 1
        
        _Tint("Tint", Color) = (1,1,1,1)
        _Brightness("Brightness", Range(0.2, 1.5)) = 1.05
        _Contrast("Contrast", Range(0.8, 1.5)) = 1.1
        _Saturation("Saturation", Range(0.5, 5)) = 0.9
        _Gamma("Gamma", Range(0.8, 1.2)) = 1.0
        _EDLStrength("EDL Strength", Range(0, 1.0)) = 0.4
        _EDLRadius("EDL Radius", Range(1, 5)) = 2.0

    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZTest LEqual
            ZWrite On
            ColorMask[_Visible]

            CGPROGRAM
            #pragma target 2.0
            #pragma require geometry
            #pragma multi_compile_fwdbase
            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry GS_Main

            #include "UnityCG.cginc"
            #define DEG2RAD 0.0174532925
            #define HALFFOV 20
            #define HALFSCREEN 540
            #define NEAR 1
            #define FAR 50
            #define MINPIXEL 1
          //  #define MAXPIXEL 10

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            float _Visible;
            half4 _Color;
            float _OverrideColor;
            float4 _Contour;
            float _HeightInterval;

            uniform sampler2D _GradientMap;
            uniform float _CycleRange;
            float _MaxDistance;

            int _MaxPixel;

            bool _OriginColor;
            
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

            appdata vert (appdata v)
            {
                appdata o = v;
                o.vertex = mul(unity_ObjectToWorld, v.vertex);

                if(_OriginColor == 1)
                {
                    o.color = v.color;
                }
                else
                {
                    float percent = o.vertex.y / _CycleRange;
                    o.color = tex2Dlod(_GradientMap, float4(percent, 0.5f, 0, 0));
                }

                return o;
            }

            [maxvertexcount(4)]
            void GS_Main(point appdata p[1], inout TriangleStream<v2f> triStream)
            {
                float3 pos = p[0].vertex.xyz;
                float3 cameraUp = UNITY_MATRIX_IT_MV[1].xyz;
                float3 cameraForward = _WorldSpaceCameraPos - pos;
                float dist = length(cameraForward);
                float3 tmp = cameraForward;
                tmp.y = 0;

                v2f newVert;
                //if (length(tmp) > 50)
                //{
                //}
                //else
                //{
                    //float _Size = lerp(_MinSize, _MaxSize, (dist - _Near) / _Far);
                    // _Size = dist * tan(HALFFOV * DEG2RAD) * 1 / HALFSCREEN * lerp(MAXPIXEL, MINPIXEL, pow(saturate(max(dist - NEAR, 0) / FAR), 0.3));
                
                if(p[0].color.a == 1)
                {
                    float _Size = dist * tan(HALFFOV * DEG2RAD) * 1 / HALFSCREEN * lerp(_MaxPixel, MINPIXEL, pow(saturate(max(dist - NEAR, 0) / FAR), 0.3));
                    float3 rightSize = normalize(cross(cameraUp, cameraForward)) * _Size * 0.5;
                    float3 cameraSize = _Size * normalize(cross(cameraForward, rightSize));
                    float4 color = p[0].color;

                    newVert.vertex = UnityWorldToClipPos(float4(pos + rightSize - cameraSize, 1));
                    newVert.uv = float2(1, 0);
                    newVert.color = color;
                    triStream.Append(newVert);
                    newVert.vertex = UnityWorldToClipPos(float4(pos - rightSize - cameraSize, 1));
                    newVert.uv = float2(0, 0);
                    newVert.color = color;
                    triStream.Append(newVert);
                    newVert.vertex = UnityWorldToClipPos(float4(pos + rightSize + cameraSize, 1));
                    newVert.uv = float2(1, 1);
                    newVert.color = color;
                    triStream.Append(newVert);
                    newVert.vertex = UnityWorldToClipPos(float4(pos - rightSize + cameraSize, 1));
                    newVert.uv = float2(0, 1);
                    newVert.color = color;
                    triStream.Append(newVert);
                }
                //}
            }

            fixed4 frag(v2f i) : SV_Target
            {
                //clip(_Visible - 0.5);
                clip(0.5 - length(i.uv - 0.5));

                float3 color = i.color.rgb;

                // 1. 先做 Gamma 校正（如果输入是 sRGB）
                color = pow(abs(color), 2.2); // sRGB to Linear

                // 2. 自然风格的 CSB（降低饱和度，轻微对比度）
                color = NaturalCSB(color, _Brightness, _Saturation, _Contrast);

                // 3. 可选：基于深度的轻微 EDL（简化版）
                // 注意：真正的 EDL 需要深度纹理，这里用距离模拟
                float dist = length(_WorldSpaceCameraPos - i.vertex);
                float edlFactor = 1.0 - saturate(dist / 200.0) * _EDLStrength * 0.15;
                color *= edlFactor;

                // 4. 转回 sRGB
                color = pow(abs(color), 1.0 / 2.2);

                // 5. 轻微的环境光（避免死黑）
                color += UNITY_LIGHTMODEL_AMBIENT.rgb * 0.05;
                
                i.color.rgb = saturate(color);

                // sample the texture
                fixed4 col = (i.color * (1 - _OverrideColor) + _OverrideColor * _Color);
                col.a = i.color.a;


                return col;
            }
            ENDCG
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZTest LEqual
            ZWrite On
            ColorMask 0

            CGPROGRAM
            #pragma target 2.0
            #pragma require geometry
            #pragma multi_compile_shadowcaster
            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry GS_Main

            #include "UnityCG.cginc"
            #define DEG2RAD 0.0174532925
            #define HALFFOV 20
            #define HALFSCREEN 540
            #define NEAR 1
            #define FAR 50
            #define MINPIXEL 1
           // #define MAXPIXEL 10

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float _Visible;
            half4 _Color;
            float _OverrideColor;
            float4 _Contour;
            float _HeightInterval;

            float _MaxDistance;

            int _MaxPixel;

            appdata vert(appdata v)
            {
                appdata o = v;
                o.vertex = mul(unity_ObjectToWorld, v.vertex);

                return o;
            }

            [maxvertexcount(4)]
            void GS_Main(point appdata p[1], inout TriangleStream<v2f> triStream)
            {
                float3 pos = p[0].vertex.xyz;
                float3 cameraUp = UNITY_MATRIX_IT_MV[1].xyz;
                float3 cameraForward = _WorldSpaceCameraPos - pos;
                float dist = length(cameraForward);
                float3 tmp = cameraForward;
                tmp.y = 0;

                v2f newVert;
                //if (length(tmp) > 50)
                //{
                //}
                //else
                //{
                    //float _Size = lerp(_MinSize, _MaxSize, (dist - _Near) / _Far);
                if(p[0].color.a == 1)
                {
                    float _Size = dist * tan(HALFFOV * DEG2RAD) * 1 / HALFSCREEN * lerp(_MaxPixel, MINPIXEL, pow(saturate(max(dist - NEAR, 0) / FAR), 0.3));
                    float3 rightSize = normalize(cross(cameraUp, cameraForward)) * _Size * 0.5;
                    float3 cameraSize = _Size * normalize(cross(cameraForward, rightSize));

                    newVert.vertex = UnityWorldToClipPos(float4(pos + rightSize - cameraSize, 1));
                    newVert.uv = float2(1, 0);
                    triStream.Append(newVert);
                    newVert.vertex = UnityWorldToClipPos(float4(pos - rightSize - cameraSize, 1));
                    newVert.uv = float2(0, 0);
                    triStream.Append(newVert);
                    newVert.vertex = UnityWorldToClipPos(float4(pos + rightSize + cameraSize, 1));
                    newVert.uv = float2(1, 1);
                    triStream.Append(newVert);
                    newVert.vertex = UnityWorldToClipPos(float4(pos - rightSize + cameraSize, 1));
                    newVert.uv = float2(0, 1);
                    triStream.Append(newVert);

                }
                //}
            }

            fixed4 frag(v2f i) : SV_Target
            {
                clip(0.5 - length(i.uv - 0.5));
                return 0;
            }
            ENDCG
        }
    }
}
