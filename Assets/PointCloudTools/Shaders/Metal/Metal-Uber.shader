// Mac Point Cloud Shader (similar to DX11-V2)

Shader "UnityCoder/PointCloud/Metal/Uber" 
{
    Properties 
    {
		_Size("Size", Float) = 1.0
		[KeywordEnum(Off, On)] _EnableColor("Enable Tint",float) = 0
		_Tint("Color Tint", Color) = (0,1,0,1)
		[KeywordEnum(Off, On)] _EnableScaling("Enable Distance Scaling",float) = 0
		_MinDist("Min Distance", float) = 0
		_MaxDist("Max Distance", float) = 100
		_MinSize("Min Size", float) = 0.1
		_MaxSize("Max Size", float) = 1.0

    }

	SubShader 
	{
		Pass 
		{
			Tags { "RenderType"="Opaque"}
			Lighting Off
			CGPROGRAM
			#pragma target 4.5
			#pragma only_renderers metal
			#pragma exclude_renderers d3d11
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ENABLECOLOR_ON _ENABLECOLOR_OFF
			#pragma multi_compile _ENABLESCALING_ON _ENABLESCALING_OFF
			#include "UnityCG.cginc"

			StructuredBuffer<half3> buf_Points;
			StructuredBuffer<fixed3> buf_Colors;

			struct appdata {
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				fixed3 color : COLOR0;
				float psize : PSIZE;
			};
			
			float _Size;
			#ifdef _ENABLESCALING_ON
			float _MinDist, _MaxDist;
			float _MinSize, _MaxSize;
			#endif

			#ifdef _ENABLECOLOR_ON
			fixed4 _Tint;
			#endif

			v2f vert (uint id : SV_VertexID, uint inst : SV_InstanceID)
			{
				v2f o;
				half4 pos = half4(buf_Points[id],1.0f);
				#ifdef _ENABLESCALING_ON
				float dist = distance(buf_Points[id], _WorldSpaceCameraPos);
				#endif

				o.pos = UnityObjectToClipPos(pos);

				fixed3 col = fixed3(buf_Colors[id]);
				#ifdef _ENABLECOLOR_ON
				col *= _Tint;
				#endif

				#if !UNITY_COLORSPACE_GAMMA
				col = col*col; // linear hack
				#endif
				o.color = col;
				
				#ifdef _ENABLESCALING_ON
				_Size = _MinSize + (dist - _MinDist) * (_MaxSize - _MinSize) / (_MaxDist - _MinDist);
				#endif

				o.psize = _Size;
				return o;
			}
			
			fixed4 frag(v2f i) : COLOR
			{
				return fixed4(i.color,1);
			}

			ENDCG
		}
	}
	// i guess for DX11 editor needs this, otherwise warning
	Fallback "UnityCoder/PointCloud/DX11/ColorSizeV2"
}