Shader "SDFShadow/SimpleSDF3D"
{
    Properties
    {
        _SDFTex ("SDF Texture3D", 3D) = "black" {}
        _Range ("Show Range", Float) = 0.03
        _StepSize ("Step Size", Float) = 0.01
        _MaxSteps ("Max Steps", Float) = 128
        _Color ("Color", Color) = (0, 0.8, 1, 0.8)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler3D _SDFTex;
            float _Range;
            float _StepSize;
            float _MaxSteps;
            float4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 localPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.localPos = v.vertex.xyz;
                return o;
            }

            bool Inside01(float3 uvw)
            {
                return all(uvw >= 0.0) && all(uvw <= 1.0);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 cameraLocal = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                float3 rayDir = normalize(i.localPos - cameraLocal);
                
                float3 p = i.localPos;

                for (int s = 0; s < 32; s++)
                {
                    if (s >= _MaxSteps)
                        break;

                    float3 uvw = p + 0.5;

                    if (!Inside01(uvw))
                        break;

                    float sdf = tex3D(_SDFTex, uvw).r;

                    if (abs(sdf) <= _Range)
                        return _Color;

                    p += rayDir * _StepSize;
                }

                discard;
                return 0;
            }
            ENDCG
        }
    }
}