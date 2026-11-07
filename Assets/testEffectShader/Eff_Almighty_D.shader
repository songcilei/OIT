Shader "Effect_D/Almighty_D" {
	Properties {
		[HideInInspector] _EmissionColor ("Emission Color", Color) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff ("Alpha Cutoff ", Range(0, 1)) = 0.5
		[ASEBegin] [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
		[Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
		[Enum(UnityEngine.Rendering.BlendMode)] _Src ("Src", Float) = 5
		[Enum(UnityEngine.Rendering.BlendMode)] _Dst ("Dst", Float) = 1
		[Enum(R,1,A,0)] _Alpha_NO_R ("Alpha_NO_R", Float) = 0
		[HDR] _Main_Color ("Main_Color", Color) = (1,1,1,1)
		[Header(Main_Tex)] _Main_Tex ("Main_Tex", 2D) = "white" {}
		[Enum(Off,0,On,1)] _Particle_SpeedUV ("Particle_Speed UV", Float) = 0
		_Brightness ("Brightness", Float) = 1
		_Alpha ("Alpha", Float) = 1
		_Main_UV ("Main_UV", Vector) = (0,0,0,0)
		[Header(Mask_Tex)] _Mask_Tex ("Mask_Tex", 2D) = "white" {}
		[Toggle(_MASK_TEX_NO_ON)] _Mask_Tex_NO ("Mask_Tex_NO", Float) = 0
		[Enum(R,0,A,1)] _Mask_A_NO_R ("Mask_A_NO_R", Float) = 0
		_Mask_Tex_Rotator ("Mask_Tex_Rotator", Range(0, 360)) = 0
		_Mask_UV ("Mask_UV", Vector) = (0,0,0,0)
		[Header(Remap_Tex)] _Remap_Tex ("Remap_Tex", 2D) = "white" {}
		[Toggle(_REMAP_NO_ON)] _Remap_NO ("Remap_NO", Float) = 0
		[Enum(R,1,A,0)] _Remap_A_NO_R ("Remap_A_NO_R", Float) = 0
		_Remap_UV ("Remap_UV", Vector) = (0,0,0,0)
		[Header(Diss_Tex)] _Diss_Tex ("Diss_Tex", 2D) = "white" {}
		[Toggle(_DISS_TEX_NO_ON)] _Diss_Tex_NO ("Diss_Tex_NO", Float) = 0
		[Enum(Off,0,On,1)] _Diss_Particle_ON ("Diss_Particle_ON", Float) = 0
		_Diss_value ("Diss_value", Float) = 0
		_Diss_Soft_value ("Diss_Soft_value", Float) = 0
		[Enum(Off,0,On,1)] _Diss_Edge_NO ("Diss_Edge_NO", Float) = 0
		[HDR] _Diss_Edge_color ("Diss_Edge_color", Color) = (1,1,1,1)
		_EdgeWidth ("EdgeWidth", Float) = 0
		_Diss_UV ("Diss_UV", Vector) = (0,0,0,0)
		[Header(Turb_Noise)] _Turb_Noise ("Turb_Noise", 2D) = "white" {}
		[Toggle(_TURB_TEX_ON_ON)] _Turb_Tex_ON ("Turb_Tex_ON", Float) = 0
		_Turb_MainTex_Value ("Turb_MainTex_Value", Float) = 0
		_Turb_Diss_Value ("Turb_Diss_Value", Float) = 0
		_Turb_Remap_Value ("Turb_Remap_Value", Float) = 0
		_Turb_UV ("Turb_UV", Vector) = (0,0,0,0)
		[Header(Vertex_offset)] _Vertex_offset ("Vertex_offset", 2D) = "white" {}
		[Toggle(_VER_TEX_NO_ON)] _Ver_Tex_NO ("Ver_Tex_NO", Float) = 0
		_VertexOffset_Value ("VertexOffset_Value", Vector) = (0,0,0,0)
		[ASEEnd] _VertexOffset_UV ("VertexOffset_UV", Vector) = (0,0,0,0)
	}
// Restored from original Android UnityFS compiled GLES3 programs.
// Native expression order and feature keyword branches retained; built-in pipeline matrix/instancing adapter.
// See work/fx_alignment_repair/original_evidence_d/index.json and d_family_restoration_report.json.
SubShader {
Tags { "QUEUE"="Transparent+5" "RenderType"="Transparent" }
Cull [_Cull]
ZTest [_ZTest]
ZWrite Off
Blend SrcAlpha [_Dst], One OneMinusSrcAlpha
Pass {
Name "Forward"
Tags { "LightMode"="ForwardBase" }
CGPROGRAM
#pragma target 3.0
#pragma vertex SourceVert
#pragma fragment SourceFrag
#pragma multi_compile_instancing
#pragma multi_compile_local __ _DISS_TEX_NO_ON
#pragma multi_compile_local __ _MASK_TEX_NO_ON
#pragma multi_compile_local __ _REMAP_NO_ON
#pragma multi_compile_local __ _TURB_TEX_ON_ON
#pragma multi_compile_local __ _VER_TEX_NO_ON
#include "UnityCG.cginc"
float4 _EmissionColor;
float _AlphaCutoff;
float _ZTest;
float _Cull;
float _Src;
float _Dst;
float _Alpha_NO_R;
float4 _Main_Color;
sampler2D _Main_Tex;
float4 _Main_Tex_ST;
float _Particle_SpeedUV;
float _Brightness;
float _Alpha;
float4 _Main_UV;
sampler2D _Mask_Tex;
float4 _Mask_Tex_ST;
float _Mask_Tex_NO;
float _Mask_A_NO_R;
float _Mask_Tex_Rotator;
float4 _Mask_UV;
sampler2D _Remap_Tex;
float4 _Remap_Tex_ST;
float _Remap_NO;
float _Remap_A_NO_R;
float4 _Remap_UV;
sampler2D _Diss_Tex;
float4 _Diss_Tex_ST;
float _Diss_Tex_NO;
float _Diss_Particle_ON;
float _Diss_value;
float _Diss_Soft_value;
float _Diss_Edge_NO;
float4 _Diss_Edge_color;
float _EdgeWidth;
float4 _Diss_UV;
sampler2D _Turb_Noise;
float4 _Turb_Noise_ST;
float _Turb_Tex_ON;
float _Turb_MainTex_Value;
float _Turb_Diss_Value;
float _Turb_Remap_Value;
float4 _Turb_UV;
sampler2D _Vertex_offset;
float4 _Vertex_offset_ST;
float _Ver_Tex_NO;
float4 _VertexOffset_Value;
float4 _VertexOffset_UV;

// GLES constructor overloads retain scalar splats and same-size vector casts.
float2 SrcVec2(float a) { return a.xx; }
float2 SrcVec2(float2 a) { return a; }
float2 SrcVec2(float a, float b) { return float2(a,b); }
float3 SrcVec3(float a) { return a.xxx; }
float3 SrcVec3(float3 a) { return a; }
float3 SrcVec3(float a,float b,float c) { return float3(a,b,c); }
float3 SrcVec3(float2 a,float b) { return float3(a,b); }
float3 SrcVec3(float a,float2 b) { return float3(a,b); }
float4 SrcVec4(float a) { return a.xxxx; }
float4 SrcVec4(float4 a) { return a; }
float4 SrcVec4(float a,float b,float c,float d) { return float4(a,b,c,d); }
float4 SrcVec4(float2 a,float2 b) { return float4(a,b); }
float4 SrcVec4(float3 a,float b) { return float4(a,b); }
float4 SrcVec4(float a,float3 b) { return float4(a,b); }
float4 SrcVec4(float2 a,float b,float c) { return float4(a,b,c); }
float4 SrcVec4(float a,float2 b,float c) { return float4(a,b,c); }
float4 SrcVec4(float a,float b,float2 c) { return float4(a,b,c); }

struct SourceInput {
 float4 vertex : POSITION;
 float3 normal : NORMAL;
 float4 color : COLOR;
 float4 uv : TEXCOORD0;
 float4 custom : TEXCOORD1;
 UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct SourceVarying {
 float4 pos : SV_POSITION;
 float4 uv : TEXCOORD0;
 float4 custom : TEXCOORD1;
 float4 color : COLOR;
 float2 maskUV : TEXCOORD2;
 float4 clipMask : TEXCOORD3;
};
// The original rotates about UV origin, clockwise, after ST and scrolling.
float2 SourceMaskRotate(float2 p,float degrees) {
 float a=degrees*0.0174532942;
 float s=sin(a),c=cos(a);
 return float2(dot(p,float2(c,s)),dot(p,float2(-s,c)));
}
SourceVarying SourceVert(SourceInput v) {
 UNITY_SETUP_INSTANCE_ID(v);
 SourceVarying o;
#if defined(_VER_TEX_NO_ON)
 float2 vertexUV=v.uv.xy*_Vertex_offset_ST.xy+_Vertex_offset_ST.zw+_Time.y*_VertexOffset_UV.xy;
 v.vertex.xyz+=tex2Dlod(_Vertex_offset,float4(vertexUV,0,0)).rgb*v.normal*_VertexOffset_Value.xyz;
#endif
 o.pos=UnityObjectToClipPos(v.vertex);
 o.uv=v.uv;
 o.custom=v.custom;
 o.color=v.color;
 o.maskUV=0;
 o.clipMask=0;
#if defined(_MASK_TEX_NO_ON)
 o.maskUV=SourceMaskRotate(v.uv.xy*_Mask_Tex_ST.xy+_Mask_Tex_ST.zw+_Time.y*_Mask_UV.xy,_Mask_Tex_Rotator);
#endif
 return o;
}
float4 SourceFrag(SourceVarying i) : SV_Target {
 float4 SV_Target0;
 float4 vs_TEXCOORD0=i.uv;
 float4 vs_TEXCOORD1=i.custom;
 float4 vs_COLOR0=i.color;
 float2 vs_TEXCOORD2=i.maskUV;
#if !defined(_DISS_TEX_NO_ON) && !defined(_MASK_TEX_NO_ON) && !defined(_REMAP_NO_ON) && !defined(_TURB_TEX_ON_ON) && !defined(_VER_TEX_NO_ON)
 // Native GLES3 64; SHA256 01af7c79924ba9ce9f2e9e7900b93e35f9aeec903ff6e213068e618df636cc17
float2 u_xlat0;
float4 u_xlat16_0;
float4 u_xlat16_1;
float3 u_xlat16_3;
float2 u_xlat4;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat4.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat4.xy = (-u_xlat0.xy) + u_xlat4.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat4.xy + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_1.x = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_1.x = _Alpha_NO_R * u_xlat16_1.x + u_xlat16_0.w;
    u_xlat16_3.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_1.yzw = u_xlat16_3.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_1.x = u_xlat16_1.x * _Main_Color.w;
    u_xlat16_1.x = u_xlat16_1.x * _Alpha;
    SV_Target0 = u_xlat16_1.yzwx * vs_COLOR0;
    return SV_Target0;
#elif !defined(_DISS_TEX_NO_ON) && defined(_MASK_TEX_NO_ON) && !defined(_REMAP_NO_ON) && !defined(_TURB_TEX_ON_ON) && !defined(_VER_TEX_NO_ON)
 // Native GLES3 65; SHA256 fdc5e88c67c1ee58550c906cfed7e9db2a677184134a46ed9a3ec893c6c0807b
float2 u_xlat0;
float4 u_xlat16_0;
float u_xlat16_1;
float3 u_xlat16_3;
float2 u_xlat4;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat4.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat4.xy = (-u_xlat0.xy) + u_xlat4.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat4.xy + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_1 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_1 = _Alpha_NO_R * u_xlat16_1 + u_xlat16_0.w;
    u_xlat16_3.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_3.xyz = u_xlat16_3.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    SV_Target0.xyz = u_xlat16_3.xyz * vs_COLOR0.xyz;
    u_xlat16_1 = u_xlat16_1 * _Main_Color.w;
    u_xlat16_1 = u_xlat16_1 * _Alpha;
    u_xlat16_0.xy = tex2D(_Mask_Tex, vs_TEXCOORD2.xy).xw;
    u_xlat16_3.x = (-u_xlat16_0.x) + u_xlat16_0.y;
    u_xlat16_3.x = _Mask_A_NO_R * u_xlat16_3.x + u_xlat16_0.x;
    u_xlat16_1 = u_xlat16_3.x * u_xlat16_1;
    SV_Target0.w = u_xlat16_1 * vs_COLOR0.w;
    return SV_Target0;
#elif defined(_DISS_TEX_NO_ON) && !defined(_MASK_TEX_NO_ON) && !defined(_REMAP_NO_ON) && !defined(_TURB_TEX_ON_ON) && !defined(_VER_TEX_NO_ON)
 // Native GLES3 66; SHA256 22082bc7335b1c9cf5e79364461646290f08b885dc9c5c77b3a7e6a465098557
float2 u_xlat0;
float4 u_xlat16_0;
float4 u_xlat16_1;
float3 u_xlat16_2;
float u_xlat3;
float u_xlat16_4;
float2 u_xlat6;
float u_xlat16_7;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Diss_Tex_ST.xy + _Diss_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * _Diss_UV.xy + u_xlat0.xy;
    u_xlat16_0.x = tex2D(_Diss_Tex, u_xlat0.xy).x;
    u_xlat0.x = u_xlat16_0.x + 1.0;
    u_xlat3 = vs_TEXCOORD1.z + (-_Diss_value);
    u_xlat3 = _Diss_Particle_ON * u_xlat3 + _Diss_value;
    u_xlat16_1.xy = (-SrcVec2(SrcVec2(_Diss_Soft_value, _Diss_Soft_value))) + SrcVec2(1.0, 2.0);
    u_xlat0.x = (-u_xlat3) * u_xlat16_1.y + u_xlat0.x;
    u_xlat16_1.x = float(1.0) / u_xlat16_1.x;
    u_xlat16_4 = u_xlat0.x + (-_Diss_Soft_value);
    u_xlat16_7 = u_xlat0.x + _EdgeWidth;
    u_xlat16_7 = u_xlat16_7 + (-_Diss_Soft_value);
    u_xlat16_7 = u_xlat16_1.x * u_xlat16_7;
    u_xlat16_7 = clamp(u_xlat16_7, 0.0, 1.0);
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_4;
    u_xlat16_1.x = clamp(u_xlat16_1.x, 0.0, 1.0);
    u_xlat16_4 = u_xlat16_1.x * -2.0 + 3.0;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_1.x;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_4;
    u_xlat16_4 = u_xlat16_7 * -2.0 + 3.0;
    u_xlat16_7 = u_xlat16_7 * u_xlat16_7;
    u_xlat16_4 = u_xlat16_4 * u_xlat16_7 + (-u_xlat16_1.x);
    u_xlat16_1.x = _Diss_Edge_NO * u_xlat16_4 + u_xlat16_1.x;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat6.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat6.xy = (-u_xlat0.xy) + u_xlat6.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat6.xy + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_7 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_7 = _Alpha_NO_R * u_xlat16_7 + u_xlat16_0.w;
    u_xlat16_2.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_2.xyz = u_xlat16_2.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_7;
    u_xlat16_1.x = u_xlat16_1.x * _Main_Color.w;
    u_xlat16_1.x = u_xlat16_1.x * _Alpha;
    SV_Target0.w = u_xlat16_1.x * vs_COLOR0.w;
    u_xlat16_1.xzw = u_xlat16_2.xyz * vs_COLOR0.xyz;
    u_xlat16_2.xyz = (-u_xlat16_2.xyz) * vs_COLOR0.xyz + _Diss_Edge_color.xyz;
    SV_Target0.xyz = SrcVec3(u_xlat16_4) * u_xlat16_2.xyz + u_xlat16_1.xzw;
    return SV_Target0;
#elif defined(_DISS_TEX_NO_ON) && defined(_MASK_TEX_NO_ON) && !defined(_REMAP_NO_ON) && !defined(_TURB_TEX_ON_ON) && !defined(_VER_TEX_NO_ON)
 // Native GLES3 67; SHA256 cc8f658216210961d7704a7f26410f007621076a968a2ec451e1b76092f6e9f9
float2 u_xlat0;
float4 u_xlat16_0;
float4 u_xlat16_1;
float3 u_xlat16_2;
float u_xlat3;
float u_xlat16_4;
float2 u_xlat6;
float u_xlat16_7;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Diss_Tex_ST.xy + _Diss_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * _Diss_UV.xy + u_xlat0.xy;
    u_xlat16_0.x = tex2D(_Diss_Tex, u_xlat0.xy).x;
    u_xlat0.x = u_xlat16_0.x + 1.0;
    u_xlat3 = vs_TEXCOORD1.z + (-_Diss_value);
    u_xlat3 = _Diss_Particle_ON * u_xlat3 + _Diss_value;
    u_xlat16_1.xy = (-SrcVec2(SrcVec2(_Diss_Soft_value, _Diss_Soft_value))) + SrcVec2(1.0, 2.0);
    u_xlat0.x = (-u_xlat3) * u_xlat16_1.y + u_xlat0.x;
    u_xlat16_1.x = float(1.0) / u_xlat16_1.x;
    u_xlat16_4 = u_xlat0.x + (-_Diss_Soft_value);
    u_xlat16_7 = u_xlat0.x + _EdgeWidth;
    u_xlat16_7 = u_xlat16_7 + (-_Diss_Soft_value);
    u_xlat16_7 = u_xlat16_1.x * u_xlat16_7;
    u_xlat16_7 = clamp(u_xlat16_7, 0.0, 1.0);
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_4;
    u_xlat16_1.x = clamp(u_xlat16_1.x, 0.0, 1.0);
    u_xlat16_4 = u_xlat16_1.x * -2.0 + 3.0;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_1.x;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_4;
    u_xlat16_4 = u_xlat16_7 * -2.0 + 3.0;
    u_xlat16_7 = u_xlat16_7 * u_xlat16_7;
    u_xlat16_4 = u_xlat16_4 * u_xlat16_7 + (-u_xlat16_1.x);
    u_xlat16_1.x = _Diss_Edge_NO * u_xlat16_4 + u_xlat16_1.x;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat6.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat6.xy = (-u_xlat0.xy) + u_xlat6.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat6.xy + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_7 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_7 = _Alpha_NO_R * u_xlat16_7 + u_xlat16_0.w;
    u_xlat16_2.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_2.xyz = u_xlat16_2.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_7;
    u_xlat16_1.x = u_xlat16_1.x * _Main_Color.w;
    u_xlat16_1.x = u_xlat16_1.x * _Alpha;
    u_xlat16_0.xy = tex2D(_Mask_Tex, vs_TEXCOORD2.xy).xw;
    u_xlat16_7 = (-u_xlat16_0.x) + u_xlat16_0.y;
    u_xlat16_7 = _Mask_A_NO_R * u_xlat16_7 + u_xlat16_0.x;
    u_xlat16_1.x = u_xlat16_7 * u_xlat16_1.x;
    SV_Target0.w = u_xlat16_1.x * vs_COLOR0.w;
    u_xlat16_1.xzw = u_xlat16_2.xyz * vs_COLOR0.xyz;
    u_xlat16_2.xyz = (-u_xlat16_2.xyz) * vs_COLOR0.xyz + _Diss_Edge_color.xyz;
    SV_Target0.xyz = SrcVec3(u_xlat16_4) * u_xlat16_2.xyz + u_xlat16_1.xzw;
    return SV_Target0;
#elif !defined(_DISS_TEX_NO_ON) && !defined(_MASK_TEX_NO_ON) && defined(_REMAP_NO_ON) && !defined(_TURB_TEX_ON_ON) && !defined(_VER_TEX_NO_ON)
 // Native GLES3 68; SHA256 fb6dadcfe1dae9c1d10936f6e06ff64054792668a4c3e860b88520c443f8500d
float2 u_xlat0;
float4 u_xlat16_0;
float u_xlat16_1;
float u_xlat16_2;
float3 u_xlat16_4;
float2 u_xlat6;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat6.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat6.xy = (-u_xlat0.xy) + u_xlat6.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat6.xy + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_1 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_1 = _Alpha_NO_R * u_xlat16_1 + u_xlat16_0.w;
    u_xlat16_4.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_4.xyz = u_xlat16_4.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_4.xyz = u_xlat16_4.xyz * vs_COLOR0.xyz;
    u_xlat16_1 = u_xlat16_1 * _Main_Color.w;
    u_xlat16_1 = u_xlat16_1 * _Alpha;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Remap_Tex_ST.xy + _Remap_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * SrcVec2(_Remap_UV.x, _Remap_UV.y) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Remap_Tex, u_xlat0.xy);
    u_xlat16_2 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_2 = _Remap_A_NO_R * u_xlat16_2 + u_xlat16_0.w;
    SV_Target0.xyz = u_xlat16_0.xyz * u_xlat16_4.xyz;
    u_xlat16_1 = u_xlat16_1 * u_xlat16_2;
    SV_Target0.w = u_xlat16_1 * vs_COLOR0.w;
    return SV_Target0;
#elif !defined(_DISS_TEX_NO_ON) && defined(_MASK_TEX_NO_ON) && defined(_REMAP_NO_ON) && !defined(_TURB_TEX_ON_ON) && !defined(_VER_TEX_NO_ON)
 // Native GLES3 69; SHA256 db227bc9c4919aa9bcef385055499f6eb2ce37b917e2fc3fb28071e83cfe8232
float2 u_xlat0;
float4 u_xlat16_0;
float u_xlat16_1;
float u_xlat16_2;
float3 u_xlat16_4;
float2 u_xlat6;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat6.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat6.xy = (-u_xlat0.xy) + u_xlat6.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat6.xy + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_1 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_1 = _Alpha_NO_R * u_xlat16_1 + u_xlat16_0.w;
    u_xlat16_4.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_4.xyz = u_xlat16_4.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_4.xyz = u_xlat16_4.xyz * vs_COLOR0.xyz;
    u_xlat16_1 = u_xlat16_1 * _Main_Color.w;
    u_xlat16_1 = u_xlat16_1 * _Alpha;
    u_xlat16_0.xy = tex2D(_Mask_Tex, vs_TEXCOORD2.xy).xw;
    u_xlat16_2 = (-u_xlat16_0.x) + u_xlat16_0.y;
    u_xlat16_2 = _Mask_A_NO_R * u_xlat16_2 + u_xlat16_0.x;
    u_xlat16_1 = u_xlat16_1 * u_xlat16_2;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Remap_Tex_ST.xy + _Remap_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * SrcVec2(_Remap_UV.x, _Remap_UV.y) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Remap_Tex, u_xlat0.xy);
    u_xlat16_2 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_2 = _Remap_A_NO_R * u_xlat16_2 + u_xlat16_0.w;
    SV_Target0.xyz = u_xlat16_0.xyz * u_xlat16_4.xyz;
    u_xlat16_1 = u_xlat16_1 * u_xlat16_2;
    SV_Target0.w = u_xlat16_1 * vs_COLOR0.w;
    return SV_Target0;
#elif defined(_DISS_TEX_NO_ON) && !defined(_MASK_TEX_NO_ON) && defined(_REMAP_NO_ON) && !defined(_TURB_TEX_ON_ON) && !defined(_VER_TEX_NO_ON)
 // Native GLES3 70; SHA256 f33383c3807fd3d61fc93625098e274ef2fdf99ae8ffc5b3614adc5e202893b7
float2 u_xlat0;
float4 u_xlat16_0;
float4 u_xlat16_1;
float3 u_xlat16_2;
float u_xlat3;
float u_xlat16_4;
float2 u_xlat6;
float u_xlat16_7;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Diss_Tex_ST.xy + _Diss_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * _Diss_UV.xy + u_xlat0.xy;
    u_xlat16_0.x = tex2D(_Diss_Tex, u_xlat0.xy).x;
    u_xlat0.x = u_xlat16_0.x + 1.0;
    u_xlat3 = vs_TEXCOORD1.z + (-_Diss_value);
    u_xlat3 = _Diss_Particle_ON * u_xlat3 + _Diss_value;
    u_xlat16_1.xy = (-SrcVec2(SrcVec2(_Diss_Soft_value, _Diss_Soft_value))) + SrcVec2(1.0, 2.0);
    u_xlat0.x = (-u_xlat3) * u_xlat16_1.y + u_xlat0.x;
    u_xlat16_1.x = float(1.0) / u_xlat16_1.x;
    u_xlat16_4 = u_xlat0.x + (-_Diss_Soft_value);
    u_xlat16_7 = u_xlat0.x + _EdgeWidth;
    u_xlat16_7 = u_xlat16_7 + (-_Diss_Soft_value);
    u_xlat16_7 = u_xlat16_1.x * u_xlat16_7;
    u_xlat16_7 = clamp(u_xlat16_7, 0.0, 1.0);
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_4;
    u_xlat16_1.x = clamp(u_xlat16_1.x, 0.0, 1.0);
    u_xlat16_4 = u_xlat16_1.x * -2.0 + 3.0;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_1.x;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_4;
    u_xlat16_4 = u_xlat16_7 * -2.0 + 3.0;
    u_xlat16_7 = u_xlat16_7 * u_xlat16_7;
    u_xlat16_4 = u_xlat16_4 * u_xlat16_7 + (-u_xlat16_1.x);
    u_xlat16_1.x = _Diss_Edge_NO * u_xlat16_4 + u_xlat16_1.x;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat6.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat6.xy = (-u_xlat0.xy) + u_xlat6.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat6.xy + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_7 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_7 = _Alpha_NO_R * u_xlat16_7 + u_xlat16_0.w;
    u_xlat16_2.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_2.xyz = u_xlat16_2.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_2.xyz = u_xlat16_2.xyz * vs_COLOR0.xyz;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_7;
    u_xlat16_1.x = u_xlat16_1.x * _Main_Color.w;
    u_xlat16_1.x = u_xlat16_1.x * _Alpha;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Remap_Tex_ST.xy + _Remap_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * SrcVec2(_Remap_UV.x, _Remap_UV.y) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Remap_Tex, u_xlat0.xy);
    u_xlat16_7 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_7 = _Remap_A_NO_R * u_xlat16_7 + u_xlat16_0.w;
    u_xlat16_1.x = u_xlat16_7 * u_xlat16_1.x;
    SV_Target0.w = u_xlat16_1.x * vs_COLOR0.w;
    u_xlat16_1.xzw = u_xlat16_0.xyz * u_xlat16_2.xyz;
    u_xlat16_2.xyz = (-u_xlat16_2.xyz) * u_xlat16_0.xyz + _Diss_Edge_color.xyz;
    SV_Target0.xyz = SrcVec3(u_xlat16_4) * u_xlat16_2.xyz + u_xlat16_1.xzw;
    return SV_Target0;
#elif defined(_DISS_TEX_NO_ON) && defined(_MASK_TEX_NO_ON) && defined(_REMAP_NO_ON) && !defined(_TURB_TEX_ON_ON) && !defined(_VER_TEX_NO_ON)
 // Native GLES3 71; SHA256 d38343bcb67a9482c423512d389f31a28d1fae79165db6b1d2b832ef67555ef6
float2 u_xlat0;
float4 u_xlat16_0;
float4 u_xlat16_1;
float3 u_xlat16_2;
float u_xlat3;
float u_xlat16_4;
float2 u_xlat6;
float u_xlat16_7;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Diss_Tex_ST.xy + _Diss_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * _Diss_UV.xy + u_xlat0.xy;
    u_xlat16_0.x = tex2D(_Diss_Tex, u_xlat0.xy).x;
    u_xlat0.x = u_xlat16_0.x + 1.0;
    u_xlat3 = vs_TEXCOORD1.z + (-_Diss_value);
    u_xlat3 = _Diss_Particle_ON * u_xlat3 + _Diss_value;
    u_xlat16_1.xy = (-SrcVec2(SrcVec2(_Diss_Soft_value, _Diss_Soft_value))) + SrcVec2(1.0, 2.0);
    u_xlat0.x = (-u_xlat3) * u_xlat16_1.y + u_xlat0.x;
    u_xlat16_1.x = float(1.0) / u_xlat16_1.x;
    u_xlat16_4 = u_xlat0.x + (-_Diss_Soft_value);
    u_xlat16_7 = u_xlat0.x + _EdgeWidth;
    u_xlat16_7 = u_xlat16_7 + (-_Diss_Soft_value);
    u_xlat16_7 = u_xlat16_1.x * u_xlat16_7;
    u_xlat16_7 = clamp(u_xlat16_7, 0.0, 1.0);
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_4;
    u_xlat16_1.x = clamp(u_xlat16_1.x, 0.0, 1.0);
    u_xlat16_4 = u_xlat16_1.x * -2.0 + 3.0;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_1.x;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_4;
    u_xlat16_4 = u_xlat16_7 * -2.0 + 3.0;
    u_xlat16_7 = u_xlat16_7 * u_xlat16_7;
    u_xlat16_4 = u_xlat16_4 * u_xlat16_7 + (-u_xlat16_1.x);
    u_xlat16_1.x = _Diss_Edge_NO * u_xlat16_4 + u_xlat16_1.x;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat6.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat6.xy = (-u_xlat0.xy) + u_xlat6.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat6.xy + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_7 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_7 = _Alpha_NO_R * u_xlat16_7 + u_xlat16_0.w;
    u_xlat16_2.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_2.xyz = u_xlat16_2.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_2.xyz = u_xlat16_2.xyz * vs_COLOR0.xyz;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_7;
    u_xlat16_1.x = u_xlat16_1.x * _Main_Color.w;
    u_xlat16_1.x = u_xlat16_1.x * _Alpha;
    u_xlat16_0.xy = tex2D(_Mask_Tex, vs_TEXCOORD2.xy).xw;
    u_xlat16_7 = (-u_xlat16_0.x) + u_xlat16_0.y;
    u_xlat16_7 = _Mask_A_NO_R * u_xlat16_7 + u_xlat16_0.x;
    u_xlat16_1.x = u_xlat16_7 * u_xlat16_1.x;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Remap_Tex_ST.xy + _Remap_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * SrcVec2(_Remap_UV.x, _Remap_UV.y) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Remap_Tex, u_xlat0.xy);
    u_xlat16_7 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_7 = _Remap_A_NO_R * u_xlat16_7 + u_xlat16_0.w;
    u_xlat16_1.x = u_xlat16_7 * u_xlat16_1.x;
    SV_Target0.w = u_xlat16_1.x * vs_COLOR0.w;
    u_xlat16_1.xzw = u_xlat16_0.xyz * u_xlat16_2.xyz;
    u_xlat16_2.xyz = (-u_xlat16_2.xyz) * u_xlat16_0.xyz + _Diss_Edge_color.xyz;
    SV_Target0.xyz = SrcVec3(u_xlat16_4) * u_xlat16_2.xyz + u_xlat16_1.xzw;
    return SV_Target0;
#elif !defined(_DISS_TEX_NO_ON) && !defined(_MASK_TEX_NO_ON) && !defined(_REMAP_NO_ON) && defined(_TURB_TEX_ON_ON) && !defined(_VER_TEX_NO_ON)
 // Native GLES3 72; SHA256 e1a78f3706996db6ab088aa5fcb1b4758747be65a5ddf26e63d21542e8c0ab72
float2 u_xlat0;
float4 u_xlat16_0;
float4 u_xlat16_1;
float3 u_xlat16_3;
float2 u_xlat4;
float u_xlat16_4;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat4.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat4.xy = (-u_xlat0.xy) + u_xlat4.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat4.xy + u_xlat0.xy;
    u_xlat4.xy = vs_TEXCOORD0.xy * _Turb_Noise_ST.xy + _Turb_Noise_ST.zw;
    u_xlat4.xy = _Time.yy * SrcVec2(_Turb_UV.x, _Turb_UV.y) + u_xlat4.xy;
    u_xlat16_4 = tex2D(_Turb_Noise, u_xlat4.xy).x;
    u_xlat0.xy = SrcVec2(u_xlat16_4) * SrcVec2(_Turb_MainTex_Value) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_1.x = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_1.x = _Alpha_NO_R * u_xlat16_1.x + u_xlat16_0.w;
    u_xlat16_3.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_1.yzw = u_xlat16_3.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_1.x = u_xlat16_1.x * _Main_Color.w;
    u_xlat16_1.x = u_xlat16_1.x * _Alpha;
    SV_Target0 = u_xlat16_1.yzwx * vs_COLOR0;
    return SV_Target0;
#elif !defined(_DISS_TEX_NO_ON) && defined(_MASK_TEX_NO_ON) && !defined(_REMAP_NO_ON) && defined(_TURB_TEX_ON_ON) && !defined(_VER_TEX_NO_ON)
 // Native GLES3 73; SHA256 6769f22d4bb1e2f6f5a19a78c3e1ca7ea46a7f0a7595699a2daaaaecb154f2d4
float2 u_xlat0;
float4 u_xlat16_0;
float u_xlat16_1;
float3 u_xlat16_3;
float2 u_xlat4;
float u_xlat16_4;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat4.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat4.xy = (-u_xlat0.xy) + u_xlat4.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat4.xy + u_xlat0.xy;
    u_xlat4.xy = vs_TEXCOORD0.xy * _Turb_Noise_ST.xy + _Turb_Noise_ST.zw;
    u_xlat4.xy = _Time.yy * SrcVec2(_Turb_UV.x, _Turb_UV.y) + u_xlat4.xy;
    u_xlat16_4 = tex2D(_Turb_Noise, u_xlat4.xy).x;
    u_xlat0.xy = SrcVec2(u_xlat16_4) * SrcVec2(_Turb_MainTex_Value) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_1 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_1 = _Alpha_NO_R * u_xlat16_1 + u_xlat16_0.w;
    u_xlat16_3.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_3.xyz = u_xlat16_3.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    SV_Target0.xyz = u_xlat16_3.xyz * vs_COLOR0.xyz;
    u_xlat16_1 = u_xlat16_1 * _Main_Color.w;
    u_xlat16_1 = u_xlat16_1 * _Alpha;
    u_xlat16_0.xy = tex2D(_Mask_Tex, vs_TEXCOORD2.xy).xw;
    u_xlat16_3.x = (-u_xlat16_0.x) + u_xlat16_0.y;
    u_xlat16_3.x = _Mask_A_NO_R * u_xlat16_3.x + u_xlat16_0.x;
    u_xlat16_1 = u_xlat16_3.x * u_xlat16_1;
    SV_Target0.w = u_xlat16_1 * vs_COLOR0.w;
    return SV_Target0;
#elif defined(_DISS_TEX_NO_ON) && !defined(_MASK_TEX_NO_ON) && !defined(_REMAP_NO_ON) && defined(_TURB_TEX_ON_ON) && !defined(_VER_TEX_NO_ON)
 // Native GLES3 74; SHA256 3030448a1cabbb026d96d3c4fcabf4585348c1ba7d6de19e37a79a75d5ab35ab
float2 u_xlat0;
float4 u_xlat16_0;
float4 u_xlat16_1;
float2 u_xlat2;
float3 u_xlat16_3;
float u_xlat4;
float u_xlat16_5;
float2 u_xlat8;
float u_xlat16_8;
float u_xlat16_9;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Diss_Tex_ST.xy + _Diss_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * _Diss_UV.xy + u_xlat0.xy;
    u_xlat8.xy = vs_TEXCOORD0.xy * _Turb_Noise_ST.xy + _Turb_Noise_ST.zw;
    u_xlat8.xy = _Time.yy * SrcVec2(_Turb_UV.x, _Turb_UV.y) + u_xlat8.xy;
    u_xlat16_8 = tex2D(_Turb_Noise, u_xlat8.xy).x;
    u_xlat0.xy = SrcVec2(u_xlat16_8) * SrcVec2(SrcVec2(_Turb_Diss_Value, _Turb_Diss_Value)) + u_xlat0.xy;
    u_xlat16_0.x = tex2D(_Diss_Tex, u_xlat0.xy).x;
    u_xlat0.x = u_xlat16_0.x + 1.0;
    u_xlat4 = vs_TEXCOORD1.z + (-_Diss_value);
    u_xlat4 = _Diss_Particle_ON * u_xlat4 + _Diss_value;
    u_xlat16_1.xy = (-SrcVec2(SrcVec2(_Diss_Soft_value, _Diss_Soft_value))) + SrcVec2(1.0, 2.0);
    u_xlat0.x = (-u_xlat4) * u_xlat16_1.y + u_xlat0.x;
    u_xlat16_1.x = float(1.0) / u_xlat16_1.x;
    u_xlat16_5 = u_xlat0.x + (-_Diss_Soft_value);
    u_xlat16_9 = u_xlat0.x + _EdgeWidth;
    u_xlat16_9 = u_xlat16_9 + (-_Diss_Soft_value);
    u_xlat16_9 = u_xlat16_1.x * u_xlat16_9;
    u_xlat16_9 = clamp(u_xlat16_9, 0.0, 1.0);
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_5;
    u_xlat16_1.x = clamp(u_xlat16_1.x, 0.0, 1.0);
    u_xlat16_5 = u_xlat16_1.x * -2.0 + 3.0;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_1.x;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_5;
    u_xlat16_5 = u_xlat16_9 * -2.0 + 3.0;
    u_xlat16_9 = u_xlat16_9 * u_xlat16_9;
    u_xlat16_5 = u_xlat16_5 * u_xlat16_9 + (-u_xlat16_1.x);
    u_xlat16_1.x = _Diss_Edge_NO * u_xlat16_5 + u_xlat16_1.x;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat2.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat2.xy = (-u_xlat0.xy) + u_xlat2.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat2.xy + u_xlat0.xy;
    u_xlat0.xy = SrcVec2(u_xlat16_8) * SrcVec2(_Turb_MainTex_Value) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_9 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_9 = _Alpha_NO_R * u_xlat16_9 + u_xlat16_0.w;
    u_xlat16_3.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_3.xyz = u_xlat16_3.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_9;
    u_xlat16_1.x = u_xlat16_1.x * _Main_Color.w;
    u_xlat16_1.x = u_xlat16_1.x * _Alpha;
    SV_Target0.w = u_xlat16_1.x * vs_COLOR0.w;
    u_xlat16_1.xzw = u_xlat16_3.xyz * vs_COLOR0.xyz;
    u_xlat16_3.xyz = (-u_xlat16_3.xyz) * vs_COLOR0.xyz + _Diss_Edge_color.xyz;
    SV_Target0.xyz = SrcVec3(u_xlat16_5) * u_xlat16_3.xyz + u_xlat16_1.xzw;
    return SV_Target0;
#elif defined(_DISS_TEX_NO_ON) && defined(_MASK_TEX_NO_ON) && !defined(_REMAP_NO_ON) && defined(_TURB_TEX_ON_ON) && !defined(_VER_TEX_NO_ON)
 // Native GLES3 75; SHA256 3bdaff44f1b724aa847c3cda73aec1077b69c12731acd1b498a3fe0cc8433132
float2 u_xlat0;
float4 u_xlat16_0;
float4 u_xlat16_1;
float2 u_xlat2;
float3 u_xlat16_3;
float u_xlat4;
float u_xlat16_5;
float2 u_xlat8;
float u_xlat16_8;
float u_xlat16_9;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Diss_Tex_ST.xy + _Diss_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * _Diss_UV.xy + u_xlat0.xy;
    u_xlat8.xy = vs_TEXCOORD0.xy * _Turb_Noise_ST.xy + _Turb_Noise_ST.zw;
    u_xlat8.xy = _Time.yy * SrcVec2(_Turb_UV.x, _Turb_UV.y) + u_xlat8.xy;
    u_xlat16_8 = tex2D(_Turb_Noise, u_xlat8.xy).x;
    u_xlat0.xy = SrcVec2(u_xlat16_8) * SrcVec2(SrcVec2(_Turb_Diss_Value, _Turb_Diss_Value)) + u_xlat0.xy;
    u_xlat16_0.x = tex2D(_Diss_Tex, u_xlat0.xy).x;
    u_xlat0.x = u_xlat16_0.x + 1.0;
    u_xlat4 = vs_TEXCOORD1.z + (-_Diss_value);
    u_xlat4 = _Diss_Particle_ON * u_xlat4 + _Diss_value;
    u_xlat16_1.xy = (-SrcVec2(SrcVec2(_Diss_Soft_value, _Diss_Soft_value))) + SrcVec2(1.0, 2.0);
    u_xlat0.x = (-u_xlat4) * u_xlat16_1.y + u_xlat0.x;
    u_xlat16_1.x = float(1.0) / u_xlat16_1.x;
    u_xlat16_5 = u_xlat0.x + (-_Diss_Soft_value);
    u_xlat16_9 = u_xlat0.x + _EdgeWidth;
    u_xlat16_9 = u_xlat16_9 + (-_Diss_Soft_value);
    u_xlat16_9 = u_xlat16_1.x * u_xlat16_9;
    u_xlat16_9 = clamp(u_xlat16_9, 0.0, 1.0);
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_5;
    u_xlat16_1.x = clamp(u_xlat16_1.x, 0.0, 1.0);
    u_xlat16_5 = u_xlat16_1.x * -2.0 + 3.0;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_1.x;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_5;
    u_xlat16_5 = u_xlat16_9 * -2.0 + 3.0;
    u_xlat16_9 = u_xlat16_9 * u_xlat16_9;
    u_xlat16_5 = u_xlat16_5 * u_xlat16_9 + (-u_xlat16_1.x);
    u_xlat16_1.x = _Diss_Edge_NO * u_xlat16_5 + u_xlat16_1.x;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat2.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat2.xy = (-u_xlat0.xy) + u_xlat2.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat2.xy + u_xlat0.xy;
    u_xlat0.xy = SrcVec2(u_xlat16_8) * SrcVec2(_Turb_MainTex_Value) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_9 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_9 = _Alpha_NO_R * u_xlat16_9 + u_xlat16_0.w;
    u_xlat16_3.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_3.xyz = u_xlat16_3.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_9;
    u_xlat16_1.x = u_xlat16_1.x * _Main_Color.w;
    u_xlat16_1.x = u_xlat16_1.x * _Alpha;
    u_xlat16_0.xy = tex2D(_Mask_Tex, vs_TEXCOORD2.xy).xw;
    u_xlat16_9 = (-u_xlat16_0.x) + u_xlat16_0.y;
    u_xlat16_9 = _Mask_A_NO_R * u_xlat16_9 + u_xlat16_0.x;
    u_xlat16_1.x = u_xlat16_9 * u_xlat16_1.x;
    SV_Target0.w = u_xlat16_1.x * vs_COLOR0.w;
    u_xlat16_1.xzw = u_xlat16_3.xyz * vs_COLOR0.xyz;
    u_xlat16_3.xyz = (-u_xlat16_3.xyz) * vs_COLOR0.xyz + _Diss_Edge_color.xyz;
    SV_Target0.xyz = SrcVec3(u_xlat16_5) * u_xlat16_3.xyz + u_xlat16_1.xzw;
    return SV_Target0;
#elif !defined(_DISS_TEX_NO_ON) && !defined(_MASK_TEX_NO_ON) && defined(_REMAP_NO_ON) && defined(_TURB_TEX_ON_ON) && !defined(_VER_TEX_NO_ON)
 // Native GLES3 76; SHA256 b7e4bfef15322a1291287b85e7839d1a2ab629e66ba52104fe863c7cb665c8fa
float2 u_xlat0;
float4 u_xlat16_0;
float4 u_xlat16_1;
float u_xlat16_2;
float u_xlat16_3;
float3 u_xlat16_6;
float2 u_xlat8;
float u_xlat16_8;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat8.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat8.xy = (-u_xlat0.xy) + u_xlat8.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat8.xy + u_xlat0.xy;
    u_xlat8.xy = vs_TEXCOORD0.xy * _Turb_Noise_ST.xy + _Turb_Noise_ST.zw;
    u_xlat8.xy = _Time.yy * SrcVec2(_Turb_UV.x, _Turb_UV.y) + u_xlat8.xy;
    u_xlat16_8 = tex2D(_Turb_Noise, u_xlat8.xy).x;
    u_xlat0.xy = SrcVec2(u_xlat16_8) * SrcVec2(_Turb_MainTex_Value) + u_xlat0.xy;
    u_xlat16_1 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_2 = (-u_xlat16_1.w) + u_xlat16_1.x;
    u_xlat16_2 = _Alpha_NO_R * u_xlat16_2 + u_xlat16_1.w;
    u_xlat16_6.xyz = u_xlat16_1.xyz * _Main_Color.xyz;
    u_xlat16_6.xyz = u_xlat16_6.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_6.xyz = u_xlat16_6.xyz * vs_COLOR0.xyz;
    u_xlat16_2 = u_xlat16_2 * _Main_Color.w;
    u_xlat16_2 = u_xlat16_2 * _Alpha;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Remap_Tex_ST.xy + _Remap_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * SrcVec2(_Remap_UV.x, _Remap_UV.y) + u_xlat0.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Turb_Remap_Value, _Turb_Remap_Value)) * SrcVec2(u_xlat16_8) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Remap_Tex, u_xlat0.xy);
    u_xlat16_3 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_3 = _Remap_A_NO_R * u_xlat16_3 + u_xlat16_0.w;
    SV_Target0.xyz = u_xlat16_0.xyz * u_xlat16_6.xyz;
    u_xlat16_2 = u_xlat16_2 * u_xlat16_3;
    SV_Target0.w = u_xlat16_2 * vs_COLOR0.w;
    return SV_Target0;
