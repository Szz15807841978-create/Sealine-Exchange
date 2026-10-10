// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "FI/OEP/GodRays"
{
	Properties
	{
		_MainTex("Main Tex", 2D) = "white" {}
		_GlowIntensity("Glow Intensity", Float) = 1
		_FresnelExponent("Fresnel Exponent", Float) = 5
		_MaxDistance("Max Distance", Float) = 100
		_Scale_A("Scale_A", Float) = 500
		_Scale_B("Scale_B", Float) = 500
		_Panner_A("Panner_A", Float) = 25
		_Panner_B("Panner_B", Float) = 5
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Custom"  "Queue" = "Transparent+0" "IgnoreProjector" = "True" "IsEmissive" = "true"  }
		Cull Off
		Blend One One , One One
		
		CGINCLUDE
		#include "UnityShaderVariables.cginc"
		#include "UnityPBSLighting.cginc"
		#include "Lighting.cginc"
		#pragma target 3.0
		struct Input
		{
			float2 uv_texcoord;
			float3 worldPos;
			float3 worldNormal;
		};

		uniform float _Scale_A;
		uniform float _Scale_B;
		uniform float _Panner_A;
		uniform float _Panner_B;
		uniform float _FresnelExponent;
		uniform float _MaxDistance;
		uniform sampler2D _MainTex;
		uniform float4 _MainTex_ST;
		uniform float _GlowIntensity;


		float4 MyCustomExpression13( float2 uv, float Scale_A, float Scale_B, float Panner_A, float Panner_B, float fresnel, float dst, float4 texCol )
		{
			float mask = (sin(uv.x * Scale_A + Panner_A * _Time.y) + 0.8) * (sin(uv.x * Scale_B + Panner_B * _Time.y) + 0.8);
			float4 col = texCol * mask * fresnel * dst;
			return col;
		}


		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float2 uv13 = i.uv_texcoord;
			float Scale_A13 = _Scale_A;
			float Scale_B13 = _Scale_B;
			float Panner_A13 = _Panner_A;
			float Panner_B13 = _Panner_B;
			float3 ase_worldPos = i.worldPos;
			float3 ase_worldViewDir = normalize( UnityWorldSpaceViewDir( ase_worldPos ) );
			float3 ase_worldNormal = i.worldNormal;
			float fresnelNdotV7 = dot( ase_worldNormal, ase_worldViewDir );
			float fresnelNode7 = ( 0.0 + 1.0 * pow( 1.0 - fresnelNdotV7, _FresnelExponent ) );
			float fresnel13 = fresnelNode7;
			float dst13 = ( 1.0 - saturate( ( length( ( _WorldSpaceCameraPos - ase_worldPos ) ) / _MaxDistance ) ) );
			float2 uv_MainTex = i.uv_texcoord * _MainTex_ST.xy + _MainTex_ST.zw;
			float4 texCol13 = ( tex2D( _MainTex, uv_MainTex ) * _GlowIntensity );
			float4 localMyCustomExpression13 = MyCustomExpression13( uv13 , Scale_A13 , Scale_B13 , Panner_A13 , Panner_B13 , fresnel13 , dst13 , texCol13 );
			o.Emission = localMyCustomExpression13.xyz;
			o.Alpha = (localMyCustomExpression13).w;
		}

		ENDCG
		CGPROGRAM
		#pragma surface surf Unlit keepalpha fullforwardshadows noambient novertexlights nolightmap  nodynlightmap nodirlightmap nofog nometa noforwardadd 

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
				float3 worldNormal : TEXCOORD3;
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
				o.worldNormal = worldNormal;
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
				surfIN.worldNormal = IN.worldNormal;
				SurfaceOutput o;
				UNITY_INITIALIZE_OUTPUT( SurfaceOutput, o )
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
1536;72;1412;657;1449.008;514.3729;1.963051;True;False
Node;AmplifyShaderEditor.WorldPosInputsNode;23;-719.4591,634.6957;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.WorldSpaceCameraPos;21;-723.4591,473.6957;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleSubtractOpNode;22;-449.4591,472.6957;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LengthOpNode;20;-277.459,471.6957;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;8;-276.7136,572.6817;Inherit;False;Property;_MaxDistance;Max Distance;4;0;Create;True;0;0;0;False;0;False;100;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;24;-88.45913,471.6957;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;4;-276.9351,677.1572;Inherit;True;Property;_MainTex;Main Tex;1;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;5;-276.6556,883.8131;Inherit;False;Property;_GlowIntensity;Glow Intensity;2;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;6;-539.6548,359.7495;Inherit;False;Property;_FresnelExponent;Fresnel Exponent;3;0;Create;True;0;0;0;False;0;False;5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;27;68.84103,471.7354;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;9;-276.1201,-142.9224;Inherit;False;Property;_Scale_A;Scale_A;5;0;Create;True;0;0;0;False;0;False;500;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;12;-276.1201,154.0778;Inherit;False;Property;_Panner_B;Panner_B;8;0;Create;True;0;0;0;False;0;False;5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;11;-276.1201,60.07761;Inherit;False;Property;_Panner_A;Panner_A;7;0;Create;True;0;0;0;False;0;False;25;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;19;-275.1284,-278.8034;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;10;-276.1201,-38.92245;Inherit;False;Property;_Scale_B;Scale_B;6;0;Create;True;0;0;0;False;0;False;500;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;18;60.67843,682.9376;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.FresnelNode;7;-278.6548,271.7495;Inherit;False;Standard;WorldNormal;ViewDir;False;False;5;0;FLOAT3;0,0,1;False;4;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;26;251.1469,471.0356;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CustomExpressionNode;13;519.5491,-279.436;Inherit;False;float mask = (sin(uv.x * Scale_A + Panner_A * _Time.y) + 0.8) * (sin(uv.x * Scale_B + Panner_B * _Time.y) + 0.8)@$float4 col = texCol * mask * fresnel * dst@$$return col@;4;False;8;True;uv;FLOAT2;0,0;In;;Inherit;False;True;Scale_A;FLOAT;0;In;;Inherit;False;True;Scale_B;FLOAT;0;In;;Inherit;False;True;Panner_A;FLOAT;0;In;;Inherit;False;True;Panner_B;FLOAT;0;In;;Inherit;False;True;fresnel;FLOAT;0;In;;Inherit;False;True;dst;FLOAT;0;In;;Inherit;False;True;texCol;FLOAT4;0,0,0,0;In;;Inherit;False;My Custom Expression;True;False;0;8;0;FLOAT2;0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.ComponentMaskNode;17;733.2658,-149.0817;Inherit;False;False;False;False;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;3;1015.842,-327.8511;Float;False;True;-1;2;ASEMaterialInspector;0;0;Unlit;FI/OEP/GodRays;False;False;False;False;True;True;True;True;True;True;True;True;False;False;True;False;False;False;False;False;False;Off;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Custom;0.5;True;True;0;True;Custom;;Transparent;All;14;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;4;1;False;-1;1;False;-1;4;1;False;-1;1;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;0;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;15;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;22;0;21;0
WireConnection;22;1;23;0
WireConnection;20;0;22;0
WireConnection;24;0;20;0
WireConnection;24;1;8;0
WireConnection;27;0;24;0
WireConnection;18;0;4;0
WireConnection;18;1;5;0
WireConnection;7;3;6;0
WireConnection;26;0;27;0
WireConnection;13;0;19;0
WireConnection;13;1;9;0
WireConnection;13;2;10;0
WireConnection;13;3;11;0
WireConnection;13;4;12;0
WireConnection;13;5;7;0
WireConnection;13;6;26;0
WireConnection;13;7;18;0
WireConnection;17;0;13;0
WireConnection;3;2;13;0
WireConnection;3;9;17;0
ASEEND*/
//CHKSM=EDDFA71618A4DF6FA722CEE5A7D263E636067D03