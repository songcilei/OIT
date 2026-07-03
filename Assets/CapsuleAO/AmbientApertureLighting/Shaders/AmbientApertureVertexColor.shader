Shader "Custom/Ambient Aperture Vertex Color"
{
    Properties
    {
        _Color ("Albedo", Color) = (1,1,1,1)
        _MainTex ("Albedo Texture", 2D) = "white" {}
        _ApertureLightRadius ("Light Angular Radius", Range(0.001, 3.14159)) = 0.12
        _SkyAmbientColor ("Sky Ambient", Color) = (0.28,0.38,0.55,1)
        _DirectStrength ("Direct Strength", Range(0, 8)) = 1
        _AmbientStrength ("Ambient Strength", Range(0, 8)) = 1
        _UseExactIntersection ("Use Exact Intersection", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AmbientApertureLighting.hlsl"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _ApertureLightRadius;
            float4 _SkyAmbientColor;
            float _DirectStrength;
            float _AmbientStrength;
            float _UseExactIntersection;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWorld : TEXCOORD1;
                float3 apertureWorld : TEXCOORD2;
                float apertureRadius : TEXCOORD3;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normalWorld = UnityObjectToWorldNormal(v.normal);

                float3 apertureLocal = v.color.rgb * 2.0 - 1.0;
                o.apertureWorld = UnityObjectToWorldNormal(apertureLocal);
                o.apertureRadius = saturate(v.color.a) * AAL_PI;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 normalWorld = normalize(i.normalWorld);
                float3 apertureWorld = normalize(i.apertureWorld);
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);

                float directVisibility;
                float ambientVisibility;
                float lambert;
                AAL_Evaluate(
                    normalWorld,
                    apertureWorld,
                    i.apertureRadius,
                    lightDirection,
                    _ApertureLightRadius,
                    _UseExactIntersection,
                    directVisibility,
                    ambientVisibility,
                    lambert);

                float3 albedo = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                float3 direct = _LightColor0.rgb * directVisibility * lambert * _DirectStrength;
                float3 ambient = _SkyAmbientColor.rgb * ambientVisibility * _AmbientStrength;
                return float4(albedo * (direct + ambient), 1.0);
            }
            ENDCG
        }
    }
}