#elif !defined(_DISS_TEX_NO_ON) && defined(_MASK_TEX_NO_ON) && defined(_REMAP_NO_ON) && defined(_TURB_TEX_ON_ON) && !defined(_VER_TEX_NO_ON)
 // Native GLES3 77; SHA256 bf4a703e776fe589bf9e1e396532169da145066617108968aa8bc82f9246c230
float2 u_xlat0;
float4 u_xlat16_0;
float4 u_xlat16_1;
float u_xlat16_2;
float u_xlat16_3;
float3 u_xlat16_6;
float2 u_xlat8;
float u_xlat16_8;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat8.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat8.xy = (-u_xlat0.xy) + u_xlat8.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat8.xy + u_xlat0.xy;
    u_xlat8.xy = vs_TEXCOORD0.xy * _Turb_Noise_ST.xy + _Turb_Noise_ST.zw;
    u_xlat8.xy = _Time.yy * SrcVec2(_Turb_UV.x, _Turb_UV.y) + u_xlat8.xy;
    u_xlat16_8 = tex2D(_Turb_Noise, u_xlat8.xy).x;
    u_xlat0.xy = SrcVec2(u_xlat16_8) * SrcVec2(_Turb_MainTex_Value) + u_xlat0.xy;
    u_xlat16_1 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_2 = (-u_xlat16_1.w) + u_xlat16_1.x;
    u_xlat16_2 = _Alpha_NO_R * u_xlat16_2 + u_xlat16_1.w;
    u_xlat16_6.xyz = u_xlat16_1.xyz * _Main_Color.xyz;
    u_xlat16_6.xyz = u_xlat16_6.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_6.xyz = u_xlat16_6.xyz * vs_COLOR0.xyz;
    u_xlat16_2 = u_xlat16_2 * _Main_Color.w;
    u_xlat16_2 = u_xlat16_2 * _Alpha;
    u_xlat16_0.xy = tex2D(_Mask_Tex, vs_TEXCOORD2.xy).xw;
    u_xlat16_3 = (-u_xlat16_0.x) + u_xlat16_0.y;
    u_xlat16_3 = _Mask_A_NO_R * u_xlat16_3 + u_xlat16_0.x;
    u_xlat16_2 = u_xlat16_2 * u_xlat16_3;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Remap_Tex_ST.xy + _Remap_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * SrcVec2(_Remap_UV.x, _Remap_UV.y) + u_xlat0.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Turb_Remap_Value, _Turb_Remap_Value)) * SrcVec2(u_xlat16_8) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Remap_Tex, u_xlat0.xy);
    u_xlat16_3 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_3 = _Remap_A_NO_R * u_xlat16_3 + u_xlat16_0.w;
    SV_Target0.xyz = u_xlat16_0.xyz * u_xlat16_6.xyz;
    u_xlat16_2 = u_xlat16_2 * u_xlat16_3;
    SV_Target0.w = u_xlat16_2 * vs_COLOR0.w;
    return SV_Target0;
