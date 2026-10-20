// UI Image / RawImage shader for Unity 6000.3 (Unity 6.3), URP.
// Usage: create a Material, choose Utopia/UI/Glitch, assign it to Image.Material.
// Use Image Type = Simple and a standalone, non-atlased texture first.
// No C# script or Shader Graph asset is required.
// Intensity = 0 restores the original texture; UI tint/alpha still apply.
// UI masking follows Unity's standard stencil and RectMask2D conventions.
// Reference: Unity-Technologies/Graphics, 6000.3/staging, CanvasPass.hlsl.

Shader "Utopia/UI/Glitch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _Intensity ("Intensity", Range(0, 1)) = 0.7
        _Distortion ("Distortion", Range(0, 0.15)) = 0.035
        _RGBSplit ("RGB Split", Range(0, 0.05)) = 0.006
        _Speed ("Speed", Range(1, 60)) = 18
        _BandCount ("Band Count", Range(1, 128)) = 32
        _BandThreshold ("Band Threshold", Range(0, 1)) = 0.65
        _NoiseAmount ("Noise Amount", Range(0, 0.5)) = 0.1
        _NoiseScale ("Noise Scale", Range(16, 512)) = 192
        _ScanlineAmount ("Scanline Amount", Range(0, 1)) = 0.12
        _ScanlineCount ("Scanline Count", Range(1, 512)) = 180
        _FlickerAmount ("Flicker Amount", Range(0, 1)) = 0.15

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _UIMaskSoftnessX ("Mask Softness X", Float) = 0
        [HideInInspector] _UIMaskSoftnessY ("Mask Softness Y", Float) = 0
        [HideInInspector] [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "False"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        // RGB is premultiplied by alpha at the end of frag().
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "UI Glitch"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // CanvasRenderer supplies the texture, vertex color, and clipping data.
            half4 _Color;
            half4 _TextureSampleAdd;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            float _Intensity;
            float _Distortion;
            float _RGBSplit;
            float _Speed;
            float _BandCount;
            float _BandThreshold;
            float _NoiseAmount;
            float _NoiseScale;
            float _ScanlineAmount;
            float _ScanlineCount;
            float _FlickerAmount;

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 mask : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Raw matrices also cover the Screen Space - Overlay Canvas path.
                output.positionCS = mul(unity_MatrixVP,
                    mul(unity_ObjectToWorld, float4(input.positionOS.xyz, 1.0)));
                output.uv = input.uv;
                output.color = input.color * _Color;

                #ifdef UNITY_UI_CLIP_RECT
                    float2 pixelSize = output.positionCS.w;
                    pixelSize /= max(abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy)),
                                     float2(0.00001, 0.00001));
                    float4 rect = clamp(_ClipRect, -2e10, 2e10);
                    output.mask = float4(
                        input.positionOS.xy * 2.0 - rect.xy - rect.zw,
                        0.25 / (0.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY)
                                + abs(pixelSize)));
                #endif

                return output;
            }

            // Keep this arithmetic at float precision: half loses the hash detail.
            float GlitchHash(float seed)
            {
                return frac(sin(seed) * 43758.5453);
            }

            half4 ReadUITexture(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv))
                       + _TextureSampleAdd;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float strength = saturate(_Intensity);
                half4 color;

                // A uniform material branch keeps Intensity = 0 visually neutral.
                if (strength <= 0.0)
                {
                    color = ReadUITexture(input.uv);
                }
                else
                {
                    // Floor makes abrupt changes rather than a smooth wobble.
                    float tick = floor(_Time.y * max(_Speed, 1.0));
                    float band = floor(input.uv.y * max(_BandCount, 1.0));
                    float randomBand = GlitchHash(band * 12.9898 + tick * 78.233);
                    float activeBand = step(_BandThreshold, randomBand);
                    float shift = (randomBand * 2.0 - 1.0) * _Distortion
                                  * strength * activeBand;
                    float rgbOffset = _RGBSplit * strength * activeBand;
                    float2 centerUV = input.uv + float2(shift, 0.0);

                    half4 center = ReadUITexture(centerUV);
                    half red = ReadUITexture(centerUV + float2(rgbOffset, 0.0)).r;
                    half blue = ReadUITexture(centerUV - float2(rgbOffset, 0.0)).b;

                    float2 grainCell = floor(input.uv * max(_NoiseScale, 1.0));
                    float grain = GlitchHash(dot(grainCell, float2(12.9898, 78.233))
                                            + tick * 37.719) - 0.5;
                    float scanline = step(0.5, frac(input.uv.y * _ScanlineCount));
                    float scanBrightness = 1.0 - scanline * _ScanlineAmount * strength;
                    float flicker = GlitchHash(tick * 91.345);
                    float flickerBrightness = 1.0 - flicker * _FlickerAmount * strength;

                    color.rgb = saturate(half3(red, center.g, blue)
                                         * scanBrightness * flickerBrightness
                                         + grain * _NoiseAmount * strength);

                    // Noise must not turn the transparent PNG border opaque.
                    float inside = step(0.0, centerUV.x) * step(centerUV.x, 1.0);
                    color.a = center.a * inside;
                }

                // Keep Image.Color and CanvasGroup fading functional.
                input.color.a = round(input.color.a * 255.0) / 255.0;
                color *= input.color;

                #ifdef UNITY_UI_CLIP_RECT
                    half2 clipFactor = saturate(
                        (_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                    color.a *= clipFactor.x * clipFactor.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                    clip(color.a - 0.001h);
                #endif

                color.rgb *= color.a;
                return color;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
