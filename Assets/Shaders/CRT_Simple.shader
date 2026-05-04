Shader "Hidden/CRT_Simple"
{
    Properties{
        _MainTex ("Source", 2D) = "white" {}
        _PixelSize ("Pixel Size (1–4)", Float) = 2
        _ScanlineIntensity ("Scanline Intensity", Range(0,1)) = 0.25
        _Curvature ("Curvature", Range(0,0.5)) = 0.08
        _Vignette ("Vignette", Range(0,1)) = 0.2
        _NoiseAmount ("Noise", Range(0,0.1)) = 0.02
    }
    SubShader{
        Tags{ "RenderType"="Opaque" "Queue"="Overlay" }
        Cull Off ZWrite Off ZTest Always
        Pass{
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _PixelSize, _ScanlineIntensity, _Curvature, _Vignette, _NoiseAmount;
            float _TimeParameters; // not used; _Time.y available via Unity

            float hash21(float2 p){
                p = frac(p*float2(123.34, 345.45));
                p += dot(p, p+34.345);
                return frac(p.x*p.y);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;

                // Barrel warp
                float2 centered = uv*2 - 1;
                float r2 = dot(centered, centered);
                float k = _Curvature;
                float2 warped = centered * (1 + k*r2);
                uv = (warped + 1) * 0.5;

                // Clamp to avoid sampling outside after warp
                uv = clamp(uv, 0.0, 1.0);

                // Pixelate (based on target texel size)
                float2 px = _MainTex_TexelSize.zw / max(_PixelSize, 1.0);
                uv = floor(uv * px) / px;

                fixed4 col = tex2D(_MainTex, uv);

                // Scanlines (row modulation)
                float scan = 1.0 - _ScanlineIntensity * (0.5 + 0.5 * sin(uv.y * _MainTex_TexelSize.w * 3.14159));
                col.rgb *= scan;

                // Vignette from warped space
                float d = length(warped);
                float vig = smoothstep(1.0, 1.0 - _Vignette, d);
                col.rgb *= (1.0 - vig * _Vignette);

                // Subtle temporal noise
                float n = (hash21(uv * _MainTex_TexelSize.zw + _Time.y) - 0.5) * 2.0 * _NoiseAmount;
                col.rgb += n;

                return col;
            }
            ENDCG
        }
    }
}
