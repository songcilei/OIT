using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class OITFeature : ScriptableRendererFeature
{
    [Header("Color Tint Settings")]
    // public Shader tintShader;
    public Shader OitShader;

    public Color tintColor;
    [Range(0, 1)] 
    public float intensity = 0.5f;
    
    //内部pass
    private OITPass _oitPass;
    private Material _tintMaterial;
    
    
    //初始化 
    public override void Create()
    {
        // _tintMaterial = CoreUtils.CreateEngineMaterial(this.OitShader);
        _oitPass = new OITPass(OitShader);
        
        // 设置执行顺序
        _oitPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    
    //每帧把pass 添加渲染管线
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // 只对游戏相机生效 
        if (renderingData.cameraData.cameraType != CameraType.Game && renderingData.cameraData.cameraType != CameraType.SceneView) return;


        
        //
        if (_oitPass == null)
        {
            return;
        }
        
        //传参给pass
        _oitPass.SetParameters(tintColor, intensity);
        
        //告诉pass 要读写颜色目标
        _oitPass.ConfigureInput(ScriptableRenderPassInput.Color);
        renderer.EnqueuePass(_oitPass);
        
        
    }
    
    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_tintMaterial);
        _tintMaterial = null;
    }
}
