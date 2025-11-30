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

            // Simple hash for noise
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // Sample the source texture using URP's Blit texture
                half4 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

                // Convert to luminance (thermal cameras see heat, approximated by brightness)
                // Weight red/yellow tones higher as they often represent heat sources
                float lum = dot(col.rgb, float3(0.35, 0.45, 0.20));

                // Boost bright areas (hot spots like engine exhaust, explosions)
                float hotSpot = smoothstep(_HotThreshold, 1.0, lum);
                lum = lum + hotSpot * 0.3;

                // Apply contrast and brightness
                lum = saturate((lum - 0.5) * _Contrast + 0.5 + _Brightness);

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
