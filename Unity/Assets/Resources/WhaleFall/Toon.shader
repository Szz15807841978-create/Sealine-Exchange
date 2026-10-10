// 搜打撤的卡通着色器：把硬边的高光/明暗压成 2~3 档，并带一圈轮廓光。
// 内置 Standard 是物理光照，落在低多边形的写实模型上会出现大片死黑和生硬高光；
// 这个着色器用 half-lambert（环绕光照）+ 色阶量化 + 暗部染色，观感更接近平涂。
// 放在 Resources 下以保证打包时被包含。
Shader "WhaleFall/Toon"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _ShadowColor ("Shadow Tint", Color) = (0.36,0.48,0.62,1)
        _Steps ("Light Steps", Range(1,5)) = 3
        _Wrap ("Light Wrap", Range(0,1)) = 0.6
        _RimColor ("Rim Color", Color) = (0.72,0.83,0.82,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3.5
        _RimStrength ("Rim Strength", Range(0,1)) = 0.3
        _Ambient ("Ambient Boost", Range(0,1)) = 0.28
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _BaseColor;
            fixed4 _ShadowColor;
            fixed4 _RimColor;
            float _Steps;
            float _Wrap;
            float _RimPower;
            float _RimStrength;
            float _Ambient;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(UnityWorldSpaceViewDir(worldPos));
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);

                // half-lambert：把明暗交界摊开，避免大片死黑
                float raw = dot(n, l);
                float wrapped = raw * 0.5 + 0.5;
                float ndl = lerp(raw, wrapped, _Wrap);
                ndl = saturate(ndl);

                // 色阶量化：把连续明暗压成 _Steps 档
                float steps = max(1.0, _Steps);
                float q = floor(ndl * steps) / max(0.0001, steps - 1.0);
                q = saturate(q);
                // 档位之间做一点点过渡，避免锯齿闪烁
                q = smoothstep(0.0, 0.06, q) * q + (1.0 - smoothstep(0.0, 0.06, q)) * q;

                fixed4 tex = tex2D(_MainTex, i.uv);
                float3 albedo = tex.rgb * _BaseColor.rgb;

                // 暗部往冷色偏，亮部用主光色
                float3 ambient = unity_AmbientSky.rgb * _Ambient;
                float3 shadowed = albedo * _ShadowColor.rgb + albedo * ambient;
                float3 lit = albedo * (_LightColor0.rgb * q + ambient);
                float3 col = lerp(shadowed, lit, q);

                // 轮廓光：让剪影从背景里跳出来
                float rim = pow(1.0 - saturate(dot(n, normalize(i.viewDir))), _RimPower);
                col += _RimColor.rgb * rim * _RimStrength;

                fixed4 outc = fixed4(col, _BaseColor.a * tex.a);
                UNITY_APPLY_FOG(i.fogCoord, outc);
                return outc;
            }
            ENDCG
        }

        // 附加光：同样量化，避免点光把表面打出死白
        Pass
        {
            Tags { "LightMode" = "ForwardAdd" }
            Blend One One
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _BaseColor;
            fixed4 _ShadowColor;
            float _Steps;
            float _Wrap;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 worldNormal : TEXCOORD1; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 l = normalize(_WorldSpaceLightPos0.xyz - i.pos.xyz * 0.0);
                float raw = dot(n, l);
                float ndl = saturate(lerp(raw, raw * 0.5 + 0.5, _Wrap));
                float steps = max(1.0, _Steps);
                float q = saturate(floor(ndl * steps) / max(0.0001, steps - 1.0));
                float3 albedo = tex2D(_MainTex, i.uv).rgb * _BaseColor.rgb;
                return fixed4(albedo * _LightColor0.rgb * q * 0.7, 0) * tex2D(_MainTex, i.uv).a;
            }
            ENDCG
        }
    }

    Fallback "Diffuse"
}
