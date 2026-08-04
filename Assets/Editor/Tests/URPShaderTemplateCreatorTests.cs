using NUnit.Framework;

public class URPShaderTemplateCreatorTests
{
    [Test]
    public void UnlitTemplateContainsUniversalForwardPass()
    {
        string shader = URPShaderTemplateCreator.CreateUnlitTemplate("Custom/TestUnlit");

        Assert.That(shader, Does.Contain("Shader \"Custom/TestUnlit\""));
        Assert.That(shader, Does.Contain("\"RenderPipeline\" = \"UniversalPipeline\""));
        Assert.That(shader, Does.Contain("\"LightMode\" = \"UniversalForward\""));
        Assert.That(shader, Does.Contain("#pragma multi_compile_instancing"));
        Assert.That(shader, Does.Contain("UNITY_VERTEX_INPUT_INSTANCE_ID"));
        Assert.That(shader, Does.Contain("UNITY_INSTANCING_BUFFER_START"));
        Assert.That(shader, Does.Contain("UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)"));
        Assert.That(shader, Does.Contain("UNITY_ACCESS_INSTANCED_PROP"));
        Assert.That(shader, Does.Contain("TransformObjectToHClip"));
        Assert.That(shader, Does.Contain("SAMPLE_TEXTURE2D(_BaseMap"));
    }

    [Test]
    public void LitPbrTemplateContainsPbrAndAdditionalLightSupport()
    {
        string shader = URPShaderTemplateCreator.CreateLitPbrTemplate("Custom/TestLitPBR");

        Assert.That(shader, Does.Contain("Shader \"Custom/TestLitPBR\""));
        Assert.That(shader, Does.Contain("#include \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl\""));
        Assert.That(shader, Does.Contain("#pragma multi_compile_instancing"));
        Assert.That(shader, Does.Contain("UNITY_TRANSFER_INSTANCE_ID"));
        Assert.That(shader, Does.Contain("UNITY_INSTANCING_BUFFER_START"));
        Assert.That(shader, Does.Contain("UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)"));
        Assert.That(shader, Does.Contain("UNITY_ACCESS_INSTANCED_PROP"));
        Assert.That(shader, Does.Contain("LightingPhysicallyBased"));
        Assert.That(shader, Does.Contain("GetMainLight"));
        Assert.That(shader, Does.Contain("GetAdditionalLightsCount"));
        Assert.That(shader, Does.Contain("GetAdditionalLight"));
        Assert.That(shader, Does.Contain("_Metallic"));
        Assert.That(shader, Does.Contain("_Smoothness"));
    }

    [Test]
    public void NormalDecalTemplateContainsDepthProjectionAndInstancedProperties()
    {
        string shader = URPShaderTemplateCreator.CreateNormalDecalTemplate("Custom/TestNormalDecal");

        Assert.That(shader, Does.Contain("Shader \"Custom/TestNormalDecal\""));
        Assert.That(shader, Does.Contain("\"LightMode\" = \"SRPDefaultUnlit\""));
        Assert.That(shader, Does.Contain("#include \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl\""));
        Assert.That(shader, Does.Contain("SampleSceneDepth"));
        Assert.That(shader, Does.Contain("ComputeWorldSpacePosition"));
        Assert.That(shader, Does.Contain("UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)"));
        Assert.That(shader, Does.Contain("UNITY_DEFINE_INSTANCED_PROP(float, _Opacity)"));
        Assert.That(shader, Does.Contain("UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)"));
        Assert.That(shader, Does.Contain("Blend SrcAlpha OneMinusSrcAlpha"));
    }

    [Test]
    public void OptimizeDecalTemplateContainsOptimizedDepthReconstructionAndFeatureSwitches()
    {
        string shader = URPShaderTemplateCreator.CreateOptimizeDecalTemplate("Custom/TestOptimizeDecal");

        Assert.That(shader, Does.Contain("Shader \"Custom/TestOptimizeDecal\""));
        Assert.That(shader, Does.Contain("Name \"OptimizeDepthDecal\""));
        Assert.That(shader, Does.Contain("\"Queue\" = \"Transparent-499\""));
        Assert.That(shader, Does.Contain("\"LightMode\" = \"UniversalForward\""));
        Assert.That(shader, Does.Contain("#pragma shader_feature_local_fragment _ProjectionAngleDiscardEnable"));
        Assert.That(shader, Does.Contain("#pragma shader_feature_local _UnityFogEnable"));
        Assert.That(shader, Does.Contain("#pragma shader_feature_local_fragment _SupportOrthographicCamera"));
        Assert.That(shader, Does.Contain("TEXTURE2D(_CameraDepthTexture)"));
        Assert.That(shader, Does.Contain("LinearEyeDepth(sceneRawDepth, _ZBufferParams)"));
        Assert.That(shader, Does.Contain("UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)"));
        Assert.That(shader, Does.Contain("UNITY_DEFINE_INSTANCED_PROP(float, _Opacity)"));
    }
}
