Shader "Unlit/WillshareCesiumUnlitTilesetShader_PointCloudAutoSize"
{
    Properties
    {
        _Tint("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {

        Pass
        {
            Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
            LOD 100
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma target 5.0
            #pragma require geometry
            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry GS_Main

            #include "UnityCG.cginc"
            #define DEG2RAD 0.0174532925
            #define HALFFOV 20
            #define HALFSCREEN 540
            #define NEAR 1
            #define FAR 20
            #define MINPIXEL 1
            #define MAXPIXEL 10

            StructuredBuffer<half3> buf_Points;
            StructuredBuffer<fixed4> buf_Colors;
            uniform float4x4 _modelMatrix;

            struct GS_INPUT
            {
                uint id : VERTEXID;
            };

            struct v2f
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            fixed4 _Tint;
            float _UseTintColor;

            GS_INPUT vert (uint id : SV_VertexID)
            {
                GS_INPUT o;
                o.id = id;
                return o;
            }

            [maxvertexcount(4)]
            void GS_Main(point GS_INPUT p[1], inout TriangleStream<v2f> triStream)
            {
                uint id = p[0].id;
                float3 pos = mul(_modelMatrix, float4(buf_Points[id], 1)).xyz;
                float3 cameraUp = UNITY_MATRIX_IT_MV[1].xyz;
                float3 cameraForward = _WorldSpaceCameraPos - pos;
                float dist = length(cameraForward);
                //float _Size = lerp(_MinSize, _MaxSize, (dist - _Near) / _Far);
                float _Size = dist * tan(HALFFOV * DEG2RAD) * 1 / HALFSCREEN *lerp(MAXPIXEL, MINPIXEL, pow(saturate(max(dist - NEAR, 0) / FAR), 0.3));
                float3 rightSize = normalize(cross(cameraUp, cameraForward)) * _Size;
                float3 cameraSize = _Size * normalize(cross(cameraForward, rightSize));

                fixed4 col = buf_Colors[id] * (1 - _UseTintColor) + _Tint * _UseTintColor;
#if !UNITY_COLORSPACE_GAMMA// && COLOR_CORRECTION
                //col = GammaToLinearSpace(col);
                col = col * col; // linear
#endif
                col.a = buf_Colors[id].a;

                v2f newVert;
                newVert.vertex = UnityWorldToClipPos(float4(pos + rightSize - cameraSize, 1));
                newVert.color = col;
                triStream.Append(newVert);
                newVert.vertex = UnityWorldToClipPos(float4(pos + rightSize + cameraSize, 1));
                newVert.color = col;
                triStream.Append(newVert);
                newVert.vertex = UnityWorldToClipPos(float4(pos - rightSize - cameraSize, 1));
                newVert.color = col;
                triStream.Append(newVert);
                newVert.vertex = UnityWorldToClipPos(float4(pos - rightSize + cameraSize, 1));
                newVert.color = col;
                triStream.Append(newVert);
            }

            fixed4 frag(v2f i) : COLOR
            {
                return i.color;
            }
            ENDCG
        }
    }
}