#elif defined(_DISS_TEX_NO_ON) && !defined(_MASK_TEX_NO_ON) && defined(_REMAP_NO_ON) && defined(_TURB_TEX_ON_ON) && !defined(_VER_TEX_NO_ON)
 // Native GLES3 78; SHA256 430b8fe8be3150e133e8b8aedda672889ee7512f9b4aa8b3214ec6557b7bfb64
float2 u_xlat0;
float4 u_xlat16_0;
float2 u_xlat1;
float4 u_xlat16_1;
float4 u_xlat16_2;
float3 u_xlat16_3;
float2 u_xlat4;
float u_xlat16_4;
float u_xlat16_6;
float u_xlat16_10;
float u_xlat16_12;
    u_xlat0.x = vs_TEXCOORD1.z + (-_Diss_value);
    u_xlat0.x = _Diss_Particle_ON * u_xlat0.x + _Diss_value;
    u_xlat4.xy = vs_TEXCOORD0.xy * _Diss_Tex_ST.xy + _Diss_Tex_ST.zw;
    u_xlat4.xy = _Time.yy * _Diss_UV.xy + u_xlat4.xy;
    u_xlat1.xy = vs_TEXCOORD0.xy * _Turb_Noise_ST.xy + _Turb_Noise_ST.zw;
    u_xlat1.xy = _Time.yy * SrcVec2(_Turb_UV.x, _Turb_UV.y) + u_xlat1.xy;
    u_xlat16_12 = tex2D(_Turb_Noise, u_xlat1.xy).x;
    u_xlat4.xy = SrcVec2(u_xlat16_12) * SrcVec2(SrcVec2(_Turb_Diss_Value, _Turb_Diss_Value)) + u_xlat4.xy;
    u_xlat16_4 = tex2D(_Diss_Tex, u_xlat4.xy).x;
    u_xlat4.x = u_xlat16_4 + 1.0;
    u_xlat16_2.xy = (-SrcVec2(SrcVec2(_Diss_Soft_value, _Diss_Soft_value))) + SrcVec2(1.0, 2.0);
    u_xlat0.x = (-u_xlat0.x) * u_xlat16_2.y + u_xlat4.x;
    u_xlat16_2.x = float(1.0) / u_xlat16_2.x;
    u_xlat16_6 = u_xlat0.x + (-_Diss_Soft_value);
    u_xlat16_10 = u_xlat0.x + _EdgeWidth;
    u_xlat16_10 = u_xlat16_10 + (-_Diss_Soft_value);
    u_xlat16_10 = u_xlat16_2.x * u_xlat16_10;
    u_xlat16_10 = clamp(u_xlat16_10, 0.0, 1.0);
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_6;
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlat16_6 = u_xlat16_2.x * -2.0 + 3.0;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_2.x;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_6;
    u_xlat16_6 = u_xlat16_10 * -2.0 + 3.0;
    u_xlat16_10 = u_xlat16_10 * u_xlat16_10;
    u_xlat16_6 = u_xlat16_6 * u_xlat16_10 + (-u_xlat16_2.x);
    u_xlat16_2.x = _Diss_Edge_NO * u_xlat16_6 + u_xlat16_2.x;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat1.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat1.xy = (-u_xlat0.xy) + u_xlat1.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat1.xy + u_xlat0.xy;
    u_xlat0.xy = SrcVec2(u_xlat16_12) * SrcVec2(_Turb_MainTex_Value) + u_xlat0.xy;
    u_xlat16_1 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_10 = (-u_xlat16_1.w) + u_xlat16_1.x;
    u_xlat16_10 = _Alpha_NO_R * u_xlat16_10 + u_xlat16_1.w;
    u_xlat16_3.xyz = u_xlat16_1.xyz * _Main_Color.xyz;
    u_xlat16_3.xyz = u_xlat16_3.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_3.xyz = u_xlat16_3.xyz * vs_COLOR0.xyz;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_10;
    u_xlat16_2.x = u_xlat16_2.x * _Main_Color.w;
    u_xlat16_2.x = u_xlat16_2.x * _Alpha;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Remap_Tex_ST.xy + _Remap_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * SrcVec2(_Remap_UV.x, _Remap_UV.y) + u_xlat0.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Turb_Remap_Value, _Turb_Remap_Value)) * SrcVec2(u_xlat16_12) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Remap_Tex, u_xlat0.xy);
    u_xlat16_10 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_10 = _Remap_A_NO_R * u_xlat16_10 + u_xlat16_0.w;
    u_xlat16_2.x = u_xlat16_10 * u_xlat16_2.x;
    SV_Target0.w = u_xlat16_2.x * vs_COLOR0.w;
    u_xlat16_2.xzw = u_xlat16_0.xyz * u_xlat16_3.xyz;
    u_xlat16_3.xyz = (-u_xlat16_3.xyz) * u_xlat16_0.xyz + _Diss_Edge_color.xyz;
    SV_Target0.xyz = SrcVec3(u_xlat16_6) * u_xlat16_3.xyz + u_xlat16_2.xzw;
    return SV_Target0;
