// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'

Shader "UnityCoder/PointCloud/DX11/PointCloudColorDx11-Pixel"
{
	Properties{
		_Tint("Tint", Color) = (1,1,1,1)
	}

	SubShader
	{
		Tags { "RenderType" = "Opaque"}
		Blend SrcAlpha OneMinusSrcAlpha

		
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
			};

			fixed4 _Tint;
			float _UseTintColor;

			ps_input vert(uint id : SV_VertexID, uint inst : SV_InstanceID)
			{
				ps_input o;
				//half3 worldPos = buf_Points[id];

				half3 worldPos = mul(_modelMatrix, float4(buf_Points[id], 1)).xyz;
				o.pos = mul(UNITY_MATRIX_VP, half4(worldPos,1.0f));


				o.color = buf_Colors[id] * (1 - _UseTintColor) + _Tint * _UseTintColor;
#if !UNITY_COLORSPACE_GAMMA
				o.color = o.color * o.color; // linear
#endif
				o.color.a = buf_Colors[id].a;
				return o;
			}

			float4 frag(ps_input i) : COLOR
			{
				//i.color.rgb = i.color.rgb + UNITY_LIGHTMODEL_AMBIENT.rgb;

				return i.color;
			}
			ENDCG
		}
	}
	Fallback Off
}