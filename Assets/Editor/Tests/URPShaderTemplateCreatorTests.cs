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
        Assert.That(shader, Does.Contain("TransformObjectToHClip"));
        Assert.That(shader, Does.Contain("SAMPLE_TEXTURE2D(_BaseMap"));
    }

    [Test]
    public void LitPbrTemplateContainsPbrAndAdditionalLightSupport()
    {
        string shader = URPShaderTemplateCreator.CreateLitPbrTemplate("Custom/TestLitPBR");

        Assert.That(shader, Does.Contain("Shader \"Custom/TestLitPBR\""));
        Assert.That(shader, Does.Contain("#include \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl\""));
        Assert.That(shader, Does.Contain("LightingPhysicallyBased"));
        Assert.That(shader, Does.Contain("GetMainLight"));
        Assert.That(shader, Does.Contain("GetAdditionalLightsCount"));
        Assert.That(shader, Does.Contain("GetAdditionalLight"));
        Assert.That(shader, Does.Contain("_Metallic"));
        Assert.That(shader, Does.Contain("_Smoothness"));
    }
}