#elif defined(_DISS_TEX_NO_ON) && defined(_MASK_TEX_NO_ON) && defined(_REMAP_NO_ON) && defined(_TURB_TEX_ON_ON) && !defined(_VER_TEX_NO_ON)
 // Native GLES3 79; SHA256 5f383cf2b13af6c98f3b9b6c8e6279387aa76d70e93d6ba709e92801dc538d3b
float2 u_xlat0;
float4 u_xlat16_0;
float2 u_xlat1;
float4 u_xlat16_1;
float4 u_xlat16_2;
float3 u_xlat16_3;
float2 u_xlat4;
float u_xlat16_4;
float u_xlat16_6;
float u_xlat16_10;
float u_xlat16_12;
    u_xlat0.x = vs_TEXCOORD1.z + (-_Diss_value);
    u_xlat0.x = _Diss_Particle_ON * u_xlat0.x + _Diss_value;
    u_xlat4.xy = vs_TEXCOORD0.xy * _Diss_Tex_ST.xy + _Diss_Tex_ST.zw;
    u_xlat4.xy = _Time.yy * _Diss_UV.xy + u_xlat4.xy;
    u_xlat1.xy = vs_TEXCOORD0.xy * _Turb_Noise_ST.xy + _Turb_Noise_ST.zw;
    u_xlat1.xy = _Time.yy * SrcVec2(_Turb_UV.x, _Turb_UV.y) + u_xlat1.xy;
    u_xlat16_12 = tex2D(_Turb_Noise, u_xlat1.xy).x;
    u_xlat4.xy = SrcVec2(u_xlat16_12) * SrcVec2(SrcVec2(_Turb_Diss_Value, _Turb_Diss_Value)) + u_xlat4.xy;
    u_xlat16_4 = tex2D(_Diss_Tex, u_xlat4.xy).x;
    u_xlat4.x = u_xlat16_4 + 1.0;
    u_xlat16_2.xy = (-SrcVec2(SrcVec2(_Diss_Soft_value, _Diss_Soft_value))) + SrcVec2(1.0, 2.0);
    u_xlat0.x = (-u_xlat0.x) * u_xlat16_2.y + u_xlat4.x;
    u_xlat16_2.x = float(1.0) / u_xlat16_2.x;
    u_xlat16_6 = u_xlat0.x + (-_Diss_Soft_value);
    u_xlat16_10 = u_xlat0.x + _EdgeWidth;
    u_xlat16_10 = u_xlat16_10 + (-_Diss_Soft_value);
    u_xlat16_10 = u_xlat16_2.x * u_xlat16_10;
    u_xlat16_10 = clamp(u_xlat16_10, 0.0, 1.0);
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_6;
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlat16_6 = u_xlat16_2.x * -2.0 + 3.0;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_2.x;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_6;
    u_xlat16_6 = u_xlat16_10 * -2.0 + 3.0;
    u_xlat16_10 = u_xlat16_10 * u_xlat16_10;
    u_xlat16_6 = u_xlat16_6 * u_xlat16_10 + (-u_xlat16_2.x);
    u_xlat16_2.x = _Diss_Edge_NO * u_xlat16_6 + u_xlat16_2.x;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat1.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat1.xy = (-u_xlat0.xy) + u_xlat1.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat1.xy + u_xlat0.xy;
    u_xlat0.xy = SrcVec2(u_xlat16_12) * SrcVec2(_Turb_MainTex_Value) + u_xlat0.xy;
    u_xlat16_1 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_10 = (-u_xlat16_1.w) + u_xlat16_1.x;
    u_xlat16_10 = _Alpha_NO_R * u_xlat16_10 + u_xlat16_1.w;
    u_xlat16_3.xyz = u_xlat16_1.xyz * _Main_Color.xyz;
    u_xlat16_3.xyz = u_xlat16_3.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_3.xyz = u_xlat16_3.xyz * vs_COLOR0.xyz;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_10;
    u_xlat16_2.x = u_xlat16_2.x * _Main_Color.w;
    u_xlat16_2.x = u_xlat16_2.x * _Alpha;
    u_xlat16_0.xy = tex2D(_Mask_Tex, vs_TEXCOORD2.xy).xw;
    u_xlat16_10 = (-u_xlat16_0.x) + u_xlat16_0.y;
    u_xlat16_10 = _Mask_A_NO_R * u_xlat16_10 + u_xlat16_0.x;
    u_xlat16_2.x = u_xlat16_10 * u_xlat16_2.x;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Remap_Tex_ST.xy + _Remap_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * SrcVec2(_Remap_UV.x, _Remap_UV.y) + u_xlat0.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Turb_Remap_Value, _Turb_Remap_Value)) * SrcVec2(u_xlat16_12) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Remap_Tex, u_xlat0.xy);
    u_xlat16_10 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_10 = _Remap_A_NO_R * u_xlat16_10 + u_xlat16_0.w;
    u_xlat16_2.x = u_xlat16_10 * u_xlat16_2.x;
    SV_Target0.w = u_xlat16_2.x * vs_COLOR0.w;
    u_xlat16_2.xzw = u_xlat16_0.xyz * u_xlat16_3.xyz;
    u_xlat16_3.xyz = (-u_xlat16_3.xyz) * u_xlat16_0.xyz + _Diss_Edge_color.xyz;
    SV_Target0.xyz = SrcVec3(u_xlat16_6) * u_xlat16_3.xyz + u_xlat16_2.xzw;
    return SV_Target0;
