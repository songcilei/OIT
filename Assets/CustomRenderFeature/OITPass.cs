using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class OITPass : ScriptableRenderPass
{
    // private Material _mat;
    private Color _color;
    private float _intensity;
    private RTHandle _cameraColor;
    ShaderTagId _shaderTagId;
    private int _oitID = Shader.PropertyToID("_OITColorTexture");
    private int _oitAlphaID = Shader.PropertyToID("OITAlphaBuffer");
    private RenderTargetIdentifier _oitDepthID; 
    private Shader oitShader;

    public OITPass(Shader colorShader)
    {
        // _mat = mat;
        profilingSampler = new ProfilingSampler("OIT_Test");
        oitShader = colorShader;

    }

    //外部设置参数
    public void SetParameters(Color color, float intensity)
    {
        _color = color;
        _intensity = intensity;
        _shaderTagId = new ShaderTagId("UniversalForward");
    }
    
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        base.OnCameraSetup(cmd, ref renderingData);
        _cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;
        // ConfigureTarget(_cameraColor);
        // ConfigureClear(ClearFlag.All,Color.black);
        cmd.GetTemporaryRT(_oitID, new RenderTextureDescriptor(renderingData.cameraData.camera.pixelWidth, renderingData.cameraData.camera.pixelHeight,RenderTextureFormat.ARGB32, 0,0,RenderTextureReadWrite.Linear));
        RenderTargetIdentifier depth = renderingData.cameraData.renderer.cameraDepthTargetHandle;
        ConfigureTarget(_oitID);
        ConfigureClear(ClearFlag.Color, new Color(0,0,0,0));
    }


    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        base.Configure(cmd, cameraTextureDescriptor);
        
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        // if (_mat==null)
        // {
        //     return;
        // }

        var cmd = CommandBufferPool.Get("OITColorCmd");
    
        using (new ProfilingScope(cmd,profilingSampler))
        {
            
            // cmd.Blit(_cameraColor,_cameraColor,_mat,0);


            var cameraData = renderingData.cameraData;
            var drawSettings = CreateDrawingSettings(
                _shaderTagId,// shaderTag
                ref renderingData,// renderingData
                SortingCriteria.CommonTransparent// 排序
                );

            // Material oldMat = drawSettings.overrideMaterial;
            // oldMat.shader = oitShader;
            // Material mat = new Material(Shader.Find("Unlit/OITShader"));
            // drawSettings.overrideShader = oitShader;
            // drawSettings.overrideShaderPassIndex = 0;

            // drawSettings.overrideMaterial = mat;
            // drawSettings.overrideMaterialPassIndex = 0;
            
            var filterSettings = new FilteringSettings(
                RenderQueueRange.transparent,// 透明
                cameraData.camera.cullingMask// 遮罩
                );
            
            // context.BeginRenderPass();
            context.DrawRenderers(
                renderingData.cullResults,
                ref drawSettings,
                ref filterSettings
                );
            
            cmd.SetGlobalTexture(_oitID,_oitID);
            
        }
        
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }
}
