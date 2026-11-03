Shader "UTOPIA/InteractionOutline"
{
 Properties { _OutlineWidth("Width (pixels)", Range(0,12))=1 _OutlineColor("Color",Color)=(1,1,1,1) }
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+1" "RenderType"="Opaque" }
 Pass {
 Name "Outline"
 Tags { "LightMode"="SRPDefaultUnlit" }
 Cull Front
 ZWrite Off
 ZTest LEqual
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float _OutlineWidth;
 half4 _OutlineColor;
 CBUFFER_END
 struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
 struct Varyings { float4 positionCS:SV_POSITION; };
 Varyings Vert(Attributes input) {
 Varyings output;
 output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
 float2 direction=mul((float3x3)UNITY_MATRIX_VP,TransformObjectToWorldNormal(input.normalOS)).xy;
 float lengthSquared=dot(direction,direction);
 if(lengthSquared>0.000001) output.positionCS.xy+=direction*rsqrt(lengthSquared)*(2.0*_OutlineWidth/_ScreenParams.xy)*output.positionCS.w;
 return output;
 }
 half4 Frag(Varyings input):SV_Target { return _OutlineColor; }
 ENDHLSL
 }
 }
}