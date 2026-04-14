using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class OITCombinePass : ScriptableRenderPass
{

    private Shader _combineShader;
    private Material mat;
    public OITCombinePass(Shader combineShader)
    {
        _combineShader = combineShader;
        mat = new Material(_combineShader);
    }
    
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get("OITBuffer");
        using (new ProfilingScope(cmd, new ProfilingSampler("OITCombinePass")))
        {
            var cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;
            // renderingData.cameraData.
            // cmd.Blit(); 
            cmd.Blit(cameraColor,cameraColor, mat);
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}
