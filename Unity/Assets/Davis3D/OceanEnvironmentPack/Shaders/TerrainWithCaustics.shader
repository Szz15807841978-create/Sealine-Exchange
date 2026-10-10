// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "FI/OEP/TerrainWithCaustics"
{
	Properties
	{
		_CausticsIntensity("_CausticsIntensity", Float) = 1
		_CausticsScale("_CausticsScale", Float) = 1
		_Splatmap("Splatmap", 2D) = "white" {}
		_Albedo01("Albedo01", 2D) = "white" {}
		[Normal]_Normal01("Normal01", 2D) = "bump" {}
		[Normal]_Normal02("Normal02", 2D) = "bump" {}
		[Normal]_Normal03("Normal03", 2D) = "bump" {}
		[Normal]_Normal04("Normal04", 2D) = "bump" {}
		_NormalIntensity01("Normal Intensity 01", Float) = 1
		_NormalIntensity02("Normal Intensity 02", Float) = 1
		_NormalIntensity03("Normal Intensity 03", Float) = 1
		_NormalIntensity04("Normal Intensity 04", Float) = 1
		_Albedo02("Albedo02", 2D) = "white" {}
		_Albedo03("Albedo03", 2D) = "white" {}
		_Albedo04("Albedo04", 2D) = "white" {}
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

		uniform sampler2D _Splatmap;
		uniform float4 _Splatmap_ST;
		uniform sampler2D _Normal01;
		uniform float4 _Normal01_ST;
		uniform float _NormalIntensity01;
		uniform sampler2D _Normal02;
		uniform float4 _Normal02_ST;
		uniform float _NormalIntensity02;
		uniform sampler2D _Normal03;
		uniform float4 _Normal03_ST;
		uniform float _NormalIntensity03;
		uniform sampler2D _Normal04;
		uniform float4 _Normal04_ST;
		uniform float _NormalIntensity04;
		uniform sampler2D _Albedo01;
		uniform float4 _Albedo01_ST;
		uniform sampler2D _Albedo02;
		uniform float4 _Albedo02_ST;
		uniform sampler2D _Albedo03;
		uniform float4 _Albedo03_ST;
		uniform sampler2D _Albedo04;
		uniform float4 _Albedo04_ST;
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


		float GetCaustics96_g8( sampler2D CausticsMap, sampler2D FlowMap, float GlobalScale, float CausticsScale_1, float CausticsScale_2, float CausticsScale_3, float FlowMapScale_1, float FlowMapScale_2, float FlowMapScale_3, float GlobalSpeed, float CausticsSpeed_1, float CausticsSpeed_2, float CausticsSpeed_3, float3 worldNormal, float localIntensity, float localScale, float CausticsWave1_Multiply, float2 uv, float FlowMapIntensity )
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
			float2 uv_Splatmap = i.uv_texcoord * _Splatmap_ST.xy + _Splatmap_ST.zw;
			float4 tex2DNode2 = tex2D( _Splatmap, uv_Splatmap );
			float4 SplatMap24 = tex2DNode2;
			float4 break29 = SplatMap24;
			float2 uv_Normal01 = i.uv_texcoord * _Normal01_ST.xy + _Normal01_ST.zw;
			float3 temp_output_28_0 = ( break29.r * UnpackScaleNormal( tex2D( _Normal01, uv_Normal01 ), _NormalIntensity01 ) );
			float2 uv_Normal02 = i.uv_texcoord * _Normal02_ST.xy + _Normal02_ST.zw;
			float3 temp_output_30_0 = ( break29.g * UnpackScaleNormal( tex2D( _Normal02, uv_Normal02 ), _NormalIntensity02 ) );
			float2 uv_Normal03 = i.uv_texcoord * _Normal03_ST.xy + _Normal03_ST.zw;
			float3 temp_output_31_0 = ( break29.b * UnpackScaleNormal( tex2D( _Normal03, uv_Normal03 ), _NormalIntensity03 ) );
			float2 uv_Normal04 = i.uv_texcoord * _Normal04_ST.xy + _Normal04_ST.zw;
			float3 temp_output_32_0 = ( break29.a * UnpackScaleNormal( tex2D( _Normal04, uv_Normal04 ), _NormalIntensity04 ) );
			o.Normal = ( temp_output_28_0 + temp_output_30_0 + temp_output_31_0 + temp_output_32_0 );
			float2 uv_Albedo01 = i.uv_texcoord * _Albedo01_ST.xy + _Albedo01_ST.zw;
			float2 uv_Albedo02 = i.uv_texcoord * _Albedo02_ST.xy + _Albedo02_ST.zw;
			float2 uv_Albedo03 = i.uv_texcoord * _Albedo03_ST.xy + _Albedo03_ST.zw;
			float2 uv_Albedo04 = i.uv_texcoord * _Albedo04_ST.xy + _Albedo04_ST.zw;
			o.Albedo = saturate( ( ( tex2DNode2.r * tex2D( _Albedo01, uv_Albedo01 ) ) + ( tex2DNode2.g * tex2D( _Albedo02, uv_Albedo02 ) ) + ( tex2DNode2.b * tex2D( _Albedo03, uv_Albedo03 ) ) + ( tex2DNode2.a * tex2D( _Albedo04, uv_Albedo04 ) ) ) ).rgb;
			sampler2D CausticsMap96_g8 = _CausticsMap;
			sampler2D FlowMap96_g8 = _FlowMap;
			float GlobalScale96_g8 = _GlobalScale;
			float CausticsScale_196_g8 = _CausticsScale_1;
			float CausticsScale_296_g8 = _CausticsScale_2;
			float CausticsScale_396_g8 = _CausticsScale_3;
			float FlowMapScale_196_g8 = _FlowMapScale_1;
			float FlowMapScale_296_g8 = _FlowMapScale_2;
			float FlowMapScale_396_g8 = _FlowMapScale_3;
			float GlobalSpeed96_g8 = _GlobalSpeed;
			float CausticsSpeed_196_g8 = _CausticsSpeed_1;
			float CausticsSpeed_296_g8 = _CausticsSpeed_2;
			float CausticsSpeed_396_g8 = _CausticsSpeed_3;
			float3 ase_worldNormal = WorldNormalVector( i, float3( 0, 0, 1 ) );
			float3 worldNormal96_g8 = ase_worldNormal;
			float localIntensity96_g8 = _CausticsIntensity;
			float localScale96_g8 = _CausticsScale;
			float CausticsWave1_Multiply96_g8 = _CausticsWave1_Multiply;
			float3 ase_worldPos = i.worldPos;
			float2 appendResult106_g8 = (float2(ase_worldPos.x , ase_worldPos.z));
			float2 uv96_g8 = appendResult106_g8;
			float FlowMapIntensity96_g8 = _FlowMapIntensity;
			float localGetCaustics96_g8 = GetCaustics96_g8( CausticsMap96_g8 , FlowMap96_g8 , GlobalScale96_g8 , CausticsScale_196_g8 , CausticsScale_296_g8 , CausticsScale_396_g8 , FlowMapScale_196_g8 , FlowMapScale_296_g8 , FlowMapScale_396_g8 , GlobalSpeed96_g8 , CausticsSpeed_196_g8 , CausticsSpeed_296_g8 , CausticsSpeed_396_g8 , worldNormal96_g8 , localIntensity96_g8 , localScale96_g8 , CausticsWave1_Multiply96_g8 , uv96_g8 , FlowMapIntensity96_g8 );
			float3 temp_cast_1 = (localGetCaustics96_g8).xxx;
			o.Emission = temp_cast_1;
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
1536;72;1411;658;669.2748;158.1802;1.414337;True;False
Node;AmplifyShaderEditor.SamplerNode;2;-1020.959,-126.3559;Inherit;True;Property;_Splatmap;Splatmap;5;0;Create;True;0;0;0;False;0;False;-1;None;30e5bd9d634595d44bcac57f42b7825f;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;24;-617.2271,-211.1368;Inherit;False;SplatMap;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;20;-1324.944,1374.037;Inherit;False;Property;_NormalIntensity01;Normal Intensity 01;11;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;25;-1019.609,1153.475;Inherit;False;24;SplatMap;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;23;-1321.332,2110.983;Inherit;False;Property;_NormalIntensity04;Normal Intensity 04;14;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;22;-1319.944,1861.536;Inherit;False;Property;_NormalIntensity03;Normal Intensity 03;13;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;21;-1321.176,1615.116;Inherit;False;Property;_NormalIntensity02;Normal Intensity 02;12;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;6;-1024.911,798.6301;Inherit;True;Property;_Albedo04;Albedo04;17;0;Create;True;0;0;0;False;0;False;-1;None;189b4eb15d2881045ad39c402b2ff20c;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;5;-1023.612,577.6299;Inherit;True;Property;_Albedo03;Albedo03;16;0;Create;True;0;0;0;False;0;False;-1;None;44c3726fd56269c4e8bf0ed6f319c912;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;3;-1022.982,126.8026;Inherit;True;Property;_Albedo01;Albedo01;6;0;Create;True;0;0;0;False;0;False;-1;None;2abbb46248428164da914825e5b4d068;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;4;-1023.611,346.2297;Inherit;True;Property;_Albedo02;Albedo02;15;0;Create;True;0;0;0;False;0;False;-1;None;0ed616c09680ec643b69fe5017a771ba;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;15;-615.0789,242.7712;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.BreakToComponentsNode;29;-826.2347,1158.322;Inherit;False;COLOR;1;0;COLOR;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.SamplerNode;7;-1023.417,1329.457;Inherit;True;Property;_Normal01;Normal01;7;1;[Normal];Create;True;0;0;0;False;0;False;-1;None;4fec2ff5df8e780419c48b3cee502106;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;13;-616.1862,13.37277;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;9;-1024.626,1816.672;Inherit;True;Property;_Normal03;Normal03;9;1;[Normal];Create;True;0;0;0;False;0;False;-1;None;6cf3d9caca5748c4fa3e13b4c9a12876;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;10;-1025.493,2065.316;Inherit;True;Property;_Normal04;Normal04;10;1;[Normal];Create;True;0;0;0;False;0;False;-1;None;a417cd0833bc5c84284c0c7f79f8f3dc;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;8;-1024.626,1568.672;Inherit;True;Property;_Normal02;Normal02;8;1;[Normal];Create;True;0;0;0;False;0;False;-1;None;62a7e2db27b3464489abcb4d72db7fd6;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;12;-616.3497,-100.0659;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;14;-616.5122,126.7395;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;16;-368.0969,-100.4288;Inherit;False;4;4;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;28;-634.778,1157.822;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;31;-633.0779,1399.693;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;30;-632.881,1285.065;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;32;-633.0779,1515.693;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.BlendNormalsNode;33;-447.97,1158.631;Inherit;False;0;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.BlendNormalsNode;34;-447.97,1286.631;Inherit;False;0;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.BlendNormalsNode;35;-197.97,1158.631;Inherit;False;0;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SaturateNode;18;-162.0969,-101.4288;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;36;-446.3956,1410.186;Inherit;False;4;4;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.FunctionNode;43;382.9919,387.7538;Inherit;False;GlowWaves;0;;8;5c3820833cf4f694b98bdc169aaa0e35;0;0;1;FLOAT;99
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;605.0948,-96.51039;Float;False;True;-1;2;ASEMaterialInspector;0;0;Standard;FI/OEP/TerrainWithCaustics;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;All;14;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;0;0;False;-1;0;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;24;0;2;0
WireConnection;15;0;2;4
WireConnection;15;1;6;0
WireConnection;29;0;25;0
WireConnection;7;5;20;0
WireConnection;13;0;2;2
WireConnection;13;1;4;0
WireConnection;9;5;22;0
WireConnection;10;5;23;0
WireConnection;8;5;21;0
WireConnection;12;0;2;1
WireConnection;12;1;3;0
WireConnection;14;0;2;3
WireConnection;14;1;5;0
WireConnection;16;0;12;0
WireConnection;16;1;13;0
WireConnection;16;2;14;0
WireConnection;16;3;15;0
WireConnection;28;0;29;0
WireConnection;28;1;7;0
WireConnection;31;0;29;2
WireConnection;31;1;9;0
WireConnection;30;0;29;1
WireConnection;30;1;8;0
WireConnection;32;0;29;3
WireConnection;32;1;10;0
WireConnection;33;0;28;0
WireConnection;33;1;30;0
WireConnection;34;0;31;0
WireConnection;34;1;32;0
WireConnection;35;0;33;0
WireConnection;35;1;34;0
WireConnection;18;0;16;0
WireConnection;36;0;28;0
WireConnection;36;1;30;0
WireConnection;36;2;31;0
WireConnection;36;3;32;0
WireConnection;0;0;18;0
WireConnection;0;1;36;0
WireConnection;0;2;43;99
ASEEND*/
//CHKSM=A3949EFA13FFEAE5BD09087586999940EFC29119