#elif !defined(_DISS_TEX_NO_ON) && !defined(_MASK_TEX_NO_ON) && !defined(_REMAP_NO_ON) && !defined(_TURB_TEX_ON_ON) && defined(_VER_TEX_NO_ON)
 // Native GLES3 80; SHA256 1d1f7fc4c749a9a30a7882ece6a0f9f88bf899ca55b43fac9b10446b9905028e
float2 u_xlat0;
float4 u_xlat16_0;
float4 u_xlat16_1;
float3 u_xlat16_3;
float2 u_xlat4;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat4.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat4.xy = (-u_xlat0.xy) + u_xlat4.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat4.xy + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_1.x = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_1.x = _Alpha_NO_R * u_xlat16_1.x + u_xlat16_0.w;
    u_xlat16_3.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_1.yzw = u_xlat16_3.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_1.x = u_xlat16_1.x * _Main_Color.w;
    u_xlat16_1.x = u_xlat16_1.x * _Alpha;
    SV_Target0 = u_xlat16_1.yzwx * vs_COLOR0;
    return SV_Target0;
#elif !defined(_DISS_TEX_NO_ON) && defined(_MASK_TEX_NO_ON) && !defined(_REMAP_NO_ON) && !defined(_TURB_TEX_ON_ON) && defined(_VER_TEX_NO_ON)
 // Native GLES3 81; SHA256 b6c2c031daba3d8bfe6994dc78957c356a9afc520a5d07dd70c2f84cbe0b79d1
float2 u_xlat0;
float4 u_xlat16_0;
float u_xlat16_1;
float3 u_xlat16_3;
float2 u_xlat4;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat4.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat4.xy = (-u_xlat0.xy) + u_xlat4.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat4.xy + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_1 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_1 = _Alpha_NO_R * u_xlat16_1 + u_xlat16_0.w;
    u_xlat16_3.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_3.xyz = u_xlat16_3.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    SV_Target0.xyz = u_xlat16_3.xyz * vs_COLOR0.xyz;
    u_xlat16_1 = u_xlat16_1 * _Main_Color.w;
    u_xlat16_1 = u_xlat16_1 * _Alpha;
    u_xlat16_0.xy = tex2D(_Mask_Tex, vs_TEXCOORD2.xy).xw;
    u_xlat16_3.x = (-u_xlat16_0.x) + u_xlat16_0.y;
    u_xlat16_3.x = _Mask_A_NO_R * u_xlat16_3.x + u_xlat16_0.x;
    u_xlat16_1 = u_xlat16_3.x * u_xlat16_1;
    SV_Target0.w = u_xlat16_1 * vs_COLOR0.w;
    return SV_Target0;
#elif defined(_DISS_TEX_NO_ON) && !defined(_MASK_TEX_NO_ON) && !defined(_REMAP_NO_ON) && !defined(_TURB_TEX_ON_ON) && defined(_VER_TEX_NO_ON)
 // Native GLES3 82; SHA256 2d06314f6dfbf61650df57a1dc9e6e502b211199aedb5b32115297b8d3959ba9
float2 u_xlat0;
float4 u_xlat16_0;
float4 u_xlat16_1;
float3 u_xlat16_2;
float u_xlat3;
float u_xlat16_4;
float2 u_xlat6;
float u_xlat16_7;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Diss_Tex_ST.xy + _Diss_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * _Diss_UV.xy + u_xlat0.xy;
    u_xlat16_0.x = tex2D(_Diss_Tex, u_xlat0.xy).x;
    u_xlat0.x = u_xlat16_0.x + 1.0;
    u_xlat3 = vs_TEXCOORD1.z + (-_Diss_value);
    u_xlat3 = _Diss_Particle_ON * u_xlat3 + _Diss_value;
    u_xlat16_1.xy = (-SrcVec2(SrcVec2(_Diss_Soft_value, _Diss_Soft_value))) + SrcVec2(1.0, 2.0);
    u_xlat0.x = (-u_xlat3) * u_xlat16_1.y + u_xlat0.x;
    u_xlat16_1.x = float(1.0) / u_xlat16_1.x;
    u_xlat16_4 = u_xlat0.x + (-_Diss_Soft_value);
    u_xlat16_7 = u_xlat0.x + _EdgeWidth;
    u_xlat16_7 = u_xlat16_7 + (-_Diss_Soft_value);
    u_xlat16_7 = u_xlat16_1.x * u_xlat16_7;
    u_xlat16_7 = clamp(u_xlat16_7, 0.0, 1.0);
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_4;
    u_xlat16_1.x = clamp(u_xlat16_1.x, 0.0, 1.0);
    u_xlat16_4 = u_xlat16_1.x * -2.0 + 3.0;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_1.x;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_4;
    u_xlat16_4 = u_xlat16_7 * -2.0 + 3.0;
    u_xlat16_7 = u_xlat16_7 * u_xlat16_7;
    u_xlat16_4 = u_xlat16_4 * u_xlat16_7 + (-u_xlat16_1.x);
    u_xlat16_1.x = _Diss_Edge_NO * u_xlat16_4 + u_xlat16_1.x;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat6.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat6.xy = (-u_xlat0.xy) + u_xlat6.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat6.xy + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_7 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_7 = _Alpha_NO_R * u_xlat16_7 + u_xlat16_0.w;
    u_xlat16_2.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_2.xyz = u_xlat16_2.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_7;
    u_xlat16_1.x = u_xlat16_1.x * _Main_Color.w;
    u_xlat16_1.x = u_xlat16_1.x * _Alpha;
    SV_Target0.w = u_xlat16_1.x * vs_COLOR0.w;
    u_xlat16_1.xzw = u_xlat16_2.xyz * vs_COLOR0.xyz;
    u_xlat16_2.xyz = (-u_xlat16_2.xyz) * vs_COLOR0.xyz + _Diss_Edge_color.xyz;
    SV_Target0.xyz = SrcVec3(u_xlat16_4) * u_xlat16_2.xyz + u_xlat16_1.xzw;
    return SV_Target0;
#elif defined(_DISS_TEX_NO_ON) && defined(_MASK_TEX_NO_ON) && !defined(_REMAP_NO_ON) && !defined(_TURB_TEX_ON_ON) && defined(_VER_TEX_NO_ON)
 // Native GLES3 83; SHA256 8c7cc98b7f2d3896a5eebdf1c819f3c7cf9df073d09c79652cfb9ee17cac5fbd
float2 u_xlat0;
float4 u_xlat16_0;
float4 u_xlat16_1;
float3 u_xlat16_2;
float u_xlat3;
float u_xlat16_4;
float2 u_xlat6;
float u_xlat16_7;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Diss_Tex_ST.xy + _Diss_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * _Diss_UV.xy + u_xlat0.xy;
    u_xlat16_0.x = tex2D(_Diss_Tex, u_xlat0.xy).x;
    u_xlat0.x = u_xlat16_0.x + 1.0;
    u_xlat3 = vs_TEXCOORD1.z + (-_Diss_value);
    u_xlat3 = _Diss_Particle_ON * u_xlat3 + _Diss_value;
    u_xlat16_1.xy = (-SrcVec2(SrcVec2(_Diss_Soft_value, _Diss_Soft_value))) + SrcVec2(1.0, 2.0);
    u_xlat0.x = (-u_xlat3) * u_xlat16_1.y + u_xlat0.x;
    u_xlat16_1.x = float(1.0) / u_xlat16_1.x;
    u_xlat16_4 = u_xlat0.x + (-_Diss_Soft_value);
    u_xlat16_7 = u_xlat0.x + _EdgeWidth;
    u_xlat16_7 = u_xlat16_7 + (-_Diss_Soft_value);
    u_xlat16_7 = u_xlat16_1.x * u_xlat16_7;
    u_xlat16_7 = clamp(u_xlat16_7, 0.0, 1.0);
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_4;
    u_xlat16_1.x = clamp(u_xlat16_1.x, 0.0, 1.0);
    u_xlat16_4 = u_xlat16_1.x * -2.0 + 3.0;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_1.x;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_4;
    u_xlat16_4 = u_xlat16_7 * -2.0 + 3.0;
    u_xlat16_7 = u_xlat16_7 * u_xlat16_7;
    u_xlat16_4 = u_xlat16_4 * u_xlat16_7 + (-u_xlat16_1.x);
    u_xlat16_1.x = _Diss_Edge_NO * u_xlat16_4 + u_xlat16_1.x;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat6.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat6.xy = (-u_xlat0.xy) + u_xlat6.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat6.xy + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_7 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_7 = _Alpha_NO_R * u_xlat16_7 + u_xlat16_0.w;
    u_xlat16_2.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_2.xyz = u_xlat16_2.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_7;
    u_xlat16_1.x = u_xlat16_1.x * _Main_Color.w;
    u_xlat16_1.x = u_xlat16_1.x * _Alpha;
    u_xlat16_0.xy = tex2D(_Mask_Tex, vs_TEXCOORD2.xy).xw;
    u_xlat16_7 = (-u_xlat16_0.x) + u_xlat16_0.y;
    u_xlat16_7 = _Mask_A_NO_R * u_xlat16_7 + u_xlat16_0.x;
    u_xlat16_1.x = u_xlat16_7 * u_xlat16_1.x;
    SV_Target0.w = u_xlat16_1.x * vs_COLOR0.w;
    u_xlat16_1.xzw = u_xlat16_2.xyz * vs_COLOR0.xyz;
    u_xlat16_2.xyz = (-u_xlat16_2.xyz) * vs_COLOR0.xyz + _Diss_Edge_color.xyz;
    SV_Target0.xyz = SrcVec3(u_xlat16_4) * u_xlat16_2.xyz + u_xlat16_1.xzw;
    return SV_Target0;
