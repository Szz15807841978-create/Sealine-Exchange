// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "FI/OEP/GrassWithCaustics"
{
	Properties
	{
		_Cutoff( "Mask Clip Value", Float ) = 0.5
		_WindGradient("Wind Gradient", 2D) = "white" {}
		_WindIntensity("Wind Intensity", Float) = 1
		_WindSpeed("Wind Speed", Float) = 0.1
		_WindMaskInvert("Wind Mask Invert", Float) = 1
		_WindWeight("Wind Weight", Float) = 1
		_WindScale("Wind Scale", Float) = 1
		_CausticsIntensity("_CausticsIntensity", Float) = 1
		_CausticsScale("_CausticsScale", Float) = 1
		_Color("Color", Color) = (1,1,1,0)
		_Glossiness1("Smoothness", Float) = 0.5
		_Metallic1("Metallic", Float) = 0
		[HDR]_EmissiveColor("Emissive Color", Color) = (0,0,0,0)
		_NormalIntensity("Normal Intensity", Float) = 1
		_MainTex("Main Tex", 2D) = "white" {}
		[Normal]_NormalTex("Normal Tex", 2D) = "bump" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "TransparentCutout"  "Queue" = "AlphaTest+0" "IsEmissive" = "true"  }
		Cull Off
		CGINCLUDE
		#include "UnityStandardUtils.cginc"
		#include "UnityPBSLighting.cginc"
		#include "Lighting.cginc"
		#pragma target 3.0
		#include "FIUtils.cginc"
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
			float3 worldPos;
			float2 uv_texcoord;
			float3 worldNormal;
			INTERNAL_DATA
		};

		uniform float _WindIntensity;
		uniform float _WindWeight;
		uniform float _WindSpeed;
		uniform float _WindScale;
		uniform float _WindMaskInvert;
		uniform sampler2D _WindGradient;
		uniform float4 _WindGradient_ST;
		uniform sampler2D _NormalTex;
		uniform float4 _NormalTex_ST;
		uniform float _NormalIntensity;
		uniform float4 _Color;
		uniform sampler2D _MainTex;
		uniform float4 _MainTex_ST;
		uniform float4 _EmissiveColor;
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
		uniform float _Metallic1;
		uniform float _Glossiness1;
		uniform float _Cutoff = 0.5;


		float3 MyCustomExpression3_g12( float3 WorldPosition, float3 AdditionalWPO, float WindIntensity, float WindWeight, float WindSpeed, float WindScale, float WindMaskInvert )
		{
			return GrassWindMovement(WorldPosition, AdditionalWPO, WindIntensity, WindWeight, WindSpeed, WindScale, WindMaskInvert);
		}


		float GetCaustics96_g6( sampler2D CausticsMap, sampler2D FlowMap, float GlobalScale, float CausticsScale_1, float CausticsScale_2, float CausticsScale_3, float FlowMapScale_1, float FlowMapScale_2, float FlowMapScale_3, float GlobalSpeed, float CausticsSpeed_1, float CausticsSpeed_2, float CausticsSpeed_3, float3 worldNormal, float localIntensity, float localScale, float CausticsWave1_Multiply, float2 uv, float FlowMapIntensity )
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


		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float3 ase_worldPos = mul( unity_ObjectToWorld, v.vertex );
			float3 WorldPosition3_g12 = ase_worldPos;
			float3 AdditionalWPO3_g12 = float3( 0,0,0 );
			float WindIntensity3_g12 = _WindIntensity;
			float WindWeight3_g12 = _WindWeight;
			float WindSpeed3_g12 = _WindSpeed;
			float WindScale3_g12 = _WindScale;
			float WindMaskInvert3_g12 = _WindMaskInvert;
			float3 localMyCustomExpression3_g12 = MyCustomExpression3_g12( WorldPosition3_g12 , AdditionalWPO3_g12 , WindIntensity3_g12 , WindWeight3_g12 , WindSpeed3_g12 , WindScale3_g12 , WindMaskInvert3_g12 );
			float2 uv_WindGradient = v.texcoord * _WindGradient_ST.xy + _WindGradient_ST.zw;
			float3 lerpResult5_g12 = lerp( float3( 0,0,0 ) , localMyCustomExpression3_g12 , tex2Dlod( _WindGradient, float4( uv_WindGradient, 0, 0.0) ).r);
			v.vertex.xyz += lerpResult5_g12;
			v.vertex.w = 1;
		}

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_NormalTex = i.uv_texcoord * _NormalTex_ST.xy + _NormalTex_ST.zw;
			o.Normal = UnpackScaleNormal( tex2D( _NormalTex, uv_NormalTex ), _NormalIntensity );
			float2 uv_MainTex = i.uv_texcoord * _MainTex_ST.xy + _MainTex_ST.zw;
			float4 tex2DNode7 = tex2D( _MainTex, uv_MainTex );
			o.Albedo = ( _Color * tex2DNode7 ).rgb;
			sampler2D CausticsMap96_g6 = _CausticsMap;
			sampler2D FlowMap96_g6 = _FlowMap;
			float GlobalScale96_g6 = _GlobalScale;
			float CausticsScale_196_g6 = _CausticsScale_1;
			float CausticsScale_296_g6 = _CausticsScale_2;
			float CausticsScale_396_g6 = _CausticsScale_3;
			float FlowMapScale_196_g6 = _FlowMapScale_1;
			float FlowMapScale_296_g6 = _FlowMapScale_2;
			float FlowMapScale_396_g6 = _FlowMapScale_3;
			float GlobalSpeed96_g6 = _GlobalSpeed;
			float CausticsSpeed_196_g6 = _CausticsSpeed_1;
			float CausticsSpeed_296_g6 = _CausticsSpeed_2;
			float CausticsSpeed_396_g6 = _CausticsSpeed_3;
			float3 ase_worldNormal = WorldNormalVector( i, float3( 0, 0, 1 ) );
			float3 worldNormal96_g6 = ase_worldNormal;
			float localIntensity96_g6 = _CausticsIntensity;
			float localScale96_g6 = _CausticsScale;
			float CausticsWave1_Multiply96_g6 = _CausticsWave1_Multiply;
			float3 ase_worldPos = i.worldPos;
			float2 appendResult106_g6 = (float2(ase_worldPos.x , ase_worldPos.z));
			float2 uv96_g6 = appendResult106_g6;
			float FlowMapIntensity96_g6 = _FlowMapIntensity;
			float localGetCaustics96_g6 = GetCaustics96_g6( CausticsMap96_g6 , FlowMap96_g6 , GlobalScale96_g6 , CausticsScale_196_g6 , CausticsScale_296_g6 , CausticsScale_396_g6 , FlowMapScale_196_g6 , FlowMapScale_296_g6 , FlowMapScale_396_g6 , GlobalSpeed96_g6 , CausticsSpeed_196_g6 , CausticsSpeed_296_g6 , CausticsSpeed_396_g6 , worldNormal96_g6 , localIntensity96_g6 , localScale96_g6 , CausticsWave1_Multiply96_g6 , uv96_g6 , FlowMapIntensity96_g6 );
			o.Emission = ( _EmissiveColor + localGetCaustics96_g6 ).rgb;
			o.Metallic = _Metallic1;
			o.Smoothness = _Glossiness1;
			o.Alpha = 1;
			clip( tex2DNode7.a - _Cutoff );
		}

		ENDCG
		CGPROGRAM
		#pragma surface surf Standard keepalpha fullforwardshadows exclude_path:deferred vertex:vertexDataFunc 

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
				vertexDataFunc( v, customInputData );
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
1536;72;1236;658;1340.396;124.4931;1.277107;True;False
Node;AmplifyShaderEditor.ColorNode;5;-896.6248,-86.34909;Inherit;False;Property;_Color;Color;13;0;Create;True;0;0;0;False;0;False;1,1,1,0;1,1,1,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;13;-1136.222,373.8835;Inherit;False;Property;_NormalIntensity;Normal Intensity;17;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;6;-894.5178,586.1878;Inherit;False;Property;_EmissiveColor;Emissive Color;16;1;[HDR];Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.FunctionNode;15;-894.9393,786.4415;Inherit;False;GlowWaves;8;;6;5c3820833cf4f694b98bdc169aaa0e35;0;0;1;FLOAT;99
Node;AmplifyShaderEditor.SamplerNode;7;-895.5178,105.188;Inherit;True;Property;_MainTex;Main Tex;18;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;8;-556.2476,-82.02914;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;9;-640.7928,590.8644;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;11;-895.3972,890.6321;Inherit;False;Property;_Metallic1;Metallic;15;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;21;-894.0302,1097.431;Inherit;False;GrassWindMovement;1;;12;18bff53d92cb42540af38da19c3f3b42;0;1;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SamplerNode;10;-895.5178,328.1879;Inherit;True;Property;_NormalTex;Normal Tex;19;1;[Normal];Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;12;-895.8445,999.4221;Inherit;False;Property;_Glossiness1;Smoothness;14;0;Create;False;0;0;0;False;0;False;0.5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;-37.01525,-81.57919;Float;False;True;-1;2;ASEMaterialInspector;0;0;Standard;FI/OEP/GrassWithCaustics;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Off;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Masked;0.5;True;True;0;False;TransparentCutout;;AlphaTest;ForwardOnly;14;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;0;0;False;-1;0;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;0;0;-1;-1;0;False;0;0;False;-1;-1;0;False;22;0;0;0;False;0.1;False;-1;0;False;-1;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;8;0;5;0
WireConnection;8;1;7;0
WireConnection;9;0;6;0
WireConnection;9;1;15;99
WireConnection;10;5;13;0
WireConnection;0;0;8;0
WireConnection;0;1;10;0
WireConnection;0;2;9;0
WireConnection;0;3;11;0
WireConnection;0;4;12;0
WireConnection;0;10;7;4
WireConnection;0;11;21;0
ASEEND*/
//CHKSM=B32EC099CCF50F56AD1873F9DB39A44418BFB6D9