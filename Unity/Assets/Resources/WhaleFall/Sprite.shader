// 搜打撤专用：直通 alpha 的无光照精灵着色器。
// 为什么要自己写：内置 Sprites/Default 是预乘 alpha（Blend One OneMinusSrcAlpha），
// 配普通 PNG 时透明区残留的白色 RGB 会被整片叠加，精灵周围出现浅色方块；
// 而 Unlit/Transparent 没有 _Color 属性，无法给地形块上色。
// 这个着色器同时要 _Color 和正确的 SrcAlpha 混合，放在 Resources 下以保证打包时被包含。
Shader "WhaleFall/Sprite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.texcoord) * i.color;
                clip(c.a - 0.002);
                return c;
            }
            ENDCG
        }
    }

    Fallback "Unlit/Transparent"
}
