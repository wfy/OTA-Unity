// use this in meshes with DX11 mode (because DX11 doesnt support point size)
// original shader by "smb02dunnal" http://forum.unity3d.com/threads/billboard-geometry-shader.169415/
// modified by unitycoder.com, this version supports transform position & scale
// this version uses R = Hue index, G = not used, B = Scale

Shader "UnityCoder/PointMeshSizeDX11TriBillOffset-CustomRGBData"
{
	Properties
	{
		_GradientTexture("Gradient Texture", 2D) = "white" {}
		_MinSize("MinSize", Float) = 1
		_MaxSize("MaxSize", Float) = 100
	}

	SubShader
	{
		Pass
		{
			Tags { "RenderType" = "Opaque"}
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
				fixed4 color : COLOR;
			};

			struct FS_INPUT
			{
				float4	pos		: POSITION;
				fixed4 color : COLOR;
			};

			float _MinSize;
			float _MaxSize;
			sampler2D _GradientTexture;

			GS_INPUT VS_Main(appdata v)
			{
				GS_INPUT o = (GS_INPUT)0;
				o.pos = UnityObjectToClipPos(v.vertex);
				fixed4 col = tex2Dlod(_GradientTexture, float4(v.color.r, 0.5f, 0, 0)); // R value sets texture sampling position
				
				#if !UNITY_COLORSPACE_GAMMA
				col = col * col; // fake linear
				#endif
				col.a = v.color.b; // take vertex blue color as alpha (for size scalar)
				o.color = col;
				return o;
			}

			[maxvertexcount(3)]
			void GS_Main(point GS_INPUT p[1], inout TriangleStream<FS_INPUT> triStream)
			{
				// based on vertex Alpha color
				float scaledSize = lerp(_MinSize, _MaxSize, p[0].color.a);

				float width = scaledSize * (_ScreenParams.z - 1);
				float height = scaledSize * (_ScreenParams.w - 1);
				float4 vertPos = p[0].pos;
				FS_INPUT newVert;
				newVert.pos = vertPos + float4(-width,-height,0,0);
				newVert.color = p[0].color;
				triStream.Append(newVert);
				newVert.pos = vertPos + float4(width,0,0,0);
				triStream.Append(newVert);
				newVert.pos = vertPos + float4(-width,height,0,0);
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