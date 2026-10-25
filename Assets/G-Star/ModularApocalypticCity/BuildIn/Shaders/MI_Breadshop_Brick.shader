Shader "Unreal/MI_Breadshop_Brick"
{
	Properties
	{
		_MainTex("MainTex (RGB)", 2D) = "white" {}
		Material_Texture2D_0( "Main_Normal", 2D ) = "white" {}
		Material_Texture2D_1( "1_Normal", 2D ) = "white" {}
		Material_Texture2D_2( "Main_Height", 2D ) = "white" {}
		Material_Texture2D_3( "1_Height", 2D ) = "white" {}
		Material_Texture2D_4( "2_Normal", 2D ) = "white" {}
		Material_Texture2D_5( "2_Height", 2D ) = "white" {}
		Material_Texture2D_6( "Main_Albedo", 2D ) = "white" {}
		Material_Texture2D_7( "Main_ORM(E)", 2D ) = "white" {}
		Material_Texture2D_8( "Macro Albedo", 2D ) = "white" {}
		Material_Texture2D_9( "1_Albedo", 2D ) = "white" {}
		Material_Texture2D_10( "1_ORM(E)", 2D ) = "white" {}
		Material_Texture2D_11( "2_Albedo", 2D ) = "white" {}
		Material_Texture2D_12( "2.ORM(E)", 2D ) = "white" {}

		View_BufferSizeAndInvSize( "View_BufferSizeAndInvSize", Vector ) = ( 1920,1080,0.00052, 0.00092 )//1920,1080,1/1920, 1/1080
		LocalObjectBoundsMin( "LocalObjectBoundsMin", Vector ) = ( 0, 0, 0, 0 )
		LocalObjectBoundsMax( "LocalObjectBoundsMax", Vector ) = ( 100, 100, 100, 0 )
	}
	SubShader 
	{
		 Tags { "RenderType" = "Opaque" }
		//BLEND_ON Tags { "RenderType" = "Transparent"  "Queue" = "Transparent" }
		
		//Blend SrcAlpha OneMinusSrcAlpha
		Cull Off

		CGPROGRAM

		#include "UnityPBSLighting.cginc"
		 #pragma surface surf Standard vertex:vert addshadow
		//BLEND_ON #pragma surface surf Standard vertex:vert alpha:fade addshadow
		
		#pragma target 5.0

		#define NUM_TEX_COORD_INTERPOLATORS 1
		#define NUM_MATERIAL_TEXCOORDS_VERTEX 1
		#define NUM_CUSTOM_VERTEX_INTERPOLATORS 0

		struct Input
		{
			//float3 Normal;
			float2 uv_MainTex : TEXCOORD0;
			float2 uv2_Material_Texture2D_0 : TEXCOORD1;
			//float2 uv2_MainTex : TEXCOORD1;
			float4 color : COLOR;
			float4 tangent;
			//float4 normal;
			float3 viewDir;
			float4 screenPos;
			float3 worldPos;
			//float3 worldNormal;
			float3 normal2;
			INTERNAL_DATA
		};
		void vert( inout appdata_full i, out Input o )
		{
			float3 p_normal = mul( float4( i.normal, 0.0f ), unity_WorldToObject );
			//half4 p_tangent = mul( unity_ObjectToWorld,i.tangent );

			//half3 normal_input = normalize( p_normal.xyz );
			//half3 tangent_input = normalize( p_tangent.xyz );
			//half3 binormal_input = cross( p_normal.xyz,tangent_input.xyz ) * i.tangent.w;
			UNITY_INITIALIZE_OUTPUT( Input, o );

			//o.worldNormal = p_normal;
			o.normal2 = p_normal;
			o.tangent = i.tangent;
			//o.binormal_input = binormal_input;
		}
		uniform sampler2D _MainTex;
		/*
		struct SurfaceOutputStandard
		{
		fixed3 Albedo;		// base (diffuse or specular) color
		fixed3 Normal;		// tangent space normal, if written
		half3 Emission;
		half Metallic;		// 0=non-metal, 1=metal
		// Smoothness is the user facing name, it should be perceptual smoothness but user should not have to deal with it.
		// Everywhere in the code you meet smoothness it is perceptual smoothness
		half Smoothness;	// 0=rough, 1=smooth
		half Occlusion;		// occlusion (default 1)
		fixed Alpha;		// alpha for transparencies
		};
		*/


		#define Texture2D sampler2D
		#define TextureCube samplerCUBE
		#define SamplerState int

		//#define UE5
		#define MATERIAL_TANGENTSPACENORMAL 1
		//struct Material
		//{
			//samplers start
			uniform sampler2D    Material_Texture2D_0;
			uniform SamplerState Material_Texture2D_0Sampler;
			uniform sampler2D    Material_Texture2D_1;
			uniform SamplerState Material_Texture2D_1Sampler;
			uniform sampler2D    Material_Texture2D_2;
			uniform SamplerState Material_Texture2D_2Sampler;
			uniform sampler2D    Material_Texture2D_3;
			uniform SamplerState Material_Texture2D_3Sampler;
			uniform sampler2D    Material_Texture2D_4;
			uniform SamplerState Material_Texture2D_4Sampler;
			uniform sampler2D    Material_Texture2D_5;
			uniform SamplerState Material_Texture2D_5Sampler;
			uniform sampler2D    Material_Texture2D_6;
			uniform SamplerState Material_Texture2D_6Sampler;
			uniform sampler2D    Material_Texture2D_7;
			uniform SamplerState Material_Texture2D_7Sampler;
			uniform sampler2D    Material_Texture2D_8;
			uniform SamplerState Material_Texture2D_8Sampler;
			uniform sampler2D    Material_Texture2D_9;
			uniform SamplerState Material_Texture2D_9Sampler;
			uniform sampler2D    Material_Texture2D_10;
			uniform SamplerState Material_Texture2D_10Sampler;
			uniform sampler2D    Material_Texture2D_11;
			uniform SamplerState Material_Texture2D_11Sampler;
			uniform sampler2D    Material_Texture2D_12;
			uniform SamplerState Material_Texture2D_12Sampler;
			
		//};

		#ifdef UE5
			#define UE_LWC_RENDER_TILE_SIZE			2097152.0
			#define UE_LWC_RENDER_TILE_SIZE_SQRT	1448.15466
			#define UE_LWC_RENDER_TILE_SIZE_RSQRT	0.000690533954
			#define UE_LWC_RENDER_TILE_SIZE_RCP		4.76837158e-07
			#define UE_LWC_RENDER_TILE_SIZE_FMOD_PI		0.673652053
			#define UE_LWC_RENDER_TILE_SIZE_FMOD_2PI	0.673652053
			#define INVARIANT(X) X
			#define PI 					(3.1415926535897932)

			//#include "LargeWorldCoordinates.hlsl"
		#endif
		struct MaterialStruct
		{
			float4 VectorExpressions[20];
			float4 ScalarExpressions[6];
			float VTPackedPageTableUniform[2];
			float VTPackedUniform[1];
		};
		struct ViewStruct
		{
			float GameTime;
			float RealTime;
			float DeltaTime;
			float PrevFrameGameTime;
			float PrevFrameRealTime;
			float MaterialTextureMipBias;
			SamplerState MaterialTextureBilinearWrapedSampler;
			SamplerState MaterialTextureBilinearClampedSampler;
			float4 PrimitiveSceneData[ 40 ];
			float4 TemporalAAParams;
			float2 ViewRectMin;
			float4 ViewSizeAndInvSize;
			float MaterialTextureDerivativeMultiply;
			uint StateFrameIndexMod8;
			float FrameNumber;
			float2 FieldOfViewWideAngles;
			float4 RuntimeVirtualTextureMipLevel;
			float PreExposure;
			float4 BufferBilinearUVMinMax;
		};
		struct ResolvedViewStruct
		{
		#ifdef UE5
			FLWCVector3 WorldCameraOrigin;
			FLWCVector3 PrevWorldCameraOrigin;
			FLWCVector3 PreViewTranslation;
			FLWCVector3 WorldViewOrigin;
		#else
			float3 WorldCameraOrigin;
			float3 PrevWorldCameraOrigin;
			float3 PreViewTranslation;
			float3 WorldViewOrigin;
		#endif
			float4 ScreenPositionScaleBias;
			float4x4 TranslatedWorldToView;
			float4x4 TranslatedWorldToCameraView;
			float4x4 TranslatedWorldToClip;
			float4x4 ViewToTranslatedWorld;
			float4x4 PrevViewToTranslatedWorld;
			float4x4 CameraViewToTranslatedWorld;
			float4 BufferBilinearUVMinMax;
			float4 XRPassthroughCameraUVs[ 2 ];
		};
		struct PrimitiveStruct
		{
			float4x4 WorldToLocal;
			float4x4 LocalToWorld;
		};

		ViewStruct View;
		ResolvedViewStruct ResolvedView;
		PrimitiveStruct Primitive;
		uniform float4 View_BufferSizeAndInvSize;
		uniform float4 LocalObjectBoundsMin;
		uniform float4 LocalObjectBoundsMax;
		uniform SamplerState Material_Wrap_WorldGroupSettings;
		uniform SamplerState Material_Clamp_WorldGroupSettings;
		
		#define PI UNITY_PI
		#include "UnrealCommon.cginc"

		MaterialStruct Material;
void InitializeExpressions()
{
	Material.VectorExpressions[0] = float4(0.000000,0.000000,0.000000,0.000000);//SelectionColor
	Material.VectorExpressions[1] = float4(1.000000,1.000000,1.000000,0.000000);//Main_Tile
	Material.VectorExpressions[2] = float4(1.000000,1.000000,1.000000,0.000000);//(Unknown)
	Material.VectorExpressions[3] = float4(1.000000,1.000000,1.000000,1.000000);//(Unknown)
	Material.VectorExpressions[4] = float4(1.000000,1.000000,1.000000,1.000000);//(Unknown)
	Material.VectorExpressions[5] = float4(1.000000,1.000000,1.000000,0.000000);//1_Tile
	Material.VectorExpressions[6] = float4(1.000000,1.000000,1.000000,0.000000);//(Unknown)
	Material.VectorExpressions[7] = float4(1.000000,1.000000,1.000000,1.000000);//(Unknown)
	Material.VectorExpressions[8] = float4(1.000000,1.000000,1.000000,1.000000);//(Unknown)
	Material.VectorExpressions[9] = float4(1.000000,1.000000,1.000000,0.000000);//2_Tile
	Material.VectorExpressions[10] = float4(1.000000,1.000000,1.000000,0.000000);//(Unknown)
	Material.VectorExpressions[11] = float4(1.000000,1.000000,1.000000,1.000000);//(Unknown)
	Material.VectorExpressions[12] = float4(1.000000,1.000000,1.000000,1.000000);//(Unknown)
	Material.VectorExpressions[13] = float4(0.000000,0.000000,0.000000,0.000000);//(Unknown)
	Material.VectorExpressions[14] = float4(1.000000,1.000000,1.000000,0.000000);//Main_AlbedoTint
	Material.VectorExpressions[15] = float4(1.000000,1.000000,1.000000,0.000000);//(Unknown)
	Material.VectorExpressions[16] = float4(1.000000,1.000000,1.000000,0.000000);//1_AlbedoTint
	Material.VectorExpressions[17] = float4(1.000000,1.000000,1.000000,0.000000);//(Unknown)
	Material.VectorExpressions[18] = float4(4.000000,4.000000,4.000000,1.000000);//2_AlbedoTint
	Material.VectorExpressions[19] = float4(4.000000,4.000000,4.000000,0.000000);//(Unknown)
	Material.ScalarExpressions[0] = float4(1.000000,1.000000,1.000000,1.000000);//(Unknown) (Unknown) (Unknown) Main_NormalIntensity
	Material.ScalarExpressions[1] = float4(1.000000,1.000000,1.000000,1.500000);//(Unknown) (Unknown) (Unknown) 1_NormalIntensity
	Material.ScalarExpressions[2] = float4(1.000000,0.000000,2.500000,1.000000);//Main_HeightIntensity 1_HeightIntensity 1_BlendPower (Unknown)
	Material.ScalarExpressions[3] = float4(1.000000,1.000000,2.000000,0.800000);//(Unknown) (Unknown) 2_NormalIntensity 2_HeightIntensity
	Material.ScalarExpressions[4] = float4(6.000000,0.000000,2048.000000,2048.000000);//2_BlendPower (Unknown) TextureSiza (Unknown)
	Material.ScalarExpressions[5] = float4(-2048.000000,0.500000,0.000000,0.000000);//(Unknown) Albedo power (Unknown) (Unknown)
}struct MaterialCollection0Type
{
	float4 Vectors[4];
};
//MPC_Weather
MaterialCollection0Type MaterialCollection0;
void Initialize_MaterialCollection0()
{
	MaterialCollection0.Vectors[0] = float4(1.000000,1.000000,0.000000,0.500000);//SandAmonunt_Ground,SandAmount,SandTopMask,WindRotation_Tree
	MaterialCollection0.Vectors[1] = float4(3.000000,2.000000,1.000000,0.000000);//WindSpeed_Tree,WindIntensity_Grass,WindSpeed_Grass,Sand?
	MaterialCollection0.Vectors[2] = float4(1.000000,-0.180000,-0.000000,0.000000);//RainWetness?,RainTrail_Speed,,
	MaterialCollection0.Vectors[3] = float4(0.000000,0.000000,0.000000,0.000000);//Vector
}
void CalcPixelMaterialInputs(in out FMaterialPixelParameters Parameters, in out FPixelMaterialInputs PixelMaterialInputs)
{
	//WorldAligned texturing & others use normals & stuff that think Z is up
	Parameters.TangentToWorld[0] = Parameters.TangentToWorld[0].xzy;
	Parameters.TangentToWorld[1] = Parameters.TangentToWorld[1].xzy;
	Parameters.TangentToWorld[2] = Parameters.TangentToWorld[2].xzy;

	float3 WorldNormalCopy = Parameters.WorldNormal;

	// Initial calculations (required for Normal)
	MaterialFloat2 Local0 = (Material.VectorExpressions[4].rg * Parameters.TexCoords[0].xy);
	MaterialFloat Local1 = MaterialStoreTexCoordScale(Parameters, Local0, 3);
	MaterialFloat4 Local2 = UnpackNormalMap(Texture2DSample(Material_Texture2D_0, GetMaterialSharedSampler(Material_Texture2D_0Sampler,View.MaterialTextureBilinearWrapedSampler),Local0));
	MaterialFloat Local3 = MaterialStoreTexSample(Parameters, Local2, 3);
	MaterialFloat3 Local4 = lerp(MaterialFloat3(MaterialFloat2(0.00000000,0.00000000),1.00000000),Local2.rgb,MaterialFloat(Material.ScalarExpressions[0].w));
	MaterialFloat2 Local5 = (Material.VectorExpressions[8].rg * Parameters.TexCoords[0].xy);
	MaterialFloat Local6 = MaterialStoreTexCoordScale(Parameters, Local5, 3);
	MaterialFloat4 Local7 = UnpackNormalMap(Texture2DSample(Material_Texture2D_1, GetMaterialSharedSampler(Material_Texture2D_1Sampler,View.MaterialTextureBilinearWrapedSampler),Local5));
	MaterialFloat Local8 = MaterialStoreTexSample(Parameters, Local7, 3);
	MaterialFloat3 Local9 = lerp(MaterialFloat3(MaterialFloat2(0.00000000,0.00000000),1.00000000),Local7.rgb,MaterialFloat(Material.ScalarExpressions[1].w));
	MaterialFloat Local10 = MaterialStoreTexCoordScale(Parameters, Local0, 12);
	MaterialFloat4 Local11 = ProcessMaterialGreyscaleTextureLookup((Texture2DSample(Material_Texture2D_2, GetMaterialSharedSampler(Material_Texture2D_2Sampler,View.MaterialTextureBilinearWrapedSampler),Local0)).r).rrrr;
	MaterialFloat Local12 = MaterialStoreTexSample(Parameters, Local11, 12);
	MaterialFloat Local13 = (Local11.r * Material.ScalarExpressions[2].x);
	MaterialFloat Local14 = (1.00000000 - Parameters.VertexColor.r);
	MaterialFloat Local15 = (Local13 + Local14);
	MaterialFloat Local16 = (Local15 + 1.00000000);
	MaterialFloat Local17 = (2.00000000 * Parameters.VertexColor.r);
	MaterialFloat Local18 = MaterialStoreTexCoordScale(Parameters, Local5, 12);
	MaterialFloat4 Local19 = ProcessMaterialGreyscaleTextureLookup((Texture2DSample(Material_Texture2D_3, GetMaterialSharedSampler(Material_Texture2D_3Sampler,View.MaterialTextureBilinearWrapedSampler),Local5)).r).rrrr;
	MaterialFloat Local20 = MaterialStoreTexSample(Parameters, Local19, 12);
	MaterialFloat Local21 = (Local19.r * Material.ScalarExpressions[2].y);
	MaterialFloat Local22 = (Local17 + Local21);
	MaterialFloat Local23 = (Local16 - Local22);
	MaterialFloat Local24 = PositiveClampedPow(Local23,Material.ScalarExpressions[2].z);
	MaterialFloat Local25 = (1.00000000 - Local24);
	MaterialFloat Local26 = min(max(Local25,0.00000000),1.00000000);
	MaterialFloat3 Local27 = lerp(Local4.rgb.rgb,Local9.rgb.rgb,MaterialFloat(Local26.r));
	MaterialFloat2 Local28 = (Material.VectorExpressions[12].rg * Parameters.TexCoords[0].xy);
	MaterialFloat Local29 = MaterialStoreTexCoordScale(Parameters, Local28, 3);
	MaterialFloat4 Local30 = UnpackNormalMap(Texture2DSample(Material_Texture2D_4, GetMaterialSharedSampler(Material_Texture2D_4Sampler,View.MaterialTextureBilinearWrapedSampler),Local28));
	MaterialFloat Local31 = MaterialStoreTexSample(Parameters, Local30, 3);
	MaterialFloat3 Local32 = lerp(MaterialFloat3(MaterialFloat2(0.00000000,0.00000000),1.00000000),Local30.rgb,MaterialFloat(Material.ScalarExpressions[3].z));
	MaterialFloat3 Local33 = lerp(Local13.rrr.rgb,Local21.rrr.rgb,MaterialFloat(Local26.r));
	MaterialFloat Local34 = (1.00000000 - Parameters.VertexColor.g);
	MaterialFloat Local35 = (Local33.rgb.r + Local34);
	MaterialFloat Local36 = (Local35 + 1.00000000);
	MaterialFloat Local37 = (2.00000000 * Parameters.VertexColor.g);
	MaterialFloat Local38 = MaterialStoreTexCoordScale(Parameters, Local28, 12);
	MaterialFloat4 Local39 = ProcessMaterialGreyscaleTextureLookup((Texture2DSample(Material_Texture2D_5, GetMaterialSharedSampler(Material_Texture2D_5Sampler,View.MaterialTextureBilinearWrapedSampler),Local28)).r).rrrr;
	MaterialFloat Local40 = MaterialStoreTexSample(Parameters, Local39, 12);
	MaterialFloat Local41 = (Local39.r * Material.ScalarExpressions[3].w);
	MaterialFloat Local42 = (Local37 + Local41);
	MaterialFloat Local43 = (Local36 - Local42);
	MaterialFloat Local44 = PositiveClampedPow(Local43,Material.ScalarExpressions[4].x);
	MaterialFloat Local45 = (1.00000000 - Local44);
	MaterialFloat Local46 = min(max(Local45,MaterialFloat(0.00000000)),MaterialFloat(1.00000000));
	MaterialFloat3 Local47 = lerp(Local27.rgb,Local32.rgb.rgb,MaterialFloat(Local46.r));

	// The Normal is a special case as it might have its own expressions and also be used to calculate other inputs, so perform the assignment here
	PixelMaterialInputs.Normal = Local47.rgb;


	// Note that here MaterialNormal can be in world space or tangent space
	float3 MaterialNormal = GetMaterialNormal(Parameters, PixelMaterialInputs);

#if MATERIAL_TANGENTSPACENORMAL
#if SIMPLE_FORWARD_SHADING
	Parameters.WorldNormal = float3(0, 0, 1);
#endif

#if FEATURE_LEVEL >= FEATURE_LEVEL_SM4
	// Mobile will rely on only the final normalize for performance
	MaterialNormal = normalize(MaterialNormal);
#endif

	// normalizing after the tangent space to world space conversion improves quality with sheared bases (UV layout to WS causes shrearing)
	// use full precision normalize to avoid overflows
	Parameters.WorldNormal = TransformTangentNormalToWorld(Parameters.TangentToWorld, MaterialNormal);

#else //MATERIAL_TANGENTSPACENORMAL

	Parameters.WorldNormal = normalize(MaterialNormal);

#endif //MATERIAL_TANGENTSPACENORMAL

#if MATERIAL_TANGENTSPACENORMAL
	// flip the normal for backfaces being rendered with a two-sided material
	Parameters.WorldNormal *= Parameters.TwoSidedSign;
#endif

	Parameters.ReflectionVector = ReflectionAboutCustomWorldNormal(Parameters, Parameters.WorldNormal, false);

#if !PARTICLE_SPRITE_FACTORY
	Parameters.Particle.MotionBlurFade = 1.0f;
#endif // !PARTICLE_SPRITE_FACTORY

	// Now the rest of the inputs
	MaterialFloat3 Local48 = lerp(0.00000000.rrr.rgb.rgb.rgb,Material.VectorExpressions[13].rgb,MaterialFloat(Material.ScalarExpressions[4].y));
	MaterialFloat Local49 = MaterialStoreTexCoordScale(Parameters, Local0, 10);
	MaterialFloat4 Local50 = ProcessMaterialColorTextureLookup(Texture2DSample(Material_Texture2D_6, GetMaterialSharedSampler(Material_Texture2D_6Sampler,View.MaterialTextureBilinearWrapedSampler),Local0));
	MaterialFloat Local51 = MaterialStoreTexSample(Parameters, Local50, 10);
	MaterialFloat Local52 = MaterialStoreTexCoordScale(Parameters, Local0, 7);
	MaterialFloat4 Local53 = Texture2DSample(Material_Texture2D_7, GetMaterialSharedSampler(Material_Texture2D_7Sampler,View.MaterialTextureBilinearWrapedSampler),Local0);
	MaterialFloat Local54 = MaterialStoreTexSample(Parameters, Local53, 7);
	MaterialFloat3 Local55 = (Local50.rgb * Local53.r);
	MaterialFloat3 Local56 = (Local55 * Material.VectorExpressions[15].rgb);
	MaterialFloat3 Local57 = (1.00000000 - Local56.rgb);
	MaterialFloat3 Local58 = (Local57 * 2.00000000);
	MaterialFloat3 Local59 = (GetWorldPosition_NoMaterialOffsets(Parameters) / Material.ScalarExpressions[5].x);
	MaterialFloat Local60 = MaterialStoreTexCoordScale(Parameters, Local59.rb, 25);
	MaterialFloat4 Local61 = ProcessMaterialColorTextureLookup(Texture2DSample(Material_Texture2D_8, GetMaterialSharedSampler(Material_Texture2D_8Sampler,View.MaterialTextureBilinearWrapedSampler),Local59.rb));
	MaterialFloat Local62 = MaterialStoreTexSample(Parameters, Local61, 25);
	MaterialFloat Local63 = MaterialStoreTexCoordScale(Parameters, Local59.gb, 25);
	MaterialFloat4 Local64 = ProcessMaterialColorTextureLookup(Texture2DSample(Material_Texture2D_8, GetMaterialSharedSampler(Material_Texture2D_8Sampler,View.MaterialTextureBilinearWrapedSampler),Local59.gb));
	MaterialFloat Local65 = MaterialStoreTexSample(Parameters, Local64, 25);
	MaterialFloat Local66 = abs(Parameters.TangentToWorld[2].r);
	MaterialFloat Local67 = lerp((0.00000000 - 1.00000000),(1.00000000 + 1.00000000),Local66);
	MaterialFloat Local68 = min(max(Local67,0.00000000),1.00000000);
	MaterialFloat3 Local69 = lerp(Local61.rgb,Local64.rgb,MaterialFloat(Local68.r.r));
	MaterialFloat Local70 = MaterialStoreTexCoordScale(Parameters, Local59.rg, 25);
	MaterialFloat4 Local71 = ProcessMaterialColorTextureLookup(Texture2DSample(Material_Texture2D_8, GetMaterialSharedSampler(Material_Texture2D_8Sampler,View.MaterialTextureBilinearWrapedSampler),Local59.rg));
	MaterialFloat Local72 = MaterialStoreTexSample(Parameters, Local71, 25);
	MaterialFloat Local73 = abs(Parameters.TangentToWorld[2].b);
	MaterialFloat Local74 = lerp((0.00000000 - 1.00000000),(1.00000000 + 1.00000000),Local73);
	MaterialFloat Local75 = min(max(Local74,0.00000000),1.00000000);
	MaterialFloat3 Local76 = lerp(Local69,Local71.rgb,MaterialFloat(Local75.r.r));
	MaterialFloat3 Local77 = PositiveClampedPow(Local76,Material.ScalarExpressions[5].y);
	MaterialFloat3 Local78 = (1.00000000 - Local77);
	MaterialFloat3 Local79 = (Local58 * Local78);
	MaterialFloat3 Local80 = (1.00000000 - Local79);
	MaterialFloat3 Local81 = (Local56.rgb * 2.00000000);
	MaterialFloat3 Local82 = (Local81 * Local77);
	MaterialFloat Local83 = ((Local56.rgb.r >= 0.50000000) ? Local80.r : Local82.r);
	MaterialFloat Local84 = ((Local56.rgb.g >= 0.50000000) ? Local80.g : Local82.g);
	MaterialFloat Local85 = ((Local56.rgb.b >= 0.50000000) ? Local80.b : Local82.b);
	MaterialFloat Local86 = MaterialStoreTexCoordScale(Parameters, Local5, 10);
	MaterialFloat4 Local87 = ProcessMaterialColorTextureLookup(Texture2DSample(Material_Texture2D_9, GetMaterialSharedSampler(Material_Texture2D_9Sampler,View.MaterialTextureBilinearWrapedSampler),Local5));
	MaterialFloat Local88 = MaterialStoreTexSample(Parameters, Local87, 10);
	MaterialFloat Local89 = MaterialStoreTexCoordScale(Parameters, Local5, 7);
	MaterialFloat4 Local90 = Texture2DSample(Material_Texture2D_10, GetMaterialSharedSampler(Material_Texture2D_10Sampler,View.MaterialTextureBilinearWrapedSampler),Local5);
	MaterialFloat Local91 = MaterialStoreTexSample(Parameters, Local90, 7);
	MaterialFloat3 Local92 = (Local87.rgb * Local90.r);
	MaterialFloat3 Local93 = (Local92 * Material.VectorExpressions[17].rgb);
	MaterialFloat3 Local94 = (1.00000000 - Local93.rgb);
	MaterialFloat3 Local95 = (Local94 * 2.00000000);
	MaterialFloat3 Local96 = (Local95 * Local78);
	MaterialFloat3 Local97 = (1.00000000 - Local96);
	MaterialFloat3 Local98 = (Local93.rgb * 2.00000000);
	MaterialFloat3 Local99 = (Local98 * Local77);
	MaterialFloat Local100 = ((Local93.rgb.r >= 0.50000000) ? Local97.r : Local99.r);
	MaterialFloat Local101 = ((Local93.rgb.g >= 0.50000000) ? Local97.g : Local99.g);
	MaterialFloat Local102 = ((Local93.rgb.b >= 0.50000000) ? Local97.b : Local99.b);
	MaterialFloat3 Local103 = lerp(MaterialFloat3(MaterialFloat2(Local83,Local84),Local85).rgb,MaterialFloat3(MaterialFloat2(Local100,Local101),Local102).rgb,MaterialFloat(Local26.r));
	MaterialFloat Local104 = MaterialStoreTexCoordScale(Parameters, Local28, 10);
	MaterialFloat4 Local105 = ProcessMaterialColorTextureLookup(Texture2DSample(Material_Texture2D_11, GetMaterialSharedSampler(Material_Texture2D_11Sampler,View.MaterialTextureBilinearWrapedSampler),Local28));
	MaterialFloat Local106 = MaterialStoreTexSample(Parameters, Local105, 10);
	MaterialFloat Local107 = MaterialStoreTexCoordScale(Parameters, Local28, 7);
	MaterialFloat4 Local108 = Texture2DSample(Material_Texture2D_12, GetMaterialSharedSampler(Material_Texture2D_12Sampler,View.MaterialTextureBilinearWrapedSampler),Local28);
	MaterialFloat Local109 = MaterialStoreTexSample(Parameters, Local108, 7);
	MaterialFloat3 Local110 = (Local105.rgb * Local108.r);
	MaterialFloat3 Local111 = (Local110 * Material.VectorExpressions[19].rgb);
	MaterialFloat3 Local112 = (1.00000000 - Local111.rgb);
	MaterialFloat3 Local113 = (Local112 * 2.00000000);
	MaterialFloat3 Local114 = (Local113 * Local78);
	MaterialFloat3 Local115 = (1.00000000 - Local114);
	MaterialFloat3 Local116 = (Local111.rgb * 2.00000000);
	MaterialFloat3 Local117 = (Local116 * Local77);
	MaterialFloat Local118 = ((Local111.rgb.r >= 0.50000000) ? Local115.r : Local117.r);
	MaterialFloat Local119 = ((Local111.rgb.g >= 0.50000000) ? Local115.g : Local117.g);
	MaterialFloat Local120 = ((Local111.rgb.b >= 0.50000000) ? Local115.b : Local117.b);
	MaterialFloat3 Local121 = lerp(Local103.rgb,MaterialFloat3(MaterialFloat2(Local118,Local119),Local120).rgb,MaterialFloat(Local46.r));
	MaterialFloat3 Local122 = lerp(Local53.b.r.rrr,Local90.b.r.rrr,MaterialFloat(Local26.r));
	MaterialFloat3 Local123 = lerp(Local122.rgb,Local108.b.r.rrr,MaterialFloat(Local46.r));
	MaterialFloat3 Local124 = lerp(Local53.g.r.rrr,Local90.g.r.rrr,MaterialFloat(Local26.r));
	MaterialFloat3 Local125 = lerp(Local124.rgb,Local108.g.r.rrr,MaterialFloat(Local46.r));
	MaterialFloat3 Local126 = lerp(Local53.r.r.rrr,Local90.r.r.rrr,MaterialFloat(Local26.r));
	MaterialFloat3 Local127 = lerp(Local126.rgb,Local108.r.r.rrr,MaterialFloat(Local46.r));

	PixelMaterialInputs.EmissiveColor = Local48;
	PixelMaterialInputs.Opacity = 1.00000000;
	PixelMaterialInputs.OpacityMask = 1.00000000;
	PixelMaterialInputs.BaseColor = Local121.rgb;
	PixelMaterialInputs.Metallic = Local123.r;
	PixelMaterialInputs.Specular = 0.50000000.r.rrr.rgb.r;
	PixelMaterialInputs.Roughness = Local125.r;
	PixelMaterialInputs.Anisotropy = 0.00000000;
	PixelMaterialInputs.Tangent = MaterialFloat3(1.00000000,0.00000000,0.00000000);
	PixelMaterialInputs.Subsurface = 0;
	PixelMaterialInputs.AmbientOcclusion = Local127.r;
	PixelMaterialInputs.Refraction = 0;
	PixelMaterialInputs.PixelDepthOffset = 0.00000000.r.rrr.rgb.r;
	PixelMaterialInputs.ShadingModel = 1;


#if MATERIAL_USES_ANISOTROPY
	Parameters.WorldTangent = CalculateAnisotropyTangent(Parameters, PixelMaterialInputs);
#else
	Parameters.WorldTangent = 0;
#endif
}
		void surf( Input In, inout SurfaceOutputStandard o )
		{
			InitializeExpressions();
	Initialize_MaterialCollection0();


			float3 Z3 = float3( 0, 0, 0 );
			float4 Z4 = float4( 0, 0, 0, 0 );

			float3 UnrealWorldPos = float3( In.worldPos.x, In.worldPos.y, In.worldPos.z );
			
			float3 UnrealNormal = In.normal2;

			FMaterialPixelParameters Parameters = (FMaterialPixelParameters)0;
			#if NUM_TEX_COORD_INTERPOLATORS > 0			
				Parameters.TexCoords[ 0 ] = float2( In.uv_MainTex.x, 1.0 - In.uv_MainTex.y );
			#endif
			#if NUM_TEX_COORD_INTERPOLATORS > 1
				Parameters.TexCoords[ 1 ] = float2( In.uv2_Material_Texture2D_0.x, 1.0 - In.uv2_Material_Texture2D_0.y );
			#endif
			#if NUM_TEX_COORD_INTERPOLATORS > 2
			for( int i = 2; i < NUM_TEX_COORD_INTERPOLATORS; i++ )
			{
				Parameters.TexCoords[ i ] = float2( In.uv_MainTex.x, 1.0 - In.uv_MainTex.y );
			}
			#endif
			Parameters.VertexColor = In.color;
			Parameters.WorldNormal = UnrealNormal;
			Parameters.ReflectionVector = half3( 0, 0, 1 );
			Parameters.CameraVector = normalize( _WorldSpaceCameraPos.xyz - UnrealWorldPos.xyz );
			//Parameters.CameraVector = mul( ( float3x3 )unity_CameraToWorld, float3( 0, 0, 1 ) ) * -1;
			Parameters.LightVector = half3( 0, 0, 0 );
			float4 screenpos = In.screenPos;
			screenpos /= screenpos.w;
			//screenpos.y = 1 - screenpos.y;
			Parameters.SvPosition = float4( screenpos.x, screenpos.y, 0, 0 );
			Parameters.ScreenPosition = Parameters.SvPosition;

			Parameters.UnMirrored = 1;

			Parameters.TwoSidedSign = 1;
			

			float3 InWorldNormal = UnrealNormal;
			float4 InTangent = In.tangent;
			float4 tangentWorld = float4( UnityObjectToWorldDir( InTangent.xyz ), InTangent.w );
			tangentWorld.xyz = normalize( tangentWorld.xyz );
			float3x3 OriginalTangentToWorld = CreateTangentToWorldPerVertex( InWorldNormal, tangentWorld.xyz, tangentWorld.w );
			Parameters.TangentToWorld = OriginalTangentToWorld;

			//WorldAlignedTexturing in UE relies on the fact that coords there are 100x larger, prepare values for that
			//but watch out for any computation that might get skewed as a side effect
			UnrealWorldPos = ToUnrealPos( UnrealWorldPos );
			
			Parameters.AbsoluteWorldPosition = UnrealWorldPos;
			Parameters.WorldPosition_CamRelative = UnrealWorldPos;
			Parameters.WorldPosition_NoOffsets = UnrealWorldPos;

			Parameters.WorldPosition_NoOffsets_CamRelative = Parameters.WorldPosition_CamRelative;
			Parameters.LightingPositionOffset = float3( 0, 0, 0 );

			Parameters.AOMaterialMask = 0;

			Parameters.Particle.RelativeTime = 0;
			Parameters.Particle.MotionBlurFade;
			Parameters.Particle.Random = 0;
			Parameters.Particle.Velocity = half4( 1, 1, 1, 1 );
			Parameters.Particle.Color = half4( 1, 1, 1, 1 );
			Parameters.Particle.TranslatedWorldPositionAndSize = float4( UnrealWorldPos, 0 );
			Parameters.Particle.MacroUV = half4(0,0,1,1);
			Parameters.Particle.DynamicParameter = half4(0,0,0,0);
			Parameters.Particle.LocalToWorld = float4x4( Z4, Z4, Z4, Z4 );
			Parameters.Particle.Size = float2(1,1);
			Parameters.Particle.SubUVCoords[ 0 ] = Parameters.Particle.SubUVCoords[ 1 ] = float2( 0, 0 );
			Parameters.Particle.SubUVLerp = 0.0;
			Parameters.TexCoordScalesParams = float2( 0, 0 );
			Parameters.PrimitiveId = 0;
			Parameters.VirtualTextureFeedback = 0;

			FPixelMaterialInputs PixelMaterialInputs = ( FPixelMaterialInputs)0;
			PixelMaterialInputs.Normal = float3( 0, 0, 1 );
			PixelMaterialInputs.ShadingModel = 0;
			PixelMaterialInputs.FrontMaterial = 0;

			View.GameTime = View.RealTime = _Time.y;// _Time is (t/20, t, t*2, t*3)
			View.PrevFrameGameTime = View.GameTime - unity_DeltaTime.x;//(dt, 1/dt, smoothDt, 1/smoothDt)
			View.PrevFrameRealTime = View.RealTime;
			View.DeltaTime = unity_DeltaTime.x;
			View.MaterialTextureMipBias = 0.0;
			View.TemporalAAParams = float4( 0, 0, 0, 0 );
			View.ViewRectMin = float2( 0, 0 );
			View.ViewSizeAndInvSize = View_BufferSizeAndInvSize;
			View.MaterialTextureDerivativeMultiply = 1.0f;
			View.StateFrameIndexMod8 = 0;
			View.FrameNumber = (int)_Time.y;
			View.FieldOfViewWideAngles = float2( PI * 0.42f, PI * 0.42f );//75degrees, default unity
			View.RuntimeVirtualTextureMipLevel = float4( 0, 0, 0, 0 );
			View.PreExposure = 0;
			View.BufferBilinearUVMinMax = float4(
				View_BufferSizeAndInvSize.z * ( 0 + 0.5 ),//EffectiveViewRect.Min.X
				View_BufferSizeAndInvSize.w * ( 0 + 0.5 ),//EffectiveViewRect.Min.Y
				View_BufferSizeAndInvSize.z * ( View_BufferSizeAndInvSize.x - 0.5 ),//EffectiveViewRect.Max.X
				View_BufferSizeAndInvSize.w * ( View_BufferSizeAndInvSize.y - 0.5 ) );//EffectiveViewRect.Max.Y

			for( int i2 = 0; i2 < 40; i2++ )
				View.PrimitiveSceneData[ i2 ] = float4( 0, 0, 0, 0 );

			float4x4 ViewMatrix = transpose( unity_MatrixV );
			float4x4 InverseViewMatrix = transpose( unity_MatrixInvV );
			float4x4 ViewProjectionMatrix = transpose( unity_MatrixVP );

			uint PrimitiveBaseOffset = Parameters.PrimitiveId * PRIMITIVE_SCENE_DATA_STRIDE;
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 0 ] = unity_ObjectToWorld[ 0 ];//LocalToWorld
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 1 ] = unity_ObjectToWorld[ 1 ];//LocalToWorld
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 2 ] = unity_ObjectToWorld[ 2 ];//LocalToWorld
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 3 ] = unity_ObjectToWorld[ 3 ];//LocalToWorld
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 6 ] = unity_WorldToObject[ 0 ];//WorldToLocal
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 7 ] = unity_WorldToObject[ 1 ];//WorldToLocal
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 8 ] = unity_WorldToObject[ 2 ];//WorldToLocal
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 9 ] = unity_WorldToObject[ 3 ];//WorldToLocal
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 10 ] = unity_WorldToObject[ 0 ];//PreviousLocalToWorld
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 11 ] = unity_WorldToObject[ 1 ];//PreviousLocalToWorld
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 12 ] = unity_WorldToObject[ 2 ];//PreviousLocalToWorld
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 13 ] = unity_WorldToObject[ 3 ];//PreviousLocalToWorld
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 18 ] = float4( ToUnrealPos( UNITY_MATRIX_M[ 3 ] ), 0 );//ActorWorldPosition
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 19 ] = LocalObjectBoundsMax - LocalObjectBoundsMin;//ObjectBounds
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 21 ] = mul( unity_ObjectToWorld, float3( 1, 0, 0 ) );//ObjectOrientation
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 23 ] = LocalObjectBoundsMin;//LocalObjectBoundsMin
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 24 ] = LocalObjectBoundsMax;//LocalObjectBoundsMax

			#ifdef UE5
				ResolvedView.WorldCameraOrigin = LWCPromote( ToUnrealPos( _WorldSpaceCameraPos.xyz ) );
				ResolvedView.PreViewTranslation = LWCPromote( float3( 0, 0, 0 ) );
				ResolvedView.WorldViewOrigin = LWCPromote( float3( 0, 0, 0 ) );
			#else
				ResolvedView.WorldCameraOrigin = ToUnrealPos( _WorldSpaceCameraPos.xyz );
				ResolvedView.PreViewTranslation = float3( 0, 0, 0 );
				ResolvedView.WorldViewOrigin = float3( 0, 0, 0 );
			#endif
			ResolvedView.PrevWorldCameraOrigin = ResolvedView.WorldCameraOrigin;
			ResolvedView.ScreenPositionScaleBias = float4( 1, 1, 0, 0 );
			ResolvedView.TranslatedWorldToView = ViewMatrix;
			ResolvedView.TranslatedWorldToCameraView = ViewMatrix;
			ResolvedView.TranslatedWorldToClip = ViewProjectionMatrix;
			ResolvedView.ViewToTranslatedWorld = InverseViewMatrix;
			ResolvedView.PrevViewToTranslatedWorld = ResolvedView.ViewToTranslatedWorld;
			ResolvedView.CameraViewToTranslatedWorld = InverseViewMatrix;
			ResolvedView.BufferBilinearUVMinMax = View.BufferBilinearUVMinMax;
			ResolvedView.XRPassthroughCameraUVs[ 0 ] = ResolvedView.XRPassthroughCameraUVs[ 1 ] = float4( 0, 0, 1, 1 );
			Primitive.WorldToLocal = unity_WorldToObject;
			Primitive.LocalToWorld = unity_ObjectToWorld;
			CalcPixelMaterialInputs( Parameters, PixelMaterialInputs );

			#define HAS_WORLDSPACE_NORMAL 0
			#if HAS_WORLDSPACE_NORMAL
				PixelMaterialInputs.Normal = mul( PixelMaterialInputs.Normal, (MaterialFloat3x3)( transpose( OriginalTangentToWorld ) ) );
			#endif

			o.Albedo = PixelMaterialInputs.BaseColor.rgb;
			o.Alpha = PixelMaterialInputs.Opacity;
			//if( PixelMaterialInputs.OpacityMask < 0.333 ) discard;

			o.Metallic = PixelMaterialInputs.Metallic;
			o.Smoothness = 1.0 - PixelMaterialInputs.Roughness;
			o.Normal = normalize( PixelMaterialInputs.Normal );
			o.Emission = PixelMaterialInputs.EmissiveColor.rgb;
			o.Occlusion = PixelMaterialInputs.AmbientOcclusion;

			//BLEND_ADDITIVE o.Alpha = ( o.Emission.r + o.Emission.g + o.Emission.b ) / 3;
		}
		ENDCG
	}
	Fallback "Diffuse"
}