#elif !defined(_DISS_TEX_NO_ON) && !defined(_MASK_TEX_NO_ON) && defined(_REMAP_NO_ON) && !defined(_TURB_TEX_ON_ON) && defined(_VER_TEX_NO_ON)
 // Native GLES3 84; SHA256 38bb5e45379de24f89b0c5e6e1dd0cb01ef07741930110c1c52f737455866e5c
float2 u_xlat0;
float4 u_xlat16_0;
float u_xlat16_1;
float u_xlat16_2;
float3 u_xlat16_4;
float2 u_xlat6;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat6.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat6.xy = (-u_xlat0.xy) + u_xlat6.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat6.xy + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_1 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_1 = _Alpha_NO_R * u_xlat16_1 + u_xlat16_0.w;
    u_xlat16_4.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_4.xyz = u_xlat16_4.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_4.xyz = u_xlat16_4.xyz * vs_COLOR0.xyz;
    u_xlat16_1 = u_xlat16_1 * _Main_Color.w;
    u_xlat16_1 = u_xlat16_1 * _Alpha;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Remap_Tex_ST.xy + _Remap_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * SrcVec2(_Remap_UV.x, _Remap_UV.y) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Remap_Tex, u_xlat0.xy);
    u_xlat16_2 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_2 = _Remap_A_NO_R * u_xlat16_2 + u_xlat16_0.w;
    SV_Target0.xyz = u_xlat16_0.xyz * u_xlat16_4.xyz;
    u_xlat16_1 = u_xlat16_1 * u_xlat16_2;
    SV_Target0.w = u_xlat16_1 * vs_COLOR0.w;
    return SV_Target0;
#elif !defined(_DISS_TEX_NO_ON) && defined(_MASK_TEX_NO_ON) && defined(_REMAP_NO_ON) && !defined(_TURB_TEX_ON_ON) && defined(_VER_TEX_NO_ON)
 // Native GLES3 85; SHA256 d0994d81190bc28ad6991dfa989e077ef225ee0619dc41b064f6e32a12b4f747
float2 u_xlat0;
float4 u_xlat16_0;
float u_xlat16_1;
float u_xlat16_2;
float3 u_xlat16_4;
float2 u_xlat6;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat6.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat6.xy = (-u_xlat0.xy) + u_xlat6.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat6.xy + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_1 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_1 = _Alpha_NO_R * u_xlat16_1 + u_xlat16_0.w;
    u_xlat16_4.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_4.xyz = u_xlat16_4.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_4.xyz = u_xlat16_4.xyz * vs_COLOR0.xyz;
    u_xlat16_1 = u_xlat16_1 * _Main_Color.w;
    u_xlat16_1 = u_xlat16_1 * _Alpha;
    u_xlat16_0.xy = tex2D(_Mask_Tex, vs_TEXCOORD2.xy).xw;
    u_xlat16_2 = (-u_xlat16_0.x) + u_xlat16_0.y;
    u_xlat16_2 = _Mask_A_NO_R * u_xlat16_2 + u_xlat16_0.x;
    u_xlat16_1 = u_xlat16_1 * u_xlat16_2;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Remap_Tex_ST.xy + _Remap_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * SrcVec2(_Remap_UV.x, _Remap_UV.y) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Remap_Tex, u_xlat0.xy);
    u_xlat16_2 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_2 = _Remap_A_NO_R * u_xlat16_2 + u_xlat16_0.w;
    SV_Target0.xyz = u_xlat16_0.xyz * u_xlat16_4.xyz;
    u_xlat16_1 = u_xlat16_1 * u_xlat16_2;
    SV_Target0.w = u_xlat16_1 * vs_COLOR0.w;
    return SV_Target0;
#elif defined(_DISS_TEX_NO_ON) && !defined(_MASK_TEX_NO_ON) && defined(_REMAP_NO_ON) && !defined(_TURB_TEX_ON_ON) && defined(_VER_TEX_NO_ON)
 // Native GLES3 86; SHA256 ec451ca5ec1d413507c480ff346aef70a36afdba261153e286e59057d1216d28
float2 u_xlat0;
float4 u_xlat16_0;
float4 u_xlat16_1;
float3 u_xlat16_2;
float u_xlat3;
float u_xlat16_4;
float2 u_xlat6;
float u_xlat16_7;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Diss_Tex_ST.xy + _Diss_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * _Diss_UV.xy + u_xlat0.xy;
    u_xlat16_0.x = tex2D(_Diss_Tex, u_xlat0.xy).x;
    u_xlat0.x = u_xlat16_0.x + 1.0;
    u_xlat3 = vs_TEXCOORD1.z + (-_Diss_value);
    u_xlat3 = _Diss_Particle_ON * u_xlat3 + _Diss_value;
    u_xlat16_1.xy = (-SrcVec2(SrcVec2(_Diss_Soft_value, _Diss_Soft_value))) + SrcVec2(1.0, 2.0);
    u_xlat0.x = (-u_xlat3) * u_xlat16_1.y + u_xlat0.x;
    u_xlat16_1.x = float(1.0) / u_xlat16_1.x;
    u_xlat16_4 = u_xlat0.x + (-_Diss_Soft_value);
    u_xlat16_7 = u_xlat0.x + _EdgeWidth;
    u_xlat16_7 = u_xlat16_7 + (-_Diss_Soft_value);
    u_xlat16_7 = u_xlat16_1.x * u_xlat16_7;
    u_xlat16_7 = clamp(u_xlat16_7, 0.0, 1.0);
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_4;
    u_xlat16_1.x = clamp(u_xlat16_1.x, 0.0, 1.0);
    u_xlat16_4 = u_xlat16_1.x * -2.0 + 3.0;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_1.x;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_4;
    u_xlat16_4 = u_xlat16_7 * -2.0 + 3.0;
    u_xlat16_7 = u_xlat16_7 * u_xlat16_7;
    u_xlat16_4 = u_xlat16_4 * u_xlat16_7 + (-u_xlat16_1.x);
    u_xlat16_1.x = _Diss_Edge_NO * u_xlat16_4 + u_xlat16_1.x;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat6.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat6.xy = (-u_xlat0.xy) + u_xlat6.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat6.xy + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_7 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_7 = _Alpha_NO_R * u_xlat16_7 + u_xlat16_0.w;
    u_xlat16_2.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_2.xyz = u_xlat16_2.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_2.xyz = u_xlat16_2.xyz * vs_COLOR0.xyz;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_7;
    u_xlat16_1.x = u_xlat16_1.x * _Main_Color.w;
    u_xlat16_1.x = u_xlat16_1.x * _Alpha;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Remap_Tex_ST.xy + _Remap_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * SrcVec2(_Remap_UV.x, _Remap_UV.y) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Remap_Tex, u_xlat0.xy);
    u_xlat16_7 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_7 = _Remap_A_NO_R * u_xlat16_7 + u_xlat16_0.w;
    u_xlat16_1.x = u_xlat16_7 * u_xlat16_1.x;
    SV_Target0.w = u_xlat16_1.x * vs_COLOR0.w;
    u_xlat16_1.xzw = u_xlat16_0.xyz * u_xlat16_2.xyz;
    u_xlat16_2.xyz = (-u_xlat16_2.xyz) * u_xlat16_0.xyz + _Diss_Edge_color.xyz;
    SV_Target0.xyz = SrcVec3(u_xlat16_4) * u_xlat16_2.xyz + u_xlat16_1.xzw;
    return SV_Target0;
#elif defined(_DISS_TEX_NO_ON) && defined(_MASK_TEX_NO_ON) && defined(_REMAP_NO_ON) && !defined(_TURB_TEX_ON_ON) && defined(_VER_TEX_NO_ON)
 // Native GLES3 87; SHA256 443122363256404abb01716bed98e5a7ca923044e6d7e581129a4c2ce5d03ef0
float2 u_xlat0;
float4 u_xlat16_0;
float4 u_xlat16_1;
float3 u_xlat16_2;
float u_xlat3;
float u_xlat16_4;
float2 u_xlat6;
float u_xlat16_7;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Diss_Tex_ST.xy + _Diss_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * _Diss_UV.xy + u_xlat0.xy;
    u_xlat16_0.x = tex2D(_Diss_Tex, u_xlat0.xy).x;
    u_xlat0.x = u_xlat16_0.x + 1.0;
    u_xlat3 = vs_TEXCOORD1.z + (-_Diss_value);
    u_xlat3 = _Diss_Particle_ON * u_xlat3 + _Diss_value;
    u_xlat16_1.xy = (-SrcVec2(SrcVec2(_Diss_Soft_value, _Diss_Soft_value))) + SrcVec2(1.0, 2.0);
    u_xlat0.x = (-u_xlat3) * u_xlat16_1.y + u_xlat0.x;
    u_xlat16_1.x = float(1.0) / u_xlat16_1.x;
    u_xlat16_4 = u_xlat0.x + (-_Diss_Soft_value);
    u_xlat16_7 = u_xlat0.x + _EdgeWidth;
    u_xlat16_7 = u_xlat16_7 + (-_Diss_Soft_value);
    u_xlat16_7 = u_xlat16_1.x * u_xlat16_7;
    u_xlat16_7 = clamp(u_xlat16_7, 0.0, 1.0);
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_4;
    u_xlat16_1.x = clamp(u_xlat16_1.x, 0.0, 1.0);
    u_xlat16_4 = u_xlat16_1.x * -2.0 + 3.0;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_1.x;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_4;
    u_xlat16_4 = u_xlat16_7 * -2.0 + 3.0;
    u_xlat16_7 = u_xlat16_7 * u_xlat16_7;
    u_xlat16_4 = u_xlat16_4 * u_xlat16_7 + (-u_xlat16_1.x);
    u_xlat16_1.x = _Diss_Edge_NO * u_xlat16_4 + u_xlat16_1.x;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat6.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat6.xy = (-u_xlat0.xy) + u_xlat6.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat6.xy + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_7 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_7 = _Alpha_NO_R * u_xlat16_7 + u_xlat16_0.w;
    u_xlat16_2.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_2.xyz = u_xlat16_2.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_2.xyz = u_xlat16_2.xyz * vs_COLOR0.xyz;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_7;
    u_xlat16_1.x = u_xlat16_1.x * _Main_Color.w;
    u_xlat16_1.x = u_xlat16_1.x * _Alpha;
    u_xlat16_0.xy = tex2D(_Mask_Tex, vs_TEXCOORD2.xy).xw;
    u_xlat16_7 = (-u_xlat16_0.x) + u_xlat16_0.y;
    u_xlat16_7 = _Mask_A_NO_R * u_xlat16_7 + u_xlat16_0.x;
    u_xlat16_1.x = u_xlat16_7 * u_xlat16_1.x;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Remap_Tex_ST.xy + _Remap_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * SrcVec2(_Remap_UV.x, _Remap_UV.y) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Remap_Tex, u_xlat0.xy);
    u_xlat16_7 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_7 = _Remap_A_NO_R * u_xlat16_7 + u_xlat16_0.w;
    u_xlat16_1.x = u_xlat16_7 * u_xlat16_1.x;
    SV_Target0.w = u_xlat16_1.x * vs_COLOR0.w;
    u_xlat16_1.xzw = u_xlat16_0.xyz * u_xlat16_2.xyz;
    u_xlat16_2.xyz = (-u_xlat16_2.xyz) * u_xlat16_0.xyz + _Diss_Edge_color.xyz;
    SV_Target0.xyz = SrcVec3(u_xlat16_4) * u_xlat16_2.xyz + u_xlat16_1.xzw;
    return SV_Target0;
#elif !defined(_DISS_TEX_NO_ON) && !defined(_MASK_TEX_NO_ON) && !defined(_REMAP_NO_ON) && defined(_TURB_TEX_ON_ON) && defined(_VER_TEX_NO_ON)
 // Native GLES3 88; SHA256 dbad902fbb39ddad3940f9c374d1e454da8a61df30d864ad8bdc368101e01c02
float2 u_xlat0;
float4 u_xlat16_0;
float4 u_xlat16_1;
float3 u_xlat16_3;
float2 u_xlat4;
float u_xlat16_4;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat4.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat4.xy = (-u_xlat0.xy) + u_xlat4.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat4.xy + u_xlat0.xy;
    u_xlat4.xy = vs_TEXCOORD0.xy * _Turb_Noise_ST.xy + _Turb_Noise_ST.zw;
    u_xlat4.xy = _Time.yy * SrcVec2(_Turb_UV.x, _Turb_UV.y) + u_xlat4.xy;
    u_xlat16_4 = tex2D(_Turb_Noise, u_xlat4.xy).x;
    u_xlat0.xy = SrcVec2(u_xlat16_4) * SrcVec2(_Turb_MainTex_Value) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_1.x = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_1.x = _Alpha_NO_R * u_xlat16_1.x + u_xlat16_0.w;
    u_xlat16_3.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_1.yzw = u_xlat16_3.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_1.x = u_xlat16_1.x * _Main_Color.w;
    u_xlat16_1.x = u_xlat16_1.x * _Alpha;
    SV_Target0 = u_xlat16_1.yzwx * vs_COLOR0;
    return SV_Target0;
