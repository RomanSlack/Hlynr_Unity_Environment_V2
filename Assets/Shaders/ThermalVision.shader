Shader "Hidden/ThermalVision"
{
    Properties
    {
        _Contrast ("Contrast", Range(0.5, 3.0)) = 1.5
        _Brightness ("Brightness", Range(-0.5, 0.5)) = 0.0
        _NoiseAmount ("Noise Amount", Range(0, 0.15)) = 0.03
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.1
        _ScanlineCount ("Scanline Count", Range(100, 800)) = 300
        _VignetteIntensity ("Vignette Intensity", Range(0, 1)) = 0.4
        _HotThreshold ("Hot Threshold", Range(0, 1)) = 0.7
        _BloomIntensity ("Bloom Intensity", Range(0, 2)) = 0.8
        _BloomThreshold ("Bloom Threshold", Range(0, 1)) = 0.5
        _BloomRadius ("Bloom Radius", Range(1, 20)) = 6.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "ThermalVision"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Contrast;
            float _Brightness;
            float _NoiseAmount;
            float _ScanlineIntensity;
            float _ScanlineCount;
            float _VignetteIntensity;
            float _HotThreshold;
            float _BloomIntensity;
            float _BloomThreshold;
            float _BloomRadius;

            // Simple hash for noise
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // Get luminance from color (weighted for thermal - reds/yellows are hotter)
            float getLuminance(half3 col)
            {
                return dot(col, float3(0.35, 0.45, 0.20));
            }

            // Sample and convert to thermal luminance
            float sampleThermalLum(float2 uv)
            {
                half4 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);
                float lum = getLuminance(col.rgb);

                // Boost hot spots
                float hotSpot = smoothstep(_HotThreshold, 1.0, lum);
                lum = lum + hotSpot * 0.3;

                // Apply contrast and brightness
                lum = saturate((lum - 0.5) * _Contrast + 0.5 + _Brightness);

                return lum;
            }

            // Smooth blur for bloom using two-pass separated Gaussian approximation
            // This samples in a disc pattern for smoother results
            float sampleBloom(float2 uv, float2 texelSize)
            {
                float bloom = 0.0;
                float totalWeight = 0.0;

                float radius = _BloomRadius;

                // Use a 13-tap disc pattern for smooth bloom at any distance
                // Golden angle disc sampling for even distribution
                const int SAMPLES = 13;
                const float goldenAngle = 2.39996323;

                for (int i = 0; i < SAMPLES; i++)
                {
                    // Distribute samples in a disc using golden angle
                    float r = sqrt((float(i) + 0.5) / float(SAMPLES));
                    float theta = float(i) * goldenAngle;

                    float2 offset = float2(cos(theta), sin(theta)) * r * radius * texelSize;
                    float2 sampleUV = uv + offset;

                    // Get luminance at this sample
                    half4 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, sampleUV);
                    float lum = getLuminance(col.rgb);

                    // Only bloom bright areas above threshold
                    float bloomLum = max(0, lum - _BloomThreshold) / (1.0 - _BloomThreshold + 0.001);
                    bloomLum = bloomLum * bloomLum; // Square for more intensity on bright spots

                    // Gaussian-like weight based on distance from center
                    float weight = 1.0 - r * 0.5;
                    weight = weight * weight;

                    bloom += bloomLum * weight;
                    totalWeight += weight;
                }

                // Also do a second larger ring for soft falloff
                const int OUTER_SAMPLES = 8;
                for (int j = 0; j < OUTER_SAMPLES; j++)
                {
                    float theta = float(j) * (6.28318 / float(OUTER_SAMPLES));
                    float2 offset = float2(cos(theta), sin(theta)) * radius * 1.5 * texelSize;
                    float2 sampleUV = uv + offset;

                    half4 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, sampleUV);
                    float lum = getLuminance(col.rgb);

                    float bloomLum = max(0, lum - _BloomThreshold) / (1.0 - _BloomThreshold + 0.001);
                    bloomLum = bloomLum * bloomLum;

                    float weight = 0.3; // Outer ring has less weight

                    bloom += bloomLum * weight;
                    totalWeight += weight;
                }

                return bloom / totalWeight;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // Get texel size for blur
                float2 texelSize = 1.0 / _ScreenParams.xy;

                // Get base thermal luminance
                float lum = sampleThermalLum(uv);

                // Calculate bloom from bright areas
                float bloom = sampleBloom(uv, texelSize) * _BloomIntensity;

                // Add bloom to luminance with soft blend
                lum = saturate(lum + bloom);

                // Add subtle noise (sensor noise)
                float noise = hash(uv * 1000.0 + _Time.y * 100.0);
                noise = (noise - 0.5) * _NoiseAmount;
                lum += noise;

                // Scanlines for that authentic drone/FLIR look
                float scanline = sin(uv.y * _ScanlineCount * 3.14159) * 0.5 + 0.5;
                scanline = lerp(1.0, scanline, _ScanlineIntensity);
                lum *= scanline;

                // Vignette (darker edges like real camera optics)
                float2 vignetteUV = uv * 2.0 - 1.0;
                float vignette = 1.0 - dot(vignetteUV, vignetteUV) * _VignetteIntensity;
                vignette = saturate(vignette);
                lum *= vignette;

                // Final grayscale output with slight green tint (night vision style)
                half3 thermalColor = half3(lum * 0.95, lum, lum * 0.92);

                return half4(thermalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
