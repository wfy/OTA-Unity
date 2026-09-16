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
            #define FAR 100
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
                    float farDist = _MaxDistance > 1.0 ? _MaxDistance : 1500.0;
                    float pixelSize = lerp((float)_MaxPixel, 6.0, pow(saturate(max(dist - NEAR, 0) / farDist), 0.3));
                    float _Size = dist * tan(HALFFOV * DEG2RAD) * 1 / HALFSCREEN * pixelSize;
                    float3 rightSize = normalize(cross(cameraUp, cameraForward)) * _Size * 0.5;
                    float3 cameraSize = _Size * normalize(cross(cameraForward, rightSize)) * 0.5;

                    bool isOrthographic = (unity_OrthoParams.w == 1.0);
                    if(isOrthographic)
                    {
                        cameraSize = cameraSize * 0.2f;
                        rightSize = rightSize * 0.2f;
                    }

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
                //}
            }

            fixed4 frag(v2f i) : SV_Target
            {
                //clip(_Visible - 0.5);
                clip(0.5 - length(i.uv - 0.5));
                // sample the texture
                fixed4 col = (i.color * (1 - _OverrideColor) + _OverrideColor * _Color);
                col.a = _Color.a;
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
            #define FAR 100
            #define MINPIXEL 1
           // #define MAXPIXEL 10

            struct appdata
            {
                float4 vertex : POSITION;
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
                    float farDist = _MaxDistance > 1.0 ? _MaxDistance : 1500.0;
                    float pixelSize = lerp((float)_MaxPixel, 6.0, pow(saturate(max(dist - NEAR, 0) / farDist), 0.3));
                    float _Size = dist * tan(HALFFOV * DEG2RAD) * 1 / HALFSCREEN * pixelSize;
                    float3 rightSize = normalize(cross(cameraUp, cameraForward)) * _Size * 0.5;
                    float3 cameraSize = _Size * normalize(cross(cameraForward, rightSize)) * 0.5;

                    bool isOrthographic = (unity_OrthoParams.w == 1.0);
                    if(isOrthographic)
                    {
                        cameraSize = cameraSize * 0.2f;
                        rightSize = rightSize * 0.2f;
                    }

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
