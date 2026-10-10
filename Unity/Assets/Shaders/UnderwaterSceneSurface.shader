Shader "WhaleFall/UnderwaterSceneSurface"
{
    Properties { _Color("Water tint", Color) = (.08,.35,.42,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct v2f { float4 pos:SV_POSITION; float3 world:TEXCOORD0; UNITY_FOG_COORDS(1) };
            v2f vert(appdata_base v) { v2f o; o.world=mul(unity_ObjectToWorld,v.vertex).xyz; o.pos=UnityObjectToClipPos(v.vertex); UNITY_TRANSFER_FOG(o,o.pos); return o; }
            fixed4 frag(v2f i):SV_Target
            {
                float a=sin(i.world.x*.7+sin(i.world.z*.4+_Time.y*.6)*2);
                float b=sin(i.world.z*.9+sin(i.world.x*.3-_Time.y*.4)*2);
                float ripple=pow(saturate(1-abs(a+b)*.7),5);
                fixed4 c=fixed4(_Color.rgb*(.86+ripple*.16+a*.025),1);
                UNITY_APPLY_FOG(i.fogCoord,c); return c;
            }
            ENDCG
        }
    }
}
