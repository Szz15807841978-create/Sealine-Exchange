Shader "Whalefall/ChestFlat"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.normal=UnityObjectToWorldNormal(v.normal); return o; }
            fixed4 frag(v2f i):SV_Target
            {
                float light = dot(normalize(i.normal), normalize(float3(-.5,1,-.7)));
                float shade = light > .65 ? 1 : (light > .05 ? .84 : .61);
                return fixed4(_Color.rgb*shade,1);
            }
            ENDCG
        }
    }
}
