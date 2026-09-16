// use this in meshes with DX11 mode (because DX11 doesnt support point size)
// original shader by "smb02dunnal" http://forum.unity3d.com/threads/billboard-geometry-shader.169415/
// modified by unitycoder.com, this version supports transform position & scale

Shader "UnityCoder/PointMeshSizeDX11TriBillOffset-ScaleByBlueRed"
{
	Properties
	{
		_Color("ColorTint", Color) = (1,1,1,1)
		_MinSize("MinSize", Float) = 1
		_MaxSize("MaxSize", Float) = 30
		//_StartColor("Start Color", Color) = (0,0,1,1)
		//_EndColor("End Color", Color) = (1,0,0,1)
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
			fixed4 _Color;
			//fixed4 _StartColor;
			//fixed4 _EndColor;

			//const float HCV_EPSILON = 1e-10;

			//// Converts a value from linear RGB to HCV (Hue, Chroma, Value)
			//float3 rgb_to_hcv(float3 rgb)
			//{
			//	// Based on work by Sam Hocevar and Emil Persson
			//	float4 P = (rgb.g < rgb.b) ? float4(rgb.bg, -1.0, 2.0 / 3.0) : float4(rgb.gb, 0.0, -1.0 / 3.0);
			//	float4 Q = (rgb.r < P.x) ? float4(P.xyw, rgb.r) : float4(rgb.r, P.yzx);
			//	float C = Q.x - min(Q.w, Q.y);
			//	float H = abs((Q.w - Q.y) / (6 * C + HCV_EPSILON) + Q.z);
			//	return float3(H, C, Q.x);
			//}

			//// Converts from linear RGB to HSV https://gist.github.com/unitycoder/aaf94ddfe040ec2da93b58d3c65ab9d9
			//float3 rgb_to_hsv(float3 rgb)
			//{
			//	float3 HCV = rgb_to_hcv(rgb);
			//	float S = HCV.y / (HCV.z + HCV_EPSILON);
			//	return float3(HCV.x, S, HCV.z);
			//}

			GS_INPUT VS_Main(appdata v)
			{
				GS_INPUT o = (GS_INPUT)0;
				o.pos = UnityObjectToClipPos(v.vertex);

				fixed4 col = v.color;
				#if !UNITY_COLORSPACE_GAMMA
				col = col * col; // linear
				#endif
				o.color = col * _Color;
				return o;
			}

			[maxvertexcount(3)]
			void GS_Main(point GS_INPUT p[1], inout TriangleStream<FS_INPUT> triStream)
			{
				//float3 origHue = rgb_to_hsv(p[0].color.rgb)*255;
				//float3 startHue = rgb_to_hsv(_StartColor.rgb)*255;
				//float3 endHue = rgb_to_hsv(_EndColor.rgb)*255;
				//float hueDistanceToMin = (min(abs(startHue - origHue), 360 - abs(startHue - origHue)))/180;
				//float hueDistanceToMax = (min(abs(endHue - origHue), 360 - abs(endHue - origHue)))/180;
				////float scalar = (-p[0].color.b + p[0].color.r) * 0.5f + 0.5f;
				//float scalar = 1-((lerp(-1,0,hueDistanceToMin) + lerp(0,2,hueDistanceToMax)) * 0.5f + 0.5f);
				////float scalar = (1-hueDistanceToMin)+hueDistanceToMax;
				////float scaledSize = lerp(_MinSize, _MaxSize, scalar);
				//float scaledSize = lerp(_MinSize, _MaxSize, scalar);

				float scalar = -p[0].color.b + p[0].color.r; // only uses Blue vs. Red
				float scaledSize = lerp(_MinSize, _MaxSize, scalar);

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