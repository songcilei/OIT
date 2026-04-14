using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class OITAlphaPass : ScriptableRenderPass
{
    private Shader _alphaShader;
    ShaderTagId _shaderTagId;
    int _oitAlphaID = Shader.PropertyToID("_OITAlphaTexture");
    private RTHandle _oitAlphaBuffer;
    
    public OITAlphaPass(Shader alphaShader)
    {
        _alphaShader = alphaShader;
        _shaderTagId = new ShaderTagId("UniversalForward");
    }
    
    public void SetParameters(Shader alphaShader)
    {
        
    }
    
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        base.OnCameraSetup(cmd, ref renderingData);
        
        // var cameraTextureDescriptor = renderingData.cameraData.cameraTargetDescriptor;
        // cameraTextureDescriptor.colorFormat = RenderTextureFormat.ARGBHalf;
        // cameraTextureDescriptor.depthBufferBits = 24;
        // RenderingUtils.ReAllocateIfNeeded(
        // ref _oitAlphaBuffer,
        // Vector2.one,
        // cameraTextureDescriptor,
        // FilterMode.Point,
        // TextureWrapMode.Clamp,
        // name: "_OITAlphaTexture"
        // );
        
        // _oitAlphaBuffer = RTHandles.Alloc(cameraTextureDescriptor, name: "_OITAlphaTexture");
        cmd.GetTemporaryRT(_oitAlphaID, new RenderTextureDescriptor(renderingData.cameraData.camera.pixelWidth, renderingData.cameraData.camera.pixelHeight,RenderTextureFormat.ARGBHalf, 24));

        // RenderTargetIdentifier color = renderingData.cameraData.renderer.cameraColorTargetHandle;
        RenderTargetIdentifier depth = renderingData.cameraData.renderer.cameraDepthTargetHandle;
        
        // cmd.SetRenderTarget(_oitAlphaID, depth);
        ConfigureTarget(_oitAlphaID,depth);
        
        ConfigureClear(ClearFlag.Color, Color.white);

        
    }


    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        base.OnCameraCleanup(cmd);
        // _oitAlphaBuffer.Release(); //如果在feature中调用的  直接给null即可 如果是非全局的 可以用.Release
    }


    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        
        var cmd = CommandBufferPool.Get("OITColorCmd");
        var cameraData = renderingData.cameraData;
        var drawSetting = CreateDrawingSettings(
                _shaderTagId,
                ref renderingData,
                SortingCriteria.CommonTransparent
            );

        drawSetting.overrideShader = _alphaShader;
        drawSetting.overrideShaderPassIndex = 0;
        
        var fliterSetting = new FilteringSettings(
            RenderQueueRange.transparent,
            cameraData.camera.cullingMask
            );

        context.DrawRenderers(
            renderingData.cullResults,
            ref drawSetting,
            ref fliterSetting
            );
        
        cmd.SetGlobalTexture("_OITAlphaTexture",_oitAlphaBuffer);
        
        
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
        
    }
    
    
    
}
