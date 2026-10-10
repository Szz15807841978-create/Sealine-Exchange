Shader "WhaleFall/SunkenShipSails"
{
    Properties
    {
        _MainTex ("Cloth", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _OpacityMask ("Torn sail mask", 2D) = "white" {}
        _Cutoff ("Cutout", Range(0,1)) = 0.333
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alphatest:_Cutoff addshadow
        #pragma target 3.0
        sampler2D _MainTex, _BumpMap, _OpacityMask;
        struct Input { float2 uv_MainTex; float2 uv_BumpMap; float2 uv_OpacityMask; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo=tex2D(_MainTex,IN.uv_MainTex).rgb;
            o.Normal=UnpackNormal(tex2D(_BumpMap,IN.uv_BumpMap));
            o.Alpha=tex2D(_OpacityMask,IN.uv_OpacityMask).r;
            o.Metallic=0; o.Smoothness=0.12;
        }
        ENDCG
    }
    FallBack "Transparent/Cutout/Diffuse"
}
