Shader "VertexFragment/SobelOutlineCg"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM

            #pragma vertex VertMain
            #pragma fragment FragMain

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;
            sampler2D _CameraGBufferTexture2;
            sampler2D _OcclusionDepthMap;

            float _OutlineThickness;
            float _OutlineDepthMultiplier;
            float _OutlineDepthBias;
            float _OutlineNormalMultiplier;
            float _OutlineNormalBias;
            float _OutlineDensity;
            float _OutlineMaxDistance;
            float _OutlineDistanceFade;
            float _OutlineMaxOrthoSize;
            float _OutlineOrthoSizeFade;
            float4 _OutlineColor;

            struct VertData
            {
                float4 vertex : POSITION;
                float4 uv     : TEXCOORD0;
            };

            struct FragData
            {
                float4 vertex   : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            FragData VertMain(VertData input)
            {
                FragData output;

                output.vertex = float4(input.vertex.xy, 0.0, 1.0);
                output.texcoord = (input.vertex.xy + 1.0) * 0.5;

                // For Direct3D Build
                output.texcoord.y = 1.0 - output.texcoord.y;

                // For Open/WebGL build
                //output.texcoord.y = output.texcoord.y;

                return output;
            }

            float4 SobelSample(sampler2D t, float2 uv, float3 offset)
            {
                float4 pixelCenter = tex2D(t, uv);
                float4 pixelLeft   = tex2D(t, uv - offset.xz);
                float4 pixelRight  = tex2D(t, uv + offset.xz);
                float4 pixelUp     = tex2D(t, uv + offset.zy);
                float4 pixelDown   = tex2D(t, uv - offset.zy);

                return abs(pixelLeft - pixelCenter)  +
                       abs(pixelRight - pixelCenter) +
                       abs(pixelUp - pixelCenter)    +
                       abs(pixelDown - pixelCenter);
            }

            float SobelSampleDepth(sampler2D t, float2 uv, float3 offset)
            {
                bool isOrtho = (unity_OrthoParams.w > 0.5);
                float pixelCenter = 0, pixelLeft = 0, pixelRight = 0, pixelUp = 0, pixelDown = 0;

                if (isOrtho)
                {
                    pixelCenter = lerp(_ProjectionParams.y, _ProjectionParams.z, tex2D(t, uv).r);
                    pixelLeft   = lerp(_ProjectionParams.y, _ProjectionParams.z, tex2D(t, uv - offset.xz).r);
                    pixelRight  = lerp(_ProjectionParams.y, _ProjectionParams.z, tex2D(t, uv + offset.xz).r);
                    pixelUp     = lerp(_ProjectionParams.y, _ProjectionParams.z, tex2D(t, uv + offset.zy).r);
                    pixelDown   = lerp(_ProjectionParams.y, _ProjectionParams.z, tex2D(t, uv - offset.zy).r);
                }
                else
                {
                    pixelCenter = LinearEyeDepth(tex2D(t, uv).r);
                    pixelLeft   = LinearEyeDepth(tex2D(t, uv - offset.xz).r);
                    pixelRight  = LinearEyeDepth(tex2D(t, uv + offset.xz).r);
                    pixelUp     = LinearEyeDepth(tex2D(t, uv + offset.zy).r);
                    pixelDown   = LinearEyeDepth(tex2D(t, uv - offset.zy).r);
                }

                return abs(pixelLeft - pixelCenter)  +
                       abs(pixelRight - pixelCenter) +
                       abs(pixelUp - pixelCenter)    +
                       abs(pixelDown - pixelCenter);
            }

            float4 FragMain(FragData input) : SV_Target
            {
                float3 sceneColor = tex2D(_MainTex, input.texcoord).rgb;
                float3 color = sceneColor;
                float3 offset = float3((1.0 / _ScreenParams.x), (1.0 / _ScreenParams.y), 0.0) * _OutlineThickness;

                // -------------------------------------------------------------------------
                // Check if this geometry is occluded
                // -------------------------------------------------------------------------

                float occlusion = SobelSample(_OcclusionDepthMap, input.texcoord.xy, offset);

                if (occlusion > 0.0)
                {
                    return float4(sceneColor, 1.0);
                }

                // -------------------------------------------------------------------------
                // Fade out the outline for distant objects & background culling.
                // -------------------------------------------------------------------------

                float rawDepth = tex2D(_CameraDepthTexture, input.texcoord.xy).r;

                // 1. 空背景直接剔除，彻底杜绝在天空/无几何体区域产生黑晕
                #if defined(UNITY_REVERSED_Z)
                if (rawDepth <= 0.00001f) return float4(sceneColor, 1.0);
                #else
                if (rawDepth >= 0.99999f) return float4(sceneColor, 1.0);
                #endif

                // 2. 根据相机模式区分控制：透视模式用绝对米数，正交模式用 orthographicSize
                bool isOrtho = (unity_OrthoParams.w > 0.5);
                float alpha = 1.0;

                if (isOrtho)
                {
                    float currentOrthoSize = unity_OrthoParams.y * 0.5;
                    float maxOrtho = _OutlineMaxOrthoSize > 0.0 ? _OutlineMaxOrthoSize : 45.0;
                    float fadeOrtho = _OutlineOrthoSizeFade > 0.0 ? _OutlineOrthoSizeFade : 15.0;
                    float startFadeOrtho = max(0.1, maxOrtho - fadeOrtho);

                    if (currentOrthoSize > maxOrtho)
                    {
                        return float4(sceneColor, 1.0);
                    }
                    else if (currentOrthoSize > startFadeOrtho)
                    {
                        alpha = 1.0 - saturate((currentOrthoSize - startFadeOrtho) / fadeOrtho);
                    }
                }
                else
                {
                    float eyeDepth = LinearEyeDepth(rawDepth);
                    float maxDist = _OutlineMaxDistance > 0.0 ? _OutlineMaxDistance : 45.0;
                    float fadeDist = _OutlineDistanceFade > 0.0 ? _OutlineDistanceFade : 15.0;
                    float startFadeDist = max(0.1, maxDist - fadeDist);

                    if (eyeDepth > maxDist)
                    {
                        return float4(sceneColor, 1.0);
                    }
                    else if (eyeDepth > startFadeDist)
                    {
                        alpha = 1.0 - saturate((eyeDepth - startFadeDist) / fadeDist);
                    }
                }

                if (alpha <= 0.0)
                {
                    return float4(sceneColor, 1.0);
                }

                // -------------------------------------------------------------------------
                // Generate the outline
                // -------------------------------------------------------------------------

                // Get the sobel depth from our pre-sampled linear depth values
                float sobelDepth = SobelSampleDepth(_CameraDepthTexture, input.texcoord.xy, offset);
                
                sobelDepth = pow(abs(saturate(sobelDepth) * _OutlineDepthMultiplier), _OutlineDepthBias);

                // Sample the normals from the GBuffer to get our normal contribution
                float3 sobelNormalVec = abs(SobelSample(_CameraGBufferTexture2, input.texcoord.xy, offset).rgb);
                float sobelNormal = sobelNormalVec.x + sobelNormalVec.y + sobelNormalVec.z;
                sobelNormal = pow(abs(sobelNormal * _OutlineNormalMultiplier), _OutlineNormalBias);

                // Calculate the combined contribution between normals and depth
                float sobelOutline = saturate(max(sobelDepth, sobelNormal));
                sobelOutline = smoothstep(_OutlineDensity, 1.0, sobelOutline) * alpha;

                // Colorize the outline
                float3 outlineColor = lerp(sceneColor, _OutlineColor.rgb, clamp(_OutlineColor.a, 0.0f, 1.0f));
                color = lerp(sceneColor, outlineColor, sobelOutline);

                return float4(color, 1.0);
            }

            ENDCG
        }
    }
}