// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "FI/OEP/Jellyfish"
{
	Properties
	{
		_VAT_Amount("VAT_Amount", Float) = 1
		_OffsetScale("Offset Scale", Float) = 1
		_VATSpeed("VAT Speed", Float) = 1
		_VAT("VAT", 2D) = "black" {}
		_EmissiveTex("Emissive Tex", 2D) = "black" {}
		_Color("Color", Color) = (1,1,1,0)
		[HDR]_EmissiveColor("Emissive Color", Color) = (0,0,0,0)
		[HDR]_EmissiveAditive("Emissive Aditive", Color) = (0,0,0,0)
		_Glossiness1("Smoothness", Float) = 0.5
		_MainTex("Main Tex", 2D) = "white" {}
		_Metallic1("Metallic", Float) = 0
		[Normal]_NormalTex("Normal Tex", 2D) = "bump" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IgnoreProjector" = "True" "IsEmissive" = "true"  }
		Cull Off
		CGINCLUDE
		#include "UnityPBSLighting.cginc"
		#include "Lighting.cginc"
		#pragma target 3.0
		#include "FIUtils.cginc"
		struct Input
		{
			float3 worldPos;
			float2 uv_texcoord;
		};

		uniform float _VATSpeed;
		uniform sampler2D _VAT;
		uniform float _OffsetScale;
		uniform float _VAT_Amount;
		uniform sampler2D _NormalTex;
		uniform float4 _NormalTex_ST;
		uniform float4 _Color;
		uniform sampler2D _MainTex;
		uniform float4 _MainTex_ST;
		uniform sampler2D _EmissiveTex;
		uniform float4 _EmissiveTex_ST;
		uniform float4 _EmissiveColor;
		uniform float4 _EmissiveAditive;
		uniform float _Metallic1;
		uniform float _Glossiness1;


		float3 MyCustomExpression1_g4( float2 uv, float VATSpeed, sampler2D VAT, float OffsetScale, float VAT_Amount, float3 worldPos )
		{
			float3 vertexOffset = MS_MorphTargets(uv, frac(_Time.y * VATSpeed + floor(worldPos.x + worldPos.z) * OffsetScale), VAT, VAT, 51);
			return float3(-vertexOffset.x, vertexOffset.z, vertexOffset.y) * 0.05 * VAT_Amount;
		}


		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float2 uv1_g4 = v.texcoord1.xy;
			float VATSpeed1_g4 = _VATSpeed;
			sampler2D VAT1_g4 = _VAT;
			float OffsetScale1_g4 = _OffsetScale;
			float VAT_Amount1_g4 = _VAT_Amount;
			float3 ase_worldPos = mul( unity_ObjectToWorld, v.vertex );
			float3 worldPos1_g4 = ase_worldPos;
			float3 localMyCustomExpression1_g4 = MyCustomExpression1_g4( uv1_g4 , VATSpeed1_g4 , VAT1_g4 , OffsetScale1_g4 , VAT_Amount1_g4 , worldPos1_g4 );
			v.vertex.xyz += localMyCustomExpression1_g4;
			v.vertex.w = 1;
		}

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_NormalTex = i.uv_texcoord * _NormalTex_ST.xy + _NormalTex_ST.zw;
			o.Normal = UnpackNormal( tex2D( _NormalTex, uv_NormalTex ) );
			float2 uv_MainTex = i.uv_texcoord * _MainTex_ST.xy + _MainTex_ST.zw;
			float4 tex2DNode3 = tex2D( _MainTex, uv_MainTex );
			o.Albedo = ( _Color * tex2DNode3 ).rgb;
			float2 uv_EmissiveTex = i.uv_texcoord * _EmissiveTex_ST.xy + _EmissiveTex_ST.zw;
			o.Emission = ( ( tex2D( _EmissiveTex, uv_EmissiveTex ) * _EmissiveColor ) + _EmissiveAditive ).rgb;
			o.Metallic = _Metallic1;
			o.Smoothness = _Glossiness1;
			o.Alpha = tex2DNode3.r;
		}

		ENDCG
		CGPROGRAM
		#pragma surface surf Standard alpha:fade keepalpha fullforwardshadows vertex:vertexDataFunc 

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
			sampler3D _DitherMaskLOD;
			struct v2f
			{
				V2F_SHADOW_CASTER;
				float2 customPack1 : TEXCOORD1;
				float3 worldPos : TEXCOORD2;
				float4 tSpace0 : TEXCOORD3;
				float4 tSpace1 : TEXCOORD4;
				float4 tSpace2 : TEXCOORD5;
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
				o.worldPos = worldPos;
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
				float3 worldPos = IN.worldPos;
				half3 worldViewDir = normalize( UnityWorldSpaceViewDir( worldPos ) );
				surfIN.worldPos = worldPos;
				SurfaceOutputStandard o;
				UNITY_INITIALIZE_OUTPUT( SurfaceOutputStandard, o )
				surf( surfIN, o );
				#if defined( CAN_SKIP_VPOS )
				float2 vpos = IN.pos;
				#endif
				half alphaRef = tex3D( _DitherMaskLOD, float3( vpos.xy * 0.25, o.Alpha * 0.9375 ) ).a;
				clip( alphaRef - 0.01 );
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
1536;72;1048;658;1632.207;-320.7952;1.446436;True;False
Node;AmplifyShaderEditor.SamplerNode;13;-763.0374,224.4879;Inherit;True;Property;_EmissiveTex;Emissive Tex;5;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;black;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;4;-762.3245,438.1854;Inherit;False;Property;_EmissiveColor;Emissive Color;7;1;[HDR];Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;5;-766.4901,-414.7458;Inherit;False;Property;_Color;Color;6;0;Create;True;0;0;0;False;0;False;1,1,1,0;1,1,1,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;3;-765.3831,-223.2087;Inherit;True;Property;_MainTex;Main Tex;10;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;15;-417.3669,228.3353;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorNode;14;-763.4424,633.4695;Inherit;False;Property;_EmissiveAditive;Emissive Aditive;8;1;[HDR];Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;6;-426.113,-410.4258;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;2;-765.3831,-1.315515;Inherit;True;Property;_NormalTex;Normal Tex;12;1;[Normal];Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;7;-233.7586,228.6679;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;8;-762.8484,941.1902;Inherit;False;Property;_Metallic1;Metallic;11;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;9;-763.2958,1048.98;Inherit;False;Property;_Glossiness1;Smoothness;9;0;Create;False;0;0;0;False;0;False;0.5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;12;-762.9885,1160.706;Inherit;False;VAT;0;;4;6fabe6e1ba8c2b64b9be7a5a9183d46c;0;0;1;FLOAT3;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;141.1484,-407.6492;Float;False;True;-1;2;ASEMaterialInspector;0;0;Standard;FI/OEP/Jellyfish;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;False;Off;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Transparent;0.5;True;True;0;False;Transparent;;Transparent;All;14;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;2;5;False;-1;10;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;16;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;15;0;13;0
WireConnection;15;1;4;0
WireConnection;6;0;5;0
WireConnection;6;1;3;0
WireConnection;7;0;15;0
WireConnection;7;1;14;0
WireConnection;0;0;6;0
WireConnection;0;1;2;0
WireConnection;0;2;7;0
WireConnection;0;3;8;0
WireConnection;0;4;9;0
WireConnection;0;9;3;1
WireConnection;0;11;12;0
ASEEND*/
//CHKSM=0B6A19618071A71123F919BFC3142C9D7B9F5B26