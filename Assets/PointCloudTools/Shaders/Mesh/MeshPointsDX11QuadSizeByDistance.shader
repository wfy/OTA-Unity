// DX11 mesh shader, scales point size by distance and min max sizes

Shader "Custom/PointMeshDX11QuadSizeByDistance" 
{
	Properties 
	{
		_MinDist("Min Distance", float) = 0
		_MaxDist("Max Distance", float) = 100
		_MinSize("Min Size", float) = 0.01
		_MaxSize("Max Size", float) = 0.1
	}

	SubShader 
	{
		Pass
		{
			Tags { "RenderType"="Opaque" }
			LOD 200
		
			CGPROGRAM
			#pragma vertex VS_Main
			#pragma fragment FS_Main
			#pragma geometry GS_Main
			
			struct appdata
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
			};
		
			struct GS_INPUT
			{
				float4	pos		: POSITION;
				fixed4 color 	: COLOR;
			};

			struct FS_INPUT
			{
				float4	pos		: POSITION;
				fixed4 color 	: COLOR;
			};

			float _MinDist, _MaxDist;
			float _MinSize, _MaxSize;

			GS_INPUT VS_Main(appdata v)
			{
				GS_INPUT o = (GS_INPUT)0;
				o.pos = UnityObjectToClipPos(v.vertex);

				fixed4 col = v.color;
				#if !UNITY_COLORSPACE_GAMMA
				col = col*col; // linear
				#endif
				o.color = col;
				return o;
			}

			[maxvertexcount(4)]
			void GS_Main(point GS_INPUT p[1], inout TriangleStream<FS_INPUT> triStream)
			{
				float dist = distance(p[0].pos, _WorldSpaceCameraPos);
				float _Size = _MinSize + (dist - _MinDist) * (_MaxSize - _MinSize) / (_MaxDist - _MinDist);

				float width = _Size*(_ScreenParams.z-1);
				float height = _Size*(_ScreenParams.w-1);
				float4 vertPos = p[0].pos;
				FS_INPUT newVert;
				newVert.pos = vertPos + float4(-width,-height,0,0);
				newVert.color = p[0].color;
				triStream.Append(newVert);
				newVert.pos = vertPos + float4(width,-height,0,0);
				triStream.Append(newVert);
				newVert.pos = vertPos + float4(-width,height,0,0);
				triStream.Append(newVert);
				newVert.pos = vertPos + float4(width,height,0,0);
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
