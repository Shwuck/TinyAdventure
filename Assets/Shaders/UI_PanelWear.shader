Shader "UI/Panel Wear"
{
    Properties{
        _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _EdgeDark ("Edge Darken", Range(0,1)) = 0.25
        _CornerVignette ("Corner Vignette", Range(0,1)) = 0.35
        _Radius ("Soft Corner Radius", Range(0,1)) = 0.15
    }
    SubShader{
        Tags{ "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off ZTest [unity_GUIZTestMode] Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass{
            Name "UIPanelWear"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t { float4 vertex:POSITION; float2 texcoord:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color;
            float _EdgeDark, _CornerVignette, _Radius;

            v2f vert (appdata_t v){
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target{
                fixed4 baseCol = tex2D(_MainTex, i.uv) * i.color;

                // Edge darken
                float2 d = abs(i.uv * 2 - 1);               // distance to center (0 at center, 1 at edges)
                float edge = max(d.x, d.y);                 // 0 center -> 1 edges
                float edgeFactor = 1.0 - _EdgeDark * smoothstep(0.6, 1.0, edge);

                // Corner vignette with soft radius
                float2 c = i.uv * (1 - i.uv);              // 0 at edges, max ~0.25 at center
                float corner = 1.0 - (c.x * c.y * 16);     // 1 at edges/corners, ~0 at center
                float rounded = smoothstep(_Radius, 1.0, 1.0 - max(d.x, d.y));
                float cornerFactor = 1.0 - _CornerVignette * (corner * (1.0 - rounded));

                baseCol.rgb *= edgeFactor * cornerFactor;
                return baseCol;
            }
            ENDCG
        }
    }
    FallBack "UI/Default"
}
