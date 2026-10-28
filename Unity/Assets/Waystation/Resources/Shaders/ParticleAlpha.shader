// Soft particles (smoke, venting vapour): alpha-blended.
// Round soft sprites with no texture: the falloff is computed from the particle quad's UVs.
Shader "Waystation/Particle Alpha"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 d = i.uv * 2.0 - 1.0;
                float a = saturate(1.0 - dot(d, d));
                a *= a;
                return float4(i.color.rgb, i.color.a * a);
            }
            ENDCG
        }
    }
}
