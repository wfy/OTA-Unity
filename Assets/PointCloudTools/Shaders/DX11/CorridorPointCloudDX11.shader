// Point Cloud DX11 Shader for Power Transmission Corridor
// High-performance ComputeBuffer rendering with Constant Screen-Space Pixel Size and Built-in Industrial Wireframe

Shader "OTA/CorridorPointCloudDX11"
{
    Properties
    {
        _Tint ("Tint Color", Color) = (1, 1, 1, 1)
        _UseTintColor ("Use Tint Color", Float) = 0.0
        _MaxPixel ("Max Pixel Size", Float) = 18.0
        _MinPixel ("Min Pixel Size", Float) = 3.5
        _MaxDistance ("Max Distance", Float) = 1500.0
    }

    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "PerformanceChecks" = "False" }
        LOD 200

        Pass
        {
            Name "ForwardBase"
            Tags { "LightMode" = "ForwardBase" }
            Cull Off
            ZTest LEqual
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma target 5.0
            #pragma vertex VS_Main
            #pragma fragment FS_Main
            #pragma geometry GS_Main
            #include "UnityCG.cginc"

            StructuredBuffer<half3> buf_Points;
            StructuredBuffer<fixed4> buf_Colors;
            uniform float4x4 _modelMatrix;

            #define DEG2RAD 0.0174532925
            #define HALFFOV 20.0
            #define HALFSCREEN 540.0
            #define NEAR 1.0

            fixed4 _Tint;
            float _UseTintColor;
            float _MaxPixel;
            float _MinPixel;
            float _MaxDistance;

            struct GS_INPUT
            {
                uint id : TEXCOORD0;
            };

            struct FS_INPUT
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                fixed4 color : COLOR;
            };

            GS_INPUT VS_Main(uint id : SV_VertexID)
            {
                GS_INPUT o;
                o.id = id;
                return o;
            }

            [maxvertexcount(4)]
            void GS_Main(point GS_INPUT p[1], inout TriangleStream<FS_INPUT> triStream)
            {
                uint id = p[0].id;
                float3 localPos = buf_Points[id];
                float3 worldPos = mul(_modelMatrix, float4(localPos, 1.0)).xyz;

                float3 camUpWorld = UNITY_MATRIX_I_V._m01_m11_m21;
                float3 cameraForward = _WorldSpaceCameraPos - worldPos;
                float dist = length(cameraForward);
                if (dist < 0.001) dist = 0.001;

                // Perspective and distance adaptive point size calculation
                // Exactly matching 3DTrackPlan_new / WillshareCesiumUnlitTilesetShader_PointCloudAutoSize
                float farDist = _MaxDistance > 1.0 ? _MaxDistance : 1500.0;
                float maxPx = _MaxPixel > 1.0 ? _MaxPixel : 18.0;
                float minPx = _MinPixel > 0.5 ? _MinPixel : 3.5;

                float pixelSize = lerp(maxPx, minPx, pow(saturate(max(dist - NEAR, 0.0) / farDist), 0.3));
                float _Size = dist * tan(HALFFOV * DEG2RAD) * (1.0 / HALFSCREEN) * pixelSize;

                // Build true orthonormal camera billboard basis
                float3 rightVec = normalize(cross(camUpWorld, cameraForward)) * (_Size * 0.5);
                float3 upVec    = normalize(cross(cameraForward, rightVec)) * (_Size * 0.5);

                bool isOrthographic = (unity_OrthoParams.w == 1.0);
                if (isOrthographic)
                {
                    rightVec *= 0.2;
                    upVec *= 0.2;
                }

                fixed4 pointColor = buf_Colors[id] * (1.0 - _UseTintColor) + _Tint * _UseTintColor;
                #if !UNITY_COLORSPACE_GAMMA
                pointColor.rgb = pointColor.rgb * pointColor.rgb;
                #endif
                pointColor.a = buf_Colors[id].a;

                FS_INPUT v;

                // Bottom-Left
                v.pos = UnityWorldToClipPos(float4(worldPos - rightVec - upVec, 1.0));
                v.uv = float2(0.0, 0.0);
                v.color = pointColor;
                triStream.Append(v);

                // Top-Left
                v.pos = UnityWorldToClipPos(float4(worldPos - rightVec + upVec, 1.0));
                v.uv = float2(0.0, 1.0);
                v.color = pointColor;
                triStream.Append(v);

                // Bottom-Right
                v.pos = UnityWorldToClipPos(float4(worldPos + rightVec - upVec, 1.0));
                v.uv = float2(1.0, 0.0);
                v.color = pointColor;
                triStream.Append(v);

                // Top-Right
                v.pos = UnityWorldToClipPos(float4(worldPos + rightVec + upVec, 1.0));
                v.uv = float2(1.0, 1.0);
                v.color = pointColor;
                triStream.Append(v);
            }

            fixed4 FS_Main(FS_INPUT i) : SV_Target
            {
                // Circular clipping: disc radius 0.5 from center (0.5, 0.5)
                clip(0.5 - length(i.uv - 0.5));
                return i.color;
            }
            ENDCG
        }
    }
    FallBack Off
}
