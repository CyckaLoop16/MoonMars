// Grim-dark placeholder lighting. Form shading is baked into the vertex colours; on top of that the
// station's own lamps (sodium floods, beacons, weld flashes) are applied from LightRig's global arrays.
// Vertex alpha marks emissive parts (lamp heads, screens, engine glow): they ignore lighting and are
// pushed above 1.0 so bloom catches them. No LightMode tag, so it renders in Built-in and URP alike.
Shader "Waystation/Lit Vertex Color"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            #define WS_MAX_LIGHTS 32

            float4 _WS_LightPos[WS_MAX_LIGHTS];   // xyz world position, w range
            float4 _WS_LightColor[WS_MAX_LIGHTS]; // rgb colour x intensity
            float _WS_LightCount;
            float4 _WS_Ambient;
            float _WS_Emission;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 light = _WS_Ambient.rgb;
                int count = (int)_WS_LightCount;

                for (int k = 0; k < WS_MAX_LIGHTS; k++)
                {
                    if (k >= count) break;
                    float3 toLight = _WS_LightPos[k].xyz - i.worldPos;
                    float dist = length(toLight);
                    float falloff = saturate(1.0 - dist / max(_WS_LightPos[k].w, 0.001));
                    float facing = saturate(dot(n, toLight / max(dist, 0.0001)) * 0.7 + 0.3);
                    light += _WS_LightColor[k].rgb * falloff * falloff * facing;
                }

                float emissive = i.color.a;
                float3 col = i.color.rgb * (light * (1.0 - emissive) + emissive * _WS_Emission);
                return float4(col, 1.0);
            }
            ENDCG
        }
    }
}