#elif !defined(_DISS_TEX_NO_ON) && defined(_MASK_TEX_NO_ON) && !defined(_REMAP_NO_ON) && defined(_TURB_TEX_ON_ON) && defined(_VER_TEX_NO_ON)
 // Native GLES3 89; SHA256 822b62ba02a1472c6af4ef83a402603c50a85e678889351947f4164a11e11701
float2 u_xlat0;
float4 u_xlat16_0;
float u_xlat16_1;
float3 u_xlat16_3;
float2 u_xlat4;
float u_xlat16_4;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat4.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat4.xy = (-u_xlat0.xy) + u_xlat4.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat4.xy + u_xlat0.xy;
    u_xlat4.xy = vs_TEXCOORD0.xy * _Turb_Noise_ST.xy + _Turb_Noise_ST.zw;
    u_xlat4.xy = _Time.yy * SrcVec2(_Turb_UV.x, _Turb_UV.y) + u_xlat4.xy;
    u_xlat16_4 = tex2D(_Turb_Noise, u_xlat4.xy).x;
    u_xlat0.xy = SrcVec2(u_xlat16_4) * SrcVec2(_Turb_MainTex_Value) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_1 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_1 = _Alpha_NO_R * u_xlat16_1 + u_xlat16_0.w;
    u_xlat16_3.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_3.xyz = u_xlat16_3.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    SV_Target0.xyz = u_xlat16_3.xyz * vs_COLOR0.xyz;
    u_xlat16_1 = u_xlat16_1 * _Main_Color.w;
    u_xlat16_1 = u_xlat16_1 * _Alpha;
    u_xlat16_0.xy = tex2D(_Mask_Tex, vs_TEXCOORD2.xy).xw;
    u_xlat16_3.x = (-u_xlat16_0.x) + u_xlat16_0.y;
    u_xlat16_3.x = _Mask_A_NO_R * u_xlat16_3.x + u_xlat16_0.x;
    u_xlat16_1 = u_xlat16_3.x * u_xlat16_1;
    SV_Target0.w = u_xlat16_1 * vs_COLOR0.w;
    return SV_Target0;
#elif defined(_DISS_TEX_NO_ON) && !defined(_MASK_TEX_NO_ON) && !defined(_REMAP_NO_ON) && defined(_TURB_TEX_ON_ON) && defined(_VER_TEX_NO_ON)
 // Native GLES3 90; SHA256 2da405a06e346e02437e9c6ece60a46fe19439928f4d51091fa4618be0066aa7
float2 u_xlat0;
float4 u_xlat16_0;
float4 u_xlat16_1;
float2 u_xlat2;
float3 u_xlat16_3;
float u_xlat4;
float u_xlat16_5;
float2 u_xlat8;
float u_xlat16_8;
float u_xlat16_9;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Diss_Tex_ST.xy + _Diss_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * _Diss_UV.xy + u_xlat0.xy;
    u_xlat8.xy = vs_TEXCOORD0.xy * _Turb_Noise_ST.xy + _Turb_Noise_ST.zw;
    u_xlat8.xy = _Time.yy * SrcVec2(_Turb_UV.x, _Turb_UV.y) + u_xlat8.xy;
    u_xlat16_8 = tex2D(_Turb_Noise, u_xlat8.xy).x;
    u_xlat0.xy = SrcVec2(u_xlat16_8) * SrcVec2(SrcVec2(_Turb_Diss_Value, _Turb_Diss_Value)) + u_xlat0.xy;
    u_xlat16_0.x = tex2D(_Diss_Tex, u_xlat0.xy).x;
    u_xlat0.x = u_xlat16_0.x + 1.0;
    u_xlat4 = vs_TEXCOORD1.z + (-_Diss_value);
    u_xlat4 = _Diss_Particle_ON * u_xlat4 + _Diss_value;
    u_xlat16_1.xy = (-SrcVec2(SrcVec2(_Diss_Soft_value, _Diss_Soft_value))) + SrcVec2(1.0, 2.0);
    u_xlat0.x = (-u_xlat4) * u_xlat16_1.y + u_xlat0.x;
    u_xlat16_1.x = float(1.0) / u_xlat16_1.x;
    u_xlat16_5 = u_xlat0.x + (-_Diss_Soft_value);
    u_xlat16_9 = u_xlat0.x + _EdgeWidth;
    u_xlat16_9 = u_xlat16_9 + (-_Diss_Soft_value);
    u_xlat16_9 = u_xlat16_1.x * u_xlat16_9;
    u_xlat16_9 = clamp(u_xlat16_9, 0.0, 1.0);
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_5;
    u_xlat16_1.x = clamp(u_xlat16_1.x, 0.0, 1.0);
    u_xlat16_5 = u_xlat16_1.x * -2.0 + 3.0;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_1.x;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_5;
    u_xlat16_5 = u_xlat16_9 * -2.0 + 3.0;
    u_xlat16_9 = u_xlat16_9 * u_xlat16_9;
    u_xlat16_5 = u_xlat16_5 * u_xlat16_9 + (-u_xlat16_1.x);
    u_xlat16_1.x = _Diss_Edge_NO * u_xlat16_5 + u_xlat16_1.x;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat2.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat2.xy = (-u_xlat0.xy) + u_xlat2.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat2.xy + u_xlat0.xy;
    u_xlat0.xy = SrcVec2(u_xlat16_8) * SrcVec2(_Turb_MainTex_Value) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_9 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_9 = _Alpha_NO_R * u_xlat16_9 + u_xlat16_0.w;
    u_xlat16_3.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_3.xyz = u_xlat16_3.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_9;
    u_xlat16_1.x = u_xlat16_1.x * _Main_Color.w;
    u_xlat16_1.x = u_xlat16_1.x * _Alpha;
    SV_Target0.w = u_xlat16_1.x * vs_COLOR0.w;
    u_xlat16_1.xzw = u_xlat16_3.xyz * vs_COLOR0.xyz;
    u_xlat16_3.xyz = (-u_xlat16_3.xyz) * vs_COLOR0.xyz + _Diss_Edge_color.xyz;
    SV_Target0.xyz = SrcVec3(u_xlat16_5) * u_xlat16_3.xyz + u_xlat16_1.xzw;
    return SV_Target0;
#elif defined(_DISS_TEX_NO_ON) && defined(_MASK_TEX_NO_ON) && !defined(_REMAP_NO_ON) && defined(_TURB_TEX_ON_ON) && defined(_VER_TEX_NO_ON)
 // Native GLES3 91; SHA256 d7057bb87f7a838c37dd1043901e3179501f4e6c468b31950560f2d4943d6084
float2 u_xlat0;
float4 u_xlat16_0;
float4 u_xlat16_1;
float2 u_xlat2;
float3 u_xlat16_3;
float u_xlat4;
float u_xlat16_5;
float2 u_xlat8;
float u_xlat16_8;
float u_xlat16_9;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Diss_Tex_ST.xy + _Diss_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * _Diss_UV.xy + u_xlat0.xy;
    u_xlat8.xy = vs_TEXCOORD0.xy * _Turb_Noise_ST.xy + _Turb_Noise_ST.zw;
    u_xlat8.xy = _Time.yy * SrcVec2(_Turb_UV.x, _Turb_UV.y) + u_xlat8.xy;
    u_xlat16_8 = tex2D(_Turb_Noise, u_xlat8.xy).x;
    u_xlat0.xy = SrcVec2(u_xlat16_8) * SrcVec2(SrcVec2(_Turb_Diss_Value, _Turb_Diss_Value)) + u_xlat0.xy;
    u_xlat16_0.x = tex2D(_Diss_Tex, u_xlat0.xy).x;
    u_xlat0.x = u_xlat16_0.x + 1.0;
    u_xlat4 = vs_TEXCOORD1.z + (-_Diss_value);
    u_xlat4 = _Diss_Particle_ON * u_xlat4 + _Diss_value;
    u_xlat16_1.xy = (-SrcVec2(SrcVec2(_Diss_Soft_value, _Diss_Soft_value))) + SrcVec2(1.0, 2.0);
    u_xlat0.x = (-u_xlat4) * u_xlat16_1.y + u_xlat0.x;
    u_xlat16_1.x = float(1.0) / u_xlat16_1.x;
    u_xlat16_5 = u_xlat0.x + (-_Diss_Soft_value);
    u_xlat16_9 = u_xlat0.x + _EdgeWidth;
    u_xlat16_9 = u_xlat16_9 + (-_Diss_Soft_value);
    u_xlat16_9 = u_xlat16_1.x * u_xlat16_9;
    u_xlat16_9 = clamp(u_xlat16_9, 0.0, 1.0);
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_5;
    u_xlat16_1.x = clamp(u_xlat16_1.x, 0.0, 1.0);
    u_xlat16_5 = u_xlat16_1.x * -2.0 + 3.0;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_1.x;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_5;
    u_xlat16_5 = u_xlat16_9 * -2.0 + 3.0;
    u_xlat16_9 = u_xlat16_9 * u_xlat16_9;
    u_xlat16_5 = u_xlat16_5 * u_xlat16_9 + (-u_xlat16_1.x);
    u_xlat16_1.x = _Diss_Edge_NO * u_xlat16_5 + u_xlat16_1.x;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat2.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat2.xy = (-u_xlat0.xy) + u_xlat2.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat2.xy + u_xlat0.xy;
    u_xlat0.xy = SrcVec2(u_xlat16_8) * SrcVec2(_Turb_MainTex_Value) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_9 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_9 = _Alpha_NO_R * u_xlat16_9 + u_xlat16_0.w;
    u_xlat16_3.xyz = u_xlat16_0.xyz * _Main_Color.xyz;
    u_xlat16_3.xyz = u_xlat16_3.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_9;
    u_xlat16_1.x = u_xlat16_1.x * _Main_Color.w;
    u_xlat16_1.x = u_xlat16_1.x * _Alpha;
    u_xlat16_0.xy = tex2D(_Mask_Tex, vs_TEXCOORD2.xy).xw;
    u_xlat16_9 = (-u_xlat16_0.x) + u_xlat16_0.y;
    u_xlat16_9 = _Mask_A_NO_R * u_xlat16_9 + u_xlat16_0.x;
    u_xlat16_1.x = u_xlat16_9 * u_xlat16_1.x;
    SV_Target0.w = u_xlat16_1.x * vs_COLOR0.w;
    u_xlat16_1.xzw = u_xlat16_3.xyz * vs_COLOR0.xyz;
    u_xlat16_3.xyz = (-u_xlat16_3.xyz) * vs_COLOR0.xyz + _Diss_Edge_color.xyz;
    SV_Target0.xyz = SrcVec3(u_xlat16_5) * u_xlat16_3.xyz + u_xlat16_1.xzw;
    return SV_Target0;
#elif !defined(_DISS_TEX_NO_ON) && !defined(_MASK_TEX_NO_ON) && defined(_REMAP_NO_ON) && defined(_TURB_TEX_ON_ON) && defined(_VER_TEX_NO_ON)
 // Native GLES3 92; SHA256 f2302a18c295be528cb16b43595e6aa614b8fdc30711d706eb774c2601e89f0a
float2 u_xlat0;
float4 u_xlat16_0;
float4 u_xlat16_1;
float u_xlat16_2;
float u_xlat16_3;
float3 u_xlat16_6;
float2 u_xlat8;
float u_xlat16_8;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat8.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat8.xy = (-u_xlat0.xy) + u_xlat8.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat8.xy + u_xlat0.xy;
    u_xlat8.xy = vs_TEXCOORD0.xy * _Turb_Noise_ST.xy + _Turb_Noise_ST.zw;
    u_xlat8.xy = _Time.yy * SrcVec2(_Turb_UV.x, _Turb_UV.y) + u_xlat8.xy;
    u_xlat16_8 = tex2D(_Turb_Noise, u_xlat8.xy).x;
    u_xlat0.xy = SrcVec2(u_xlat16_8) * SrcVec2(_Turb_MainTex_Value) + u_xlat0.xy;
    u_xlat16_1 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_2 = (-u_xlat16_1.w) + u_xlat16_1.x;
    u_xlat16_2 = _Alpha_NO_R * u_xlat16_2 + u_xlat16_1.w;
    u_xlat16_6.xyz = u_xlat16_1.xyz * _Main_Color.xyz;
    u_xlat16_6.xyz = u_xlat16_6.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_6.xyz = u_xlat16_6.xyz * vs_COLOR0.xyz;
    u_xlat16_2 = u_xlat16_2 * _Main_Color.w;
    u_xlat16_2 = u_xlat16_2 * _Alpha;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Remap_Tex_ST.xy + _Remap_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * SrcVec2(_Remap_UV.x, _Remap_UV.y) + u_xlat0.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Turb_Remap_Value, _Turb_Remap_Value)) * SrcVec2(u_xlat16_8) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Remap_Tex, u_xlat0.xy);
    u_xlat16_3 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_3 = _Remap_A_NO_R * u_xlat16_3 + u_xlat16_0.w;
    SV_Target0.xyz = u_xlat16_0.xyz * u_xlat16_6.xyz;
    u_xlat16_2 = u_xlat16_2 * u_xlat16_3;
    SV_Target0.w = u_xlat16_2 * vs_COLOR0.w;
    return SV_Target0;
#elif !defined(_DISS_TEX_NO_ON) && defined(_MASK_TEX_NO_ON) && defined(_REMAP_NO_ON) && defined(_TURB_TEX_ON_ON) && defined(_VER_TEX_NO_ON)
 // Native GLES3 93; SHA256 c8e14ca004b513b7fedee7ca12308184a2a150654cdb941d39d721746209a38c
