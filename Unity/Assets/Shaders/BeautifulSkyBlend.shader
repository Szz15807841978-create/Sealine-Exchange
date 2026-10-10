Shader "WhaleFall/Beautiful Sky Blend"
{
    Properties
    {
        [NoScaleOffset] _SunnyTex ("Sunny Cubemap", Cube) = "grey" {}
        [NoScaleOffset] _RainyTex ("Rainy Cubemap", Cube) = "grey" {}
        _Blend ("Rain Amount", Range(0, 1)) = 0
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Exposure ("Exposure", Range(0, 4)) = 1
        _Rotation ("Rotation", Range(0, 360)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            samplerCUBE _SunnyTex;
            samplerCUBE _RainyTex;
            half _Blend;
            half4 _Tint;
            half _Exposure;
            float _Rotation;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };

            float3 RotateAroundY(float3 direction, float degrees)
            {
                float angle = radians(degrees);
                float sine = sin(angle);
                float cosine = cos(angle);
                return float3(cosine * direction.x - sine * direction.z, direction.y,
                    sine * direction.x + cosine * direction.z);
            }

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.direction = RotateAroundY(input.vertex.xyz, _Rotation);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                half3 sunny = texCUBE(_SunnyTex, input.direction).rgb;
                half3 rainy = texCUBE(_RainyTex, input.direction).rgb;
                return half4(lerp(sunny, rainy, saturate(_Blend)) * _Tint.rgb * _Exposure, 1);
            }
            ENDCG
        }
    }
}
