// PointCloud Shader for DX11 Viewer with PointSize and ColorTint

Shader "UnityCoder/PointCloud/DX11/ColorSizeV2"
{
	Properties
	{
		_Tint("Tint", Color) = (1,1,1,1)
		_Size("Size", Float) = 0.01
	}

	SubShader
	{
		Pass
		{
			Tags { "Queue" = "Transparent" "RenderType" = "Opaque" }
			LOD 200

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

			struct GS_INPUT
			{
				uint id : TEXCOORD0;
			};

			struct FS_INPUT
			{
				half4  pos   : POSITION;
				fixed4 color : COLOR;
			};

			float _Size;
			fixed4 _Tint;
			float _UseTintColor;

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
				float3 pos = mul(_modelMatrix, float4(buf_Points[id], 1)).xyz;

				float3 camUpWorld = UNITY_MATRIX_I_V._m01_m11_m21;
				float3 cameraForward = _WorldSpaceCameraPos - pos;
				float3 rightSize = normalize(cross(camUpWorld, cameraForward)) * (_Size * 0.5);
				float3 cameraSize = normalize(cross(cameraForward, rightSize)) * (_Size * 0.5);

				fixed4 col = buf_Colors[id] * (1 - _UseTintColor) + _Tint * _UseTintColor;
				#if !UNITY_COLORSPACE_GAMMA
				col = col * col; // linear
				#endif
				col.a = buf_Colors[id].a;

				FS_INPUT newVert;
				newVert.pos = UnityWorldToClipPos(float4(pos + rightSize - cameraSize, 1));
				newVert.color = col;
				triStream.Append(newVert);
				newVert.pos = UnityWorldToClipPos(float4(pos + rightSize + cameraSize, 1));
				triStream.Append(newVert);
				newVert.pos = UnityWorldToClipPos(float4(pos - rightSize - cameraSize, 1));
				triStream.Append(newVert);
				newVert.pos = UnityWorldToClipPos(float4(pos - rightSize + cameraSize, 1));
				triStream.Append(newVert);
			}

			fixed4 FS_Main(FS_INPUT input) : COLOR
			{
				return input.color;
			}
			ENDCG
		}
	}
}