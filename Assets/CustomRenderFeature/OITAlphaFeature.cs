using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class OITAlphaFeature : ScriptableRendererFeature
{

    public Shader oitAlphaShader;
    private OITAlphaPass _oitAlphaPass;
    
    public override void Create()
    {
        _oitAlphaPass = new OITAlphaPass(oitAlphaShader);
        _oitAlphaPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType != CameraType.Game && renderingData.cameraData.cameraType != CameraType.SceneView) return;


        if (_oitAlphaPass == null)
        {
            return;
        }
        
        _oitAlphaPass.SetParameters(oitAlphaShader);
        
        _oitAlphaPass.ConfigureInput(ScriptableRenderPassInput.Color);
        
        
        renderer.EnqueuePass(_oitAlphaPass);
        
    }

    protected override void Dispose(bool disposing)
    {
    }
}
