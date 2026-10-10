// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "FI/OEP/TwoGradientsWithCaustics"
{
	Properties
	{
		_CausticsIntensity("_CausticsIntensity", Float) = 1
		_CausticsScale("_CausticsScale", Float) = 1
		_Color("Color 01", Color) = (1,1,1,0)
		_Color2("Color 02", Color) = (1,1,1,0)
		_MainTex("Main Tex", 2D) = "white" {}
		_Glossiness2("Smoothness", Float) = 0.5
		[Normal]_NormalTex("Normal Tex", 2D) = "bump" {}
		[Normal]_NormalDetailTex("Normal Detail Tex", 2D) = "bump" {}
		_Metallic2("Metallic", Float) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" "IsEmissive" = "true"  }
		Cull Back
		CGINCLUDE
		#include "UnityStandardUtils.cginc"
		#include "UnityPBSLighting.cginc"
		#include "Lighting.cginc"
		#pragma target 3.0
		#ifdef UNITY_PASS_SHADOWCASTER
			#undef INTERNAL_DATA
			#undef WorldReflectionVector
			#undef WorldNormalVector
			#define INTERNAL_DATA half3 internalSurfaceTtoW0; half3 internalSurfaceTtoW1; half3 internalSurfaceTtoW2;
			#define WorldReflectionVector(data,normal) reflect (data.worldRefl, half3(dot(data.internalSurfaceTtoW0,normal), dot(data.internalSurfaceTtoW1,normal), dot(data.internalSurfaceTtoW2,normal)))
			#define WorldNormalVector(data,normal) half3(dot(data.internalSurfaceTtoW0,normal), dot(data.internalSurfaceTtoW1,normal), dot(data.internalSurfaceTtoW2,normal))
		#endif
		struct Input
		{
			float2 uv_texcoord;
			float3 worldNormal;
			INTERNAL_DATA
			float3 worldPos;
		};

		uniform sampler2D _NormalTex;
		uniform float4 _NormalTex_ST;
		uniform sampler2D _NormalDetailTex;
		uniform float4 _NormalDetailTex_ST;
		uniform float4 _Color;
		uniform sampler2D _MainTex;
		uniform float4 _MainTex_ST;
		uniform float4 _Color2;
		uniform sampler2D _CausticsMap;
		uniform sampler2D _FlowMap;
		uniform float _GlobalScale;
		uniform float _CausticsScale_1;
		uniform float _CausticsScale_2;
		uniform float _CausticsScale_3;
		uniform float _FlowMapScale_1;
		uniform float _FlowMapScale_2;
		uniform float _FlowMapScale_3;
		uniform float _GlobalSpeed;
		uniform float _CausticsSpeed_1;
		uniform float _CausticsSpeed_2;
		uniform float _CausticsSpeed_3;
		uniform float _CausticsIntensity;
		uniform float _CausticsScale;
		uniform float _CausticsWave1_Multiply;
		uniform float _FlowMapIntensity;
		uniform float _Metallic2;
		uniform float _Glossiness2;


		float GetCaustics96_g1( sampler2D CausticsMap, sampler2D FlowMap, float GlobalScale, float CausticsScale_1, float CausticsScale_2, float CausticsScale_3, float FlowMapScale_1, float FlowMapScale_2, float FlowMapScale_3, float GlobalSpeed, float CausticsSpeed_1, float CausticsSpeed_2, float CausticsSpeed_3, float3 worldNormal, float localIntensity, float localScale, float CausticsWave1_Multiply, float2 uv, float FlowMapIntensity )
		{
			float2 uv1 = CausticsScale_1 * localScale * GlobalScale * lerp(uv, tex2D(FlowMap, uv * FlowMapScale_1 * GlobalScale * localScale).rg, FlowMapIntensity);
			float2 uv2 = CausticsScale_2 * localScale * GlobalScale * lerp(uv, tex2D(FlowMap, uv * FlowMapScale_2 * GlobalScale * localScale).rg, FlowMapIntensity);
			float2 uv3 = CausticsScale_3 * localScale * GlobalScale * lerp(uv, tex2D(FlowMap, uv * FlowMapScale_3 * GlobalScale * localScale).rg, FlowMapIntensity);
			float waves_1a = tex2D(CausticsMap, uv1 + _Time.y * CausticsSpeed_1 * GlobalSpeed + .1);
			float waves_2a = tex2D(CausticsMap, uv2 + _Time.y * CausticsSpeed_2 * GlobalSpeed + .2);
			float waves_3a = tex2D(CausticsMap, uv3 + _Time.y * CausticsSpeed_3 * GlobalSpeed + .3);
				
			float waves_a = localIntensity * CausticsWave1_Multiply * (waves_1a + waves_2a) * waves_3a;
					
			float mask = (worldNormal.y + 0.7);
			mask = saturate(mask * mask);
				
			return waves_a * mask;
		}


		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_NormalTex = i.uv_texcoord * _NormalTex_ST.xy + _NormalTex_ST.zw;
			float2 uv_NormalDetailTex = i.uv_texcoord * _NormalDetailTex_ST.xy + _NormalDetailTex_ST.zw;
			o.Normal = BlendNormals( UnpackNormal( tex2D( _NormalTex, uv_NormalTex ) ) , UnpackNormal( tex2D( _NormalDetailTex, uv_NormalDetailTex ) ) );
			float2 uv_MainTex = i.uv_texcoord * _MainTex_ST.xy + _MainTex_ST.zw;
			float4 temp_output_5_0 = saturate( tex2D( _MainTex, uv_MainTex ) );
			o.Albedo = ( ( _Color * temp_output_5_0 ) + ( ( 1.0 - temp_output_5_0 ) * _Color2 ) ).rgb;
			sampler2D CausticsMap96_g1 = _CausticsMap;
			sampler2D FlowMap96_g1 = _FlowMap;
			float GlobalScale96_g1 = _GlobalScale;
			float CausticsScale_196_g1 = _CausticsScale_1;
			float CausticsScale_296_g1 = _CausticsScale_2;
			float CausticsScale_396_g1 = _CausticsScale_3;
			float FlowMapScale_196_g1 = _FlowMapScale_1;
			float FlowMapScale_296_g1 = _FlowMapScale_2;
			float FlowMapScale_396_g1 = _FlowMapScale_3;
			float GlobalSpeed96_g1 = _GlobalSpeed;
			float CausticsSpeed_196_g1 = _CausticsSpeed_1;
			float CausticsSpeed_296_g1 = _CausticsSpeed_2;
			float CausticsSpeed_396_g1 = _CausticsSpeed_3;
			float3 ase_worldNormal = WorldNormalVector( i, float3( 0, 0, 1 ) );
			float3 worldNormal96_g1 = ase_worldNormal;
			float localIntensity96_g1 = _CausticsIntensity;
			float localScale96_g1 = _CausticsScale;
			float CausticsWave1_Multiply96_g1 = _CausticsWave1_Multiply;
			float3 ase_worldPos = i.worldPos;
			float2 appendResult106_g1 = (float2(ase_worldPos.x , ase_worldPos.z));
			float2 uv96_g1 = appendResult106_g1;
			float FlowMapIntensity96_g1 = _FlowMapIntensity;
			float localGetCaustics96_g1 = GetCaustics96_g1( CausticsMap96_g1 , FlowMap96_g1 , GlobalScale96_g1 , CausticsScale_196_g1 , CausticsScale_296_g1 , CausticsScale_396_g1 , FlowMapScale_196_g1 , FlowMapScale_296_g1 , FlowMapScale_396_g1 , GlobalSpeed96_g1 , CausticsSpeed_196_g1 , CausticsSpeed_296_g1 , CausticsSpeed_396_g1 , worldNormal96_g1 , localIntensity96_g1 , localScale96_g1 , CausticsWave1_Multiply96_g1 , uv96_g1 , FlowMapIntensity96_g1 );
			float3 temp_cast_1 = (localGetCaustics96_g1).xxx;
			o.Emission = temp_cast_1;
			o.Metallic = _Metallic2;
			o.Smoothness = _Glossiness2;
			o.Alpha = 1;
		}

		ENDCG
		CGPROGRAM
		#pragma surface surf Standard keepalpha fullforwardshadows 

		ENDCG
		Pass
		{
			Name "ShadowCaster"
			Tags{ "LightMode" = "ShadowCaster" }
			ZWrite On
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#pragma multi_compile_shadowcaster
			#pragma multi_compile UNITY_PASS_SHADOWCASTER
			#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
			#include "HLSLSupport.cginc"
			#if ( SHADER_API_D3D11 || SHADER_API_GLCORE || SHADER_API_GLES || SHADER_API_GLES3 || SHADER_API_METAL || SHADER_API_VULKAN )
				#define CAN_SKIP_VPOS
			#endif
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "UnityPBSLighting.cginc"
			struct v2f
			{
				V2F_SHADOW_CASTER;
				float2 customPack1 : TEXCOORD1;
				float4 tSpace0 : TEXCOORD2;
				float4 tSpace1 : TEXCOORD3;
				float4 tSpace2 : TEXCOORD4;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
			v2f vert( appdata_full v )
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID( v );
				UNITY_INITIALIZE_OUTPUT( v2f, o );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );
				UNITY_TRANSFER_INSTANCE_ID( v, o );
				Input customInputData;
				float3 worldPos = mul( unity_ObjectToWorld, v.vertex ).xyz;
				half3 worldNormal = UnityObjectToWorldNormal( v.normal );
				half3 worldTangent = UnityObjectToWorldDir( v.tangent.xyz );
				half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
				half3 worldBinormal = cross( worldNormal, worldTangent ) * tangentSign;
				o.tSpace0 = float4( worldTangent.x, worldBinormal.x, worldNormal.x, worldPos.x );
				o.tSpace1 = float4( worldTangent.y, worldBinormal.y, worldNormal.y, worldPos.y );
				o.tSpace2 = float4( worldTangent.z, worldBinormal.z, worldNormal.z, worldPos.z );
				o.customPack1.xy = customInputData.uv_texcoord;
				o.customPack1.xy = v.texcoord;
				TRANSFER_SHADOW_CASTER_NORMALOFFSET( o )
				return o;
			}
			half4 frag( v2f IN
			#if !defined( CAN_SKIP_VPOS )
			, UNITY_VPOS_TYPE vpos : VPOS
			#endif
			) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				Input surfIN;
				UNITY_INITIALIZE_OUTPUT( Input, surfIN );
				surfIN.uv_texcoord = IN.customPack1.xy;
				float3 worldPos = float3( IN.tSpace0.w, IN.tSpace1.w, IN.tSpace2.w );
				half3 worldViewDir = normalize( UnityWorldSpaceViewDir( worldPos ) );
				surfIN.worldPos = worldPos;
				surfIN.worldNormal = float3( IN.tSpace0.z, IN.tSpace1.z, IN.tSpace2.z );
				surfIN.internalSurfaceTtoW0 = IN.tSpace0.xyz;
				surfIN.internalSurfaceTtoW1 = IN.tSpace1.xyz;
				surfIN.internalSurfaceTtoW2 = IN.tSpace2.xyz;
				SurfaceOutputStandard o;
				UNITY_INITIALIZE_OUTPUT( SurfaceOutputStandard, o )
				surf( surfIN, o );
				#if defined( CAN_SKIP_VPOS )
				float2 vpos = IN.pos;
				#endif
				SHADOW_CASTER_FRAGMENT( IN )
			}
			ENDCG
		}
	}
	Fallback "Diffuse"
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=18900
1536;72;1239;658;1847.325;188.9583;2.137143;True;False
Node;AmplifyShaderEditor.SamplerNode;4;-890.2211,82.25142;Inherit;True;Property;_MainTex;Main Tex;7;0;Create;False;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SaturateNode;5;-543.5,87;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorNode;2;-891.3281,-109.2857;Inherit;False;Property;_Color;Color 01;5;0;Create;False;0;0;0;False;0;False;1,1,1,0;1,1,1,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.OneMinusNode;6;-331.7483,88.49078;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorNode;7;-329.7483,182.4908;Inherit;False;Property;_Color2;Color 02;6;0;Create;False;0;0;0;False;0;False;1,1,1,0;1,1,1,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;3;-377.951,-105.9657;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;8;-73.74829,89.49078;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;13;-891.345,663.4619;Inherit;True;Property;_NormalDetailTex;Normal Detail Tex;10;1;[Normal];Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;10;-888.6649,436.3719;Inherit;True;Property;_NormalTex;Normal Tex;9;1;[Normal];Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;9;108.8576,-102.564;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.BlendNormalsNode;16;-536.2756,441.5162;Inherit;False;0;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;14;-889.5424,979.7405;Inherit;False;Property;_Metallic2;Metallic;11;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;1;-888.8706,891.7167;Inherit;False;GlowWaves;0;;1;5c3820833cf4f694b98bdc169aaa0e35;0;0;1;FLOAT;99
Node;AmplifyShaderEditor.RangedFloatNode;15;-888.9898,1073.531;Inherit;False;Property;_Glossiness2;Smoothness;8;0;Create;False;0;0;0;False;0;False;0.5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;491.1103,-106.9104;Float;False;True;-1;2;ASEMaterialInspector;0;0;Standard;FI/OEP/TwoGradientsWithCaustics;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;All;14;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;0;0;False;-1;0;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;5;0;4;0
WireConnection;6;0;5;0
WireConnection;3;0;2;0
WireConnection;3;1;5;0
WireConnection;8;0;6;0
WireConnection;8;1;7;0
WireConnection;9;0;3;0
WireConnection;9;1;8;0
WireConnection;16;0;10;0
WireConnection;16;1;13;0
WireConnection;0;0;9;0
WireConnection;0;1;16;0
WireConnection;0;2;1;99
WireConnection;0;3;14;0
WireConnection;0;4;15;0
ASEEND*/
//CHKSM=1DB829682EC18F0FE86C10B32BCA60C74199B56E