float2 u_xlat0;
float4 u_xlat16_0;
float4 u_xlat16_1;
float u_xlat16_2;
float u_xlat16_3;
float3 u_xlat16_6;
float2 u_xlat8;
float u_xlat16_8;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat8.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat8.xy = (-u_xlat0.xy) + u_xlat8.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat8.xy + u_xlat0.xy;
    u_xlat8.xy = vs_TEXCOORD0.xy * _Turb_Noise_ST.xy + _Turb_Noise_ST.zw;
    u_xlat8.xy = _Time.yy * SrcVec2(_Turb_UV.x, _Turb_UV.y) + u_xlat8.xy;
    u_xlat16_8 = tex2D(_Turb_Noise, u_xlat8.xy).x;
    u_xlat0.xy = SrcVec2(u_xlat16_8) * SrcVec2(_Turb_MainTex_Value) + u_xlat0.xy;
    u_xlat16_1 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_2 = (-u_xlat16_1.w) + u_xlat16_1.x;
    u_xlat16_2 = _Alpha_NO_R * u_xlat16_2 + u_xlat16_1.w;
    u_xlat16_6.xyz = u_xlat16_1.xyz * _Main_Color.xyz;
    u_xlat16_6.xyz = u_xlat16_6.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_6.xyz = u_xlat16_6.xyz * vs_COLOR0.xyz;
    u_xlat16_2 = u_xlat16_2 * _Main_Color.w;
    u_xlat16_2 = u_xlat16_2 * _Alpha;
    u_xlat16_0.xy = tex2D(_Mask_Tex, vs_TEXCOORD2.xy).xw;
    u_xlat16_3 = (-u_xlat16_0.x) + u_xlat16_0.y;
    u_xlat16_3 = _Mask_A_NO_R * u_xlat16_3 + u_xlat16_0.x;
    u_xlat16_2 = u_xlat16_2 * u_xlat16_3;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Remap_Tex_ST.xy + _Remap_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * SrcVec2(_Remap_UV.x, _Remap_UV.y) + u_xlat0.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Turb_Remap_Value, _Turb_Remap_Value)) * SrcVec2(u_xlat16_8) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Remap_Tex, u_xlat0.xy);
    u_xlat16_3 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_3 = _Remap_A_NO_R * u_xlat16_3 + u_xlat16_0.w;
    SV_Target0.xyz = u_xlat16_0.xyz * u_xlat16_6.xyz;
    u_xlat16_2 = u_xlat16_2 * u_xlat16_3;
    SV_Target0.w = u_xlat16_2 * vs_COLOR0.w;
    return SV_Target0;
#elif defined(_DISS_TEX_NO_ON) && !defined(_MASK_TEX_NO_ON) && defined(_REMAP_NO_ON) && defined(_TURB_TEX_ON_ON) && defined(_VER_TEX_NO_ON)
 // Native GLES3 94; SHA256 ea4db562b63b2779868ad210d04c107ae6a1bb07172d6e6de30fd2af884af951
float2 u_xlat0;
float4 u_xlat16_0;
float2 u_xlat1;
float4 u_xlat16_1;
float4 u_xlat16_2;
float3 u_xlat16_3;
float2 u_xlat4;
float u_xlat16_4;
float u_xlat16_6;
float u_xlat16_10;
float u_xlat16_12;
    u_xlat0.x = vs_TEXCOORD1.z + (-_Diss_value);
    u_xlat0.x = _Diss_Particle_ON * u_xlat0.x + _Diss_value;
    u_xlat4.xy = vs_TEXCOORD0.xy * _Diss_Tex_ST.xy + _Diss_Tex_ST.zw;
    u_xlat4.xy = _Time.yy * _Diss_UV.xy + u_xlat4.xy;
    u_xlat1.xy = vs_TEXCOORD0.xy * _Turb_Noise_ST.xy + _Turb_Noise_ST.zw;
    u_xlat1.xy = _Time.yy * SrcVec2(_Turb_UV.x, _Turb_UV.y) + u_xlat1.xy;
    u_xlat16_12 = tex2D(_Turb_Noise, u_xlat1.xy).x;
    u_xlat4.xy = SrcVec2(u_xlat16_12) * SrcVec2(SrcVec2(_Turb_Diss_Value, _Turb_Diss_Value)) + u_xlat4.xy;
    u_xlat16_4 = tex2D(_Diss_Tex, u_xlat4.xy).x;
    u_xlat4.x = u_xlat16_4 + 1.0;
    u_xlat16_2.xy = (-SrcVec2(SrcVec2(_Diss_Soft_value, _Diss_Soft_value))) + SrcVec2(1.0, 2.0);
    u_xlat0.x = (-u_xlat0.x) * u_xlat16_2.y + u_xlat4.x;
    u_xlat16_2.x = float(1.0) / u_xlat16_2.x;
    u_xlat16_6 = u_xlat0.x + (-_Diss_Soft_value);
    u_xlat16_10 = u_xlat0.x + _EdgeWidth;
    u_xlat16_10 = u_xlat16_10 + (-_Diss_Soft_value);
    u_xlat16_10 = u_xlat16_2.x * u_xlat16_10;
    u_xlat16_10 = clamp(u_xlat16_10, 0.0, 1.0);
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_6;
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlat16_6 = u_xlat16_2.x * -2.0 + 3.0;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_2.x;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_6;
    u_xlat16_6 = u_xlat16_10 * -2.0 + 3.0;
    u_xlat16_10 = u_xlat16_10 * u_xlat16_10;
    u_xlat16_6 = u_xlat16_6 * u_xlat16_10 + (-u_xlat16_2.x);
    u_xlat16_2.x = _Diss_Edge_NO * u_xlat16_6 + u_xlat16_2.x;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat1.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat1.xy = (-u_xlat0.xy) + u_xlat1.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat1.xy + u_xlat0.xy;
    u_xlat0.xy = SrcVec2(u_xlat16_12) * SrcVec2(_Turb_MainTex_Value) + u_xlat0.xy;
    u_xlat16_1 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_10 = (-u_xlat16_1.w) + u_xlat16_1.x;
    u_xlat16_10 = _Alpha_NO_R * u_xlat16_10 + u_xlat16_1.w;
    u_xlat16_3.xyz = u_xlat16_1.xyz * _Main_Color.xyz;
    u_xlat16_3.xyz = u_xlat16_3.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_3.xyz = u_xlat16_3.xyz * vs_COLOR0.xyz;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_10;
    u_xlat16_2.x = u_xlat16_2.x * _Main_Color.w;
    u_xlat16_2.x = u_xlat16_2.x * _Alpha;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Remap_Tex_ST.xy + _Remap_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * SrcVec2(_Remap_UV.x, _Remap_UV.y) + u_xlat0.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Turb_Remap_Value, _Turb_Remap_Value)) * SrcVec2(u_xlat16_12) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Remap_Tex, u_xlat0.xy);
    u_xlat16_10 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_10 = _Remap_A_NO_R * u_xlat16_10 + u_xlat16_0.w;
    u_xlat16_2.x = u_xlat16_10 * u_xlat16_2.x;
    SV_Target0.w = u_xlat16_2.x * vs_COLOR0.w;
    u_xlat16_2.xzw = u_xlat16_0.xyz * u_xlat16_3.xyz;
    u_xlat16_3.xyz = (-u_xlat16_3.xyz) * u_xlat16_0.xyz + _Diss_Edge_color.xyz;
    SV_Target0.xyz = SrcVec3(u_xlat16_6) * u_xlat16_3.xyz + u_xlat16_2.xzw;
    return SV_Target0;
#elif defined(_DISS_TEX_NO_ON) && defined(_MASK_TEX_NO_ON) && defined(_REMAP_NO_ON) && defined(_TURB_TEX_ON_ON) && defined(_VER_TEX_NO_ON)
 // Native GLES3 95; SHA256 3df5b922c72e5263e0027b3bbe93b20701e569360091bae18b546966fb264504
float2 u_xlat0;
float4 u_xlat16_0;
float2 u_xlat1;
float4 u_xlat16_1;
float4 u_xlat16_2;
float3 u_xlat16_3;
float2 u_xlat4;
float u_xlat16_4;
float u_xlat16_6;
float u_xlat16_10;
float u_xlat16_12;
    u_xlat0.x = vs_TEXCOORD1.z + (-_Diss_value);
    u_xlat0.x = _Diss_Particle_ON * u_xlat0.x + _Diss_value;
    u_xlat4.xy = vs_TEXCOORD0.xy * _Diss_Tex_ST.xy + _Diss_Tex_ST.zw;
    u_xlat4.xy = _Time.yy * _Diss_UV.xy + u_xlat4.xy;
    u_xlat1.xy = vs_TEXCOORD0.xy * _Turb_Noise_ST.xy + _Turb_Noise_ST.zw;
    u_xlat1.xy = _Time.yy * SrcVec2(_Turb_UV.x, _Turb_UV.y) + u_xlat1.xy;
    u_xlat16_12 = tex2D(_Turb_Noise, u_xlat1.xy).x;
    u_xlat4.xy = SrcVec2(u_xlat16_12) * SrcVec2(SrcVec2(_Turb_Diss_Value, _Turb_Diss_Value)) + u_xlat4.xy;
    u_xlat16_4 = tex2D(_Diss_Tex, u_xlat4.xy).x;
    u_xlat4.x = u_xlat16_4 + 1.0;
    u_xlat16_2.xy = (-SrcVec2(SrcVec2(_Diss_Soft_value, _Diss_Soft_value))) + SrcVec2(1.0, 2.0);
    u_xlat0.x = (-u_xlat0.x) * u_xlat16_2.y + u_xlat4.x;
    u_xlat16_2.x = float(1.0) / u_xlat16_2.x;
    u_xlat16_6 = u_xlat0.x + (-_Diss_Soft_value);
    u_xlat16_10 = u_xlat0.x + _EdgeWidth;
    u_xlat16_10 = u_xlat16_10 + (-_Diss_Soft_value);
    u_xlat16_10 = u_xlat16_2.x * u_xlat16_10;
    u_xlat16_10 = clamp(u_xlat16_10, 0.0, 1.0);
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_6;
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlat16_6 = u_xlat16_2.x * -2.0 + 3.0;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_2.x;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_6;
    u_xlat16_6 = u_xlat16_10 * -2.0 + 3.0;
    u_xlat16_10 = u_xlat16_10 * u_xlat16_10;
    u_xlat16_6 = u_xlat16_6 * u_xlat16_10 + (-u_xlat16_2.x);
    u_xlat16_2.x = _Diss_Edge_NO * u_xlat16_6 + u_xlat16_2.x;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Main_Tex_ST.xy + _Main_Tex_ST.zw;
    u_xlat1.xy = u_xlat0.xy + vs_TEXCOORD1.xy;
    u_xlat0.xy = _Time.yy * _Main_UV.xy + u_xlat0.xy;
    u_xlat1.xy = (-u_xlat0.xy) + u_xlat1.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Particle_SpeedUV, _Particle_SpeedUV)) * u_xlat1.xy + u_xlat0.xy;
    u_xlat0.xy = SrcVec2(u_xlat16_12) * SrcVec2(_Turb_MainTex_Value) + u_xlat0.xy;
    u_xlat16_1 = tex2D(_Main_Tex, u_xlat0.xy);
    u_xlat16_10 = (-u_xlat16_1.w) + u_xlat16_1.x;
    u_xlat16_10 = _Alpha_NO_R * u_xlat16_10 + u_xlat16_1.w;
    u_xlat16_3.xyz = u_xlat16_1.xyz * _Main_Color.xyz;
    u_xlat16_3.xyz = u_xlat16_3.xyz * SrcVec3(SrcVec3(_Brightness, _Brightness, _Brightness));
    u_xlat16_3.xyz = u_xlat16_3.xyz * vs_COLOR0.xyz;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_10;
    u_xlat16_2.x = u_xlat16_2.x * _Main_Color.w;
    u_xlat16_2.x = u_xlat16_2.x * _Alpha;
    u_xlat16_0.xy = tex2D(_Mask_Tex, vs_TEXCOORD2.xy).xw;
    u_xlat16_10 = (-u_xlat16_0.x) + u_xlat16_0.y;
    u_xlat16_10 = _Mask_A_NO_R * u_xlat16_10 + u_xlat16_0.x;
    u_xlat16_2.x = u_xlat16_10 * u_xlat16_2.x;
    u_xlat0.xy = vs_TEXCOORD0.xy * _Remap_Tex_ST.xy + _Remap_Tex_ST.zw;
    u_xlat0.xy = _Time.yy * SrcVec2(_Remap_UV.x, _Remap_UV.y) + u_xlat0.xy;
    u_xlat0.xy = SrcVec2(SrcVec2(_Turb_Remap_Value, _Turb_Remap_Value)) * SrcVec2(u_xlat16_12) + u_xlat0.xy;
    u_xlat16_0 = tex2D(_Remap_Tex, u_xlat0.xy);
    u_xlat16_10 = (-u_xlat16_0.w) + u_xlat16_0.x;
    u_xlat16_10 = _Remap_A_NO_R * u_xlat16_10 + u_xlat16_0.w;
    u_xlat16_2.x = u_xlat16_10 * u_xlat16_2.x;
    SV_Target0.w = u_xlat16_2.x * vs_COLOR0.w;
    u_xlat16_2.xzw = u_xlat16_0.xyz * u_xlat16_3.xyz;
    u_xlat16_3.xyz = (-u_xlat16_3.xyz) * u_xlat16_0.xyz + _Diss_Edge_color.xyz;
    SV_Target0.xyz = SrcVec3(u_xlat16_6) * u_xlat16_3.xyz + u_xlat16_2.xzw;
    return SV_Target0;
#endif
}
ENDCG
}
}
Fallback Off
}
