Shader "Unlit/VoxelGIShader"
{
    Properties
    {
        [HDR]_BaseColor("Color",Color)=(1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        
//voxel GI        
//        _VoxelTex("VoxelTex",3D) = "black"{}
        _lowAABB("lowAABB",Vector) = (0,0,0,0)
        _highAABB("upAABB",Vector) = (0,0,0,0)
        _TrackThreshold("TrackThreshold",Range(0,1)) = 1
        _TrackMaxCount("TrackThresholdCount",Int) = 3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
                        // 设置关键字
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _AdditionalLights
            
            // 接收阴影所需关键字
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal:NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 normal:NORMAL;
                float4 color:COLOR;
                float4 vertex : SV_POSITION;
                float3 worldPos:TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
//Voxel GI-----------------------------------------
            sampler3D _VoxelTex;
            float4 _VoxelTex_ST;
            float4 _VoxelTex_TexelSize;
            
            float4 _lowAABB;
            float4 _highAABB;
            int _TrackMaxCount;
            float _TrackThreshold;
            float4 _BaseColor;
            
            ///求法向量 构建垂直正交基 后  求法向量和各个象限均分向量  
            void GetNormalFourSlopeDir(float3 normal, out float3 lu, out float3 ru, out float3 ld, out float3 rd)
            {
                float3 temp = abs(normal.y) < 0.99f ? float3(0,1,0) : float3(0,0,1);
                float3 tan = normalize(cross(temp, normal));
                float3 bi = normalize(cross(normal, tan));

                
                
                lu = normalize(-tan + bi+ normal);
                ru = normalize(tan + bi+ normal);
                ld = normalize(-tan - bi+ normal);
                rd = normalize(tan - bi+ normal);
            }
            
            float3 GetVoxelGI(float3 worldPos,float3 N)
            {
                //voxel
                float3 voxelUV = (worldPos - _lowAABB.xyz)/(_highAABB.xyz - _lowAABB.xyz);//从世界坐标映射到3D纹理坐标系
                float step = 1.0f/_VoxelTex_TexelSize.z;//获取到单次步长
                float3 detalColor = 0;
                float3 detailUV_N,detailUV_lu,detailUV_ru,detailUV_ld,detailUV_rd = 0;
                float3 lu,ru,ld,rd;
                GetNormalFourSlopeDir(N,lu,ru,ld,rd);
                for (int i = 0; i < _TrackMaxCount; ++i)
                {
                    float3 NstepDir = N * step;
                    detailUV_N += NstepDir * pow(2,i)*_TrackThreshold + NstepDir;
                    float3 RayUV_N = voxelUV + detailUV_N;
                    detalColor += tex3Dlod(_VoxelTex,float4(RayUV_N,i)).rgb;
                    
                    
                    float3 LUstepDir = normalize(lu)*step;//uv 空间
                    detailUV_lu += LUstepDir * pow(2,i)*_TrackThreshold + LUstepDir; // 这里是在法线方向上移动步长，进行追踪 +LUStepDir是为了不采样到自身的体素
                    float3 RayUV_lu = voxelUV + detailUV_lu;
                    detalColor += tex3Dlod(_VoxelTex,float4(RayUV_lu,i)).rgb * saturate(dot(N,lu));//这里采用的是 密集体素 + mipmap 的追踪的方法
                    
                    float3 RUstepDir = normalize(ru)*step;
                    detailUV_ru += RUstepDir * pow(2,i)*_TrackThreshold+RUstepDir; // 这里是在法线方向上移动步长 +RUStepDir是为了不采样到自身的体素
                    float3 RayUV_ru = voxelUV + detailUV_ru;
                    detalColor += tex3Dlod(_VoxelTex,float4(RayUV_ru,i)).rgb* saturate(dot(N,ru));//这里采用的是 密集体素 + mipmap 的追踪的方法
                    
                    float3 LDstepDir = normalize(ld)*step;
                    detailUV_ld += LDstepDir * pow(2,i)*_TrackThreshold+LDstepDir; // 这里是在法线方向上移动步长，进行追踪
                    float3 RayUV_ld = voxelUV + detailUV_ld;
                    detalColor += tex3Dlod(_VoxelTex,float4(RayUV_ld,i)).rgb* saturate(dot(N,ld));//这里采用的是 密集体素 + mipmap 的追踪的方法
                    
                    float3 RDstepDir = normalize(rd)*step;
                    detailUV_rd += RDstepDir* pow(2,i)*_TrackThreshold+RDstepDir; // 这里是在法线方向上移动步长，进行追踪
                    float3 RayUV_rd = voxelUV + detailUV_rd;
                    detalColor += tex3Dlod(_VoxelTex,float4(RayUV_rd,i)).rgb* saturate(dot(N,rd));//这里采用的是 密集体素 + mipmap 的追踪的方法
                }
                // detalColor/=4;
                return detalColor;
            }
//Voxel  GI
            v2f vert (appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld,v.vertex).xyz;

                o.vertex = mul(UNITY_MATRIX_VP,float4(worldPos,1));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float3 N = mul(v.normal,(float3x3)unity_WorldToObject);
                o.normal = normalize(N);
                o.worldPos = worldPos;
                o.color.rgb = 1;
                return o;
            }
            


            float4 frag (v2f i) : SV_Target
            {
            
                float3 N = normalize(i.normal);
                float3 L = normalize(_MainLightPosition);
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 LColor = _MainLightColor;
                float NdotL = saturate(dot(N,L));

                float3 ambient = SampleSH(N);
                
                float3 gi = GetVoxelGI(i.worldPos,N);
                
                float4 col = tex2D(_MainTex, i.uv)*_BaseColor;
                
                
                // 获取阴影坐标
                float4 shadowCoord = TransformWorldToShadowCoord(i.worldPos.xyz);
                
                // 计算主光源与阴影
                Light mainLight = GetMainLight(shadowCoord);

                
                
                
                
                float4 finalColor = 1;
                finalColor.rgb = col.rgb*NdotL*mainLight.shadowAttenuation*LColor
                    +col.rgb*gi
                    +col.rgb * ambient
                        ;

                return float4(finalColor.rgb,1);
            }
            ENDHLSL
        }
        
             // 计算阴影的Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Off
            ZWrite On
            ZTest LEqual
            
            HLSLPROGRAM
            
            // 设置关键字
            #pragma shader_feature _ALPHATEST_ON
            
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            
            float3 _LightDirection;
            
            struct Attributes
            {
                float4 positionOS: POSITION;
                float3 normalOS: NORMAL;
                float2 texcoord: TEXCOORD0;
            };
            
            struct Varyings
            {
                float2 uv: TEXCOORD0;
                float4 positionCS: SV_POSITION;
            };
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            // 获取裁剪空间下的阴影坐标
            float4 GetShadowPositionHClips(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                // 获取阴影专用裁剪空间下的坐标
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                
                // 判断是否是在DirectX平台翻转过坐标
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                
                return positionCS;
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.positionCS = GetShadowPositionHClips(input);
                return output;
            }
            
            
            half4 frag(Varyings input): SV_TARGET
            {
                //可以使用该方法替代下面的裁剪操作，但是需要引用函数库ShaderLibrary/SurfaceInput.hlsl
                //Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
                //or
                half4 albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                #if defined(_ALPHATEST_ON)
                    // clip(albedoAlpha.a - _Cutoff);
                #endif
                
                return 0;
            }
            
            ENDHLSL            
        }
    }
}
