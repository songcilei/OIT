using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class OITCombineFeature : ScriptableRendererFeature
{

    public Shader combineShader;
    private OITCombinePass _oitCombinePass;
    public override void Create()
    {
        _oitCombinePass = new OITCombinePass(combineShader);
        _oitCombinePass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType != CameraType.Game && renderingData.cameraData.cameraType != CameraType.SceneView) return;
        if (_oitCombinePass == null)
        {
            return;
        }
        

        
        //告诉pass 要读写颜色目标
        _oitCombinePass.ConfigureInput(ScriptableRenderPassInput.Color);
        renderer.EnqueuePass(_oitCombinePass);
    }
}
